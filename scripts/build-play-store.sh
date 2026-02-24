#!/bin/bash

# Build and optionally upload Famick Home Android app to Play Store Internal Testing
#
# Usage:
#   ./scripts/build-play-store.sh                    # Build signed AAB
#   ./scripts/build-play-store.sh --build-number 42  # Explicit build number
#   ./scripts/build-play-store.sh --verbose           # Show detailed build output
#
# Prerequisites:
#   1. Copy scripts/.env.example to scripts/.env and fill in Android values
#   2. Complete steps in docs/play-store-setup.md
#   3. .NET 10 SDK with MAUI Android workload installed
#   4. Java 17 (Homebrew: brew install openjdk@17)

set -e

REPO_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." && pwd )"
SCRIPT_DIR="$REPO_ROOT/scripts"
MOBILE_PROJECT="$REPO_ROOT/src/Famick.HomeManagement.Mobile/Famick.HomeManagement.Mobile.csproj"
BUILD_NUMBER_FILE="$REPO_ROOT/.build-number"
echo "Repository root: $REPO_ROOT"
echo "Script directory: $SCRIPT_DIR"
echo "Mobile project: $MOBILE_PROJECT"
echo "Build number file: $BUILD_NUMBER_FILE"

# Defaults
VERBOSE=false
BUILD_NUMBER=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --build-number)
            BUILD_NUMBER="$2"
            shift 2
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --build-number N   Set explicit build number (auto-increments if omitted)"
            echo "  --verbose          Show detailed build output"
            echo "  --help, -h         Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Run '$0 --help' for usage"
            exit 1
            ;;
    esac
done

# Output control
if [ "$VERBOSE" = true ]; then
    BUILD_OUTPUT="/dev/stdout"
else
    BUILD_OUTPUT="/dev/null"
fi

# Load environment
ENV_FILE="$SCRIPT_DIR/.env"
if [ ! -f "$ENV_FILE" ]; then
    echo "Error: $ENV_FILE not found"
    echo "Copy scripts/.env.example to scripts/.env and fill in your values."
    exit 1
fi
# shellcheck source=/dev/null
source "$ENV_FILE"

# Validate required environment variables
MISSING=()
[ -z "$ANDROID_KEYSTORE_PATH" ] && MISSING+=("ANDROID_KEYSTORE_PATH")
[ -z "$ANDROID_KEY_ALIAS" ] && MISSING+=("ANDROID_KEY_ALIAS")
[ -z "$ANDROID_KEYSTORE_PASSWORD" ] && MISSING+=("ANDROID_KEYSTORE_PASSWORD")
[ -z "$ANDROID_KEY_PASSWORD" ] && MISSING+=("ANDROID_KEY_PASSWORD")

if [ ${#MISSING[@]} -gt 0 ]; then
    echo "Error: Missing required environment variables in scripts/.env:"
    for var in "${MISSING[@]}"; do
        echo "  - $var"
    done
    exit 1
fi

# Validate prerequisites
echo "=== Validating prerequisites ==="

if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet SDK not found. Install .NET 10 SDK."
    exit 1
fi

if ! command -v java &> /dev/null; then
    echo "Error: Java not found. Install Java 17: brew install openjdk@17"
    exit 1
fi

# Verify keystore exists
if [ ! -f "$ANDROID_KEYSTORE_PATH" ]; then
    echo "Error: Keystore not found at: $ANDROID_KEYSTORE_PATH"
    echo "Generate one with: keytool -genkey -v -keystore famick-upload.keystore -alias famick -keyalg RSA -keysize 2048 -validity 10000"
    exit 1
fi

echo "  dotnet: $(dotnet --version)"
echo "  Java: $(java -version 2>&1 | head -1)"
echo "  Keystore: $ANDROID_KEYSTORE_PATH"

# Determine build number
if [ -z "$BUILD_NUMBER" ]; then
    if [ -f "$BUILD_NUMBER_FILE" ]; then
        BUILD_NUMBER=$(cat "$BUILD_NUMBER_FILE")
        BUILD_NUMBER=$((BUILD_NUMBER + 1))
    else
        BUILD_NUMBER=1
    fi
fi
echo "  Build number: $BUILD_NUMBER"

# Build MAUI Android app
echo ""
echo "=== Building Famick Home Android app ==="
echo "  Configuration: Release"
echo "  Target: net10.0-android"

dotnet publish "$MOBILE_PROJECT" \
    -f net10.0-android \
    -c Release \
    /p:AndroidKeyStore=true \
    /p:AndroidSigningKeyStore="$ANDROID_KEYSTORE_PATH" \
    /p:AndroidSigningKeyAlias="$ANDROID_KEY_ALIAS" \
    /p:AndroidSigningStorePass="$ANDROID_KEYSTORE_PASSWORD" \
    /p:AndroidSigningKeyPass="$ANDROID_KEY_PASSWORD" \
    /p:ApplicationVersion="$BUILD_NUMBER" \
    > "$BUILD_OUTPUT" 2>&1

# Find the AAB
AAB_PATH=$(find "$REPO_ROOT/src/Famick.HomeManagement.Mobile" -name "*-Signed.aab" -path "*/publish/*" -type f 2>/dev/null | head -1)

if [ -z "$AAB_PATH" ]; then
    echo "Error: No .aab file found in publish output."
    echo "Check the build output with --verbose for details."
    exit 1
fi

echo "  AAB: $AAB_PATH"
echo "  Size: $(du -h "$AAB_PATH" | cut -f1)"

# Save build number
echo "$BUILD_NUMBER" > "$BUILD_NUMBER_FILE"

echo ""
echo "=== Done ==="
echo "  Build number: $BUILD_NUMBER"
echo "  AAB: $AAB_PATH"
echo ""
echo "To upload manually, go to Google Play Console:"
echo "  1. Open https://play.google.com/console"
echo "  2. Select Famick Home > Internal testing > Create new release"
echo "  3. Upload the AAB file above"
echo "  4. Review and roll out"
