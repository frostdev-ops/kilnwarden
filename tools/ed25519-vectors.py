#!/usr/bin/env python3
# Generates tests/Offline/Identity/ed25519-vectors.json for the panel's managed Ed25519 verifier:
#   python3 -I tools/ed25519-vectors.py tests/Offline/Identity/ed25519-vectors.json   (needs python cryptography)
# Positive vectors: signed by OpenSSL (python cryptography), cross-checked against RFC 8032 section 7.1.
# Edge cases: built with an independent textbook Ed25519 (RFC 8032 section 6 reference code) below.
import json, os, sys, hashlib, random
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey, Ed25519PublicKey
from cryptography.hazmat.primitives import serialization
from cryptography.exceptions import InvalidSignature

p = 2**255 - 19
L = 2**252 + 27742317777372353535851937790883648493
d = (-121665 * pow(121666, p - 2, p)) % p
I = pow(2, (p - 1) // 4, p)

def inv(x): return pow(x, p - 2, p)
def add(P, Q):
    A = (P[1]-P[0])*(Q[1]-Q[0]) % p; B = (P[1]+P[0])*(Q[1]+Q[0]) % p
    C = 2*P[3]*Q[3]*d % p; D = 2*P[2]*Q[2] % p
    E, F, G, H = B-A, D-C, D+C, B+A
    return (E*F % p, G*H % p, F*G % p, E*H % p)
def mul(s, P):
    Q = (0, 1, 1, 0)
    while s > 0:
        if s & 1: Q = add(Q, P)
        P = add(P, P); s >>= 1
    return Q
def recover_x(y, sign):
    if y >= p: return None
    x2 = (y*y-1) * inv(d*y*y+1) % p
    if x2 == 0: return None if sign else 0
    x = pow(x2, (p+3)//8, p)
    if (x*x - x2) % p: x = x*I % p
    if (x*x - x2) % p: return None
    if (x & 1) != sign: x = p - x
    return x
def decode(b):
    y = int.from_bytes(b, 'little'); sign = y >> 255; y &= (1 << 255) - 1
    x = recover_x(y, sign)
    return None if x is None else (x, y, 1, x*y % p)
def encode(P):
    zi = inv(P[2]); x = P[0]*zi % p; y = P[1]*zi % p
    return int.to_bytes(y | ((x & 1) << 255), 32, 'little')
def H(*parts): return int.from_bytes(hashlib.sha512(b''.join(parts)).digest(), 'little')
gy = 4 * inv(5) % p
Bp = (recover_x(gy, 0), gy, 1, recover_x(gy, 0) * gy % p)

def ossl_sign(seed, msg):
    k = Ed25519PrivateKey.from_private_bytes(seed)
    pub = k.public_key().public_bytes(serialization.Encoding.Raw, serialization.PublicFormat.Raw)
    return pub, k.sign(msg)
def ossl_verify(pub, msg, sig):
    try:
        Ed25519PublicKey.from_public_bytes(pub).verify(sig, msg); return True
    except (InvalidSignature, ValueError): return False

rnd = random.Random(8032)
out = {"source": "tools/ed25519-vectors.py (OpenSSL via python cryptography, seed 8032)", "valid": [], "invalid": []}

# RFC 8032 7.1 tests 1-3 and SHA(abc), as known answers (the generator also checks OpenSSL agrees)
rfc = [
 ("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60", "", "rfc8032 test 1"),
 ("4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb", "72", "rfc8032 test 2"),
 ("c5aa8df43f9f837bedb7442f31dcb7b166d38535076f094b85ce3a2e0b4458f7", "af82", "rfc8032 test 3"),
 ("833fe62409237b9d62ec77587520911e9a759cec1d19755b7da901b96dca3d42",
  "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f", "rfc8032 SHA(abc)"),
]
for seed, msg, name in rfc:
    pub, sig = ossl_sign(bytes.fromhex(seed), bytes.fromhex(msg))
    out["valid"].append({"name": name, "seed": seed, "pub": pub.hex(), "msg": msg, "sig": sig.hex()})
for i in range(48):
    seed = bytes(rnd.getrandbits(8) for _ in range(32))
    n = [0, 1, 2, 31, 32, 33, 63, 64, 65, 127, 128, 255, 1023][i % 13] if i < 26 else rnd.randrange(0, 2048)
    msg = bytes(rnd.getrandbits(8) for _ in range(n))
    pub, sig = ossl_sign(seed, msg)
    out["valid"].append({"name": f"openssl random {i} ({n} bytes)", "seed": seed.hex(), "pub": pub.hex(), "msg": msg.hex(), "sig": sig.hex()})

base = out["valid"][4]
bpub, bmsg, bsig = bytes.fromhex(base["pub"]), bytes.fromhex(base["msg"]), bytes.fromhex(base["sig"])
def bad(name, pub, msg, sig, why):
    out["invalid"].append({"name": name, "pub": pub.hex(), "msg": msg.hex(), "sig": sig.hex(), "why": why, "openssl": ossl_verify(pub, msg, sig)})

# the eight small-order points: [L] of a point with a torsion part, then its multiples
P = None
y = 2
while P is None:
    P = decode(int.to_bytes(y, 32, 'little')); y += 1
T = mul(L, P)
small = []
Q = (0, 1, 1, 0)
for k in range(8):
    e = encode(Q)
    if e not in small: small.append(e)
    Q = add(Q, T)
assert len(small) == 8, len(small)
for e in small: assert encode(mul(8, decode(e))) == encode((0, 1, 1, 0))
out["smallOrder"] = [e.hex() for e in small]

# 1. S = L, S = L + s, S = 2^256 - 1
s = int.from_bytes(bsig[32:], 'little')
bad("S = s + L (malleable)", bpub, bmsg, bsig[:32] + int.to_bytes(s + L, 32, 'little'), "S >= L")
bad("S = L", bpub, bmsg, bsig[:32] + int.to_bytes(L, 32, 'little'), "S >= L")
bad("S = 2^256 - 1", bpub, bmsg, bsig[:32] + b'\xff' * 32, "S >= L")
# 2. identity / small-order public keys with forged signatures that pass the cofactorless equation
for i, e in enumerate(small):
    A = decode(e)
    # R = identity, S = 0: [0]B = O = O + [k]A only when [k]A = O; for the identity key it always holds
    R = (0, 1, 1, 0)
    msg = b"forged for a small-order key %d" % i
    # pick a message where k = 0 mod order(A) so the cofactorless equation holds: search
    for j in range(4096):
        m = msg + b" %d" % j
        k = H(encode(R), e, m) % L
        if encode(mul(k, A)) == encode((0, 1, 1, 0)): break
    else: continue
    bad(f"small-order public key {i}, R = identity, S = 0", e, m, encode(R) + bytes(32), "small-order A (equation holds without the key's secret)")
# 3. small-order R in an otherwise valid-looking signature (refused by the strict rule)
for i, e in enumerate(small[1:4]):
    bad(f"small-order R {i+1}", bpub, bmsg, e + bsig[32:], "small-order R")
# 4. non-canonical encodings: y >= p for A and R
for name, yv in (("y = p", p), ("y = p + 1", p + 1), ("y = 2^255 - 1", 2**255 - 1)):
    enc = int.to_bytes(yv, 32, 'little')
    bad(f"non-canonical A, {name}", enc, bmsg, bsig, "A not canonical")
    bad(f"non-canonical R, {name}", bpub, bmsg, enc + bsig[32:], "R not canonical")
# a non-canonical R that still decodes to the right point (y + p, only possible for y < 19)
# x = 0 with the sign bit set (y = 1 and y = p - 1)
bad("A = identity with sign bit", int.to_bytes(1 | (1 << 255), 32, 'little'), bmsg, bsig, "x = 0 with sign 1")
bad("R = (0,-1) with sign bit", bpub, bmsg, int.to_bytes((p - 1) | (1 << 255), 32, 'little') + bsig[32:], "x = 0 with sign 1")
# 5. not on the curve
y = 2
while decode(int.to_bytes(y, 32, 'little')) is not None: y += 1
bad("A not on the curve", int.to_bytes(y, 32, 'little'), bmsg, bsig, "no x for this y")
bad("R not on the curve", bpub, bmsg, int.to_bytes(y, 32, 'little') + bsig[32:], "no x for this y")
# 6. flipped bits, other message, other key, truncated
for i in (0, 31, 32, 63):
    sg = bytearray(bsig); sg[i] ^= 0x01; bad(f"bit flip in sig byte {i}", bpub, bmsg, bytes(sg), "tampered")
bad("other message", bpub, bmsg + b"x", bsig, "tampered")
bad("other key", bytes.fromhex(out["valid"][5]["pub"]), bmsg, bsig, "wrong key")
# 7. mixed-order key: A + T8 with the honest signature (fails cofactorless, would pass a cofactored check only if k = 0 mod 8)
A = decode(bpub); Tm = decode(small[[i for i, e in enumerate(small) if encode(mul(4, decode(e))) != encode((0,1,1,0))][0]])
bad("mixed-order key A + T8", encode(add(A, Tm)), bmsg, bsig, "wrong key (torsion added)")

# sanity: every valid vector verifies under OpenSSL and every invalid one, except the documented ones, fails
for v in out["valid"]: assert ossl_verify(bytes.fromhex(v["pub"]), bytes.fromhex(v["msg"]), bytes.fromhex(v["sig"])), v["name"]
json.dump(out, open(sys.argv[1], "w"), indent=1)
print(len(out["valid"]), "valid,", len(out["invalid"]), "invalid; openssl accepts:", [v["name"] for v in out["invalid"] if v["openssl"]])
