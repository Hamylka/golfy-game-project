# M2 phone checks

Measured on 2026-11-06 on an Honor 500 Pro (Adreno 750, Vulkan, 1280x2800, `FrameRateCap` 60). The raw per-frame data is `dev-build.csv`.

## How it was captured

- **Development build**, Android, with the scripting define `ISOGOLF_MEASURE`. Development builds run slower than release builds, so these figures are an upper bound. A release build was not measured: the dev build already holds 60 fps with no slow frame, so there was nothing to separate.
- `PhoneMeasurement` (in `Assets/_Project/Scripts/Measurement/`, compiled only with that define) plays the game by itself through synthetic touches fed to the real Input System, so the slingshot input, aim preview and camera follow all run as in play. It starts after 4 s, grabs the ball, moves the finger every frame for 3 s (so the aim changes every frame and `Predict` runs every frame), releases, and repeats once the ball rests. 150 s on `Main`, then 150 s on `Sandbox_LongCourse`, at the default zoom.
- `AimPreview.Predict` is wrapped in a `ProfilerMarker` (`AimPreview.Predict`, no cost in release builds), read with `ProfilerRecorder`. Frame times come from `Time.unscaledDeltaTime`, CPU main thread, render thread and GPU from `FrameTimingManager`.
- Each row of the table counts frames after the first 5 s. Times are in milliseconds unless marked.

## Cost of the aim prediction

| Case | Frames with a prediction | p50 | p95 | p99 | Max |
|---|---|---|---|---|---|
| Arc, full range of power, `Main` | 2,792 | 0.075 ms | 0.093 ms | 0.187 ms | 1.01 ms |
| Arc, `Sandbox_LongCourse` | 2,522 | 0.073 ms | 0.098 ms | 0.185 ms | 0.83 ms |
| Putt, `Sandbox_LongCourse` | 135 | 0.235 ms | 0.438 ms | 0.494 ms | 0.49 ms |

An arc prediction is cheap. The occasional 0.8 to 1 ms maximum is a single frame in a run of about 2,800.

**Putt coverage is thin.** On `Main` the ball never came to rest on the green in 21 shots, so there was no putt there. On the long course the run put the ball on the green once: one 3 s aim, 135 predictions, with the pull sweeping between 15% and 90% of the range. The editor measured 0.37 ms for the longest putt (231 steps); the phone's worst putt here was 0.49 ms in a development build. That is not enough to say "the longest putt costs X". A longer putt sweep needs the harness to steer the ball onto the green (or a start position on it), which is a small change.

## Frame time during a shot

Default zoom, camera follow running, a development build.

| Scene, state | Frames | Frame (dt) p50 / max | CPU main p50 / p99 | GPU p50 / p99 | Render thread p50 |
|---|---|---|---|---|---|
| `Main`, flight | 3,188 | 16.53 / 16.53 | 4.02 / 5.55 | 8.20 / 10.39 | 0.85 |
| `Main`, aiming | 3,783 | 16.53 / 16.54 | 4.38 / 5.98 | 8.99 / 10.25 | 0.91 |
| `Main`, resting | 1,800 | 16.53 / 16.54 | 4.29 / 6.11 | 8.71 / 9.99 | 0.87 |
| Long course, flight | 3,370 | 16.53 / 16.53 | 3.62 / 4.68 | 8.21 / 10.31 | 0.93 |
| Long course, aiming | 3,416 | 16.53 / 16.53 | 3.96 / 5.19 | 8.33 / 10.39 | 1.00 |
| Long course, resting | 1,803 | 16.53 / 16.53 | 3.81 / 5.17 | 8.24 / 10.94 | 0.95 |

- The frame stayed at 60 fps (16.53 ms) in every state: no frame was longer than 16.54 ms in 19,343 frames after the first 5 s of each scene.
- GPU time is about 8 to 9 ms, a little above the 5.8 ms of the M1.5 release build. That is the development build, and the preview and ball add some. A single GPU outlier of 15 to 16 ms appeared on the long course (a handful of frames). It didn't cost a frame.
- Camera follow adds nothing visible to the frame time: flight is not slower than rest.

## To judge by eye on the phone

Answers from the player on 2026-11-06, played on the phone with a normal release build.

| Check | Answer |
|---|---|
| Slopes read well; single facets not predicting the roll is acceptable | Pass for the current soft slopes: they fit and feel natural. Recheck when M3 makes them steeper. |
| Ball sitting up to 17.5 cm off a facet in the rough | Pass |
| Drawn surface borders vs true outlines (up to 27 cm at the pond, 11 cm at the green) | Pass |
| Camera follow: vertical band 40 to 60%, side band +-0.2 | Pass |
| Default zoom and the reset view button | Pass |
| Sky shown when the camera follows near the island's edge | Pass |
| Tuned shot values from PR 27 (full power overshoots the hole, livelier bounces) | Pass |

## Result

All seven by-eye checks passed, and the frame time held 60 fps in every state. One gap stays open: the longest putt's prediction cost was not measured on the phone (see above); its Editor figure is 0.37 ms and the phone's one sampled putt peaked at 0.49 ms. Recheck slopes by eye once M3 generates steeper ones.
