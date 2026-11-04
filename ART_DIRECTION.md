# Art Direction: Isometric Golf, "Pixel Diorama"

> Place at the project root (or reference it from `CLAUDE.md`) so that Claude Code / Opus follows the art direction on every task.
> Hex codes are estimates taken from the reference images: refine them with a color picker on the screenshots.

## 1. Intent

Each level is a **floating miniature island** set on a white background, like a scale model seen from above. Mood: **Mediterranean summer, calm, serene solitude, gentle nostalgia**. The player should feel almost alone on a peaceful island: a place inhabited by nature and by a few traces of human presence, but never by a crowd. Not a competitive arena.

Keywords: *isometric pixel art, diorama, scale model, peaceful island, solitude, summer afternoon, warm natural tones*.

## 2. Pixel-art look in 3D

The reference style is pixel art. We achieve it in real-time 3D:

- **Orthographic camera**, 2:1 isometric angle (X rotation ≈ 30°, Y = 45°), fixed view facing south-east. No perspective.
- **Low-resolution rendering** into a RenderTexture in portrait (start at 270×480), **Point** filtering, integer upscale (×3, ×4) to the screen.
- **Pixel-perfect snapping**: the camera position is rounded to the size of one world-space texel to prevent shimmering during movement. Compensate the residual offset during upscale to keep panning smooth.
- **No anti-aliasing**. Edges stay crisp and jagged.
- Pipeline: **URP**, a Renderer Feature for the pixelation pass, Shader Graph for materials.

## 3. Palette

**Warm, natural, slightly desaturated** palette. The only saturated colors are small accents (balls, flag, a few props).

| Role | Shades (light → dark) |
|---|---|
| Background | `#FFFFFF` → gradient to `#E4E4E4` at the bottom of the screen |
| Grass / vegetation | `#A7B86A` `#7E9A48` `#56713A` `#34482A` |
| Clay (warm ground) | `#D98A5C` `#BF6C43` `#9C5236` |
| Sand / gravel | `#E8D9B5` `#D6C196` `#B39E74` |
| Light stone | `#F1ECE0` `#DAD3C3` `#B8B0A0` |
| Slate roofs | `#A3A3A0` `#7F807E` `#5C5D5C` |
| Tile roofs | `#D4875E` `#B2663F` |
| Water | `#4FB0B8` `#2F8A96` `#1F5F6E` + highlights `#CFF1EE` |
| Wood (benches, borders) | `#8B5A35` `#6A4128` `#4A2C1C` |
| Metal / fencing | `#5E625F` `#3E4240` |
| Accents | red `#C8423B`, blue `#3D5E9C`, yellow `#E6B53A`, off-white `#F2EEE6` |

Rules:
- 3 to 4 shades per material, maximum.
- Shadows are **never black**: they are darker, cooler versions of the base color.
- Color quantization (LUT or palette post-process) can enforce consistency.

## 4. Lighting and shadows

- A single sun, **top-left**, warm (`#FFF1D6`), fairly high in the sky: early afternoon.
- Cast shadows are **crisp with soft edges** (dithered penumbra), slightly transparent, cool-tinted (violet / blue-grey).
- Trees cast large shadows on the ground: an important compositional element.
- **3-step toon shading** (light, midtone, shadow) through a ramp, no continuous gradients.
- Ambience: warm, generous ambient light, no truly dark areas.

## 5. Texture and material

The style's signature is **pixel-level high-frequency noise** on every large surface:

- Clay / sand: scattered small light dots (pebbles, marks).
- Water: foam and highlights as short horizontal white dashes, darker areas under bridges and rocks.
- Stone: readable rectangular blocks, joints marked one shade darker, slight variation from block to block.
- Grass: organic grain, alternating light / dark in small clumps.
- **Ordered dithering (4×4 Bayer)** for shadow transitions and gradients.

No photo textures, no fine normal maps: material comes from noise and shading.

## 6. Diorama shape

- Each scene is an **island resting on white void**, with no visible base.
- The outline is an **irregular, dithered grass fringe** spilling past the geometric shape: never a clean edge.
- Vegetation: trees with **cauliflower foliage** (clusters of spheres), shaded in clumps, short trunk. Round bushes.
- Simple, readable volumes: boxes, cylinders, arches, gabled or conical roofs.
- Outlines: no black lines. A **one-pixel outline one shade darker than the object**, on silhouettes only (depth / normals edge detection at low resolution).

## 7. Scale, solitude and life

- **No characters on screen**, or exceptionally one distant, still silhouette (a fisherman on a rock, a gardener far away): never more than one per level.
- **No club, no avatar**: the ball *is* the player.
- The scale-model feel comes from objects: a bench, a shed, a flag, a low wall are enough to convey the size of the world.
- Life comes from ambience: rustling foliage, a fluttering flag, animated water highlights, birds taking off as the ball passes, butterflies, a cloud shadow drifting across the island.
- Traces of human presence without humans: an empty bench, a rake left in a bunker, a forgotten bucket of balls, a bicycle against a wall, laundry drying on a line.
- Density: a few clusters of detail, lots of calm surfaces and empty space around them. Visual silence is part of the mood.

## 8. Interface

- **Minimal and diegetic**. No cluttered HUD.
- Power bar: a small yellow gauge (`#E6B53A`) with a dark border, floating above the ball, pixel style.
- Aiming: a dotted arc of white pixels following the bell-shaped shot curve, with the landing point marked by a small cross or circle on the ground.
- Shot gesture: to be defined (drag-and-release, hold-and-release…). Whatever gesture is chosen, its visual feedback is limited to the gauge and the arc.
- Typography: a small, readable pixel font in a warm dark color (`#3A2E25`) on the white background.
- Transitions: fade to white between levels, or the next island "dropping" into place.

## 9. Golf adaptation

### Player and shot

- **The ball is the player.** Pure white, 2×2 px (3×3 if readability requires it), with a round one-pixel shadow on the ground, essential for reading height in isometric view.
- **Bell-shaped arc** shots: rise, apex, descent. The shadow stays on the ground and meets the ball on landing: it is what makes the arc readable.
- Light trail in flight (a few fading pixels), a small puff of grass or sand on impact, short bounces.

### Progression and orientation

- Progression through **procedurally generated levels**, each one an island diorama.
- Constant play axis: start in the **south-west**, finish in the **north-east**. On screen: from **bottom-left to top-right**, along a diagonal of the isometric grid.
- The start is always in the lower-left part of the island, the green in the upper-right part: the eye reads the level in the direction of the shot.
- The screen is forced portrait, so the diagonal play axis does not fit in one view: each level opens with a slow pan along the course from the hole back to the tee, then the camera follows the ball. No animated zoom, because changing the pixel density would make the image shimmer.
- Between levels, the next island appears to the north-east (camera slide or fade to white).

### Procedural kit

To keep generation faithful to the art direction, levels are assembled from a **modular kit** that follows the palette and the rules above:

- Island outline: shape generated with noise, dithered grass fringe applied automatically along the whole edge.
- Surfaces: fairway, green, rough, bunker, water, gravel, each with its own material and noise.
- Props: trees, bushes, rocks, low walls, sheds, benches, bridges. Placed by rules: low density, grouped in clusters rather than scattered, never on the main shot line.
- Per-seed variation: island shape and length, obstacles, mood (see variants).

### Course elements

- **Fairway**: mowing stripes alternating two greens (`#8FA85A` / `#7E9A48`), aligned with the SW → NE axis.
- **Green**: lighter and smoother green, very little noise, red or yellow flag.
- **Rough**: darker, grainier grass, blending into the edge fringe.
- **Bunkers**: sand palette, rolled edge, a few rake marks.
- **Water hazards**: water palette, round white rocks, small stone bridge.
- **Scenery**: olive trees, cypresses, dry-stone walls, tile-roofed shed, a small chapel or lighthouse standing alone at the island's edge. No spectators.

## 10. Avoid

- Perspective, strong depth of field, bloom, chromatic aberration.
- Neon colors, excessive saturation, pure black.
- Clean, geometric diorama edges.
- High-resolution or realistic textures.
- Intrusive HUD, large buttons.
- Many characters, crowds, spectators.
- Clubs, golfer avatar, hands or arms on screen.

---

## Variants to explore

Same visual grammar, different moods. Useful for varying courses or testing a more personal identity.

**A. Golden hour (late afternoon)**: low sun, very long shadows, palette pushed toward orange and pink, violet shadows, small lanterns lighting up on a shed.

**B. Scottish links**: cool, desaturated palette (grey-greens, pale sand, misty white sky), dunes, visible wind (animated grass, taut flag), dark stone walls, light rain rendered with dithering.

**C. Night golf**: midnight-blue background instead of white, glowing balls and holes, street lamps casting circles of warm light, fireflies.

**D. Seasons**: the same course in several versions (blooming spring, Mediterranean summer, russet autumn, snowy winter) by swapping only the palette through a LUT: very cheap to produce.

**E. Village mini-golf**: mini-golf obstacles (windmills, loops, bridges) built into deserted Mediterranean village squares at siesta time. Consistent with the "stone bridge" reference.

**F. Smooth diorama (no pixels)**: same composition, same palette, but clean full-resolution toon 3D rendering with a slight tilt-shift. Fallback option if the pixel pipeline proves too costly.

**G. Pixel low-poly**: a hybrid between the pixel feel of the references and low-poly. Palette, lighting, white background and colored outlines stay unchanged: only the material evolves.
- Assumed low-poly geometry: visible facets, **flat normals** (per-face flat shading), terrain with gentle triangulated undulations.
- Pixelation kept but less aggressive (e.g. 540×960 render instead of 270×480), so each facet stays readable.
- Shading: each facet takes one step of the toon ramp; dithering is limited to the edges of cast shadows.
- Lighter surface noise: facets replace part of the grain. A few dots remain on sand and gravel.
- Trees: clusters of low-detail icospheres, cypresses as 6–8-sided cones.
- Water: faceted plane with animated vertices, highlights as light facets rather than dashes.
- Island edge: the grass fringe becomes a border of small prismatic tufts, still irregular.
