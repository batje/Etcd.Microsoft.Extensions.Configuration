using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using dotnet_etcd;
using dotnet_etcd.interfaces;
using Etcd.Microsoft.Extensions.Configuration.Auth;
using Etcd.Microsoft.Extensions.Configuration.Settings;
using Grpc.Core;

namespace Etcd.Microsoft.Extensions.Configuration.Client;

/// <summary>
/// Provides etcd client factory
/// </summary>
public class EtcdClientFactory : IEtcdClientFactory
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EtcdClientFactory" /> class.
	/// </summary>
	/// <param name="settings">The settings.</param>
	/// <exception cref="ArgumentNullException">settings</exception>
	public EtcdClientFactory(IEtcdSettings? settings = null)
	{
		var environmentSetting = EnvironmentSettingsFactory.Create();

		Settings = new EtcdSettings(settings?.ConnectionString ?? environmentSetting.ConnectionString, settings?.CaCertificateFile);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EtcdClientFactory" /> class.
	/// </summary>
	/// <param name="settings">The settings.</param>
	/// <param name="clientCertificate">The TLS client certificate (with private key) presented to the etcd server.</param>
	public EtcdClientFactory(IEtcdSettings? settings, X509Certificate2? clientCertificate)
		: this(settings) =>
		ClientCertificate = clientCertificate;

	/// <summary>
	/// Gets the settings.
	/// </summary>
	/// <value>
	/// The settings.
	/// </value>
	public IEtcdSettings Settings { get; }

	/// <summary>
	/// Gets the TLS client certificate presented to the etcd server, if any.
	/// </summary>
	public X509Certificate2? ClientCertificate { get; }

	/// <summary>
	/// Creates the etcd client instance.
	/// </summary>
	/// <returns></returns>
	public IEtcdClient Create()
	{
		if (string.IsNullOrEmpty(Settings.ConnectionString))
			throw new EtcdConfigurationException("Connection string is missing, should be passed in AddEtcd parameters or set in environment variables.");

		var isHttps = Settings.ConnectionString!.StartsWith("https", StringComparison.OrdinalIgnoreCase);

		if (ClientCertificate == null && Settings.CaCertificateFile == null)
			return new EtcdClient(Settings.ConnectionString, configureChannelOptions: (options =>
			{
				options.Credentials = isHttps
					? ChannelCredentials.SecureSsl
					: ChannelCredentials.Insecure;
			}));

		if (!isHttps)
			throw new EtcdConfigurationException("A client certificate or CA certificate is configured, but the etcd connection string does not use https.");

		var caCertificates = Settings.CaCertificateFile != null
			? CertificateLoader.LoadCaCertificates(Settings.CaCertificateFile)
			: null;

		return new EtcdClient(Settings.ConnectionString, options => ConfigureSslOptions(options, ClientCertificate, caCertificates));
	}

	/// <summary>
	/// Configures the TLS options: presents the client certificate and validates the server against the custom CA, if provided.
	/// </summary>
	/// <param name="options">The SSL options.</param>
	/// <param name="clientCertificate">The client certificate.</param>
	/// <param name="caCertificates">The trusted CA certificates.</param>
	internal static void ConfigureSslOptions(SslClientAuthenticationOptions options,
		X509Certificate2? clientCertificate,
		X509Certificate2Collection? caCertificates)
	{
		if (clientCertificate != null)
		{
			options.ClientCertificates = [clientCertificate];
			options.LocalCertificateSelectionCallback = (_, _, _, _, _) => clientCertificate;
		}

		if (caCertificates != null)
			options.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
				ValidateServerCertificate(certificate, errors, caCertificates);
	}

	/// <summary>
	/// Validates the server certificate against a custom set of trusted CA certificates.
	/// Host name mismatches are never accepted.
	/// </summary>
	/// <param name="certificate">The server certificate.</param>
	/// <param name="errors">The errors found by the default validation.</param>
	/// <param name="caCertificates">The trusted CA certificates.</param>
	internal static bool ValidateServerCertificate(X509Certificate? certificate, SslPolicyErrors errors, X509Certificate2Collection caCertificates)
	{
		if (errors == SslPolicyErrors.None)
			return true;

		// Only chain errors can be fixed by the custom CA, name mismatches or missing certificates can not.
		if (certificate == null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
			return false;

		using var chain = new X509Chain();

		chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
		chain.ChainPolicy.CustomTrustStore.AddRange(caCertificates);
		chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

		return chain.Build(certificate as X509Certificate2 ?? new X509Certificate2(certificate));
	}
}
