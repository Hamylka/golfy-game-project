# CLAUDE.md: Iso Golf

Guidance for Claude Code working on this repository. Read this file and `ART_DIRECTION.md` before any task.

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
- **Play axis:** the tee is placed toward world −X, the hole toward world +X. A shot travels up the screen, from bottom-left to top-right.
- **Camera:** perspective, 45° vertical field of view, rotation about (40, 80, 0). It never rotates during gameplay. These conventions take precedence over any looser camera wording in `ART_DIRECTION.md`.
- **Screen:** forced portrait, with long, narrow islands. Par 3 courses fit entirely in one view. Longer courses (par 4, par 5, possibly more) are only partly visible, and the camera moves along the course during play without rotating.

### Code

- C#, English only (names, comments, commits).
- Root namespace `IsoGolf`, sub-namespaces per folder (`IsoGolf.Gameplay`, `IsoGolf.Generation`, `IsoGolf.Rendering`, `IsoGolf.UI`).
- One assembly definition per top-level script folder.
- `[SerializeField] private` fields, no public mutable fields.
- Tunable values live in **ScriptableObjects** (`ShotSettings`, `LevelGenSettings`, `PaletteSettings`), never as magic numbers.
- Keep MonoBehaviours thin; put logic in plain C# classes that can be unit-tested.
- **Determinism:** procedural generation uses a seeded `System.Random` passed explicitly. Never use `UnityEngine.Random` in generation code. The same seed must always produce the same level.

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

- **Camera (`Rendering`):** fixed-rotation perspective camera, rendering at native resolution with MSAA. It frames a par 3 island whole on any portrait screen, and moves along longer courses. The player moves around with a one-finger drag and zooms with a pinch, within the island. A hold and drag that starts on the ball begins the shot instead. Camera gestures are blocked while aiming and allowed during flight.
- **Ball (`Gameplay`):** the flight follows a predictable bell-shaped arc computed from aim direction and power (ballistic solution), then hands over to physics for bounce and roll. The aim preview must match the real flight exactly.
- **Ball shadow:** a separate ground-projected blob that always sits on the surface under the ball. It is essential for reading height; never remove it.
- **Shot input (`Gameplay`):** behind an `IShotInput` interface. The gesture starts with a **hold and drag on the ball**; touches elsewhere move the camera. Keep it swappable.
- **Aim arc and power gauge (`UI`):** dotted arc following the shot curve, landing marker on the ground, small yellow gauge above the ball. Nothing else on screen during a shot.
- **Level generator (`Generation`):** seed → island outline (noise) → surfaces (fairway, green, rough, bunkers, water) → props placed by rules (clusters, low density, never on the main shot line) → tee at −X, hole at +X. Follow the *Procedural kit* section of `ART_DIRECTION.md`.
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
2. **Shot prototype:** ball, arc flight, shadow, aim preview, power gauge, placeholder input.
3. **Island generator:** seeded outline, tufted grass edge, surfaces, tee and hole placement.
4. **Props and art pass:** modular kit, placement rules, ambient life (foliage, flag, water, birds).
5. **Progression:** level flow, transitions, seed sequence.

## Don'ts

- No clubs, golfer avatar, crowds or spectators.
- No pixelation, no camera rotation, no bloom, no depth of field.
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
