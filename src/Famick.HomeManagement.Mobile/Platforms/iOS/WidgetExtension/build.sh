#!/bin/bash

# Build script for iOS App Extensions (Widget + Share)
# This script builds all extension targets for both device and simulator
#
# Usage:
#   ./build.sh             # Development build (device + simulator)
#   ./build.sh --release   # Distribution build (device only, with distribution signing)
#
# Environment variables for --release mode:
#   DEVELOPMENT_TEAM                    - Apple Team ID (default: 7A6WPZLCK9)
#   CODE_SIGN_IDENTITY                  - Signing identity (default: Apple Distribution)
#   WIDGET_PROVISIONING_PROFILE         - Provisioning profile name for widget extension
#   SHARE_EXT_PROVISIONING_PROFILE      - Provisioning profile name for share extension
#   MARKETING_VERSION                   - CFBundleShortVersionString (default: 1.0)
#   CURRENT_PROJECT_VERSION             - CFBundleVersion / build number (default: 1)

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$SCRIPT_DIR/QuickConsumeWidget/QuickConsumeWidget.xcodeproj"
BUILD_DIR="$SCRIPT_DIR/XReleases"
RELEASE_MODE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --release)
            RELEASE_MODE=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--release]"
            exit 1
            ;;
    esac
done

echo "=== Building iOS Extensions (Widget + Share) ==="
echo "Script directory: $SCRIPT_DIR"
echo "Build directory: $BUILD_DIR"
echo "Mode: $([ "$RELEASE_MODE" = true ] && echo "Distribution (release)" || echo "Development")"

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$BUILD_DIR"

# Check if Xcode project exists
if [ ! -d "$PROJECT_DIR" ]; then
    echo "Error: Xcode project not found at $PROJECT_DIR"
    echo "Please create the Xcode project first using Xcode."
    exit 1
fi

# Helper to get provisioning profile for a scheme (release mode)
get_profile_for_scheme() {
    case "$1" in
        QuickConsumeWidgetExtensionExtension) echo "$WIDGET_PROVISIONING_PROFILE" ;;
        ShareContactExtension) echo "$SHARE_EXT_PROVISIONING_PROFILE" ;;
        *) echo "" ;;
    esac
}

if [ "$RELEASE_MODE" = true ]; then
    # Distribution build - device only
    TEAM_ID="${DEVELOPMENT_TEAM:-7A6WPZLCK9}"
    SIGN_IDENTITY="${CODE_SIGN_IDENTITY:-Apple Distribution}"

    # Version overrides
    VERSION_ARGS=()
    [ -n "$MARKETING_VERSION" ] && VERSION_ARGS+=("MARKETING_VERSION=$MARKETING_VERSION")
    [ -n "$CURRENT_PROJECT_VERSION" ] && VERSION_ARGS+=("CURRENT_PROJECT_VERSION=$CURRENT_PROJECT_VERSION")

    echo "Building for iOS device (distribution)..."
    echo "  Team ID: $TEAM_ID"
    echo "  Sign identity: $SIGN_IDENTITY"
    [ -n "$WIDGET_PROVISIONING_PROFILE" ] && echo "  Widget profile: $WIDGET_PROVISIONING_PROFILE"
    [ -n "$SHARE_EXT_PROVISIONING_PROFILE" ] && echo "  Share profile:  $SHARE_EXT_PROVISIONING_PROFILE"
    [ -n "$MARKETING_VERSION" ] && echo "  Version: $MARKETING_VERSION"
    [ -n "$CURRENT_PROJECT_VERSION" ] && echo "  Build: $CURRENT_PROJECT_VERSION"

    # Clean once before building all schemes
    xcodebuild -project "$PROJECT_DIR" \
        -scheme "QuickConsumeWidgetExtensionExtension" \
        -configuration Release \
        -sdk iphoneos \
        BUILD_DIR="$BUILD_DIR" \
        clean

    for SCHEME in QuickConsumeWidgetExtensionExtension ShareContactExtension; do
        PROFILE=$(get_profile_for_scheme "$SCHEME")
        PROFILE_ARGS=()
        if [ -n "$PROFILE" ]; then
            PROFILE_ARGS+=("PROVISIONING_PROFILE_SPECIFIER=$PROFILE")
        fi

        echo ""
        echo "--- Building $SCHEME (device, distribution) ---"
        xcodebuild -project "$PROJECT_DIR" \
            -scheme "$SCHEME" \
            -configuration Release \
            -sdk iphoneos \
            BUILD_DIR="$BUILD_DIR" \
            CODE_SIGN_IDENTITY="$SIGN_IDENTITY" \
            DEVELOPMENT_TEAM="$TEAM_ID" \
            CODE_SIGN_STYLE="Manual" \
            "${PROFILE_ARGS[@]}" \
            "${VERSION_ARGS[@]}" \
            build
    done

    echo ""
    echo "=== Distribution Build Complete ==="
    echo "Widget: $BUILD_DIR/Release-iphoneos/QuickConsumeWidgetExtensionExtension.appex"
    echo "Share:  $BUILD_DIR/Release-iphoneos/ShareContactExtension.appex"
else
    # Development build - device + simulator
    # Clean once before building all schemes
    xcodebuild -project "$PROJECT_DIR" \
        -scheme "QuickConsumeWidgetExtensionExtension" \
        -configuration Release \
        -sdk iphoneos \
        BUILD_DIR="$BUILD_DIR" \
        clean

    for SCHEME in QuickConsumeWidgetExtensionExtension ShareContactExtension; do
        echo ""
        echo "--- Building $SCHEME (device) ---"
        xcodebuild -project "$PROJECT_DIR" \
            -scheme "$SCHEME" \
            -configuration Release \
            -sdk iphoneos \
            BUILD_DIR="$BUILD_DIR" \
            build

        echo ""
        echo "--- Building $SCHEME (simulator) ---"
        xcodebuild -project "$PROJECT_DIR" \
            -scheme "$SCHEME" \
            -configuration Release \
            -sdk iphonesimulator \
            BUILD_DIR="$BUILD_DIR" \
            build
    done

    echo ""
    echo "=== Build Complete ==="
    echo "Widget (device):    $BUILD_DIR/Release-iphoneos/QuickConsumeWidgetExtensionExtension.appex"
    echo "Widget (simulator): $BUILD_DIR/Release-iphonesimulator/QuickConsumeWidgetExtensionExtension.appex"
    echo "Share (device):     $BUILD_DIR/Release-iphoneos/ShareContactExtension.appex"
    echo "Share (simulator):  $BUILD_DIR/Release-iphonesimulator/ShareContactExtension.appex"
fi
