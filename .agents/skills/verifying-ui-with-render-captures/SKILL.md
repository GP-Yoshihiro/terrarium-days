---
name: verifying-ui-with-render-captures
description: Use when a Unity UI Toolkit layout, sprite placement, draw order or style property must be confirmed visually without a device, when a user reports something "looks wrong" on a phone, or when unsure whether a USS/C# style (background-size, scale modes, transforms) actually behaves as assumed in the installed Unity version.
---

# Verifying UI with render captures

## Overview
Don't guess how UI renders — render it. Two tools: a **scene capture** (the real screen at device resolution, for looking) and a **pixel probe** (a tiny explicit test that renders one element and prints pixel colours, for settling a factual question).

## Scene capture (this repo)
`scripts/capture-screens.sh` → `Logs/Screens/*.png` (iPhone 15 Pro 1179×2556: idle, happy+hearts, eat, threat). It is an `[Explicit]` PlayMode test that loads the scene, sets the UIDocument's `PanelSettings.targetTexture` to a RenderTexture, waits, then `ReadPixels` → PNG. Restore `targetTexture = null` in TearDown.
- The editor reports a full-screen safe area: no notch/home-bar insets.
- Real clock and local time apply (night tint at night).

Read cheaply: crop and shrink before viewing —
```bash
sips -c 700 1179 --cropOffset 1000 0 Logs/Screens/01-idle.png --out /tmp/c.png && sips -Z 800 /tmp/c.png
```

## Pixel probe (settle "does X work?")
Explicit PlayMode test, no scene: `PanelSettings` instance with `targetTexture` (RenderTexture) + `ConstantPixelSize`, a `UIDocument` on a new GameObject, add one element per variant, wait 3 frames, `ReadPixels`, log a few pixel colours and `resolvedStyle` values with a grep-able prefix (`EXP …`). Choose sample points whose colour differs per hypothesis (e.g. top row = ceiling vs wall). Put variant USS files in a temporary `Resources/` folder and `Resources.Load<StyleSheet>`. Run with `-testFilter <Class>`, `grep "EXP "` the log, then **delete the experiment**.

Verified in Unity 6000.5.10f1 (240×321 art in a 400×200 box):
| Style | Result |
|---|---|
| `background-size: cover` + `background-position-y: bottom` (USS or C#) | cover, bottom-anchored ✔ |
| `-unity-background-scale-mode: scale-and-crop` | cover, **centred** (floor cropped off in wide boxes) |
| nothing | stretch-to-fill |

## Common mistakes
- Explaining a visual bug from memory of the API ("USS doesn't support it") — probe it; the guess is often wrong.
- Reading full 1179×2556 screenshots (expensive) instead of crops.
- Leaving experiment tests/assets in the project.
