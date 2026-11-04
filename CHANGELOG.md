# Changelog

All notable progress on Iso Golf. The format follows [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

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
