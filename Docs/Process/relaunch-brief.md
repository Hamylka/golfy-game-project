# Relaunching a subtask: when and how

Why: every call re-reads the whole context, so a subtask at 400k costs about three times more per call than a fresh one at 150k. The token audit of 4-12 Nov 2026 estimated the net saving at about 14-20% on the heaviest sessions (F3.3, M4 4.4), and none on short ones. A relaunch costs roughly 230k token-equivalents (fresh base context, reloading files, writing the brief), so it only pays when about 15 or more calls of work remain.

## When to relaunch (subtask, soft limit 300k)

Relaunch only when all of these hold:
1. The context is past 300k (`get_usage`, session `self`).
2. More than about 15 calls of work remain.
3. You are at a natural boundary: a step is finished and committed, or just before a long test run, or a decision has just been written to `Docs/Decisions/` or the plan.
4. No reasoning lives only in the conversation. If some does (a failing rule being tuned, a measurement being interpreted), write it into the brief first, in the "Decisions and why" part.

Otherwise finish the task here. The hub's limit is 350k and has no soft zone: at the limit, the hub writes its own hand-over the same way and a new hub takes over.

## Procedure

1. The outgoing subtask writes the brief below to `Docs/Plans/handoff-<branch-slug>.md` (not committed unless the director asks) and messages the hub with its path. It commits or stashes nothing it did not decide to.
2. The hub reads the brief, fixes what is wrong, then starts a fresh subtask (`start_session`, context "fresh", same branch, `use_worktree: false` only if the original did the same) whose prompt is the brief followed by the two lines "Read the brief, then reply with your understanding of the state and the next step in five lines, and wait." The hub checks the reply before saying go.
3. Once the new subtask has confirmed, the hub archives the old one and deletes the handoff file when the PR is merged.

## The brief (fill every part, keep it under about 80 lines)

```
# Handoff: <task title> (<branch>, PR <number or none>)

## Goal
One paragraph: what this task delivers and what "done" means (acceptance, tests to run, PR description requirements).

## State now
- Branch, last commit (hash + message), uncommitted files and why.
- Steps done / in progress / not started (a short checklist).
- Tests: which were run (names, counts, durations), which pass, which fail and why (paste the failing assertion in one line).

## Decisions and why
Each decision on one line: the choice, the reason, the rejected alternative. Include director answers verbatim (quote them) and any number you tuned with the value you landed on. Mark anything that is NOT written in a Docs/Decisions file or the plan.

## Where to look
Files to read first, with line ranges when it matters (e.g. `FairwayShapePlacer.cs` lines 120-260). Files NOT to re-read (already understood, summarised here in one line each).

## Next steps
Ordered, concrete, each with the command or test to run and what a good result looks like.

## Traps
What cost time here and would again: a flaky test, a slow rule test (give its duration), a Unity quirk, a decision that looks wrong but is deliberate.

## Rules that still apply
CLAUDE.md applies as usual. Reminders: targeted tests only (names, never groups); announce runs over 2 minutes; context soft limit 300k, ask the hub before continuing past it again.
```

## Prompt for the outgoing subtask

> Context is past 300k and about N calls of work remain. Write the relaunch brief to `Docs/Plans/handoff-<branch-slug>.md` following `Docs/Process/relaunch-brief.md`, including every decision whose reasoning is only in this conversation. Then message the hub with the path and stop.
