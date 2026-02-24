# QuickConsumeWidget - iOS Widget Extension

This directory contains the Swift/SwiftUI source files for the Quick Consume iOS widget.

## Overview

The widget displays:

- A barcode scanner icon for quick access
- Count of expired items (red badge)
- Count of items expiring soon (orange badge)

Tapping the widget opens the Famick Home app directly to the Quick Consume page.

## Prerequisites

- Xcode 15.0 or later
- iOS 17.0+ deployment target
- Apple Developer account with App Groups capability

## Creating the Xcode Project

Since the `.xcodeproj` file is not included in version control, you need to create it in Xcode.

### Step 1: Create a New iOS App Project

1. Open Xcode
2. **File > New > Project**
3. Choose **iOS > App** template, click Next
4. Configure the app:
   - Product Name: `QuickConsumeWidget`
   - Team: Your Apple Developer Team
   - Organization Identifier: `com.famick.homemanagement`
   - Interface: SwiftUI
   - Language: Swift
5. Click Next and save to this directory (`WidgetExtension/QuickConsumeWidget/`)

### Step 2: Add Widget Extension Target

1. With the project open, go to **File > New > Target**
2. Choose **iOS > Widget Extension**, click Next
3. Configure the widget:
   - Product Name: `QuickConsumeWidgetExtension`
   - Team: Your Apple Developer Team
   - Bundle Identifier: `com.famick.homemanagement.QuickConsumeWidgetExtension`
   - Include Configuration App Intent: **No**
   - Include Live Activity: **No**
4. Click Finish
5. When prompted "Activate QuickConsumeWidgetExtension scheme?", click **Activate**

### Step 3: Add the Custom Swift Files

The widget extension auto-generates placeholder Swift files. You need to add the custom implementation files:

1. Go to **File > Add Files to "QuickConsumeWidget"...**
2. Navigate to `WidgetExtension/QuickConsumeWidget/` folder
3. Select these 3 files (Cmd+click to select multiple):
   - `QuickConsumeEntry.swift`
   - `QuickConsumeProvider.swift`
   - `QuickConsumeWidget.swift`
4. In the options dialog:
   - Action: **Reference files in place**
   - Targets: **Uncheck** "QuickConsumeWidget", **Check** "QuickConsumeWidgetExtensionExtension"
5. Click **Finish**
6. When asked about Objective-C bridging header, click **Don't Create**

### Step 4: Configure App Groups

1. In the Project Navigator, select the **QuickConsumeWidget** project (blue icon at top)
2. Under TARGETS, select **QuickConsumeWidgetExtensionExtension**
3. Go to **Signing & Capabilities** tab
4. Click **+ Capability**
5. Search for and add **App Groups**
6. Click the **+** under App Groups
7. Add: `group.com.famick.homemanagement`

### Step 5: Verify Build Settings

1. With the widget extension target selected, go to **General** tab
2. Under Identity, verify:
   - Display Name: `QuickConsumeWidgetExtension`
   - Bundle Identifier: `com.famick.homemanagement.QuickConsumeWidgetExtension`
3. Under Minimum Deployments, set iOS to **17.0** or your minimum supported version

## App Group Configuration

The widget uses an App Group to share data with the main app:

- **App Group ID:** `group.com.famick.homemanagement`

This must be configured in:

1. The main MAUI app's Entitlements.plist (already done)
2. The widget extension's Signing & Capabilities (Step 4 above)
3. Apple Developer Portal for both app IDs

## Building

After creating the Xcode project:

```bash
chmod +x build.sh
./build.sh
```

This will build the widget for both device and simulator, placing outputs in `XReleases/`.

## Data Sharing

The main app writes widget data to the shared App Group container:

```swift
// Keys used by the widget
"ExpiringItemCount" (Int): Number of expired items
"DueSoonItemCount" (Int): Number of items expiring soon
```

The main app updates these values after:

- User login
- Dashboard load
- Consume action
- App backgrounding

## Files

| File                           | Description                              |
| ------------------------------ | ---------------------------------------- |
| `QuickConsumeWidget.swift`     | Main widget definition and views         |
| `QuickConsumeProvider.swift`   | Timeline provider that reads shared data |
| `QuickConsumeEntry.swift`      | Timeline entry model                     |
| `QuickConsumeWidget.entitlements` | App Group capability                  |
| `Info.plist`                   | Widget extension configuration           |
| `build.sh`                     | Build script for both platforms          |

## Supported Widget Sizes

- **Small**: Icon + title + badges
- **Medium**: Icon + title + detailed counts

## Deep Link

The widget opens the main app using: `famick://quick-consume`
