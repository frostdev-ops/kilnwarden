#!/usr/bin/env bash
# repro.sh [commit]: packs the mod twice from two clean exports of one commit (git archive: no build output, no
# node_modules) and compares the zips byte for byte. Needs the same prerequisites as a normal build (README "Building from source"),
# a kiln-sdk link in this checkout, and native/kwtls/dist (its hash is checked against windows-build.sha256 by the
# pack itself). Heavy: on a shared machine, run it at low priority.
# REPRO_BROKER_PINS=<file>: the broker's public pins (https://frostdev.io/kiln/auth/discord/keys, saved as a file) for a
# release build with Discord sign-in. Each export gets its own copy, so a build that recorded the file's path would differ.
set -euo pipefail
here=$(cd "$(dirname "$0")/../.." && pwd)
rev=${1:-HEAD}
commit=$(git -C "$here" rev-parse "$rev^{commit}")
epoch=$(git -C "$here" log -1 --format=%ct "$commit")
work=$(mktemp -d "${TMPDIR:-/tmp}/kwa-repro.XXXXXX")
trap 'rm -rf -- "$work"' EXIT
sums=()
pins=()
if [ -n "${REPRO_BROKER_PINS:-}" ]; then
  [ -s "$REPRO_BROKER_PINS" ] || { echo "repro.sh: REPRO_BROKER_PINS=$REPRO_BROKER_PINS is not a file" >&2; exit 2; }
  echo "broker pins: $(sha256sum < "$REPRO_BROKER_PINS" | cut -d' ' -f1)"
fi
for n in 1 2; do
  d=$work/$n
  mkdir -p "$d"
  git -C "$here" archive "$commit" | tar -x -C "$d"
  ln -s "$(readlink -f "$here/kiln-sdk")" "$d/kiln-sdk"
  mkdir -p "$d/native/kwtls/dist" && cp "$here"/native/kwtls/dist/kwtls-*.dll* "$d/native/kwtls/dist/"
  if [ -n "${REPRO_BROKER_PINS:-}" ]; then cp "$REPRO_BROKER_PINS" "$work/pins-$n.json"; pins=("-p:WebAdminBrokerPins=$work/pins-$n.json"); fi
  (cd "$d/web" && bun install --frozen-lockfile >/dev/null && bun run build >/dev/null)
  (cd "$d" && SOURCE_DATE_EPOCH=$epoch dotnet build src/Kilnwarden -c Release -t:KilnPack -nologo -v:q "${pins[@]}" >/dev/null)
  zip=$(ls "$d"/bin/Release/*.zip)
  sums+=("$(sha256sum "$zip" | cut -d' ' -f1)")
  [ -n "${REPRO_KEEP:-}" ] && cp "$zip" "$REPRO_KEEP/build-$n.zip"
  echo "build $n: $(basename "$zip") sha256 ${sums[-1]}"
done
if [[ ${sums[0]} == "${sums[1]}" ]]; then echo "reproducible: both builds of $commit pack to the same bytes"; exit 0; fi
echo "NOT reproducible; the entries that differ:" >&2
python3 -I - "$work/1" "$work/2" <<'PY' >&2
import hashlib, sys, zipfile, glob
z = [zipfile.ZipFile(glob.glob(d + "/bin/Release/*.zip")[0]) for d in sys.argv[1:3]]
h = [{i.filename: hashlib.sha256(x.read(i)).hexdigest() for i in x.infolist()} for x in z]
for name in sorted(set(h[0]) | set(h[1])):
    if h[0].get(name) != h[1].get(name): print("  " + name)
PY
exit 1
