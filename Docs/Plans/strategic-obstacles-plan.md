# Strategic obstacles plan: props on rough islands, spines and dogleg corners

Status: **decided** (2026-11-12, `docs/strategic-obstacles-plan`). The director answered the 8 open questions and accepted the PR split. The answers are recorded word for word at the end, and the sections below are written from them. This is a plan only: no code, scene or asset changes. Implementation is PRs S1-S2, before M4's 4.7 (look recheck), so the density and the look are judged with strategic props in.

The director's idea (memory note `strategic-obstacles-idea`, 2026-11-12; M4 plan Q4 B): put props (trees, and built props such as sheds and walls) on rough islands and fork spines, and trees in the rough at the inside of some dogleg corners. They should appear often enough to create shot strategy and avoid sameness, but not on every course. This breaks today's rule "never in a shot corridor" for the **direct and cut lines only**. The **safe route and its landing capsules stay clear**, so a safe route always exists by construction. Out of scope, post-M5: tiny floating islets in inside corners, and wind.

## What this builds on (state at `develop` 41dffd3)

- **Keep-outs.** `PropZones.ClearOfPlay` keeps every prop and its shadow `ShotCorridorHalfWidth` (2 m) off **every** `ShotLine` (`Direct`, `Cut`, `Safe`), plus every landing capsule, the tee, the +X end, the green and the bunkers. `PropZones.Fits` adds the edge, rough-only ground, `fairwayMargin` (0.3 m) and the ponds.
- **Safe legs and the direct line overlap.** On a straight course, and on every par 3, the `Safe` legs (rest spot to rest spot on the centreline) and the `Direct` line run almost together down the middle, over any rough island. So opening only the direct and cut corridors frees almost nothing there (measured below).
- **Streams.** Props are salt 7, one sub-stream per cluster. Built props use keys 100-150 and the spine uses `SpineBase` 160, placed last. Plain courses stay bit-identical when a later sub-stream is added after them.
- **Checks.** `CheckedCourse.Generate` picks the variant on the bare course. With props it runs `GapsClear` and `RestsNearHole` (safe-route shots) and drops every prop a check shot struck (`KeepProps`, up to 3 rounds). It does not play the safe legs themselves.
- **Aim preview.** `AimPreview` calls `BallSimulation.PredictHit`, which runs `FindHit`, the same search `Shoot` uses. It returns the first chord hit, raised crowns included (`FallThrough`). So the dots already stop at a crown's contact with the mark, even though the real ball goes on into the leaves at 15% speed (`foliage` along-keep 0.15). Nothing after the hit is shown.
- **Reference figures.** Props per checked course 4.98 / 9.10 / 14.47 (par 3 / 4 / 5, F3.4). Reroll survey 1 / 29 / 26, none exhausted. Par 3 ace sweep: mean 0.59%, max 0.86% (seed 46), full power never holes. Bare generation 53.6 / 88.7 ms per par 4 / 5 (Editor). A cypress fits on 8 of 92 spines (F3.4).
- **No arc height in `Generation`.** The generator mirrors only `minCarry`/`fullCarry` from `ShotSettings`; `Gameplay` references `Generation`, not the reverse.

## Measurements (2026-11-12, Editor, read-only, `execute_code`)

Seeds 0-99 per par, bare variant 0, complete plans only (99 / 88 / 88), 0.5 m grid. Footprints: bush reach 0.6 m and 0.8 m tall, olive 1.3 / 2.3 m, cypress 0.5 / 3.6 m. A spot is "opened" when it passes `Fits` with every other keep-out but fails it today. Rules compared:

- **A**: safe legs keep their 2D corridor (prop and shadow); direct and cut opened.
- **S**: the prop stays out of every corridor; only its shadow may cross the direct and cut corridors.
- **B**: like A, but a prop may stand under a safe leg mid-flight if the leg's planned arc (power bisected to land at the leg's end) passes over its top + ball radius + margin (0.3 or 0.6 m) along the whole footprint. Take-off and landing ends count as no clearance.

Share of courses with an opened spot in a strategic zone (spine, rough island, split band, cut corner):

| Prop | Par 3 (A / B) | Par 4 (A / B) | Par 5 (A / B) |
|---|---|---|---|
| Bush | 0% / 0% | 28% / 33% | 59% / 68% |
| Olive | 0% / 0% | 8% / 30% | 26% / 57% |
| Cypress | 0% / 1% | 14% / 34% | 42% / 65% |

Per zone (courses, today / A / S / B 0.3):

- **Spines** (13 par 4 forks, 31 par 5): cypress 0/6/5/13 and 4/20/19/28; olive 0/5/1/13 and 3/16/0/29; bushes already fit on nearly all today.
- **Splits** (13, 14): every split takes a bush, olive or cypress under B; under A at most 10/13 bushes.
- **Cut corners**: a `Cut` line exists on 3 of 88 par 4s and 26 of 88 par 5s (a corner gets one only when 90% power cuts it). Par 5 under B: bush 26/26, olive 15/26, cypress 26/26.
- **Rough islands** (12, 15, 25): **no prop fits under any rule**. The ovals are 1.2-2 m wide; a bush needs 1.8 m of rough (its 0.6 m reach and the 0.3 m fairway margin on each side).
- **Margin**: 0.3 vs 0.6 m barely matters (par 5 olives on spines: 29 vs 27 courses).

Safe legs, checked courses seeds 0-19: median length 20.3 / 18.4 / 19.8 m; apex above the ground min / median / max 1.41 / 3.55 / 4.19 m (par 3), 1.43 / 3.22 / 4.52 (par 4), 0.92 / 3.60 / 4.70 (par 5). So bushes and olives fit under a leg's middle, cypresses rarely. Playing every safe leg to rest with props costs 0.67 / 2.00 / 3.92 ms per course (Editor, an upper bound for a flight-only check). Today 4 of 54 par 4 legs and 2 of 82 par 5 legs played at their planned power already strike a prop somewhere, most likely after landing (bounce or roll past the capsule), which is why the new check counts flight strikes only.

## 1. Where, which kinds, how often (Q2 B, Q3 B, Q4 A, Q6 A, Q8 A)

| Zone | Kinds | Notes |
|---|---|---|
| **Fork spine** | trees (olive, cypress), bushes, rocks; a shed or a short wall where the spine's free ground holds its whole footprint | the strategic group joins the F3.4 spine theme's ground: the spine cluster may now reach into the direct corridor |
| **Split band** | trees, bushes, rocks; a short wall along the band where it fits | under the safe legs' middle only (Q1 B) |
| **Rough island** (Q2 B) | small props only: a rock or a low bush, reach at most 0.5 m | `fairwayMargin` 0.15 m on ovals only (`props.strategicOvalMargin`); no shape change |
| **Dogleg inside corner** (Q4 A) | trees, bushes, rocks; a wall where it fits | on the `Cut` line, beside the existing `CornerCarry` hazard, never on it (hazard keep-out unchanged) |

- **Kinds (Q6 A)**: trees, bushes and rocks everywhere. Sheds and walls only on spines and corners whose ground holds their whole footprint and shadow. No benches, traces or landmarks as strategic props.
- **How often (Q3 B)**: one strategic group per course, drawn when the course has room, with `props.strategicChance` 60 (an integer percentage, retunable), 1-3 props (`props.strategicMembers`). Expected share with Q1 B: par 3 0-12%, par 4 about 20%, par 5 about 40%. S2 measures the real share.
- **Par 3 (Q8 A)**: included, through rough islands only (12 of 99 par 3s have one), so at most about 7% of par 3s with the 60% chance.
- **Stream**: a new sub-stream of the props stream, key `PropPlacement.StrategicBase` = 170, after the spine's 160, and **placed last**. Earlier props never read later ones, so every other prop stays bit for bit, except where Q7 B changes the spine rule (§5). A course without room or with a missed draw is bit-identical. The zone, the draw, the kinds and the members are integer draws; every accept/reject test compares whole centimetres.
- **Recorded as props, not hazards** (a technical decision, not asked): `CourseProp` gains a strategic flag (and the zone it stands in) for tests and debug drawing. It gets no `HazardRole` and doesn't count in the hazard counts. Play reads only its collision shapes, like every other prop.

## 2. Which corridors and capsules stay clear (Q1 B, Q5)

- **Stays strict, always**: every landing capsule, the tee, the green and 1.5 m around it, hazards with their margins, the +X end, the fairway (with its margin), and each safe leg's **take-off and landing**.
- **Height-aware safe legs (Q1 B)**: a strategic prop may stand within a safe leg's 2 m corridor only where that leg's planned arc passes at least `props.strategicSafeClearance` (0.3 m) over the prop's top (plus the ball's radius), along the prop's whole footprint. Its shadow may then cross that corridor. Every non-strategic prop keeps today's strict rule.
- **Arc height in `Generation`**: `LevelGenSettings` gains a small mirrored shot model (launch angle or apex per carry, mirrored from `ShotSettings` like `minCarry`/`fullCarry`). A test holds it within a few centimetres of `BallSimulation.ArcFor` over the power range, as `ShotReach` is held today. The leg's power comes from the safe route's own carry model.
- **New check, `SafeLegsFly`** (in `CourseChecks.Run` with props on): each safe leg is played from its rest spot at its planned power, and its first flight must strike nothing. Bounces and rolls are not counted (see the measurement above). It costs at most 0.7 / 2.0 / 3.9 ms per course in the Editor, against 54 / 89 ms today. A strike drops the prop struck, like every other check (`KeepProps`).
- **Direct and cut corridors** may now hold strategic props.
- **How much a line may be blocked (Q5, director: "plutôt B mais pas dans 100% des cas (si ça fait sens)")**, the hub's and this plan's reading: by default the group leaves the line playable (A's rule): at least 1.5 m clear across the direct or cut corridor, or every member lower than the full-power apex over it, so it can be threaded or lofted. A **full block** (no gap, too tall to loft) is allowed on a share of placements only, `props.strategicFullBlockChance` **25** (an integer percentage, retunable), and only where it makes sense:
  - never on a par 3, where the direct line is the tee shot;
  - only on a `Cut` line or on a fork's risky-branch direct line, where the safe route is a real alternative of the same number of shots (it reaches the green in at most par − 1 shots, so going round costs no extra stroke to the green);
  - never on the direct line from the tee of a straight course, so the first shot always offers more than one choice.
  25% keeps a fully blocked line a surprise (about 1 course in 20 overall) while most strategic groups stay a risk to judge rather than a wall. If S2 measures that the full-block cases cluster on a few corner shapes, the director retunes the value.
- **The safe route is never recomputed for a strategic prop**: by construction the prop keeps out of every capsule and every leg's flight, and `SafeLegsFly` confirms it with the real simulation. A candidate that fails a keep-out is rejected, and the next slot is tried (`memberTries`, as for the spine).

## 3. Checks, ace sweep, rerolls

- **Rerolls cannot move** (1 / 29 / 26): the variant is picked on the bare course, and a prop that breaks a check is dropped, never the course (director: Q1 A, PR 4.3). The dropped-prop count may rise; S2 quotes it.
- **Ace sweep (par 3)**: strategic props on par 3 sit only on rough islands mid-way under the tee shot, as low props, and keep 1.5 m off the green. Expected effect: no change or a slight drop (today mean 0.59%, max 0.86%). Re-run over checked par 3 seeds 0-49 in S2; the gate stays under 1% per seed with full power never holing.
- **What the checks still don't play**: the direct and cut lines. A strategic group may make them hard or (Q5) impossible; the safe route is what the checks guarantee.

## 4. Aim preview

No code change. `PredictHit` uses the shot's own search, so a shot aimed through a strategic tree shows its dots up to the contact (crown or trunk) with the hit mark and no landing ring. S2 adds a test on a generated course with a strategic tree on the direct line: the preview's hit time and contact equal the played ball's first strike, for a crown (fall-through) and a trunk. The same mark is used for soft and hard props.

## 5. Cypresses on spines (Q7 B)

Today a cypress fits on 8 of 92 spines, because its long shadow reaches a corridor. Decision: on a spine, a cypress's **shadow may cross the direct and cut corridors**; the tree itself stays out of every corridor, and its shadow stays off the safe legs and the capsules. Measured: 5 of 13 par 4 forks and 19 of 31 par 5 forks (about 55%, was about 9%). This changes the spine theme, so **spine props move on fork courses**: S2 recaptures the spine reference and keeps every other prop bit-identical. A strategic group on the spine (§1) may in addition stand in the direct corridor.

## 6. Counts and cost

- **Props per checked course**: 4.98 / 9.10 / 14.47 today. Expected after S2: + about 0.1 / 0.4 / 0.8 on average (1-3 props on about 7% / 20% / 40% of courses), plus the cypress-for-bush swaps on spines (no count change). S2 measures the real figures.
- **Generation cost**: strategic placement is a handful of `Fits` tests per candidate slot with the arc test, similar to the spine (+17 ms per fork course in F3.4). `SafeLegsFly` adds up to 0.7 / 2.0 / 3.9 ms per course (Editor). S1 and S2 quote bare and checked generation in the Editor against 53.6 / 88.7 ms bare and 151 / 251 ms checked; the phone figure comes in 4.9.

## 7. Tests (per the test policy)

Per PR, targeted tests through `run_tests` with explicit `test_names` only. Each PR also runs the placement and checks rule tests on seeds 0-49, with `Explicit` 0-199 twins run by hand only if the director asks. Full suite not run, scheduled at end of M4.

- **S1**: `StrategicKeepOuts` (corridors by line kind; height-aware legs accept a low prop under a leg's middle and reject it at take-off and landing); `ArcHeightModelMatchesSimulation`; `SafeLegsFly` (passes on today's courses, fails with a prop placed under a leg's landing); **`PlainCoursesBitIdentical`** (seeds 0-49 per par: every prop and check result unchanged, since nothing is placed yet); existing `PropPlacement`, `SpineProp`, `CourseCheck` rule tests. Measurement: `SafeLegsFlyCost`.
- **S2**: `StrategicPropsStayOutOfCapsulesAndSafeFlights`, `StrategicGroupLeavesALineOrBlocksItOnlyWhereAllowed` (Q5 rules, never on par 3), `StrategicPropsOnlySmallOnRoughIslands`, `OtherPropsBitIdentical` (against a reference captured before S2, spine aside), `SpineCypressShadowRule`, `PreviewStopsAtStrategicTree`; ace gate tests. Explicit measurements quoted in the PR: strategic share per par and zone, full-block share, props per course, dropped props, reroll survey (must stay 1 / 29 / 26), ace sweep (checked par 3 seeds 0-49), generation cost. Then a **director's phone review** (a few seeds per par with strategic groups).

## 8. PR split (accepted: "découpage OK")

One session at a time on the main checkout (they share `PropPlacement`, `PropZones`, `CourseChecks`). Opus for both.

| PR | Branch | What | Depends on | Model |
|---|---|---|---|---|
| S1 | `feature/strategic-zones` | corridors by line kind in `PropZones`; height-aware safe legs with the mirrored arc model in `LevelGenSettings`; `SafeLegsFly` in `CourseChecks`; settings (`strategicChance`, `strategicMembers`, `strategicSafeClearance`, `strategicFullBlockChance`, `strategicOvalMargin`); no strategic prop placed, every course bit-identical | this plan | Opus |
| S2 | `feature/strategic-props` | placement on sub-stream 170, placed last: spines, splits, rough islands, cut corners; kinds; chance; Q5 block rule; Q7 spine cypress rule; strategic flag on `CourseProp`; preview test; measurements; director's phone review | S1 | Opus |

Then 4.6 (birds), 4.7 (look recheck, with strategic props in), 4.8 (waterfall), 4.9 (phone checks).

## Risks

- **A safe leg played a little short flies lower** than the planned arc. The 0.3 m margin covers the planned power only, and the player's own lay-up may differ. The capsule and the take-off ends stay strict, and the margin is a setting (0.6 m costs almost no room).
- **Mirrored arc model drift**: if `ShotSettings` changes, the mirrored model must follow. The test catches it, as for `minCarry`/`fullCarry`.
- **Full blocks may feel unfair** if they appear where the safe route is much longer; the "same number of shots" condition and the 25% share keep them rare. Judged on the phone in S2.
- **Small props on rough islands may read as clutter** on a 1.2-2 m oval; the phone review decides, and wider ovals (Q2 A) stay the fallback.

## Follow-ups (not in scope)

- Tiny floating islets in inside corners, and wind (post-M5).
- Forks round a dogleg corner (split fairway plan, Q7 B): would give par 4 more cut corners.
- Wider rough-island ovals for trees (Q2 A), if the small props read poorly.

## Decisions (director, 2026-11-12, word for word)

Relayed by the M4 hub (third, `local_c738ee0f`):

> Q1 B, Q2 B, Q3 B, Q4 A, Q5 plutôt B mais pas dans 100% des cas (si ça fait sens), Q6 A, Q7 B, Q8 A, découpage OK

The hub's reading of Q5, accepted: a full block of the direct or cut line is allowed on a share of strategic placements only (a retunable setting); the others keep A's rule. This plan proposes the share (25%) and the cases where it makes sense (§2).

| Q | Question | Options (A first) | Chosen |
|---|---|---|---|
| 1 | What "the safe route stays clear" means | A strict 2D safe corridors (bush room 0/28/59%) / B height-aware: under a safe leg's middle if the planned arc clears the top by 0.3 m, new flight check (bush room 0/33/68%, every split) | B |
| 2 | Rough islands (ovals 1.2-2 m, no prop fits today) | A widen ovals that get a prop / B small props only, 0.15 m margin on ovals / C bare | B |
| 3 | How often | A every course with room, 1-3 props / B 60% chance when there is room, 1-3 props (about 0-12 / 20 / 40%) / C 30%, 1-2 props | B |
| 4 | Dogleg inside corners | A trees on the cut line beside the corner hazard / B a tree replaces the hazard sometimes / C A plus corners with no cut line | A |
| 5 | How much a line may be blocked | A partial: a 1.5 m gap or under the full-power apex / B full block allowed / C only loftable props | "plutôt B mais pas dans 100% des cas" |
| 6 | Kinds | A trees, bushes, rocks; sheds and walls on spines and corners where they fit / B vegetation and rocks / C everything | A |
| 7 | Cypresses on spines | A keep the rule / B shadow may cross direct and cut corridors (about 55% of forks) / C the tree may also stand in the direct corridor | B |
| 8 | Par 3 | A through rough islands only / B none | A |
| — | PR split | S1 zones and check, S2 placement | "découpage OK" |
