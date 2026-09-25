---
name: unity-verify
description: Verify Terrarium Days through the narrowest Unity CLI test, a render capture, or an iPhone device build, and report concise evidence.
---

# Unity verification workflow (macOS / iOS)

1. Classify the change: pure gameplay logic, scene/UI integration, visual layout/art, or iOS build configuration.
2. Pick the narrowest check:
   - Logic → `scripts/run-unity-tests.sh EditMode`
   - Scene/UI wiring → `scripts/run-unity-tests.sh PlayMode`
   - Visual layout, art, draw order → `scripts/capture-screens.sh`, then view cropped PNGs (REQUIRED: verifying-ui-with-render-captures)
   - Device-only behaviour (permissions, network, safe area) → `scripts/build-ios.sh --run` (REQUIRED on failure: unity-ios-device-build)
3. Read results with `scripts/test-summary.py` (totals + failures only). Never read the raw XML/log unless the summary points at something it cannot show.
4. Run builds and test runs in the background; wait for the completion notice instead of polling.
5. Report the exact command, pass/fail counts, and what was *not* verified (e.g. "weather only verifiable on device").

If Unity is unavailable, do not fabricate a result. Report the missing prerequisite and keep the code for later verification.
