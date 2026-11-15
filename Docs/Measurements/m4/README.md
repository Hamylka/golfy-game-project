# M4 phone checks (PR 4.9)

Measured on 2026-11-15 on the director's Honor 500 Pro (HONOR DNP-NX9, Adreno 750, Vulkan, 1280x2800, 60 fps cap), by the same synthetic-touch autoplay as M3 PR 9 (`PhoneMeasurement`: a flag file `measure.txt` holding `<par> <seed> [putt]`, 150 s per course, the first 5 s dropped, 60 fps cap = 16.52 ms per frame). Every figure below comes from a CSV written on the phone (`phone_checks.csv`, copies kept here), from the `MEASURE_SUMMARY` / `COURSE_TIMING` lines in logcat, or from the Editor where it says so. One run per course (single run each), so small differences are noise.

## The builds

| Build | Flags and defines | Used for |
|---|---|---|
| **Dev** (`dev=True` in the CSV header) | IL2CPP, Development Build, `ISOGOLF_MEASURE` | CPU main, GPU, draw calls, triangles, `AimPreview.Predict`, bird and butterfly markers |
| **Non-development** (`dev=False`) | IL2CPP, no development flag, `ISOGOLF_MEASURE` (which only adds the logger, the autoplay and the IMGUI seed strip) | frame time as a player gets it ("release flags") |

A separate release build with `ISOGOLF_RELEASE_LOG` instead was **skipped** (hub decision, to save a 16 min build and budget): the non-development build has the release flags and compiled code, and the extra code of `ISOGOLF_MEASURE` is the autoplay, the logger and a small IMGUI strip. That could only cost CPU, and every frame sat on the 60 fps cap, so it cannot hide a miss; it could only understate the headroom. The profiler markers, `Predict` and draw-call counters do not exist outside a development build (read 0 in the non-development CSVs), hence the two builds.

The first attempt at a dev build was not one (the build tool's `development` parameter was missing), so one batch ran non-development (kept, above) and a one-course check; and two earlier batches were void (the flag file never reached the phone). Only the two builds above are used.

## 1 and 2. Frame time, props and all ambient life

Courses: par 5 seed 33 (M3 baseline), par 5 seed 5 (the most props over seeds 0-49: **24**, tied with seed 20; seed 33 has 15; Editor sweep of `CheckedCourse.Generate`), par 4 seed 46 (waterfall), par 4 seed 7 (15 props, no waterfall).

**Dev build**, per state (frames, frame ms p50 / p99 / max; CPU main ms p50 / p99; GPU ms p50 / p99 / max):

| Course | State | Frames | Frame p50 / p99 / max | CPU main p50 / p99 | GPU p50 / p99 / max |
|---|---|---|---|---|---|
| par 5 s33 | Resting | 1,800 | 16.52 / 16.52 / 16.52 | 4.89 / 7.24 | 8.67 / 10.48 / 18.39 |
| | Aiming (arc) | 2,490 | 16.52 / 16.52 / 16.52 | 5.11 / 7.93 | 8.53 / 10.23 / 17.07 |
| | Aiming (putt) | 1,281 | 16.52 / 16.52 / 16.52 | 6.29 / 10.02 | 8.57 / 10.06 / 14.34 |
| | Flight | 3,206 | 16.52 / 16.52 / 16.52 | 4.69 / 7.36 | 8.60 / 10.22 / 16.85 |
| par 5 s5 (24 props) | Resting | 1,800 | 16.52 / 16.52 / 16.52 | 4.42 / 5.87 | 9.62 / 10.45 / 11.40 |
| | Aiming (arc) | 3,560 | 16.52 / 16.52 / 16.52 | 4.93 / 7.26 | 10.01 / 10.72 / 11.96 |
| | Flight | 3,234 | 16.52 / 16.52 / 16.52 | 4.29 / 5.80 | 9.09 / 10.36 / 12.26 |
| par 4 s46 (waterfall) | Resting | 1,800 | 16.52 / 16.52 / 16.52 | 4.87 / 6.65 | 8.94 / 11.30 / 17.03 |
| | Aiming (arc) | 3,767 | 16.52 / 16.52 / 16.53 | 5.19 / 7.71 | 8.55 / 10.32 / 17.21 |
| | Flight | 3,204 | 16.52 / 16.52 / **99.13** | 4.61 / 6.54 | 8.31 / 10.28 / 17.24 |
| par 4 s7 | Resting | 1,800 | 16.52 / 16.52 / 16.52 | 4.82 / 6.56 | 8.70 / 10.97 / 17.32 |
| | Aiming (arc) | 2,908 | 16.52 / 16.52 / 16.52 | 5.15 / 7.27 | 8.47 / 10.57 / 16.82 |
| | Flight | 3,335 | 16.52 / 16.52 / 16.53 | 4.61 / 6.24 | 8.43 / 10.41 / 17.00 |

**Non-development build** (frame time only; this is the release-flags figure). Frames after the first 5 s, p50 / p99 / max in ms:

| Course | Frames | p50 / p99 | Max |
|---|---|---|---|
| par 5 s33 | 8,775 | 16.52 / 16.52 | 16.52 |
| par 5 s5 | 8,776 | 16.52 / 16.52 | 16.53 |
| par 4 s46 | 8,772 | 16.52 / 16.52 | **82.62** (one frame, in flight) |
| par 4 s7 | 8,774 | 16.52 / 16.53 | 16.53 |

No GPU or CPU figure exists for this build (`FrameTimingManager` read 0 there). Raw data: `par5-seed33-nondev.csv`, `par4-seed46-nondev.csv`, `par5-seed33-dev.csv`, `par5-seed5-dev.csv`, `par4-seed46-dev.csv`, `par4-seed7-putt-dev.csv`.

Against M3 (par 5 s33, dev build then: 60 fps, frame max 16.51, GPU p50 9.5 / p99 10.9, CPU main p50 4.1-4.7): now frame max 16.52, GPU p50 8.5-8.7 (resting/aim/flight), p99 10.2-10.5, CPU main p50 4.7-5.1 with props and all ambient life. Same order, slightly more CPU main (+0.4 to +0.6 ms), no GPU rise.

**Waterfall hitch.** On par 4 seed 46 a single frame of 82.6 ms (non-dev) / 99.1 ms and 82.6 ms (two dev runs) appears **at t = 8.28 s in the dev runs, the moment the first shot has just left the ball** (ball state flying, shot 1), in all three runs of this course and in none of the three other courses. At that frame the logged CPU main (4.5-5.6 ms), render thread (1.2-1.4 ms) and GPU (8.2-9.4 ms) are normal, so the figures I have do not show where the time went (the frame timing lags the frame by some frames). **Cause not established.** The best-supported hypothesis, not proven: the autoplay's camera follow brings the transparent waterfall sheet (own material, never drawn before) into view on the first shot and the first draw creates its pipeline state on Vulkan. A test to settle it: a shader/PSO warm-up (a `ShaderVariantCollection` for the waterfall graph, or one off-screen draw at load) and rerun; or a Profiler capture with deep profiling around t = 8 s. Other possible causes from the hub's list (GC, a bird take-off mesh) fit the numbers less: GC would show in CPU main, and the bird cost is under 1 ms. It is one frame in about 8,500 and it did not repeat during the aim.

Targets from `Docs/Plans/m4-plan.md` (end-of-M4, targets and not measurements): 60 fps held on par 5 seed 33: **met**. GPU p99 under 13 ms: **met** (worst 11.30). Terrain + props build under 300 ms: **met on par 5 s33 (252.7 ms), missed on par 5 s5 (321.7 ms)**, see below. Draw calls under baseline + 3: **baseline not measured in M3, not checkable**; the absolute figures are below.

## 3. Putt prediction cost (open since M2)

The putt autoplay places the ball on the green 2-5 m from the hole and aims and putts (flag `4 7 putt`; code behind the measurement defines only). `AimPreview.Predict` sample, in microseconds, over the frames where the marker fired (zero samples dropped), dev build:

| Course | Aim | Samples | p50 | p99 | Max |
|---|---|---|---|---|---|
| par 4 s7 (putt-only run) | putt | 2,835 | 1,058 | 3,900 | 9,798 |
| par 4 s7 | putt | 529 | 889 | 3,565 | 4,117 |
| par 5 s33 | putt | 945 | 1,257 | 3,826 | 4,908 |
| par 5 s5 | putt | 135 | 992 | 1,675 | 4,369 |
| par 5 s33 | arc | 1,747 | 418 | 1,016 | 3,723 |
| par 5 s5 | arc | 2,670 | 375 | 809 | 9,990 |
| par 4 s7 | arc | 2,188 | 322 | 904 | 4,276 |

So a putt preview costs about 1 ms at the median and about 3.5-4 ms at p99 on the phone (arc previews about 0.3-0.4 ms, 1 ms at p99). In the putt-only run, CPU main while aiming a putt is p50 6.09 / p99 9.69 / max 15.88 ms, against 4.6-5.1 at rest: still under the 16.5 ms frame, so no frame was lost, but a putt aim is the most expensive state in the game and has the least headroom (max 15.88 of 16.52 ms). Dev build, so an upper bound. The markers fire while the finger is moving (the autoplay moves it every frame); a real finger is slower.

## 4. Course build time on the phone (`COURSE_TIMING`)

Phone, dev build (non-dev builds in brackets where taken; they agree within noise), ms: generation + checks (includes placing the props and the checks with props on), terrain build (includes the props mesh), and the props mesh alone.

| Course | Props | Generate + checks | Terrain build | Props mesh |
|---|---|---|---|---|
| par 5 s33 | 15 | 152.2 [143.3] | 252.7 [242.6] | 2.1 |
| par 5 s5 | 24 | 178.6 [186.3] | **321.7** [311.2] | 2.5 |
| par 4 s46 | 13 | 146.8 / 152.1 [150.1] | 172.3 / 182.9 [183.6] | 0.6 / 0.8 |
| par 4 s7 | 15 | 73.5 / 74.0 [69.9] | 128.4 / 130.4 [136.4] | 2.1 / 2.0 |

The props mesh itself is about 2 ms (under 1% of a course build): the cost of props is in placement and the checks, which the generation figure carries. The Editor's checked generation (67 / 176 / 326 ms for par 3 / 4 / 5, quoted from the plan) was not remeasured here; on the phone par 4 takes 73-152 ms and par 5 143-186 ms, which is the same range or lower (one run each, so not a like-for-like benchmark). Compared with M3's phone figures (generation + checks 14-37 ms, terrain 47-220 ms), generation is **4-6 times slower** (props and the strategic checks run with props) and terrain build up to 1.5 times slower (the waterfall and props meshes are in it): a course now needs about 0.2 to 0.5 s to appear, once per course. The first frame after load takes about 2.1 s (seen in every run; M3 saw the same).

## 5. Bird and butterfly meshes rebuilt every frame

Measured with `ProfilerMarker`s now left in `BirdsView.Refresh` and `ButterflyView.Pose` (they cost nothing outside a development build). Dev build, microseconds per frame after the first 5 s, all courses:

| | p50 | p99 | Max |
|---|---|---|---|
| Birds (`Refresh`) | 33-51 | 59-175 | 408-1,063 |
| Butterflies (`Pose`) | 18-21 | 26-32 | 394-735 |

Together about 0.07 ms per frame at the median, 0.2 ms at p99, against a 16.5 ms frame and a CPU main of 4.4-5.1 ms: **it does not matter, no fix proposed.** The maxima (up to about 1 ms, single frames) are the only visible cost, and only in a development build.

## 6. Draw calls, set-pass calls and triangles (dev build)

From the profiler recorders on the phone ("Draw Calls Count", "SetPass Calls Count", "Triangles Count"; "Batches Count" reads 0 on this device, so it is not reported). p50 per course, frames after 5 s (the load frame excluded):

| Course type | Course | Draw calls p50 / max | SetPass p50 / max | Triangles p50 |
|---|---|---|---|---|
| Props | par 4 s7 | 33 / 34 | 23 / 23 | 91,207 |
| Props (24) | par 5 s5 | 32 / 33 | 23 / 23 | 103,288 |
| Props | par 5 s33 | 32 / 34 | 23 / 23 | 100,445 |
| Props + waterfall | par 4 s46 | 33 / 37 | 23 / 25 | 93,794 |

A dry course without props was **not measured**: `Main` always places props, so there is no such course to play without changing the generator; and M3 did not record draw calls, so there is no baseline either. What the figures do show: the waterfall course costs at most 4 more draw calls than the others at p99-max (37 against 34) and 2 more set-pass calls in its worst frames, p50 the same (33 against 32-33). The waterfall adds no cost at the median.

## 7. For the director to look at, by eye (not judged here)

1. The first shot on the waterfall course (seed 46): is there a visible stutter as the camera first follows the ball? That is the 82-99 ms frame.
2. Putt aiming on the green: does the dotted putt preview ever lag behind the finger (it is the dearest state, 6 ms CPU main median)?
3. The look with 24 props (par 5 seed 5): density, clusters, the corridor still clear.
4. The waterfall sheet: transparency against the clouds and the sky, the foam brink, no band of island shadow across it.
5. Birds taking off and butterflies: do they read as small and calm, and are they ever distracting?
6. Chip-ins and pole collisions: do they feel predictable on the preview?
7. The rechecked art (outline, slope readability, bunker outlines) on this build, since the look recheck PR was judged on captures.
8. The cloud shadow crossing the course: subtle enough?
9. Course appearance time (up to 0.5 s on a par 5): does the transition to the next hole hide it?

## Waterfall warm-up (`fix/m4-waterfall-warmup`)

`WaterfallWarmup` draws the waterfall material once, invisibly, at every course load (a one-triangle mesh with vertex alpha 0 just past the main camera's near plane, kept 2 frames). Dev build (`dev=True`), IL2CPP, `ISOGOLF_MEASURE`, same phone and recipe (flag `4 46`, 170 s per run, first 5 s dropped). The old hitch was one frame of 82.6-99.1 ms at t = 8.28 s in all 3 earlier runs of this course. Raw data: `par4-seed46-warmup-dev-run1.csv`, `-run2.csv`, `-run3.csv`.

| Run | Frames | Frame max (ms) | Frames over 17 ms | Frame at t ≈ 8.3 s |
|---|---|---|---|---|
| 1 | 8,774 | 46.06 | 3 (t = 76.28 s 34.07 ms, then 23.83 and 17.54; t = 83.32 s 46.06 ms, then 25.00 and 17.50; both while aiming, shots 10 and 11) | 16.52 |
| 2 | 8,777 | 16.52 | 0 | 16.52 |
| 3 | 8,776 | 16.53 | 0 | 16.52 |

Result: **the first-shot hitch did not recur in 3 runs (it appeared in 3 of 3 before)**, which supports the hypothesis (first draw of the transparent material on Vulkan) without proving it: the warm-up replaces that first draw, and the frame is gone. Run 1 shows two isolated frames of 34 and 46 ms in the aim phase (shots 10-11, long after the first shot); they are not the old hitch, did not appear in runs 2 and 3, and I did not investigate them (a single run each, no profiler capture). The `MEASURE_WATERFALL_VISIBLE` log (build with `ISOGOLF_MEASURE` only) reads frame 316, t = 8.194 s (run 2) and frame 315, t = 8.191 s (run 3): the renderer is first seen by a camera about 0.09 s before the old hitch time (8.28 s), consistent with the camera following the first shot onto the waterfall. Run 1's logcat kept no such line. The visibility log was not in the earlier build, so the two events were not measured in the same run. No non-development run was made after the fix.

## Limits

- Dev build for CPU, GPU and Predict (upper bounds). One run of 150 s per course, a single seed each; no other par 5 beyond s33 and s5, no par 3 on the phone.
- The autoplay putts only in the dedicated putt run, and shots are random in power and direction, so not every prop type is struck.
- The IMGUI strip is drawn in the non-development build too.
- The cause of the waterfall hitch is a hypothesis; the warm-up section above supports it, and does not prove it.
- Memory on the phone was not measured.
- The seed 46 non-dev hitch (82.62 ms) and the dev hitches (99.13, 82.61 ms) are quantised to the phone's refresh (a few missed vsyncs).

## Defines after the builds

`ISOGOLF_MEASURE` is removed from the Android scripting defines in `ProjectSettings/ProjectSettings.asset` (empty list), and `EditorUserBuildSettings.development` is back to off. `ISOGOLF_RELEASE_LOG` was never set for a build.
