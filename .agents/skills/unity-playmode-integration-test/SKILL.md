---
name: unity-playmode-integration-test
description: Pattern for writing a minimal PlayMode integration test that checks a MonoBehaviour/UI seam actually wires up to the tested Core/Gameplay logic, without coupling the test to Build Settings or wall-clock waits. Consult before adding anything under Assets/Tests/PlayMode/.
---

# Minimal PlayMode integration test pattern

## PlayMode tests always start from a blank scene

Regardless of what scene is open in the Editor, a PlayMode test run begins in an empty scene containing only a camera. Nothing from your project is present until the test puts it there. This means: **do not assume the game's real scene (e.g. `Assets/Scenes/Terrarium.unity`) is loaded** — either build the minimal object graph from code, or explicitly load a scene.

## Prefer building the minimal graph from code over loading the real scene

Two options, in order of preference for this project's "minimal scene integration test" scope (per `AGENTS.md`'s `Assets/Tests/PlayMode/` description):

1. **Build it in the test** — `new GameObject("Terrarium").AddComponent<TerrariumView>()`, wire its serialized dependencies via the component's public API, and assert. No Build Settings dependency, no coupling to the real scene's authored state, fastest to run and easiest to reason about. Use this for checking that a UI adapter or presenter component correctly reads/writes the plain Core/Gameplay types (e.g. does `TerrariumView.Render` update a status bar when `PetState.Hunger` changes) — the actual integration seam worth a PlayMode test, since the Core/Gameplay logic itself is already covered by EditMode tests per this repo's `CLAUDE.md` ("Add or update an EditMode test whenever changing time, status, growth, save, or offline-progress logic").
2. **Load the real scene** — only when the test needs to validate the scene's own authored setup (references wired in the Inspector, prefab placement). Requires the scene to be listed in `EditorBuildSettings.scenes` (Build Settings); load it with `SceneManager.LoadScene("Terrarium")` in `[UnitySetUp]`/`[SetUp]`. This couples the test to build configuration, so reach for option 1 first.

## `[UnityTest]` vs `[Test]`

- Plain NUnit `[Test]` runs synchronously within one frame. A `MonoBehaviour` added via `AddComponent` has its `Awake` called synchronously as part of that call, so a same-frame `[Test]` can already assert on `Awake`-time setup without any yield.
- Use `[UnityTest]` returning `IEnumerator` only when the behavior under test genuinely needs a frame to pass — e.g. asserting on `Start` (which Unity defers to just before the first frame update), or a coroutine's progress. `yield return null` skips exactly one frame.
- Avoid `WaitForSeconds` / real-time waits — they make the test slow and non-deterministic under load. If the behavior depends on elapsed time (this project's `OfflineProgressCalculator`/debug time multiplier), drive it by calling the deterministic calculator directly with a supplied elapsed duration rather than waiting on the actual clock inside a PlayMode test — that determinism is exactly why those calculators are designed to be plain, non-MonoBehaviour classes tested in EditMode instead.
- For physics-dependent behavior specifically, `yield return new WaitForFixedUpdate()` aligns the assertion with the physics step; this prototype has no physics dependency in its care-and-growth loop, so it should rarely if ever be needed here.

## Clean up what you create

PlayMode tests in the same run do not get a fresh domain reload between test cases by default, so objects/scenes left behind by one test can leak into the next. Destroy anything instantiated in `[TearDown]`:

```csharp
[TearDown]
public void TearDown()
{
    Object.Destroy(terrariumGameObject);
}
```

If a test loaded a real scene, restore a clean scene in `[TearDown]` the same way EditMode scene tests do (`NewSceneMode.Single`), so the next test starts from a known state.

## A `[RequireComponent(typeof(UIDocument))]` view needs an Awake-reinitialization guard to be testable

A `TerrariumView`-style `MonoBehaviour` that reads a real `UIDocument.rootVisualElement` in `Awake()` fights the "build it in the test" pattern above: `AddComponent<TerrariumView>()` auto-adds an empty, unconfigured `UIDocument` (via `RequireComponent`), and if the GameObject is active, `Awake()` fires immediately and binds against that empty document — `Q<T>()` finds nothing, every cached field goes null, and the test's own hand-built tree is never even seen.

The fix used throughout this project's `TerrariumView`:

1. Keep the test's GameObject **inactive** while constructing it (`gameObject.SetActive(false)` right after `new GameObject(...)`), so `Awake()` is deferred rather than firing on `AddComponent`.
2. Call the component's own `BindElements(VisualElement root)` with a hand-built tree, then its `Initialize(...)`/equivalent setup method, directly — bypassing `Awake()` entirely.
3. Guard `Awake()` itself with an idempotency check — `if (state != null) { return; }` (or whatever field `Initialize` sets) — so that if the GameObject is *later* activated (see next section), `Awake()` no-ops instead of clobbering the test's manual setup by rebinding against the real, empty `UIDocument`.

```csharp
gameObject = new GameObject("ViewUnderTest");
gameObject.SetActive(false);
view = gameObject.AddComponent<TerrariumView>();   // Awake deferred — GameObject is inactive
view.BindElements(handBuiltRoot);                  // query the test's own tree
view.Initialize(testState, testTuning);            // sets the guard field, paints the tree
gameObject.SetActive(true);                        // now safe — Awake() sees the guard and returns
```

## `StartCoroutine` on an inactive GameObject is a hard test failure, not a quiet no-op

If the behavior under test calls `StartCoroutine` (e.g. an auto-hide timer, a live per-frame tick), it will not silently do nothing on an inactive GameObject — Unity logs `[Error] Coroutine couldn't be started because the game object '...' is inactive!`, and this project's Unity version reports it as an **Error**-level log, which the Unity Test Framework treats as an unhandled failure by default (`Unhandled log message: ... Use UnityEngine.TestTools.LogAssert.Expect`), failing the test even though every assertion in the test body passed. Two ways to handle it, in order of preference:

1. **Activate the GameObject** once setup is safe (see the Awake-guard above) so `StartCoroutine` runs for real, matching production behavior. This is what this project does — after `SetActive(true)`, plain `[Test]` methods still run synchronously within a single frame (a coroutine's body only resumes on the *next* frame's `yield return null`), so calling a method that starts a coroutine inside a `[Test]` is safe: the coroutine is scheduled but its loop body never actually executes during that synchronous test, so it cannot interfere with the test's own assertions or a subsequent direct call to the coroutine's per-tick logic (see below).
2. If activating isn't practical, wrap the call in `LogAssert.Expect(LogType.Error, new Regex("Coroutine couldn't be started"))` — but this only suppresses the failure, it doesn't make the coroutine actually run, so prefer option 1 whenever the test needs the coroutine's effect, not just to avoid the error.

## Expose framework-event handlers as public methods with plain parameters, not simulated events

A UI Toolkit callback signature like `void OnSliderChanged(ChangeEvent<float> evt)` is awkward to unit-test directly — `ChangeEvent<T>` has no simple public constructor, and simulating a real slider drag or button click through the event system is unnecessary ceremony for testing business logic. Instead, keep the callback as a thin one-line adapter and put the actual logic in a public method with a plain parameter:

```csharp
// Production: thin adapter registered on the control.
debugGrowthSlider.RegisterValueChangedCallback(evt => OnDebugGrowthChanged(evt.newValue));

// Public, plain-parameter, directly testable:
public void OnDebugGrowthChanged(double value) { state.Growth = value; Render(state, tuning); }
```

```csharp
// Test: call the logic directly, no event simulation needed.
view.OnDebugGrowthChanged(42d);
Assert.That(view.State.Growth, Is.EqualTo(42d));
```

This project's `TerrariumView` applies the same idea to every interactive element: `OnFeedClicked()`, `OnDecorRowClicked(string decorId)`, `ApplyLiveTickDelta(TimeSpan realDelta)` are all public methods a test calls directly — never a simulated `Button` click or a real coroutine frame wait. When a handler must close over a loop variable (e.g. one `clicked` callback per decor-catalog row), store the built delegate in a `Dictionary<string, Action>` alongside the button, both to allow this direct-call testing style per key and to unregister the exact same delegate instance in `OnDestroy`.

## Assembly definition: do not Editor-restrict the PlayMode test assembly

Unlike the EditMode test assembly (`includePlatforms: ["Editor"]`, see the `unity-project-scaffold` skill), the PlayMode test assembly should target "Any Platform" (leave `includePlatforms`/`excludePlatforms` empty) or explicitly list every platform PlayMode tests must run on — PlayMode tests can execute on-device, not only inside the Editor process. Otherwise the shape matches: `references` the runtime assembly, `optionalUnityReferences: ["TestAssemblies"]`, `autoReferenced: false`.

## Sources

- [Unity Test Framework: Scene-based tests](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/course/scene-based-tests.html)
- [Unity Test Framework: UnityTest attribute](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-attribute-unitytest.html)
