# Changelog

All notable progress on Iso Golf. The format follows [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

### 2026-11-11: M4 built props (`feature/m4-built-props`, PR 4.4)

#### Added

- Built props, traces and landmarks in the kit (`PropBuilder`, from `PropMeshKit`, palette only, flat facets, one seeded `System.Random` each): tile-roofed **shed** (whitewashed stone, wooden door, two-tone tile roof), wooden **bench**, small **stone bridge** (a row of stone blocks from below the lower bank up to a humped deck, parapets), **rake** lying flat, red **bucket** of white balls, **bicycle** (two tyre rings, blue frame), **laundry** on a line between two posts, **chapel** (nave, slate roof, bell tower) and **lighthouse** (eight-sided tapering tower, darker band, slate gallery, lantern and cap). New helpers `AddFrustum`, `AddUprightDisc`, `AddUprightRing`. Proportions in `PropKitSettings`; new `PaletteSettings` entries (wood light and dark, tile, slate, metal, red, blue, off-white), written to the assets through the Editor.
- Collision shapes: one oriented box fitted to the mesh for the shed, bench, bicycle, chapel and the **bridge (one block from the water to the parapet top across the whole span)**; two stacked cylinders for the lighthouse; a cylinder for the bucket; the two posts of the laundry (the cloths don't collide); **none for the flat rake** (director Q11 A).
- `CourseProp.YawDegrees` (which way a built prop faces, whole degrees), `CourseProp.IsLine`/`IsTrace`; `PropKind` gains the nine kinds. Walls and bridges still take their direction from `End`.
- Placement (`PropPlacement`, props stream, each from sub-streams keyed above the 4.3 clusters so those stay bit-identical): **built clusters** (a shed or a bench facing the course, then its trace and up to two bushes or olives; extra to the density's clusters, `builtClusters` per par 0-1 / 1-2 / 1-2): a shed gets a bicycle on its gable wall or laundry behind it (director: the bicycle leans on the shed, the laundry hangs between two free posts), a bench a bucket; **rake** in a bunker on about half the courses that have one, at the rim farthest from the shot lines; **bridge**, at most one per course, across the narrowest chord of a pond that no corridor or capsule touches, deck 0.7 m past the banks, every point of the span kept out of the corridors, capsules, tee, green, bunkers and the +X end; **landmark** (chapel with its door toward the course, or lighthouse), at most one, on a promontory of the island's edge (a ring of 3 m around it at most 60% island), 0.8-2.4 m in from the edge, at least 3.5 m from the centreline, never in the +X end, its shadow out of the corridors. Traces at most 2 per course. Every frequency in `LevelGenSettings.props` (`PropRules`).
- Checks: a shed or bench dropped by the checks takes its trace with it.
- `PropShowroom` pages (Vegetation, Built, Traces) for `Sandbox_Props`.
- Tests: `BuiltPropKitTests` (51: determinism, budgets, palette only, flat normals, colliders hold the mesh, shapes and materials, rake has none, bridge one block, and **a ball dropped onto the bridge**: 36 shots of different powers and aims onto the block's deck, none comes to rest on it, the hit cap ends the loop cleanly with perfectly bouncy stone), `BuiltPropPlacementTests` (7 + 1 explicit): 4.3 clusters bit-identical with and without the built props, determinism, frequencies over seeds 0-199, **bridge never changes the checks** (no check shot strikes it, same result with and without) and spans water between two rough banks, landmark site, rake flat on sand and rolled over by the ball, a trace beside its built prop. `PropPlacementTests` keep-out check now covers the built kinds.
- Captures in `Docs/Screenshots/m4-built-props/`.

#### Changed after review (director's decisions, relayed by the hub)

- **Bridge rule (option A)**: only the bridge's own footprint (span, banks, shadow of what stands above the higher bank) must keep clear of every corridor, capsule and other keep-out, instead of the whole pond; `bridgeChance` 100%. Measured over seeds 0-199: bridges on 12 / 3 / 5 courses of 200 for par 3 / 4 / 5 (6% / 1.5% / 2.5%, 20 of 600), still rare because the footprint usually touches a corridor. The check that the safe route and `CourseChecks` are identical with and without the bridge stays. A bridge's shadow height was measured from its foot in the water (1.9 m); it now counts only above the higher bank.
- **Fairway rule**: every prop's whole collision footprint plus `PropRules.fairwayMargin` (0.3 m, in `LevelGenSettings`, written through the Editor; negative turns it off) stays off fairway ground, besides the corridors: `PropZones.OverFairway` samples the fairway field at the footprint's centre and two rings of 24 points out to reach + margin. The green and bunkers keep their keep-outs. Before the rule, 11.6% / 16.0% / 17.5% of the placed props (par 3 / 4 / 5, seeds 0-199) had a collision shape reaching fairway ground (an overhanging crown or shed); after it, 0. Cost: props per course 7.1 / 11.7 / 17.2 before, **5.3 / 9.1 / 14.5 after** (generated layouts, built props included; checked courses 5.3 / 9.2 / 14.7, 0 dropped by the checks; two par 3 seeds of the ace sweep have no prop at all): the courses get sparser, which the density review will weigh.
- **Director's phone review** (par 5 seed 103 lighthouse, seed 120 red object): the lighthouse's gallery was an open, thin, flaring ring that showed its underside, and its lantern (the darkest slate) read near black, so some faces looked wrongly lit; the gallery is now a closed slab (sides, a slightly sloped top, an underside) and the lantern and cap take lighter slate shades. The darkest wood shade (4A2C1C, vertex colour 17/6/3 once converted) also read near black on bench legs and doors; they use the trunk shade now. A numeric check found no inward-facing normal on the lighthouse. The red object was a bucket of balls standing on a steep bank (0.29 m lower ground a metre from its bench, so it looked to sit on the bench) and small: built props and traces now stand only where the ground under their footprint slopes at most `builtMaxSlope` (30%, rise over run; 15% left almost no built prop, the terrain's facets are too bumpy for it), and the bucket is 1.4 times larger. Test `BuiltProps_StandOnLevelGround_OverSeeds0To99`.
- Test: `NoPropShapeReachesFairway_OverSeeds0To199` (sampling each shape's own outline, independent of the zones) and the 4.3 keep-out test covers the margin; the other keep-outs are unchanged. Explicit measurement `FairwayRule_BeforeAndAfter`.

#### Measured (EditMode)

- 823 tests, 808 pass, 15 explicit skipped, 0 failed (baseline on develop: 764, 750, 14, 0), before the review changes above.
- Props per checked course, seeds 0-199: par 3 **7.1**, par 4 **11.6**, par 5 **17.2** (PR 4.3: 6.0 / 9.5 / 14.1). 0 props dropped by the checks.
- Courses holding each kind, seeds 0-199 per par (3 / 4 / 5): bench 44 / 104 / 156, shed 2 / 7 / 18, rake 102 / 102 / 91, bucket 29 / 79 / 114, bicycle 1 / 1 / 8, laundry 0 / 0 / 0, chapel 9 / 15 / 36, lighthouse 28 / 27 / 35, **bridge 0 / 0 / 1**. Landmarks are on about 25% of courses (50% asked: the site rules cut it). Sheds are rare because a shed's footprint (about 2 m of free ground) rarely fits between the corridors. Bridges: 438 ponds over the 600 courses, 14 with no corridor or capsule touching them, 5 able to take a bridge.
- Fine ace sweep, checked par 3 seeds 0-49, with props, pole and the built props: **mean 0.59%**, max 0.86% (seed 46), 0 seeds at or over 1%, full power never holes (unchanged from 4.3, re-run after the review changes below).

### 2026-11-11: M4 prop placement (`feature/m4-prop-placement`, PR 4.3)

#### Added

- `PropPlacement` (`Generation`): props placed by rules on generated courses, after the hazards, from a third random stream (salt 7, from seed, par and variant), with one sub-stream per cluster. Every discrete choice is an integer draw, every accept/reject compares whole centimetres. Output: `CourseLayout.Props` (`CourseProp`: kind, foot, wall end, scale, mesh seed, cluster, collision shapes), `CourseLayout.PropShapes`, `CourseLayout.WithProps`.
- Hard keep-outs (`PropZones`), tested on each candidate's **real collision reach**: each candidate is built from its mesh seed and measured, so no prop shape can reach into a zone. The zones: 2 m corridors either side of every direct, cut and safe line (the shadow of a tall prop too), every rest spot's landing capsule, tee 3 m, green + 1.5 m, bunkers and ponds + 0.5 m (+ 0.1 m for the 24-point ring's chords), the island edge (gaps included), and the +X end kept for M5 (past the hole, a 60° fan around +X). Round white rocks are the only props allowed by water, just outside a pond's rim.
- Clusters: a start needs 1 m of free ground around it on the rough; the start is an even integer draw over the valid ones (director: Q2 A, instead of the plan's ranking by distance from the lines); a start that can't hold two props tries another (3 attempts). Themes: olive grove (an olive that finds no room becomes a bush), cypress line along the course with a wall beside it, bushes and rocks, edge rocks or wall (never within 3 m of a pond, so never where water meets the edge), round white rocks on a pond's rim. Density per par and every tunable in `LevelGenSettings` (`propKit`, `props`: `PropRules`, `PropDensity`), written to the asset through the Editor.
- Shadows: generation can't see the sun, so `PropRules.shadowHeading` (-125° from the course heading, turned with the course like `SunHeading.YawFor`) and `shadowLength` (0.78 per metre of height) hold `Main`'s sun; a test reads the scene's sun against them.
- Checks with props (`CheckedCourse`): the variant is still picked on the bare course, so props never change which island is kept; then the checks play again with the props and the flag pole, and when a check fails with them, every prop a check shot struck is dropped (director: Q1 A), up to 3 rounds, then all props go. A cluster left with one prop keeps it. `CheckedCourseResult.DroppedProps`; `BallSimulation.Struck` lists the obstacles a shot struck.
- `Main`: the generator's prop kit builds the props mesh (one draw call on the land material, plus its shadow pass), and `LevelBootstrap.collisions` is on, so the ball strikes the props and, in flight, the pole. `TerrainView.Build(layout, courseKit)` builds the same props from the same seeds. `Sandbox_Par3` is unchanged (hand-placed trees). `COURSE_TIMING` logs the prop count and drops.
- `PropPlacementTests` (12): determinism, props on/off/no kit leave islands, slopes, green, pin, hazards and plan equal, keep-outs never broken over seeds 0-199 of every par (checked by sampling the true outlines and the safe route's capsule samples, independently of `PropZones`), clusters within their par's counts, fewer clusters keep the first ones, display shapes equal the placement's bit for bit, every shape within its measured reach, hand-made courses without props, checked courses passing with props and pole, a struck prop dropped while an unstruck one stays, the shadow rule against `Main`'s sun. `MainSceneTests.MainPlaysItsGeneratedProps_WithCollisionsAndThePole`. `ShotSweep` takes obstacles; `CourseCheckTests.HoleInOne_OnPinnedPar3s` plays with props and pole.
- Captures in `Docs/Screenshots/m4-prop-placement/`.

#### Measured (EditMode)

- 764 tests, 750 pass, 14 explicit skipped, 0 failed. No existing pin moved (variants, hazards, ace gates).
- Props placed, seeds 0-39, variant 0: par 3 6.4 per course (2.35 clusters), par 4 9.8 (3.55), par 5 15.2 (5.6). Checked courses, seeds 0-199: par 3 6.0, par 4 9.5, par 5 14.1 per course. **0 props dropped** by the checks over the 600 courses.
- Fine ace sweep, checked par 3 seeds 0-49, with props and pole: **mean 0.59%** (same courses bare: 0.49%), max 0.86% (seed 46), 0 seeds at or over 1%, full power never holes. Pole restitution not tuned.
- Reroll survey, seeds 0-199: par 3 1, par 4 29, par 5 26 rerolled, 0 exhausted: unchanged, as built.
- Time per checked course (Editor, Mono, seeds 0-14): par 3 37 → 62 ms, par 4 55 → 94 ms, par 5 65 → 119 ms (bare → with props: placement 10-24 ms on average, plus the checks played again with props). M3 PR 9 found the phone in the Editor's range for generation; not measured on the phone yet.

### 2026-11-10: M4 ball collisions (`feature/m4-ball-collisions`, PR 4.2)

#### Added

- `BallObstacles` (`Gameplay`): the props' `PropShape`s and, optionally, the flag pole, each grown by the ball's radius, with a uniform 2 m grid. A straight move of the ball's center is tested exactly against each grown shape (vertical cylinder, ellipsoid, yaw box: where the segment enters it), so nothing is missed between two steps; only entries count, the earliest wins, ties to the lowest index.
- Collisions in `BallSimulation`, deterministic, no Unity physics, in the course container's local space, behind an optional constructor argument (null: every shot plays exactly as before):
  - **Flight**: each arc looks up front for its first hit before its landing, on chords at the landing search's step (0.02 s; a chord strays at most g·dt²/8 = 0.5 mm from the parabola, at any power). The ball bounces off with the material's response and flies a new arc that leaves that obstacle out. Hard: wood (restitution 0.45, along-keep 0.7), stone (0.55, 0.75). Soft foliage: a raised crown lets the ball on into the leaves with 15% of its speed, so it falls through and lands under the tree (it may meet its own trunk on the way); a bush on the ground bounces it off weakly (0.05, 0.15).
  - **Hit cap** (`maxHits` 8): the last hit takes all the speed, and the ball drops straight down, past every obstacle in flight, for the rest of the shot.
  - **Rolling**: the same grown shapes at the ball's height (trunks, rocks, walls, bushes; crowns are out of reach), the speed bounced off along the ground, the ball set back 1 mm. A ball left slower than `obstacleRestSpeed` (0.05 m/s) rests against the obstacle, so a slope pushing it into a wall can't make it strike forever.
  - **Flag pole**, in flight only (hops included): a cylinder at the hole, restitution 0.25. A ball reaching it low, already slowed by its landing or first hop, falls over the hole and `HoleRim` decides; higher up it bounces away. A rolling ball ignores it, so putting is unchanged.
- `ShotSettings` (Collisions): `ballRadius` 0.12, `poleRadius` 0.03, `poleHeight` 1.8 (the drawn sizes, checked by a test), `maxHits`, `obstacleRestSpeed`, and an `ObstacleResponse` (restitution, along-keep) for wood, stone, foliage and the pole. Written to `ShotSettings.asset` through the Editor.
- Aim preview: `BallSimulation.PredictHit` runs the shot's own search; a shot that strikes something shows its dots up to the hit, a small ring facing the camera at the contact (`AimPreviewSettings` hit mark: 0.09 m, inner 0.055 m) and no landing ring.
- `LevelBootstrap.collisions` (off by default): on for `Sandbox_Par3`, whose hand-placed trees now give their shapes (`PropBuilder.BuildTrees` fills a list, `TerrainView.PropShapes`). `Main` stays without collisions or pole until PR 4.3 brings its props (director's choice A), so it plays as its checks and the ace sweep do.
- `BallCollisionTests` (22): each shape and material, a glancing hit, the soft crown drop, crown re-entry (none after the drop; a ball punched up from under a crown is caught by it), the hit cap, rolling into a trunk head-on and off-centre, resting against a wall on a slope, pole chip-in possible but power-dependent, a hop striking the pole where it would have lipped out (deterministic), putting unchanged by the pole, no props ≡ no change, determinism at any frame rate, the preview matching the played shot (a fan of 779 shots on `Sandbox_Par3`: 309 tree hits, 470 clear; 401 pole shots), the grid against testing every shape, simulation sizes against the drawn pieces.
- Captures in `Docs/Screenshots/m4-ball-collisions/`.

#### Measured (EditMode)

- 751 tests, 737 pass, 14 explicit skipped, 0 failed; no existing pin moved. The ace sweep is unaffected on `Main` (no props, no pole yet).
- Pole, flat test green, shots straight at the hole from 4, 10 and 16 m, powers 0-1 by 0.0005: the pole adds 27, 35 and 38 holed shots of 2001 and takes none away; those chip-ins all strike it below 0.18 m. For PR 4.3 to measure in the ace sweep with props.

### 2026-11-10: M4 prop kit (`feature/m4-prop-kit`, PR 4.1)

#### Added

- `PropMeshKit`: shared mesh helpers (prism, cone, box with slight jitter, gable roof (first used in 4.4), jittered blob, block wall) writing flat-shaded, palette-coloured triangles into `TerrainMeshData`.
- `PropBuilder`: olive tree, cypress (6-8 sides), round bush, faceted rock (20 facets), round white rock (80 facets), low dry-stone wall (blocks in running bond, a tone picked per block). Each takes a seeded `System.Random`.
- `PropKitSettings` (+ `Settings/PropKit.asset`): the props' proportions. The tree values moved here from `TreeSettings`, which keeps only the hand-placed list and its seed.
- `PropShape` / `PropMaterial`: one collider shape per prop part as plain data for PR 4.2 (vertical cylinder for trunks and a cypress's foliage, ellipsoid for crowns, bushes and rocks, yaw box for walls; material Wood, Stone or Foliage; `Level(point)` for inside tests). No collision code yet. Ellipsoids and boxes are fitted to the built mesh, so they enclose it by construction.
- Sway weight channel: `TerrainMeshData.Sway`, written to UV1.x by `ToMesh` only when some vertex has a non-zero weight (0 on trunks, rocks and walls, rising from 0 at the crown's bottom to 1 at its top on foliage). The terrain's own meshes carry no extra channel; no shader reads it until 4.5.
- Palette: `OliveLight` (#8A9E5E, the grass row's hue a little greyer) and `StoneLight/Mid/Dark` (#F1ECE0 #DAD3C3 #B8B0A0), written into `Palette.asset`.
- `Sandbox_Props` scene (`PropShowroom`): every kind in a row, three seeds each, flat ground, the game's material, sun and camera angle.
- Tests: `PropKitTests` (determinism, budgets, palette only, flat normals, colliders enclosing the mesh, materials, sway, helpers), `HandPlacedTreeTests.SandboxTreesKeepTheirShapeThroughTheKit`.

#### Changed

- `TreeBuilder` is folded into the kit (`PropBuilder.BuildTrees`); `TerrainView` gets a `propKit` field, set on `Sandbox_Par3`. `Sandbox_Par3`'s ten trees keep the same 2260 triangles at the same positions (checked equal to the old code to 0.1 mm).
- Olive crowns' light shade is `OliveLight` instead of `FoliageLight`: the only look change, a little greyer. Before/after in `Docs/Screenshots/m4-prop-kit/`.

#### Measured (EditMode, triangles per prop at scale 1)

- Olive 412, cypress 40, bush 120, rock 20, round rock 80, 4.5 m wall 376.

### 2026-11-10: M4 props and art pass decisions (`docs/m4-plan`)

#### Added

- `Docs/Plans/m4-plan.md`: the M4 plan, with the director's 12 answers word for word, the PR split (4.1 prop kit to 4.9 phone checks) and the model per PR.

#### Changed

- Props: built procedurally in C# from a shared mesh kit (no modelled prefabs): olive trees, cypresses, round bushes, rocks, dry-stone walls, a shed, a bench, one or two traces, a small decorative stone bridge (a block collider, never a way across a pond, placed where no shot line or landing capsule passes, at most one per course) and at most one landmark (chapel or lighthouse, about one course in two). Static props in one mesh on the land material; foliage may need its own animated mesh (at most +2 draw calls).
- Placement: own random stream (salt 7), clusters, low density (par 3 2-3 clusters, par 4 3-5, par 5 4-7) tunable in `LevelGenSettings` and reviewed on the phone so the island doesn't feel empty. The strict rule stays: no prop in a shot corridor (direct, cut or safe), a landing capsule, the tee, around the green, on a hazard or on the +X end kept for M5. Strategic obstacles on shot lines are a possible follow-up after the M4 playtest.
- Ball collisions: in `BallSimulation`, deterministic; hard trunks, rocks, walls and built props, soft crowns and bushes; flat traces don't collide; the flag pole counts in flight only, so putting is unchanged. The aim preview stops its dots at the hit with a small mark. Checks and the ace sweep run with props on.
- Ambient life: flag, foliage, water highlights and birds in M4; butterflies and a cloud shadow tried on the phone, kept only if approved.
- Art rechecks on the phone with props in: outline, slope readability (facet contrast and height tint first, a green tilt hint only if needed), bunker outlines. The waterfall where water meets the edge moves into M4.
- `CLAUDE.md` (Key systems: Props, Ball collisions, Ambient life, aim preview; Milestones) and `ART_DIRECTION.md` (§6, §7, §8, §9) describe the decisions.

### 2026-11-10: Surface junctions, fairway ends and bunker outlines (`fix/m3-surface-junctions`, F2)

#### Fixed

- **Bunker outlines (seed 6 par 4 and all others)**: `TerrainBuilder` coloured each piece of ground from the true outline read at the piece's centroid, while the cut itself is a straight chord across a 1 m cell. A bunker of about 1 m radius curves away from its chord by more than a piece is wide, so sand wedges stuck out beyond the drawn border and sand was left out inside it. A piece now takes its colour from the side of each border cut (bunker, green, fairway) it fell on. Mesh colouring only: no layout, seed or ball behaviour changes, and `BallGround` keeps the true outlines (the M2 limits of 27 cm and 11 cm are not widened). Before: 5, 15, 15 and 11 sand triangles disagreeing with their border on seeds 4, 6, 3 par 4 and 5 par 5, 737 over seeds 0-29 of the three pars; after: 0.
- **Raw fairway end at the green (seed 4 par 4) and at the tee**: the fairway ended in a flat cut across the centreline, and the green sits off the centreline, so the corner poked out beside it. Both ends are now rounded tips (`FairwayLayout.StartCap` / `EndCap`, quarter ellipse, `fairwayCapDepth` 2.5 m). Chosen by the director per course from the green's guard: a guard beside the green (at least `fairwayIntoGreenGuardAngle`, 25 degrees, off the line back toward the tee) lets the fairway run into the green; a guard in front, or an island green, keeps the fairway `fairwayShortOfHole` (3.7 m) short behind a rough apron. No new draw in any stream.

#### Added

- `SurfaceColourTests` and `FairwayJunctionTests`, explicit `FairwayJunctionTests.LookShare`. EditMode: 668 tests, 654 pass, 14 explicit skipped, 0 failed; no pinned figure moved.
- Captures in `Docs/Screenshots/m3-surface-junctions/`.

#### Measured (Editor)

- Looks over seeds 0-199: rough apron 131/200 on par 3, 150/200 on par 4, 141/200 on par 5; the rest run into the green.
- Fine ace sweep, checked par 3 seeds 0-49: mean 0.49% (was 0.48%), max 0.78% (seed 0, unchanged), 0 seeds at or over 1%, full power never holes.
- Reroll survey, seeds 0-199: par 3 1, par 4 29, par 5 26 rerolled, unchanged; 0 exhausted.
- `CLAUDE.md` (Level generator) and `ART_DIRECTION.md` (Course elements) describe the rule.

### 2026-11-09: Camera heading reads the course ahead and leans toward the hole (`fix/m3-camera-heading-par5-seed6`, F1)

#### Changed

- The automatic heading at rest, when the hole is out of a full shot's reach, is now the **course ahead, leaning toward the hole**, instead of the leg the ball is on:
  - the course ahead is the direction of the centreline from the ball's projection on it to `CameraRigSettings.headingLookAhead` (16 m, about half a full shot) further on, stopped at the hole end (`CameraHeading.Ahead`, was `Leg`). Mid-leg it is the leg's heading; short of a bend it turns gradually toward the next leg, with no threshold;
  - then it turns toward the hole by at most `holeHeadingFarTurn` (20 degrees), so a distant hole leans into view without the view looking across the void beside a dogleg;
  - on the tee (start view, lost tee shot) it faces the course ahead only, so the tee's fairway runs straight up the screen.
  - Facing the hole within reach, the twist, the approach zoom and the never-in-flight rule are unchanged. Director's choices: look-ahead over a hard leg switch; after the first capture ("still not enough toward the hole"), a bounded lean toward the hole over a longer look-ahead or a 50/50 blend; the tee kept on the course ahead.
- No existing pinned heading moved: the dogleg fixture's tee yaw 80 holds thanks to the tee rule, and the framing, shadow-distance and reachable-yaw pins pass unchanged.
- `CLAUDE.md` (Camera conventions, Camera heading) and `ART_DIRECTION.md` (Framing) describe the new rule.

#### Fixed

- Generated par 5 seed 6 (phone check, M3 PR 9, follow-up 1): a full tee shot rests at (-6.41, -5.21), 0.37 m short of a 32 degree bend, with the hole 45.0 m away (reach 31.97 m). The view faced the first leg (-2.0 degrees, yaw 82), past the corner at the island's edge and the sky. It now faces 15.37 degrees (yaw 64.63), the hole's own bearing (the course ahead alone gave 29.28 and left the hole well off screen to the right), with the fairway, the pond and the green's bunkers straight ahead.

#### Added

- Tests in `CameraHeadingTests`: the look-ahead (mid-leg, just short of a bend, gradual with no jump, at and past the hole end, a ball off to one side, zero look-ahead), the far turn (bounded at its limit, facing the hole within it, zero, straight course, on the tee), and seed 6 par 5's rest heading pinned with the real `BallSimulation`.
- Captures in `Docs/Screenshots/m3-camera-heading/`.

### 2026-11-09: M3 phone checks (`feature/m3-phone-checks`, PR 9)

#### Added

- Phone-check tooling, compiled only with the scripting define `ISOGOLF_MEASURE` (never in a release build): `PhoneChecksPanel` (seed -/+ and par 3/4/5 strip, frame-time readout, `FRAMES` log every 5 s), `DebugCourseOverride` (`Gameplay`, seed/par from PlayerPrefs, read by `LevelBootstrap`), `COURSE_TIMING` log of generation + checks and terrain build. `PhoneMeasurement` autoplay now starts only when a `measure.txt` ("<par> <seed>") is pushed to the app's files folder.
- `Docs/Measurements/m3/README.md` with results, `par5-seed33.csv`, and device screenshots in `Docs/Screenshots/m3-phone-checks/`.

#### Results (Honor 500 Pro, development build)

- Passed: gaps, water, bunkers and the livelier green (PR 43); sun per level (PR 38); shadow fade on par 4/5 accepted.
- Partial: slopes hard to read, rough fairway/green junction (seed 4 par 4) and fairway bunker outline (seed 6 par 4); camera heading after a full-power shot on seed 6 par 5.
- Measured: par 5 seed 33 held 60 fps (16.51 ms max over 8,782 frames); generation + checks 14-37 ms, terrain build 47-220 ms on the phone.
- Follow-ups for M4 or later are listed in the results doc. No gameplay code changed.

### 2026-11-09: Main on a generated course (`feature/m3-main-generated`, PR 8)

#### Added

- `LevelBootstrap` (`Gameplay`) generates the course when given `LevelGenSettings`: `CheckedCourse.Generate(seed, par, generator, ball.Shot)`, then `TerrainView.Build(layout)`, then the camera, sun, clouds and ball as before. It logs the variant kept, the candidates tried and the failed checks (`CheckedCourseResult` stays readable on `Generated`). `seed` and `par` are Inspector fields until M5.
- `Sandbox_Par3`: the old `Main` (hand-made par 3 with its hand-placed trees), the fixed test course.
- Tests: `MainSceneTests` checks that `Main` has a generator and no trees, and that `Sandbox_Par3` keeps the hand-made shape and trees.
- Captures in `Docs/Screenshots/m3-main-generated/`.

#### Changed

- `Main` plays a generated, checked course (seed 1, par 3 by default) with no trees: on a generated course hand-placed trees would land on bunkers, shot lines and the safe route. M4's placement rules bring them back (director's choice).
- `TerrainView` takes a layout (`Build(CourseLayout)`) or, as `Build()`, its hand-made shape; it no longer holds a generator, seed or par (`Generation` can't see the checks in `Gameplay`).
- `CameraCourse.LongPath` is now a generated, checked par 5 (seed 33, straight first leg, a gap), `"generated:5:33"`. Moved pins: `CameraPathTests` framing figures re-measured (they pin today's framing now, the old ones belonged to the old island; the straight par 3 rows still hold the pre-heading capture) and the "with or without centreline" check keeps only the par 3, because a generated par 5's islands wobble around a centreline 1.7 m off the axis (2.35 m difference); `ShadowDistanceTests` figures re-measured (worst heading / starting heading, m): 9:16 69.14 / 55.02 (was 68.03 / 58.58), 1280:2800 76.21 / 63.36 (was 76.17 / 67.84), 9:21 78.83 / 66.37 (was 79.17 / 71.11).
- `CLAUDE.md` names the new scene layout.

#### Removed

- `Sandbox_LongCourse`, `Par5SandboxCourseShape`, and the scenes `Sandbox_Gap`, `Sandbox_Dogleg` and `Sandbox_Generated` (generated courses replace them to the eye). `GapCourseShape` and `DoglegCourseShape` stay as test fixtures (camera, island area, shadow, multi-island and dogleg tests).

#### Notes

- Not checked in Play Mode: a shot and the rest framing on generated courses (Play Mode doesn't tick while Unity is unfocused); those paths are covered by the existing camera and ball tests. Start views were captured for par 3 seed 1, par 4 seed 3 and par 5 seed 33.

### 2026-11-09: Course checks and deterministic reroll (`feature/m3-course-checks`, PR 7)

#### Added

- `CourseChecks` and `CheckedCourse` (`Gameplay`, plain C#): checks a generated course with the real `BallSimulation` on its `BallGround`, and tries variants 0, 1, ... of a (seed, par) until one passes, at most `checkVariants` (8). If none passes it keeps the one failing the fewest checks (the lowest variant on a tie) and logs a warning. Same (seed, par), same course. The checks, all cheap enough for the phone:
  - `IncompletePlan`: `CoursePlan.Complete` (every required role placed, a safe route);
  - `GapNotCleared`: every safe-route leg across open sky brings a ball to rest past the gap at a power from 0.30 to the lay-up power (0.70), aimed at the next rest spot;
  - `NoRestNearHole`: from the safe route's last rest spot, some approach (aims -1, 0, +1 degrees, powers 0.02-1.00 by 0.02) holes or stops on the green within 2 m of the hole.
- `LevelGenSettings` (and asset): `checkVariants` 8, `restNearHole` 2, `restCheckAim` 1, `restCheckPower` 0.02-1.00, `checkPowerStep` 0.02, `gapCheckMinPower` 0.3.
- Tests: `CourseCheckTests` (11: pinned seeds and their variants, determinism, every checked course passes on seeds 0-29 per par, incomplete plans reroll, each check can fail, exhaustion keeps the least bad and warns, fine ace sweep on two pinned par 3s); explicit `CourseCheckMeasurements` (reroll survey over seeds 0-199 per par, fine ace sweep over checked par 3 seeds 0-49). EditMode: 642 tests, 629 pass, 13 explicit skipped, 0 failed.

#### Changed

- `ShotSettings.green.restitution` 0.30 → 0.42 (the director's choice among the measured options): a ball landing on the green hops higher, so where it lands matters more. Putts are unchanged. Generator levers alone could not reach the ace target (best: par 3 length 22-26 m and a smaller green, 0.93% mean, 7 of 16 seeds at or over 1%), because aces land on the green itself 3-4 m short of the hole and roll in.
- `BallOnCourseTests.FullTeeShot_...` is now `StrongTeeShot_...` at power 0.95: on the hand-made 28 m par 3, a full shot now hops over the back of the green and off the island. On generated par 3s (18-26 m) a full shot straight at the hole was already lost on 30 of 30 seeds before this change.
- `CLAUDE.md`: where the checks run (cheap ones on the phone, the ace rate held by tuning and gated by tests).

#### Notes

- Ace sweep (fine: +-6 degrees by 0.1, power 0.50-1.00 by 0.01) over checked par 3 seeds 0-49: mean 0.48%, max 0.78% (seed 0), no seed at or over 1%, full power never holes. Before (PR 6b): mean 1.17%, max 1.73%, 41 seeds at or over 1%.
- Reroll survey, seeds 0-199: par 3 rerolled 1 (seed 9), par 4 29, par 5 26; no seed runs out of variants (highest variant kept: 1, 3, 2). Every reroll is an incomplete plan: the gap and rest-near-hole checks failed on no variant 0 of the 600 courses, so today they are guards for later tuning. 1.01, 1.18 and 1.15 candidates per course; 26.7, 51.1 and 69.1 ms per checked course (Editor, Mono; not measured on the phone).
- One fine ace sweep costs 2.3-3.4 s per par 3 in the Editor, which is why it stays offline. The phone path is not wired yet: `TerrainView` still calls `CourseGenerator.Generate` directly; PR 8 (Main on a generated course) switches it to `CheckedCourse`.
- Plan deviation: the approach powers start at 0.02, not the 0.30 the plan asked about. The last rest spot is often a lay-up about 8 m short, and 0.30 already carries about 8.5 m, so with 0.30 every approach from there flew off the island (12-20% false failures).

### 2026-11-09: Surfaces and hazards (`feature/m3-surfaces-hazards`, PR 6b)

#### Added

- `CourseHazards` (green and pin, hazards by role), `SafeRoute` (+ `RouteWorld`), `HazardGeometry` (`HazardOutline`: 24-point true outline by bisection, metric distances), `CoursePlan` on `CourseLayout.Plan` (hazard roles, shot lines, safe route, missing roles). Generated courses now have a green guard (30% water), a hazard to carry at each dogleg corner that a 90% shot can cut, a hazard across the drive on straight par 4/5s, an optional second guard, and flanks beside the safe route's rest spots, up to the drawn count (par 3 2-4, par 4 3-5, par 5 3-6; a gap counts as one). The hole stays, the green moves 0.5-1 m away from its guard. Water is about one hazard in five.
- A second random stream (surfaces) for green, pin and hazards: islands, centreline, fairway, relief and elevation of a seed are unchanged (a test compares them with hazards off).
- Ponds get a per-pond water level (rim spread at most 0.35 m, water 0.22 m under the lowest rim point and 8 cm under the drawn rim); ponds can run off the island edge (water edge, no tufts).
- `LevelGenSettings` (and asset): shot model copy (`minCarry` 1, `fullCarry` 26, `fullReach` 32, `cutPower`, `layupPower`, `safeStep`), per-par `hazardCount`, `secondGuardChance`, `driveZoneChance`, and the hazard, green and pin, and safe route fields.
- Tests: `CourseHazardTests`, `SafeRouteTests` (5), `LevelGenShotModelTests` (1); explicit `CourseHazardMeasurements` (ace sweep, generation time, seed survey, strategy maps). 40 new tests. EditMode: 629 tests, 618 pass, 11 explicit skipped, 0 failed.
- Captures in `Docs/Screenshots/m3-surfaces-hazards/` and strategy maps in `strategy-map/`.

#### Changed (plan deviations, each one setting or one line to undo)

- Safe route is found by counting the fewest shots back from the safe target, then walking forward; greedy dead-ended before gaps.
- `safeRouteExtraShots` = 1: the route may take par shots (plan: par - 1). With 0, about one par 5 in eight had no route on the bare course (a gap close to the green).
- `waterFallsBackToSand`: a pond that finds no place is retried as a bunker (plan: slot stays empty). Without it a third of drive zones stayed empty.
- `fullReach` is 32, not 30: `ShotReach.FullPowerDistance` is 31.97 (a test pins it within 1 m).
- Pin: the extra offset along the green's side instead of along the approach, so the hole is always `pinOffset` toward the guard. The hole cannot keep 1.5 m from the green border (it already sits 1.4 m off the centre): measured minimum border distance over 200 seeds: 1.38 m (par 3), 1.24 m (par 4), 1.22 m (par 5).
- Tuned to reach 85% complete courses: safe capsule half-width 1.5 m (2.0 left no room for flanks on a 10 m island), shot corridor 2.0, bunker radius 0.8-1.5 m, `bunkerFromEdge` 0.4, front guard angle 10-35 degrees, 24 tries per slot; ponds point across the edge, corner and flank ponds draw around the cut line; a drive-zone gap counts as the drive zone's hazard; the final approach of the safe route is 1.2 m wide and needs only the last metre of its middle on the green.

#### Notes

- Complete courses (all required roles and a route), 200 seeds per par: par 3 199, par 4 171, par 5 174. Generation takes 14.7, 25.2 and 33.1 ms per course (Editor, Mono; not measured on the phone).
- Ace sweep over generated par 3 seeds 0-49: mean 1.17%, max 1.73% (seed 32), 41 seeds at or above 1%, full power never holes. Above the 1% target: PR 7's job (hazards only change the line, not the lie at the hole).

### 2026-11-07: Hazard shapes (`refactor/m3-hazard-shapes`, PR 6a)

Data and rendering support for PR 6b's hazards. No generator change, nothing visible on any scene.

#### Changed

- Per-pond water level: `PondLayout` (outline + `WaterLevel`), `CourseLayout.Ponds` is now a list of them. `CourseFields.WaterLevelAt` returns the level of the pond whose field is lowest at a point (the layout's default level with no pond). `TerrainBuilder.Lower` (bank walls, water, water edges) and `BallGround.LandingHeight` / `DrawnHeight` use it. Hand-made courses wrap their one pond at the same level, so every pin holds.
- `EllipseShape.Heading` (degrees from +X toward +Z, default 0): `CourseFields` rotates into the ellipse's frame when it is not 0, and runs the exact old code when it is.
- Smooth sand on generated courses: on `AlongCentreline` courses the facet noise is multiplied by `1 - bunkerCalm` in `Height` and `FeltHeight`. Hand-made courses keep the exact path.
- `CourseFields.BroadHeight` / `BroadSlope`: the ground without facet noise (same masks, swells, green tilt and elevation as `Height`), for PR 6b.

#### Added

- `PondLevelTests` (6) and `EllipseHeadingTests` (3).

### 2026-11-07: Seeded elevation (`feature/m3-elevation`)

#### Added

- `ElevationProfile` (plain C#): knots along the centreline (distance, height, side slope), joined by monotone cubic Hermite curves with Fritsch-Butland tangents. Flat before the first knot (the tee) and after the last (the green), so both sit on level ground. With evenly spaced knots the slope never exceeds twice a step's rise over its run. `CourseLayout.Elevation` carries it; hand-made courses get `Empty`, which adds exactly 0 (`Par3PinTests` hold).
- `CoursePath.Blend`: reads a value that depends on distance along and across the path leg by leg, blended with weights that fade over `legBlend` (4 m) from the nearest leg. `Project` alone jumps by about 5 m on the inside of a 35 degree bend; the blend doesn't, and is exactly one leg's value away from bends.
- `CourseFields.Elevation`, and `Height` and `FeltHeight` include it (the ball feels every slope). `SkirtBottom` is the profile height minus the depth, so a raised or lowered stretch keeps its earth band.
- `CourseGenerator`: after the earlier draws (outlines are unchanged), the green's height, the inner knots' heights, their side slopes, then the green's tilt (2-6%, whole degrees). Heights are whole centimetres, every draw stays where the green is still reachable within the slope limit, camber changes at most 2% per knot.
- `LevelGenSettings` (and its asset): per par `fairwayRelief` (0.5 / 1.0 / 1.2 m), `greenHeight` (+-0.6 / 1.2 / 1.5 m), `knotSpacing` (6 / 8 / 9 m); shared `maxAlongSlope` (9%), `maxCamber` (4%), `maxCamberStep` (2%), `greenTilt` (2-6%), `plateauMargin` (1 m), `legBlend` (4 m).
- `ElevationTests` (21): the curve (flat ends, no overshoot, slope bound, no terraces), the blend, determinism, ranges, flat tee, under 1% across 1.5 m around the hole, green tilt, gradient limits, no step at bends, the skirt, and the ball on a 9% climb, a camber, a tilted green and the ground's second differences. EditMode: 580 tests, 573 passing, 7 explicit measurements skipped.
- Captures in `Docs/Screenshots/m3-elevation/`.

#### Changed

- `LevelGenSettings.flatRadius` 1.2 -> 1.35 m: the green tilt reached 1% at 1.41 m from the hole on a 6% tilt, short of the 1.5 m decided in M3. This also widens the flat ground at the tee and its noise mask a little.
- The approach zoom frames the hole at its own height (`60cb654`), so a raised or lowered green still sits about 62% up the screen.

#### Notes

- Measured over 60 seeds per par, steepest felt gradient on the fairway: 7.7% (par 3), 10.1% (par 4), 10.2% (par 5); on the green: 6.4%, 7.4%, 6.0%. The green tilt's fade ring into the rough reaches 13%; the slope anywhere on a bent island, 11.9%.
- A par 3 is only 18-26 m long, so the slope limit lets its green sit at most about 0.6 m from the tee. The 1.5 m range is reachable on par 4s and 5s.
- Ponds don't exist on generated courses yet. In PR 6 a pond's level (`Lower()`, `BallGround._waterLevel`) has to become relative to the profile at the pond.

### 2026-11-07: Outline generator (`feature/m3-outline-generator`)

#### Added

- `CourseGenerator.Generate(seed, par, settings, variant = 0)`: a seeded course as a plain `CourseLayout`. Steps, in a fixed order on one `SeededRandom`: length, centreline (par 3 straight along +X; par 4 and 5 a dogleg of 15–35° half the time, a par 5 dogleg turning twice half of those; the others soft bends of 3–8°; no leg ever beyond 35° of +X), stretches of land (one gap of 4–10 m on about one par 4 or 5 in three, at least 15 m past the tee and 10 m before the hole; an island green on about 15% of par 3s), one turned oval island per leg piece (running on 5 m past a bend so the legs join), then the fairway along the centreline and a plain green at the hole. The course is centred on the origin. Ground stays flat as on the hand-made courses; elevation (PR 5), hazards and the tee and hole strategy (PR 6) and the checks (PR 7, which use `variant`) plug in after these steps.
- `SeededRandom`: a seeded `System.Random` drawn from in whole numbers only (percent, centimetres, whole degrees), so a seed gives the same course in the Editor and on the phone. Seeds go through `CourseNoise.Hash(seed, par, variant)`.
- `LevelGenSettings` (and `Settings/LevelGenSettings.asset`): every range above, per par. Par 4s get a 60% gap chance: a par 4 dogleg rarely has room for a gap 8 m clear of its corner, so this keeps about one in three overall (146 of 400 seeds on par 4, 128 on par 5).
- `CoursePath.Project` (distance along and signed distance across, end legs running on) and `PointAt`.
- `TerrainView` can generate its course: set its `generator` settings, `seed` and `par`, otherwise it reads the hand-made shape as before.
- `Sandbox_Generated` scene (`Sandbox_Dogleg`'s set-up, via MCP), seed and par on the `Terrain` object, read at Play.
- `CourseGeneratorTests` (27) and `GeneratedShadowTests` (5, plus 3 explicit measurements). EditMode: 553 tests, 546 passing, 7 explicit measurements skipped.
- Captures in `Docs/Screenshots/m3-outline-generator/`: straight and island-green par 3s, a dogleg each way, a gap par 4, a par 5 dogleg with a gap, a par 5 turning twice with a gap, each at the default zoom and fully zoomed out.

#### Changed

- `CourseLayout.AlongCentreline`: on generated courses (and now the dogleg fixture) the fairway, its mowing stripes and the calm band of the ground relief follow the centreline instead of X. Stripes run along each leg and bend at the corner: a proposal for the open question in `ART_DIRECTION.md`, which stays open until the director judges the captures. Hand-made courses keep the old code path exactly; `Par3PinTests` hold.
- Noise offsets on `IslandOutline`, `FairwayLayout` and `GroundRelief`, so generated islands of one size don't share a wobble. 0 on hand-made courses.
- `DoglegCourseShape`: its fairway now follows both legs (it ran along X and left the second leg bare). Its shadow-fade pins moved by up to 0.17 m.

#### Known issues

- Shadow fade, decided with the director: generated par 4s and 5s reach past the fade start (56.8 m) by design. Over 40 seeds on 9:21, the framed stretch at the worst heading is 66–73 m on par 4 and 72–82 m on par 5, and even at the default zoom the top of the screen shows ground about 67 m away, because a 40° pitch looks far ahead and a long island fills the top of the screen. Narrower islands and a shorter framing didn't bring it under (par 5 still 67 m). Par 3s stay within it (53.8 m at worst on 9:21, tested). Shadows simply fade on the far ground; whether it shows, especially once M4's trees cast shadows, is judged on the Honor in PR 9. The long-course test only stops it from growing.
- A course turning right leaves the screen on the right when fully zoomed out from the tee (the overview faces the first leg); the player pans, and at rest the camera turns with the leg.
- Short stretches (an island-green par 3's main island, a first island before a gap) read as wide ovals rather than long narrow islands; to tune with the widths once elevation and hazards are in.

### 2026-11-07: Sun per level (`feature/m3-sun-per-level`)

#### Added

- `SunHeading` (plain C#): the sun's yaw for a course heading. It is the scene's authored yaw (215° on every scene, right for +X) plus the camera's yaw offset from +X, so the sun keeps the same place on screen as the camera's starting heading and sits top-left on average. Pitch (52°) is unchanged. `CourseHeading` is tee to hole on a bent course and +X on a straight one, like `CameraHeading`, so `Main` keeps its sun exactly (its hole is 0.4 m off the axis).
- `LevelSun` (thin MonoBehaviour): captures the authored rotation and applies the yaw once. `LevelBootstrap` calls it at level start, before the clouds, which bake their tones from the sun's direction. Nothing turns it afterwards, so shadows never move. Wired on `Main` and the four sandbox courses; scenes without the field keep their sun.
- `SunHeadingTests` (12). EditMode: 518 tests, 514 passing, 4 explicit measurements skipped.

#### Notes

- Captures at −80°, 0° and +80° are in `Docs/Screenshots/m3-sun-per-level/`. They turn the camera and the sun around `Main`'s +X island in Play Mode (not saved), so the island is seen side-on and the clouds keep the tones baked for 0°. The sun stays top-left of the screen at all three by construction; the shadow fade (56.8 m) is unchanged.

### 2026-11-06: Approach framing (`feature/m3-approach-framing`)

#### Added

- `ApproachFraming`: near the hole, the camera at rest comes just close enough to put the hole `approachHoleViewportY` up the screen (0.24, about 62%), with the ball at its rest spot (30% up, which keeps room for a full pull below it). No threshold: the zoom grows smoothly from the default zoom to `approachClosestZoom` (1, the closest the player can zoom) as the ball nears the hole. On `Main` it starts closing in at about 10 m and is fully in by about 4 m. Beyond a full-power shot (where the heading follows the leg) it stays at the default zoom.
- `CameraFollow.Approach`: the rest ease zooms around the ball (which stays put on screen) while it turns and eases it to its spot; the reset view takes the approach zoom; the level start too (on every fixture the tee is far, so it is the default zoom, pinned). Never in flight. It wins over a manual zoom, which lasts until the next shot. The twist never changes the zoom, and a twist or a finger during the rest ease stops it where it is.
- `CameraNavigator.ZoomAbout(point, zoom)` and `FreePositionWith(…)` (the framing without the limits, so the zoom doesn't depend on where the island's edge is).
- `ApproachFramingTests` (27). EditMode: 506 tests, 502 passing, 4 explicit measurements skipped, as before.

#### Changed

- `CLAUDE.md` (Key systems: Approach framing) and `ART_DIRECTION.md` §9 record the decisions taken with the director: continuous closeness, ball and hole framed together, the approach zoom wins at each rest, and it engages like the heading (at rest and on the reset view only).
- Shadow fade: the approach camera is never higher than the default one (tested on the four fixtures), so nothing new lies past the 56.8 m fade start.

### 2026-11-06: Two-finger twist (`feature/m3-camera-twist`)

#### Added

- A two-finger twist turns the view up to ±`maxTwist` (45°) around the automatic heading, alongside the pinch zoom and the two-finger pan, and they can happen together. The angle comes from the line between the fingers on the ground (not on screen, where perspective distorts it), and the view turns around the ground between the fingers, so the ground under both fingers stays under them.
- `TwistFilter`: a dead zone (`CameraRigSettings.twistDeadZone`, 4°) so a plain pinch never wobbles the heading; only the part past it counts, so the view doesn't jump as it engages. Fingers closer than `twistMinSpread` (0.1 viewport units) are ignored, since their angle is noise.
- `CameraNavigator.ApplyGesture` now returns the angle the fingers turned, and `GroundUnder(viewport)` is public. `CameraFollow.TwistBy(degrees, pivot)` turns from the view's real yaw, within the limit.
- `CameraTwistTests` (26). EditMode: 479 tests, all passing.

#### Changed

- `CameraFollow.SetTwist` no longer ends the follow during a shot (it used to put the follow back to idle, so the next frame took the shot for a new one and cleared the twist). It still ends the camera's own eases (rest ease, reset view), so nothing fights the fingers. The twist lasts until the next shot or the reset view, as decided in PR 34; a twist made during flight is kept at rest.
- No glide after releasing a twist: it is bounded, and a view still turning after the fingers lift is hard to stop on the angle wanted.
- Same blocking as the other camera gestures: blocked while aiming, allowed during flight.

### 2026-11-06: Camera heading and island limits (`feature/m3-camera-yaw`)

#### Added

- `CameraView` takes a yaw as well as the fixed pitch (`CameraView(yaw, …)`, `WithYaw`), never a roll. `IsoCameraConvention.YawFor(heading)` shows any ground heading up the screen with the same slight lean as +X (yaw 80 for +X, 115 for a leg turned 35° right), and `HeadingOf` is its inverse.
- `CameraHeading`: the automatic heading for a ball at rest. It faces the hole when the hole is within a full-power shot (`ShotReach.FullPowerDistance`, flat fairway, carry and roll, about 30 m), otherwise the direction of the centreline leg the ball is on (+X on a straight course). Slopes and rough are left out, so the heading is one predictable number per level.
- `CameraFollow` turns the camera only at rest: when the ball stops it picks the heading once, then turns around the ball (which keeps it in place on screen) while easing it to its rest spot. The reset view turns to the same heading. During flight the camera only translates. A twist hook (`SetTwist`, `Twist`, `Heading`) for PR 3d; the next shot and the reset view clear the twist.
- `CameraNavigator.TurnTo(yaw, pivot)`, `SetPose`, `PositionWith(…, yaw)`, `View`, `Yaw` and `ViewCentre`.
- `IslandArea`: the view's centre stays over the course's islands, or within 3 m of them or of the centreline, at every zoom and heading. The centreline keeps a way across every gap. The nearest allowed point is exact, so a pan sliding along the limit stays smooth.
- `CameraRigSettings`: `islandMargin` (3 m), `headingSmoothTime` (0.5 s), `headingMinTurn` (5°: a new heading closer than that to the current one is ignored, so a straight course never turns by a hair), `holeHeadingMinDistance` (0.3 m, just past the hole's capture radius, so a short putt faces the hole too), `holeHeadingMaxTurn` (180°, no cap) and `maxTwist` (45°).
- `CameraHeadingTests` (35), `IslandAreaTests` (15) and new `CameraPathTests` (15) and `ShadowDistanceTests` (14 plus 4 explicit measurements). EditMode: 453 tests, all passing.

#### Changed

- `holeHeadingMaxTurn` caps how far the heading toward the hole may turn from the ball's leg. It ships at 180°, the decision taken literally, which the director chose after trying a 45° cap: a ball past the green turns the view to face the hole, and the course then runs down the screen. A lower value keeps the course running up it.
- The zoomed-out view is no longer locked: it pans over the island like any zoom. PR 33's corridor, `doglegMaxStepBack`, `restLookAhead` and `restMaxShift` are gone, since each leg is now seen along its own heading. `CameraFraming.FitCourse` returns the zoomed-out camera, framed facing the first leg; its height is the one scale on every heading.
- Clouds: `IslandCamera.VisibleGround` covers every zoomed-out view the limits allow, at every heading the camera can take (twist included), so the cloud layer is larger than before.
- `TerrainView.Edge` and `BallView.Shot` are exposed for the camera; `LevelBootstrap` passes the island's edges, the hole and the full-power reach.
- `Main` and `Sandbox_LongCourse` keep their framing, their level start and their rest after a shot along the axis, at the convention's exact rotation (pinned).

#### Known issues

- Shadow fade (starts at 56.8 m). Framed stretch at the worst heading, then at the starting heading, on 9:16 / Honor / 9:21: par 3 49.1 / 54.9 / 57.0 and 48.1 / 54.0 / 56.1 m; long course 68.0 / 76.2 / 79.2 and 58.6 / 67.8 / 71.1 m; gap 56.8 / 65.3 / 68.3 and 55.4 / 63.8 / 66.7 m; dogleg 63.6 / 72.3 / 75.5 and 55.8 / 65.0 / 68.4 m. The par 3 is past the fade start only when turned on 9:21 (by 0.2 m). The three sandbox fixtures were already past it at their starting heading before this PR, unchecked until now.
- With the zoomed-out view free to pan, the par 3 viewed from its tee end shows its far tip 67 m away on 9:21, past the fade start. At the default zoom the farthest island point on screen stays at 55.2 m.

### 2026-11-06: Camera rotation decisions (`feature/m3-camera-rotation-plan`)

#### Changed

- The camera may now turn, which `CLAUDE.md` and `ART_DIRECTION.md` used to forbid. Its pitch stays about 40° and it never rolls.
- Automatic heading: the camera turns itself only at rest, while it eases onto a stopped ball, and on the reset view. It faces the hole when a full-power shot can reach it (from `ShotSettings`), otherwise the heading of the leg the ball is on. During flight it only translates.
- Twist: a two-finger twist turns the view up to ±45° around the automatic heading, next to the pinch zoom. It lasts until the next shot or the reset view.
- Limits: the view's centre stays above the island, with about 3 m of margin, at every zoom and heading. This replaces PR 33's zoomed-out corridor and the axis-aligned rectangle. Fully zoomed out keeps one width-set scale on every course.
- Sun: set once per level from the course's tee-to-hole heading, so it sits top-left of the screen on average, then never moves. To check at the extremes (about ±80°) when built. A time of day per level (variants A and C) may come later; a day-night cycle during play is not planned, because a moving sun makes crisp shadows shimmer.
- Consequence for the outline generator (PR 4): with each leg seen along its own heading, PR 33's limit of about 12 m width for legs turning right goes away. The shadow fade check (the farthest framed point within the 56.8 m fade start up to 9:21) now applies along each heading.
- Planned PRs before the generator: camera heading and island limits (`feature/m3-camera-yaw`), twist gesture (`feature/m3-camera-twist`), sun per level (`feature/m3-sun-per-level`).

### 2026-11-06: Camera along the course's centreline (`feature/m3-camera-path`)

#### Added

- `GroundPath` (`IsoGolf.Rendering`): a polyline on the ground whose end legs run on, with projection (distance along, offset across), points along it and sideways offsets with mitred or per-leg corners.
- `CameraFraming.FitCourse(points, centreline, …)` frames a course along its centreline. `CourseFrame.Corridor` is the polyline the zoomed-out view travels, as moves from `Home`; `CorridorLength` is its length. On a bent course the first stretch is laid along the first leg, every later leg is centred on screen by measuring the outline, and the corridor runs on until the course's last quarter stretch is in view.
- `CameraNavigator` takes that corridor. Zoomed out, the view's centre stays on it; zooming in, the allowed area is every blend of a corridor point and a ground-limit point, which only grows with the zoom and still lands zoomed-out views on the corridor. The nearest allowed point is found exactly, with no search.
- `CameraFollow`: on a course that bends, the rest spot (and the reset view) moves the ball sideways, toward the side the course turns away from, so the course ahead crosses the view. `CameraRigSettings.restLookAhead` (10 m) and `restMaxShift` (0.25) tune it. A straight course keeps the rest spot exactly.
- `CameraRigSettings.doglegMaxStepBack` (1): how much further back the zoomed-out view may go so a leg running diagonally on screen keeps its whole width. At 1, every course keeps the same scale, as `CLAUDE.md` asks; on the dogleg fixture the second leg's sides then reach up to 1.08–1.21 of the half-screen at the overview's middle row. At 1.5 everything fits, 1.32–1.38× further back, which also shrinks the ball at every zoom on that course. The director picks by playtest.
- Turned islands: `IslandOutline.CenterZ` and `Heading`, and the same on `ExtraIsland`. An island on the axis is measured untouched. `CourseShapeSettings.centrelineBends` adds bends between tee and hole.
- Test fixture `DoglegCourseShape` (55 m par 4, a leg along +X, then 35 degrees toward −Z, i.e. right on screen) and `Sandbox_Dogleg` scene (`Sandbox_LongCourse`'s setup on the fixture). Its relief, fairway and stripes still run along X until PR 4.
- `GroundPathTests` (6), `CameraPathTests` (28 cases) and `DoglegFixtureTests` (5). The pins hold `Main`'s par 3 and `Sandbox_LongCourse` at their framing from before this PR on 9:16, 9:21 and 1280×2800, and the straight corridor equals the old rectangle limits over 300 random moves. EditMode: 406 tests, all passing.

#### Changed

- A centreline with a single leg (every straight course) frames along +X exactly as before, whatever its hole's small offset to the side.
- `IslandCamera.SetIsland(points, centreline)`, `VisibleGround` covers the whole corridor's footprint, and `LevelBootstrap` passes the layout's centreline.

### 2026-11-06: Several islands in one course (`feature/m3-multi-island`)

#### Added

- A `CourseLayout` can hold several islands with open sky between them. `IslandOutline` has a `CenterX`; the island field is the union of every outline, so a point in a gap is `CourseSurface.OffIsland`, and a ball landing or rolling into it is lost and returns to where the shot was played through the existing `BallSimulation` path (unchanged).
- Terrain, earth band and tufts already follow the island field, so each island gets its own outline, fringe and band, with no face or tuft across a gap.
- `EdgeSegment.Inner` (`EdgeSurface.Land` or `Water`) tags each edge piece with the surface just inside it. Where a pond reaches the island edge the builder now also emits water pieces, at the water level. `TuftBuilder` skips them, so the tufts don't change; a later PR can use the tag for a waterfall.
- `CourseShapeSettings`: `CenterX` and `ExtraIslands` for hand-made courses with a gap. Empty by default, so the par 3 and the long course are unchanged.
- Test fixture `GapCourseShape` (two islands, 4.5 m of gap at the tips, a pond reaching the first island's edge, the green on the second) and `Sandbox_Gap` scene (Main's setup, no trees, which would hover over the gap).
- `MultiIslandTests` (12) and `Par3PinTests` (4). The pins fix the Main par 3's land, water, earth band and tufts, so this PR is shown not to move them. EditMode: 367 tests, all passing.

#### Fixed

- `CourseFields.Island` was exactly 0 on the axis past an island's tip, so a grid corner out in the gap counted as lying on the border and a thin spike of ground reached into the gap. It is now positive there. The par 3 is unaffected (its tips fall on grid corners; pinned).

### 2026-11-06: Course layout and course noise (`refactor/m3-course-layout`)

#### Added

- `CourseLayout` (`IsoGolf.Generation`): plain, read-only course data, the shape M3's generator will output. It holds a list of islands (`IslandOutline`), a centreline from tee to hole (`CoursePath`, straight for now), the fairway, greens with their own tilt, bunkers, ponds and swells as lists, the ground relief, the water level, tee, hole, flat areas and the edge band.
- `CourseNoise`: a smooth 2D gradient noise in plain C#, with an integer hash and an optional seed, so a course is the same in the Editor and on Android. Its values run from 0 to 1, 0.5 on whole numbers.
- `CourseNoiseTests` (pinned values, range, smoothness, seed) and `CourseLayoutTests` (layout from the hand-made shape, lists of surfaces, no ponds or bunkers). EditMode: 351 tests, all passing.

#### Changed

- `CourseShapeSettings.ToLayout()` turns the hand-made shape into a `CourseLayout`. `CourseFields`, `CourseHeightField`, `TerrainBuilder`, `TreeBuilder` and `BallGround` read the layout, and `TerrainView.Layout` replaces `TerrainView.Shape`. Neither course asset changed.
- `CourseFields` uses `CourseNoise` instead of `Mathf.PerlinNoise`, so the par 3's and the long course's edges have a new wobble of the same size. On the par 3 the island's half-width moved 0.32 m on average (1.09 m at most) and the fairway's edges 0.38 m (0.82 m at most). Clouds keep their own noise.
- Hole-in-one sweep on the par 3, before and after: unchanged. The fine sweep holes 61 of 6171 (0.99%) at powers 0.87 to 0.93, the grid 0 of 1331, and full power never holes.
- `SandboxTrees`: two trees by the tee moved 0.5 to 0.7 m inward, from z ±4.2 and 4.0 to ±3.5, as the island's waist there is now narrower.
- `TerrainMeshData` keeps slivers down to about 0.5 mm². The par 3's bunker tip now grazes a grid corner, and dropping its slivers opened a crack of about 1 cm.
- `BallOnCourseTests`: water pieces may sit 0.1 field units past the pond's outline, as land pieces already could (one sits at 0.029).
- URP shadow distance 70 m to 71 m (approved by the human). The par 3's far tip moved, and the framed camera is now 56.1 m from it on a 9:21 screen, past the old 56 m fade start. Figures for each screen are in `Docs/Measurements/m1-5/README.md`.
- Follow-up for the outline generator (PR 4): a generated course, framed fully zoomed out, must keep its farthest point, the earth band under its tips included, within the shadow fade start (56.8 m) up to 9:21. Check it in a test rather than by raising the shadow distance again. (Written after the merge in d33d2dd and left on the branch; restored here by PR 4, which settles it.)

### 2026-11-06: M3 island generator decisions (`feature/m3-plan`)

#### Changed

- Lengths, tee to hole: par 3 18–26 m, par 4 40–55 m, par 5 65–85 m. A par 3 never sits within 3 m of the full-power distance (about 30 m on flat fairway).
- Doglegs: par 3 stays straight; about half of par 4s and 5s turn once by up to 35°, and a par 5 may bend twice. The others get soft bends. A course always keeps heading toward +X. The camera's zoomed-out view will travel along the course's centreline instead of a straight line along +X.
- Long-course scale: every par zooms out to the same width-set scale. Fully zoomed out, a long course shows mostly whole as an overview; at the default zoom it's partly visible. `CLAUDE.md` and `ART_DIRECTION.md` no longer say long courses are only partly visible.
- Gaps: about one par 4 or 5 in three has one gap, 4–10 m wide, never right in front of the tee; now and then a par 3 has an island green. A ball in a gap returns to where the shot was played, like one off the island. A lay-up spot always clears the gap with 70% power or less. This closes the open question in `ART_DIRECTION.md`.
- Elevation, moderate: fairway rises and dips up to about ±1.2 m with slopes up to about 10%, the green up to 1.5 m above or below the tee and tilted 2–6%, under 1% across at least 1.5 m around the hole, and a flat tee.
- Strategy, risk and reward: hazards guard the direct line at the green, at a dogleg's corner and across a gap, and a safe route always exists. Holes in one: under 1% of the `ShotSweep` on every generated par 3, about 0.5% on average over seeds, never at full power.
- Determinism: same seed, same course in the Editor and on the phone. The generator gets its own C# noise instead of `Mathf.PerlinNoise`, and makes every choice from integers. Seeds may change with generator updates until release.
- Hand-made courses: the generator outputs a plain `CourseLayout`. `CourseShapeSettings` stays as one source of it, and the par 3 stays as a fixed test course. `Main` plays a generated course. `Sandbox_LongCourse` and `Par5SandboxCourseShape` are removed once a generated par 5 replaces them.
- Left room for: M4 props and ball collisions (shot corridor, prop zones), no tufts where water meets the edge and a waterfall (edge segments tagged by surface), M5 level flow (the course's +X end; M5 picks the par per seed).
- Open: on a dogleg, whether the mowing stripes follow each leg, to judge on a capture.
- Planned PR order: course layout refactor, multiple islands, camera path, outline generator, elevation, surfaces and hazards, course checks, generated `Main`, phone checks.

### 2026-11-06: M2 phone checks (`feature/m2-phone-checks`)

#### Added

- `Docs/Measurements/m2/`: phone measurements on the Honor 500 Pro (development build, 60 fps cap) and the player's answers to the by-eye checks. Raw per-frame data in `dev-build.csv`.
- `PhoneMeasurement` (`IsoGolf.Measurement`), compiled only with the scripting define `ISOGOLF_MEASURE`, so no normal build contains it. It plays shots through synthetic touches and writes a CSV of frame timings and the prediction's cost.
- A `ProfilerMarker` around `AimPreview.Predict` (no cost in release builds).

#### Measured

- Aim prediction on the phone: arcs p50 0.075 ms, p95 0.09 ms, p99 0.19 ms, worst frame 1.0 ms (about 2,800 predictions per scene). Putts: one 3 s aim, p50 0.235 ms, max 0.49 ms. The longest putt wasn't reached, so its cost on the phone is still unmeasured.
- Frames during shots with camera follow at the default zoom: 60 fps with no frame over 16.54 ms in about 19,300 frames; CPU main thread p50 3.6 to 4.4 ms, GPU p50 8.2 to 9.0 ms (development build).
- By eye, the player passed slopes (for the current soft ones), the ball's offset from facets in the rough, the surface border differences, the camera follow, the default zoom and reset button, the sky near the island's edge, and the tuned shot values.
- M2 is ready to close. Left over: render scale (needs a low-end phone), the longest putt's cost, and rechecking slopes once M3 steepens them.

### 2026-11-06: Horizontal camera follow (`feature/m2-camera-follow-horizontal`)

#### Changed

- `CameraFollow`: a flying ball is now also held between about 40% and 60% of the screen's width, on top of the vertical band. Like the lower edge of the vertical band, the side edges only count once the ball has been inside them, so a ball resting off-centre after a pan doesn't make the camera jump at launch. Until then a wide backstop (0.85) applies, so it can't leave the screen.
- `CameraRigSettings.followZoneSide` 0.8 to 0.2 (default and asset). The camera's limits are unchanged: a framed par 3 fully zoomed out stays locked, a long course zoomed out only travels along +X, and from the default zoom inward the camera follows sideways. On a corridor-limited view the two bands can't both be met, so the camera settles on a compromise.
- Measured in Play Mode on `Sandbox_LongCourse` (1080x2400, default zoom, one full-power pull along +X from the tee, camera follow at 60+ fps): the ball peaked at x +0.23 (band edge 0.2) and y +0.28 (edge 0.2), came back to x 0.00 and rested at y -0.40. One run; not yet checked on the phone.
- Tests: three new `CameraFollowTests` (side band, no jerk at launch, side backstop); the long-course past-the-zone test now allows 0.05 of compromise.

### 2026-11-05: M2 tuning (`feature/m2-tuning`)

#### Added

- `ShotSweep` and `ShotSweepTests` (EditMode, no scene): plays shots from the tee at the hole over a range of aims and powers and counts what holes out, for any `ShotSettings`. It runs at 0.01 power steps as well as the old 0.05 grid. Tests pin that the sweep is repeatable, that holes in one stay rare (under 1.5% of the fine sweep) and that full power never holes.

#### Changed

- `ShotSettings`: `maxCarry` 22 to 26 and `launchAngle` 40 to 36 (apex at full power 4.6 m to 4.7 m, so camera framing and follow are unaffected); fairway restitution 0.30 to 0.40 and tangent keep 0.55 to 0.45; green restitution 0.25 to 0.30 and tangent keep 0.50 to 0.45; rough restitution 0.20 to 0.25. Bounces are livelier and the landing spot matters more. Everything else (pull curves, carries, capture and lip-out, gesture radius, aim preview) is unchanged.
- Why: a full-power shot always holed out when aimed within about 1 degree, because any pull past 30% of the screen is exactly power 1. Full power now ends 2 m past the hole; a hole in one needs a power of about 0.87 to 0.93, so a pull within roughly 11 px, as well as the aim.
- Sweep, par 3 (28.2 m), +-6 deg at 0.1 deg: grid (power 0.50 to 1.00 at 0.05) 11 of 1331 holed before, 0 after; fine (0.01 steps) 42 of 6171 before at powers 0.97 to 1.00, 61 of 6171 after at powers 0.87 to 0.93. A straight full-power shot used to land at x=8.3, roll 6.8 m and stop 0.84 m past the hole; now it lands at x=12.0, rolls 4.3 m and stops 2.1 m past.
- Left for the phone: whether slopes read, the ball sitting up to 17.5 cm off a facet in the rough, and whether the livelier hops (about 0.9 m at full power) feel right.

### 2026-11-05: Reset view button (`feature/m2-reset-view`)

#### Added

- A small reset view button in the top right, inside the safe area. Pressing it eases the camera back to the default view: the default zoom, with the ball at its rest spot. It takes 0.35 s (`CameraRigSettings.ResetSeconds`), and a finger on the screen or a shot ends it where it is.
- It shows only while the ball rests and nothing is being aimed, so nothing else is on screen during a shot, the flight included (decided with the player).
- `ResetViewButton` draws it as a mesh in front of the camera, with no Canvas or EventSystem: a see-through cream disc, and a ring and dot in warm dark `#3A2E25`. `ResetViewButtonLayout` and `ResetViewButtonState` hold the placement and press rules as plain C#: a press counts when the touch began and lifted on the button, and sliding off or a second finger cancels it.
- `CameraFollow` has a reset phase, and `CameraNavigator.PositionWith` works out the target once, limits applied.
- `AimMesh.AddRing`, button colors in `PaletteSettings`, and `ResetViewButtonSettings` with its asset, with starting values for the tuning PR.
- Tests for the layout, the press rules, the reset ease, `PositionWith` and the ring, and a button check in `MainSceneTests`.

#### Changed

- `IslandCamera.TouchClaim` is now `AddTouchClaim` and `RemoveTouchClaim`: claims add up, so the button and the ball can't overwrite each other. `SlingshotShotInput` and `SandboxBallTouchClaim` use them.
- `IsoGolf.UI` now references `IsoGolf.Rendering` and `Unity.InputSystem`.

### 2026-11-05: Camera follow (`feature/m2-camera-follow`)

#### Added

- `CameraFollow`: moves the camera for the shot in play, at the player's zoom. It stays still until the ball reaches a band between about 40% and 60% of the screen's height, glides to keep it there, and eases until a stopped ball sits about 30% up the screen with the course ahead. A finger during a shot pauses this until the ball has stopped and the finger has lifted, and a finger during the ease cancels it, so an aim never starts on a moving camera. Outside a shot it does nothing, so a pan is never fought.
- The band's lower edge only counts once the ball has reached it, so a shot from low on screen doesn't pull the camera backward at launch. A backstop and side limits keep a backward or sideways shot on screen.
- `BallCameraFollow`: passes the ball's position, free of the course's floating, and its state to the camera.
- Long courses: `CameraFraming.FitCourse` frames a course longer than `framedLength` in stretches, and `CameraNavigator` lets the zoomed out view travel along +X. A par 3 keeps its exact framing and stays locked when zoomed out.
- `Sandbox_LongCourse` and `Par5SandboxCourseShape`: an 80 m hand-made course without trees, to test the follow until M3 generates courses.
- Follow, rest and long course settings in `CameraRigSettings`, all starting values for the tuning PR. The rest spot leaves room for a full pull on phones from 9:16 to 20:9, but not quite on a 3:4 tablet.
- `CameraFollowTests`, corridor tests in `CameraNavigatorTests` and `CameraFramingTests`, and a follow check in `MainSceneTests`.

#### Changed

- Every level now starts at a default zoom, halfway between fully out and the closest, instead of fully zoomed out, after a playtest. A par 3 still fits whole when fully zoomed out. `CLAUDE.md` and `ART_DIRECTION.md` record this.
- `CameraNavigator` reaches its full limits from the default zoom, not only at the closest zoom, so the ball can rest at its spot along the whole course.
- `IslandCamera.VisibleGround` covers the whole corridor, so clouds don't run out on a long course.
- `LevelBootstrap` starts the view on the ball.

### 2026-11-05: Aim preview (`feature/m2-aim-preview`)

#### Added

- `AimPreview`: what the player sees while aiming, in one mesh under `Course`. An arc shows dots through the ball's flight and a ring where it first lands. A putt shows dots low on the ground along the first 40% of its true path by distance, with no end marker, and the lightest putts keep one dot so they still show a direction. Both can reveal a hazard, and a putt can reveal a lip-out.
- The power gauge: a small yellow bar with a dark border above the ball, the same size on screen at any zoom or distance. It's a mesh rather than UGUI, so it floats with the course and adds no Canvas. It shows power after the pull curve.
- `GaugeMotion`: the gauge's fill eases toward the power, rising and falling, and starts from empty on each aim. Only the drawing eases: the shot always uses the real power. The dots follow the real power at once.
- `BallSimulation.PredictPutt` and `PredictLanding`. A predicted putt plays the real shot on a private copy of the simulation, so it can't disagree with the shot, and allocates nothing.
- `DottedPath`, `AimMesh`, `AimPreviewSettings` and its asset, with starting values for the tuning PR.
- Inner radius on `GroundDisc`, so the landing ring drapes over facets as the shadow does.
- Aim mark and gauge colors in `PaletteSettings`.
- `DottedPathTests`, `AimMeshTests`, `PredictedPutt_*` and `PredictedLanding_*` tests, ring tests in `GroundDiscTests`, and an aim preview check in `MainSceneTests`.

#### Changed

- `IsoGolf.UI` now references `IsoGolf.Gameplay` and `IsoGolf.Generation`.
- `BallView` exposes `Ground` and `Radius`, for the preview.

#### Fixed

- The course's floating no longer changes a shot's power or direction under a still finger. From the grab on, `SlingshotShotInput` reads the ball where it was grabbed.

### 2026-11-05: Shot input (`feature/m2-shot-input`)

#### Added

- `IShotInput`, the swappable input, and `SlingshotShotInput`, its drag on the ball: pull back and let go to shoot, and let go back on the ball to cancel. The direction runs from the finger through the ball on screen, and power comes from the pull on screen, so it's the same in every direction and at every zoom. Nothing shows the aim yet, until the aim preview PR.
- `ShotGesture`: the gesture as plain C#. A second finger is ignored while aiming, and a touch the system takes away cancels the shot.
- `ShotController`: plays released shots, and returns a holed ball to the tee after 2 seconds, until M5's level flow.
- Gesture settings in `ShotSettings`: touch radius 8% of the screen width, full power at 30%, and no hold time. Power follows a curve over the pull, so light shots get more of it: squared for a putt, to the power of 1.5 for an arc. All are starting values for the tuning PR.
- The lightest arc now carries 1 m, down from 1.5 m, after a playtest found light shots too strong.
- `ShotGestureTests`, and a slingshot check in `MainSceneTests`.

#### Removed

- The temporary `TestShotTap`, `TapGesture` and `TestShotAim`, so tapping the ground no longer plays a shot.
- `BallView.Ground` and `BallView.Shot`, which only those classes used.

### 2026-11-05: Lip-out (`feature/m2-lip-out`)

#### Added

- `HoleRim`: the rule for a ball crossing the hole's rim, with no randomness. The same line at the same speed always ends the same way.
- A ball too fast for its line lips out: it curves away from the center, as tight as the hole, and loses speed. A dead-center ball carries on straight and a graze barely changes. This replaces running straight over the hole.
- A ball that uses up its speed while still over the hole drops in.
- `Lip_*` tests in `BallSimulationTests`.

#### Changed

- Capture is fairer for central hits: the fastest capture depends on the line, from 2.6 m/s through the center down to 1 m/s at the rim, in place of 2.2 m/s for every line. Lines far enough off center now lip out at speeds that used to drop. All values are for the tuning PR to judge.
- `ShotSettings`: `holeCaptureSpeed` is replaced by `centerCaptureSpeed`, `rimCaptureSpeed`, `lipOutMaxTurn` and `lipOutSpeedKeep`.

### 2026-11-05: Ball in the scene (`feature/m2-ball-in-scene`)

#### Added

- `BallView`: the ball in `Main`, under `Course`, drawn from `BallSimulation` in the course's local space. It has 80 facets, spins as it moves, and casts no shadow of its own.
- `BallShadowView` and `GroundDisc`: the ball's shadow, a disc mesh draped over the facets and over the water, trimmed at the island's edge, with no depth texture.
- `HoleView`: the hole, a dark disc on the ground, and the flag, whose flutter waits for M4.
- `GroundShadow`, an Unlit transparent Shader Graph using vertex colors and alpha, and its material.
- `PlayPieceSettings` and its asset, `PlayPieceBuilder`, and ball, shadow, hole and flag colors in `PaletteSettings`.
- `TestShotTap`, `TapGesture` and `TestShotAim`: **temporary**, until the shot input PR. Tap the ground to play the ball toward that spot, and tap after holing out to return to the tee.
- `GroundDiscTests`, `PlayPieceBuilderTests`, `BallViewPartsTests`, and ball checks in `MainSceneTests`.

#### Changed

- `TerrainMeshData.ToMesh`, moved out of `TerrainView` so the ball and flag can share it.
- `IsoGolf.Gameplay` references `Unity.InputSystem`.
- `CLAUDE.md`: a new first section, the Unity agent workflow, asks for a compile through the MCP after every script change, never asks the human to focus the Editor, and asks to fix console errors until the build is clean.

### 2026-11-05: Ball simulation (`feature/m2-ball-simulation`)

#### Added

- `BallSimulation`: the ball's flight, bounces, roll, hole capture and lost ball, as plain C# in the course's local space at a fixed time step. Nothing in a scene uses it yet.
- `BallisticArc`: a flight as an exact formula of time, with the search for where it meets the ground. The ball and the coming aim preview read the same formula, so they can't disagree.
- `ShotSettings` and its asset: power sets a carry or a putt distance, and each surface has its own bounce and rolling friction. An arc from the rough carries 75% as far and one from a bunker 50%, by the human's decision on the penalty, with the amounts to tune. All values are starting points for the tuning PR.
- `IBallGround` and `BallGround`: the ball sits and lands on `Height`, feels `Slope`, and takes water and the island's edge from the outlines, not from height.
- `CourseSurface` and `CourseFields.Surface`, in the order the terrain draws surfaces.
- `BallisticArcTests`, `BallSimulationTests` and `BallOnCourseTests`.

### 2026-11-04: Smooth ground, tee, hole and slopes (`feature/m2-height-field`)

#### Added

- `CourseHeightField`: the ground as the ball meets it, as smooth Catmull-Rom curves over the terrain grid. The ball sits on a curve through the visible vertices, and feels the slope of a second curve, which on the fairway and green leaves out the facet noise. It also gives the height of the visible facet at any point.
- Tee and hole positions in `CourseShapeSettings`, with flat ground around both.
- `CourseHeightFieldTests`.

#### Changed

- The `Main` par 3 has hand-set slopes so M2 can test the ball on them: a rise and a dip on the fairway, and a green tilted by 5%, flat around the hole. M3 generates slopes from a seed.
### 2026-11-04: Camera glide (`feature/camera-momentum`)

#### Added

- `PanMomentum`: a camera drag keeps gliding and slows down after release, like vertical scrolling on a phone. Any finger landing stops it, a finger that pauses before lifting doesn't glide, and it stops at the limits with no bounce.
- Glide settings in `CameraRigSettings`.
- `PanMomentumTests`, and glide tests in `CameraGestureRouterTests`.

#### Fixed

- `IslandCamera` ran the gesture router twice in a frame when no finger was down and a mouse was present, which would have doubled the glide.
### 2026-11-04: M2 shot decisions (`feature/m2-plan`)

#### Changed

- Shot gesture: a drag starting on the ball, pulled back like a slingshot, with no hold time by default. Releasing back on the ball cancels.
- Putt preview: dots along the first part of the true path, about 40%, with no end marker. This closes the open question in `ART_DIRECTION.md`.
- Putting from just off the green: the green's drawn border is the strict limit, and just outside it a shot is a short arc.
- Longer courses: the camera stays still until the ball nears the top of the view, then glides after it, and at rest eases until the ball sits low on screen. This closes the open question in `ART_DIRECTION.md`.
- A finger on the screen during flight pauses that follow, and the camera eases back to the ball once the ball has stopped and the finger has lifted.
- A ball in the water or off the island returns to where the shot was played after about a second. Gaps between islands stay with the M3 plan.
- Rolling uses Catmull-Rom curves over the terrain grid: the ball sits on one through the visible vertices, and feels the slope of another, which leaves out the facet noise on the fairway and green. This keeps the approved facets, chosen on the phone over calmer terrain.
- The ball shadow is a disc mesh following the facets, with no depth texture.
- The camera drag gets momentum: it keeps gliding and slows down after release.

### 2026-11-04: M1.5 performance and the look in `Main.unity` (`feature/m1-5-performance-main-scene`)

#### Added

- `FrameRateCap` and `PerformanceSettings`: the game now asks for 60 fps. The 30 fps seen in the sandbox was Android's default cap, not a real cost.
- `Docs/Measurements/m1-5/`: phone measurements, and the design for the automatic render scale, which waits for a test on a low-end phone.
- `MainSceneTests` and `ShadowDistanceTests`, and a `LookSettingsTests` check that the depth texture stays off.

#### Changed

- `Main.unity` now holds the current look, copied from `Sandbox_Island`: `Course`, terrain, trees, tufts, clouds, sky, light and camera. The ten test trees stay until M4.
- `IsoGolf_RPAsset`: the depth texture is off, by the human's decision. It was left over from the pixel pipeline, nothing reads it, and removing it took the GPU from about 9.7 ms to 5.9 ms per frame on the phone, with only sub-pixel changes along edges.
- The 70 m shadow distance and the 15,440 triangle clouds stay, after measuring them.

### 2026-11-04: M1.4 tufted island edge and outline decision (`feature/island-tufts`)

#### Added

- `TuftBuilder` and `TuftSettings`: a seeded, continuous grass fringe along the island's dry edge, in its own mesh under `Course`, so it floats with the island. `Sandbox_Island` uses it.
- `TerrainResult.Edge`, the pieces of the island edge on dry ground.
- Tuft colors in `PaletteSettings`.
- `TuftBuilderTests`.

#### Changed

- `ART_DIRECTION.md`: the grass edge is a fringe of small, short, blunt tufts folding over the edge, chosen on the phone over separate prisms and over longer or sharper teeth.
- `ART_DIRECTION.md`: no outline for now, after phone tests at about 2 px and about 1 px, with a recheck in M4 once real props are in.
- `ART_DIRECTION.md`: there is no fringe where water reaches the island's edge. Water falling from there is an idea for later.

### 2026-11-04: Island floating (`feature/island-float`)

#### Added

- `CourseFloat` and `CourseFloatSettings`: the whole course rises and falls very gently, about 0.15 m each way over 7 seconds, judged on the phone. `Sandbox_Island` has a new `Course` container holding the terrain.
- `CourseFloatTests`.

#### Changed

- `CLAUDE.md`: the ball's simulation runs in the course container's local space and never reads the float offset, so floating can't change a shot. All islands of a course float together.
- `CLAUDE.md` and `ART_DIRECTION.md`: a course may have gaps, as separate islands that belong to it, with the details left to the M3 plan.

### 2026-11-04: Blue sky and clouds (`feature/floating-clouds`)

#### Added

- `CloudBuilder`, `CloudSettings` and `CloudView`: seeded faceted clouds below the island, in one mesh with one draw call, drifting very slowly. `Sandbox_Island` uses them.
- `CloudUnlit`, an Unlit Shader Graph using vertex colors, and the `Clouds` material.
- `CameraFraming.GroundFootprint` and `IslandCamera.VisibleGround`, so clouds cover exactly what the framed view can see.
- `CloudBuilderTests`.

#### Changed

- The island now floats in a light blue sky above clouds instead of on a white gradient, chosen from side-by-side options on the phone and in Play Mode. This closes the open question in `ART_DIRECTION.md`.
- `BackgroundGradient` has a Top Color property, which was fixed to white inside the graph.
- `PaletteSettings` has cloud colors, and `LookSettingsTests` checks the sky and clouds instead of the grey gradient.
- `TreeBuilder` shares a new `Icosahedron` class with the clouds.

### 2026-11-04: M1.3 materials and lighting (`feature/materials-lighting`)

#### Added

- `TreeBuilder` and `TreeSettings`: seeded low poly cauliflower trees and cypresses, built into one mesh with the terrain material. `SandboxTrees` places ten of them on `Sandbox_Island`, so shadows can be judged.
- `ShadowTint`, a custom function in `LowPolyVertexColor` that tints cast shadows cool through Emission, while facets that merely face away from the sun keep the warm ambient.
- `BackgroundGradient`, an Unlit Shader Graph used as the skybox: white fading to `#E4E4E4` at the bottom of the screen.
- `LookSettingsTests`.

#### Changed

- The look was chosen on the phone from side-by-side options: crisp shadows, plain lighting (no toon ramp), the gradient background, cool shadows, today's ambient and a lighter rough (`#6A8541`).
- `IsoGolf_RPAsset`: shadow distance from 150 m to 70 m and soft shadow quality from Medium to Low, which is what makes shadows crisper.
- `ART_DIRECTION.md`: settles shadow crispness and the toon ramp, and adds islands floating above clouds as an open idea.

### 2026-11-04: Slopes and putting planned (`feature/plan-slopes-and-putting`)

#### Changed

- Slopes join the plan: the ball reacts to them from M2, and M3 generates elevation by seed and par, starting gently. Cliffs, plateaus and elevated tees wait for a later version.
- On the green, every shot is a putt along the ground, with no aerial arc.

### 2026-11-04: Unity diagnostics off (`fix/disable-unity-diagnostics`)

#### Changed

- Engine diagnostics and analytics submission are off, by decision. Both had been on from the project template.

### 2026-11-04: M1.2 terrain mesh (`feature/terrain-mesh`)

#### Added

- `TerrainBuilder` and `CourseFields`: build a faceted island from a course shape, cutting every triangle along each surface's exact border, with mowing stripes, an earth band under the edge and pond banks. The land mesh is checked to be watertight, so no pinholes show the background.
- `CourseShapeSettings` with the par 3 shape, and `PaletteSettings`, with their assets.
- `LowPolyVertexColor`, a URP Lit Shader Graph using vertex colors, and the `Terrain` material.
- `TerrainView`, and `LevelBootstrap`, which builds the terrain and points the camera at it.
- `Sandbox_Island.unity`.

#### Changed

- `IslandCamera` can frame the real island outline instead of a box.

### 2026-11-04: M1.1 camera framing and gestures (`feature/camera-framing`)

#### Added

- `CameraFraming`: fits an island box on any portrait screen shape with the fixed perspective camera.
- `CameraNavigator`, `CameraGestureRouter` and `IslandCamera`: one-finger drag to move around and pinch to zoom around the point between the fingers, kept within the island. A touch starting on the ball is left alone for the shot. Fully zoomed out always returns to the framed view. Mouse drag and wheel do the same in the Editor and for WebGL later.
- `CameraRigSettings` and its asset, for the screen fill, closest zoom and wheel step.
- `Sandbox_Camera.unity`, with a stand-in island.

#### Changed

- The shot starts with a hold and drag on the ball, and touches elsewhere move the camera. Camera gestures are blocked while aiming and allowed during flight. M2 connects these to the real shot.

### 2026-11-04: Pixel pipeline removed (`feature/remove-pixel-pipeline`)

#### Changed

- `Main.unity` now uses the perspective camera: 45° field of view at rotation (40, 80, 0), with the sun moved to stay at screen top-left.
- `IsoCameraConvention` and its tests follow the new camera.
- `IsoGolf_RPAsset` has one renderer, renamed `IsoGolf_Renderer`, with 4× MSAA, a 150 m shadow distance and a 2048 shadow map, as in the look test.

#### Removed

- The M1.1 pixel pipeline: `PixelCamera`, `PixelPresenter`, `PixelPresentFeature`, `PixelGrid`, `PixelCameraSettings`, their tests, the present renderer and `Sandbox_PixelPipeline.unity` with its materials.

### 2026-11-04: Art direction changed to low poly (`feature/art-direction-low-poly`)

#### Changed

- The art direction moves from the pixel diorama to a low poly diorama, chosen on the phone from a look test: perspective camera at a 45° field of view, course running up the screen with a slight lean to the right, native resolution with 4× MSAA, flat-shaded facets and clean surface borders.
- The fairway uses long mowing stripes, with no tile or checkerboard pattern.
- The Mediterranean palette and white background stay.
- Camera framing depends on course length: par 3 courses fit entirely in one view, and longer ones are partly visible with the camera moving along them. This replaces the level-opening pan decided for the pixel look.

#### Removed

- The pixel diorama is now variant F. Its unmerged snapping work stays on `feature/pixel-snapping`, and the M1.1 pixel code still in `develop` will be removed in its own PR.

### 2026-11-04: M1.1 pixel render path (`feature/pixel-render-path`)

#### Added

- `IsoGolf_RPAsset` with a game renderer and a present renderer in `Assets/_Project/Rendering/`: HDR off, depth texture on, soft shadows on, render scale 1, no volume profile.
- `PixelCamera`, `PixelPresenter` and `PixelPresentFeature`: the world renders into a low-res texture, then a second camera upscales it to the screen with nearest-neighbour sampling.
- `PixelGrid`, which works out the integer scale, view size, render size and orthographic size for any screen, with tests. It picks the scale closest to 270 px wide, with no black bars, at 24 game pixels per unit.
- `PixelCameraSettings` and its asset.
- `Sandbox_PixelPipeline.unity`, with a grey-box test island and sandbox materials.

#### Changed

- The `Mobile` quality level now uses `IsoGolf_RPAsset`.
- Camera decision: each level opens with a slow pan from the hole back to the tee, replacing the brief wide view, because an animated zoom would make the image shimmer.

### 2026-11-04: Unity AI packages removed (`feature/remove-unity-ai-packages`)

#### Removed

- `com.unity.ai.assistant` and `com.unity.ai.inference`, with the packages they pulled in, their scripting defines (`SENTIS_ANALYTICS_ENABLED`, `APP_UI_EDITOR_ONLY`), the `com.unity.dt.app-ui` build config entry and the assistant's settings file. CoplayDev is the only Unity MCP on this project.

### 2026-11-04: Portrait and camera framing (`feature/portrait-orientation`)

#### Changed

- The game is now forced portrait, with a 270×480 starting render resolution (previously 480×270).
- Company name set to Hamylka.
- Camera decision: the play axis stays bottom-left to top-right. Each level opens on a brief wide view of the island, then the camera pans to follow the ball.

### 2026-11-04: M0 project bootstrap (`feature/project-bootstrap`)

#### Added

- `Assets/_Project/Scenes/Main.unity`, the main scene and only build scene: orthographic camera at rotation (30, 45, 0), white background, warm directional sun from screen top-left.
- `Assets/_Project/` structure with one assembly definition each for `Gameplay`, `Generation`, `Rendering` and `UI`. `Art/`, `Prefabs/` and `Rendering/` will be created when first used, because Git does not track empty folders.
- `IsoCameraConvention`, the single definition of the camera orientation.
- EditMode test assembly with tests for the iso compass and the 2:1 projection.
- `CHANGELOG.md`.

#### Changed

- Target platform is now Android; WebGL comes later, once the core game works. The Editor uses the `Mobile` quality level and `Mobile_RPAsset`.
- `InputSystem_Actions` moved to `Assets/_Project/Settings/`.
- `ART_DIRECTION_EN.md` renamed to `ART_DIRECTION.md`, the name `CLAUDE.md` refers to.
- `CLAUDE.md` adapted to this game: removed sections from another project (telemetry, sync, `Docs/Results` reporting, hardware protocol), added the target platform, and made CoplayDev the only Unity MCP.

#### Removed

- Template content: `SampleScene`, `TutorialInfo`, `Readme.asset`.

#### Fixed

- CoplayDev MCP server failing to start: under Python 3.15, installing it fell back to compiling PyYAML from source, which needs MSVC. Fixed on the development machine with `uv python pin --global 3.13`; no project file is involved.
