#!/usr/bin/env bash
# kctl.sh <lab> <name> <command...>: one Kiln control-folder command in a lab; prints its result json.
#   tools/kctl.sh webadmin-step0 rt reload-test kilnwarden 5
C=$HOME/srv/enshrouded/labs/${1:?lab}/kiln/control
n=${2:?name}; shift 2
rm -f "$C/$n.result.json"
echo "$*" > "$C/$n.cmd"
for i in $(seq 1 1200); do [ -s "$C/$n.result.json" ] && { cat "$C/$n.result.json"; echo; exit 0; }; sleep 0.25; done
echo "no result after 300 s" >&2; exit 1
