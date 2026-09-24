---
name: unity-jsonutility-save-data
description: Patterns and pitfalls for using JsonUtility to persist gameplay save data — field-vs-property serialization, collection restrictions, and how to round-trip DateTime/DateTimeOffset which JsonUtility cannot serialize natively. Consult before designing or changing a JsonUtility-based save/load shape.
---

# JsonUtility save-data patterns

## Unity serializes fields only, not properties

Official wording: **"Unity serializes fields only, not properties."** A save-data DTO must expose plain public fields (as `PetSaveData` in this project does — `public double hunger;`, not `public double Hunger { get; set; }`). If you write a DTO with auto-properties expecting them to round-trip, `JsonUtility.ToJson`/`FromJson` will silently produce `{}`-shaped output for those members instead of erroring — there's no compiler or runtime warning, just missing data in the written file. Keep game-facing state (`PetState`) as properties for validation/clamping, and keep the save DTO (`PetSaveData`) as a separate plain-field `[Serializable]` class that a mapping method converts to/from — exactly the split this project already uses (`SaveService.ToSaveData`/`FromSaveData`).

## `FromJson` vs `FromJsonOverwrite` — only matters for `MonoBehaviour`/`ScriptableObject` targets

Official wording: **"When deserializing JSON into subclasses of `MonoBehaviour` or `ScriptableObject`, you must use the `FromJsonOverwrite` method. If you try to use `FromJson`, Unity throws an exception because this behavior is not supported."** This project's save DTO (`PetSaveData`) is a plain `[Serializable]` class, not a `MonoBehaviour`/`ScriptableObject`, so plain `JsonUtility.FromJson<PetSaveData>(json)` (as `SaveService.LoadOrCreateDefault` uses) is correct and this restriction doesn't apply here — but it's the first thing to check if a *different* future save/load path ever targets a `ScriptableObject` data asset directly instead of a plain DTO.

## Collections: `List<T>` works as a *field*, not as the root object

The docs restrict what you can pass as the **root** argument to `ToJson`/`FromJson`: *"Unity does not support passing other types directly to the API, such as primitive types or arrays"* — you cannot call `JsonUtility.ToJson(myList)` directly on a bare `List<T>`. That restriction is about the root call, not about `List<T>` as a *field inside* a `[Serializable]` class: `PetSaveData.unlockedDecorIds` (`public List<string> unlockedDecorIds;`) round-trips correctly as a nested field, confirmed by this project's own passing `SaveService` tests. `Dictionary<TKey,TValue>` is not addressed anywhere in Unity's own JsonUtility documentation (checked both the API reference and the JSON serialization manual page directly) — treat it as unsupported by default and use a `List<T>` of a small `[Serializable]` key/value struct instead if a save shape ever needs map-like data; don't assume `Dictionary` works just because it wasn't explicitly called out as forbidden.

## `DateTime`/`DateTimeOffset` are not natively serializable fields — store as a formatted string

Unity's docs don't explicitly enumerate `DateTime`/`DateTimeOffset` as unsupported, but empirically (and consistent with JsonUtility's supported-type list being narrow and undocumented in full) a `DateTimeOffset` field does not round-trip through `JsonUtility` — this project never attempts it directly. Instead, `SaveService`/`PetSaveData` store the timestamp as a `string` field and convert explicitly at the boundary:

```csharp
// PetSaveData.cs
public string lastSavedAtUtc;   // not DateTimeOffset — JsonUtility can't serialize that field type

// SaveService.cs — write
lastSavedAtUtc = state.LastSavedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture)

// SaveService.cs — read
LastSavedAtUtc = DateTimeOffset.ParseExact(data.lastSavedAtUtc, TimestampFormat, CultureInfo.InvariantCulture)
```

Two details in this pattern are load-bearing, not incidental style:
- **`TimestampFormat = "o"`** — the round-trip ("O"/"o") format specifier is the one .NET format that preserves the full offset and sub-second precision losslessly; a friendlier format (e.g. `"yyyy-MM-dd HH:mm:ss"`) would silently truncate precision or drop the UTC offset, corrupting the elapsed-time math this project's whole offline-progress system depends on (see `OfflineProgressCalculator`).
- **`CultureInfo.InvariantCulture`** on both `ToString` and `ParseExact` — without it, formatting/parsing follows the OS locale, so a save file written on one machine's locale could fail to parse (or parse to the wrong instant) when read back under a different locale. Always pin culture explicitly for any persisted, machine-read timestamp string; never rely on the current-thread default culture for save data.

This is the general pattern for *any* type JsonUtility can't serialize directly (also applies to `TimeSpan`, `Guid`, `Enum` values you want as readable text rather than an int): add a `string`-typed field to the DTO and convert explicitly in the mapping methods, rather than trying to make the real type serializable.

## Malformed/missing data: partial fields don't throw, but invalid JSON can

Official wording on a field with no matching JSON value: *"the serializer leaves the constructed values for those fields"* — i.e., a missing field in the source JSON is not an error, the DTO's own default field value (or the no-arg constructor's assignment) is kept. This project's `FromSaveData` leans on that for graceful upgrades — e.g. `data.unlockedDecorIds ?? new List<string> { PetState.DefaultDecorId }` treats a genuinely absent/null field as an explicit "supply the default" case rather than a crash.

Genuinely malformed input (not valid JSON at all — truncated file, non-JSON garbage) is a different failure mode and **is** expected to throw. `SaveService.LoadOrCreateDefault` wraps the read+parse in `try/catch`, logs, and falls back to a fresh in-memory default state **without overwriting the corrupt file on disk** — this is the correct resilience shape for save data: prefer losing the current session's continuity over destructively clobbering a file that might be recoverable (by a future migration, manual inspection, or a support request) if left untouched.

## Sources

- [Unity Scripting API: JsonUtility](https://docs.unity3d.com/ScriptReference/JsonUtility.html) — "must be a custom C# type you have defined... not a primitive type such as `bool` or `string` or a collection type such as `List<T>` or an array" (root-argument restriction).
- [Unity Manual: JSON serialization](https://docs.unity3d.com/Manual/json-serialization.html) — "Unity serializes fields only, not properties"; `FromJson` vs `FromJsonOverwrite` exception behavior for `MonoBehaviour`/`ScriptableObject`; "the serializer leaves the constructed values for those fields" (missing-field behavior).
- Patterns cross-checked against this repo's own working, test-passing implementation: `Assets/Scripts/Core/SaveService.cs`, `Assets/Scripts/Core/PetSaveData.cs`.
