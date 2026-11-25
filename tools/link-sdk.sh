#!/usr/bin/env bash
# link-sdk.sh [sdk folder]: links kiln-sdk (the NuGet source of nuget.config) to the sdk/ folder of the Kiln release
# global.json names (Kiln.Sdk; Directory.Build.props WebAdminKilnSdk must be the same version).
set -euo pipefail
cd "$(dirname "$0")/.."
ver=$(sed -n 's/.*"Kiln.Sdk": *"\([^"]*\)".*/\1/p' global.json)
sdk=${1:?usage: tools/link-sdk.sh <the sdk/ folder of the Kiln release global.json names>}
[ -f "$sdk/Kiln.Sdk.$ver.nupkg" ] || { echo "no Kiln.Sdk $ver package in $sdk (global.json names the version)" >&2; exit 1; }
[ -f "$sdk/Kiln.TestKit.$ver.nupkg" ] || { echo "no Kiln.TestKit $ver package in $sdk" >&2; exit 1; }
ln -sfn "$sdk" kiln-sdk
echo "kiln-sdk -> $sdk"
