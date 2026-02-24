#!/bin/bash

# Build script for QuickConsumeWidget iOS Widget Extension
# This script builds the widget extension for both device and simulator
#
# Usage:
#   ./build.sh             # Development build (device + simulator)
#   ./build.sh --release   # Distribution build (device only, with distribution signing)
#
# Environment variables for --release mode:
#   DEVELOPMENT_TEAM                 - Apple Team ID (default: 7A6WPZLCK9)
#   CODE_SIGN_IDENTITY               - Signing identity (default: Apple Distribution)
#   WIDGET_PROVISIONING_PROFILE      - Provisioning profile name
#   MARKETING_VERSION                - CFBundleShortVersionString (default: 1.0)
#   CURRENT_PROJECT_VERSION          - CFBundleVersion / build number (default: 1)

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

echo "=== Building QuickConsumeWidget iOS Widget Extension ==="
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

if [ "$RELEASE_MODE" = true ]; then
    # Distribution build - device only
    TEAM_ID="${DEVELOPMENT_TEAM:-7A6WPZLCK9}"
    SIGN_IDENTITY="${CODE_SIGN_IDENTITY:-Apple Distribution}"

    SIGNING_ARGS=(
        CODE_SIGN_IDENTITY="$SIGN_IDENTITY"
        DEVELOPMENT_TEAM="$TEAM_ID"
        CODE_SIGN_STYLE="Manual"
    )

    if [ -n "$WIDGET_PROVISIONING_PROFILE" ]; then
        SIGNING_ARGS+=(PROVISIONING_PROFILE_SPECIFIER="$WIDGET_PROVISIONING_PROFILE")
    fi

    # Version overrides
    VERSION_ARGS=()
    [ -n "$MARKETING_VERSION" ] && VERSION_ARGS+=(MARKETING_VERSION="$MARKETING_VERSION")
    [ -n "$CURRENT_PROJECT_VERSION" ] && VERSION_ARGS+=(CURRENT_PROJECT_VERSION="$CURRENT_PROJECT_VERSION")

    echo "Building for iOS device (distribution)..."
    echo "  Team ID: $TEAM_ID"
    echo "  Sign identity: $SIGN_IDENTITY"
    [ -n "$WIDGET_PROVISIONING_PROFILE" ] && echo "  Profile: $WIDGET_PROVISIONING_PROFILE"
    [ -n "$MARKETING_VERSION" ] && echo "  Version: $MARKETING_VERSION"
    [ -n "$CURRENT_PROJECT_VERSION" ] && echo "  Build: $CURRENT_PROJECT_VERSION"

    xcodebuild -project "$PROJECT_DIR" \
        -scheme "QuickConsumeWidgetExtensionExtension" \
        -configuration Release \
        -sdk iphoneos \
        BUILD_DIR="$BUILD_DIR" \
        "${SIGNING_ARGS[@]}" \
        "${VERSION_ARGS[@]}" \
        clean build

    echo "=== Distribution Build Complete ==="
    echo "Device build: $BUILD_DIR/Release-iphoneos/QuickConsumeWidgetExtensionExtension.appex"
else
    # Development build - device + simulator
    echo "Building for iOS device..."
    xcodebuild -project "$PROJECT_DIR" \
        -scheme "QuickConsumeWidgetExtensionExtension" \
        -configuration Release \
        -sdk iphoneos \
        BUILD_DIR="$BUILD_DIR" \
        clean build

    echo "Building for iOS simulator..."
    xcodebuild -project "$PROJECT_DIR" \
        -scheme "QuickConsumeWidgetExtensionExtension" \
        -configuration Release \
        -sdk iphonesimulator \
        BUILD_DIR="$BUILD_DIR" \
        clean build

    echo "=== Build Complete ==="
    echo "Device build: $BUILD_DIR/Release-iphoneos/QuickConsumeWidgetExtensionExtension.appex"
    echo "Simulator build: $BUILD_DIR/Release-iphonesimulator/QuickConsumeWidgetExtensionExtension.appex"
fi
