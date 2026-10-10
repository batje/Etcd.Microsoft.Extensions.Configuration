using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Etcd.Microsoft.Extensions.Configuration.Auth;

/// <summary>
/// Provides helpers to load PEM encoded certificates for etcd TLS connections.
/// </summary>
public static class CertificateLoader
{
	/// <summary>
	/// Loads a client certificate with its private key from PEM files.
	/// </summary>
	/// <param name="certificateFile">The path to the PEM encoded certificate (may contain the full chain, the first certificate is used).</param>
	/// <param name="keyFile">The path to the PEM encoded private key (PKCS#1, PKCS#8 or EC). When <c>null</c> the key is read from <paramref name="certificateFile"/>.</param>
	/// <exception cref="EtcdConfigurationException">The files can not be found or the certificate has no private key.</exception>
	public static X509Certificate2 LoadClientCertificate(string certificateFile, string? keyFile = null)
	{
		if (string.IsNullOrEmpty(certificateFile))
			throw new EtcdConfigurationException("Etcd client certificate file is not specified.");

		CheckFileExists(certificateFile, "client certificate");

		if (!string.IsNullOrEmpty(keyFile))
			CheckFileExists(keyFile!, "client key");

		X509Certificate2 certificate;

		try
		{
			using var pemCertificate = X509Certificate2.CreateFromPemFile(certificateFile, string.IsNullOrEmpty(keyFile) ? null : keyFile);

			// Certificates created from PEM files have an ephemeral key, which SslStream on Windows can not use.
			// Round-tripping through PKCS#12 gives a certificate that works on every platform.
			certificate = LoadPkcs12(pemCertificate.Export(X509ContentType.Pkcs12));
		}
		catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or ArgumentException)
		{
			throw new EtcdConfigurationException($"Unable to load etcd client certificate from `{certificateFile}`" +
				(string.IsNullOrEmpty(keyFile) ? string.Empty : $" with key `{keyFile}`") + $": {e.Message}");
		}

		if (!certificate.HasPrivateKey)
			throw new EtcdConfigurationException($"Etcd client certificate `{certificateFile}` has no private key.");

		return certificate;
	}

	/// <summary>
	/// Loads one or more CA certificates from a PEM file.
	/// </summary>
	/// <param name="caCertificateFile">The path to the PEM encoded CA certificate(s).</param>
	/// <exception cref="EtcdConfigurationException">The file can not be found or contains no certificates.</exception>
	public static X509Certificate2Collection LoadCaCertificates(string caCertificateFile)
	{
		if (string.IsNullOrEmpty(caCertificateFile))
			throw new EtcdConfigurationException("Etcd CA certificate file is not specified.");

		CheckFileExists(caCertificateFile, "CA certificate");

		var collection = new X509Certificate2Collection();

		try
		{
			collection.ImportFromPemFile(caCertificateFile);
		}
		catch (System.Security.Cryptography.CryptographicException e)
		{
			throw new EtcdConfigurationException($"Unable to load etcd CA certificate from `{caCertificateFile}`: {e.Message}");
		}

		if (collection.Count == 0)
			throw new EtcdConfigurationException($"Etcd CA certificate file `{caCertificateFile}` contains no certificates.");

		return collection;
	}

	/// <summary>
	/// Gets the common name (CN) of the certificate subject.
	/// </summary>
	/// <param name="certificate">The certificate.</param>
	/// <returns>The common name, or <c>null</c> when the subject has no common name.</returns>
	public static string? GetCommonName(X509Certificate2 certificate)
	{
		ArgumentNullException.ThrowIfNull(certificate);

		var commonName = certificate.GetNameInfo(X509NameType.SimpleName, false);

		return string.IsNullOrEmpty(commonName) ? null : commonName;
	}

	private static void CheckFileExists(string path, string description)
	{
		if (!File.Exists(path))
			throw new EtcdConfigurationException($"Etcd {description} file `{path}` not found.");
	}

	private static X509Certificate2 LoadPkcs12(byte[] data) =>
#if NET9_0_OR_GREATER
		X509CertificateLoader.LoadPkcs12(data, null);
#else
		new(data);
#endif
}
