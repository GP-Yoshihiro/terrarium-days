---
name: placing-sprites-on-painted-floor
description: Use when 2D sprites (pets, props, decor) must stand on a floor painted inside a background image and they look floating, sunk, misaligned on some screen sizes, or drawn in the wrong front/back order — especially with transparent-padded PNGs, drop shadows, or cropped/scaled backgrounds.
---

# Placing sprites on a painted floor

## Overview
Percent offsets never line up with a floor that is *painted* in the art. Measure the art once, then place background, props and characters through **one projection** from art pixels to screen pixels, standing each sprite on its **ground contact line**.

## Measure (source-image pixels, once per asset)
| What | How | Trap |
|---|---|---|
| Floor | Top surface of the painted floor: back edge y + left/right x, front edge y + left/right x (a trapezoid) | The floor's *front face* / frame below it is not walkable |
| Sprite contact line | Lowest row of the object's **dark outline** (≥4 dark px), not the alpha bbox | A drop shadow below the base makes the prop float by the shadow's height |
| Sprite body span | Visible body left/right x | Transparent padding makes percent placement drift |

Script it (read PNG alpha/rgb); don't eyeball.

## Core pattern
```
scale   = max(viewW/artW, viewH/artH)          // cover
offsetX = (viewW - artW*scale)/2               // crop sides evenly
offsetY =  viewH - artH*scale                  // bottom-anchored: crop the ceiling, never the floor
floorLine(depth) = lerp(back, front, depth)    // y, left, right in view px
place(sprite, x∈[0,1], depth∈[0,1], bodyW):
  k   = bodyW*depthScale(depth) / (bodyRight-bodyLeft)   // px per source px
  box = sprite image size * k                  // keep the PNG's own aspect
  top = floorY - contactY*k
  left= floorLeft + x*(floorRight-floorLeft-bodyPx) - bodyLeft*k
```
- Draw the **background from the same projection** (its own element sized to `artW*scale × artH*scale` at the offsets, parent `overflow:hidden`). Never let USS size the background independently.
- Positions are floor coordinates (x, depth). Depth also drives a mild size scale (back ≈ 0.8).
- Pivot flips/scales at the contact point (`transform-origin` = body centre x, contactY%) so feet never lift.

## Draw order
Sort key = (layer, depth, tieBreak): Background < World < Lighting < Effects; within World the further-forward contact line draws later; fixed tie-break (e.g. decor < pet); hanging props sort behind the floor. Re-sort every frame, reorder children only when the order changed.

## Verify
- Unit test: `feetY == floorLine(depth).y` for several view aspects and depths; body stays within floor edges.
- Render the real scene to a PNG at device resolution and look (REQUIRED: verifying-ui-with-render-captures).

## Common mistakes
- Aligning the alpha-bbox bottom (shadow) instead of the outline base.
- Centered crop (`scale-and-crop`) on a wide view: the front of the floor falls off-screen, so everything "floats" against the back wall.
- Width in % of parent width but height in % of parent height → non-square box, `scale-to-fit` letterboxes and the feet move per device.
- Placing props at the floor's back edge (depth 0) — they sit on the wall line; use mid-floor depths.
