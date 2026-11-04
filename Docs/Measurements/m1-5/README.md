# M1.5 performance measurements

Measured on 2026-11-04 on an Honor 500 Pro (Snapdragon 8 Gen 3, Adreno 750, Android 16) at 1280×2800 with Vulkan. The display ran at 60 Hz in these tests. The CSV files here are the raw results, written by a throwaway test app. Each row is a 12 s or 28 s sample after a short settling period. Times are in milliseconds, with `p50` the median.

Compare GPU times only between release builds. A development build costs more, and one extra pass shows up in its counters.

## Final build

`final-release.csv`: `Main.unity`, release build, 60 fps from `FrameRateCap`, depth texture off, 5 minutes.

| | Result |
|---|---|
| Frame rate | 60.6 fps in every 30 s sample, with no slow frames |
| GPU, median | 5.82 to 5.98 ms, no upward drift |
| GPU, 95th percentile | 5.96 to 6.11 ms |
| CPU main thread, median | 3.02 to 3.21 ms |
| CPU render thread, median | 0.58 to 0.59 ms |
| Draw calls, SetPass calls | 9, 4 |
| Triangles | 26,870 |

## The 30 fps seen in the sandbox was a cap

`round1-release.csv`: with nothing set, the game ran at 30.3 fps with 30 ms of each frame spent waiting. Asking for 60 gave 60.6 fps at the same GPU cost, about 9.7 ms. Asking for 90 or 120 still gave 60.6 fps, so the game doesn't go above the 60 Hz the display reported in these tests.

## Cost of each feature

Medians over repeated on and off samples. The `Before` column is the build with the depth texture on, the `After` column the build with it off. A dash means that setup wasn't measured.

| Feature | Before | After | Decision |
|---|---|---|---|
| Depth texture, unused | 3.8 ms | - | **Off**, no filled area changes |
| Second shadow sample in `ShadowTint` | 0.70 ms | 0.68 ms | Keep: it's the chosen look and the budget allows it |
| All shadows | 0.82 ms | - | Keep |
| 15,440 triangle clouds, whole mesh | 0.16 and 0.60 ms, noisy | 0.43 ms | Keep at full detail: halving them could save roughly 0.2 ms |
| 1,960 triangle tufts | 0.33 ms | - | Keep |

## Render scale and MSAA

`round2-depth-texture-release.csv`, depth texture off, median GPU time.

| Setting | GPU | Saving | Look |
|---|---|---|---|
| Scale 1.0, 4× MSAA | 5.84 to 5.97 ms | - | Chosen look |
| Scale 0.85 | 4.49 to 4.53 ms | 24% | Not judged on the phone |
| Scale 0.70 | 3.54 to 3.56 ms | 40% | Not judged on the phone |
| 2× MSAA | 4.47 to 4.48 ms | 24% | Not judged on the phone |

## Removing the depth texture doesn't change the image

With time frozen, the same frame was captured on the phone with the depth texture off and on. Across the 3,584,000 pixels, 97.4% match exactly. No differing pixel sits in a filled area: every one lies in a thin line along a geometry edge. The largest difference is 128 of 255, on a few high-contrast edges. See `depth-difference-x8-phone.png` in `Docs/Screenshots/m1-5/`. Both images look equally antialiased, but the human judges this on the phone.

## Batching

From the Frame Debugger in Play Mode with the Android target, before the depth texture was removed.

| Pass | Draws | Triangles | Meshes |
|---|---|---|---|
| Shadow map | 3 in one SRP batch | 3,895 | Land, Trees, Water |
| Opaque | 4 in one SRP batch | 5,855 | Land, Water, Trees, Tufts |
| Opaque | 1 | 15,440 | Clouds, its own shader |

`Tufts` shares the land batch and casts no shadow, as intended. Water casts a shadow nothing needs, worth one draw call and 96 triangles.

## Profiler capture

A 20 s capture from a development build, with the profiler adding its own overhead. Every fifth frame was read. The average frame was 16.40 ms, of which 11.36 ms was `WaitForTargetFPS`, 1.50 ms the camera's render, and 0.04 ms script updates. No sampled frame allocated managed memory. The 47 MB `.raw` file isn't committed.

## Shadow distance

URP starts fading shadows at 80% of the 70 m shadow distance, which is 56 m. The farthest point of the par 3 island from the framed camera is 45.1 m on a 3:4 tablet, 47.0 m at 9:16, 52.5 m on this phone, 54.5 m at 9:21 and 56.0 m at 9:22. So 70 m stays, with nothing to spare on a screen as tall as 9:22. `ShadowDistanceTests` guards this up to 9:21, and the longer courses of M3 will need a new check.

## Automatic render scale: decision, not built

This phone can't settle the thresholds: at 5.9 ms of GPU in a 16.7 ms frame, it never needs a drop. The recommended design is:

- Fixed steps of 1.0, 0.85 and 0.7, using the savings above.
- Step down when GPU time stays above a threshold for a few seconds, and never step back up during a level, so the image doesn't visibly change back and forth.
- Try the full scale again at the start of each level.
- Consider 2× MSAA as another step, since it saves as much as scale 0.85. Which of the two looks better needs a phone comparison.

Choosing a tier by device model at startup is simpler, but needs a device list that goes stale.

**Measurement needed before building it:** a real low-end Android phone, measuring GPU time at each step and judging the look. Reading GPU time in release builds also needs `enableFrameTimingStats` on in Player Settings, which is off today.
