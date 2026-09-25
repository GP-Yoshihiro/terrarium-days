---
name: unity-feature
description: Implement one small Unity feature for Terrarium Days with explicit scope and a verifiable acceptance criterion.
---

# Unity feature workflow

Use this workflow only for one bounded behavior. Read `GAME.md`, then inspect only the source files that implement or call that behavior.

1. State the behavior, target files, and acceptance criteria in at most five lines.
2. If the request combines unrelated work, split it into separately verifiable tasks before editing.
3. Keep game rules in plain C# services or models. UI and `MonoBehaviour` components may call those services but must not own elapsed-time calculations.
4. Put tunable values in a dedicated data/configuration object rather than scattering constants.
5. Add/update the narrowest useful EditMode test for gameplay logic.
6. Run the smallest relevant test command. Do not launch a full iOS device build unless requested or required by the acceptance criteria (see unity-verify).
7. Finish with four bullets: changed files, behavior verified, test command/result, remaining risk.

Never add packages, networking, monetization, notifications, or a second pet without an explicit request.
