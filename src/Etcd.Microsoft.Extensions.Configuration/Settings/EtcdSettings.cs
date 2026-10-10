namespace Etcd.Microsoft.Extensions.Configuration.Settings;

/// <summary>
/// Provides etcd settings
/// </summary>
public class EtcdSettings : IEtcdSettings
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EtcdSettings" /> class.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	public EtcdSettings(string? connectionString) => ConnectionString = connectionString;

	/// <summary>
	/// Initializes a new instance of the <see cref="EtcdSettings" /> class.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	/// <param name="caCertificateFile">The path to a PEM file with the CA certificate(s) used to validate the etcd server certificate.</param>
	public EtcdSettings(string? connectionString, string? caCertificateFile)
	{
		ConnectionString = connectionString;
		CaCertificateFile = string.IsNullOrEmpty(caCertificateFile) ? null : caCertificateFile;
	}

	/// <summary>
	/// Gets the connection string.
	/// </summary>
	/// <value>
	/// The connection string.
	/// </value>
	public string? ConnectionString { get; }

	/// <summary>
	/// Gets the path to a PEM file with the CA certificate(s) used to validate the etcd server certificate.
	/// </summary>
	/// <value>
	/// The CA certificate file path.
	/// </value>
	public string? CaCertificateFile { get; }
}