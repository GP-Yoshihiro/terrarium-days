# Phase 7: Quality Improvement Design

## 1. Purpose

The player-facing feature set through event sales is implemented. Phase 7 improves the reliability and maintainability of that existing game: visual presentation and UI, functional correctness, runtime performance, bug-prone code, and crash resistance. Work should be driven by reproducible defects or measurements rather than speculative rewrites.

## 2. Goals

- Find and fix objectively broken or inconsistent image and UI presentation across the supported iOS layouts.
- Reduce maintenance risk in high-complexity code while preserving existing behavior and save compatibility.
- Identify runtime performance costs, then optimize only measured bottlenecks.
- Exercise high-risk flows for crashes and state corruption, including save/load, app pause/resume, navigation, and purchases/breeding/incubation/events.
- Keep every discovered issue reproducible through an automated test where practical.

## 3. Non-goals

- No new gameplay systems, economy balancing, or content additions.
- No subjective art or layout redesign without a concrete issue or user approval.
- No broad rewrite or optimization without before/after evidence.
- No changes to save schema unless a verified defect requires one.

## 4. Constraints

- Unity 6 editor version remains 6000.5.10f1; iOS remains the target platform. Physical verification uses the connected iPhone 15 Pro; iPad layout is included in automated captures if the existing test setup supports it, but no physical iPad is assumed.
- Preserve the offline-first model and current game rules in `GAME.md`.
- Work one issue-sized change at a time; add regression coverage before fixing behavior.
- Never modify or delete the pre-existing untracked handoff file under `outputs/`.
- Baseline after the latest phase: EditMode 755 passed; PlayMode 131 passed / 17 skipped; captures 17 passed. Test counts must not decrease.

## 5. Approach

1. Establish a reproducible quality baseline: full test suite, iOS build/launch, captures for home, cage detail, shop, incubator, and all event surfaces, and a focused inspection of render/update and asset-loading paths.
2. Rank findings by player impact, crash risk, frequency, and evidence. Separate visual defects, correctness defects, measured performance issues, and structural maintenance risks.
3. Implement the highest-value confirmed finding first, with a regression test or a measurable before/after check. Keep each change narrowly scoped and reviewable.
4. Crash/state smoke coverage must exercise: normal and legacy save load; malformed-save recovery; repeated navigation through all tabs with modal open/close; pause/resume; and save/reload after purchase, pairing, incubation/hatching, and event actions. Assert no unexpected exception/error log and that key persisted values (wallet, cages, animals, event progress) remain consistent.
5. Repeat the audit on remaining findings only after the current change passes its focused tests and review.
6. Finish with the full suite, screen-capture comparison, iOS build/launch, and a concise list of remaining risks.

## 6. Acceptance criteria

- The smoke scenarios in §5 complete without an unexpected exception/error log or inconsistent persisted state.
- UI and image changes fix a demonstrated issue and pass the relevant capture/device check; no unapproved aesthetic redesign is introduced.
- Any performance change includes a baseline and a comparable post-change measurement; otherwise the code is left unchanged.
- EditMode, PlayMode, and capture results have zero failures and no fewer tests than the phase-7 baseline.
- The iOS build installs and launches without a new startup exception or error.

## 7. Design decisions

- This is an evidence-led hardening phase, not an open-ended cleanup: findings must name the affected flow and verification method.
- Refactoring is allowed when it lowers a demonstrated maintenance risk and has behavior-preserving tests; it does not justify changing gameplay or UI semantics.
- The initial baseline uses the current device and repository test setup. Performance thresholds are set only after measuring the current build.
