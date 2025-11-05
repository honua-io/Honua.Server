# Honua Field - User Flows & Workflows

**Version:** 1.0
**Date:** November 2025
**Platform:** .NET MAUI (iOS & Android)

---

## Table of Contents

1. [Core Workflows](#1-core-workflows)
2. [Data Collection Flows](#2-data-collection-flows)
3. [Offline & Sync Flows](#3-offline--sync-flows)
4. [Voice Command Flows](#4-voice-command-flows-phase-2)
5. [AR Workflows](#5-ar-workflows-phase-3)
6. [Error & Edge Cases](#6-error--edge-cases)

---

## 1. Core Workflows

### 1.1 First-Time User Onboarding

```
Start App (First Launch)
    ↓
Splash Screen (2 seconds)
    ↓
Welcome Screen
    ├─ "Get Started" button
    ├─ Skip onboarding →
    ↓
Permissions Request (in sequence)
    ├─ Location Access
    │   ├─ "Allow While Using App"
    │   ├─ "Allow Once"
    │   └─ "Don't Allow" → Warning dialog
    ├─ Camera Access
    │   ├─ "Allow"
    │   └─ "Don't Allow" → Limited functionality
    └─ Notifications (optional)
        ├─ "Allow"
        └─ "Don't Allow"
    ↓
Login Screen
    ├─ Email input
    ├─ Password input
    ├─ "Sign In" button
    ├─ "Forgot Password?" link
    └─ SSO options (if available)
    ↓
Initial Data Sync
    ├─ Progress indicator
    ├─ "Downloading collections..."
    └─ "Ready to go!"
    ↓
Tutorial Overlay (optional, dismissible)
    ├─ Map screen: "Tap + to create features"
    ├─ Point to GPS button
    ├─ Point to sync status
    └─ "Got it" / "Skip tutorial"
    ↓
Main Map Screen ✓
```

---

### 1.2 Login & Authentication

```
App Launch (Existing User)
    ↓
Splash Screen
    ↓
Check Stored Credentials
    ├─ Valid Token → Main Map Screen ✓
    └─ Expired/Missing Token → Login Screen
        ├─ Auto-fill email (if remembered)
        ├─ Enter credentials
        ├─ "Sign In" →
        │   ├─ Success → Main Map ✓
        │   └─ Error → Show error message
        │       ├─ "Invalid credentials"
        │       ├─ "Network error"
        │       └─ Retry button
        └─ Biometric Login (if enabled)
            ├─ Face ID / Touch ID prompt
            ├─ Success → Main Map ✓
            └─ Fail → Password fallback
```

---

## 2. Data Collection Flows

### 2.1 Create Point Feature (GPS Location)

```
User on Map Screen
    ↓
Tap FAB (+) button
    ↓
Quick Actions Menu appears
    ├─ 📍 Create Point
    ├─ 📏 Create Line
    ├─ ⬡  Create Polygon
    ├─ 📷 Quick Photo
    └─ 🎤 Voice Note
    ↓
Select "📍 Create Point"
    ↓
[Map shows crosshair at GPS location]
    ↓
Options:
    ├─ Accept GPS Location
    │   ├─ Check accuracy: < 5m → Green ✓
    │   ├─ 5-15m → Yellow ⚠️
    │   └─ > 15m → Red ✗ "Improve accuracy?"
    │       ├─ Wait for better signal
    │       ├─ Move to open area
    │       └─ Continue anyway
    │   ↓
    │   Feature Form opens (GPS coordinates auto-filled)
    │
    ├─ Tap different location on map
    │   └─ Crosshair moves to tapped point
    │       └→ Feature Form with selected coords
    │
    └─ Cancel → Return to map
    ↓
Feature Form Screen
    ├─ Select Feature Type (dropdown)
    │   ├─ Recent types shown first
    │   └─ Search all types
    ├─ Fill required fields (marked with *)
    │   ├─ Text inputs (with autocomplete)
    │   ├─ Number inputs
    │   ├─ Dropdowns
    │   ├─ Radio buttons
    │   └─ Checkboxes
    ├─ Add Photos (optional)
    │   ├─ Take Photo →
    │   │   ├─ Camera opens
    │   │   ├─ Capture
    │   │   └─ Thumbnail added to gallery
    │   └─ Choose from Library
    ├─ Add Notes (optional, voice available)
    │   └─ Tap 🎤 for voice-to-text
    ├─ Validate fields
    │   ├─ Missing required → Red border + error
    │   ├─ Invalid format → Error message
    │   └─ All valid → "Save" button enabled
    └─ Save Options
        ├─ "Save" → Save to local DB
        │   ├─ Success animation ✓
        │   ├─ Toast: "Feature saved"
        │   ├─ Return to map
        │   └─ Feature appears on map
        ├─ "Save & New" → Save and open blank form
        └─ "Cancel" →
            ├─ Unsaved changes? → Confirm dialog
            │   ├─ "Discard" → Return to map
            │   ├─ "Save Draft" → Save locally
            │   └─ "Cancel" → Stay on form
            └─ No changes → Return to map
```

---

### 2.2 Edit Existing Feature

```
User on Map Screen
    ↓
Tap feature marker/point
    ↓
Feature Popup appears (callout)
    ├─ Feature name/ID
    ├─ Key attributes preview
    ├─ Thumbnail (if photo exists)
    └─ Actions:
        ├─ "View Details" →
        │   ↓
        │   Feature Detail Screen
        │   ├─ All attributes displayed
        │   ├─ Photo gallery
        │   ├─ Location map
        │   └─ Action buttons:
        │       ├─ ✏️ Edit → Feature Form (edit mode)
        │       ├─ 📋 Copy
        │       ├─ 🗑️ Delete → Confirm dialog
        │       └─ 📤 Share
        │
        ├─ "Edit" → Feature Form (pre-filled)
        │   ├─ Modify fields
        │   ├─ Add/remove photos
        │   ├─ Update location (drag map marker)
        │   └─ Save changes
        │       ├─ Optimistic locking check
        │       │   ├─ No conflict → Save ✓
        │       │   └─ Conflict detected →
        │       │       └─ Conflict Resolution Dialog
        │       │           ├─ "Keep My Version"
        │       │           ├─ "Use Server Version"
        │       │           ├─ "View Diff"
        │       │           └─ "Merge Changes"
        │       └─ Mark as pending sync
        │
        ├─ "Navigate" → GPS navigation to feature
        │
        └─ "Close" → Return to map
```

---

### 2.3 Quick Photo Capture

```
User on Map Screen (at field location)
    ↓
Tap FAB (+) → Select "📷 Quick Photo"
    ↓
Camera opens immediately
    ├─ Capture photo
    ├─ Preview screen
    │   ├─ "Retake"
    │   └─ "Use Photo"
    ↓
Quick Attach Dialog
    ├─ "Attach to existing feature?"
    │   ├─ Show nearby features list
    │   ├─ Select feature → Photo attached
    │   └─ "Create new feature" →
    │       └→ Feature Form with photo pre-attached
    │
    ├─ "Save to gallery only"
    │   └─ Photo saved, can attach later
    │
    └─ "Discard"
```

---

## 3. Offline & Sync Flows

### 3.1 Download Collection for Offline Use

```
User on Collections Screen
    ↓
Select collection to download
    ↓
Collection Detail Screen
    ├─ Collection info
    ├─ Feature count
    ├─ Last sync time
    └─ "Download for Offline" button
        ↓
        Download Options Dialog
        ├─ "Current Map Extent"
        │   └─ Downloads only features visible on map
        ├─ "Custom Area"
        │   ├─ Draw bounding box on map
        │   └─ Feature count preview
        ├─ "Entire Collection"
        │   └─ Warning if > 1000 features
        └─ Include options:
            ├─ ☑ Download photos
            ├─ ☑ Download map tiles
            └─ ☑ Download related features
        ↓
        "Start Download" button
        ↓
        Download Progress
        ├─ Progress bar
        ├─ "Downloading features... 45/125"
        ├─ "Downloading photos... 12/45"
        ├─ "Downloading map tiles... 234/567"
        ├─ Pausable/cancellable
        └─ Complete ✓
            ├─ Success toast
            └─ Collection marked with ⬇ icon
```

---

### 3.2 Offline Mode Detection & Handling

```
App Running (Online)
    ↓
Network Lost (airplane mode, no signal, etc.)
    ↓
Offline Detection (automatic)
    ├─ Status bar updates: 🔴 Offline
    ├─ Toast notification: "You're offline. Changes will sync later."
    └─ UI adapts:
        ├─ Sync button disabled
        ├─ Online-only features grayed out
        ├─ Downloaded collections remain accessible
        └─ New features saved to sync queue
    ↓
User continues working offline
    ├─ Create features → Saved locally, marked pending
    ├─ Edit features → Changes queued
    ├─ Delete features → Marked for deletion
    ├─ View downloaded data → Works normally
    └─ View online-only data → "Not available offline" message
    ↓
Network Restored
    ├─ Status bar: 🟢 Online
    ├─ Toast: "Back online! Syncing changes..."
    └─ Auto-sync (if enabled)
        ├─ Upload pending changes
        ├─ Pull server updates
        ├─ Resolve conflicts (if any)
        └─ Complete ✓
```

---

### 3.3 Manual Sync with Conflicts

```
User on Sync Screen
    ↓
Tap "Sync All" button
    ↓
Sync Process Starts
    ├─ Progress indicator
    ├─ "Uploading 3 features..."
    ├─ "Downloading updates..."
    └─ Conflict detected ⚠️
        ↓
        Sync pauses, Conflict notification
        ↓
        Conflicts Screen
        ├─ List of conflicted features
        │   ├─ Feature ID/name
        │   ├─ "Modified by you and Jane Smith"
        │   └─ Timestamp info
        ├─ Tap conflict item →
        │   ↓
        │   Conflict Resolution Dialog
        │   ├─ Side-by-side comparison
        │   │   ├─ Your Version (left)
        │   │   ├─ Server Version (right)
        │   │   └─ Changed fields highlighted
        │   ├─ Actions:
        │   │   ├─ "Keep Mine" → Overwrite server
        │   │   ├─ "Use Server's" → Discard local
        │   │   ├─ "Merge" → Manual field selection
        │   │   │   ├─ For each field:
        │   │   │   │   ├─ ◯ My version
        │   │   │   │   ├─ ◯ Server version
        │   │   │   │   └─ ◯ Custom value
        │   │   │   └─ "Apply Merge"
        │   │   └─ "Keep Both" → Create duplicate
        │   └─ "Skip for Now" → Resolve later
        │
        └─ Resolve all conflicts
            ↓
            Resume sync
            ↓
            Sync Complete ✓
            └─ Toast: "All changes synced"
```

---

## 4. Voice Command Flows (Phase 2)

### 4.1 Voice-Activated Data Entry

```
User on Feature Form
    ↓
Tap 🎤 microphone icon on any text field
    ↓
Voice Input Activated
    ├─ Microphone animation (listening)
    ├─ "Speak now..." hint
    └─ User speaks: "Leaning slightly to the west"
        ↓
        Speech-to-text processing
        ├─ Real-time transcription shown
        ├─ Confidence indicators
        └─ Auto-submit on pause (1 second)
            ↓
            Text appears in field
            ├─ Editable (can correct mistakes)
            └─ Continue to next field
```

---

### 4.2 Hands-Free Feature Creation (Ray-Ban Glasses)

```
Field Worker wearing Ray-Ban glasses
    ↓
Voice: "Hey Honua, create new inspection"
    ↓
Phone App activates
    ├─ Audio feedback: "Starting inspection. What type?"
    └─ Listening for response
        ↓
        Voice: "Utility pole"
        ↓
        Audio: "Utility pole confirmed. Asset ID?"
        ↓
        Voice: "Pole one two four seven"
        ↓
        Audio: "Asset ID: POLE-1247. Is that correct?"
        ↓
        Voice: "Yes" / "Correct"
        ↓
        Audio: "Great. What's the height in feet?"
        ↓
        Voice: "Thirty five feet"
        ↓
        Audio: "Height: 35 feet. Condition?"
        ↓
        Voice: "Good"
        ↓
        Audio: "Condition: Good. Ready for a photo?"
        ↓
        Voice: "Take photo"
        ↓
        Ray-Ban camera triggers
        ├─ Photo captured
        └─ Audio: "Photo captured"
            ↓
            Audio: "Anything else to add?"
            ↓
            Voice: "No" / "That's all"
            ↓
            Phone saves feature to local DB
            ├─ GPS location auto-added
            ├─ Timestamp recorded
            └─ Audio: "Feature saved successfully"
```

---

## 5. AR Workflows (Phase 3)

### 5.1 AR Underground Utility Visualization (Quest 3)

```
Technician puts on Quest 3 headset
    ↓
Launch Honua AR App
    ↓
AR Initialization
    ├─ Request camera permission
    ├─ Spatial tracking calibration
    └─ GPS location acquisition
        ↓
        AR View loads
        ├─ Passthrough camera feed
        ├─ GPS accuracy indicator (top-left)
        └─ Compass/heading display
        ↓
        App queries server:
        ├─ GET /collections/utilities/items?bbox=...
        └─ GET /v1.1/Datastreams(gpr-sensor)/Observations
        ↓
        AR Overlay renders:
        ├─ Yellow lines (gas pipes) on ground
        ├─ Blue lines (water pipes)
        ├─ Red lines (electric cables)
        ├─ Depth labels: "-2.3m"
        ├─ Safe dig zones (green circles)
        └─ Live GPR sensor data overlay
        ↓
        User walks around site
        ├─ Utilities remain anchored to GPS coords
        ├─ Perspective changes as user moves
        ├─ Labels always face user
        └─ Distance-based LOD (detail level)
        ↓
        User detects new utility (GPR sensor alert)
        ├─ AR shows: "STRONG SIGNAL - Buried utility"
        ├─ Depth reading: "-2.5m"
        └─ User marks location (hand pinch gesture)
            ↓
            Quick Capture Dialog
            ├─ "Mark as:"
            │   ├─ ◯ Gas
            │   ├─ ◯ Water
            │   ├─ ◯ Electric
            │   └─ ◯ Unknown
            ├─ Depth: -2.5m (from sensor)
            ├─ Confidence: High
            └─ "Save Detection" (voice or hand gesture)
                ↓
                Upload to server immediately (if online)
                ├─ POST /collections/field_detections/items
                └─ Appears in AR for all team members
```

---

### 5.2 AR Measurement Tool

```
User in AR View (Quest 3)
    ↓
Voice: "Measure distance"
    OR
    Hand gesture: Point at object 1 + pinch
    ↓
AR Measurement Mode activated
    ├─ Crosshair appears
    ├─ "Tap to place start point"
    └─ User taps/pinches at point A
        ↓
        Start point anchored
        ├─ Sphere marker appears at point A
        └─ "Tap to place end point"
            ↓
            User moves to point B, taps/pinches
            ↓
            End point anchored
            ├─ Sphere marker at point B
            ├─ Line drawn between A and B
            └─ Distance label appears mid-line
                ├─ "Distance: 15.3m"
                ├─ "Horizontal: 14.8m"
                └─ "Vertical: ±2.1m"
                ↓
                Measurement saved
                ├─ "Save Measurement" option
                │   └─ Attach to feature or save standalone
                ├─ "New Measurement"
                └─ "Exit Measurement Mode"
```

---

## 6. Error & Edge Cases

### 6.1 Poor GPS Accuracy

```
User attempts to create feature
    ↓
GPS accuracy check: ±45 meters 🔴
    ↓
Warning Dialog:
    ┌────────────────────────────────┐
    │   ⚠️ Low GPS Accuracy          │
    │                                │
    │ Current accuracy: ±45 meters   │
    │                                │
    │ Tips to improve:               │
    │ • Move to open area            │
    │ • Wait for more satellites     │
    │ • Connect external GNSS        │
    │                                │
    │ [Wait] [Continue Anyway]       │
    └────────────────────────────────┘
    ├─ "Wait" →
    │   ├─ GPS status monitor shown
    │   ├─ Updates every 2 seconds
    │   └─ Auto-proceed when < 15m
    │
    └─ "Continue Anyway" →
        ├─ Feature saved with accuracy flag
        ├─ Warning icon on map marker
        └─ Low accuracy noted in properties
```

---

### 6.2 Sync Failure

```
Auto-sync triggered
    ↓
Upload attempts...
    ↓
Network error / Server error
    ↓
Sync Failed
    ├─ Error notification
    ├─ Retry count incremented
    └─ Retry Logic:
        ├─ Attempt 1: Retry after 10 seconds
        ├─ Attempt 2: Retry after 30 seconds
        ├─ Attempt 3: Retry after 1 minute
        ├─ Attempt 4: Retry after 5 minutes
        └─ Attempt 5+: Wait for manual sync
            ↓
            Persistent notification:
            "Some changes haven't synced"
            ├─ Tap to view pending items
            ├─ Manual "Retry" button
            └─ "Sync Later" option
```

---

### 6.3 Photo Capture Failure

```
User taps camera icon
    ↓
Camera permission denied
    ↓
Permission Dialog:
    ┌────────────────────────────────┐
    │   📷 Camera Access Required    │
    │                                │
    │ Honua needs camera access to   │
    │ capture photos of features.    │
    │                                │
    │ [Open Settings] [Cancel]       │
    └────────────────────────────────┘
    ├─ "Open Settings" → iOS/Android settings
    └─ "Cancel" → Return to form
        └─ Camera button disabled
        └─ "Choose from library" still available
```

---

### 6.4 Storage Full

```
User tries to download collection
    ↓
Storage check: < 100MB available
    ↓
Warning Dialog:
    ┌────────────────────────────────┐
    │   💾 Storage Almost Full       │
    │                                │
    │ Only 85MB available            │
    │ This download needs ~200MB     │
    │                                │
    │ Manage Storage:                │
    │ • Delete old photos (45MB)     │
    │ • Clear map cache (120MB)      │
    │ • Remove collections           │
    │                                │
    │ [Manage Storage] [Cancel]      │
    └────────────────────────────────┘
    ├─ "Manage Storage" →
    │   └─ Storage Management Screen
    │       ├─ Collections list with sizes
    │       ├─ "Delete downloaded tiles"
    │       ├─ "Clear photo cache"
    │       └─ "Free Up Space" suggestions
    │
    └─ "Cancel" → Don't download
```

---

### 6.5 Duplicate Feature Warning

```
User creates feature
    ↓
Server checks for duplicates
    ├─ Same location (within 5m)
    ├─ Same type
    └─ Within 24 hours
        ↓
        Possible Duplicate Detected
        ┌────────────────────────────────┐
        │   ⚠️ Possible Duplicate        │
        │                                │
        │ Similar feature exists:        │
        │ • POLE-1247                    │
        │ • 3 meters away                │
        │ • Created 2 hours ago          │
        │ • By: Jane Smith               │
        │                                │
        │ [View Existing] [Create Anyway]│
        └────────────────────────────────┘
        ├─ "View Existing" → Show existing feature
        │   ├─ "This is the same" → Don't create
        │   └─ "Different feature" → Continue
        │
        └─ "Create Anyway" → Save new feature
            └─ Flag as potential duplicate
```

---

## 7. Happy Path Summary

### Quick Feature Creation (2 minutes)

```
1. Arrive at location (GPS locks: ±1.2m) .................. 10s
2. Tap FAB (+) → Create Point ............................. 2s
3. Select feature type (autocomplete) ..................... 5s
4. Fill required fields (3 fields) ........................ 30s
5. Take photo (camera → capture → done) ................... 15s
6. Add voice note .......................................... 20s
7. Review & save ........................................... 3s
8. Return to map (feature appears) ......................... 2s
───────────────────────────────────────────────────────────────
Total: ~87 seconds
```

**Optimizations:**
- Voice input: Reduces to ~45 seconds
- Ray-Ban hands-free: Reduces to ~30 seconds (parallel activities)
- Smart suggestions: Saves 5-10 seconds per field

---

## Appendix: State Diagrams

### Feature Lifecycle States

```
┌─────────┐
│  Draft  │ ← User creating
└────┬────┘
     │ Save
     ↓
┌─────────┐
│ Pending │ ← Saved locally, not synced
└────┬────┘
     │ Sync
     ├──→ [Success] → ┌────────┐
     │                 │ Synced │
     │                 └────────┘
     │
     └──→ [Conflict] → ┌──────────┐
                        │ Conflict │ → User resolves → Synced
                        └──────────┘
```

### Sync States

```
Idle → Queued → Syncing → Success → Idle
         ↓                    ↓
         ├──→ Paused          ├──→ Conflict → Resolved → Idle
         └──→ Failed → Retry  └──→ Error → Retry → Idle
```

---

**Document Status:** ✅ Complete
**Next Review:** After user testing
**Related Docs:** UI_UX_SPECIFICATION.md, DESIGN_DOCUMENT.md
