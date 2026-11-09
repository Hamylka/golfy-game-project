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
- **Screen:** forced portrait, with long, narrow islands. Par 3 courses fit entirely in one view when fully zoomed out, and every course starts closer than that, at a default zoom. Every par zooms out to the same scale, set by the island's width, so the ball and props are the same size on every course. Fully zoomed out, a longer course (par 4, par 5) shows mostly whole as an overview, its far end small; at the default zoom it is only partly visible, and the camera moves along the course during play, turning only at rest.

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

- **Camera (`Rendering`):** perspective camera with a fixed pitch, rendering at native resolution with MSAA. Fully zoomed out, it frames a par 3 island whole on any portrait screen, at the same width-set scale on every course. A level starts at a closer default zoom, with the ball low on screen. The player moves around with a one-finger drag, zooms with a pinch, and turns the view with a two-finger twist, up to ±45° around the automatic heading. The view's centre stays above the island, with about 3 m of margin, at every zoom and heading (this replaces PR 33's zoomed-out corridor along the centreline and the axis-aligned limits).
- **Camera heading (`Rendering`, decided in the M3 camera rotation plan):** the camera turns itself only at rest: while it eases onto a stopped ball, and on the reset view. It faces the hole when the hole is within a full-power shot (from `ShotSettings`), otherwise the course ahead: the direction of the centreline from the ball's spot on it to a look-ahead distance further on (`CameraRigSettings.headingLookAhead`, 16 m, about half a full shot), stopped at the hole end, then turned toward the hole by at most `holeHeadingFarTurn` (20°), so a distant hole leans into view without the view looking across the void beside a dogleg; on the tee (start view, lost tee shot) it faces the course ahead only. Mid-leg the course ahead is the leg's heading; a ball resting short of a bend faces mostly the next leg, turning gradually with no threshold (decided in F1, `fix/m3-camera-heading-par5-seed6`). During flight it only translates. A twist made by hand lasts until the next shot or the reset view. The reset view also clears the twist. A drag that starts on the ball begins the shot instead. A camera drag keeps gliding and slows down after release. Camera gestures are blocked while aiming and allowed during flight. During a shot the camera stays still until the ball reaches the middle of the screen, then glides to keep it between about 40% and 60% of the screen's height and width, and at rest eases until the ball sits low on screen with the course ahead. A finger on the screen during flight pauses this follow; the camera eases back to the ball once the ball has stopped and the finger has lifted.
- **Approach framing (`Rendering`, decided in PR 3d', `feature/m3-approach-framing`):** for short shots the camera comes closer, with no fixed threshold. At rest it zooms just enough to put the hole about 62% up the screen, the ball staying at its rest spot 30% up, which keeps room below it for a full pull; the heading faces the hole, so the hole sits above the ball, with the convention's slight lean to the right. Further away (beyond about 10 m on `Main`) that zoom is the default zoom, and the closest zoom is reached around 4 m. It applies like the heading: only at rest, while easing onto a stopped ball, and on the reset view, never in flight. It wins over a manual zoom, which lasts until the next shot. The twist never changes the zoom, and a twist or a finger during the rest ease stops the zoom where it is.
- **Ball (`Gameplay`):** away from the green, the flight follows a predictable bell-shaped arc computed from aim direction and power (ballistic solution), then hands over to bounce and roll. The aim preview must match the real flight exactly. **On the green, every shot is a putt:** the ball rolls along the ground, with no flight and no aerial arc. The green's drawn border is the strict limit: just outside it, a shot is a short arc. The ball reacts to slopes everywhere: it rolls downhill, curves on a side slope, travels further going down and stops sooner going up. Rolling must not bump on terrain facets, so it uses smooth Catmull-Rom curves over the terrain grid, not the visible triangles. The ball sits on a curve through the visible vertices, and feels the slope of a second curve, which on the fairway and green leaves out the facet noise, so facets can read well without pushing the ball around. A ball in the water, off the island or in a gap between islands returns to where the shot was played after about a second. At the hole, a central line is captured at higher speed than one near the rim, and a ball too fast for its line lips out, with no randomness.
- **Ball shadow:** a separate blob that always sits on the surface under the ball, as a small disc mesh following the facets. It needs no depth texture, so no URP decal. It is essential for reading height; never remove it.
- **Shot input (`Gameplay`):** behind an `IShotInput` interface. The gesture is a **drag that starts on the ball, pulled back like a slingshot**, with no hold time by default. Releasing back on the ball cancels. Touches elsewhere move the camera. Keep it swappable.
- **Aim arc and power gauge (`UI`):** dotted arc following the shot curve, landing marker on the ground, small yellow gauge above the ball. On the green, the arc and landing marker are replaced by a putt preview: dots along the first part of the putt's true path, about 40%, with no end marker. Nothing else on screen during a shot.
- **Level generator (`Generation`):** (seed, par) → centreline → island outlines and gaps (noise) → elevation → surfaces and hazards (fairway, green, rough, bunkers, water) → tee and hole → checks → props placed by rules (M4: clusters, low density, never on the main shot line). The output is a plain C# `CourseLayout`, read by the terrain, the ball's ground and the camera; tunable ranges live in `LevelGenSettings`. Follow the *Procedural kit* section of `ART_DIRECTION.md`. Decided in the M3 plan:
  - **Lengths, tee to hole:** par 3 18–26 m, par 4 40–55 m, par 5 65–85 m. A full-power shot ends about 30 m away on flat fairway, and a par 3 never sits within 3 m of that.
  - **Shape:** par 3 is straight. About half of par 4s and 5s have a dogleg of up to 35°, and a par 5 may bend twice; the others get soft bends. Every course keeps heading toward +X.
  - **Gaps:** about one par 4 or 5 in three has one gap, 4–10 m wide, across the whole course and never right in front of the tee; now and then a par 3 has an island green. A gap is open sky, crossed only in the air, and a ball landing or rolling into it is lost like a ball off the island. There is always a lay-up spot from which the gap clears with 70% power or less.
  - **Elevation:** the fairway rises and dips up to about ±1.2 m, slopes up to about 10%; the green sits up to 1.5 m above or below the tee and tilts 2–6%. Under 1% across at least 1.5 m around the hole, and flat around the tee.
  - **Strategy:** risk and reward. The green has a hazard on the side the direct line comes from, or in front; a dogleg corner or long drive zone has a hazard to carry to cut it; a safe route always exists. 2–4 hazards on a par 3, up to 3–6 on a par 5, never on the tee or at the hole.
  - **Checks, automatic:** a ball can come to rest near the hole; every gap clears from its lay-up spot; on every par 3 the hole-in-one sweep (`ShotSweep`) stays under 1%, about 0.5% on average over seeds, and full power never holes. A seed that fails moves on to its next variant, deterministically.
  - **Where the checks run (decided in M3 PR 7):** `Gameplay`'s `CheckedCourse.Generate` runs the cheap checks when a course is generated, on the phone too, with the real `BallSimulation`: a complete `CoursePlan`; every safe-route leg across a gap brings a ball to rest past it with lay-up power or less; from the safe route's last rest spot some approach (aims −1°, 0, +1°, powers 0.02-1.00) holes or stops on the green within 2 m of the hole. It tries variants 0, 1, ... up to 8, and if none passes keeps the one failing the fewest checks (the lowest on a tie) with a warning. The ace sweep costs seconds per course, so it never runs on the phone: the ace rate is held by `ShotSettings` tuning (green restitution 0.42) and gated by tests. A baked table of variants may come in M5 over the shipped seed sequence.
  - **Hazards (decided in the M3 PR 6 plan, built in PR 6b):** placed by role, required roles first (`CoursePlan` records each hazard's `HazardRole`, the shot lines and any role that found no place). A gap counts as one hazard; the green guard (sand or, 30%, water) sits on the side the direct line comes from; each dogleg corner that can be cut with 90% power gets a hazard the cut shot has to carry; a straight par 4 or 5 gets a hazard across the drive (reaching over the centreline, carry it or lay up); a second bunker beside the green and flanks beside the safe route's rest spots fill up to the drawn count (par 3 2-4, par 4 3-5, par 5 3-6). Water about one hazard in five overall. The hole stays where the centreline ends; the green moves 0.5-1 m away from its guard, so the hole sits toward the guarded side. A **safe route** always exists by construction: rest spots along the centreline from the tee to the middle of the green's safe half, each with a clear landing capsule, steps of at most 24 m, a gap crossed from a lay-up spot within 70% power, at most par shots; it is recomputed for every candidate hazard and a candidate that breaks it is rejected. The green, pin and hazards draw from their own random stream (the surfaces stream), so tuning them never moves an island or a slope, and every accept/reject test compares whole centimetres.
  - **Leaves room for:** a shot corridor and prop zones for M4 props and ball collisions; edge segments tagged by the surface beside them, for no tufts where water meets the edge and a later waterfall; the course's +X end, where M5 places the next island. M5 picks the par for each seed.
- **Course container (`Rendering`):** everything on the course lives under one `Course` object, including the ball from M2, and `CourseFloat` moves it gently up and down. The motion is visual only: the ball's simulation runs in the container's local space and never reads the offset, so a shot lands the same wherever the course is in its motion. The ball therefore uses no Unity physics on world-space colliders. The camera and clouds stay outside the container. All islands of a course float together.
- **Level flow (`Gameplay`):** hole reached → transition → next seed. The next island appears toward +X.

## Workflow

1. **Plan first** for anything touching more than one system: list the files and steps, then wait for approval.
2. Work in **small, testable steps**. One feature per step.
3. After each step: console clean, Play Mode check, short summary of what changed and how to test it.
4. Suggest a commit message at the end of each step (conventional style: `feat:`, `fix:`, `refactor:`, `art:`).
5. Prefer a `Sandbox_*` scene for experiments before touching `Main.unity`.
6. If something is ambiguous (game design, art direction, gesture), ask instead of guessing.

## Milestones

1. **Low poly pipeline sandbox:** camera framing, faceted terrain with clean surface borders, lighting and shadows, on a test island. Validate the look and performance on the phone.
2. **Shot prototype:** ball, arc flight, putting on the green, rolling on slopes, shadow, aim preview, power gauge, placeholder input.
3. **Island generator:** seeded outline, elevation by seed and par (fairway rises and dips, sloped greens), tufted grass edge, surfaces, tee and hole placement. Doglegs on long courses, with a camera that turns with the course at rest and a two-finger twist, and gaps as separate islands that belong to the course. The hand-made par 3 (`CourseShapeSettings`, `Par3CourseShape`) stays as a fixed test course in `Sandbox_Par3` (with the hand-placed trees) and in tests; the hand-made gap and dogleg shapes stay as test fixtures only (no scene). `Main` plays a generated, checked course (`LevelBootstrap` calls `CheckedCourse.Generate`), with a seed and par in the Inspector until M5, and no trees until M4's placement rules.
4. **Props and art pass:** modular kit, placement rules, ambient life (foliage, flag, water, birds).
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
