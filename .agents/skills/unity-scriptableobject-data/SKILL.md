---
name: unity-scriptableobject-data
description: Pattern for authoring Unity ScriptableObject tuning/config data that stays designer-editable in the Inspector but does not force EditMode tests or gameplay services to depend on UnityEngine.Object. Consult before adding a new tuning data asset or a CLI/editor-script asset generator.
---

# ScriptableObject tuning data pattern

## Keep the tunable values in a plain class; let the ScriptableObject be a thin host

Gameplay services (a `CareService`, an `OfflineProgressCalculator`, anything under `Assets/Scripts/Gameplay/` or `Core/`) should depend on a **plain C# data type** for their tuning values, not on `UnityEngine.ScriptableObject` directly. Only the asset-hosting wrapper touches `ScriptableObject`:

```csharp
// Core/CareTuning.cs — plain class, zero UnityEngine dependency, `new`-able in EditMode tests.
public sealed class CareTuning
{
    public double FeedHungerAmount { get; set; } = 30d;
    // ...
}

// Data/CareTuningAsset.cs — the only place that touches ScriptableObject.
[CreateAssetMenu(fileName = "CareTuning", menuName = "Terrarium Days/Care Tuning")]
public sealed class CareTuningAsset : ScriptableObject
{
    [SerializeField] private double feedHungerAmount = 30d;
    // ... one [SerializeField] per CareTuning property, Inspector-editable

    public CareTuning ToCareTuning() => new CareTuning { FeedHungerAmount = feedHungerAmount, /* ... */ };
}
```

This is why this repo's `CareTuning` (see `Assets/Scripts/Core/CareTuning.cs`) is a plain class rather than a `ScriptableObject` today: nothing yet needs Inspector editing, and every consumer can be constructed and asserted against in EditMode tests with no `AssetDatabase`, no scene, no Editor context. Promote to the two-type split above only once a tuning value actually needs to be designer-editable — don't add the `ScriptableObject` wrapper speculatively.

## Why this split matters for testing

- A test that does `new CareTuning()` runs anywhere, instantly, with zero Unity bootstrap cost.
- A test that depends on a `ScriptableObject` needs `ScriptableObject.CreateInstance<T>()` at minimum, and if the code path touches `AssetDatabase`, the test can only run inside the Editor process at all (fine for EditMode, but it couples pure logic to Editor-only APIs for no reason).
- Keeping services constructor-injected with the plain type means the same service works identically whether the values came from a ScriptableObject asset, a JSON save file, or a test literal.

## Authoring the asset in the Editor (interactive)

`[CreateAssetMenu(fileName = "...", menuName = "...")]` on the wrapper class adds a "Create > <menuName>" entry in the Project window's right-click menu. Designers create and tweak the asset from there; no script changes needed after the wrapper exists. Assets serialize as YAML text by default, so they diff and merge reasonably in git.

## Authoring the asset from a script (CLI / idempotent bootstrap)

For CLI-driven or idempotent setup (the pattern already used by `Assets/Editor/ProjectBootstrap.cs` for folders/scenes), generate the asset the same way:

```csharp
if (!File.Exists(assetPath))
{
    var asset = ScriptableObject.CreateInstance<CareTuningAsset>();
    AssetDatabase.CreateAsset(asset, assetPath); // path must end in a native asset extension, e.g. ".asset"
}
```

Notes:
- Changes made to an existing SO instance via script in Edit mode are **not** auto-saved — call `EditorUtility.SetDirty(asset)` after mutating it, then `AssetDatabase.SaveAssets()` (ProjectBootstrap already calls `AssetDatabase.SaveAssets()` once at the end of its run; reuse that same call rather than adding a second one).
- Guard creation with a `File.Exists` (or `AssetDatabase.LoadAssetAtPath`) check so the bootstrap stays idempotent and never clobbers designer edits on a second run — same rule CLAUDE.md already states for gameplay code ("Preserve existing user changes").

## Runtime mutation caution

A `ScriptableObject` asset is a single shared instance across all consumers, including across play sessions in the Editor (it does not reset on domain reload the way scene objects do). Never mutate the asset's own fields at runtime — always convert to the plain data type first (`ToCareTuning()` above) and mutate that copy, or clone the asset with `Instantiate()` before touching it. Mutating the shared asset directly corrupts the designer's saved values on disk the next time `AssetDatabase.SaveAssets()` runs.

## Confirmed in practice, not just in theory

This isn't a speculative recommendation — it's what actually happened. `CareTuning` stayed a plain class for this prototype's entire build, through every later step that might plausibly have forced the promotion: `CareService`, `OfflineProgressCalculator`, and `DecorUnlockService` all consumed it directly; a developer panel was eventually built with live `Slider` controls that edit `PetState` values at runtime (a case that specifically motivated the "runtime mutation caution" section above) — and even that never needed `CareTuning` itself to become a `ScriptableObject`, because the sliders mutate the plain `PetState`/`CareTuning` instances the view already owns, not a shared asset. Across roughly 50+ EditMode/PlayMode tests touching this data, none needed `AssetDatabase`, a scene, or `ScriptableObject.CreateInstance`. Treat "no Inspector-editing need has appeared yet" as a genuinely reliable signal to keep deferring the wrapper, not just an optimistic guess — for a prototype of this size, the need may never appear at all.

## Sources

- [Unity Manual: ScriptableObject (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/class-ScriptableObject.html)
- [Unity Scripting API: AssetDatabase.CreateAsset](https://docs.unity3d.com/ScriptReference/AssetDatabase.CreateAsset.html)
- [Unity Scripting API: ScriptableObject (6000.3)](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ScriptableObject.html)
