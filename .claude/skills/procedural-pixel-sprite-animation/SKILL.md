---
name: procedural-pixel-sprite-animation
description: Use when a 2D game needs new character animation frames (walk, idle, eat, sleep, reactions, colour variants) and no artist or suitable asset exists, when AI-generated art has fake "checkerboard" transparency, or when a walk cycle looks like sliding/moonwalking.
---

# Procedural pixel-sprite animation

## Overview
Draw the character from a **pose** (spine points with a thickness profile, legs as hip→knee→foot segments, face features) at low resolution, colour by position along/across the body, outline, then upscale ×2 nearest-neighbour. Every frame shares one palette, grid and ground line, so animation is consistent and placement stays exact. Reference implementation: `tools/sprites/gecko_sprites.py` (pure Python, no deps).

## Pipeline
1. `Pose(...)` per frame → `render(pose)` → canvas of colours/None.
2. Outline pass: empty pixel touching the silhouette → outline colour.
3. Colour variants are palette transforms of the same canvas (e.g. pale pre-shed skin) — never re-pose.
4. Write `Resources/<Set>/<clip>_<nn>.png`; import with point filter, no mips, uncompressed (an `AssetPostprocessor` keyed on the folder).
5. Re-measure the contact line and body span after changing proportions (REQUIRED: placing-sprites-on-painted-floor).

## Walk cycles that don't slide
- Use the animal's real gait: sprawling lizards trot in **diagonal couplets** (near-fore + far-hind), duty factor ≈ 0.7 (leopard gecko), trunk bends laterally (girdles swing in anti-phase), tail carried off the ground.
- **Pixel-lock the stance:** body advances `STEP` px per frame and a planted foot moves back exactly `STEP` px relative to the body (stance = N_stance frames, swing lifts and returns). Stride = frames × STEP.
- **Advance walk frames by distance walked, not time:** `frame = floor(distance_src_px / stride * frames) % frames`, distance measured at the feet and converted to sprite pixels (divide by on-screen px per source px). Any speed, mood or depth scale then stays slip-free.
- Verify with a **world-lock strip**: stack the frames vertically, each shifted by −STEP×i; planted feet must stay in one column.

## Quick checks
| Check | How |
|---|---|
| Ground line identical in all frames | contact sheet with a red line at the contact row |
| Readable at game size | zoom crops ×2 beside the old art |
| Fake transparency in sourced PNGs | alpha all 255 + corners alternating two greys → flood-fill near-neutral greys from the border (outline stops it), zero RGB of cleared pixels |

## Common mistakes
- Time-based walk frames with variable movement speed → moonwalking feet.
- Moving the planted foot with the girdle sway — sway the hip joint, keep the foot fixed.
- Jumps that lift the body but keep feet on the ground read as tiptoeing; move feet with the body only for real jumps.
