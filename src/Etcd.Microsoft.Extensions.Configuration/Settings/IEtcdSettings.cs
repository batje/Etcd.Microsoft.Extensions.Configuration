namespace Etcd.Microsoft.Extensions.Configuration.Settings;

/// <summary>
/// Represents etcd settings
/// </summary>
public interface IEtcdSettings
{
	/// <summary>
	/// Gets the connection string.
	/// </summary>
	/// <value>
	/// The connection string.
	/// </value>
	string? ConnectionString { get; }

	/// <summary>
	/// Gets the path to a PEM file with the CA certificate(s) used to validate the etcd server certificate.
	/// When <c>null</c>, the server certificate is validated against the system certificate store.
	/// </summary>
	/// <value>
	/// The CA certificate file path.
	/// </value>
	string? CaCertificateFile => null;
}