# M4 plan: props and art pass

Status: **decided** (2026-11-10, `docs/m4-plan`). The director answered the 12 open questions; the answers are recorded word for word at the end, and the sections below are written from them.

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

### Which props (Q2: B)

| Group | Props | Role |
|---|---|---|
| Vegetation | olive tree (cauliflower crown, short trunk; today's `TreeBuilder` look), cypress (6–8-sided cone), round bush | clusters, shadows, the main life of the island |
| Stone | rocks (faceted, light stone palette, 0.3–1.2 m), round white rocks by water (§9 *Water hazards*), low dry-stone wall (rectangular blocks, slight per-block tone change) | clusters, edge accents, walls along the rough |
| Built | tile-roofed shed, bench (wood), small decorative stone bridge over a pond | scale cues, "inhabited but empty" |
| Traces (§7) | rake in a bunker, bucket of balls, bicycle against a wall, laundry on a line | one or two per course at most |
| Landmark | lone chapel or lighthouse at the island's edge | at most one per course |

Out of M4: characters (even the single distant silhouette §7 allows), audio, time of day per level, bigger terrain features, strategic obstacles on shot lines (Q4, a possible post-playtest follow-up).

**Bridge**: decorative. It spans a pond only where no shot line's corridor and no landing capsule passes over the pond (it is placed like any other prop, after the safe route, and never changes it), across the pond's narrow part on the side away from the shot lines, from rough to rough. Its collider is one block (arch to deck): a ball that hits it bounces off like stone, and the ball never rolls on the deck: a hop that comes back down on it bounces again or drops off, so the bridge never carries a ball across and never saves one from the water. The pond's risk is therefore unchanged. Frequency: at most one per course, on about one pond in three that has room for it (`LevelGenSettings`, tunable).

**Landmark**: a chapel or a lighthouse (one integer draw), at most one per course, on about one course in two (tunable), on a promontory of the island edge away from the play axis, never in the +X end kept for M5, never casting its shadow over a corridor.

### How they're built (Q1: A)

**Procedural meshes in C#**, like `TreeBuilder` and `TuftBuilder`: a small shared kit of mesh helpers (`PropMeshKit`: prism, box with slight jitter, gable roof, cone, jittered blob, block wall) writing flat-shaded triangles with palette vertex colours into `TerrainMeshData`. Each prop kind is a plain C# builder taking a seeded `System.Random`, so the same seed gives the same prop on every device, and its proportions live in a `PropKitSettings` ScriptableObject (today's `TreeSettings` proportions move there). No modelled prefabs, no new tool or package.

### Palette per material (ART_DIRECTION §3; 3–4 shades each)

- Foliage: the grass/vegetation row (`#A7B86A` `#7E9A48` `#56713A` `#34482A`), the darkest kept off most facets as today. Olive crowns lean slightly silvery: a new `OliveLight` within that row's hue, checked on the phone.
- Trunks and wood (bench, shed door, bucket, rake): `#8B5A35` `#6A4128` `#4A2C1C`.
- Rocks, walls, chapel, lighthouse, bridge: light stone `#F1ECE0` `#DAD3C3` `#B8B0A0`.
- Shed roof: tile `#D4875E` `#B2663F`; chapel or lighthouse roof: slate `#A3A3A0` `#7F807E` `#5C5D5C`.
- Accents only on traces (laundry: off-white, blue; bucket: red; bicycle: blue or metal `#5E625F`).
- Every new colour goes in `PaletteSettings`, never a literal in a builder.

### Rendering budget

- **Static props**: one mesh on the land material (as trees and tufts are today): one draw call plus its shadow-caster pass. SRP Batcher compatible, no GPU instancing needed.
- **Animated foliage** (Q8): first choice, a per-vertex sway weight in a spare channel (UV1) of the same land material, so trees and bushes stay in the props mesh and the terrain pays a multiply by zero. If that proves dearer on the phone (the terrain's vertex count pays the shader's sway code too) or shows in the terrain's shadows, the fallback is a **separate foliage mesh on its own animated material**: one more draw call plus its shadow pass. Budget either way: **props mesh + foliage mesh = at most +2 draw calls and +2 shadow-caster passes** over today; the flag stays its own mesh as today.
- Triangle budget, to confirm by measurement in PR 4.1: an olive tree is about 400 triangles today (5 blobs × 80); a par 5 with 25 trees, 15 bushes, 20 rocks, 2 walls and 2 built props stays near 20k triangles, about the terrain's own order. Simplify the blobs (one subdivision on smaller crowns) if the phone says so.
- Targets for the end-of-M4 check (targets, not measurements): 60 fps held on par 5 seed 33, GPU p99 under 13 ms, terrain + props build under 300 ms on the phone, draw calls no more than baseline + 3 (props, foliage, birds).

## 2. Placement rules

New `PropPlacement` (`Generation`, plain C#), run after hazards, output `CourseLayout.Props` (kind, position, yaw, scale, collider) so the terrain view, the ball's ground and the checks all read the same list.

- **Own stream**: a third `SeededRandom`, salt 7, from (seed, par, variant), read after the surfaces stream. Tuning props never moves an island, a slope, the green or a hazard (a test compares a course with props on and off). One sub-stream per cluster, so removing a cluster leaves the others where they were. Every accept/reject test compares whole centimetres; every discrete choice is an integer draw.
- **Keep-out zones** (Q4: A, hard, never broken): the shot corridors (`ShotCorridorHalfWidth`, 2 m, either side of every `ShotLine`: direct, cut and safe), every safe-route rest spot's landing capsule, the tee (radius 3 m), the green and 1.5 m around it, hazards' outlines + 0.5 m, water (except round white rocks at a pond's rim and the bridge), gaps, and the **+X end** of the course (the last 6 m beyond the hole and a 60° fan around +X, kept free for M5's next island).
- **Prop zones**: rough between the corridors and the island edge. Clusters only start where the zone is at least 2 m wide (1 m of free ground around the start; 3 m left too few starts on par 3s, see PR 4.3), and a cluster's start is an even integer draw over the valid starts, not ranked by distance from the shot lines (**decided in PR 4.3, director: Q2 A**).
- **Clusters, low density** (Q3: A): par 3 2–3 clusters (about 8–12 props), par 4 3–5, par 5 4–7, each cluster a centre, a theme (olive grove, cypress line along a wall, bushes and rocks, a built prop with its trace) and 2–6 members around it with a minimum spacing. Between clusters, empty ground: visual silence. The counts, members per cluster and spacing live in **`LevelGenSettings`** (a `props` block per par), tunable without code. The director checks on the phone that it doesn't feel too empty: **PR 4.3 ends with a density review** on the phone (a few seeds per par, raising or lowering the counts until approved), and PR 4.4 rechecks it once the built props are in.
- **Shadows count**: a tree's shadow falls toward the bottom-right of the screen, so a cluster is checked against the corridor with its shadow footprint, not only its trunk, so the tee shot never starts inside a large shadow (a look rule, judged on the phone).
- **Edge props**: rocks and short walls along the island edge (Land segments only, never where `EdgeSurface.Water`), the landmark on a promontory away from the play axis.
- **Hand-made par 3**: `Sandbox_Par3` keeps its hand-placed trees (a fixed test course), now built by the kit.

## 3. Ball collisions

Inside `BallSimulation`, deterministic like `HoleRim`, against simple shapes held by each prop (vertical cylinder for trunks and the pole, sphere or squashed sphere for crowns, bushes and rocks, oriented box for walls, benches, sheds, the bridge and the landmark). A uniform 2 m grid over the course finds the few shapes near the ball each step. Everything standing collides; flat traces lying on the ground (the rake in a bunker) don't, and the ball rolls over them (Q11: A).

- **Flight**: the arc's first hit is found up front, like the landing today: the ball's sphere swept along the arc against the shapes near it, at the same step the landing search uses. The arc ends at whichever comes first, landing or hit. A hit reflects the velocity about the contact normal with a per-material restitution and along-keep (`ShotSettings`: wood, stone, foliage, pole), then the ball flies a new arc, like a bounce.
- **Crowns and bushes are soft** (Q5: A): most of the speed is lost and the ball drops out under the tree, as through leaves. Trunks, rocks, walls and built props are hard and bounce the ball.
- **Rolling**: the same shapes as 2D circles and boxes at ground level stop or deflect a rolling ball (trunks, rocks, walls, bench legs).
- **Flag pole, in flight only** (Q7: A): a thin cylinder at the hole's centre, up to the pennant. A hit in flight takes the speed into the pole with a low restitution: a slow ball dropping onto the pole falls down beside it and `HoleRim` decides as for any ball over the hole; a fast one bounces off. So a chip-in off the pole depends on power, and no pole hit guarantees one. A rolling ball ignores the pole: `HoleRim` decides as today, and putting is unchanged.
- **Aim preview** (Q6: A): the dots stop exactly where the real ball would hit, with a small mark at the contact point and no landing ring; what follows the hit is not shown. The preview still never lies, and a chip-in off the pole stays a shot to find.
- **Checks**: `CourseChecks` and the ace sweep run with collisions on, because props are part of the checked course. If a prop breaks a check, the placement drops that prop (its own stream, so nothing else moves) rather than rerolling the course. **Decided in PR 4.3 (director: Q1 A)**: every prop a check shot struck is dropped, only those; a cluster left with a single prop keeps it. The variant is still picked on the bare course, so props never change which island is kept. The ace sweep is re-run over checked par 3 seeds 0–49 after collisions and after placement: the target stays under 1% per seed, about 0.5% mean, full power never holes. Pole hits are the likely risk; tune the pole's restitution first.

## 4. Ambient life (Q8: A)

All motion is visual only and in the shader where it can be, so the simulation never sees it and the course float stays untouched.

| Element | Status | How | Cost |
|---|---|---|---|
| Flag flutters calmly, the pole never moves | built | vertex animation in `LowPolyVertexColor`, weight 0 at the pole, rising to the pennant's free end | vertex math only |
| Foliage rustle | built | sway weight per vertex (0 on trunks, terrain, rocks, walls), slow low-amplitude noise per tree from its position (see the draw-call budget) | vertex math only |
| Water highlights | built | water material: some lighter facets (`#CFF1EE`) that fade in and out slowly, chosen per facet from a hash | vertex/fragment math only |
| Birds take off as the ball passes | built | 2–5 tiny faceted birds per course, perched in a cluster; when the ball flies or rolls within a few metres they flap up and away along a fixed path, then come back later; CPU-driven transforms, no physics | a few transforms |
| Butterflies | tried in the look-test build (4.7), kept only if approved on the phone | 1–3 near flowering bushes, a looping path, CPU | a few transforms |
| Cloud shadow | tried in the look-test build (4.7), kept only if approved on the phone | one large soft darkening drifting slowly across the island, a world-space term in the terrain shader | fragment math only |

- Normals don't move with the sway, so each facet keeps its tone and nothing shimmers (by design, given §4's crisp shadows). Shadows move with the sway because the shadow-caster pass runs the same vertex code; amplitude stays small enough that the crisp shadow edges don't swim (to check on the phone).
- WebGL-safe: no compute shaders, no threads, no GPU readback.

## 5. Art rechecks folded into M4

Done once trees, their shadows and real props are in, on the phone, through a **throwaway look-test build** (PR 4.7) that cycles options with a tap (the approach that worked in M1), probably over several rounds:

- **Outline**: none, ~1 px, ~2 px (cool dark tone, never black), on a course with props.
- **Slope readability** (Q10: A; director, M3 phone check: "très dur de voir les pentes"). First round: (a) stronger facet contrast on the ground only (lower ambient share on terrain, props unchanged) and (b) a subtle height tint (rough and fairway lighter on crests, darker in dips, a few percent). Only if (a) and (b) aren't enough, a second round with (c) a tilt hint on the green only (its mowing grain darkening along the fall line), as ART_DIRECTION asks facet shading to be judged before any guide mark.
- **Bunker outlines**: F2 made the colour match the drawn border; the border is still chords across a 1 m grid. If the phone still shows a polygonal bunker, the grid is subdivided locally to 0.5 m inside each bunker's bounds (about 4× the triangles there).
- **Butterflies and cloud shadow** (Q8), on and off.
- **Waterfall** where water meets the edge (Q9: A, PR 4.8): a faceted falling sheet under `EdgeSurface.Water` segments, with a little foam, fading into the sky above the clouds.

## 6. End-of-M4 phone check

On the Honor 500 Pro, `Docs/Measurements/m4/README.md`, like M3 PR 9:

- Frame time with props and ambient life on the longest par 5 (seed 33, and the par 5 with the most props over seeds 0–49), autoplay as in M3, dev build with `ISOGOLF_MEASURE`.
- The same autoplay in a **release build** (no `ISOGOLF_MEASURE`, no development flag): frame time from a minimal logger behind a second define, or from a short Android GPU Inspector / Unity Profiler capture if the release flags don't allow it (to decide in that PR).
- **Putt prediction cost** (open since M2): an autoplay that putts, reading the existing `AimPreview.Predict` profiler marker.
- Terrain + props build time per course (`COURSE_TIMING` gains a props figure).
- By eye: the look, the density, the life, the collisions and chip-ins off the pole, the rechecked art choices.

## 7. PR split and order

Each PR into `develop`, reviewed and merged before any PR depending on it. Models: Opus for ball collisions, `BallSimulation`, the aim preview and the seeded placement (Q12: A); Sonnet for the rest, its plan being written here.

| PR | Branch | What | Depends on | Model |
|---|---|---|---|---|
| 4.1 | `feature/m4-prop-kit` | `PropMeshKit`, olive, cypress, bush, rocks, wall; `PropKitSettings`; `TreeBuilder` folded into the kit; collider shape per prop; palette entries; `Sandbox_Props` showroom scene; triangle counts | — | Sonnet |
| 4.2 | `feature/m4-ball-collisions` | collisions in `BallSimulation` (flight, roll, soft crowns, pole in flight), `ShotSettings` materials, aim preview stopping at a hit, tests; tried on `Sandbox_Par3`'s trees | 4.1 (shapes) | Opus |
| 4.3 | `feature/m4-prop-placement` | `PropPlacement`, `CourseLayout.Props`, keep-out zones, clusters, props stream, density in `LevelGenSettings`; checks and ace sweep with props; `Main` gets props; `MainSceneTests` updated; **density review on the phone** | 4.1, 4.2 | Opus |
| 4.4 | `feature/m4-built-props` | shed, bench, bridge, traces, landmark, and their placement themes; density recheck | 4.3 | Sonnet |
| 4.5 | `feature/m4-ambient-shader` | sway weight channel (or the foliage mesh fallback), flag flutter, foliage rustle, water highlights | 4.1 (shares the kit and `PlayPieceBuilder`) | Sonnet |
| 4.6 | `feature/m4-birds` | birds, triggered by the ball's position | 4.3 | Sonnet |
| 4.7 | `feature/m4-look-recheck` | throwaway look-test build: outline, slope readability, bunker outlines, butterflies, cloud shadow; then the chosen options for real, one round per phone test | 4.3, 4.5 | Sonnet (Opus if a terrain mesh change) |
| 4.8 | `feature/m4-waterfall` | waterfall on water edges | 4.5 | Sonnet |
| 4.9 | `feature/m4-phone-checks` | end-of-M4 phone check | all | Sonnet |

Files shared, so these never run at the same time: 4.1, 4.4 and 4.5 (prop builders, palette); 4.2 and 4.3 (`BallGround` / `CourseLayout`, checks); 4.5, 4.7 and 4.8 (`LowPolyVertexColor`, water material). Can run in parallel once their dependencies merge: 4.4 with 4.5, 4.6 with 4.5, 4.8 with 4.6.

## Follow-ups after M4 (not in scope)

- **Strategic obstacles** (Q4 B): trees allowed on the direct and cut lines (never on the safe route or its capsules), 1–2 per course, counted as hazards with a new role. Possibly after the M4 playtest, the director's call.

## Decisions (director, 2026-11-10, word for word)

Relayed by the M4 hub:

> Q1 A
> Q2 B
> Q3 A, à voir si ça ne donne pas un ressentiment trop vide
> Q4 A, potentiellement B après playtest
> Q5 A
> Q6 A
> Q7 A
> Q9 A
> Q10 A
> Q11 A
> Q12 A

and then "Q8 A", after the hub explained that A builds the flag, foliage, water highlights and birds for sure, while butterflies and the cloud shadow are tried in the test app and kept only if judged on the phone.

| Q | Question | Options (A first) | Chosen |
|---|---|---|---|
| 1 | How props are built | A procedural C# meshes, one mesh on the land material / B modelled prefabs | A |
| 2 | Kit contents beyond trees, bushes, rocks, walls, shed, bench, traces | A + one landmark, no bridge / B + landmark + decorative stone bridge (block collider, no rolling on the deck) | B |
| 3 | Density | A low (par 3 2–3 clusters, par 4 3–5, par 5 4–7) / B about double | A, tunable, reviewed on the phone |
| 4 | Obstacles near shot lines | A strict: none in the corridors or capsules / B strategic obstacles on direct and cut lines | A, B maybe after playtest |
| 5 | Crowns and bushes | A soft (ball drops under the tree) / B hard like a trunk | A |
| 6 | Aim preview on a hit | A dots stop at the hit with a mark / B show the bounce too | A |
| 7 | Pole and a rolling ball | A pole counts in flight only / B pole stops or deflects a rolling ball too | A |
| 8 | Ambient life in M4 | A flag, foliage, water, birds; butterflies and cloud shadow tried on the phone / B all of it | A |
| 9 | Waterfall | A in M4, last art PR / B after M5 | A |
| 10 | Slope readability test | A facet contrast + height tint first, green hint only if needed / B all three at once | A |
| 11 | Small traces | A everything standing collides, flat traces don't / B traces only out of reach, all colliding | A |
| 12 | Model for the placement PR | A Opus / B Sonnet | A |
