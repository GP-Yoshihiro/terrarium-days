---
name: unity-project-scaffold
description: Reusable folder/assembly-definition layout and idempotent CLI bootstrap pattern for starting a new Unity prototype cleanly. Consult when creating a new Unity project, adding a new asmdef, or reorganizing script folders.
---

# Unity project scaffold pattern

## Folder layout that keeps EditMode tests cheap

Separate runtime logic from Editor-only tooling and from tests, with one assembly definition per compilation boundary:

```
Assets/
  Scripts/
    <RootName>.Runtime.asmdef   # covers Core/, Gameplay/, UI/ below — one runtime assembly
    Core/                       # time, save, state — MonoBehaviour-free, EditMode testable
    Gameplay/                   # care/actions/growth logic — MonoBehaviour-free where possible
    UI/                         # views and input adapters only; thin, calls into Core/Gameplay
  Data/                         # ScriptableObject assets (see unity-scriptableobject-data skill)
  Editor/                       # CLI entry points, bootstrap, build automation — Editor-only
  Tests/
    EditMode/<RootName>.EditModeTests.asmdef   # references the Runtime asmdef, Editor-only platform
    PlayMode/<RootName>.PlayModeTests.asmdef   # references Runtime asmdef, scene/integration tests
```

The PlayMode test asmdef's shape differs from the EditMode one in one field — see the `unity-playmode-integration-test` skill for why (`includePlatforms` stays empty/"Any Platform" instead of `["Editor"]`, since PlayMode tests can run on-device).

An asmdef's assembly covers its own folder **and all subfolders that don't have their own asmdef** — dropping one `.asmdef` at `Assets/Scripts/` is enough to cover `Core/`, `Gameplay/`, and `UI/` as a single runtime assembly without one per subfolder. Only split further if compile-time isolation between those subfolders becomes an actual problem.

## asmdef field reference (grounded in this repo's actual files)

Runtime assembly (`Assets/Scripts/<RootName>.Runtime.asmdef`):

```json
{
  "name": "TerrariumDays.Runtime",
  "rootNamespace": "TerrariumDays",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

EditMode test assembly (`Assets/Tests/EditMode/<RootName>.EditModeTests.asmdef`):

```json
{
  "name": "TerrariumDays.EditModeTests",
  "rootNamespace": "TerrariumDays.Tests",
  "references": ["TerrariumDays.Runtime"],
  "includePlatforms": ["Editor"],
  "autoReferenced": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
```

Key rules:
- `includePlatforms: ["Editor"]` restricts an assembly to the Editor — required for EditMode test assemblies so they never get pulled into a player build.
- `optionalUnityReferences: ["TestAssemblies"]` is what actually pulls in NUnit/the Unity Test Framework references; without it the assembly compiles but `[Test]` attributes won't resolve.
- `autoReferenced: false` on the test assembly keeps it from being implicitly referenced by other assemblies (nothing runtime-side should ever depend on a test assembly).
- List the runtime assembly by name in `references` (Unity resolves it); for **cross-package** references prefer the assembly's GUID instead of its name, since a GUID reference survives the assembly being renamed later — a plain string name reference does not.
- Assembly references cannot be cyclic: if the runtime assembly ever needs something from the test assembly (it shouldn't) or two runtime assemblies reference each other, Unity refuses to compile. Keep dependencies one-directional: tests → runtime, never the reverse.
- Prefer creating/editing `.asmdef` files through the Editor's own UI or by hand-editing this exact JSON shape — hand-rolling unfamiliar fields invites subtle validation errors that only surface as confusing compile failures.

## Idempotent CLI bootstrap

For a setup step that must be safe to re-run (first import, CI, a teammate's fresh clone), follow the pattern in `Assets/Editor/ProjectBootstrap.cs`: a single static `Configure()` method invoked via `-executeMethod` (see the `unity-cli-batchmode` skill) that:

1. Ensures each folder exists with a guard (`AssetDatabase.IsValidFolder` before `AssetDatabase.CreateFolder`), walking the path one segment at a time — `CreateFolder` requires the parent to already exist and only creates one level at a time.
2. Sets `PlayerSettings` (company/product name, orientation, application identifier) — safe to re-run since these are plain assignments, not additive.
3. Creates a starter scene only `if (!File.Exists(scenePath))`, so re-running bootstrap never clobbers scene edits.
4. Wires `EditorBuildSettings.scenes` last, after the scene is guaranteed to exist.
5. Ends with one `AssetDatabase.SaveAssets()` + `AssetDatabase.Refresh()` — do not scatter additional `SaveAssets` calls through the method; one at the end after all mutations is sufficient and avoids redundant disk writes.

This mirrors the ScriptableObject creation guard in the `unity-scriptableobject-data` skill (`if (!File.Exists(assetPath))`) — the same idempotency rule applies to every kind of generated asset: folders, scenes, and data assets alike.

## Common C# pitfall: `using System;` collides with `UnityEngine.Object`

Adding `using System;` to a file that also calls a plain `Object` member (most commonly `Object.DestroyImmediate(...)` / `Object.Destroy(...)` in a test's `[TearDown]`) breaks the build with `error CS0104: 'Object' is an ambiguous reference between 'UnityEngine.Object' and 'object'` (C# reports `System.Object` by its keyword alias `object` here) — both `UnityEngine.Object` and `System.Object` are now in scope and both match the bare name `Object`. This is easy to trigger by accident: a test file that starts with only `using UnityEngine;` compiles fine calling `Object.DestroyImmediate(go)`, and later gains a `using System;` (to get `DateTimeOffset`, `Action`, etc. for a new test) — the previously-fine `Object.DestroyImmediate` line now fails to compile, with no changes to that line itself.

Fix by fully qualifying the call at the point of ambiguity rather than removing either `using`:

```csharp
UnityEngine.Object.DestroyImmediate(gameObject); // unambiguous, keeps both usings
```

Don't reach for a `using Object = UnityEngine.Object;` alias as the default fix — it silently shadows the bare name for the *entire file*, which is more surprising to a future reader than a handful of explicit `UnityEngine.Object.` qualifications at the actual call sites. Reserve the alias for a file that calls `UnityEngine.Object` members frequently enough that repeated qualification would hurt readability more than the alias's shadowing risk.

## Packages/manifest.json

`Packages/manifest.json` lists UPM package dependencies (including the built-in `com.unity.modules.*` entries Unity itself manages). Hand-edit it only to add/pin a specific package version; let the Editor's Package Manager window make additive changes during normal work so `packages-lock.json` stays consistent with it — a manually edited manifest that drifts from the lock file can cause the next Editor open to silently re-resolve versions you didn't intend. Never delete `com.unity.modules.*` entries by hand to "clean up" the manifest; they correspond to engine modules the project may depend on transitively even if no script directly imports them.

Distinguish those engine-module entries from ordinary template scaffolding packages Unity's own project templates add by default — e.g. this repo's manifest carries `com.unity.multiplayer.center` even though the project is explicitly single-pet/no-networking (per `GAME.md`) and nothing under `Assets/` references it (checked directly: no matches for "multiplayer" anywhere in `Assets/`). That's normal — it's the Multiplayer Center *editor tooling* window Unity 6 project templates include by default, not something the project team opted into, and it costs nothing at runtime since it's Editor-only. Unlike a `com.unity.modules.*` entry, it's safe to leave alone (it's inert) or remove later if it's ever actually a nuisance — neither is urgent, and removing unused-but-harmless template scaffolding isn't the same risk category as deleting an engine module.

## Sources

- [Unity Manual: Create or edit the assembly definitions (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-asmdef.html)
- [Unity Manual: Project manifest file](https://docs.unity3d.com/Manual/upm-manifestPrj.html)
- [Unity Manual: Package management with the project manifest file (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/managing-packages-manifest.html)
