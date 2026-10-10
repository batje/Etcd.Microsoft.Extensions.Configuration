using System;
using System.Security.Cryptography.X509Certificates;

namespace Etcd.Microsoft.Extensions.Configuration.Auth;

/// <summary>
/// Provides TLS client certificate credentials.
/// The etcd server authenticates the client using the common name (CN) of the certificate,
/// see https://etcd.io/docs/latest/op-guide/authentication/rbac/#using-tls-common-name.
/// No password and no authentication token are used.
/// </summary>
/// <seealso cref="ICredentials" />
public class CertificateCredentials : ICredentials
{
	private const string DefaultCertificateFileEnvironmentVariableName = "ETCD_CLIENT_CERTIFICATE_FILE";
	private const string DefaultKeyFileEnvironmentVariableName = "ETCD_CLIENT_KEY_FILE";

	/// <summary>
	/// Initializes a new instance of the <see cref="CertificateCredentials"/> class.
	/// </summary>
	/// <param name="clientCertificate">The client certificate, including its private key.</param>
	/// <param name="information">The information about the credentials.</param>
	/// <exception cref="ArgumentNullException">clientCertificate</exception>
	/// <exception cref="ArgumentException">The certificate has no private key or no common name.</exception>
	public CertificateCredentials(X509Certificate2 clientCertificate, string? information = null)
	{
		ArgumentNullException.ThrowIfNull(clientCertificate);

		if (!clientCertificate.HasPrivateKey)
			throw new ArgumentException("The etcd client certificate has no private key.", nameof(clientCertificate));

		UserName = CertificateLoader.GetCommonName(clientCertificate)
			?? throw new ArgumentException("The etcd client certificate subject has no common name (CN).", nameof(clientCertificate));

		ClientCertificate = clientCertificate;
		Information = information;
	}

	/// <summary>
	/// Gets the client certificate.
	/// </summary>
	public X509Certificate2 ClientCertificate { get; }

	/// <summary>
	/// Gets the name of the user. This is the common name (CN) of the client certificate, which etcd uses as user name.
	/// </summary>
	public string UserName { get; }

	/// <summary>
	/// Gets the password. Always empty, certificate authentication does not use passwords.
	/// </summary>
	public string Password => string.Empty;

	/// <summary>
	/// Gets the information about the credentials.
	/// </summary>
	public string? Information { get; }

	/// <summary>
	/// Gets the string representation of the credentials.
	/// </summary>
	override public string ToString() => Information ?? $"etcd client certificate credentials (CN={UserName})";

	/// <summary>
	/// Creates certificate credentials from PEM files.
	/// </summary>
	/// <param name="certificateFile">The path to the PEM encoded client certificate.</param>
	/// <param name="keyFile">The path to the PEM encoded private key. When <c>null</c> the key is read from <paramref name="certificateFile"/>.</param>
	/// <exception cref="EtcdConfigurationException">The certificate can not be loaded.</exception>
	public static CertificateCredentials FromPemFiles(string certificateFile, string? keyFile = null) =>
		new(CertificateLoader.LoadClientCertificate(certificateFile, keyFile),
			$"etcd client certificate credentials from file `{certificateFile}`");

	/// <summary>
	/// Creates certificate credentials from PEM files whose paths are read from environment variables.
	/// </summary>
	/// <param name="certificateFileEnvironmentVariableName">The name of the environment variable holding the client certificate path.</param>
	/// <param name="keyFileEnvironmentVariableName">The name of the environment variable holding the private key path.</param>
	/// <exception cref="EtcdException">The certificate file environment variable is not set.</exception>
	/// <exception cref="EtcdConfigurationException">The certificate can not be loaded.</exception>
	public static CertificateCredentials FromEnvironmentVariables(
		string certificateFileEnvironmentVariableName = DefaultCertificateFileEnvironmentVariableName,
		string keyFileEnvironmentVariableName = DefaultKeyFileEnvironmentVariableName)
	{
		var certificateFile = Environment.GetEnvironmentVariable(certificateFileEnvironmentVariableName);
		var keyFile = Environment.GetEnvironmentVariable(keyFileEnvironmentVariableName);

		if (string.IsNullOrEmpty(certificateFile))
			throw new EtcdException($"Etcd client certificate file is not found in the environment variable `{certificateFileEnvironmentVariableName}`.");

		return new(CertificateLoader.LoadClientCertificate(certificateFile, keyFile),
			$"etcd client certificate credentials from environment variables ({certificateFileEnvironmentVariableName}, {keyFileEnvironmentVariableName})");
	}
}
