using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Etcd.Microsoft.Extensions.Configuration.Auth;
using Etcd.Microsoft.Extensions.Configuration.Settings;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

#nullable enable

namespace Etcd.Microsoft.Extensions.Configuration.IntegrationTests;

/// <summary>
/// Integration tests for TLS client certificate (common name) authentication.
/// Requires an etcd https listener with client certificate authentication and the certificates
/// created by tests/terraform/tls.tf. See README.md, section "Testing with TLS client certificates".
/// </summary>
[TestFixture]
[Category("Integration")]
public class CertificateAuthenticationTests
{
	private const string ConnectionStringEnvironmentVariableName = "ETCD_TLS_TEST_CONNECTION_STRING";
	private const string CertificatesFolderEnvironmentVariableName = "ETCD_TLS_TEST_CERTS_FOLDER";
	private const string DefaultConnectionString = "https://localhost:2378";

	private string _connectionString = null!;
	private string _certificatesFolder = null!;

	private string CaFile => Path.Combine(_certificatesFolder, "ca.crt");
	private string ClientCertificateFile => Path.Combine(_certificatesFolder, "client.crt");
	private string ClientKeyFile => Path.Combine(_certificatesFolder, "client.key");

	[OneTimeSetUp]
	public void OneTimeSetUp()
	{
		_connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName) ?? DefaultConnectionString;

		var folder = Environment.GetEnvironmentVariable(CertificatesFolderEnvironmentVariableName) ?? FindCertificatesFolder();

		if (folder == null || !File.Exists(Path.Combine(folder, "client.crt")))
			Assert.Ignore("TLS test certificates not found. Run `terraform apply` in tests/terraform and configure the etcd https listener, " +
				$"or set {CertificatesFolderEnvironmentVariableName}. See README.md, section \"Testing with TLS client certificates\".");

		_certificatesFolder = folder!;
	}

	[Test]
	public void Build_WithClientCertificate_ValuesLoaded()
	{
		// Arrange

		var credentials = CertificateCredentials.FromPemFiles(ClientCertificateFile, ClientKeyFile);
		var etcdSettings = new EtcdSettings(_connectionString, CaFile);

		// Act

		var config = new ConfigurationBuilder()
			.AddEtcd(credentials, etcdSettings)
			.AddEtcd(credentials, etcdSettings, "MyPrefix")
			.AddEtcd(credentials, etcdSettings, "MYCOMPLEX/prefix", "/")
			.Build();

		// Assert

		Assert.That(credentials.UserName, Is.EqualTo("MyCertUser"));
		ConfigurationBuilderTests.PerformTest(config);
	}

	[Test]
	public void Build_WithClientCertificateFromEnvironment_ValuesLoaded()
	{
		// Arrange

		Environment.SetEnvironmentVariable("ETCD_TEST_CLIENT_CERTIFICATE_FILE", ClientCertificateFile);
		Environment.SetEnvironmentVariable("ETCD_TEST_CLIENT_KEY_FILE", ClientKeyFile);

		try
		{
			var credentials = CertificateCredentials.FromEnvironmentVariables("ETCD_TEST_CLIENT_CERTIFICATE_FILE", "ETCD_TEST_CLIENT_KEY_FILE");
			var etcdSettings = new EtcdSettings(_connectionString, CaFile);

			// Act

			var config = new ConfigurationBuilder()
				.AddEtcd(credentials, etcdSettings)
				.AddEtcd(credentials, etcdSettings, "MyPrefix")
				.AddEtcd(credentials, etcdSettings, "MYCOMPLEX/prefix", "/")
				.Build();

			// Assert

			ConfigurationBuilderTests.PerformTest(config);
		}
		finally
		{
			Environment.SetEnvironmentVariable("ETCD_TEST_CLIENT_CERTIFICATE_FILE", null);
			Environment.SetEnvironmentVariable("ETCD_TEST_CLIENT_KEY_FILE", null);
		}
	}

	[Test]
	public void Build_WithSettingsFromJsonConfiguration_ValuesLoaded()
	{
		// Arrange

		var jsonConfig = new ConfigurationBuilder()
			.AddInMemoryCollection(new[]
			{
				new System.Collections.Generic.KeyValuePair<string, string?>("EtcdSettings:ConnectionString", _connectionString),
				new System.Collections.Generic.KeyValuePair<string, string?>("EtcdSettings:CaCertificateFile", CaFile)
			})
			.Build();

		var credentials = CertificateCredentials.FromPemFiles(ClientCertificateFile, ClientKeyFile);
		var etcdSettings = new ConfigurationBasedEtcdSettings(jsonConfig);

		// Act

		var config = new ConfigurationBuilder()
			.AddEtcd(credentials, etcdSettings, "MyPrefix")
			.Build();

		// Assert

		Assert.That(config.GetSection("TestAppSection")["Item1"], Is.EqualTo("1234321"));
	}

	[Test]
	public void Build_WithCertificateOfUnknownUser_Throws()
	{
		// Arrange

		var credentials = CertificateCredentials.FromPemFiles(
			Path.Combine(_certificatesFolder, "unknown-client.crt"),
			Path.Combine(_certificatesFolder, "unknown-client.key"));

		var etcdSettings = new EtcdSettings(_connectionString, CaFile);

		// Act & Assert

		Assert.That(() => new ConfigurationBuilder()
				.AddEtcd(credentials, etcdSettings, "MyPrefix")
				.Build(),
			Throws.InstanceOf<EtcdException>());
	}

	[Test]
	public void Build_WithUntrustedServerCa_Throws()
	{
		// Arrange

		var untrustedCaFile = Path.Combine(Path.GetTempPath(), $"etcd-untrusted-ca-{Guid.NewGuid():N}.crt");

		using (var key = RSA.Create(2048))
		{
			var request = new CertificateRequest("CN=Untrusted CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
			using var untrustedCa = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
			File.WriteAllText(untrustedCaFile, untrustedCa.ExportCertificatePem());
		}

		try
		{
			var credentials = CertificateCredentials.FromPemFiles(ClientCertificateFile, ClientKeyFile);
			var etcdSettings = new EtcdSettings(_connectionString, untrustedCaFile);

			// Act & Assert

			Assert.That(() => new ConfigurationBuilder()
					.AddEtcd(credentials, etcdSettings, "MyPrefix")
					.Build(),
				Throws.InstanceOf<EtcdException>());
		}
		finally
		{
			File.Delete(untrustedCaFile);
		}
	}

	private static string? FindCertificatesFolder()
	{
		var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "tests", "certs");

			if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate).Any())
				return candidate;

			directory = directory.Parent;
		}

		return null;
	}
}
