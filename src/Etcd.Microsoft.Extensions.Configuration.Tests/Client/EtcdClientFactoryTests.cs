using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Etcd.Microsoft.Extensions.Configuration.Auth;
using Etcd.Microsoft.Extensions.Configuration.Client;
using Etcd.Microsoft.Extensions.Configuration.Settings;
using Etcd.Microsoft.Extensions.Configuration.Tests.Auth;
using NUnit.Framework;

#nullable enable

namespace Etcd.Microsoft.Extensions.Configuration.Tests.Client;

[TestFixture]
public class EtcdClientFactoryTests
{
	private TestCertificates _certificates = null!;

	[OneTimeSetUp]
	public void OneTimeSetUp() => _certificates = new TestCertificates();

	[OneTimeTearDown]
	public void OneTimeTearDown() => _certificates.Dispose();

	[Test]
	public void Ctor_SettingsWithCaFile_CaFileKept()
	{
		// Act
		var factory = new EtcdClientFactory(new EtcdSettings("https://localhost:2379", _certificates.CaFile));

		// Assert
		Assert.That(factory.Settings.CaCertificateFile, Is.EqualTo(_certificates.CaFile));
		Assert.That(factory.ClientCertificate, Is.Null);
	}

	[Test]
	public void Create_ClientCertificateWithHttp_Throws()
	{
		// Arrange
		var credentials = CertificateCredentials.FromPemFiles(_certificates.ClientCertificateFile, _certificates.ClientKeyFile);
		var factory = new EtcdClientFactory(new EtcdSettings("http://localhost:2379"), credentials.ClientCertificate);

		// Act & Assert
		var e = Assert.Throws<EtcdConfigurationException>(() => factory.Create());
		Assert.That(e!.Message, Does.Contain("https"));
	}

	[Test]
	public void Create_ClientCertificateWithHttps_ClientCreated()
	{
		// Arrange
		var credentials = CertificateCredentials.FromPemFiles(_certificates.ClientCertificateFile, _certificates.ClientKeyFile);
		var factory = new EtcdClientFactory(new EtcdSettings("https://localhost:2379", _certificates.CaFile), credentials.ClientCertificate);

		// Act
		var client = factory.Create();

		// Assert
		Assert.That(client, Is.Not.Null);
		(client as System.IDisposable)?.Dispose();
	}

	[Test]
	public void ConfigureSslOptions_ClientCertificate_Presented()
	{
		// Arrange
		var options = new SslClientAuthenticationOptions();

		// Act
		EtcdClientFactory.ConfigureSslOptions(options, _certificates.Client, null);

		// Assert
		Assert.That(options.ClientCertificates, Has.Count.EqualTo(1));
		Assert.That(options.ClientCertificates![0], Is.SameAs(_certificates.Client));
		Assert.That(options.RemoteCertificateValidationCallback, Is.Null);
	}

	[Test]
	public void ValidateServerCertificate_SignedByCustomCa_Accepted()
	{
		// Arrange
		var ca = new X509Certificate2Collection(_certificates.Ca);

		// Act & Assert
		Assert.That(EtcdClientFactory.ValidateServerCertificate(_certificates.Server, SslPolicyErrors.RemoteCertificateChainErrors, ca), Is.True);
	}

	[Test]
	public void ValidateServerCertificate_SignedByOtherCa_Rejected()
	{
		// Arrange
		var ca = new X509Certificate2Collection(_certificates.OtherCa);

		// Act & Assert
		Assert.That(EtcdClientFactory.ValidateServerCertificate(_certificates.Server, SslPolicyErrors.RemoteCertificateChainErrors, ca), Is.False);
	}

	[Test]
	public void ValidateServerCertificate_NameMismatch_Rejected()
	{
		// Arrange
		var ca = new X509Certificate2Collection(_certificates.Ca);

		// Act & Assert
		Assert.That(EtcdClientFactory.ValidateServerCertificate(_certificates.Server,
			SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors, ca), Is.False);
	}

	[Test]
	public void ValidateServerCertificate_NoErrors_Accepted()
	{
		// Act & Assert
		Assert.That(EtcdClientFactory.ValidateServerCertificate(_certificates.Server, SslPolicyErrors.None, []), Is.True);
	}
}
