#!/bin/bash
set -e
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
export ANDROID_HOME="$HOME/Library/Android/sdk"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
EMULATOR="$ANDROID_HOME/emulator/emulator"
AVD_NAME="mgrcode-avd"
ADB="$ANDROID_HOME/platform-tools/adb"

EMULATOR_PID=""
BOOTED=false

# Check if emulator is already running
if "$ADB" devices 2>/dev/null | grep -q "emulator.*device"; then
    echo "=== Emulator already running ==="
    BOOTED=true
fi

# Start emulator if not running
if [ "$BOOTED" = false ]; then
    echo "=== Starting emulator (cold boot, subsequent boots will use snapshot) ==="
    "$EMULATOR" -avd "$AVD_NAME" -no-boot-anim &
    EMULATOR_PID=$!

    echo "=== Waiting for emulator to boot ==="
    "$ADB" wait-for-device
    while true; do
        STATUS=$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')
        if [ "$STATUS" = "1" ]; then
            break
        fi
        sleep 2
    done
    echo "=== Boot complete ==="
fi

echo "=== Building and deploying ==="
if [ "$1" = "blazor" ]; then
    dotnet build "$SCRIPT_DIR/MgrCode.BlazorApp/MgrCode.BlazorApp.csproj" -f net10.0-android
    dotnet run --project "$SCRIPT_DIR/MgrCode.BlazorApp/MgrCode.BlazorApp.csproj" -f net10.0-android --no-build
else
    dotnet build "$SCRIPT_DIR/MgrCode.XamlApp/MgrCode.XamlApp.csproj" -f net10.0-android
    dotnet run --project "$SCRIPT_DIR/MgrCode.XamlApp/MgrCode.XamlApp.csproj" -f net10.0-android --no-build
fi

if [ -n "$EMULATOR_PID" ]; then
    wait $EMULATOR_PID
fi
