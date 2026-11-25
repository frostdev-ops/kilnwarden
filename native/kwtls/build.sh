#!/usr/bin/env bash
# build.sh [windows|host|all] [--record]: builds kwtls (the web admin's TLS engine).
#   windows: the DLL the mod ships, cross-built on Linux with cargo xwin (as Kiln's release builds kilnctl.exe and
#            kiln.exe, tools/release/release.sh): x86_64-pc-windows-msvc, static CRT, --locked, no build paths in the
#            file, a deterministic PE (/Brepro). Writes dist/kwtls-<version>.dll and dist/kwtls-<version>.dll.json (the
#            build manifest: sha256, size, imports, exports, toolchain, source). Fails unless the DLL imports Windows
#            system DLLs only and exports exactly the $exports kwtls_* functions. Compares the sha256 with
#            windows-build.sha256 (the recorded build of this source); --record rewrites that file.
#   host:    cargo build for this machine -> dist/libkwtls.so (the offline tests' TLS engine on Linux).
# The mod's KilnDeploy/KilnPack check the DLL against its manifest (src/Kilnwarden/Kilnwarden.csproj).
# Needs: rustup target x86_64-pc-windows-msvc, cargo-xwin, clang (clang-cl, lld-link, llvm-readobj).
# Heavy: on a shared machine, run it at low priority (for example nice -n 19 native/kwtls/build.sh windows).
set -euo pipefail
cd "$(dirname "$0")"
here=$PWD
what=all record=0
for a in "$@"; do
  case $a in
    windows|host|all) what=$a ;;
    --record) record=1 ;;
    *) echo "usage: build.sh [windows|host|all] [--record]" >&2; exit 2 ;;
  esac
done
version=$(sed -n 's/^version = "\(.*\)"/\1/p' Cargo.toml | head -1)
win=x86_64-pc-windows-msvc
exports=15
# the only DLLs the Windows build may import: Windows itself (static CRT, so no vcruntime/ucrt redistributable)
allowed_imports='^(ADVAPI32|KERNEL32|ntdll|bcrypt|bcryptprimitives|api-ms-win-core-[a-z0-9-]+)\.dll$'
dir=${CARGO_TARGET_DIR:-target}
mkdir -p dist
die() { echo "build.sh: $*" >&2; exit 1; }

if [[ $what == windows || $what == all ]]; then
  command -v cargo-xwin >/dev/null || die "cargo-xwin is missing (cargo install --locked cargo-xwin)"
  command -v llvm-readobj >/dev/null || die "llvm-readobj is missing (llvm)"
  rustup target list --installed 2>/dev/null | grep -qx $win || die "rustup target add $win"
  out=dist/kwtls-$version.dll
  rm -f "$out" "$out.json"
  # RUSTFLAGS replace .cargo/config.toml's target rustflags, so the static CRT is repeated here. The remaps keep this
  # folder, the home folder and the crate cache out of the DLL (panic locations); /Brepro gives a fixed PE timestamp;
  # /DEBUG:NONE: no PDB and no CodeView id (the PDB holds build paths and its id would differ per build folder);
  # /ignore:4099 drops lld-link's "no PDB for libvcruntime.lib" noise.
  cargo_home=${CARGO_HOME:-$HOME/.cargo}
  unset RUSTFLAGS
  CARGO_ENCODED_RUSTFLAGS=$(printf '%s\x1f' -Ctarget-feature=+crt-static "--remap-path-prefix=$here=/kwtls" \
    "--remap-path-prefix=$cargo_home=/cargo" "--remap-path-prefix=$HOME=/build" -Clink-arg=/Brepro -Clink-arg=/DEBUG:NONE -Clink-arg=/ignore:4099)
  export CARGO_ENCODED_RUSTFLAGS=${CARGO_ENCODED_RUSTFLAGS%$'\x1f'} SOURCE_DATE_EPOCH=${SOURCE_DATE_EPOCH:-0}
  cargo xwin build --release --locked --target $win
  cp "$dir/$win/release/kwtls.dll" "$out"

  imports=$(llvm-readobj --coff-imports "$out" | sed -n 's/^ *Name: \(.*\.dll\)$/\1/Ip' | sort -u)
  bad=$(grep -Ev "$allowed_imports" <<<"$imports" || true)
  [[ -z $bad ]] || die "$out imports non-system DLLs: $(tr '\n' ' ' <<<"$bad")"
  names=$(llvm-readobj --coff-exports "$out" | sed -n 's/^ *Name: \(.*\)$/\1/p' | sort)
  n=$(grep -c '^kwtls_' <<<"$names" || true)
  [[ $n == "$exports" && $(wc -l <<<"$names") == "$exports" ]] || die "$out exports $(wc -l <<<"$names") functions, $n kwtls_*: expected $exports kwtls_*"
  leak=$(grep -c -a -F -e "$here" -e "$HOME" "$out" || true)
  [[ $leak == 0 ]] || die "$out holds a build path ($here or $HOME)"

  sha=$(sha256sum "$out" | cut -d' ' -f1)
  src=$(git rev-parse --short=12 HEAD 2>/dev/null || echo unknown)
  [[ -z $(git status --porcelain -- . ':!dist' 2>/dev/null) ]] || src="$src+dirty"
  xwin_done=$HOME/.cache/cargo-xwin/xwin/DONE
  crt=$(grep -o -m1 'VC\.[0-9.]*CRT' "$xwin_done" 2>/dev/null | sed 's/^VC\.//; s/\.CRT$//' || true)
  sdk=$(grep -o -m1 'Win11SDK_[0-9.]*' "$xwin_done" 2>/dev/null | sed 's/^Win11SDK_//' || true)
  python3 -I - "$out" "$version" "$win" "$sha" "$src" "$(rustc -V)" "$(cargo xwin --version)" "${crt:-unknown}" "${sdk:-unknown}" \
    "$(sha256sum Cargo.lock | cut -d' ' -f1)" "$imports" "$names" <<'PY'
import json, os, sys
out, version, target, sha, src, rustc, xwin, crt, sdk, lock, imports, names = sys.argv[1:]
json.dump({
    "format": 1, "file": os.path.basename(out), "version": version, "target": target, "crt": "static",
    "sha256": sha, "bytes": os.path.getsize(out),
    "imports": imports.split(), "exports": names.split(),
    "source": src, "cargoLockSha256": lock,
    "toolchain": {"rustc": rustc, "cargoXwin": xwin, "msvcCrt": crt, "windowsSdk": sdk},
    "build": "native/kwtls/build.sh windows",
}, open(out + ".json", "w"), indent=2)
PY
  echo "$out ($(stat -c %s "$out") bytes) sha256 $sha"
  echo "  imports: $(tr '\n' ' ' <<<"$imports")"
  echo "  exports: $n kwtls_*"
  if ((record)); then
    echo "$sha  kwtls-$version.dll" > windows-build.sha256
    echo "  recorded in windows-build.sha256"
  elif [[ -f windows-build.sha256 ]]; then
    if grep -q "^$sha  kwtls-$version.dll$" windows-build.sha256; then echo "  = windows-build.sha256 (the recorded build)"
    else echo "  differs from windows-build.sha256: source or toolchain changed since it was recorded (./build.sh windows --record)" >&2; fi
  fi
fi
if [[ $what == host || $what == all ]]; then
  # a plain host build: RUSTFLAGS from the Windows step must not leak into it
  CARGO_ENCODED_RUSTFLAGS= cargo build --release --locked
  cp "$dir/release/libkwtls.so" dist/libkwtls.so
  echo "dist/libkwtls.so"
fi
