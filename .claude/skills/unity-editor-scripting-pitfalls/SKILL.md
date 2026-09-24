---
name: unity-editor-scripting-pitfalls
description: Order-of-operations bugs in Unity Editor automation scripts that compile and run without error yet silently produce wrong output (nulled asset references, no exception). Consult when writing or debugging an Editor script that loads an asset and also switches scenes or triggers a domain-affecting AssetDatabase call in the same method.
---

# Editor scripting order-of-operations pitfalls

## Loading an asset, then calling `EditorSceneManager.OpenScene`, can silently null the reference

**Symptom observed in this repo:** `TerrariumSceneSetup.Configure()` loaded a `PanelSettings` asset via `AssetDatabase.LoadAssetAtPath<PanelSettings>(path)`, then called `EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)`, then assigned the loaded reference to a `UIDocument.panelSettings` field and saved the scene. The assignment compiled and ran with **no exception, no console error** — but the saved scene's YAML showed `m_PanelSettings: {fileID: 0}` (an unassigned reference), even though the exact same pattern for a second asset (a `VisualTreeAsset` loaded the same way) serialized correctly in the same method.

**Root cause:** this matches Unity's well-documented "fake null" behavior for `UnityEngine.Object` — the C# wrapper still exists and is not `null` by `ReferenceEquals`, but the underlying native object has been invalidated, so `== null` (Unity's overridden operator) returns `true` and the object no longer serializes as a valid reference. Community reports (including a titled Unity Issue Tracker entry, "Scriptable Object reference is lost when loading a scene with EditorSceneManager.OpenScene method") describe exactly this: a `ScriptableObject` reference loaded via `AssetDatabase` becomes invalid after an `EditorSceneManager.OpenScene` call in the same script execution, apparently because the scene switch can trigger Unity to unload/reimport assets not otherwise rooted by an active reference.

**Fix:** load (or create) any asset you intend to assign into the scene **after** the `OpenScene` call, not before:

```csharp
// Wrong order — panelSettings can silently become a fake-null by the time it's assigned:
var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
uiDocument.panelSettings = panelSettings; // may serialize as {fileID: 0}

// Right order — load fresh once the target scene is already active:
var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
uiDocument.panelSettings = panelSettings; // serializes correctly
```

If the asset must be *created* (not just loaded) before the scene switch — e.g. an idempotent "ensure this asset exists" step — split it into two: create/`AssetDatabase.SaveAssets()` it in its own method first (returning `void`, not the created reference), then `OpenScene`, then re-load it fresh by path for the actual assignment. Do not carry a "just created" object reference across an `OpenScene` call either — it is subject to the same invalidation.

## Diagnosing this class of bug

The symptom is a *silent* wrong result, not a crash — the same shape as several other Unity fake-null reports (a `ScriptableObject` value reading null after entering/exiting Play Mode, or after a script reimport). When an Editor-script-assigned reference field serializes as `{fileID: 0}` despite the assignment line clearly running:

1. Don't assume the assignment itself is buggy — add a temporary `Debug.Log` immediately after the assignment checking `yourRef == null` **and** (if it's a `UnityEngine.Object`) `AssetDatabase.TryGetGUIDAndLocalFileIdentifier(yourRef, out var guid, out long localId)`. If `== null` is `true` but `TryGetGUIDAndLocalFileIdentifier` still returns a real GUID, that is the fake-null signature — the wrapper still remembers what it pointed to, but the live reference is gone.
2. Check whether anything between the asset load and the assignment could invalidate references: `EditorSceneManager.OpenScene`/`NewScene`, `AssetDatabase.Refresh()`, a script compile/domain reload, or entering/exiting Play Mode are the usual triggers.
3. Reorder so the load happens as close as possible to the assignment, ideally after any scene/domain-affecting call, and re-test.

## Alternative workaround: pin a reference in a `static` field

Re-checked this issue at the tracker URL below (2026-09-24): the page itself now serves a 404 ("the issue may have been made private or removed, or may predate our bug database migration"), so it's still not directly fetchable/quotable — the title and the summary below come from a search index's cached snippet of the report, not a verified primary-source quote, and should be weighted accordingly. With that caveat, the reported root-cause explanation is that **Unity's unused-asset unloading pass does not consider the C# execution stack** — a local variable in a still-running Editor script method does not, by itself, keep an asset "rooted" against unloading the way an active scene/inspector reference does, and a scene switch is one of the operations that can trigger that unloading pass mid-method. The reported workaround besides reordering (this skill's primary fix, already proven in this repo) is to hold the reference in a `static` field for the duration of the operation, which does count as a rooting reference. Prefer the reorder-after-`OpenScene` fix already documented above when it's this simple to apply (as it was in `TerrariumSceneSetup.cs`); reach for a `static` pinning field only if a script's structure genuinely can't be reordered (e.g. the load must happen in one method and the assignment in a later callback with a scene switch unavoidably in between).

## Sources

- Unity Discussions: [UnityEngine.Object == operator fake null: Fix and optimization proposal](https://discussions.unity.com/t/unityengine-object-operator-fake-null-fix-and-optimization-proposal/1734475) — mechanism explanation (managed wrapper survives, native object destroyed, custom `==` override).
- Unity Issue Tracker: [Scriptable Object reference is lost when loading a scene with EditorSceneManager.OpenScene method](https://issuetracker.unity3d.com/issues/scriptable-object-reference-is-lost-when-loading-a-scene-with-editorscenemanager-dot-openscene-method) — title and root-cause summary corroborated via search index; the page itself 404s on direct fetch (re-confirmed 2026-09-24), so treat the "execution stack not considered by unused-asset unloading" explanation and the static-field workaround above as search-snippet-sourced, not a verified direct quote.
- Empirically diagnosed and fixed in this repo: `Assets/Editor/TerrariumSceneSetup.cs` (see its git history / inline comment for the exact before/after).
