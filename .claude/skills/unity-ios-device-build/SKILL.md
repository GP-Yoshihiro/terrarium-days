---
name: unity-ios-device-build
description: Use when a Unity-exported Xcode project fails to build, install or launch on a physical iPhone from a Mac — xcodebuild "requires Xcode" / CommandLineTools, "Sandbox: il2cpp deny", UnityFramework umbrella-header or module errors, "Device is busy (Waiting to reconnect)", "not explicitly trusted", codesign hanging, or Unity Hub module install failures.
---

# Unity → iPhone device build (macOS, Xcode 26)

## Symptom → cause → fix
| Symptom | Cause | Fix (no sudo needed) |
|---|---|---|
| `xcodebuild requires Xcode … CommandLineTools` | xcode-select points at CLT | Prefix commands with `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer` (per-process; `sudo xcode-select -s` is the user's call) |
| `Sandbox: il2cpp deny(1) file-read-data …libhostfxr.dylib` | User Script Sandboxing on (Xcode "recommended settings") | `ENABLE_USER_SCRIPT_SANDBOXING = NO` |
| `umbrella header … does not include header`, `double-quoted include in framework header`, VerifyModule `could not build module` | Module Verifier on — Unity headers aren't modular. **Not** a stale-cache issue | `ENABLE_MODULE_VERIFIER = NO` |
| Device listed but `Device is busy (Waiting to reconnect)`; `devicectl … developerModeStatus: disabled`, `ddiServicesAvailable: false` | **Developer Mode off** (not a pairing problem) | iPhone: Settings → Privacy & Security → Developer Mode → on, reboot, confirm |
| Install OK, launch: `not explicitly trusted by the user` | Free/dev cert not trusted | iPhone: Settings → General → VPN & Device Management → trust the developer |
| CLI build stuck at `/usr/bin/codesign` | Keychain access prompt waiting on the Mac screen | User enters login password, "Always Allow" (never type it for them) |
| Unity Hub `ERROR_NOT_ENOUGH_SPACE_TO_DOWNLOAD` | iOS module ≈3 GB + import ≈2 GB + IL2CPP build ≈3 GB | Check `df -h /System/Volumes/Data`; leftover installers in `~/Library/Application Support/UnityHub/downloads` |

## Make fixes survive re-export
- `Assets/Editor/…PostProcess.cs` with `[PostProcessBuild]` + `UnityEditor.iOS.Xcode.PBXProject`: set both flags above on project, main and framework targets. Wrap in `#if UNITY_IOS`.
- Player Settings: `appleDeveloperTeamID`, `appleEnableAutomaticSigning: 1`, and `locationUsageDescription` etc. for any permission used.

## Diagnose fast
```bash
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
xcrun devicectl list devices
xcrun devicectl device info details --device <id> | grep -E "developerMode|ddiServices"
xcodebuild -project <p> -scheme Unity-iPhone -showdestinations
grep -E "error:|BUILD (SUCCEEDED|FAILED)" build.log | sort -u     # never read the whole log
```
Build with `-destination 'generic/platform=iOS'` to separate compile problems from device problems.

## Install / launch without Xcode UI
```bash
xcrun devicectl device install app --device <id> <path>/TerrariumDays.app
xcrun devicectl device process launch --terminate-existing --device <id> <bundle-id>
```
In this repo `scripts/build-ios.sh --run` does export → build → install → launch.

## Common mistakes
- Accepting Xcode's "Update to recommended settings" — silently re-enables both flags.
- Running a device build while the phone is locked or Developer Mode is off and concluding "Xcode is broken".
- A simulator can't run a Device-SDK export; re-export with the Simulator SDK if needed.
