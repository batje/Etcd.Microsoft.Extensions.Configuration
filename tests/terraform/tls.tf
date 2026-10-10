# ── TLS certificates for the client certificate (common name) integration tests ─
#
# Creates a throw-away CA, a server certificate for the local etcd instance and
# client certificates. The files are written to ../certs (tests/certs), which is
# git-ignored. See README.md, section "Testing with TLS client certificates".
#
# The common name (CN) of a client certificate is the etcd user name.

locals {
  certs_folder = "${path.module}/../certs"
}

# ── Certificate Authority ──────────────────────────────────────────────────────

resource "tls_private_key" "ca" {
  algorithm = "RSA"
  rsa_bits  = 4096
}

resource "tls_self_signed_cert" "ca" {
  private_key_pem = tls_private_key.ca.private_key_pem

  subject {
    common_name  = "Etcd.Microsoft.Extensions.Configuration test CA"
    organization = "Simplify community"
  }

  is_ca_certificate     = true
  validity_period_hours = 24 * 365 * 5

  allowed_uses = [
    "cert_signing",
    "crl_signing",
    "digital_signature",
  ]
}

# ── Server certificate (for the local etcd instance) ───────────────────────────

resource "tls_private_key" "server" {
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "tls_cert_request" "server" {
  private_key_pem = tls_private_key.server.private_key_pem

  subject {
    common_name = "localhost"
  }

  dns_names    = ["localhost"]
  ip_addresses = ["127.0.0.1", "::1"]
}

resource "tls_locally_signed_cert" "server" {
  cert_request_pem   = tls_cert_request.server.cert_request_pem
  ca_private_key_pem = tls_private_key.ca.private_key_pem
  ca_cert_pem        = tls_self_signed_cert.ca.cert_pem

  validity_period_hours = 24 * 365 * 5

  allowed_uses = [
    "digital_signature",
    "key_encipherment",
    "server_auth",
  ]
}

# ── Client certificates ────────────────────────────────────────────────────────

locals {
  # client file name => common name (= etcd user name)
  client_certificates = {
    # Has an etcd user (see etcd_user.cert_user below)
    "client" = etcd_user.cert_user.username
    # Has no etcd user, used to test that unknown users are rejected
    "unknown-client" = "UnknownCertUser"
  }
}

resource "tls_private_key" "client" {
  for_each = local.client_certificates

  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "tls_cert_request" "client" {
  for_each = local.client_certificates

  private_key_pem = tls_private_key.client[each.key].private_key_pem

  subject {
    common_name = each.value
  }
}

resource "tls_locally_signed_cert" "client" {
  for_each = local.client_certificates

  cert_request_pem   = tls_cert_request.client[each.key].cert_request_pem
  ca_private_key_pem = tls_private_key.ca.private_key_pem
  ca_cert_pem        = tls_self_signed_cert.ca.cert_pem

  validity_period_hours = 24 * 365 * 5

  allowed_uses = [
    "digital_signature",
    "key_encipherment",
    "client_auth",
  ]
}

# ── Files ──────────────────────────────────────────────────────────────────────

resource "local_file" "ca_cert" {
  filename        = "${local.certs_folder}/ca.crt"
  content         = tls_self_signed_cert.ca.cert_pem
  file_permission = "0644"
}

# The CA key is a throw-away test key. It is written so other projects can sign their own
# test client certificates for this etcd instance (e.g. GridboxEtcdConfiguration/tests/terraform).
resource "local_sensitive_file" "ca_key" {
  filename        = "${local.certs_folder}/ca.key"
  content         = tls_private_key.ca.private_key_pem
  file_permission = "0600"
}

resource "local_file" "server_cert" {
  filename        = "${local.certs_folder}/server.crt"
  content         = tls_locally_signed_cert.server.cert_pem
  file_permission = "0644"
}

resource "local_sensitive_file" "server_key" {
  filename        = "${local.certs_folder}/server.key"
  content         = tls_private_key.server.private_key_pem
  file_permission = "0600"
}

resource "local_file" "client_cert" {
  for_each = local.client_certificates

  filename        = "${local.certs_folder}/${each.key}.crt"
  content         = tls_locally_signed_cert.client[each.key].cert_pem
  file_permission = "0644"
}

resource "local_sensitive_file" "client_key" {
  for_each = local.client_certificates

  filename        = "${local.certs_folder}/${each.key}.key"
  content         = tls_private_key.client[each.key].private_key_pem
  file_permission = "0600"
}
