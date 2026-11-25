// SPDX-License-Identifier: MIT
//! kwtls: rustls server connections behind a C ABI, for the web admin's managed TLS stream
//! (src/Kilnwarden.Core/Tls/Kwtls*.cs). The managed side owns the socket and moves ciphertext in and out; this
//! library never does I/O and never blocks.
//!
//! Rules of the ABI (version = the crate version, also the DLL's file name kwtls-<version>.dll):
//! - Every export catches panics: a bug here fails one connection (-1 / null), never the game server's process.
//! - Errors: a negative return (or null), the text via kwtls_last_error on the same thread right after the call.
//! - A connection is used by one thread at a time (the managed side holds a lock around every call on it).
//! - A config can be freed while connections made from it live (each holds its own reference).
use rustls::pki_types::pem::PemObject;
use rustls::pki_types::{CertificateDer, PrivateKeyDer};
use rustls::{ServerConfig, ServerConnection};
use std::cell::RefCell;
use std::io::{Read, Write};
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::sync::Arc;

thread_local!(static LAST: RefCell<String> = const { RefCell::new(String::new()) });

fn set_error(e: impl std::fmt::Display) {
    LAST.with(|l| *l.borrow_mut() = e.to_string());
}

fn fail(e: impl std::fmt::Display) -> isize {
    set_error(e);
    -1
}

/// Runs `f`, turning a panic into the error value `on_panic`.
fn guard<T>(on_panic: T, f: impl FnOnce() -> T) -> T {
    match catch_unwind(AssertUnwindSafe(f)) {
        Ok(v) => v,
        Err(p) => {
            let msg = p.downcast_ref::<&str>().map(|s| s.to_string())
                .or_else(|| p.downcast_ref::<String>().cloned()).unwrap_or_else(|| "panic".into());
            set_error(format!("kwtls panic: {msg}"));
            on_panic
        }
    }
}

unsafe fn slice<'a>(p: *const u8, len: usize) -> &'a [u8] {
    if p.is_null() || len == 0 { &[] } else { std::slice::from_raw_parts(p, len) }
}

unsafe fn slice_mut<'a>(p: *mut u8, len: usize) -> &'a mut [u8] {
    if p.is_null() || len == 0 { &mut [] } else { std::slice::from_raw_parts_mut(p, len) }
}

unsafe fn put(buf: *mut u8, cap: usize, text: &[u8]) -> isize {
    if text.len() > cap { return fail(format!("buffer too small: {} bytes needed", text.len())); }
    if !text.is_empty() { std::ptr::copy_nonoverlapping(text.as_ptr(), buf, text.len()); }
    text.len() as isize
}

/// "1.0.0 rustls/0.23.46" into buf; returns its length.
#[no_mangle]
pub unsafe extern "C" fn kwtls_version(buf: *mut u8, cap: usize) -> isize {
    guard(-1, || put(buf, cap, concat!(env!("CARGO_PKG_VERSION"), " rustls/0.23").as_bytes()))
}

pub struct Config(Arc<ServerConfig>);

/// A server config. cert_pem: leaf first, then intermediates (fullchain.pem); key_pem: PKCS#8, PKCS#1 or SEC1; alpn:
/// comma-separated protocol names. TLS 1.3 and 1.2 (forward-secret AEAD suites only: rustls's defaults). The key must
/// match the leaf. Null on error.
#[no_mangle]
pub unsafe extern "C" fn kwtls_config_new(cert_pem: *const u8, cert_len: usize, key_pem: *const u8, key_len: usize,
                                          alpn: *const u8, alpn_len: usize) -> *mut Config {
    guard(std::ptr::null_mut(), || {
        let cert = slice(cert_pem, cert_len);
        let key = slice(key_pem, key_len);
        let alpn = match std::str::from_utf8(slice(alpn, alpn_len)) { Ok(a) => a, Err(e) => { set_error(e); return std::ptr::null_mut(); } };
        let chain: Result<Vec<CertificateDer<'static>>, _> = CertificateDer::pem_slice_iter(cert).collect();
        let chain = match chain {
            Ok(c) if !c.is_empty() => c,
            Ok(_) => { set_error("no certificate in the PEM"); return std::ptr::null_mut(); }
            Err(e) => { set_error(format!("certificate PEM: {e}")); return std::ptr::null_mut(); }
        };
        let key = match PrivateKeyDer::from_pem_slice(key) { Ok(k) => k, Err(e) => { set_error(format!("key PEM: {e}")); return std::ptr::null_mut(); } };
        let provider = Arc::new(rustls::crypto::ring::default_provider());
        let builder = match ServerConfig::builder_with_provider(provider.clone())
            .with_protocol_versions(&[&rustls::version::TLS13, &rustls::version::TLS12]) {
            Ok(b) => b,
            Err(e) => { set_error(e); return std::ptr::null_mut(); }
        };
        // with_single_cert does not compare the key with the leaf: do it, so a mismatched pair fails here, not in
        // every handshake
        let signing = match provider.key_provider.load_private_key(key.clone_key()) { Ok(k) => k, Err(e) => { set_error(format!("key: {e}")); return std::ptr::null_mut(); } };
        let certified = rustls::sign::CertifiedKey::new(chain.clone(), signing);
        if let Err(e) = certified.keys_match() { set_error(format!("the key does not match the certificate: {e}")); return std::ptr::null_mut(); }
        let mut cfg = match builder.with_no_client_auth().with_single_cert(chain, key) {
            Ok(c) => c,
            Err(e) => { set_error(e); return std::ptr::null_mut(); }
        };
        cfg.alpn_protocols = alpn.split(',').map(str::trim).filter(|s| !s.is_empty()).map(|s| s.as_bytes().to_vec()).collect();
        cfg.max_early_data_size = 0;
        cfg.send_tls13_tickets = 2;
        Box::into_raw(Box::new(Config(Arc::new(cfg))))
    })
}

#[no_mangle]
pub unsafe extern "C" fn kwtls_config_free(c: *mut Config) {
    guard((), || if !c.is_null() { drop(Box::from_raw(c)); })
}

#[no_mangle]
pub unsafe extern "C" fn kwtls_conn_new(c: *const Config) -> *mut ServerConnection {
    guard(std::ptr::null_mut(), || {
        if c.is_null() { set_error("null config"); return std::ptr::null_mut(); }
        match ServerConnection::new((*c).0.clone()) {
            Ok(s) => Box::into_raw(Box::new(s)),
            Err(e) => { set_error(e); std::ptr::null_mut() }
        }
    })
}

#[no_mangle]
pub unsafe extern "C" fn kwtls_conn_free(s: *mut ServerConnection) {
    guard((), || if !s.is_null() { drop(Box::from_raw(s)); })
}

/// Ciphertext from the socket. Returns bytes consumed, or -1. Stops early (returns fewer) while 16 KiB of plaintext wait
/// for kwtls_read: rustls refuses more until the reader catches up, so the caller keeps the rest and feeds it again.
#[no_mangle]
pub unsafe extern "C" fn kwtls_read_tls(s: *mut ServerConnection, buf: *const u8, len: usize) -> isize {
    guard(-1, || {
        if s.is_null() { return fail("null connection"); }
        let s = &mut *s;
        let mut data = slice(buf, len);
        let mut used = 0usize;
        while !data.is_empty() {
            match s.process_new_packets() {
                Ok(st) if st.plaintext_bytes_to_read() >= 16 * 1024 => break,
                Ok(_) => {}
                Err(e) => return fail(e),
            }
            match s.read_tls(&mut data) {
                Ok(0) => break,
                Ok(n) => used += n,
                // rustls's own buffer is full: give it time to process (the loop above), or stop for now
                Err(e) if e.kind() == std::io::ErrorKind::Other && used > 0 => break,
                Err(e) => return fail(e),
            }
        }
        if let Err(e) = s.process_new_packets() { return fail(e); }
        used as isize
    })
}

/// Ciphertext to send. Returns bytes written into buf (0: nothing pending), or -1.
#[no_mangle]
pub unsafe extern "C" fn kwtls_write_tls(s: *mut ServerConnection, buf: *mut u8, cap: usize) -> isize {
    guard(-1, || {
        if s.is_null() { return fail("null connection"); }
        let s = &mut *s;
        let mut out = slice_mut(buf, cap);
        let mut n = 0usize;
        while s.wants_write() && !out.is_empty() {
            match s.write_tls(&mut out) { Ok(0) => break, Ok(k) => n += k, Err(e) => return fail(e) }
        }
        n as isize
    })
}

#[no_mangle]
pub unsafe extern "C" fn kwtls_is_handshaking(s: *const ServerConnection) -> i32 {
    guard(0, || if s.is_null() { 0 } else { (*s).is_handshaking() as i32 })
}

#[no_mangle]
pub unsafe extern "C" fn kwtls_wants_write(s: *const ServerConnection) -> i32 {
    guard(0, || if s.is_null() { 0 } else { (*s).wants_write() as i32 })
}

/// Plaintext out. Returns bytes, 0 at a clean close (close_notify), -2 when none is buffered yet, -1 on error.
#[no_mangle]
pub unsafe extern "C" fn kwtls_read(s: *mut ServerConnection, buf: *mut u8, cap: usize) -> isize {
    guard(-1, || {
        if s.is_null() { return fail("null connection"); }
        if cap == 0 { return 0; }
        match (*s).reader().read(slice_mut(buf, cap)) {
            Ok(n) => n as isize,
            Err(e) if e.kind() == std::io::ErrorKind::WouldBlock => -2,
            Err(e) => fail(e),
        }
    })
}

/// Plaintext in (buffered by rustls; drain kwtls_write_tls afterwards). Returns bytes taken, or -1.
#[no_mangle]
pub unsafe extern "C" fn kwtls_write(s: *mut ServerConnection, buf: *const u8, len: usize) -> isize {
    guard(-1, || {
        if s.is_null() { return fail("null connection"); }
        match (*s).writer().write(slice(buf, len)) { Ok(n) => n as isize, Err(e) => fail(e) }
    })
}

#[no_mangle]
pub unsafe extern "C" fn kwtls_send_close(s: *mut ServerConnection) {
    guard((), || if !s.is_null() { (*s).send_close_notify(); })
}

/// "TLSv1.3\tTLS13_AES_256_GCM_SHA384\thttp/1.1\tlocalhost" (protocol, suite, ALPN, SNI; empty fields allowed) into buf.
#[no_mangle]
pub unsafe extern "C" fn kwtls_info(s: *const ServerConnection, buf: *mut u8, cap: usize) -> isize {
    guard(-1, || {
        if s.is_null() { return fail("null connection"); }
        let s = &*s;
        let proto = match s.protocol_version() {
            Some(rustls::ProtocolVersion::TLSv1_3) => "TLSv1.3".to_string(),
            Some(rustls::ProtocolVersion::TLSv1_2) => "TLSv1.2".to_string(),
            Some(v) => format!("{v:?}"),
            None => String::new(),
        };
        let suite = s.negotiated_cipher_suite().map(|c| format!("{:?}", c.suite())).unwrap_or_default();
        let alpn = s.alpn_protocol().map(|a| String::from_utf8_lossy(a).replace('\t', " ")).unwrap_or_default();
        let sni = s.server_name().unwrap_or("").replace('\t', " ");
        put(buf, cap, format!("{proto}\t{suite}\t{alpn}\t{sni}").as_bytes())
    })
}

/// A self-signed ECDSA P-256 server certificate (rcgen): names = comma-separated DNS names and IP addresses (the SAN),
/// cn = the subject's common name, serial = 1..20 bytes, validity from..to as UTC dates (y, m, d). The certificate PEM
/// goes into cert_buf and the PKCS#8 key PEM into key_buf; their lengths into *cert_len / *key_len. Returns 0 or -1.
#[no_mangle]
pub unsafe extern "C" fn kwtls_self_signed(names: *const u8, names_len: usize, cn: *const u8, cn_len: usize,
                                           serial: *const u8, serial_len: usize,
                                           from_y: i32, from_m: u8, from_d: u8, to_y: i32, to_m: u8, to_d: u8,
                                           cert_buf: *mut u8, cert_cap: usize, cert_len: *mut usize,
                                           key_buf: *mut u8, key_cap: usize, key_len: *mut usize) -> isize {
    guard(-1, || {
        use rcgen::{CertificateParams, DistinguishedName, DnType, ExtendedKeyUsagePurpose, IsCa, KeyPair, KeyUsagePurpose,
                    SerialNumber, PKCS_ECDSA_P256_SHA256};
        if cert_len.is_null() || key_len.is_null() { return fail("null length pointer"); }
        let names = match std::str::from_utf8(slice(names, names_len)) { Ok(s) => s, Err(e) => return fail(e) };
        let cn = match std::str::from_utf8(slice(cn, cn_len)) { Ok(s) => s, Err(e) => return fail(e) };
        let names: Vec<String> = names.split(',').map(str::trim).filter(|s| !s.is_empty()).map(String::from).collect();
        if names.is_empty() { return fail("no names"); }
        let serial = slice(serial, serial_len);
        if serial.is_empty() || serial.len() > 20 { return fail("serial must be 1 to 20 bytes"); }
        let valid = |y: i32, m: u8, d: u8| (1970..=9999).contains(&y) && (1..=12).contains(&m) && (1..=31).contains(&d);
        if !valid(from_y, from_m, from_d) || !valid(to_y, to_m, to_d) { return fail("bad validity dates"); }
        let mut params = match CertificateParams::new(names) { Ok(p) => p, Err(e) => return fail(e) };
        let mut dn = DistinguishedName::new();
        dn.push(DnType::CommonName, cn);
        params.distinguished_name = dn;
        params.serial_number = Some(SerialNumber::from_slice(serial));
        params.not_before = rcgen::date_time_ymd(from_y, from_m, from_d);
        params.not_after = rcgen::date_time_ymd(to_y, to_m, to_d);
        params.is_ca = IsCa::ExplicitNoCa;
        params.key_usages = vec![KeyUsagePurpose::DigitalSignature];
        params.extended_key_usages = vec![ExtendedKeyUsagePurpose::ServerAuth];
        let key = match KeyPair::generate_for(&PKCS_ECDSA_P256_SHA256) { Ok(k) => k, Err(e) => return fail(e) };
        let cert = match params.self_signed(&key) { Ok(c) => c, Err(e) => return fail(e) };
        let cert_pem = cert.pem();
        let key_pem = key.serialize_pem();
        if cert_pem.len() > cert_cap || key_pem.len() > key_cap { return fail("buffer too small"); }
        std::ptr::copy_nonoverlapping(cert_pem.as_ptr(), cert_buf, cert_pem.len());
        std::ptr::copy_nonoverlapping(key_pem.as_ptr(), key_buf, key_pem.len());
        *cert_len = cert_pem.len();
        *key_len = key_pem.len();
        0
    })
}

/// The last error of this thread into buf; returns its length.
#[no_mangle]
pub unsafe extern "C" fn kwtls_last_error(buf: *mut u8, cap: usize) -> isize {
    guard(0, || LAST.with(|l| {
        let t = l.borrow();
        let n = t.len().min(cap);
        if n > 0 { std::ptr::copy_nonoverlapping(t.as_ptr(), buf, n); }
        n as isize
    }))
}

#[cfg(test)]
mod tests;
