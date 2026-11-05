# Changelog

All notable progress on Iso Golf. The format follows [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

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
