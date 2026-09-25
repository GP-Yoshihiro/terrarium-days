# Terrarium Days — Claude Code project guide

## 絶対遵守：応答は日本語
- ユーザーへの応答は、最終報告だけでなく途中経過・ツール実行の合間の一文・見出し・表・質問（AskUserQuestion の選択肢を含む）まですべて日本語で書くこと。例外なし。
- コード、コードコメント、コミットメッセージ、スキル本文（`.claude/skills/`）は既存の書き方（英語）に合わせてよい。

## Project goal
Create an offline-first iOS (iPhone/iPad) portrait prototype in Unity: a leopard-gecko breeder simulation: care, breeding, incubation and event sales across multiple cages. The playable scope and acceptance criteria are in `GAME.md`.

## Current stage
This repository is a Unity 6 project on **6000.5.10f1** (upgraded from 6000.5.3f1 on 2026-09-24), developed on **macOS** with Xcode 26. iOS Build Support and a Unity Personal license are active. Keep this Editor version fixed for the prototype unless an explicit upgrade is requested. The executable is `/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity` (override with `UNITY_PATH`). The target platform is iOS only; Android support was removed on 2026-09-24.

iOS build notes:
- `Assets/Editor/IosXcodePostProcess.cs` sets `ENABLE_USER_SCRIPT_SANDBOXING = NO` (IL2CPP's run script is blocked otherwise) and `ENABLE_MODULE_VERIFIER = NO` (Unity framework headers fail verification) on every export. Do not accept Xcode's "Update to recommended settings", which re-enables both.
- Signing: automatic, team `JCS2DTL738`, bundle id `com.terrariumdays.prototype` (set in Player Settings).
- The iPhone must have Developer Mode on and trust the developer certificate (Settings → General → VPN & Device Management) before an install can launch.

## Always do
- Read `GAME.md` before changing gameplay behavior.
- Work on one feature-sized task at a time; target 30–90 minutes of implementation.
- Inspect only the files relevant to the task. Do not scan generated Unity folders such as `Library/`.
- Preserve existing user changes. Do not reset, revert, or reformat unrelated files.
- Keep gameplay calculation independent of UI and `MonoBehaviour` where possible so it can be EditMode tested.
- Add or update an EditMode test whenever changing time, status, growth, save, or offline-progress logic.
- Use `double` or `DateTimeOffset` for elapsed-time calculations; clamp all player-facing values.
- Report changed files, test command, result, and any remaining risk in four short bullets.

## Terrarium coordinates
- Everything in the terrarium is placed by `Core/TerrariumProjection.cs` from art measurements in `Core/TerrariumArtLayout.cs`: the background itself, the floor trapezoid, and each sprite's ground contact line (the base of its outline, not its drop shadow).
- Floor positions are `X` 0–1 (left→right) and `Depth` 0–1 (back→front). Do not position pet/decor with USS; add or edit entries in `TerrariumArtLayout` instead.

## Pet life model
- Daily rhythm follows the device's local clock (`Core/DayPhase.cs`): leopard geckos sleep long by day (next to floor decor, their hide) and are active at dusk/night. Tuning lives in `Core/PetBehaviourTuning.cs`.
- Growth is by body weight and age (grams / game months, `Core/CareTuning.cs`), not a growth gauge. Food refusal (拒食) and shedding (脱皮) are time-driven and deterministic (`Core/AppetiteModel.cs`, `Core/OfflineProgressCalculator.cs`): once a stage's weight/age target is met the pet fasts for `PreGrowthFastGameDays` before the stage-up lands; a shed refuses food for `PreShedGameDays` beforehand and recurs every `YoungShedIntervalGameDays` (baby/juvenile) or `AdultShedIntervalGameDays` (adult), and on every stage-up. Game time runs at 1 real day = 1 game month. While fasting, hunger falls slower and does not count against health or growth. Sex is hidden until the animal is juvenile-or-older *and* has shed since (`PetState.SexRevealed`, set in `Core/OfflineProgressCalculator.cs`), not simply on reaching that stage.

## Morph appearance
- `Resources/Gecko` frames are recoloured at runtime by palette (`Core/MorphAppearance.cs`, `UI/MorphRecolor.cs`), so they must stay imported with Read/Write enabled (`isReadable: 1`) and their base colours must match `MorphAppearance.Normal` exactly — otherwise the runtime recolour and `MorphRecolorTests.GeckoSprites_UseOnlyPaletteColours` drift apart. Pre-shed colouring is derived at runtime (`MorphAppearance.PreShed`); `Resources/GeckoPreShed` was removed and `tools/sprites/gecko_sprites.py` no longer writes pre-shed images.

## Scope limits for this prototype
- Multiple animals across multiple cages, breeding, incubation, money, and shop purchases are in scope. Still no ads, accounts, push notifications, or real-money purchases. Adult animals never die (eggs can fail).
- Networking: the only allowed use is the optional current-location weather (`UI/WeatherService.cs`, Open-Meteo, no API key, coordinates rounded to ~1 km), approved 2026-09-24. Everything else must work offline; never make gameplay depend on the network.
- Do not add a water shader, procedural animation system, or third-party package without explicit approval. The pet's sprite-frame animation (idle, walk, eat, sleep, yawn, threat, happy + hearts/Zzz) was approved on 2026-09-24: behaviour lives in `Gameplay/PetBehaviour.cs`, rendering in `UI/PetActor.cs`, tuning in `Core/PetBehaviourTuning.cs`; extend these rather than adding another animation system.
- Keep all tuning values in data assets or dedicated configuration classes, never scattered magic numbers.

## Expected Unity layout after project creation
- `Assets/Scripts/Core/` — time, save, state, and offline progress
- `Assets/Scripts/Core/Colony*.cs` — the multi-cage colony (cages, animals, wallet), its session/save service, and offline progress across the whole colony
- `Assets/Scripts/Core/GameCalendar.cs` — the in-game calendar (real 1 day = 1 in-game month) used for age and growth display
- `Assets/Scripts/Gameplay/` — care actions, status, and growth
- `Assets/Scripts/UI/` — views and input adapters only
- `Assets/Data/` — ScriptableObject tuning data
- `Assets/Tests/EditMode/` — deterministic logic tests
- `Assets/Tests/PlayMode/` — minimal scene integration tests

## Common commands (macOS)
- Verify Unity tests: `scripts/run-unity-tests.sh` (or `scripts/run-unity-tests.sh EditMode`); results in `Logs/*-results.xml`
- Summarise results/logs (totals + failures only — use this instead of reading XML/logs): `scripts/test-summary.py`
- iOS build → Xcode build: `scripts/build-ios.sh`
- iOS build → install and launch on the connected iPhone: `scripts/build-ios.sh --run`
- Screenshot the real scene at iPhone 15 Pro size (idle / happy / eat / threat) into `Logs/Screens/`: `scripts/capture-screens.sh` — use it to check layout and art before a device build.
- Regenerate the gecko animation frames and heart/Zzz effects into `Assets/Resources/`: `python3 tools/sprites/gecko_sprites.py` (then re-measure `TerrariumArtLayout.Pet` if the ground line moves).
- Check repository state: `git status --short`
- `scripts/*.ps1` are the older Windows entry points and are no longer maintained.

## Token-efficient working agreement
- Before implementation, restate the single requested behavior and acceptance criteria in no more than five lines.
- Prefer targeted file paths and `rg` searches over reading whole directories.
- Do not mix feature work, refactoring, and visual polish in one task.
- When an error occurs, collect the smallest relevant log excerpt, form one hypothesis, make one change, then rerun the narrowest test.
- Put reusable, occasional workflows in `.claude/skills/`; keep this file concise because it is loaded every session.
