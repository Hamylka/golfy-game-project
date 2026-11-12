#!/usr/bin/env bash
# Waits for a Unity test run to finish without polling the MCP bridge, and prints its summary.
# Usage:  since=$(date +%s%3N);  <start the run>;  Tools/wait-for-test-run.sh "$since" [timeout_seconds]
# Run it in the background from an agent (Bash run_in_background): the harness wakes the agent once, when it exits.
# Exit codes: 0 finished with no failure, 1 finished with failures, 2 timed out (Editor busy or closed).
# Reads Temp/IsoGolfTestRun/last-run.json, written by Assets/_Project/Scripts/EditorTools/TestRunDoneFile.cs.
since="${1:?usage: wait-for-test-run.sh <since_unix_ms> [timeout_seconds]}"
timeout="${2:-900}"
root="$(cd "$(dirname "$0")/.." && pwd)"
file="$root/Temp/IsoGolfTestRun/last-run.json"
deadline=$(( $(date +%s) + timeout ))
while [ "$(date +%s)" -lt "$deadline" ]; do
  if [ -f "$file" ]; then
    json="$(cat "$file" 2>/dev/null)"
    case "$json" in
      *'"state":"finished"'*)
        finished="$(printf '%s' "$json" | sed -n 's/.*"finishedUnixMs":\([0-9]*\).*/\1/p')"
        if [ -n "$finished" ] && [ "$finished" -ge "$since" ]; then
          printf '%s\n' "$json"
          failed="$(printf '%s' "$json" | sed -n 's/.*"failed":\([0-9]*\).*/\1/p')"
          if [ "${failed:-1}" = "0" ]; then exit 0; else exit 1; fi
        fi
        ;;
    esac
  fi
  sleep 2
done
echo "wait-for-test-run: no finished run after ${timeout}s (is the Editor open and idle?)" >&2
exit 2
