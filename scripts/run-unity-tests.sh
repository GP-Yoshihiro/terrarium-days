#!/usr/bin/env bash
# Runs the Unity EditMode and/or PlayMode tests from the CLI on macOS.
# usage: scripts/run-unity-tests.sh [EditMode|PlayMode|All]   (default: All)
set -euo pipefail

UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
PLATFORMS="${1:-All}"
[ "$PLATFORMS" = "All" ] && PLATFORMS="EditMode PlayMode"

mkdir -p "$PROJECT/Logs"
status=0
for platform in $PLATFORMS; do
    results="$PROJECT/Logs/$platform-results.xml"
    rm -f "$results"
    "$UNITY" -batchmode -projectPath "$PROJECT" -buildTarget iOS -runTests -testPlatform "$platform" \
        -testResults "$results" -logFile "$PROJECT/Logs/$platform.log" || status=$?
    if [ -f "$results" ]; then
        echo "$platform: $(grep -o '<test-run[^>]*>' "$results" | grep -oE '(total|passed|failed)="[0-9]+"' | tr '\n' ' ')"
    else
        echo "$platform: no results (see Logs/$platform.log)"
        status=1
    fi
done
exit "$status"
