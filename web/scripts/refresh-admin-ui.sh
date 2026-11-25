#!/usr/bin/env bash
# refresh-admin-ui.sh <kiln worktree>: builds @kiln/admin-ui in a kiln worktree (launcher/packages/admin-ui, `bun run
# build:admin-ui` in launcher/) and vendors the BUILT package into web/vendor/admin-ui/: package.json, the bundle, the
# stylesheet and the declarations (no source map: it would carry the launcher's sources). VENDOR.json records the kiln
# commit and a hash of every file. The worktree's launcher/ must be clean, so the commit says exactly what was built.
# Never copy admin-ui sources by hand; never publish the package.
#
#   git -C <kiln checkout> worktree add <dir> <kiln commit>
#   web/scripts/refresh-admin-ui.sh <dir>
#
#   web/scripts/refresh-admin-ui.sh --export <kiln repo or worktree> <commit>
#       builds from `git archive <commit> launcher` in a scratch folder instead: nothing in the kiln checkout is
#       touched (no node_modules, no dist), and any commit can be vendored without a worktree of its own.
set -euo pipefail
here=$(cd "$(dirname "$0")/.." && pwd)
dest="$here/vendor/admin-ui"
if [ "${1:-}" = "--export" ]; then
  repo=${2:?usage: refresh-admin-ui.sh --export <kiln repo> <commit>}; rev=${3:?usage: refresh-admin-ui.sh --export <kiln repo> <commit>}
  commit=$(git -C "$repo" rev-parse "$rev^{commit}")
  subject=$(git -C "$repo" log -1 --format=%s "$commit")
  scratch=$(mktemp -d "${TMPDIR:-/tmp}/kwa-admin-ui.XXXXXX")
  trap 'rm -rf -- "$scratch"' EXIT
  git -C "$repo" archive "$commit" launcher | tar -x -C "$scratch"
  kiln=$scratch
else
  kiln=${1:?usage: refresh-admin-ui.sh <kiln worktree> | --export <kiln repo> <commit>}
  kiln=$(cd "$kiln" && pwd)
  if [ -n "$(git -C "$kiln" status --porcelain -- launcher ':!launcher/packages/admin-ui/dist' ':!launcher/node_modules')" ]; then
    echo "$kiln/launcher has uncommitted changes: commit or stash them first (the vendored build must match a commit)" >&2
    exit 1
  fi
  commit=$(git -C "$kiln" rev-parse HEAD)
  subject=$(git -C "$kiln" log -1 --format=%s HEAD)
fi
[ -f "$kiln/launcher/packages/admin-ui/package.json" ] || { echo "no launcher/packages/admin-ui in $kiln" >&2; exit 1; }
# KWA_SLOT: an optional wrapper for heavy commands (a low-priority runner on a shared machine)
slot=${KWA_SLOT:-}
run() { if [ -n "$slot" ] && [ -x "$slot" ]; then "$slot" "$@"; else "$@"; fi; }
( cd "$kiln/launcher" && run bun install --frozen-lockfile && run bun run build:admin-ui )
src="$kiln/launcher/packages/admin-ui"
rm -rf "$dest.new"
mkdir -p "$dest.new"
cp "$src/package.json" "$dest.new/package.json"
cp -r "$src/dist" "$dest.new/dist"
find "$dest.new/dist" -name '*.map' -delete
# the bundle still names its source map; the map is not vendored
sed -i '/^\/\/# sourceMappingURL=/d' "$dest.new/dist/admin-ui.js"
bun -e '
  const { createHash } = require("node:crypto");
  const { readdirSync, readFileSync, statSync, writeFileSync } = require("node:fs");
  const { join, relative } = require("node:path");
  const [dir, commit, subject] = process.argv.slice(1);
  const walk = (d) => readdirSync(d).flatMap((n) => (statSync(join(d, n)).isDirectory() ? walk(join(d, n)) : [join(d, n)]));
  const files = Object.fromEntries(walk(dir).sort().map((f) => [relative(dir, f), createHash("sha256").update(readFileSync(f)).digest("hex")]));
  writeFileSync(join(dir, "VENDOR.json"), JSON.stringify({ package: "@kiln/admin-ui", kilnCommit: commit, kilnSubject: subject,
    built: "bun run build:admin-ui (launcher/)", files }, null, 2) + "\n");
' "$dest.new" "$commit" "$subject"
rm -rf "$dest"
mv "$dest.new" "$dest"
echo "vendored @kiln/admin-ui from kiln $commit into ${dest#$here/}"
