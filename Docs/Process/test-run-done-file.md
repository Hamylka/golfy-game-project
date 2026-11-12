# Waiting for a Unity test run without polling

## Problem

The Unity MCP bridge writes no results file, and `get_test_job` returns after about 30 s whatever `wait_timeout` says. Each poll re-reads the agent's whole context, so a 10-minute run costs about 20 polls. In the F3 and M4 subtasks, polls were about 30% of the cache reads (token audit, 2026-11-12).

## How it works

- `Assets/_Project/Scripts/EditorTools/TestRunDoneFile.cs` (Editor only, assembly `IsoGolf.EditorTools`) registers a `TestRunnerApi` callback. When a run starts and when it finishes, from the MCP bridge or the Test Runner window alike, it writes `Temp/IsoGolfTestRun/last-run.json` (one line of JSON: `state`, start and finish times in Unix ms, passed, failed, skipped, inconclusive, duration, result state, up to 20 failed test names). It only observes a run. `Temp/` is not committed and disappears when the Editor closes.
- `Tools/wait-for-test-run.sh <since_unix_ms> [timeout_s]` sleeps in a loop (a shell, no tokens) until a run finished at or after `since`, prints the JSON, and exits 0 (no failure), 1 (failures) or 2 (timeout, default 900 s).

## Agent recipe

1. Check the Editor is idle (`mcpforunity://editor/state`).
2. In one Bash call with `run_in_background: true`:
   `since=$(date +%s%3N); Tools/wait-for-test-run.sh "$since" 900`
3. Start the run with `run_tests` and explicit `test_names`.
4. Do not poll. The harness wakes the agent once when the script exits; its output is the summary. Use `get_test_job` with `include_failed_tests` only if a test failed and the message is needed.

If the script times out (exit 2), the Editor may be busy, closed or domain-reloading: read `mcpforunity://editor/state`, use `clear_stuck` if a job is orphaned, and fall back to `get_test_job`.

## Limits

- One file, overwritten by each run: do not start two runs at once.
- A domain reload during a run loses the start time (the file then records the finish time as the start), and an EditMode run that reloads the domain may not send its finish callback. The script then times out and `get_test_job` is the fallback.
- Measured on one short run only when this was written (see the PR description).
