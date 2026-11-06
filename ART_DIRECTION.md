# Art Direction: Iso Golf, "Low Poly Diorama"

> Place at the project root (or reference it from `CLAUDE.md`) so that Claude Code / Opus follows the art direction on every task.
> Hex codes are estimates taken from the reference images: refine them with a color picker on the screenshots.

## 1. Intent

Each level is a **floating miniature island** in a light blue sky, a little above the clouds, like a scale model seen from above. Mood: **Mediterranean summer, calm, serene solitude, gentle nostalgia**. The player should feel almost alone on a peaceful island: a place inhabited by nature and by a few traces of human presence, but never by a crowd. Not a competitive arena.

Keywords: *low poly, faceted, diorama, scale model, peaceful island, solitude, summer afternoon, warm natural tones*.

## 2. Low poly look in 3D

Clean, full-resolution real-time 3D, chosen on the phone from a look test (`feature/lowpoly-look-test`):

- **Perspective camera**, 45° vertical field of view, rotation about (40, 80, 0) on a course heading along +X. The camera sits behind the tee and looks along the course; from M3 its heading turns with the course at rest, and the player can twist it up to ±45°, so what lies ahead, the course or the hole, runs up the screen. The depth is part of the look: the tee is close and large, the hole small and distant.
- **Faceted geometry**: flat normals, so each triangle takes one tone. Terrain has gentle triangulated undulations, flat near the shot line and more broken toward the edges.
- **Clean surface borders**: fairway, green, bunker, water and island edges are cut along their real outlines, never along the triangle grid as sawteeth.
- **Native resolution** with 4× MSAA, in forced portrait. Render scale may be lowered on weaker phones.
- Pipeline: **URP**, Shader Graph for materials.

## 3. Palette

**Warm, natural, slightly desaturated** palette. The only saturated colors are small accents (balls, flag, a few props). If the look turns out too flat in play, revisit the palette before anything else.

| Role | Shades (light → dark) |
|---|---|
| Sky (background) | `#8FC7EA` at the top of the screen → `#CFE8F7` at the bottom |
| Clouds | `#FFFFFF` on sunward facets → `#D6DEED` on shaded facets |
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

## 4. Lighting and shadows

- A single sun, **top-left of the screen**, warm (`#FFF1D6`), fairly high in the sky: early afternoon. It is set once per level from the course's overall heading (tee to hole), so it sits top-left on average, and then never moves: shadows never turn. On a dogleg leg or with the twist it drifts from top-left; checked at the extremes (about ±80°) when built. A time of day per level (variants A and C) may come later; a day-night cycle running during play is not planned, since a moving sun makes crisp shadows shimmer.
- Cast shadows are slightly transparent and cool-tinted (violet / blue-grey), with crisp edges that keep the shape of what casts them. Chosen on the phone in M1.3 over soft blurred shadows and hard jagged ones.
- Trees cast large shadows on the ground: an important compositional element.
- Each facet takes a single tone from the light, with plain lighting. A stepped toon ramp was tried on the phone in M1.3 and rejected: it flattens the facets that make slopes readable, and bleaches sand and green.
- Ambience: warm, generous ambient light, no truly dark areas.

## 5. Texture and material

Material comes from **facets and color**, not from textures:

- Clay / sand: facets, with a few small lighter stones as geometry where a surface needs interest.
- Water: a faceted plane, with highlights as lighter facets, and darker areas under bridges and rocks.
- Stone: readable rectangular blocks, with slight variation from block to block.
- Grass: tone changes between facets, in the rough more than on the fairway.

No photo textures, no fine normal maps.

## 6. Diorama shape

- Each scene is an **island floating a little above the clouds**, with a short uneven band of earth visible under its edge. Chosen on the phone and in Play Mode over a white gradient, a continuous cloud sea (which read as ice) and a soft bank fixed to the camera, for the feeling of lightness and the depth clouds give when the camera moves.
- Clouds: scattered clusters of faceted blobs, well below the island, with the sky showing between them. Each facet takes one pale, baked tone, never lit shading, which turns them into grey boulders. They drift very slowly across the screen.
- The whole course **floats very gently up and down**, about 0.15 m each way over 7 seconds, against clouds that stay at their own height. All its islands move together. The motion is only visual and never affects the ball.
- A course may have **gaps**: separate islands that still belong to one course, with open sky between them. About one par 4 or 5 in three has one, 4–10 m wide, and now and then a par 3 has its green on its own islet. Each island has its own fringe and earth band. A ball that falls into a gap is lost, like one off the island's edge.
- Portrait and the camera angle favor a **long, narrow island**: about 34 × 10 m for the par 3 in the look test, and longer for higher pars (tee to hole: par 3 18–26 m, par 4 40–55 m, par 5 65–85 m).
- Par 3 islands are straight. About half of par 4s and 5s bend once as a **dogleg** of up to 35°, and a par 5 may bend twice; the others bend softly. Every course keeps heading up the screen.
- The outline is irregular, and its grass edge is a **continuous fringe of small, short, blunt tufts**: never a clean geometric edge. The tufts overlap and fold over the edge like thatch, so they read as the ground itself rather than objects on it. Chosen on the phone in M1.4 over separate prisms, which read as rough shards, and over longer or sharper teeth, which looked too spiky.
- Where water reaches the island's edge, there is no fringe. Water falling from that edge is an idea for later.
- Vegetation: trees with **cauliflower foliage** (clusters of low-detail faceted blobs), short trunk. Cypresses as 6- to 8-sided cones. Round bushes.
- Simple, readable volumes: boxes, cylinders, arches, gabled or conical roofs.
- Outlines: none for now. Tested on the phone in M1.4 at about 2 px and about 1 px: tone already separates the shapes, and an outline pushes the look toward a cartoon, drawing lines inside tree crowns. Recheck in M4, once real props are in. Never black lines.

## 7. Scale, solitude and life

- **No characters on screen**, or exceptionally one distant, still silhouette (a fisherman on a rock, a gardener far away): never more than one per level.
- **No club, no avatar**: the ball *is* the player.
- The scale-model feel comes from objects: a bench, a shed, a flag, a low wall are enough to convey the size of the world.
- Life comes from ambience: rustling foliage, a fluttering flag, animated water highlights, birds taking off as the ball passes, butterflies, a cloud shadow drifting across the island.
- Traces of human presence without humans: an empty bench, a rake left in a bunker, a forgotten bucket of balls, a bicycle against a wall, laundry drying on a line.
- Density: a few clusters of detail, lots of calm surfaces and empty space around them. Visual silence is part of the mood.

## 8. Interface

- **Minimal and diegetic**. No cluttered HUD.
- Power bar: a small yellow gauge (`#E6B53A`) with a dark border, floating above the ball.
- Aiming: a dotted white arc following the bell-shaped shot curve, with the landing point marked by a small cross or circle on the ground.
- Putting: on the green there is no arc and no landing marker. The preview stays on the ground: dots along the first part of the putt's true path, about 40%, with no end marker. It never lies, and it leaves the rest of the slope to be read from the facets.
- Shot gesture: a drag that starts on the ball, pulled back like a slingshot. Its visual feedback is limited to the gauge and the arc.
- Camera gestures: one-finger drag to move around, which keeps gliding and slows down after release, pinch to zoom, and a two-finger twist to turn the view up to ±45°, all within the island. Blocked while aiming, allowed during flight.
- Typography: a small, readable font in a warm dark color (`#3A2E25`) over the sky.
- Transitions: fade to white between levels, or the next island "dropping" into place.

## 9. Golf adaptation

### Player and shot

- **The ball is the player.** Off-white and small, but always readable on the phone, with a round shadow on the ground, essential for reading height.
- **Bell-shaped arc** shots away from the green: rise, apex, descent. The shadow stays on the ground and meets the ball on landing: it is what makes the arc readable.
- **Putts on the green**: the ball only rolls along the ground, with no flight. The green's drawn border is the strict limit: just outside it, a shot is a short arc.
- **Lost ball**: in the water, off the island or in a gap between islands, the ball sinks or falls out of sight, then returns to where the shot was played after about a second.
- **Slopes affect the ball**: it rolls downhill, curves on a side slope, travels further going down and stops sooner going up. Slopes stay moderate, which suits the calm mood: the fairway rises and dips up to about ±1.2 m, the green sits up to 1.5 m above or below the tee and tilts 2–6%, and the ground is nearly flat around the hole and flat at the tee. Slopes must be readable on the phone through facet shading; judge that before adding any guide marks.
- Light trail in flight, a small puff of grass or sand on impact, short bounces.

### Progression and orientation

- Progression through **procedurally generated levels**, each one an island diorama.
- Constant play axis: start in the **south-west**, finish in the **north-east**. On screen: the course runs **up the screen with a slight lean to the right**, from bottom-left to top-right.
- The start is always in the lower part of the island, the green in the upper part: the eye reads the level in the direction of the shot.
- **Par 3 courses** fit the whole island in one view when fully zoomed out, and every course starts closer than that, at a default zoom. Every par zooms out to the same scale, so the ball and props keep their size. **Longer courses (par 4, par 5)** show mostly whole when fully zoomed out, as an overview for planning a dogleg or a gap, with the far end small. At the default zoom they are only partly visible, which leaves room for more detail on the course: the camera moves along the course during play, and only turns at rest: once the ball stops it faces the hole if a full shot can reach it, otherwise the leg ahead. It stays still until the ball reaches the middle of the screen, then glides to keep it between about 40% and 60% of the screen's height and width, and at rest eases until the ball sits low on screen with the course ahead. A finger on the screen during flight pauses this follow, and the camera eases back to the ball once the ball has stopped and the finger has lifted. Between levels, the next island appears to the north-east (camera slide or fade to white).

### Procedural kit

To keep generation faithful to the art direction, levels are assembled from a **modular kit** that follows the palette and the rules above:

- Island outline: shape generated with noise along the course's centreline, one or more islands per course, tufted grass edge applied automatically along the whole dry edge of each.
- Strategy: hazards guard the direct line (beside or in front of the green, at a dogleg's corner, across a gap), and a safer, longer route always exists. Holes in one stay rare.
- Surfaces: fairway, green, rough, bunker, water, gravel, each with its own material.
- Props: trees, bushes, rocks, low walls, sheds, benches, bridges. Placed by rules: low density, grouped in clusters rather than scattered, never on the main shot line.
- Per-seed variation: island shape and length, elevation, obstacles, mood (see variants).
- Bigger terrain features (cliffs, plateaus, elevated tees) and aids for reading the green are for a later version.

### Course elements

- **Fairway**: long mowing stripes alternating two greens (`#8FA85A` / `#7E9A48`), aligned with the SW → NE axis. **No tile or checkerboard pattern.** **Open:** on a dogleg, whether the stripes follow each leg or keep the main axis, to judge on a capture when the generator places surfaces.
- **Green**: lighter and smoother green, gently sloped, flat around the hole, red or yellow flag.
- **Rough**: darker grass (`#6A8541`) with more broken facets, blending into the edge.
- **Bunkers**: sand palette, rolled edge, a few rake marks.
- **Water hazards**: water palette, round white rocks, small stone bridge.
- **Scenery**: olive trees, cypresses, dry-stone walls, tile-roofed shed, a small chapel or lighthouse standing alone at the island's edge. No spectators.

## 10. Avoid

- Pixelation, strong depth of field, bloom, chromatic aberration.
- Neon colors, excessive saturation, pure black.
- Clean, geometric diorama edges.
- Tiled or checkerboard surfaces.
- High-resolution or realistic textures.
- Intrusive HUD, large buttons.
- Many characters, crowds, spectators.
- Clubs, golfer avatar, hands or arms on screen.

---

## Variants to explore

Same visual grammar, different moods. Useful for varying courses or testing a more personal identity.

**A. Golden hour (late afternoon)**: low sun, very long shadows, palette pushed toward orange and pink, violet shadows, small lanterns lighting up on a shed.

**B. Scottish links**: cool, desaturated palette (grey-greens, pale sand, misty white sky), dunes, visible wind (animated grass, taut flag), dark stone walls, light rain.

**C. Night golf**: midnight-blue background instead of the blue sky, glowing balls and holes, street lamps casting circles of warm light, fireflies.

**D. Seasons**: the same course in several versions (blooming spring, Mediterranean summer, russet autumn, snowy winter) by swapping only the palette: very cheap to produce.

**E. Village mini-golf**: mini-golf obstacles (windmills, loops, bridges) built into deserted Mediterranean village squares at siesta time. Consistent with the "stone bridge" reference.

**F. Pixel diorama**: the earlier direction, tried and set aside: orthographic 2:1 isometric camera, low-resolution render with integer upscale, camera snapped to the pixel grid. The work is kept on the `feature/pixel-snapping` branch.
