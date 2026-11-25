#!/usr/bin/env python3
# Generates tests/Offline/Ops/minisign-vectors.json for the panel's minisign verifier (signed update packages):
#   python3 -I tools/minisign-vectors.py tests/Offline/Ops/minisign-vectors.json   (needs python cryptography)
# Format (https://jedisct1.github.io/minisign/): a public key is base64("Ed" | key id (8) | Ed25519 key (32)); a
# signature file is "untrusted comment: ..." / base64(alg | key id | sig(64)) / "trusted comment: ..." /
# base64(global sig(64)). alg "ED": sig over BLAKE2b-512(file) (minisign's default since 0.10), "Ed": over the file
# itself (legacy). The global signature covers sig(64) | the trusted comment's text. BLAKE2b vectors come from hashlib.
import base64, hashlib, json, os, sys
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives import serialization

def raw_pub(k):
    return k.public_key().public_bytes(serialization.Encoding.Raw, serialization.PublicFormat.Raw)

def keypair(seed, key_id):
    k = Ed25519PrivateKey.from_private_bytes(hashlib.sha256(seed).digest())
    pub = base64.b64encode(b"Ed" + key_id + raw_pub(k)).decode()
    return k, key_id, pub

def sign(k, key_id, data, alg=b"ED", trusted="timestamp:1764072000\tfile:package.zip\thashed", untrusted="signature from minisign secret key"):
    msg = hashlib.blake2b(data, digest_size=64).digest() if alg == b"ED" else data
    sig = k.sign(msg)
    glob = k.sign(sig + trusted.encode())
    return (f"untrusted comment: {untrusted}\n" + base64.b64encode(alg + key_id + sig).decode() + "\n" +
            f"trusted comment: {trusted}\n" + base64.b64encode(glob).decode() + "\n")

k1, id1, pub1 = keypair(b"kilnwarden test key 1", bytes.fromhex("1122334455667788"))
k2, id2, pub2 = keypair(b"kilnwarden test key 2", bytes.fromhex("8877665544332211"))
data = (b"PK\x03\x04 a pretend package " * 200)[:4000]
good = sign(k1, id1, data)
legacy = sign(k1, id1, data, alg=b"Ed")
other = sign(k2, id2, data)
lines = good.splitlines()
tampered_comment = "\n".join([lines[0], lines[1], "trusted comment: timestamp:1764072000\tfile:evil.zip", lines[3]]) + "\n"
# the same key id, another key: the signature cannot verify
k3 = Ed25519PrivateKey.from_private_bytes(hashlib.sha256(b"impostor").digest())
impostor = sign(k3, id1, data)

blake = []
for msg in [b"", b"abc", bytes(range(128)), bytes(range(129)) , bytes(i % 251 for i in range(300000))]:
    blake.append({"inputHex": msg.hex() if len(msg) <= 1024 else None, "pattern251": len(msg) if len(msg) > 1024 else None,
                  "blake2b512": hashlib.blake2b(msg, digest_size=64).hexdigest()})

out = {
    "comment": "made by tools/minisign-vectors.py",
    "publicKey1": pub1, "publicKey1File": f"untrusted comment: minisign public key {id1[::-1].hex().upper()}\n{pub1}\n",
    "publicKey2": pub2,
    "dataBase64": base64.b64encode(data).decode(),
    "signaturePrehashed": good, "signatureLegacy": legacy, "signatureOtherKey": other,
    "signatureTamperedComment": tampered_comment, "signatureImpostor": impostor,
    "blake2b": blake,
}
with open(sys.argv[1], "w") as f:
    json.dump(out, f, indent=2)
    f.write("\n")
print("wrote", sys.argv[1])
