using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Etcd.Microsoft.Extensions.Configuration.Tests.Auth;

/// <summary>
/// Creates a throw-away CA, server and client certificates as PEM files in a temporary folder.
/// </summary>
public sealed class TestCertificates : IDisposable
{
	public TestCertificates(string clientCommonName = "MyCertUser")
	{
		Folder = Path.Combine(Path.GetTempPath(), "etcd-cert-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Folder);

		using var caKey = RSA.Create(2048);
		var caRequest = new CertificateRequest("CN=Test etcd CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
		caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
		Ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

		Server = CreateSigned("CN=localhost", true);
		Client = CreateSigned($"CN={clientCommonName}", false);

		using var otherCaKey = RSA.Create(2048);
		var otherCaRequest = new CertificateRequest("CN=Other CA", otherCaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		otherCaRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
		OtherCa = otherCaRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

		CaFile = Write("ca.crt", Ca.ExportCertificatePem());
		OtherCaFile = Write("other-ca.crt", OtherCa.ExportCertificatePem());
		ClientCertificateFile = Write("client.crt", Client.ExportCertificatePem());
		ClientKeyFile = Write("client.key", Client.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem());
		ClientCombinedFile = Write("client-combined.pem", Client.ExportCertificatePem() + "\n" + Client.GetRSAPrivateKey()!.ExportRSAPrivateKeyPem());
	}

	public string Folder { get; }
	public X509Certificate2 Ca { get; }
	public X509Certificate2 OtherCa { get; }
	public X509Certificate2 Server { get; }
	public X509Certificate2 Client { get; }
	public string CaFile { get; }
	public string OtherCaFile { get; }
	public string ClientCertificateFile { get; }
	public string ClientKeyFile { get; }
	public string ClientCombinedFile { get; }

	public void Dispose()
	{
		Ca.Dispose();
		OtherCa.Dispose();
		Server.Dispose();
		Client.Dispose();

		if (Directory.Exists(Folder))
			Directory.Delete(Folder, true);
	}

	private X509Certificate2 CreateSigned(string subject, bool isServer)
	{
		using var key = RSA.Create(2048);
		var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
		request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
			[new Oid(isServer ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2")], false));

		if (isServer)
		{
			var san = new SubjectAlternativeNameBuilder();
			san.AddDnsName("localhost");
			request.CertificateExtensions.Add(san.Build());
		}

		var serial = new byte[16];
		RandomNumberGenerator.Fill(serial);
		serial[0] &= 0x7F;

		using var signed = request.Create(Ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(10), serial);

		return signed.CopyWithPrivateKey(key);
	}

	private string Write(string name, string content)
	{
		var path = Path.Combine(Folder, name);
		File.WriteAllText(path, content);

		return path;
	}
}
