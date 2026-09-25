---
name: unity-uitoolkit-runtime-styling
description: Patterns for driving UI Toolkit visuals from C# at runtime — dynamic USS class swapping, show/hide via DisplayStyle (overlays) vs Visibility (reserved in-flow slots), referencing project assets from USS, and syncing a control's value without re-firing its own callback. Consult before writing a MonoBehaviour that mutates a UIDocument's visual tree in response to game state.
---

# UI Toolkit runtime styling patterns

## Drive state-dependent appearance with `AddToClassList`/`RemoveFromClassList`, not inline styles

This is Unity's own documented best practice, not just a convention this project happened to land on: inline styles (`element.style.xxx = ...`) always take precedence over USS selectors and don't compose, so they're for one-off per-element exceptions, not for a value that switches between a fixed set of visual states. For anything with named states — a status bar's color tier, which decoration icon is showing — define one USS class per state and swap:

```csharp
barFill.RemoveFromClassList("status-good");
barFill.RemoveFromClassList("status-warning");
barFill.RemoveFromClassList("status-critical");
barFill.AddToClassList(LevelClassName(level));
```

For a class name computed from a dynamic key (this project's decor icon, keyed by decor id) rather than a fixed small set, track which class you last applied so you can remove exactly that one before adding the new one — don't try to enumerate/guess what might currently be applied:

```csharp
if (appliedDecorIconClass != null) { decorImageElement.RemoveFromClassList(appliedDecorIconClass); }
appliedDecorIconClass = $"decor-icon-{petState.SelectedDecorId}";
decorImageElement.AddToClassList(appliedDecorIconClass);
```

Reserve `element.style.width = new Length(...)` (an inline style) for genuinely continuous values a class list can't express — a status bar's fill percentage, a gauge width — not for anything with a finite set of named states.

**Typo risk:** class names toggled this way are plain strings with no compile-time checking — a typo in either the C# string or the USS selector compiles fine and just silently fails to style anything. There is no built-in mitigation from Unity; for a class list large enough to matter, define the class-name strings as `const` fields near where they're used (as this project already does for its three status-tier names) rather than re-typing the literal at each call site, so a typo is a single edit away from being caught by "find usages" instead of hidden in scattered string literals.

## Show/hide: `style.display` for overlays, `style.visibility` for in-flow status text

`DisplayStyle.None` removes the element from layout entirely (equivalent to CSS `display: none` — siblings reflow as if it isn't there); `Visibility.Hidden` keeps its layout space reserved but invisible. For modals, drawers, and panels that should not occupy space while closed — this project's milestone modal, decor drawer, and debug panel — `style.display` is the correct one:

```csharp
milestoneModal.style.display = DisplayStyle.Flex; // show
milestoneModal.style.display = DisplayStyle.None;  // hide, and stop reserving its layout space
```

**Exception — anything in the normal layout flow that appears and disappears often** (a feedback/log line between the terrarium and the buttons, a weather/status row): toggling `display` makes every sibling reflow, so the terrarium and buttons jump each time a message shows. Give it a fixed height (`height` + `flex-shrink: 0`, `white-space: nowrap`, `text-overflow: ellipsis`) and toggle `style.visibility` instead; the slot stays reserved.

Initialize every such overlay to `DisplayStyle.None` in your view's setup path (this project does it once in `Initialize()`, covering the milestone modal, decor drawer, and debug panel together) so the UXML's authored default (visible, for easy editing in the UI Builder / by eye in the raw markup) never leaks into a freshly-loaded screen.

## Reference project assets from USS with a leading-slash path

`background-image: url("/Assets/Art/terrarium_background.png");` — the leading `/` makes the path project-root-relative, which resolves correctly both in the Editor and in a built player (as opposed to a path relative to the `.uss` file's own folder, which is also valid syntax but easy to get wrong when USS/UXML files move). This project's `Terrarium.uss` uses this for every background-image rule, including the ones swapped in dynamically via class list (see above) — the URL lives in the USS rule, so the C# side only ever needs to know the class name, never a texture reference or `Resources.Load` path.

## Sync a control's displayed value without re-triggering its own callback

When you programmatically refresh a `Slider`/similar control from current game state (e.g. repopulating a debug panel's sliders when it opens), setting `.value` directly fires that control's own `RegisterValueChangedCallback` handler — which, if that handler writes the value back into game state and re-renders, is at best redundant and at worst a feedback loop. Use `SetValueWithoutNotify` for this direction of sync:

```csharp
debugHungerSlider.SetValueWithoutNotify((float)state.Hunger); // display only, no callback fires
```

Reserve the real `.value` setter (or a full `ChangeEvent` dispatch) for when you genuinely want the change to be treated as user/programmatic input that should flow through the normal handler.

## Performance: keep selectors shallow

Avoid selectors ending in a universal `*` or matching broad built-in classes (e.g. `.unity-button`) across a large tree — Unity's own guidance warns deep/broad selectors are evaluated against a large portion of the visual tree and can cost measurable style-resolution time as a UI grows. Scope selectors to the specific classes you define (as this project's `.status-good`/`.decor-row`/etc. already do) rather than relying on element-type or built-in-class selectors for anything performance-sensitive.

## `UIDocument` is officially deprecated in Unity 6.5+ in favor of `Panel Renderer` — correction from earlier tonight

**Correcting an earlier note in this file:** a first pass at this research (based on a "What's New" summary page) concluded UIDocument was not deprecated. Checking the actual component manual page directly (`UIE-create-ui-document-component.html`, 6000.5) shows that was too optimistic — the precise official wording is: "UI Document components is obsolete and superseded by the Panel Renderer component," and "Because the UI Document component has been deprecated, **you can't add the component to new GameObjects in the Unity Editor**." So UIDocument is genuinely deprecated as of Unity 6.5, not merely "also available."

**What this means for this project, concretely:**
- The restriction documented is specifically about **adding UIDocument via the Editor's own UI** (Add Component menu/search). Existing UIDocument components already assigned to a GameObject — this project's `TerrariumUI` GameObject in `Assets/Scenes/Terrarium.unity`, wired by `TerrariumSceneSetup.cs` — are explicitly stated to keep working: "Existing UI Document components in your project will continue to work, if they have already been assigned to a GameObject."
- **Scripted addition still works, empirically confirmed by this project's own test suite.** `TerrariumView`'s `[RequireComponent(typeof(UIDocument))]` attribute triggers `AddComponent<UIDocument>()` every single time a `TerrariumView` is added to a `GameObject` — which happens in every one of this project's ~24 PlayMode tests (`gameObject.AddComponent<TerrariumView>()` in `TerrariumViewTests.cs`), and all of them passed as of tonight's spot-check. The official deprecation notice doesn't state that scripted `AddComponent<UIDocument>()` is blocked, only that the Editor's own add-component UI won't offer it for new GameObjects — and this project's evidence is consistent with that reading.
- **Practical guidance for this project going forward:** don't add a *new* `UIDocument` to a scene by hand through the Editor's Add Component search (it likely won't be offered, or will be flagged) — script it via `AddComponent<UIDocument>()` in an idempotent Editor bootstrap step instead (as `TerrariumSceneSetup.cs` already does), or, better, treat any *new* UI screen this project adds as a candidate for `Panel Renderer` from the start rather than extending the now-deprecated pattern further. Migrating the *existing* `TerrariumView`/`UIDocument` pair is a deliberate future decision, not urgent (it still works, per Unity's own compatibility statement) — but this is weaker justification for deferral than "not deprecated at all" was.

Unity 6.5 also switched UI Toolkit's default text renderer to the Advanced Text Generator (ATG), which requires no project setting to benefit from — this project's UI already gets its reported 10-40% text CPU-cost improvement over the old TextCore path for free, nothing to configure or verify.

## Sources

- [Unity Manual: Styling (UI Toolkit for advanced Unity developers, 6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/styling.html)
- [Unity Manual: New in Unity 6.5](https://docs.unity3d.com/6000.5/Documentation/Manual/WhatsNewUnity65.html) — Panel Renderer introduction, Advanced Text Generator default.
- [Unity Manual: UI Document component (6000.5)](https://docs.unity3d.com/6000.5/Documentation/Manual/UIE-create-ui-document-component.html) — the authoritative source for UIDocument's actual deprecation wording; prefer this over a "What's New" summary page when precision matters, per the correction above.
- [Unity Scripting API: UIElements.Visibility.Hidden](https://docs.unity3d.com/ScriptReference/UIElements.Visibility.Hidden.html) — direct source for the DisplayStyle-vs-Visibility distinction, re-verified word for word against this skill's claim.
- Patterns cross-checked against this repo's own working implementation: `Assets/Scripts/UI/TerrariumView.cs`, `Assets/UI/Terrarium.uss`.
