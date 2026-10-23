# Phase 7 Quality Hardening — UI Responsibility Extraction Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce the maintenance risk of the oversized `TerrariumView` by isolating navigation and shop responsibilities without changing runtime behavior.

**Architecture:** Keep `TerrariumView` as one partial MonoBehaviour and move only the existing navigation methods into a focused partial file. Existing PlayMode and capture tests remain the behavior contract; no new gameplay or UI semantics are introduced.

**Tech Stack:** Unity 6.0.5.10f1, C#, UI Toolkit, NUnit EditMode/PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-10-23-phase7-quality-design.md`

## Global Constraints

- Keep Unity Editor at 6000.5.10f1 and iOS as the target platform.
- Preserve the offline-first model, game rules, and save compatibility.
- Work on one issue-sized change at a time; add regression coverage before behavior changes.
- Do not optimize performance without comparable before/after measurements.
- Do not modify or delete `outputs/Claude_引き継ぎファイル一覧.md`.
- Keep test counts at or above EditMode 755, PlayMode 131 passed / 17 skipped, and captures 17.

## Review Focus

- Navigation methods still bind to the same `ShellNavigator` callbacks and preserve selection/modal behavior — verify the existing `TerrariumViewTests` navigation, selection, family-tree, and tab-switch cases.
- Partial-class extraction compiles with all UI callbacks and event subscriptions intact — run full EditMode and PlayMode suites.
- Captured screens remain unchanged — rerun all 17 capture tests and compare the home/cage/shop/incubator/event captures.
- No new Unity compiler, startup, or unexpected PlayMode logs — summarize build/test logs with `scripts/test-summary.py`.
- No accidental behavior or save changes enter this refactor — inspect the final diff for only the planned `TerrariumView` partials and extracted methods.

---

### Task 1: Isolate cage and tab navigation

**Files:**
- Create: `Assets/Scripts/UI/TerrariumView.Navigation.cs` and its Unity `.meta` file
- Modify: `Assets/Scripts/UI/TerrariumView.cs:18,489-718`
- Verify: `Assets/Tests/PlayMode/TerrariumViewTests.cs`

**Interfaces:**
- Consumes: Existing private members and methods of `TerrariumView`; C# partial members share the same type and access.
- Produces: No API changes. `SelectCage`, `ClearSelection`, `ShowCageStep`, `OnHomeButtonClicked`, `OnPrevCageButtonClicked`, `OnNextCageButtonClicked`, `OnHomeIncubatorButtonClicked`, `OnCageTapped`, and `OnTabChanged` keep their existing signatures and behavior.

- [x] **Step 1: Run the focused PlayMode behavior contract before moving code**

Run: `bash scripts/run-unity-tests.sh PlayMode`
Expected: at least 131 passed, 17 skipped, 0 failed; navigation and selection tests pass.

- [x] **Step 2: Extract only the listed navigation methods**

Change the declaration in `TerrariumView.cs` to `public sealed partial class TerrariumView`. Move the nine listed method bodies without edits into `TerrariumView.Navigation.cs`, with the existing namespace and required imports. Do not move fields, alter callbacks, or change logic.

- [x] **Step 3: Run the full verification suite**

Run: `bash scripts/run-unity-tests.sh EditMode`, `bash scripts/run-unity-tests.sh PlayMode`, and `bash scripts/capture-screens.sh`; summarize via `python3 scripts/test-summary.py`.
Expected: EditMode ≥755 passed; PlayMode ≥131 passed and 17 skipped; all 17 captures pass; zero failures.

- [x] **Step 4: Check the diff and build**

Run: `git diff --check` and `bash scripts/build-ios.sh`.
Expected: clean whitespace check, successful iOS build, and diff limited to the partial declaration plus the extracted methods.

---

## Initial baseline (2026-10-23)

- EditMode: 755 passed, 0 failed.
- PlayMode: 131 passed, 17 skipped, 0 failed.
- Capture: 17 passed, 0 failed.
- iOS export/build succeeded; install/launch was not performed because no device was connected.
- Existing navigation tests cover pause/resume, tab-switch family-tree dismissal, and several complete game flows. The extraction is therefore a behavior-preserving first refactor, not a claim that all Phase 7 quality goals are complete.
- No performance optimization is planned until a reproducible bottleneck has a measured baseline.
- Refactor verification: EditMode 755/755, PlayMode 131 passed / 17 skipped / 0 failed, captures 17/17, `git diff --check` clean, iOS build succeeded.

### Task 2: Isolate shop transactions

**Files:**
- Create: `Assets/Scripts/UI/TerrariumView.Shop.cs` and its Unity `.meta` file
- Modify: `Assets/Scripts/UI/TerrariumView.cs:18,2304-2490`
- Verify: `Assets/Tests/PlayMode/TerrariumViewTests.cs`

**Interfaces:**
- Consumes: Existing private members and methods of `TerrariumView`; partial members share one type and access.
- Produces: No API changes. `ShowShopMessage`, `OnShopOfferBuyRequested`, `ConfirmBuyAnimal`, `OnShopItemBuyRequested`, `ConfirmBuyItem`, `OnShopWholesaleRequested`, and `ConfirmWholesale` keep their existing signatures and behavior.

- [x] **Step 1: Confirm the shop behavior contract before moving code**

Run: `bash scripts/run-unity-tests.sh PlayMode`
Expected: at least 131 passed, 17 skipped, 0 failed; animal/supply purchases, insufficient-funds feedback, wholesale, listed-animal refusal, and stale-confirmation tests pass.

- [x] **Step 2: Extract only the listed shop methods**

Keep the existing `partial` declaration. Move the seven listed methods and their XML documentation without edits into `TerrariumView.Shop.cs`, with required imports and the existing namespace. Do not move shared feedback helpers, fields, pairing code, or change logic.

- [x] **Step 3: Run the full verification suite**

Run: `bash scripts/run-unity-tests.sh EditMode`, `bash scripts/run-unity-tests.sh PlayMode`, and `bash scripts/capture-screens.sh`; summarize via `python3 scripts/test-summary.py`.
Expected: EditMode ≥755 passed; PlayMode ≥131 passed and 17 skipped; all 17 captures pass; zero failures.

- [x] **Step 4: Check the diff and build**

Run: `git diff --check` and `bash scripts/build-ios.sh`.
Expected: clean whitespace check, successful iOS build, and only the listed methods move between the target files.

### Task 3: Stress tab and modal transitions

**Files:**
- Modify: `Assets/Tests/PlayMode/TerrariumViewTests.cs` near `SwitchingTabs_ClosesTheFamilyTree`

**Interfaces:**
- Consumes: `ShellNavigator.ShowTab(ShellTab)`, `TerrariumView.OnLedgerAnimalTapped(int)`, and the existing PlayMode fixture.
- Produces: `RepeatedNavigationAcrossAllTabs_ClosesFamilyTreeWithoutUnexpectedLogs`, a PlayMode smoke test that covers all six tabs for three rounds while opening and dismissing the family-tree modal.

- [x] **Step 1: Add the repeated-navigation smoke test**

Create a temporary-save colony, add a second animal, initialize the cage-list view omitted by this test harness's skipped `Awake`, attach the same `OnTabChanged` delegate as the existing family-tree test, and repeat the six `ShellTab` values three times. Before each tab switch, open the family-tree modal; afterward assert the modal is hidden and the navigator selected the requested tab. Finish with `LogAssert.NoUnexpectedReceived()`.

- [x] **Step 2: Run the focused and full verification**

Run: `bash scripts/run-unity-tests.sh PlayMode`, then EditMode and all capture tests; summarize with `python3 scripts/test-summary.py`.
Expected: the new smoke test passes; EditMode ≥755 passed; PlayMode ≥132 passed and 17 skipped; captures 17/17; zero failures.

## Current verification (2026-10-23)

- EditMode: 755 passed, 0 failed.
- PlayMode: 132 passed, 17 skipped, 0 failed (includes the new 18-transition smoke test).
- Capture: 17 passed, 0 failed.
- iOS build succeeded; physical install/launch remains unavailable while no device is connected.
- Final code review: no findings for Tasks 1–4; Task 5 review follows.

### Task 4: Isolate event UI transactions

**Files:**
- Create: `Assets/Scripts/UI/TerrariumView.Events.cs` and its Unity `.meta` file
- Modify: `Assets/Scripts/UI/TerrariumView.cs:18,2398-2550`
- Verify: `Assets/Tests/PlayMode/TerrariumViewTests.cs`

**Interfaces:**
- Consumes: Existing private members and methods of `TerrariumView`; partial members share one type and access.
- Produces: No API changes. `WireEventEvents`, `OnEventActionRequested`, `OnEventServeAllRequested`, `ConfirmEventServeAll`, `OnEventFinishRequested`, `OnEventCloseRequested`, `OnEventShopRequested`, `OnEventBuyConfirmed`, `OnEventRegisterRequested`, `ConfirmEventRegister`, and `OnEventSaveRequested` keep their existing signatures and behavior.

- [x] **Step 1: Confirm the event behavior contract before moving code**

Run: `bash scripts/run-unity-tests.sh PlayMode`
Expected: at least 132 passed, 17 skipped, 0 failed; event sale, buy, no-cage/no-money dialogs, registration, action replay, and result flows pass.

- [x] **Step 2: Extract only the listed event methods**

Keep the existing `partial` declaration. Move the eleven listed methods and their XML documentation without edits into `TerrariumView.Events.cs`, with required imports and the existing namespace. Do not move incubator/pairing methods, fields, or change logic.

- [x] **Step 3: Run the full verification suite**

Run: `bash scripts/run-unity-tests.sh EditMode`, `bash scripts/run-unity-tests.sh PlayMode`, and `bash scripts/capture-screens.sh`; summarize via `python3 scripts/test-summary.py`.
Expected: EditMode ≥755 passed; PlayMode ≥132 passed and 17 skipped; captures 17/17; zero failures.

- [x] **Step 4: Check the diff and build**

Run: `git diff --check` and `bash scripts/build-ios.sh`.
Expected: clean whitespace check, successful iOS build, and only the listed methods move between the target files.

Verification: EditMode 755/755, PlayMode 132 passed / 17 skipped / 0 failed, captures 17/17, iOS build succeeded.
Review note: the final reviewer found one incubator XML summary left on `OnNestBoxButtonClicked`; it was moved to `OnMoveEggsButtonClicked`, and `git diff --check` is clean.

### Task 5: Isolate incubator UI transactions

**Files:**
- Create: `Assets/Scripts/UI/TerrariumView.Incubator.cs` and its Unity `.meta` file
- Modify: `Assets/Scripts/UI/TerrariumView.cs:18,671-692,2354-2449`
- Verify: `Assets/Tests/PlayMode/TerrariumViewTests.cs`

**Interfaces:**
- Consumes: Existing private members and methods of `TerrariumView`; partial members share one type and access.
- Produces: No API changes. `OnMoveEggsButtonClicked`, `OnIncubatorTemperatureChangeRequested`, `OnIncubatorWaterRequested`, `OnIncubatorEggTapped`, `OnIncubatorRemoveRequested`, `WireIncubatorEvents`, `OnTrayMoveToCageRequested`, `OnTraySellRequested`, and `AfterIncubatorAction` keep their existing signatures and behavior.

- [x] **Step 1: Confirm incubator behavior before moving code**

Run: `bash scripts/run-unity-tests.sh PlayMode`
Expected: at least 132 passed, 17 skipped, 0 failed; incubator settings/water/removal, hatch tray move/sell, cage-egg transfer, and the complete breeding-to-hatching loop pass.

- [x] **Step 2: Extract only the listed incubator methods**

Move the nine listed methods and their XML documentation without edits into `TerrariumView.Incubator.cs`. Keep `OnNestBoxButtonClicked` and pairing operations in the main file; do not change logic.

- [x] **Step 3: Run full verification**

Run: `bash scripts/run-unity-tests.sh EditMode`, `bash scripts/run-unity-tests.sh PlayMode`, and `bash scripts/capture-screens.sh`; summarize via `python3 scripts/test-summary.py`.
Expected: EditMode ≥755 passed; PlayMode ≥132 passed and 17 skipped; captures 17/17; zero failures.

- [x] **Step 4: Check the diff and build**

Run: `git diff --check` and `bash scripts/build-ios.sh`.
Expected: clean whitespace check, successful iOS build, and only the listed methods move between the target files.

Verification: EditMode 755/755, PlayMode 132 passed / 17 skipped / 0 failed, captures 17/17, iOS build succeeded.
