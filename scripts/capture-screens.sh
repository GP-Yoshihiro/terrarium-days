#!/usr/bin/env bash
# Renders the Terrarium scene at iPhone 15 Pro resolution into Logs/Screens/*.png
# (idle, happy with hearts, eating, threat) for checking layout without a device.
set -euo pipefail

UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"

mkdir -p "$PROJECT/Logs/Screens"
"$UNITY" -batchmode -projectPath "$PROJECT" -buildTarget iOS -runTests -testPlatform PlayMode \
    -testFilter TerrariumDays.Tests.ScreenCaptureTests \
    -testResults "$PROJECT/Logs/capture-results.xml" -logFile "$PROJECT/Logs/capture.log"
ls -1 "$PROJECT/Logs/Screens"
