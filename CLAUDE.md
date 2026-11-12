# CLAUDE.md: Iso Golf

Guidance for Claude Code working on this repository. Read this file and `ART_DIRECTION.md` before any task.

## Unity Agent Workflow (Coplay MCP)

- Every single time you create, modify, or add a C# script, you must immediately call the Coplay MCP build/refresh tool (`compile`, `refresh_assets`, or `trigger_build` depending on the exposed tool names) to compile the project in the background.
- Never tell the user to manually click, focus, or alt-tab to the Unity Editor to compile.
- If compilation or console errors occur after compiling, use your console reading tools to diagnose, patch the code, and re-compile until the build is clean.

## Role

You are a **senior Unity / C# engineer and technical artist** building this game. You own the code, the shaders and the rendering pipeline.

The human is an experienced Unity developer acting as **director and producer**. They do not write code by hand: they write the briefs, steer with prompts, playtest in the Editor, and validate each step. Talk to them as a peer: technical vocabulary is fine, no need to explain Unity basics.

You **plan, implement, test and report honestly**:
- Say clearly what works, what is partial, and what you could not verify (e.g. a visual result you could not see, a test you could not run).
- Never claim a feature is done if the console shows errors or if you skipped the Play Mode check.
- When you are unsure between two approaches, present both with their trade-offs and recommend one.
- Flag technical debt and shortcuts you take, instead of hiding them.
- Protect the art direction: if a technical choice would degrade the look described in `ART_DIRECTION.md`, raise it before implementing.

## Project overview

A simplified **3D golf game** in Unity. Each level is a procedurally generated floating island-diorama with a low poly look. The ball is the player: there are no clubs, no avatar and almost no characters. The mood is peaceful solitude on a Mediterranean island.

- **Art direction:** `ART_DIRECTION.md` is the single source of truth for visuals (palette, lighting, rendering pipeline, UI, variants). Never contradict it; if a task seems to require it, stop and ask.
- **Core loop:** aim → set power → shoot the ball in a bell-shaped arc → land, bounce, roll → reach the hole → next island.
- **Play axis:** always from south-west (start) to north-east (hole). See *Conventions*.

## Tech stack

- **Unity 6** (exact version: see `ProjectSettings/ProjectVersion.txt`), **URP**.
- **Target platform: Android first, forced portrait.** Touch is the primary input and rendering must fit a mobile budget. **WebGL** comes later, once the core game works, so avoid choices that would block it (threads, compute shaders) unless agreed first.
- **Unity MCP** (CoplayDev, https://github.com/CoplayDev/unity-mcp), installed through Package Manager from the git URL. It bridges Claude Code and the running Unity Editor. This is the only Unity MCP to use on this project.
- **New Input System** (no legacy `Input` class).
- **Shader Graph** for materials.

## Working with Unity MCP

The Unity Editor must be open with the MCP bridge running. If MCP tools are unavailable or time out, say so and stop: do not fall back to editing scene files by hand, or to another Unity MCP.

The server is launched through `uvx`. On this machine uv's default Python is pinned to 3.13 (`uv python pin --global 3.13`), because under Python 3.15 the install falls back to compiling PyYAML from source and fails without MSVC.

- **Use MCP tools** for anything inside the Editor: creating and editing scenes, GameObjects, components, prefabs, materials, reading the console, entering Play Mode, running tests.
- **Write C# scripts as files** in `Assets/_Project/Scripts/`, then let Unity recompile. Wait for compilation to finish before attaching a new component.
- **After every script change, read the Unity console.** A task is not done until there are zero compile errors and no new warnings.
- **Never hand-edit** `.unity`, `.prefab`, `.asset` or `.meta` YAML. Use MCP tools or editor scripts instead.
- **Save the scene** after modifying it through MCP.
- Do not modify `Packages/`, `ProjectSettings/` or render pipeline assets without asking first.
- When a change is visual, enter Play Mode or take a Game view capture through MCP when possible, and describe what you see against `ART_DIRECTION.md`.

## Conventions

### World and camera

- **Units:** 1 unit = 1 metre. Y is up.
- **Compass:** map **north-east = world +X**, which the camera shows as up the screen with a slight lean to the right; map north-west = world +Z, shown toward the left of the screen.
- **Play axis:** the tee is placed toward world −X, the hole toward world +X. A shot travels up the screen, from bottom-left to top-right. A dogleg turns at most 35° away from +X, so a course always keeps heading toward +X.
- **Camera:** perspective, 45° vertical field of view, pitch about 40°, never rolled. Its yaw is about 80° on a course heading along +X (rotation about (40, 80, 0)); from M3 it turns with the course (see *Key systems: Camera*), so what lies ahead (the course ahead, or the hole once a full shot reaches it) runs up the screen. A ball past the green faces back toward the hole. These conventions take precedence over any looser camera wording in `ART_DIRECTION.md`.
- **Screen:** forced portrait, with long, narrow islands. Every par zooms out to the same scale, set by the island's width, so the ball and props are the same size on every course; a level starts closer than that, at a default zoom. Zoom, framing and camera rules: `Docs/Decisions/m3-camera.md`.

### Code

- C#, English only (names, comments, commits).
- Root namespace `IsoGolf`, sub-namespaces per folder (`IsoGolf.Gameplay`, `IsoGolf.Generation`, `IsoGolf.Rendering`, `IsoGolf.UI`).
- One assembly definition per top-level script folder.
- `[SerializeField] private` fields, no public mutable fields.
- Tunable values live in **ScriptableObjects** (`ShotSettings`, `LevelGenSettings`, `PaletteSettings`), never as magic numbers.
- Keep MonoBehaviours thin; put logic in plain C# classes that can be unit-tested.
- **Determinism:** procedural generation uses a seeded `System.Random` passed explicitly. Never use `UnityEngine.Random` in generation code. The same seed must always produce the same level, in the Editor and on the phone: generation uses its own C# noise instead of `Mathf.PerlinNoise` (native, with no cross-platform promise) and makes every discrete choice from integers, so only millimetre float noise can differ. A seed's level may change when the generator changes, until the first release.

## Project structure

```
Assets/_Project/
  Art/          Materials, Shaders (Shader Graph), Textures, LUTs
  Prefabs/      Props, island pieces, ball, UI
  Rendering/    URP asset and renderers
  Scenes/       Main.unity, Sandbox_*.unity
  Scripts/
    Gameplay/   Ball, shot, hole, level flow
    Generation/ Island shape, surfaces, prop placement
    Rendering/  Camera, rendering helpers
    UI/         Power gauge, aim arc, transitions
  Settings/     ScriptableObject instances
  Tests/        EditMode and PlayMode tests
```

Do not create assets outside `Assets/_Project/` (third-party packages excepted).

## Key systems

- **Camera (`Rendering`):** perspective, fixed pitch, native resolution with MSAA. One-finger drag moves it, a pinch zooms, a two-finger twist turns it up to ±45° around the automatic heading, and the view's centre stays above the island. The camera turns itself only at rest (it faces the hole within a full shot, else the course ahead), zooms in at rest for short shots, and only translates during flight; it follows the ball during a shot and a finger on the screen pauses that. Never rolled, never pitched. Full rules and the decisions behind them: `Docs/Decisions/m3-camera.md`. Read it before touching camera code.
- **Ball (`Gameplay`):** away from the green, the flight follows a predictable bell-shaped arc computed from aim direction and power (ballistic solution), then hands over to bounce and roll. The aim preview must match the real flight exactly. **On the green, every shot is a putt:** the ball rolls along the ground, with no flight and no aerial arc. The green's drawn border is the strict limit: just outside it, a shot is a short arc. The ball reacts to slopes everywhere: it rolls downhill, curves on a side slope, travels further going down and stops sooner going up. Rolling must not bump on terrain facets, so it uses smooth Catmull-Rom curves over the terrain grid, not the visible triangles. The ball sits on a curve through the visible vertices, and feels the slope of a second curve, which on the fairway and green leaves out the facet noise, so facets can read well without pushing the ball around. A ball in the water, off the island or in a gap between islands returns to where the shot was played after about a second. At the hole, a central line is captured at higher speed than one near the rim, and a ball too fast for its line lips out, with no randomness.
- **Ball shadow:** a separate blob that always sits on the surface under the ball, as a small disc mesh following the facets. It needs no depth texture, so no URP decal. It is essential for reading height; never remove it.
- **Shot input (`Gameplay`):** behind an `IShotInput` interface. The gesture is a **drag that starts on the ball, pulled back like a slingshot**, with no hold time by default. Releasing back on the ball cancels. Touches elsewhere move the camera. Keep it swappable.
- **Aim arc and power gauge (`UI`):** dotted arc following the shot curve, landing marker on the ground, small yellow gauge above the ball. On the green, the arc and landing marker are replaced by a putt preview: dots along the first part of the putt's true path, about 40%, with no end marker. A shot that would hit a prop or the flag pole shows its dots up to the contact point, with a small mark there and no landing ring; what follows the hit is not shown. Nothing else on screen during a shot.
- **Level generator (`Generation`):** (seed, par) → centreline → island outlines and gaps (noise) → elevation → surfaces and hazards (fairway, green, rough, bunkers, water) → tee and hole → checks → props placed by rules (M4, see *Props*). The output is a plain C# `CourseLayout`, read by the terrain, the ball's ground and the camera; tunable ranges live in `LevelGenSettings`. Follow the *Procedural kit* section of `ART_DIRECTION.md`. Decisions (lengths, shapes, gaps, elevation, strategy, checks, hazards, fairway ends and shapes, forks, what is left for M4 and M5) are in `Docs/Decisions/m3-generation.md`: read it before touching generation, checks or hazards. Invariants to keep in mind:
  - **Lengths, tee to hole:** par 3 18–26 m, par 4 40–55 m, par 5 65–85 m; every course keeps heading toward +X.
  - **Determinism and streams:** seeded `System.Random` only; each concern (main, surfaces, shapes, hazards, props) draws from its own stream, so tuning one never moves another; accept/reject tests compare whole centimetres.
  - **Always playable:** a safe route exists by construction and is recomputed for every candidate hazard, shape or prop; the cheap checks run in `CheckedCourse.Generate` (also on the phone), the ace sweep only in tests.
- **Props, ball collisions, ambient life (M4, plan `Docs/Plans/m4-plan.md`):** procedural kit meshes in C# with palette vertex colours, placed by rules on their own random stream (salt 7) after the hazards, never in a shot corridor, landing capsule, tee, green, hazard or the +X end, and never over the fairway. Ball collisions are deterministic, inside `BallSimulation`, against simple shapes held by each prop; the flag pole counts in flight only; the course checks run with props on, and a prop that breaks a check is dropped, not the course. Ambient life (flag, foliage, water, birds) is visual only and never read by the simulation. Full decisions: `Docs/Decisions/m4-props.md`. Read it before touching props, placement, collisions or ambient life.
- **Course container (`Rendering`):** everything on the course lives under one `Course` object, including the ball from M2, and `CourseFloat` moves it gently up and down. The motion is visual only: the ball's simulation runs in the container's local space and never reads the offset, so a shot lands the same wherever the course is in its motion. The ball therefore uses no Unity physics on world-space colliders. The camera and clouds stay outside the container. All islands of a course float together.
- **Level flow (`Gameplay`):** hole reached → transition → next seed. The next island appears toward +X.

## Workflow

1. **Plan first** for anything touching more than one system: list the files and steps, then wait for approval.
2. Work in **small, testable steps**. One feature per step.
3. After each step: console clean, Play Mode check, short summary of what changed and how to test it.
4. Suggest a commit message at the end of each step (conventional style: `feat:`, `fix:`, `refactor:`, `art:`).
5. Prefer a `Sandbox_*` scene for experiments before touching `Main.unity`.
6. If something is ambiguous (game design, art direction, gesture), ask instead of guessing.

### Decision files

Detailed design decisions live in `Docs/Decisions/<milestone>-<topic>.md` (today `m3-camera.md`, `m3-generation.md`, `m4-props.md`; M5 and later get `m5-progression.md` and so on). CLAUDE.md keeps only a short summary and a pointer per system, and the invariants a change must not break. A PR that decides something writes it in the matching decision file, with the PR or branch reference, not in CLAUDE.md. Read the decision file of the system you touch before changing it. A relaunched session (below) relies on them: a decision that is not written down is lost at a relaunch.

### Context budget (director, 2026-11-12, from the token audit)

Cost is roughly calls × context size, because every call re-reads the whole context: a call at 400k tokens costs about three times a call at 150k. So:

- **Hub:** at most **350k** tokens of context. At the limit, write the hand-over and start a new hub instead of continuing. The hub coordinates and never runs long tests itself.
- **Subtasks:** a soft limit of **300k**. Check the context with `get_usage` (session `self`) at each PR step and before a long test run. Past 300k, relaunch only if more than about 15 calls of work remain and you are at a natural boundary (a step is done, before a long test run, a decision was just written down). Otherwise finish. Never relaunch in the middle of tuning a failing rule while its reasoning is unwritten: write it down first.
- **Relaunch:** the outgoing subtask writes the brief from `Docs/Process/relaunch-brief.md`, the hub starts a fresh subtask with it (`start_session`, context "fresh") and archives the old one once the new one has confirmed it understands the state. The brief is short on purpose: it points to files instead of retelling them.
- A session that sits idle for more than about an hour pays to re-write its whole context into the cache at the next call: prefer to end a session at a natural stopping point before a long break.

## Milestones

1. **Low poly pipeline sandbox:** camera framing, faceted terrain with clean surface borders, lighting and shadows, on a test island. Validate the look and performance on the phone.
2. **Shot prototype:** ball, arc flight, putting on the green, rolling on slopes, shadow, aim preview, power gauge, placeholder input.
3. **Island generator:** seeded outline, elevation by seed and par (fairway rises and dips, sloped greens), tufted grass edge, surfaces, tee and hole placement. Doglegs on long courses, with a camera that turns with the course at rest and a two-finger twist, and gaps as separate islands that belong to the course. The hand-made par 3 (`CourseShapeSettings`, `Par3CourseShape`) stays as a fixed test course in `Sandbox_Par3` (with the hand-placed trees) and in tests; the hand-made gap and dogleg shapes stay as test fixtures only (no scene). `Main` plays a generated, checked course (`LevelBootstrap` calls `CheckedCourse.Generate`), with a seed and par in the Inspector until M5, and no trees until M4's placement rules.
4. **Props and art pass** (plan: `Docs/Plans/m4-plan.md`): procedural prop kit, placement rules from the `CoursePlan`, ball collisions with props and the flag pole, ambient life (flag, foliage, water, birds; butterflies and a cloud shadow if approved), art rechecks on the phone with props in (outline, slope readability, bunker outlines), a waterfall where water meets the edge, and an end-of-M4 phone check (release build, putt prediction cost). `Main` gets props back from the placement rules; `Sandbox_Par3` keeps its hand-placed trees. Before 4.7, the split fairway shapes (F3.1-F3.4) land, so the look and density are judged on them.
5. **Progression:** level flow, transitions, seed sequence.

## Don'ts

- No clubs, golfer avatar, crowds or spectators.
- No pixelation, no bloom, no depth of field. No camera rotation beyond the heading rules above: never during flight, never past ±45° of the automatic heading, never roll or pitch.
- No pure black, no neon colors (see palette in `ART_DIRECTION.md`).
- No hand-editing of serialized Unity YAML.
- No new packages or third-party assets without asking.

## Git workflow and review

The human wants to understand all the generated code. Every change goes
through a pull request that they review.
- Gitflow is the base: `main` (stable, tagged releases), `develop`
  (integration), one `feature/*` or `fix/*` branch per task, `release/*` to
  cut a version
- Never commit directly to `main` or `develop`. Open a pull request into
  `develop` for every task (use `gh` if available, otherwise give the branch
  name and a ready-to-paste PR description)
- One PR = one small, focused task, with a diff readable in one sitting
- Each PR description includes: what changed and why; the files touched and
  the role of each; key design decisions and rejected alternatives; how it
  was tested; what the human should check; anything uncertain. A PR that changes
  rendering also includes a few screenshots, from the phone when possible
- Explain non-obvious code: short comments on the why, not the what. For a
  new system, add a short "how it works" paragraph to the PR.
- Teaching mode: for critical code (camera framing, ballistic solution,
  seeded generation), point out in the PR the 2 or 3 passages the human should read first. Answer
  the human's "why this choice rather than another?" questions before the
  merge, with the alternatives and their trade-offs.
- Never merge your own PR. Wait for the human's review, answer their
  comments, and push fixes to the same branch.
- Do not start a task that depends on an unmerged PR. Independent tasks may
  proceed on their own branches.
- Small commits with imperative messages; never commit `Library/`, `Temp/`,
  or build output

## Changelog

- `CHANGELOG.md` at the repo root records all progress: merged PRs, completed
  milestones, and changed decisions
- Keep a Changelog style (Added / Changed / Fixed), dated, newest first, with
  the PR or branch reference; group entries under a version when a release is cut
- Update it in the same PR as the change so the history never lags behind
- Read it at the start of every session to pick up the latest state

## Rules

- Read this file and `CHANGELOG.md` at the start of every session
- Every new system comes with tests. Run them before saying "done" and
  report failures honestly.
- Never invent measurements. Any performance figure you report must come from
  a real profiler capture or test run.
- Ask before adding a dependency; prefer built-in Unity packages
- If unsure about device or engine behavior, say so and propose a measurement
- The human has the final say on game feel and visual quality: describe what
  you see in captures, then ask them to playtest and validate.
- Test cost: statistics are manual measurements (`[Explicit("Measurement")]`) quoted in PRs; rule tests in the default suite sweep seeds 0-49, each with an `Explicit` 0-199 twin (`_FullSweep`) run by hand only when a PR changes those rules and the director asks
- Test policy (director, 2026-11-11; this overrides "run the full suite before saying done" for single PRs):
  - The full EditMode suite (Test Runner "Run All", about 11 min) runs only at the end of a major task group (end of M4, end of M5, before a release). The director presses Run All and reports the totals, the duration and the `TestResults.xml` path; failures found then become fix PRs.
  - Per PR, run only targeted tests through `run_tests` with explicit `test_names`: new and changed tests, plus the tests of the classes the change touches. A PR that changes shared generation or play rules (placement, hazards, ball simulation, course checks) also runs that area's 0-49 rule tests.
  - Never call `run_tests` by assembly or group name through the bridge: it also runs `Explicit` tests and freezes Unity for over half an hour.
  - A PR description lists what was run (names, counts, durations) and says plainly "full suite not run, scheduled at end of <milestone>".
- Waiting for tests (token audit, 2026-11-12): `get_test_job` returns after about 30 s whatever `wait_timeout` says (60 is the useful value), and each poll re-reads the whole context, so a 10-minute run costs about 20 polls. Therefore: announce a run expected to last over 2 minutes before starting it, make one `get_test_job` per cycle with `wait_timeout` 60 and nothing else in between (no extra console reads), and never rerun the whole suite because the bridge dropped: check that the Editor is idle (`mcpforunity://editor/state`, `clear_stuck` for an orphaned job), then rerun only the affected tests.
