---
name: ai-generated-placeholder-art
description: Workflow for sourcing style-consistent placeholder game art via a browser-based AI image generator (e.g. Gemini) when no suitable free/CC0 asset exists, including how to pull the generated pixels out of a sandboxed browser pane and crop a multi-item sheet into separate files. Consult when a task needs new sprite/icon art and free-asset search has failed or been explicitly declined.
---

# AI-generated placeholder art workflow

## When to reach for this

Try free/CC0 asset sources first — itch.io (filter by CC0/free tags), OpenGameArt.org, Kenney.nl are reputable starting points. Reach for AI generation when: the subject is too specific/niche for existing free packs (a particular species, a specific prop combination), what's available is stylistically mismatched (fantasy-mascot art for a grounded/realistic subject, or vice versa), or the user explicitly asks for generated art. This is a real workflow with real steps and real limits — not a fallback to reach for reflexively; a few free-asset searches first is usually worth the time.

**Permission boundary:** downloading/using a generated image is still "downloading a file" — confirm with the user before treating any generated image as final, same as any other file download. Never attempt to log in on the user's behalf; if the built-in browser needs a session, open the page and have the user log in themselves in that pane.

## Getting style-consistent results across multiple assets

Keep every generation call in the **same** chat conversation with the image model, and explicitly reference the earlier result in each new prompt ("matching the exact same pixel art style as the gecko sprite above"). Image models otherwise have no persistent style memory between separate conversations — same-conversation continuity is the only lever for visual consistency across a multi-asset set (a pet + its environment + several decor props, for example).

Ask for **one item per image** when the item is a hero/centerpiece (needs full prompt attention to get right), but ask for **several related items in one image** when they're smaller/secondary (icons, decorations) — one generation call producing 5-7 small props in a grid is far cheaper than 5-7 separate calls, at the cost of the model's own layout being unreliable (see below).

Do not expect a "N items in a single clean row" instruction to be followed precisely — image models frequently ignore exact count/layout instructions, producing an irregular grid with some duplicated or missing items instead. Plan to crop generously and pick the cleanest instance of each needed item rather than relying on a predictable fixed grid.

## Extracting pixels out of a sandboxed browser pane

A "download" button inside an isolated/sandboxed browser pane (as opposed to the user's real Chrome) often does not land the file anywhere the agent's filesystem tools can see — there is no accessible Downloads folder to check. The reliable extraction path is to pull the already-rendered `<img>` element's pixels via canvas, not to trigger the site's own download mechanism:

```javascript
// Run via the browser's JS-execution tool, against the page showing the generated image.
const img = Array.from(document.querySelectorAll('img')).find(i => i.naturalWidth > 200);
const canvas = document.createElement('canvas');
canvas.width = img.naturalWidth; canvas.height = img.naturalHeight;
const ctx = canvas.getContext('2d');
ctx.imageSmoothingEnabled = false; // preserve hard pixel edges for pixel-art style
ctx.drawImage(img, 0, 0);
canvas.toDataURL('image/png'); // returns "data:image/png;base64,...."
```

This works even when a direct `fetch()` of the image's `blob:` URL fails with a CORS-style error — reading through an already-loaded `<img>` via canvas is a same-origin operation from the page's own perspective, unlike a fresh cross-origin fetch.

**Size limits force a file round-trip, which is fine — use it.** A full-resolution generated image's data URL (easily 100K+ characters as base64) will exceed a single tool call's inline-output limit. When that happens the tool run's own fallback writes the full output to a local file and reports its path — don't fight this by trying to shrink the request further than necessary; just read that file back:

```powershell
$raw = Get-Content -Raw "<path the tool reported>"
$m = [regex]::Match($raw, "[A-Za-z0-9+/]{200,}={0,2}")   # the base64 payload, regardless of what JSON/quoting wraps it
$bytes = [Convert]::FromBase64String($m.Value)
[System.IO.File]::WriteAllBytes("Assets/Art/whatever.png", $bytes)
```

The regex-extract approach sidesteps a real gotcha: the tool's own JSON-wrapping of a returned string can end up double-encoded (quotes appearing as literal characters inside the parsed value), which breaks a naive `ConvertFrom-Json | .text | FromBase64String` pipeline with a "not a valid Base-64 string" error. Since the base64 alphabet (`A-Za-z0-9+/=`) never legitimately appears in surrounding JSON punctuation, matching the longest run of it directly is more robust than trusting the wrapping format.

To keep the round-trip fast for a batch of small crops, downscale in the same canvas call (draw into a smaller canvas / use the 6-argument `drawImage` for a sub-region) rather than extracting full-resolution and downscaling afterward — this also keeps each individual result closer to (or under) the inline-output limit, avoiding the file round-trip for the smaller follow-up crops.

## Cropping one generated sheet into several final assets

When several small items landed in one irregular grid image (see above), crop each needed item with the 9-argument `drawImage` (source rect → destination rect), picking generous fractional bounding boxes by eye from a lower-res preview rather than trying to compute exact pixel boundaries:

```javascript
const sx = img.naturalWidth * 0.5, sy = 0, sw = img.naturalWidth * 0.5, sh = img.naturalHeight * 0.33; // e.g. top-right quadrant
const outCanvas = document.createElement('canvas');
outCanvas.width = 180; outCanvas.height = Math.round(180 * sh / sw);
outCanvas.getContext('2d').drawImage(img, sx, sy, sw, sh, 0, 0, outCanvas.width, outCanvas.height);
outCanvas.toDataURL('image/png');
```

Render one full-sheet preview at a moderate size first (e.g. ~220px wide) and actually look at it (via the Read tool on the saved PNG) before deciding crop boundaries — guessing fractional boundaries blind wastes round-trips on bad crops.

## If a source site blocks automated access, stop — don't work around it

A Cloudflare (or similar) bot-verification challenge page, or a browser-pane navigation that gets denied to an unfamiliar signed-URL domain, is a real access boundary, not a bug to route around. Pivot to a different source (or to generation, per this skill) rather than retrying with obfuscated URLs, alternate navigation paths, or anything else aimed at defeating the block — this matches the standing prohibition on bypassing bot-detection.

Observed concretely: itch.io's tag/search listing pages (`itch.io/game-assets/free/tag-...`) served a Cloudflare challenge in a sandboxed browser pane, while a specific project's own page (`<creator>.itch.io/<project>`) loaded normally — reachability can differ page-to-page on the same site. A direct-download link's signed CDN URL (e.g. an R2/S3-style `X-Amz-...` URL obtained from the page's own download API response) can also be refused by the browser pane's own navigation policy even when the referring page loaded fine — treat that refusal the same way, as a boundary to stop at, not a fetch-vs-navigate technicality to route around via `fetch()` or a different tab.
