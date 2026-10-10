# ── Prefix Range Ends ──────────────────────────────────────────────────────────

data "etcd_prefix_range_end" "testsection" {
  key = "TestSection"
}

data "etcd_prefix_range_end" "myprefix" {
  key = "MyPrefix"
}

data "etcd_prefix_range_end" "mycomplex_prefix" {
  key = "MyComplex/Prefix"
}

# ── Roles ──────────────────────────────────────────────────────────────────────

resource "etcd_role" "test_section" {
  name = "TestSection"

  permissions {
    permission = "read"
    key        = data.etcd_prefix_range_end.testsection.key
    range_end  = data.etcd_prefix_range_end.testsection.range_end
  }
}

resource "etcd_role" "my_prefix" {
  name = "MyPrefix"

  permissions {
    permission = "read"
    key        = data.etcd_prefix_range_end.myprefix.key
    range_end  = data.etcd_prefix_range_end.myprefix.range_end
  }
}

resource "etcd_role" "my_complex_prefix" {
  name = "MyComplexPrefix"

  # One "readwrite" permission, not separate "read" and "write" blocks: etcd stores one
  # permission per key range, so a second grant on the same range replaces the first.
  permissions {
    permission = "readwrite"
    key        = data.etcd_prefix_range_end.mycomplex_prefix.key
    range_end  = data.etcd_prefix_range_end.mycomplex_prefix.range_end
  }
}

# ── Users ──────────────────────────────────────────────────────────────────────

resource "etcd_user" "user" {
  username = "MyUserName"
  password = "passw"
  roles    = [etcd_role.test_section.name, etcd_role.my_prefix.name, etcd_role.my_complex_prefix.name]
}

# A user without password: it can only log in with a TLS client certificate
# whose common name (CN) is "MyCertUser". The certificate is created in tls.tf.
resource "etcd_user" "cert_user" {
  username = "MyCertUser"
  roles    = [etcd_role.test_section.name, etcd_role.my_prefix.name, etcd_role.my_complex_prefix.name]
}
