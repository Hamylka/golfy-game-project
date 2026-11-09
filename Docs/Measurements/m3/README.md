# M3 phone checks (PR 9)

Played and measured on 2026-11-09 on the director's Honor 500 Pro (1280x2800, 60 fps cap) with a **development build** of `Main` (so figures are an upper bound), built with the scripting define `ISOGOLF_MEASURE`: a seed/par strip at the top of the screen (< seed >, P3/P4/P5), a frame-time readout, and timing logs. None of it exists in a release build. APK: `Builds/Android/IsoGolf_PhoneChecks.apk` (not committed).

## By eye (the director's words)

| Check | Result | Words |
|---|---|---|
| Steeper slopes, green tilt, fade ring (PR 40) | **Partial** | "Partiellement bon." Seed 4 par 4: the end of the fairway near the green is badly finished, a very harsh, raw cut between green and fairway. Seed 6 par 4: the central fairway bunker is badly outlined. "Globalement très dur de voir les pentes." |
| Gaps, water, bunkers, livelier green (PR 43) | **Pass** | "Tout passe": lost ball returns, bunkers slow the ball, green bounce fine. |
| Doglegs with the camera (PR 37): heading at rest, twist, reset, approach zoom | **Partial** | Seed 6 par 5, full-power shot: the camera does not turn the right way, not fully toward the hole. "Le reste que j'ai pu tester est bon." |
| Sun per level, cloud tones on bent courses (PR 38) | **Pass** | "Tout passe." |
| Shadow fade on par 4/5 (66-82 m worst heading) | **Pass** | "Acceptable." |

## Measured on the phone

Course generation plus checks and terrain build, from `COURSE_TIMING` lines in logcat (single run each, dev build):

| Seed, par | Generate + checks | Terrain build | Variant / candidates |
|---|---|---|---|
| 33, par 5 | 30.3 ms | 219.5 ms | 0 / 1 |
| 4, par 4 | 36.6 ms | 127.9 ms | 2 / 3 |
| 6, par 4 | 22.7 ms | 90.8 ms | 1 / 2 |
| 7, par 4 | 13.9 ms | 91.6 ms | 0 / 1 |
| 8, par 4 | 28.2 ms | 151.7 ms | 0 / 1 |
| 1, par 3 | 21.2 ms | 46.6 ms | 0 / 1 |

Editor figures from PR 43 were 27/51/69 ms for par 3/4/5, so the phone is in the same range. The terrain build is the larger cost (up to 220 ms on the par 5), once per course.

Frame time on the longest par 5 (seed 33), 150 s of autoplay through synthetic touches (`PhoneMeasurement`, 21 shots, default zoom, camera follow running), 8,782 frames after the first 5 s. Raw data: `par5-seed33.csv`.

| State | Frames | Frame p50 / p99 / max | CPU main p50 / p99 | GPU p50 / p99 / max |
|---|---|---|---|---|
| Resting | 1,800 | 16.51 / 16.51 / 16.51 | 4.29 / 5.85 | 9.48 / 10.88 / 11.55 |
| Aiming (arc) | 3,782 | 16.51 / 16.51 / 16.51 | 4.70 / 7.10 | 9.67 / 10.77 / 12.10 |
| Flight | 3,200 | 16.51 / 16.51 / 16.51 | 4.09 / 5.57 | 9.21 / 10.59 / 12.92 |

No frame was longer than 16.51 ms: 60 fps held with no slow frame. The first frame after a scene load takes about 2.1 s (shader and terrain build, seen in the `FRAMES` log). A release build and other seeds' par 5s were not measured; the autoplay never reached a putt on this course, so the putt prediction cost is still open from M2.

## Limits of this check

- Slopes: no figure, only the director's impression. Screenshots: `Docs/Screenshots/m3-phone-checks/` (seed 4 par 4, seed 6 par 4, seed 33 par 5 start views, from the device).
- The camera issue on seed 6 par 5 was not reproduced or diagnosed here. By design (CLAUDE.md, Camera heading) the camera faces the hole only when it is within a full-power shot, otherwise the heading of the leg the ball is on, so this may be that rule rather than a bug; the director's report says it feels wrong.

## Follow-ups (not fixed here)

1. **Camera heading on par 5 seed 6 after a full-power shot** (Opus, camera framing): reproduce, decide whether the leg-heading rule should lean toward the hole sooner.
2. **Fairway-to-green junction, seed 4 par 4** (generator surfaces, M4 art pass or a Sonnet fix): raw cut between fairway and green.
3. **Bunker outline, seed 6 par 4**: poorly outlined fairway bunker (hazard outline smoothing).
4. **Slope readability**: slopes are hard to read at the new steepness. Options to weigh with the director: stronger facet shading, subtle contour tint, or a visible tilt hint on the green.
5. Putt prediction cost on the phone (open since M2) and a release-build frame measurement.
