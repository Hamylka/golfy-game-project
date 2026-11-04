# Changelog

All notable progress on Iso Golf. The format follows [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

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
