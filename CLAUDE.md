# Terrarium Days — Claude Code project guide

## Project goal
Create a small, offline-first iOS (iPhone/iPad) portrait prototype in Unity: nurture one leopard gecko in a terrarium. The playable scope and acceptance criteria are in `GAME.md`.

## Current stage
This repository is a Unity 6 project created with **6000.5.3f1**. iOS Build Support and a Unity Personal license are active. Keep this Editor version fixed for the prototype unless an explicit upgrade is requested. The executable is `C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.3f1\\Editor\\Unity.exe`. This project switched its target platform from Android to iOS on 2026-09-24; Android Build Support, `Build-Android.ps1`, and `BuildAutomation.BuildAndroidDevelopment` no longer exist in this repo. A CLI build from this Windows machine only produces an Xcode project (`scripts/Build-iOS.ps1`) — compiling, signing, and installing onto a real iPhone/iPad requires opening that project in Xcode on a Mac.

## Always do
- Read `GAME.md` before changing gameplay behavior.
- Work on one feature-sized task at a time; target 30–90 minutes of implementation.
- Inspect only the files relevant to the task. Do not scan generated Unity folders such as `Library/`.
- Preserve existing user changes. Do not reset, revert, or reformat unrelated files.
- Keep gameplay calculation independent of UI and `MonoBehaviour` where possible so it can be EditMode tested.
- Add or update an EditMode test whenever changing time, status, growth, save, or offline-progress logic.
- Use `double` or `DateTimeOffset` for elapsed-time calculations; clamp all player-facing values.
- Report changed files, test command, result, and any remaining risk in four short bullets.

## Scope limits for this prototype
- One pet only; no networking, ads, accounts, push notifications, breeding, or purchases.
- Do not add a water shader, procedural animation system, or third-party package without explicit approval.
- Keep all tuning values in data assets or dedicated configuration classes, never scattered magic numbers.

## Expected Unity layout after project creation
- `Assets/Scripts/Core/` — time, save, state, and offline progress
- `Assets/Scripts/Gameplay/` — care actions, status, and growth
- `Assets/Scripts/UI/` — views and input adapters only
- `Assets/Data/` — ScriptableObject tuning data
- `Assets/Tests/EditMode/` — deterministic logic tests
- `Assets/Tests/PlayMode/` — minimal scene integration tests

## Common commands
- Verify Unity tests: `powershell -ExecutionPolicy Bypass -File scripts/Run-UnityTests.ps1 -UnityPath 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.3f1\\Editor\\Unity.exe'`
- iOS build (produces an Xcode project only; build/sign/install still needs Xcode on a Mac): `powershell -ExecutionPolicy Bypass -File scripts/Build-iOS.ps1 -UnityPath 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.3f1\\Editor\\Unity.exe'`
- Check repository state: `git status --short`

## Token-efficient working agreement
- Before implementation, restate the single requested behavior and acceptance criteria in no more than five lines.
- Prefer targeted file paths and `rg` searches over reading whole directories.
- Do not mix feature work, refactoring, and visual polish in one task.
- When an error occurs, collect the smallest relevant log excerpt, form one hypothesis, make one change, then rerun the narrowest test.
- Put reusable, occasional workflows in `.claude/skills/`; keep this file concise because it is loaded every session.
