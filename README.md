# Etcd.Microsoft.Extensions.Configuration

[![NuGet Version](https://img.shields.io/nuget/v/Etcd.Microsoft.Extensions.Configuration)](https://www.nuget.org/packages/Etcd.Microsoft.Extensions.Configuration/)
[![NuGet Download](https://img.shields.io/nuget/dt/Etcd.Microsoft.Extensions.Configuration)](https://www.nuget.org/packages/Etcd.Microsoft.Extensions.Configuration/)
[![Build Package](https://github.com/SimplifyNet/Etcd.Microsoft.Extensions.Configuration/actions/workflows/build.yml/badge.svg)](https://github.com/SimplifyNet/Etcd.Microsoft.Extensions.Configuration/actions/workflows/build.yml)
[![Libraries.io dependency status for latest release](https://img.shields.io/librariesio/release/nuget/Etcd.Microsoft.Extensions.Configuration)](https://libraries.io/nuget/Etcd.Microsoft.Extensions.Configuration)
[![CodeFactor Grade](https://img.shields.io/codefactor/grade/github/SimplifyNet/Etcd.Microsoft.Extensions.Configuration)](https://www.codefactor.io/repository/github/simplifynet/Etcd.Microsoft.Extensions.Configuration)
![Platform](https://img.shields.io/badge/platform-.NET%2010.0%20%7C%20.NET%209.0%20%7C%20.NET%208.0-lightgrey)

Etcd-based configuration provider for Microsoft.Extensions.Configuration.

> Keep in mind that starting with v2, on Windows it requires Windows 10 or Windows Server 2016 (or later) because the internal gRPC client uses the HTTP/2 protocol for communication (<https://docs.microsoft.com/en-us/answers/questions/310032/2012-r2-net-core-http2.html>).

## Quick Start

### HTTP

```csharp
var config = new ConfigurationBuilder()
  .AddEtcd(
    new Credentials("MyEtcdUserName", "passw"),
    new EtcdSettings("http://serveraddress:2379"))
  .Build();

var mySection = config.GetSection("MySection");
var myKeyValue = mySection["MyKeyName"];
```

### HTTPS

When using HTTPS, a *.crt CA certificate (provided by the etcd administrator) should be placed in a well-known system certificate store (depending on the OS).

* For Windows, it is `Trusted Root Certification Authority`.
* For Linux, at least for Arch/Manjaro Linux, it should be placed in `/etc/ca-certificates/trust-source/anchors/`, followed by the `sudo trust extract-compat` command.

### HTTPS with Settings from a Local JSON File

appsettings.json

```json
{
  "EtcdSettings": {
    "ConnectionString": "https://serveraddress:2379"
  }
}
```

```csharp
var jsonConfig = new ConfigurationBuilder()
  .AddJsonFile("appsettings.json")
  .Build();

var config = new ConfigurationBuilder()
  .AddEtcd(
    new Credentials("MyEtcdUserName", "passw"),
    new ConfigurationBasedEtcdSettings(jsonConfig))
  .Build();

var mySection = config.GetSection("MySection");
var myKeyValue = mySection["MyKeyName"];
```

Settings can be mixed from different locations.

### HTTPS with a private CA

If the etcd server certificate is signed by a private CA, you can pass the CA certificate (PEM) instead of installing it in the system store:

```csharp
var config = new ConfigurationBuilder()
  .AddEtcd(
    new Credentials("MyEtcdUserName", "passw"),
    new EtcdSettings("https://serveraddress:2379", "/etc/etcd/pki/ca.crt"))
  .Build();
```

or in appsettings.json:

```json
{
  "EtcdSettings": {
    "ConnectionString": "https://serveraddress:2379",
    "CaCertificateFile": "/etc/etcd/pki/ca.crt"
  }
}
```

Host name checks still apply: the server certificate must contain the host name of the connection string.

### TLS client certificate authentication (common name)

etcd can [authenticate users by the common name (CN) of a TLS client certificate](https://etcd.io/docs/latest/op-guide/authentication/rbac/#using-tls-common-name). The client does not need a password and does not request an authentication token. The CN of the certificate is the etcd user name, and that user's roles decide which keys can be read.

```csharp
var config = new ConfigurationBuilder()
  .AddEtcd(
    CertificateCredentials.FromPemFiles("/etc/myapp/etcd/client.crt", "/etc/myapp/etcd/client.key"),
    new EtcdSettings("https://serveraddress:2379", "/etc/myapp/etcd/ca.crt"))
  .Build();
```

* `CertificateCredentials.FromPemFiles(certificateFile, keyFile)` loads a PEM certificate and a PEM private key (PKCS#1, PKCS#8 or EC). If `keyFile` is omitted, the key is read from the certificate file.
* `CertificateCredentials.FromEnvironmentVariables()` reads the file paths from `ETCD_CLIENT_CERTIFICATE_FILE` and `ETCD_CLIENT_KEY_FILE`. You can pass other variable names.
* `new CertificateCredentials(X509Certificate2)` accepts a certificate you loaded yourself. It must have a private key.
* The connection string must use `https`.

Requirements on the etcd side:

* the server runs with `--client-cert-auth=true` and `--trusted-ca-file=<CA that signed the client certificates>`
* an etcd user exists whose name equals the certificate CN. It can be created without a password (`etcdctl user add <name> --no-password`) so it can only log in with the certificate
* that user has a role with read permission on the key prefixes you load

Watching keys (`enableWatch`) works the same as with password authentication.

## Testing

To set up an integration test environment, make sure you have an etcd instance running on the standard port, without SSL: http://localhost:2379

On Ubuntu, simply run 

```
sudo apt-get install etcd-server etcd-client
```

Create a root account and enable authentication

```
etcdctl --endpoints=http://localhost:2379 user add root
```
Enable authentication:
```
etcdctl --endpoints=http://localhost:2379 auth enable
```

For your convenience, terraform files are included in the folder tests/terraform/

In the file ```tests/terraform/main.tf``` replace the password with the one you just used when creating the root user.

in the tests/terraform folder, init terraform:

```
terraform init
```
to see what changes will be made:
```
terraform plan
```
to execute the changes, and create the keys:
```
terraform apply
```

then run the tests from the src folder

### Testing with TLS client certificates

The tests in `CertificateAuthenticationTests` connect to an https listener that requires a client certificate. Without the certificates they are reported as *ignored*. The existing http listener on port 2379 stays in place for the password tests.

1. `terraform apply` in `tests/terraform` (see above) also:
   * creates a test CA (`ca.crt`, `ca.key`), a server certificate for `localhost` and two client certificates in `tests/certs/`: `client.crt` (CN `MyCertUser`) and `unknown-client.crt` (CN `UnknownCertUser`, which has no etcd user)
   * creates the etcd user `MyCertUser` **without a password**, with the same roles as `MyUserName`

   The `tls` and `local` providers are new, so run `terraform init -upgrade` first if you already initialised the folder.

2. Give etcd access to the server certificate:

   ```
   sudo mkdir -p /etc/etcd/pki
   sudo cp tests/certs/ca.crt tests/certs/server.crt tests/certs/server.key /etc/etcd/pki/
   sudo chown -R etcd:etcd /etc/etcd/pki
   sudo chmod 600 /etc/etcd/pki/server.key
   ```

3. Add an https listener with client certificate authentication. With the Ubuntu `etcd-server` package, edit `/etc/default/etcd`:

   ```
   ETCD_LISTEN_CLIENT_URLS="http://localhost:2379,https://localhost:2378"
   ETCD_ADVERTISE_CLIENT_URLS="http://localhost:2379,https://localhost:2378"
   ETCD_CERT_FILE="/etc/etcd/pki/server.crt"
   ETCD_KEY_FILE="/etc/etcd/pki/server.key"
   ETCD_TRUSTED_CA_FILE="/etc/etcd/pki/ca.crt"
   ETCD_CLIENT_CERT_AUTH="true"
   ```

   (or pass the same values as `--listen-client-urls`, `--advertise-client-urls`, `--cert-file`, `--key-file`, `--trusted-ca-file` and `--client-cert-auth` command line flags) and restart etcd:

   ```
   sudo systemctl restart etcd
   ```

4. Check the setup:

   ```
   etcdctl --endpoints=https://localhost:2378 --cacert=tests/certs/ca.crt \
     --cert=tests/certs/client.crt --key=tests/certs/client.key \
     get TestSection --prefix
   ```

   This must print the TestSection keys without asking for a user or password.

5. Run the tests from the `src` folder: `dotnet test`

The tests can be pointed at another setup with these environment variables:

| Variable | Default |
|---|---|
| `ETCD_TLS_TEST_CONNECTION_STRING` | `https://localhost:2378` |
| `ETCD_TLS_TEST_CERTS_FOLDER` | the `tests/certs` folder, found by searching upwards from the test binaries |

## Contributing

There are many ways in which you can participate in the project. Like most open-source software projects, contributing code is just one of many ways you can help improve. Some of the things you could help out with are:

* Documentation (both code and features)
* Bug reports
* Bug fixes
* Feature requests
* Feature implementations
* Test coverage
* Code quality
* Sample applications

## License

Licensed under the GNU Lesser General Public License.
