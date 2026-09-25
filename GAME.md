# Terrarium Days

## 1. One-line pitch
スマホ縦画面で、ヒョウモントカゲモドキのブリーダーとして飼育・繁殖・孵化・イベント販売を回すシミュレーション。

## 2. Prototype goal
- iOS(iPhone/iPad)で起動し、世話・放置進行・成長・セーブ／ロードを一通り体験できる。
- 一人のプレイヤーが、数分の操作でゲームの全循環を理解できる。
- 開発者は時間倍率を変え、実時間を待たずに全ての成長段階と放置計算を検証できる。

## 3. Audience and platform
- Platform: iOS (iPhone, iPad)
- Orientation: portrait
- Visual style: 2D, warm terrarium, free or placeholder assets only
- Input: tap only
- Online requirement: none

## 4. Core loop
1. アプリを開き、ヤモリと5つの状態値を確認する。
2. 餌やり、水の交換、掃除のいずれかをタップして世話する。
3. 状態が良い時間ほど健康と成長が進む。
4. アプリを閉じている間も状態は変化する。
5. 成長段階または装飾を解放し、成体到達を目指す。

## 5. Player actions
| Action | Immediate effect | Constraint |
| --- | --- | --- |
| Feed | Hunger increases | Cannot exceed maximum hunger |
| Refresh water | Hydration increases | Cannot exceed maximum hydration |
| Clean | Cleanliness increases | Cannot exceed maximum cleanliness |
| Select decor | Changes only the terrarium appearance | Unlocked items only |

## 6. State model
All status values are clamped to 0–100.

| State | Meaning | Starts at |
| --- | --- | ---: |
| Hunger | 100 = full, 0 = starving | 80 |
| Hydration | 100 = hydrated, 0 = dehydrated | 80 |
| Cleanliness | 100 = clean habitat | 80 |
| Health | Overall condition calculated from care quality | 100 |
| Weight | Body weight in grams; growth stage advances by weight and age, not a gauge | 3 g |

Growth stages: `Baby` → `Juvenile` → `Adult` (Adult at 40 g and 10 game months; exact thresholds live in the tuning data asset).

Exact decay rates, action amounts, and stage thresholds must live in one tuning data asset. Do not hardcode them in UI code.

## 7. Time and offline progress
- Store `lastSavedAtUtc` on every state-changing action and on application pause/quit.
- On launch/resume, calculate elapsed real time using UTC.
- Apply status decay and growth from elapsed time in a deterministic, UI-independent calculator.
- Cap a single offline calculation at 12 hours for the prototype, and display the applied elapsed time in the debug panel.
- Healthy condition: hunger, hydration, and cleanliness are all at least 40. Only then does growth advance at its normal rate.
- Low condition: if any care status is below 20, health decreases. Health never goes below 0.
- Default player pacing: Adult is reachable after seven healthy real-world days.
- Development pacing: a debug-only time multiplier supports 1×, 60×, and 600×.

## 8. MVP screens
1. **Terrarium** — pet, status bars, three care buttons, growth stage, selected decor.
2. **Care feedback** — a small text/animation confirmation after an action.
3. **Decor drawer** — select from up to five unlocked placeholder decorations.
4. **Milestone modal** — shown on each growth-stage change and at Adult completion.
5. **Developer panel** — time multiplier, simulate elapsed time, edit status values, clear save.

## 9. MVP feature checklist
- [ ] One 2D terrarium scene and one pet placeholder sprite
- [ ] Tap actions: feed, refresh water, clean
- [ ] Status display and clamped state updates
- [ ] Deterministic time-decay and offline-progress calculator
- [ ] JSON save/load to `Application.persistentDataPath`
- [ ] Growth stages and milestone modal
- [ ] Five placeholder decorations and simple unlock rules
- [ ] Debug time controls
- [ ] EditMode tests for offline progress and save round-trip
- [ ] iOS development build runs on a device

## 10. Explicitly out of scope
- Diseases, death of adult animals, combat, achievements, cloud saves, login, ads, IAP, notifications, social features, localization, and analytics.

## 11. Definition of done
- A new player can perform all three care actions and see their effect.
- Closing and reopening the app applies capped offline progress correctly.
- At 600× debug speed, the pet can reach Adult (40 g, 10 game months) without errors.
- EditMode tests pass from Unity CLI.
- An iOS development build installs and reaches the Terrarium screen.

Detailed spec: docs/superpowers/specs/2026-09-25-breeder-sim-design.md
