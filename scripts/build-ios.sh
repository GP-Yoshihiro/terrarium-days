#!/usr/bin/env bash
# Exports the Xcode project with Unity, builds it with Xcode, and (optionally) installs
# and launches it on a connected iPhone.
# usage: scripts/build-ios.sh [--run [DEVICE]]
#   DEVICE is a devicectl identifier or name; defaults to the first connected device.
set -euo pipefail

UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity}"
export DEVELOPER_DIR="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$PROJECT/Builds/iOS/TerrariumDays"
DERIVED="$PROJECT/Builds/iOS/DerivedData"
BUNDLE_ID="com.terrariumdays.prototype"

mkdir -p "$PROJECT/Logs"
echo "==> Unity: exporting Xcode project"
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -buildTarget iOS \
    -executeMethod TerrariumDays.Editor.BuildAutomation.BuildIosDevelopment \
    -buildOutput "$OUT" -logFile "$PROJECT/Logs/ios-build.log"

echo "==> Xcode: building for device"
xcodebuild -project "$OUT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Debug \
    -destination 'generic/platform=iOS' -derivedDataPath "$DERIVED" -allowProvisioningUpdates \
    build > "$PROJECT/Logs/xcodebuild.log" 2>&1 \
    || { grep -E "error:" "$PROJECT/Logs/xcodebuild.log" | sort -u | head -20; exit 1; }
APP="$DERIVED/Build/Products/Debug-iphoneos/TerrariumDays.app"
echo "Built: $APP"

if [ "${1:-}" = "--run" ]; then
    DEVICE="${2:-$(xcrun devicectl list devices 2>/dev/null | awk '/connected/ {print $3; exit}')}"
    [ -n "$DEVICE" ] || { echo "No connected device found (enable Developer Mode and unlock it)." >&2; exit 1; }
    echo "==> Installing and launching on $DEVICE"
    xcrun devicectl device install app --device "$DEVICE" "$APP"
    xcrun devicectl device process launch --terminate-existing --device "$DEVICE" "$BUNDLE_ID"
fi
