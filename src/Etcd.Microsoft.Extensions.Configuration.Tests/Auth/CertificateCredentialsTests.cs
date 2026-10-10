using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Etcd.Microsoft.Extensions.Configuration.Auth;
using NUnit.Framework;

#nullable enable

namespace Etcd.Microsoft.Extensions.Configuration.Tests.Auth;

[TestFixture]
public class CertificateCredentialsTests
{
	private TestCertificates _certificates = null!;

	[OneTimeSetUp]
	public void OneTimeSetUp() => _certificates = new TestCertificates();

	[OneTimeTearDown]
	public void OneTimeTearDown() => _certificates.Dispose();

	[Test]
	public void FromPemFiles_SeparateKeyFile_UserNameIsCommonName()
	{
		// Act
		var credentials = CertificateCredentials.FromPemFiles(_certificates.ClientCertificateFile, _certificates.ClientKeyFile);

		// Assert
		Assert.That(credentials.UserName, Is.EqualTo("MyCertUser"));
		Assert.That(credentials.Password, Is.Empty);
		Assert.That(credentials.ClientCertificate.HasPrivateKey, Is.True);
		Assert.That(credentials.ToString(), Does.Contain(_certificates.ClientCertificateFile));
	}

	[Test]
	public void FromPemFiles_CombinedFile_Loaded()
	{
		// Act
		var credentials = CertificateCredentials.FromPemFiles(_certificates.ClientCombinedFile);

		// Assert
		Assert.That(credentials.UserName, Is.EqualTo("MyCertUser"));
		Assert.That(credentials.ClientCertificate.HasPrivateKey, Is.True);
	}

	[Test]
	public void FromPemFiles_MissingCertificateFile_Throws()
	{
		// Act & Assert
		var e = Assert.Throws<EtcdConfigurationException>(() =>
			CertificateCredentials.FromPemFiles(Path.Combine(_certificates.Folder, "missing.crt"), _certificates.ClientKeyFile));

		Assert.That(e!.Message, Does.Contain("not found"));
	}

	[Test]
	public void FromPemFiles_MissingKeyFile_Throws()
	{
		// Act & Assert
		var e = Assert.Throws<EtcdConfigurationException>(() =>
			CertificateCredentials.FromPemFiles(_certificates.ClientCertificateFile, Path.Combine(_certificates.Folder, "missing.key")));

		Assert.That(e!.Message, Does.Contain("client key"));
	}

	[Test]
	public void FromPemFiles_CertificateWithoutKey_Throws()
	{
		// Act & Assert
		Assert.Throws<EtcdConfigurationException>(() => CertificateCredentials.FromPemFiles(_certificates.ClientCertificateFile));
	}

	[Test]
	public void FromEnvironmentVariables_VariablesSet_Loaded()
	{
		// Arrange
		Environment.SetEnvironmentVariable("ETCD_TEST_CERT_FILE", _certificates.ClientCertificateFile);
		Environment.SetEnvironmentVariable("ETCD_TEST_KEY_FILE", _certificates.ClientKeyFile);

		try
		{
			// Act
			var credentials = CertificateCredentials.FromEnvironmentVariables("ETCD_TEST_CERT_FILE", "ETCD_TEST_KEY_FILE");

			// Assert
			Assert.That(credentials.UserName, Is.EqualTo("MyCertUser"));
			Assert.That(credentials.ToString(), Does.Contain("ETCD_TEST_CERT_FILE"));
		}
		finally
		{
			Environment.SetEnvironmentVariable("ETCD_TEST_CERT_FILE", null);
			Environment.SetEnvironmentVariable("ETCD_TEST_KEY_FILE", null);
		}
	}

	[Test]
	public void FromEnvironmentVariables_VariableMissing_Throws()
	{
		// Act & Assert
		Assert.Throws<EtcdException>(() => CertificateCredentials.FromEnvironmentVariables("ETCD_TEST_NOT_EXISTING_1", "ETCD_TEST_NOT_EXISTING_2"));
	}

	[Test]
	public void Ctor_CertificateWithoutPrivateKey_Throws()
	{
		// Arrange
		using var publicOnly = X509CertificateLoader.LoadCertificate(_certificates.Client.RawData);

		// Act & Assert
		Assert.Throws<ArgumentException>(() => _ = new CertificateCredentials(publicOnly));
	}

	[Test]
	public void Ctor_CertificateWithoutCommonName_Throws()
	{
		// Arrange
		using var key = RSA.Create(2048);
		var request = new CertificateRequest("O=No common name", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

		// Act & Assert
		var e = Assert.Throws<ArgumentException>(() => _ = new CertificateCredentials(certificate));
		Assert.That(e!.Message, Does.Contain("common name"));
	}

	[Test]
	public void LoadCaCertificates_ValidFile_Loaded()
	{
		// Act
		var collection = CertificateLoader.LoadCaCertificates(_certificates.CaFile);

		// Assert
		Assert.That(collection, Has.Count.EqualTo(1));
		Assert.That(collection[0].Thumbprint, Is.EqualTo(_certificates.Ca.Thumbprint));
	}

	[Test]
	public void LoadCaCertificates_EmptyFile_Throws()
	{
		// Arrange
		var emptyFile = Path.Combine(_certificates.Folder, "empty.crt");
		File.WriteAllText(emptyFile, string.Empty);

		// Act & Assert
		Assert.Throws<EtcdConfigurationException>(() => CertificateLoader.LoadCaCertificates(emptyFile));
	}
}
