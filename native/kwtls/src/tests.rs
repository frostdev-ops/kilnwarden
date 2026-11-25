// SPDX-License-Identifier: MIT
//! The C ABI driven the way the managed glue drives it, against a rustls client.
use super::*;
use rustls::pki_types::ServerName;
use rustls::{ClientConfig, ClientConnection, RootCertStore};

fn err() -> String {
    let mut b = [0u8; 512];
    let n = unsafe { kwtls_last_error(b.as_mut_ptr(), b.len()) };
    String::from_utf8_lossy(&b[..n as usize]).into_owned()
}

fn self_signed(names: &str) -> (Vec<u8>, Vec<u8>) {
    let mut cert = vec![0u8; 8192];
    let mut key = vec![0u8; 8192];
    let (mut cl, mut kl) = (0usize, 0usize);
    let serial = [7u8; 16];
    let r = unsafe {
        kwtls_self_signed(names.as_ptr(), names.len(), b"test".as_ptr(), 4, serial.as_ptr(), serial.len(),
                          2026, 1, 1, 2028, 1, 1, cert.as_mut_ptr(), cert.len(), &mut cl, key.as_mut_ptr(), key.len(), &mut kl)
    };
    assert_eq!(r, 0, "{}", err());
    cert.truncate(cl);
    key.truncate(kl);
    (cert, key)
}

fn config(cert: &[u8], key: &[u8]) -> *mut Config {
    unsafe { kwtls_config_new(cert.as_ptr(), cert.len(), key.as_ptr(), key.len(), b"http/1.1".as_ptr(), 8) }
}

fn client(cert_pem: &[u8], name: &str) -> ClientConnection {
    let mut roots = RootCertStore::empty();
    for c in CertificateDer::pem_slice_iter(cert_pem) { roots.add(c.unwrap()).unwrap(); }
    let mut cfg = ClientConfig::builder_with_provider(Arc::new(rustls::crypto::ring::default_provider()))
        .with_safe_default_protocol_versions().unwrap().with_root_certificates(roots).with_no_client_auth();
    cfg.alpn_protocols = vec![b"http/1.1".to_vec()];
    ClientConnection::new(Arc::new(cfg), ServerName::try_from(name.to_string()).unwrap()).unwrap()
}

/// Moves bytes both ways until neither side has anything to send. Small server buffers on purpose.
fn pump(c: &mut ClientConnection, s: *mut ServerConnection, pending: &mut Vec<u8>, got: &mut Vec<u8>) {
    for _ in 0..10_000 {
        let mut moved = false;
        let mut out = Vec::new();
        while c.wants_write() { c.write_tls(&mut out).unwrap(); }
        if !out.is_empty() { pending.extend_from_slice(&out); moved = true; }
        while !pending.is_empty() {
            let take = pending.len().min(1500);
            let n = unsafe { kwtls_read_tls(s, pending.as_ptr(), take) };
            assert!(n >= 0, "server read_tls: {}", err());
            if n == 0 { break; }
            pending.drain(..n as usize);
            moved = true;
        }
        let mut buf = [0u8; 700];
        loop {
            let n = unsafe { kwtls_write_tls(s, buf.as_mut_ptr(), buf.len()) };
            assert!(n >= 0, "{}", err());
            if n == 0 { break; }
            let mut rd = &buf[..n as usize];
            while !rd.is_empty() {
                c.read_tls(&mut rd).unwrap();
                c.process_new_packets().unwrap();
                let mut tmp = [0u8; 4096];
                while let Ok(k) = c.reader().read(&mut tmp) { if k == 0 { break; } got.extend_from_slice(&tmp[..k]); }
            }
            moved = true;
        }
        if !moved { return; }
    }
    panic!("no progress");
}

fn read_plain(s: *mut ServerConnection) -> Vec<u8> {
    let mut all = Vec::new();
    let mut buf = [0u8; 4096];
    loop {
        let n = unsafe { kwtls_read(s, buf.as_mut_ptr(), buf.len()) };
        if n <= 0 { return all; }
        all.extend_from_slice(&buf[..n as usize]);
    }
}

#[test]
fn version_matches_the_crate() {
    let mut b = [0u8; 64];
    let n = unsafe { kwtls_version(b.as_mut_ptr(), b.len()) };
    assert!(std::str::from_utf8(&b[..n as usize]).unwrap().starts_with(concat!(env!("CARGO_PKG_VERSION"), " ")));
}

#[test]
fn handshake_tls13_ecdsa_and_data_both_ways_with_backpressure() {
    let (cert, key) = self_signed("localhost,127.0.0.1");
    assert!(std::str::from_utf8(&cert).unwrap().starts_with("-----BEGIN CERTIFICATE-----"));
    let cfg = config(&cert, &key);
    assert!(!cfg.is_null(), "{}", err());
    let s = unsafe { kwtls_conn_new(cfg) };
    unsafe { kwtls_config_free(cfg) }; // the connection keeps its own reference
    let mut c = client(&cert, "localhost");
    let mut pending = Vec::new();
    let mut client_got = Vec::new();
    pump(&mut c, s, &mut pending, &mut client_got);
    assert_eq!(unsafe { kwtls_is_handshaking(s) }, 0);
    assert_eq!(c.protocol_version(), Some(rustls::ProtocolVersion::TLSv1_3));
    let mut info = [0u8; 256];
    let n = unsafe { kwtls_info(s, info.as_mut_ptr(), info.len()) };
    let info = std::str::from_utf8(&info[..n as usize]).unwrap().to_string();
    assert!(info.starts_with("TLSv1.3\tTLS13_"), "{info}");
    assert!(info.ends_with("\thttp/1.1\tlocalhost"), "{info}");

    // 200 KB client -> server, read in steps (rustls stops taking ciphertext at 16 KiB of unread plaintext)
    let big: Vec<u8> = (0..200_000u32).map(|i| (i % 251) as u8).collect();
    let mut got = Vec::new();
    let mut sent = 0;
    for _ in 0..10_000 {
        if sent < big.len() {
            let n = c.writer().write(&big[sent..(sent + 8192).min(big.len())]).unwrap();
            sent += n;
        }
        pump(&mut c, s, &mut pending, &mut client_got);
        got.extend(read_plain(s));
        if got.len() >= big.len() { break; }
    }
    assert_eq!(got, big);

    // server -> client
    let msg = vec![b'x'; 70_000];
    let mut off = 0;
    while off < msg.len() {
        let n = unsafe { kwtls_write(s, msg[off..].as_ptr(), msg.len() - off) };
        assert!(n > 0, "{}", err());
        off += n as usize;
        pump(&mut c, s, &mut pending, &mut client_got);
    }
    assert_eq!(client_got.len(), msg.len());

    // close_notify from the client: the server reads 0
    c.send_close_notify();
    pump(&mut c, s, &mut pending, &mut client_got);
    let mut b = [0u8; 16];
    assert_eq!(unsafe { kwtls_read(s, b.as_mut_ptr(), b.len()) }, 0);
    unsafe { kwtls_conn_free(s) };
}

#[test]
fn mismatched_key_is_refused_at_config_time() {
    let (cert, _) = self_signed("localhost");
    let (_, other_key) = self_signed("localhost");
    assert!(config(&cert, &other_key).is_null());
    assert!(err().contains("does not match"), "{}", err());
}

#[test]
fn bad_inputs_are_errors_not_crashes() {
    assert!(config(b"junk", b"junk").is_null());
    assert!(config(b"", b"").is_null());
    assert!(unsafe { kwtls_conn_new(std::ptr::null()) }.is_null());
    assert_eq!(unsafe { kwtls_read_tls(std::ptr::null_mut(), b"x".as_ptr(), 1) }, -1);
    assert_eq!(unsafe { kwtls_write_tls(std::ptr::null_mut(), std::ptr::null_mut(), 0) }, -1);
    unsafe { kwtls_conn_free(std::ptr::null_mut()) };
    unsafe { kwtls_config_free(std::ptr::null_mut()) };
    let mut cl = 0usize;
    let mut kl = 0usize;
    let mut tiny = [0u8; 10];
    let r = unsafe { kwtls_self_signed(b"localhost".as_ptr(), 9, b"x".as_ptr(), 1, b"\x01".as_ptr(), 1, 2026, 1, 1, 2027, 1, 1,
                                       tiny.as_mut_ptr(), tiny.len(), &mut cl, tiny.as_mut_ptr(), tiny.len(), &mut kl) };
    assert_eq!(r, -1);
    let r = unsafe { kwtls_self_signed(b"".as_ptr(), 0, b"x".as_ptr(), 1, b"\x01".as_ptr(), 1, 2026, 1, 1, 2027, 1, 1,
                                       tiny.as_mut_ptr(), tiny.len(), &mut cl, tiny.as_mut_ptr(), tiny.len(), &mut kl) };
    assert_eq!(r, -1);
}

/// Random and mutated client bytes: every call returns, nothing panics past the ABI.
#[test]
fn fuzz_read_tls_with_mutated_client_hellos() {
    let (cert, key) = self_signed("localhost");
    let cfg = config(&cert, &key);
    let mut hello = Vec::new();
    let mut c = client(&cert, "localhost");
    c.write_tls(&mut hello).unwrap();
    let mut seed = 0x9e3779b97f4a7c15u64;
    let mut next = || { seed ^= seed << 13; seed ^= seed >> 7; seed ^= seed << 17; seed };
    for i in 0..3000 {
        let s = unsafe { kwtls_conn_new(cfg) };
        let mut input = if i % 3 == 0 { (0..(next() % 600)).map(|_| next() as u8).collect::<Vec<u8>>() } else { hello.clone() };
        for _ in 0..(next() % 8) {
            if input.is_empty() { break; }
            let at = (next() as usize) % input.len();
            match next() % 3 { 0 => input[at] = next() as u8, 1 => { input.remove(at); } _ => input.insert(at, next() as u8) }
        }
        let mut off = 0;
        while off < input.len() {
            let n = unsafe { kwtls_read_tls(s, input[off..].as_ptr(), input.len() - off) };
            if n <= 0 { break; }
            off += n as usize;
        }
        let mut out = [0u8; 8192];
        while unsafe { kwtls_write_tls(s, out.as_mut_ptr(), out.len()) } > 0 {}
        let mut info = [0u8; 256];
        unsafe { kwtls_info(s, info.as_mut_ptr(), info.len()) };
        unsafe { kwtls_conn_free(s) };
    }
    unsafe { kwtls_config_free(cfg) };
}
