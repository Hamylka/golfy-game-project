# M4 plan: props and art pass

Status: **draft, open questions sent to the director** (2026-11-10, `docs/m4-plan`). Decisions are folded in once answered; until then every "Recommended" below is a proposal, not a decision.

M4 brings the island to life: a small kit of low poly props placed by rules from the generator's `CoursePlan`, the ball colliding with them (and with the flag pole), ambient motion, and the art rechecks M1–M3 left for "once real props are in". It ends with a phone check on the longest par 5.

## What M4 builds on (state after M3, F1, F2)

- **Generation** outputs a plain `CourseLayout` with a `CoursePlan`: hazard roles, the shot lines (`Direct`, `Cut`, `Safe`), the safe route's rest spots (each with a clear landing capsule, `SafeRoute.CapsuleSamples`), `ShotCorridorHalfWidth` (2 m, "the strip M4 keeps clear of props"), `GuardDirection`, `SafeTarget`. Two seeded streams today: main (islands, centreline, elevation) and surfaces (salt 6: green, pin, hazards). Every accept/reject test compares whole centimetres.
- **Terrain** (`TerrainBuilder`, `TerrainView`): flat-shaded triangles with vertex colours on one land material (`LowPolyVertexColor` Shader Graph), water on its own material, tufts and trees sharing the land material. Edge segments are tagged `EdgeSurface.Land` / `Water`.
- **Trees** exist only in `Sandbox_Par3`: `TreeBuilder` (cauliflower crowns of jittered subdivided icosahedra, 7-sided cypress cones) from a hand-written `TreeSettings.Placements` list, all in one mesh, one draw call. `Main` has none on purpose (M3 PR 8). Today the ball flies and rolls **through** those trees.
- **Ball**: `BallSimulation`, fixed step, no Unity physics, in the `Course` container's local space. A flight is a `BallisticArc` whose landing is found up front (`FindLanding`); bounces are new arcs; rolling uses Catmull-Rom ground. `HoleRim` decides capture and lip-out with no randomness. The aim preview draws `ArcFor` + `PredictLanding`, and putts through `PredictPutt` (a private copy playing the shot), so it can't disagree with a played shot.
- **Flag**: `PlayPieceBuilder.BuildFlag`, pole and pennant in one static mesh (`HoleView`).
- **Checks**: `CheckedCourse.Generate` runs cheap checks with the real `BallSimulation` (complete plan, gaps clear at lay-up power, a rest near the hole), variants 0–8. The fine ace sweep (par 3 seeds 0–49: mean 0.49%, max 0.78%) runs offline in tests.
- **Phone** (Honor 500 Pro, dev build, M3 PR 9): par 5 seed 33 holds 60 fps, frame max 16.51 ms, GPU p50 9.5 ms / p99 ≤ 10.9 ms, CPU main p50 4.1–4.7 ms; generation + checks 14–37 ms, terrain build 47–220 ms. Not measured: a release build, the putt prediction cost (open since M2).

## 1. Prop kit

### Which props

| Group | Props | Role |
|---|---|---|
| Vegetation | olive tree (cauliflower crown, short trunk; today's `TreeBuilder` look), cypress (6–8-sided cone), round bush | clusters, shadows, the main life of the island |
| Stone | rocks (faceted, light stone palette, 0.3–1.2 m), round white rocks by water (§9 *Water hazards*), low dry-stone wall (rectangular blocks, slight per-block tone change) | clusters, edge accents, walls along the rough |
| Built | tile-roofed shed, bench (wood), small stone bridge (see Q2) | scale cues, "inhabited but empty" |
| Traces (§7) | rake in a bunker, bucket of balls, bicycle against a wall, laundry on a line | one or two per course at most |
| Landmark | lone chapel or lighthouse at the island's edge (see Q2) | at most one per course |

Out of M4: characters (even the single distant silhouette §7 allows), audio, time of day per level, bigger terrain features.

### How they're built

**Recommended (Q1): procedural meshes in C#**, like `TreeBuilder` and `TuftBuilder`: a small shared kit of mesh helpers (`PropMeshKit`: prism, box with slight jitter, gable roof, cone, jittered blob, block wall) writing flat-shaded triangles with palette vertex colours into `TerrainMeshData`. Each prop kind is a plain C# builder taking a seeded `System.Random`, so the same seed gives the same tree on every device, and its proportions live in a `PropKitSettings` ScriptableObject (today's `TreeSettings` proportions move there).

### Palette per material (ART_DIRECTION §3; 3–4 shades each)

- Foliage: the grass/vegetation row (`#A7B86A` `#7E9A48` `#56713A` `#34482A`), the darkest kept off most facets as today. Olive crowns lean slightly silvery: a new `OliveLight` within that row's hue, checked on the phone.
- Trunks and wood (bench, shed door, bucket, rake): `#8B5A35` `#6A4128` `#4A2C1C`.
- Rocks, walls, chapel, bridge: light stone `#F1ECE0` `#DAD3C3` `#B8B0A0`.
- Shed roof: tile `#D4875E` `#B2663F`; chapel or lighthouse roof: slate `#A3A3A0` `#7F807E` `#5C5D5C`.
- Accents only on traces (laundry: off-white, blue; bucket: red; bicycle: blue or metal `#5E625F`).
- Every new colour goes in `PaletteSettings`, never a literal in a builder.

### Rendering budget

- **Recommended (Q1): every static prop in one mesh on the land material** (as trees and tufts are today), so props add one draw call plus their shadow-caster pass; animated foliage stays in that mesh too, through a per-vertex sway weight (§4). SRP Batcher compatible, no GPU instancing needed.
- Triangle budget, to confirm by measurement in PR 4.1: an olive tree is about 400 triangles today (5 blobs × 80); a par 5 with 25 trees, 15 bushes, 20 rocks, 2 walls and 2 built props stays near 20k triangles, about the terrain's own order. Simplify the blobs (one subdivision on smaller crowns) if the phone says so.
- Targets for the end-of-M4 check (targets, not measurements): 60 fps held on par 5 seed 33, GPU p99 under 13 ms, terrain + props build under 300 ms on the phone, draw calls no more than baseline + 3.

## 2. Placement rules

New `PropPlacement` (`Generation`, plain C#), run after hazards, output `CourseLayout.Props` (kind, position, yaw, scale, collider) so the terrain view, the ball's ground and the checks all read the same list.

- **Own stream**: a third `SeededRandom`, salt 7, from (seed, par, variant), read after the surfaces stream. Tuning props never moves an island, a slope, the green or a hazard (a test compares a course with props on and off). One sub-stream per cluster, so removing a cluster leaves the others where they were. Every accept/reject test compares whole centimetres; every discrete choice is an integer draw.
- **Keep-out zones** (hard, never broken): the shot corridors (`ShotCorridorHalfWidth` either side of every `ShotLine`, see Q4 for `Direct` and `Cut`), every safe-route rest spot's landing capsule, the tee (radius 3 m), the green and 1.5 m around it except the green's guard side bunker, hazards' outlines + 0.5 m, water (except round white rocks at a pond's rim), gaps, and the **+X end** of the course (the last 6 m beyond the hole and a 60° fan around +X, kept free for M5's next island).
- **Prop zones**: rough between the corridors and the island edge, ranked by distance from every shot line. Clusters only start where the zone is at least 3 m wide.
- **Clusters, low density**: a course draws a cluster count (Q5), each cluster a centre, a theme (olive grove, cypress line along a wall, bushes and rocks, a built prop with its trace) and 2–6 members around it with a minimum spacing. Between clusters, empty ground: visual silence. Shadows count: a tree's shadow falls toward the bottom-right of the screen, so a cluster is checked against the corridor with its shadow footprint, not only its trunk, so the tee shot never starts inside a large shadow (a look rule, judged on the phone).
- **Edge props**: rocks and short walls along the island edge (Land segments only, never where `EdgeSurface.Water`), the landmark on a promontory away from the play axis.
- **Hand-made par 3**: `Sandbox_Par3` keeps its hand-placed trees (a fixed test course), now built by the kit.

## 3. Ball collisions

Inside `BallSimulation`, deterministic like `HoleRim`, against simple shapes held by each prop (vertical cylinder for trunks and the pole, sphere or squashed sphere for crowns, bushes and rocks, oriented box for walls, benches and sheds). A uniform 2 m grid over the course finds the few shapes near the ball each step.

- **Flight**: the arc's first hit is found up front, like the landing today: the ball's sphere swept along the arc against the shapes near it, at the same step the landing search uses. The arc ends at whichever comes first, landing or hit. A hit reflects the velocity about the contact normal with a per-material restitution and along-keep (`ShotSettings`: wood, stone, foliage, pole), then the ball flies a new arc, like a bounce. Crowns and bushes are **soft** (Q6): most of the speed is lost and the ball drops out under the tree.
- **Rolling**: the same shapes as 2D circles and boxes at ground level stop or deflect a rolling ball (trunks, rocks, walls, bench legs). The pole on the ground: Q8.
- **Flag pole**: a thin cylinder at the hole's centre, up to the pennant. A hit in flight takes the speed into the pole with a low restitution: a slow ball dropping onto the pole falls down beside it and `HoleRim` decides as for any ball over the hole; a fast one bounces off. So a chip-in off the pole depends on power, and no pole hit guarantees one. The rule has no randomness, so the same chip always does the same thing.
- **Aim preview** (Q7): the preview draws the same arc search, so it can stop exactly where the real ball would hit.
- **Checks**: `CourseChecks` and the ace sweep run with collisions on, because props are part of the checked course. If a prop breaks a check, the placement drops that prop (its own stream, so nothing else moves) rather than rerolling the course. The ace sweep is re-run over checked par 3 seeds 0–49 after collisions and after placement: the target stays under 1% per seed, about 0.5% mean, full power never holes. Pole hits are the likely risk; tune the pole's restitution first.

## 4. Ambient life

All motion is visual only and in the shader where it can be, so the simulation never sees it and the course float stays untouched.

| Element | How | Cost |
|---|---|---|
| Flag flutters calmly, the pole never moves | vertex animation in `LowPolyVertexColor`, weight 0 at the pole, rising to the pennant's free end (pennant and pole stay one mesh) | vertex math only |
| Foliage rustle | same shader, a small sway weight per vertex (0 on trunks, terrain, rocks, walls), slow low-amplitude noise per tree from its position | vertex math only |
| Water highlights | water material: some lighter facets (`#CFF1EE`) that fade in and out slowly, chosen per facet from a hash | vertex/fragment math only |
| Birds take off as the ball passes | 2–5 tiny faceted birds per course, perched in a cluster; when the ball flies or rolls within a few metres they flap up and away along a fixed path, then come back later; CPU-driven transforms, no physics | a few transforms |
| Butterflies | 1–3 near flowering bushes, a looping path, CPU | a few transforms |
| Cloud shadow | one large soft darkening drifting slowly across the island, as a world-space term in the terrain shader | fragment math only |

- The sway weight rides in a spare vertex channel (UV1), so every land-material mesh keeps one material and the terrain pays a multiply by zero. Normals don't move with the sway, so each facet keeps its tone and nothing shimmers (by design, given §4's crisp shadows). Shadows move with the sway because the shadow-caster pass runs the same vertex code; amplitude stays small enough that the crisp shadow edges don't swim (to check on the phone).
- WebGL-safe: no compute shaders, no threads, no GPU readback.

## 5. Art rechecks folded into M4

Done once trees, their shadows and real props are in, on the phone, through a **throwaway look-test build** that cycles options with a tap (the approach that worked in M1), probably over several rounds:

- **Outline**: none, ~1 px, ~2 px (cool dark tone, never black), on a course with props.
- **Slope readability** (director, M3 phone check: "très dur de voir les pentes"): (a) stronger facet contrast on the ground only (lower ambient share on terrain, props unchanged); (b) a subtle height tint (rough and fairway lighter on crests, darker in dips, a few percent); (c) on the green only, a tilt hint: the green's mowing grain darkening along the fall line, or a faint stripe aligned with it. ART_DIRECTION asks to judge facet shading before adding guide marks, so (a) and (b) come before (c).
- **Bunker outlines**: F2 made the colour match the drawn border; the border is still chords across a 1 m grid. Option: subdivide the grid locally to 0.5 m inside each bunker's bounds (about 4× the triangles there) if the phone still shows a polygonal bunker.
- **Waterfall** where water meets the edge (edges are tagged, Q10): a faceted falling sheet under `EdgeSurface.Water` segments, with a little foam, fading into the sky above the clouds.

## 6. End-of-M4 phone check

On the Honor 500 Pro, `Docs/Measurements/m4/README.md`, like M3 PR 9:

- Frame time with props and ambient life on the longest par 5 (seed 33, and the par 5 with the most props over seeds 0–49), autoplay as in M3, dev build with `ISOGOLF_MEASURE`.
- The same autoplay in a **release build** (no `ISOGOLF_MEASURE`, no development flag): frame time from a minimal logger behind a second define, or from a short Android GPU Inspector / Unity Profiler capture if the release flags don't allow it (to decide in that PR).
- **Putt prediction cost** (open since M2): an autoplay that putts, reading the existing `AimPreview.Predict` profiler marker.
- Terrain + props build time per course (`COURSE_TIMING` gains a props figure).
- By eye: the look, the life, the collisions and chip-ins off the pole, the rechecked art choices.

## 7. PR split and order

Proposed order (each PR into `develop`, reviewed and merged before any PR depending on it). The model per PR follows the director's rule (Opus for ball collisions, `BallSimulation`, the aim preview; Sonnet once a plan is written); placement is listed as Opus because it is seeded generation, which CLAUDE.md names as critical code (a judgement, the director may overrule).

| PR | Branch | What | Depends on | Model |
|---|---|---|---|---|
| 4.1 | `feature/m4-prop-kit` | `PropMeshKit`, olive, cypress, bush, rocks, wall; `PropKitSettings`; `TreeBuilder` folded into the kit; collider shape per prop; palette entries; `Sandbox_Props` showroom scene; triangle counts | — | Sonnet |
| 4.2 | `feature/m4-ball-collisions` | collisions in `BallSimulation` (flight, roll, pole), `ShotSettings` materials, aim preview on a hit, tests; tried on `Sandbox_Par3`'s trees | 4.1 (shapes) | Opus |
| 4.3 | `feature/m4-prop-placement` | `PropPlacement`, `CourseLayout.Props`, keep-out zones, clusters, props stream; checks and ace sweep with props; `Main` gets props; `MainSceneTests` updated | 4.1, 4.2 | Opus |
| 4.4 | `feature/m4-built-props` | shed, bench, bridge (if kept), traces, landmark (if kept), and their placement themes | 4.3 | Sonnet |
| 4.5 | `feature/m4-ambient-shader` | sway weight channel, flag flutter, foliage rustle, water highlights, cloud shadow | 4.1 (shares `TreeBuilder`/kit and `PlayPieceBuilder`) | Sonnet |
| 4.6 | `feature/m4-birds` | birds (and butterflies if kept), triggered by the ball's position | 4.3 | Sonnet |
| 4.7 | `feature/m4-look-recheck` | throwaway look-test build: outline, slope readability, bunker outlines; then the chosen options for real, one round per phone test | 4.3, 4.5 | Sonnet (Opus if a terrain mesh change) |
| 4.8 | `feature/m4-waterfall` | waterfall on water edges (if kept in M4) | 4.5 | Sonnet |
| 4.9 | `feature/m4-phone-checks` | end-of-M4 phone check | all | Sonnet |

Files shared, so these never run at the same time: 4.1, 4.4 and 4.5 (prop builders, palette); 4.2 and 4.3 (`BallGround` / `CourseLayout`, checks); 4.5, 4.7 and 4.8 (`LowPolyVertexColor`, water material). Can run in parallel once their dependencies merge: 4.4 with 4.5, 4.6 with 4.5, 4.8 with 4.6.

## Open questions

Sent to the director through the M4 hub on 2026-11-10. Answers will be recorded here word for word.

(see the batch in the hub message; folded in here once answered)
