---
name: unity-verify
description: Verify Terrarium Days through the narrowest Unity CLI test or Android build and report concise evidence.
---

# Unity verification workflow

1. Identify whether the change is pure gameplay logic, scene integration, or Android build configuration.
2. Use EditMode tests for pure logic first. Use PlayMode only for scene integration. Use Android build only for release/readiness checks.
3. Run the repository script with the explicitly supplied Unity executable path. Do not guess an Editor installation path.
4. Read the generated log only until the first actionable failure or final summary.
5. Report the exact command, pass/fail result, and a minimal log location or excerpt.

If Unity is unavailable, do not fabricate a result. Report the missing prerequisite and preserve the code for later verification.
