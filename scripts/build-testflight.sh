#!/bin/bash

# Build and upload Famick Home iOS app to TestFlight
#
# Usage:
#   ./scripts/build-testflight.sh                    # Build and upload
#   ./scripts/build-testflight.sh --skip-upload       # Build only
#   ./scripts/build-testflight.sh --skip-widget       # Skip widget extension build
#   ./scripts/build-testflight.sh --build-number 42   # Explicit build number
#   ./scripts/build-testflight.sh --verbose           # Show detailed build output
#
# Prerequisites:
#   1. Copy scripts/.env.example to scripts/.env and fill in values
#   2. Complete steps in docs/testflight-setup.md
#   3. .NET 10 SDK with MAUI iOS workload installed
#   4. Xcode with command-line tools

set -e

REPO_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." && pwd )"
SCRIPT_DIR="$REPO_ROOT/scripts"
MOBILE_PROJECT="$REPO_ROOT/src/Famick.HomeManagement.Mobile/Famick.HomeManagement.Mobile.csproj"
WIDGET_DIR="$REPO_ROOT/src/Famick.HomeManagement.Mobile/Platforms/iOS/WidgetExtension"
BUILD_NUMBER_FILE="$REPO_ROOT/.build-number"
echo "Repository root: $REPO_ROOT"
echo "Script directory: $SCRIPT_DIR"
echo "Mobile project: $MOBILE_PROJECT"
echo "Widget directory: $WIDGET_DIR"
echo "Build number file: $BUILD_NUMBER_FILE"

# Defaults
SKIP_UPLOAD=false
SKIP_WIDGET=false
VERBOSE=false
BUILD_NUMBER=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --build-number)
            BUILD_NUMBER="$2"
            shift 2
            ;;
        --skip-upload)
            SKIP_UPLOAD=true
            shift
            ;;
        --skip-widget)
            SKIP_WIDGET=true
            shift
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
            echo "  --skip-upload      Build only, don't upload to TestFlight"
            echo "  --skip-widget      Skip widget extension build"
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
[ -z "$APPLE_DISTRIBUTION_IDENTITY" ] && MISSING+=("APPLE_DISTRIBUTION_IDENTITY")
[ -z "$APP_PROVISIONING_PROFILE" ] && MISSING+=("APP_PROVISIONING_PROFILE")

if [ "$SKIP_UPLOAD" = false ]; then
    [ -z "$APP_STORE_CONNECT_KEY_ID" ] && MISSING+=("APP_STORE_CONNECT_KEY_ID")
    [ -z "$APP_STORE_CONNECT_ISSUER_ID" ] && MISSING+=("APP_STORE_CONNECT_ISSUER_ID")
fi

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

if ! command -v xcodebuild &> /dev/null; then
    echo "Error: Xcode command-line tools not found."
    exit 1
fi

# Verify signing identity exists
if ! security find-identity -v -p codesigning | grep -q "$APPLE_DISTRIBUTION_IDENTITY"; then
    echo "Error: Signing identity not found: $APPLE_DISTRIBUTION_IDENTITY"
    echo "Run 'security find-identity -v -p codesigning' to see available identities."
    exit 1
fi

echo "  dotnet: $(dotnet --version)"
echo "  Xcode: $(xcodebuild -version | head -1)"
echo "  Signing identity: $APPLE_DISTRIBUTION_IDENTITY"

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

# Build widget extension
if [ "$SKIP_WIDGET" = false ]; then
    echo ""
    echo "=== Building widget extension ==="
    WIDGET_PROVISIONING_PROFILE="${WIDGET_PROVISIONING_PROFILE}" \
        "$WIDGET_DIR/build.sh" --release > "$BUILD_OUTPUT" 2>&1
    echo "  Widget extension built successfully"
else
    echo ""
    echo "=== Skipping widget extension build ==="
fi

# Build MAUI iOS app
echo ""
echo "=== Building Famick Home iOS app ==="
echo "  Configuration: Release"
echo "  Target: net10.0-ios / ios-arm64"

dotnet publish "$MOBILE_PROJECT" \
    -f net10.0-ios \
    -c Release \
    -r ios-arm64 \
    /p:CodesignKey="$APPLE_DISTRIBUTION_IDENTITY" \
    /p:CodesignProvision="$APP_PROVISIONING_PROFILE" \
    /p:ApplicationVersion="$BUILD_NUMBER" \
    > "$BUILD_OUTPUT" 2>&1

# Find the IPA
IPA_PATH=$(find "$REPO_ROOT/src/Famick.HomeManagement.Mobile" -name "*.ipa" -path "*/publish/*" -type f 2>/dev/null | head -1)

if [ -z "$IPA_PATH" ]; then
    echo "Error: No .ipa file found in publish output."
    echo "Check the build output with --verbose for details."
    exit 1
fi

echo "  IPA: $IPA_PATH"
echo "  Size: $(du -h "$IPA_PATH" | cut -f1)"

# Upload to TestFlight
if [ "$SKIP_UPLOAD" = false ]; then
    echo ""
    echo "=== Uploading to TestFlight ==="

    xcrun altool --upload-app \
        --type ios \
        --file "$IPA_PATH" \
        --apiKey "$APP_STORE_CONNECT_KEY_ID" \
        --apiIssuer "$APP_STORE_CONNECT_ISSUER_ID"

    echo "  Upload complete! Check App Store Connect for processing status."
else
    echo ""
    echo "=== Upload skipped (--skip-upload) ==="
fi

# Save build number
echo "$BUILD_NUMBER" > "$BUILD_NUMBER_FILE"

echo ""
echo "=== Done ==="
echo "  Build number: $BUILD_NUMBER"
echo "  IPA: $IPA_PATH"
[ "$SKIP_UPLOAD" = false ] && echo "  Status: Uploaded to TestFlight"
