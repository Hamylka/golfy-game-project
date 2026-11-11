# Split fairway plan: rough islands, split fairways and forks

Status: **decided** (2026-11-11, `docs/split-fairway-plan`). The director answered the 9 open questions "Tout en A"; the answers and the hub's two accepted notes are recorded at the end, and the sections below are written from them. This is a plan only: no code, scene or asset changes. Implementation is PRs F3.1–F3.4, all before M4's PR 4.7 (look recheck), so the look and the prop density are judged on the new shapes.

The director's idea (memory note `split-fairway-idea`, 2026-11-11): a generated fairway should not always be one strip. Three shapes: a rough island in the middle of the passage; the fairway split into several parts; a Y fork into two or more strips that may merge again toward the end. Aims: more varied, more strategic courses, and room for props (trees on the rough between branches) without breaking "never on a shot line".

## What this builds on (state at `develop` 575af14)

- **The fairway is one field.** `CourseFields.Fairway` is a strip along the centreline: `FairwayLayout` half width 2.5 m plus a 1.6 m noise wobble, from 2 m behind the tee to `CourseGenerator.FairwayEnd` (into the green, or 3.7 m short behind a rough apron, F2), with rounded tips (`fairwayCapDepth` 2.5 m). Its zero line is the drawn border.
- **Every consumer reads only that field**: `TerrainBuilder` cuts triangles along it (`SplitAll(_fields.Fairway, InFairway)`) and colours by the side of the cut; `BallGround.Surface` → `CourseFields.Surface` gives Fairway or Rough; `FeltHeight` calms the facet noise within 1 m outside the border; `PropPlacement.OverFairway` keeps every footprint plus `PropRules.fairwayMargin` (0.3 m) off it. **Hazards and `SafeRoute` never read it**: a landing capsule is tested against hazards and the island only. So a new fairway shape reaches the ball, the mesh and the props through the field alone.
- **Stripes** are lines of constant `Across` (signed distance from the centreline), alternating every `stripeWidth` (1 m). They run along each leg and are continuous everywhere on the island.
- **Width is the binding limit.** An island is `islandHalfWidth` 4.6–5.2 m half wide with a 2.2–3.2 m wobble, so about 9–10 m across. Props keep 2 m either side of every shot line (`ShotCorridorHalfWidth`), and on a straight course the `Direct` line (tee or drive spot → hole) runs down the centreline, as does the safe route (rest spots on the centreline, capsules 6.5 m × 3 m). So the middle of the passage is inside the direct and safe corridors.
- **The camera measures the real outline width** (`CameraFraming.FitCourse`, across the first leg) for the zoomed-out scale; the shadow fade starts at 56.8 m. Widening an island would change both on that course.
- **Streams**: main (length, bends, land, islands, noise offsets, elevation), surfaces (salt 6: green and pin, then hazards), props (salt 7). Accept/reject tests compare whole centimetres; discrete choices are integer draws.
- **Reference figures**: props per checked course 5.2 / 9.0 / 14.2 (par 3 / 4 / 5, after PR 4.4); reroll survey seeds 0-199: par 3 1, par 4 29, par 5 26 rerolled, none exhausted; generation + checks 14–37 ms on the phone (M3 PR 9), plus props since 4.3; par 3 ace sweep mean 0.49%, max 0.78%.

## 1. Shapes and frequencies (Q1: A, Q7: A)

Three shapes, at most one per course, except that a par 5 may get two (on different legs or stretches). Plain courses stay the majority on par 3.

| Shape | What it is | Par 3 | Par 4 | Par 5 |
|---|---|---|---|---|
| Plain | today's one strip | ~80% | ~45% | ~35% |
| **Rough island** | an oval of rough inside a locally widened strip, at least 2 m of fairway on either side | ~20% | ~20% | ~20% |
| **Split** | a rough band across the strip; the fairway comes in parts, each with rounded tips | — | ~15% | ~15% |
| **Fork** | two branches round a spine of rough, rejoining before the fairway's end tip | — | ~20% | ~30% |

All weights are integer percentages in `LevelGenSettings` (a `fairwayShapes` block per par); the shape kind is one integer draw. **The fork share depends on Q4 A** (no outline change): F3.1 measures how often a fork fits. If that is far below the target, F3.3 goes back to the director with Q4 B (a local bulge) as the fallback, rather than silently lowering the frequency (hub note 1, accepted).

### Sizes (starting values, tunable)

- **Rough island**: 2.5–5 m long, 1.2–2 m wide (whole centimetres), an oval with the hazards' small wobble, turned along the centreline. The strip widens around it: half width + oval half width + 2 m of fairway on each side, eased in and out over 4 m. Placed at a full drive's landing (about 26–31 m from the shot's origin on par 4/5, beyond the ≤ 24 m safe lay-up), or mid-fairway on a par 3.
- **Split**: a rough band 1.5–3 m along the centreline, across the whole strip. The pieces either side end in rounded tips (`fairwayCapDepth`), so the band reads as rough between two tips, never a straight cut.
- **Fork**: the spine is at least 3.5 m wide (Q5 A needs this for props) and 8–14 m long, with rounded ends. Branches are 2.2–2.6 m wide (safe branch the wider). Branch centres sit about ±(spine half + branch half) from the centreline, so the fairway spans about 8–9 m and needs an island half width near 5.4 m there. Branches open and close along the spine's rounded ends, so the shape reads as a Y opening and a Y closing.

### Combinations (Q7: A)

- **No shape across a dogleg bend**: every shape keeps the bend clearance gaps use (`GapStarts`' window around each bend). Soft bends are allowed. Forks round a dogleg corner (the inside branch as the cut) are a possible later PR (Q7 B).
- **Tee and gaps**: no shape within 6 m of the tee, or within 6 m of a gap's tips. A split never sits just before or after a gap (it would read as a second gap).
- **Island green**: no shape on the island green's own island (its tee island may have a rough island, par 3).
- **Fairway ends (F2)**: forks rejoin at least `fairwayCapDepth` (2.5 m) before the fairway's end, so the end tip, the run into the green and the apron are untouched. No shape reaches within 2 m of the fairway's start tip.
- **Capsules**: a rough island or a split never overlaps any safe-route landing capsule or the final approach (see §3).
- **Hazard roles** stay as they are, with one addition on forks (§3, `BranchGuard`). Rough islands, bands and spines are not hazards: they don't count toward the hazard count (Q8 A).

### Sketches

Plan view, tee on the left, hole on the right (world +X); `=` fairway, `.` rough, `#` island edge.

Rough island (par 3, 4 or 5):

```
####################################################
#........========================..................#
#...==============....=============================#
#T=============.........============================H
#...==============....=============================#
#........========================..................#
####################################################
                ^ rough oval at a full drive's landing, the strip widened round it
```

Split (par 4 or 5):

```
######################################################
#...=================).........(====================.#
#T===================)..band...(=====================H
#...=================).........(====================.#
######################################################
                     ^ 1.5-3 m rough band, rounded tips either side: carry it or lay up short
```

Fork (par 4 or 5), safe branch below (wider, carries the safe route), risky branch above (carries the direct line):

```
###############################################################
#...............[S]=======================.....................#   risky branch: direct line, BranchGuard [S]
#.......=========(.......................)=========...........#
#T===============(  spine: prop cluster  )====================H
#.......=========(.......................)=========...........#
#...............=============================.................#   safe branch: wider, safe route rests here
###############################################################
                 ^ spine >= 3.5 m wide, 8-14 m long, rounded ends; no shot line crosses it
```

## 2. Representation (Q2: A, Q3: A)

### Fairway pieces in the centreline's frame

`FairwayLayout` gains a list of **pieces** and a list of **rough ovals**:

- A `FairwayPiece` is a strip described in the main centreline's frame: a range along it (`From`, `To`, metres from the tee), an across **offset** (constant, or eased in and out at its ends with a smoothstep over a given length), a **half width** (likewise eased), and a rounded tip at each end (cap depth, 0 for a flat join where two pieces overlap). It reuses today's field: project once on the centreline (`CoursePath.Project` → `s`, `side`), then `side - offset(s)` stands in for `side`.
- A rough oval is an `EllipseShape` (the same type bunkers use, turned along the centreline), cut out of the fairway.
- **Fairway field = min over pieces (union), then max with −oval (subtraction).** One `Project` call per point, whatever the number of pieces.
- **A plain course is exactly one piece**, offset 0, today's half width, wobble and caps, and no ovals. F3.1 proves every plain course is bit-identical to today (fields sampled on a grid, terrain mesh hash, `CourseLayout` equality).
- A rough island: the main piece plus one widening piece over the oval's span, and the oval. A split: two pieces with a band between their tips. A fork: a main piece before, two offset branch pieces, a main piece after, with the spine as the rough between the branches. The spine's shape comes from the branches' rounded ends and eased offsets, so no extra primitive is needed.

Rejected (Q2 B): one `CoursePath` per branch. It is more general, but every consumer (`Across`, stripes, elevation, `FeltHeight`'s relief, the camera) would have to choose a centreline, the stripes would need seams at the fork and the merge, and the field would cost one projection per branch.

### What stays "the centreline"

- **Camera heading** (`CameraNavigator`, `headingLookAhead`) and **approach framing**: unchanged. A ball on a branch projects onto the main centreline, as a ball in the rough does today.
- **Elevation profile**: unchanged. It runs along the centreline with its side slope across it, so two branches 4–5 m apart can sit a few centimetres to about 20 cm apart in height (a 4% side slope gives about 18 cm). This is a free source of difference between branches and is kept as it is.
- **Mowing stripes (Q3 A)**: the main centreline's stripes, unchanged. They stay continuous through the fork and the merge with no seam. A branch stays within about 12° of the centreline at its eased ends and parallel to it along the spine, so stripes on a branch run along it. (Q3 B, stripes per branch, would add a visible seam where the branches meet.)
- **`FeltHeight`**: the fairway calm reaches 1 m outside the border, so a narrow rough oval (1.2–2 m wide) feels almost calm, like the axis today, while the drawn facets follow the relief. The ball feels a rough surface (response below) on calm ground. This is accepted; the spine (≥ 3.5 m) keeps rough relief in its middle.

### What else reads the shape

- `CoursePlan` gains `FairwayShape` (kind; spine or oval or band bounds along the centreline; the safe and risky branch offsets on a fork) for the props, tests and debug drawing.
- `CourseLayout` equality and the debug overlay (`DebugCourseOverride`, if it draws the fairway) show the pieces.

## 3. Strategy, shot lines and the safe route (Q5: A, Q6: A, Q8: A)

### Each shape's risk and reward

- **Rough island**: a full drive must pick a side or land in the rough (a shorter roll, `ShotSettings` rough response), while the safe lay-up stops short of it. It is a soft penalty, not a hazard (Q8 A: plain rough, no new surface response).
- **Split**: the band sits beyond a lay-up and short of a full shot's landing, or where a full shot must carry it. Landing in it costs roll, not a stroke.
- **Fork (Q6 A)**: a **wide safe branch** and a **narrower risky branch**:
  - The safe branch is the one on the green's safe side (away from `GuardDirection`). It carries the safe route and leads to the green's safe half (`SafeTarget`).
  - The risky branch is more direct: on a soft bend it is the inside branch (shorter), otherwise the one on the guard's side, whose line toward the pin is straighter. It carries the direct line and a new required hazard role, `BranchGuard`: a bunker (or, 20% of the time, water) beside or in the risky branch, which its line passes within 1.5 m of. On a fork course `BranchGuard` replaces `DriveZone` (a cross hazard over the centreline would only fall on the spine). It is placed by the same candidate machinery, on the surfaces stream, after the green guard and before the second guard (draw order in §5).
  - Both branches rejoin before the green, so the choice is about the drive: the risky branch is shorter or straighter but narrower and guarded, the safe branch wider and longer.

### Shot lines (Q5 A)

- **On a fork, the direct line follows the risky branch**: two `ShotLine`s of kind `Direct`, from `DirectOrigin` to a point on the risky branch's centre near the middle of the spine, then on to the hole. `RuleGuardNearDirectLine` uses the last segment. No shot line crosses the spine.
- **Cut lines** are unchanged (no fork across a dogleg bend, so cut lines never meet a spine).
- **Safe lines** follow the safe route's legs as today.
- **Corridors stay strict** (2 m either side of every line). The spine (≥ 3.5 m) then keeps a strip outside every corridor, and that strip is prop room (§4).
- **Rough islands and splits** sit on the direct and safe lines, so they stay bare (no props).

### The safe route, by construction

- **Rest spots move across onto the safe branch.** `RouteWorld` reads the shape: where the safe branch runs, a rest spot at distance `s` sits at `centreline(s) + left(s) × safeOffset(s)`, and its capsule samples are shifted by the same offset. Elsewhere, offset 0, as today. Steps of at most 24 m (`SafeStep`, as the crow flies), at most par − 1 + 1 shots, gap lay-ups within 70% power: unchanged rules on the moved points.
- **New accept rule** (whole centimetres): a rough oval, a band or a spine never overlaps a capsule sample or a final-approach sample. A capsule is landing ground, and the safe route promises a clean landing.
- **Shape placement re-runs `SafeRoute.Find`** on the bare course with the shape, before any hazard: a candidate that leaves no route is rejected, like a hazard candidate. Every hazard candidate then re-runs it as today. So "a safe route always exists" still holds by construction.
- `CourseChecks` (gap leg, rest near hole) play from the moved rest spots, with the real `BallSimulation`.

### Hazard roles that change

| Role | Change |
|---|---|
| `GreenGuard`, `SecondGreenGuard`, `CornerCarry` | unchanged; `GreenGuard`'s direct-line rule uses the direct polyline's last segment |
| `DriveZone` | not used on a fork course (replaced by `BranchGuard`); unchanged elsewhere |
| `BranchGuard` (new, required on forks) | beside or in the risky branch, its line passes within 1.5 m; water 20%, falls back to sand like the others |
| `Flank` | beside the safe route's rest spots, which may now sit on the safe branch |
| All | a hazard may not sit on the spine or a rough oval (it would erase the shape); new rule, whole centimetres |

## 4. Props (Q5: A)

- **Spine prop room**: the strip of the spine outside every corridor and every capsule, minus `PropRules.fairwayMargin` (0.3 m) from both branches' borders. With a 3.5 m spine and branch lines at the branch centres, that is about 1–2 m wide over 8–14 m. Wider spines give more.
- **Spine theme** (F3.4): a new cluster theme on the props stream (salt 7), one sub-stream keyed after the existing ones so today's clusters stay bit-identical: a short cypress line along the spine, or bushes and rocks, with the shadow rule (a tall prop's shadow out of the corridors) as for any prop. An olive only where the free strip is wide enough for its reach and shadow. The spine cluster is extra to the density counts, like the built clusters.
- **Rough islands and splits** stay bare (they are on the lines).
- **Expected effect** (to measure in F3.4, not measured): roughly +0.5 props per par 4 (20% forks × about 2.5 props) and +1 per par 5 (30% × about 3). From 5.2 / 9.0 / 14.2 to about 5.2 / 9.5 / 15. Small: the density settings (`LevelGenSettings.props`) stay the main way to fill the islands. If a fork's shape leaves a branch's outer rough wider than today, the existing clusters can also use it.

## 5. Determinism and streams

- **New stream, salt 8, "fairway shapes"**, from (seed, par, variant) like salts 6 and 7. Drawn **after `GreenAndPin`** (it reads the guard side and the safe target, without drawing from the surfaces stream) and **before the hazards**. Draw order on it: the shape kind (one integer over the weights); then for each shape a fixed list of numbers (its place along the centreline as an even integer draw over the valid starts, its sizes in whole centimetres, a side as an integer), drawn whether the candidate is accepted or not, so the stream stays aligned. A par 5 that draws two shapes draws the second after the first, on sub-stream 2.
- **The main and surfaces streams don't move.** Their draw counts are unchanged, so every island, slope, green, pin and hazard candidate stays as drawn.
- **Plain courses stay bit-identical** (tested in F3.1 and again in F3.2/F3.3). **Shaped courses change**: their hazards (new rules reject some candidates; `BranchGuard` replaces `DriveZone`), their safe route (moved rest spots), their props, and possibly their reroll variant. Allowed until the first release.
- **Pins that may move**, listed by an `Explicit` measurement in F3.2 and F3.3 and re-pinned in the same PR: `MainSceneTests`' seed and par, F1's par 5 seed 6 heading figures (`CameraHeadingTests`), the phone par 5 seed 33 (`CameraCourse.LongPath`, `CameraPathTests`, `ShadowDistanceTests`), PR 4.4's par 5 seed 120 captures, PR 4.3's prop counts per course. A shape that lands on a pinned course keeps its pin's purpose (a pin may move to another seed with the same feature if needed; said in the PR).
- **Whole centimetres** in every accept/reject test (widths at sample points, clearances to capsules, the spine's free strip); integer draws for every discrete choice.

## 6. Checks and balance

- **`CheckedCourse` rerolls**: shape placement can't fail a course: a shape that finds no valid place leaves the course plain (recorded in the plan, not a missing role). `BranchGuard` is a required role on forks, so a fork whose guard finds no place rerolls like any missing role. Reroll survey (seeds 0–199) re-run in F3.2 and F3.3; target: no par exhausted, rerolls not far above today's 1 / 29 / 26.
- **Ace rate**: par 3 gets only rough islands, off the green, but rough short of the green changes some bounces. The ace sweep (par 3 seeds 0–49, `Explicit`) is re-run in F3.2. Target unchanged: mean under 1%, about 0.5%, full power never holes. The gating test stays.
- **Phone generation cost**: each `Fairway()` call takes a min over at most 4 pieces and a max with at most 2 ovals after one projection, against 1 strip today. It is called per triangle cut, by `FeltHeight`, and on 49 points per prop candidate. Expected a few ms at most on top of 14–37 ms; measured with `COURSE_TIMING` in F3.1 (plain) and F3.3 (forks), Editor figures, the phone in 4.9.
- **Shadow fade**: no outline change (Q4 A), so the course's extent and the 56.8 m fade are unchanged.
- **`BallGround` and putting**: a ball on a rough oval, a band or a spine is on rough (`CourseFields.Surface` already gives Rough there). The green and putting are untouched. The aim preview plays the same simulation, so it can't disagree.

## 7. Tests (per the test policy)

Rule tests run over seeds 0–49 in the default suite, each with an `Explicit` 0–199 twin (`_FullSweep`); statistics are `Explicit("Measurement")` and quoted in the PRs. Per PR, targeted tests only through `run_tests` with explicit `test_names`; full suite not run, scheduled at the end of M4.

- **F3.1**: `PlainCoursesAreBitIdentical` (fields on a grid, mesh hash, every par, seeds 0–49); piece field unit tests (offset, eased width, caps, union, oval cut, a point exactly on a border); measurement `ForkFitShare` (per par, how often an island is wide enough for a 3.5 m spine and two branches, with the clearances of §1) and `FairwayFieldCost`.
- **F3.2**: `RoughIslandsAndBandsNeverTouchACapsule`, `ShapesKeepClearOfTeeGapsBendsAndEnds`, `ShapedCoursesKeepASafeRoute`, `ShapeStreamNeverMovesIslandsOrHazardCandidates`, `ABallOnARoughIslandIsOnRough`; measurements: shape shares per par, reroll survey, ace sweep, moved pins.
- **F3.3**: `ForkBranchesKeepTheirWidths`, `ForksRejoinBeforeTheEndTip`, `NoShotLineCrossesASpine`, `SafeRouteRestsOnTheSafeBranch`, `BranchGuardIsBesideTheRiskyLine`, `NoHazardOnASpineOrOval`; measurements: fork share against the 20% / 30% target (the Q4 B decision point), reroll survey, ace sweep, generation cost.
- **F3.4**: `SpinePropsStayOutOfCorridorsCapsulesAndFairway`, `SpineThemeLeavesOtherClustersBitIdentical`; measurement: props per course per par.

## 8. PR split and order (Q9: A)

All four land **before 4.7**, so the look recheck and the density are judged on the new shapes. 4.6 (birds) may run in between. **Each F3 PR runs as one session at a time on the main checkout** (they share `CourseFields`, `SafeRoute`, `CourseHazards`, `PropPlacement`). Opus for all four (generator, safe route, checks, seeded placement).

| PR | Branch | What | Depends on | Model |
|---|---|---|---|---|
| F3.1 | `feature/fairway-pieces` | `FairwayPiece`, rough ovals, union/subtraction field, `CoursePlan.FairwayShape`; no shape drawn; plain courses bit-identical; measurements `ForkFitShare`, `FairwayFieldCost` | this plan | Opus |
| F3.2 | `feature/fairway-rough-islands` | stream salt 8, shape kind draw, rough islands and splits, placement rules, capsule keep-out, `LevelGenSettings.fairwayShapes`; rule tests; reroll and ace re-measure; moved pins | F3.1 | Opus |
| F3.3 | `feature/fairway-forks` | forks, safe route on the safe branch, direct polyline, `BranchGuard`, hazards off spines; rule tests; reroll, ace and cost re-measure; **if the fork share is far below target, back to the director with Q4 B (bulge)** | F3.2 | Opus |
| F3.4 | `feature/fairway-spine-props` | spine theme on the props stream, density measurement; **director's phone review of the shapes** (a few seeds per par) | F3.3 | Opus |

Then 4.7 (look recheck) as planned, with the shapes in. 4.8 (waterfall) and 4.9 (phone checks) unchanged.

## Follow-ups (not in scope)

- Forks round a dogleg corner, inside branch as the cut (Q7 B).
- Local island bulge for wider forks (Q4 B), only if F3.3's measurement calls for it.
- Deeper rough for spines and ovals (Q8 B).
- Strategic obstacles on direct and cut lines (M4 Q4 B).

## Decisions (director, 2026-11-11, word for word)

Relayed by the M4 hub (second, `local_1c63ac85`):

> Tout en A

The hub's two notes, accepted with the A answers:

1. Q1 A's fork frequencies (20% on par 4, 30% on par 5) depend on Q4 A (no outline change). F3.1's `Explicit` measurement gives the real share of courses where a fork fits. If it is far below the target, F3.3 comes back to the director with Q4 B (a local bulge) as the fallback, rather than silently lowering the frequency.
2. Q9 A: F3.1 to F3.4 all land before 4.7. 4.6 birds may run in between. Each F3 PR is one session at a time on the main checkout.

| Q | Question | Options (A first) | Chosen |
|---|---|---|---|
| 1 | Shapes and frequencies | A all three, one per course (par 5 may get two); par 3 ~20% rough island; par 4 45/20/15/20 (plain/island/split/fork); par 5 35/20/15/30; tunable / B rough islands and forks only / C at least 70% shaped | A |
| 2 | Representation | A pieces in the centreline's frame + rough ovals, one field, one centreline / B one centreline per branch | A |
| 3 | Stripes on a branch | A main centreline's stripes, no seam, branches within ~12° / B stripes per branch, with seams | A |
| 4 | Fork width | A forks only where the island is already wide enough, no outline change / B local island bulge / C narrow forks (2.6 m spine, bushes and rocks only) | A |
| 5 | Shot lines on a fork | A direct line follows the risky branch, strict 2 m corridors, spine holds props / B straight direct line over a bare spine / C height-aware corridors | A |
| 6 | What each branch offers | A wide safe branch vs narrow risky branch with `BranchGuard` (replaces `DriveZone`) / B equal branches / C A plus the risky branch downhill | A |
| 7 | Combinations | A no shape across a dogleg bend, none within 6 m of tee or gap, none on an island green, forks rejoin ≥ 2.5 m before the end / B A plus forks round a dogleg corner | A |
| 8 | Rough island feel | A plain rough, not a hazard / B deeper rough (new surface response) | A |
| 9 | Timing against M4 | A plan, then F3.1–F3.4 all before 4.7 / B plan only before 4.7, implementation after M4 | A |

### F3.2: where shapes sit (director, 2026-11-11, word for word)

Relayed by the M4 hub, after F3.2 measured how often §1's windows yield a place:

> C, partout

§1 placed a rough island "at a full drive's landing" (26-31 m from the shot's origin) and §3 the band between a lay-up and a full carry (20-26 m). On a par 4 those windows sit exactly where the generator puts the bend and, often, the gap, and their clearances then cover the whole window. Share of courses carrying the shape, seeds 0-99, bare variant 0 (rough island / split):

| Window measured from | Par 4 | Par 5 |
|---|---|---|
| A. the tee (§1 as written) | 1% / 2% | 7% / 12% |
| B. the tee and every rest spot of the bare safe route | 2% / 3% | 12% / 14% |
| **C. no window: anywhere the clearances allow** (chosen) | 17% / 14% | 30% / 16% (second shape included) |

So on par 4 and par 5, as on par 3, a shape sits anywhere its clearances allow (tee, gaps, bends, fairway tips, other shapes), clear of every landing capsule and the final approach, with the safe route re-run. `LevelGenSettings.fairwayShapes.windowOrigin` is `Anywhere`; `TeeOnly` and `EveryShot` stay available. The island-edge margin stays at 1 m.
