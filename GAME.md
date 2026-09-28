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
5. 成長段階を進め、成体到達を目指す。

## 5. Player actions
| Action | Immediate effect | Constraint |
| --- | --- | --- |
| Feed | Hunger increases | Cannot exceed maximum hunger |
| Refresh water | Hydration increases | Cannot exceed maximum hydration |
| Clean | Cleanliness increases | Cannot exceed maximum cleanliness |
| Select decor | Changes only the terrarium appearance | Owned (unplaced) items only, within that cage's decor slots |

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
- Debug fast-forward is saved as a game-clock offset (`Colony.GameClockOffset`): game-now = real-now + offset, and closing/reopening the app or reloading a save never rewinds the in-game date. Only "clear save" resets the offset.

## 8. MVP screens
1. **Terrarium** — pet, status bars, three care buttons, growth stage, selected decor.
2. **Care feedback** — a small text/animation confirmation after an action.
3. **Decor drawer** — lists every decor item, owned or not (an unowned item shows as "未所持（ショップで購入）"), limited by that cage's decor slots (see §13).
4. **Milestone modal** — shown on each growth-stage change and at Adult completion.
5. **Developer panel** — time multiplier, simulate elapsed time, edit status values, clear save.
6. **Shop** — buy animals and supplies, wholesale animals, priced from the market (see §13).
7. **Ledger** — every animal at a glance and every money movement (see §13).

## 9. MVP feature checklist
- [ ] One 2D terrarium scene and one pet placeholder sprite
- [ ] Tap actions: feed, refresh water, clean
- [ ] Status display and clamped state updates
- [ ] Deterministic time-decay and offline-progress calculator
- [ ] JSON save/load to `Application.persistentDataPath`
- [ ] Growth stages and milestone modal
- [ ] Placeholder decorations, bought in the shop and placed per cage (see §13)
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

## 12. Genetics, morphs, and personality (phase 2)
- **Genetics**: 8 single-locus genes (the 3 albino strains are separate loci; mack snow is codominant — 2 copies is super snow; white & yellow is dominant) plus Hypo and Tangerine as polygenic 0–100 values (child = parents' mean ± random spread, clamped).
- **Morph names**: visible genes are listed by name, with trade names for known combinations (Tremper albino + Blizzard = ブレイジングブリザード, Tremper albino + Eclipse = レイプター); Hypo/Tangerine are named once high enough (Hypo ≥ 70, Tangerine ≥ 60).
- **Possible hets**: the true genotype stays hidden. The player only sees what an animal's known information implies — proven hets, and possible-het percentages (66% for het × het, 50% for het × normal) for a child whose visible morph doesn't rule the gene out.
- **Personality**: one of 5 per animal, each with its own threat/walk/hide/fasting/weight-gain/price multipliers and a male×female compatibility table; a hatchling has a 20% chance each of taking a parent's personality, otherwise random. Animals hatched by the player know it from birth. Shop-bought animals revealing personality after purchase is planned but not yet implemented (段階6 ショップ) — today a bought animal's `PersonalityKnown` flag just stays as saved.
- **Sex reveal**: a self-raised animal's sex is revealed the moment it becomes Young or older, since the stage-up always includes a shed. An animal that was already Young+ with unknown sex when loaded (e.g. migrated from an older save) instead reveals at its next shed thereafter.
- **Visual differentiation**: the base sprite is recoloured at runtime from each morph's palette (colour swap, pattern removal, eye colour); recoloured frames are cached per palette (including the Tangerine blend, quantised to steps of 0.1), not per morph name alone.

## 13. Economy, shop, and per-cage decor (phase 3)
- **Money and ledger**: the colony has one wallet (yen) and every movement — feed cost, electricity, a shop purchase, an animal sale, a wholesale payout — is recorded as a ledger entry (date, category, amount, note) for the ledger screen. A bill such as electricity is charged even into the red; a purchase only succeeds if affordable.
- **Market price (相場)**: an animal's reference price is its morph's base price (most expensive named element, ×1.3 per additional element) × a het bonus (proven hets, and possible hets scaled by their probability) × a polygenic bonus for high hypo/tangerine × a growth-stage multiplier × a female multiplier once sex is known × a personality multiplier once known, rounded to the nearest 100 yen. An event demand factor (0.8–1.3, boosted at big events for pricier morphs) multiplies this outside the shop; the shop and wholesale below use no event demand yet.
- **Shop — stock and pricing**: the shop restocks 4–6 animals once per game month, deterministically from a seed (same seed and month always gives the same stock), mostly babies with a few juveniles, skewed toward common morphs; proven and possible hets are disclosed truthfully (at most two per animal). An animal sells for the market price × a shop markup; buying needs an empty cage and moves the animal into the colony.
- **Shop — personality reveal**: a bought animal's personality is hidden at purchase and becomes known automatically some days later, without further action.
- **Supplies**: the shop also sells cages (small/standard/large), racks (raise the cage limit, capped), decor, nest boxes, and incubators. Nest boxes and incubators are priced now but only usable once breeding and incubation are implemented (later phases).
- **Wholesale**: any owned animal can be sold back to the shop for the market price × a wholesale rate (well below the shop's selling price); this is allowed even for the colony's last animal.
- **Per-cage decor and slots**: decor is bought into a shared inventory, then placed into a specific cage's decor slots (small 1, standard 2, large 3; at most one of each item per cage) from that cage's own decor drawer. Removing a placed item returns it to the inventory.
- **Decor migration**: a save from before per-cage decor existed has its previously "unlocked" decor moved into the shared inventory on first load, with an on-screen notice; the player then places it from a cage's decor drawer.

## 14. Breeding, eggs, and weakness (phase 4)
- **Breeding season and conditions**: the breeding season is game months 3–9. A female can pair once she is 10 game months old and at least 45 g; a male, 8 months and 40 g.
- **Pairing**: started from the cage detail, the new breeding tab, or the ledger. The female visits the male's cage for 3 game days — her own cage shows empty, his shows a "visiting" marker — then the pairing resolves as success or failure; the forecast screen shows the chance beforehand (base 70%, up to 95%, better with a good match).
- **Gravid state and egg-laying schedule**: a successful pairing goes gravid and lays its first clutch 21–28 game days later, then every 14–28 game days after that, 4–8 clutches over the season, 2 eggs per clutch (10% chance of just 1). Each clutch costs the female 3–5 g of body weight; laying stops once she is under 40 g.
- **Nest box and eggs**: an egg laid with a nest box in the cage develops at room temperature and hatches in phase 5. An egg laid without one dries out after 2 game days (a notice shows the remaining time); a nest-box egg that spends 3 total game days below 24 ℃ (the floor eggs need to develop, separate from the room-temperature range above) also fails.
- **Weakness**: health at 0 makes an animal weak — it cannot breed and its market price drops to 0.3× — until health recovers to 30, when a notice clears the state.
- **Room temperature**: the current-location weather's temperature, clamped to 18–30 ℃, feeds breeding and eggs. With no usable weather (location off, failed, or offline) it falls back to 24 ℃ and is labelled accordingly.

Detailed spec: docs/superpowers/specs/2026-09-25-breeder-sim-design.md
