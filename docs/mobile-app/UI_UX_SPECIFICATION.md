# Honua Field - UI/UX Specification

**Version:** 1.0
**Date:** November 2025
**Status:** Design Phase
**Platform:** .NET MAUI (iOS & Android)

---

## Table of Contents

1. [Design Philosophy](#1-design-philosophy)
2. [Visual Design System](#2-visual-design-system)
3. [Screen Wireframes](#3-screen-wireframes)
4. [Component Library](#4-component-library)
5. [Interaction Patterns](#5-interaction-patterns)
6. [Accessibility](#6-accessibility)
7. [Responsive Design](#7-responsive-design)
8. [Dark Mode](#8-dark-mode)

---

## 1. Design Philosophy

### Core Principles

**1. Field-First Design**
- Large touch targets (minimum 44x44 pt)
- High contrast for outdoor visibility
- One-handed operation support
- Minimize text input (voice alternative)

**2. Offline-Aware UI**
- Clear sync status indicators
- Graceful degradation when offline
- Visual feedback for pending changes
- Conflict resolution workflows

**3. Progressive Disclosure**
- Show essential info first
- Advanced features behind clear affordances
- Contextual help where needed
- Empty states with guidance

**4. Data-Dense but Clear**
- Information hierarchy
- Scannable layouts
- Visual grouping
- Smart defaults

---

## 2. Visual Design System

### Color Palette

**Primary Colors:**
```
Primary Blue:    #0066CC (Interactive elements, primary actions)
Primary Dark:    #004080 (Pressed states, headers)
Primary Light:   #3385DB (Hover states, highlights)
```

**Semantic Colors:**
```
Success Green:   #28A745 (Synced, valid, online)
Warning Orange:  #FD7E14 (Pending, caution, low accuracy)
Error Red:       #DC3545 (Conflicts, errors, offline)
Info Blue:       #17A2B8 (Information, tips)
```

**Neutral Colors:**
```
Text Primary:    #212529 (Headings, important text)
Text Secondary:  #6C757D (Secondary text, labels)
Text Disabled:   #ADB5BD (Disabled states)
Background:      #FFFFFF (Main background)
Surface:         #F8F9FA (Cards, elevated surfaces)
Border:          #DEE2E6 (Dividers, borders)
```

**Map-Specific Colors:**
```
Feature Point:   #FF6B6B (Point features)
Feature Line:    #4ECDC4 (Line features)
Feature Polygon: #95E1D3 (Polygon features - fill 30% opacity)
Selected:        #FFD93D (Selected features - bright yellow)
GPS Accuracy:    #0066CC (GPS circle - 30% opacity)
```

### Typography

**Font Family:**
- iOS: San Francisco
- Android: Roboto
- Fallback: System default

**Type Scale:**

```
H1 (Screen Titles):      28pt / Bold / -0.5% tracking
H2 (Section Headers):    22pt / Semibold / -0.3% tracking
H3 (Subsection):         18pt / Semibold / 0% tracking
Body Large:              17pt / Regular / 0% tracking
Body:                    15pt / Regular / 0% tracking
Body Small:              13pt / Regular / 0% tracking
Caption:                 12pt / Regular / 0.5% tracking
Button:                  15pt / Semibold / 0.2% tracking (uppercase)
```

**Line Heights:**
- Headings: 1.2x
- Body text: 1.5x
- Captions: 1.4x

### Spacing System

**Base Unit:** 8pt

```
XXS: 4pt   (Tight spacing within components)
XS:  8pt   (Component padding)
S:   12pt  (Small gaps)
M:   16pt  (Default spacing)
L:   24pt  (Section spacing)
XL:  32pt  (Large sections)
XXL: 48pt  (Major sections)
```

### Elevation & Shadows

```
Level 0 (Flat):
  No shadow

Level 1 (Raised):
  iOS:     shadow(offset: 0,2  blur: 4  color: rgba(0,0,0,0.1))
  Android: elevation: 2dp

Level 2 (Elevated):
  iOS:     shadow(offset: 0,4  blur: 8  color: rgba(0,0,0,0.12))
  Android: elevation: 4dp

Level 3 (Floating):
  iOS:     shadow(offset: 0,8  blur: 16 color: rgba(0,0,0,0.15))
  Android: elevation: 8dp
```

### Border Radius

```
None:     0pt   (Alerts, full-bleed elements)
Small:    4pt   (Buttons, inputs, tags)
Medium:   8pt   (Cards, panels)
Large:    12pt  (Modal dialogs)
Round:    50%   (Avatar, icon buttons)
```

---

## 3. Screen Wireframes

### 3.1 Map Screen (Primary)

```
┌────────────────────────────────────────────┐
│ ≡  Honua Field                🔍 👤 ⚙️    │ ← Status Bar
├────────────────────────────────────────────┤
│ 📍 Current Location: Field Site A          │ ← Location Bar
│ GPS Accuracy: ±1.2m  🟢 Online            │
├────────────────────────────────────────────┤
│                                            │
│                                            │
│            [  MAP VIEW  ]                  │
│                                            │
│         ●  ●  ●                           │
│            ▲ (GPS cursor)                  │
│       ●  ●   ●                            │
│                                            │
│                                            │
│  [+]  Zoom In                             │
│  [-]  Zoom Out                            │
│  [⊕]  Center on GPS                       │
│  [🧭] Compass                             │
│  [📐] Measure                             │
│                                            │
├────────────────────────────────────────────┤
│ 🗺️ Map │📍Features│📋Tasks│🔄Sync│⚙️More│ ← Tab Bar
└────────────────────────────────────────────┘
```

**Interaction Zones:**
- Top 20%: Status/location info (read-only)
- Middle 60%: Map (pan, zoom, tap features)
- Bottom 20%: Tab navigation + FAB

**Floating Action Button (FAB):**
```
  [+]  ← FAB (Bottom Right)

  Tap → Quick Actions Menu:
  ├─ 📍 Create Point
  ├─ 📏 Create Line
  ├─ ⬡  Create Polygon
  ├─ 📷 Quick Photo
  └─ 🎤 Voice Note
```

---

### 3.2 Feature Form Screen

```
┌────────────────────────────────────────────┐
│ ← Back      Create Feature        Save ✓  │ ← Header
├────────────────────────────────────────────┤
│                                            │
│  Feature Type: Utility Pole               │
│  ┌──────────────────────────────────────┐ │
│  │ Utility Pole             ▼ │          │ │ ← Dropdown
│  └──────────────────────────────────────┘ │
│                                            │
│  Asset ID *                                │
│  ┌──────────────────────────────────────┐ │
│  │ POLE-                      🎤│        │ │ ← Text + Voice
│  └──────────────────────────────────────┘ │
│  Auto-suggestions: POLE-1247, POLE-1248   │ ← AI Suggestions
│                                            │
│  Height (feet)                             │
│  ┌──────────────────────────────────────┐ │
│  │ 35                        📏│         │ │ ← Number + Measure
│  └──────────────────────────────────────┘ │
│                                            │
│  Condition                                 │
│  ┌────┬────┬────┬────┐                   │
│  │Good│Fair│Poor│N/A │                   │ ← Radio Buttons
│  └────┴────┴────┴────┘                   │
│    ✓                                      │
│                                            │
│  Photos (2)                                │
│  ┌──────┬──────┐                         │
│  │ 📷   │ 📷   │ [+]                     │ ← Photo Gallery
│  │ IMG  │ IMG  │                          │
│  └──────┴──────┘                         │
│                                            │
│  Notes                                     │
│  ┌──────────────────────────────────────┐ │
│  │ Leaning slightly west...   🎤│        │ │ ← Text Area + Voice
│  │                              │        │ │
│  └──────────────────────────────────────┘ │
│                                            │
│  Location: -122.4194, 37.7749 (±1.2m)    │
│  [📍 Update GPS] [🗺️ Select on Map]      │
│                                            │
├────────────────────────────────────────────┤
│         [Cancel]        [Save] ✓          │ ← Actions
└────────────────────────────────────────────┘
```

**Visual Hierarchy:**
1. Required fields marked with *
2. AI suggestions appear below inputs (subtle blue background)
3. Validation errors appear below fields (red text + icon)
4. Photos in scrollable horizontal gallery
5. GPS accuracy shown with color coding

---

### 3.3 Collections List Screen

```
┌────────────────────────────────────────────┐
│ Collections                    🔍 ⊕        │ ← Header
├────────────────────────────────────────────┤
│ Search collections...                      │ ← Search Bar
├────────────────────────────────────────────┤
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ 📍 Utility Poles              125 ▸ │  │ ← Collection Card
│ │ Last synced: 2 hours ago    🟢      │  │
│ │ ⬇ Downloaded  ⏱️ Pending: 3         │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ 🌳 Street Trees               89 ▸  │  │
│ │ Last synced: 1 day ago      🟡      │  │
│ │ ☁️ Online only  🔄 Syncing...       │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ 🔥 Fire Hydrants              42 ▸  │  │
│ │ Last synced: Never          🔴      │  │
│ │ ☁️ Not downloaded                   │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ 📊 Inspections (Custom)       16 ▸  │  │
│ │ Last synced: 30 min ago     🟢      │  │
│ │ ⬇ Downloaded  ✓ All synced         │  │
│ └──────────────────────────────────────┘  │
│                                            │
├────────────────────────────────────────────┤
│ 🗺️ Map │📍Features│📋Tasks│🔄Sync│⚙️More│
└────────────────────────────────────────────┘
```

**Status Indicators:**
- 🟢 Green: Recently synced, no pending changes
- 🟡 Yellow: Syncing or pending changes
- 🔴 Red: Never synced or sync error
- ⬇ Downloaded icon: Available offline
- ☁️ Cloud icon: Online only

---

### 3.4 Sync Screen

```
┌────────────────────────────────────────────┐
│ Sync Status                    ↻ Sync All │ ← Header
├────────────────────────────────────────────┤
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ 🟢 Online                           │  │ ← Status Card
│ │ Last sync: 5 minutes ago             │  │
│ │ Next auto-sync: in 25 minutes        │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ Pending Changes (3)                        │ ← Section
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ ↑ POLE-1247 (Created)              │  │ ← Pending Item
│ │ Utility Poles • 2 min ago            │  │
│ │ [⌫ Discard] [↑ Sync Now]           │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ ↻ TREE-089 (Modified)              │  │
│ │ Street Trees • 15 min ago            │  │
│ │ [⌫ Discard] [↑ Sync Now]           │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ ✗ HYDRANT-42 (Deleted)             │  │
│ │ Fire Hydrants • 1 hour ago           │  │
│ │ [⎌ Undo] [↑ Sync Now]              │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ Conflicts (1) 🔴                          │ ← Conflicts Section
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │ ⚠️ POLE-1248 (Conflict)            │  │ ← Conflict Item
│ │ Modified by you and Jane Smith       │  │
│ │ [👁️ Review] [⚡ Auto-resolve]        │  │
│ └──────────────────────────────────────┘  │
│                                            │
├────────────────────────────────────────────┤
│ 🗺️ Map │📍Features│📋Tasks│🔄Sync│⚙️More│
└────────────────────────────────────────────┘
```

**Color Coding:**
- Green ↑: New features to upload
- Blue ↻: Modified features to sync
- Red ✗: Deleted features
- Yellow ⚠️: Conflicts requiring resolution

---

### 3.5 Settings Screen

```
┌────────────────────────────────────────────┐
│ ← Back        Settings                     │ ← Header
├────────────────────────────────────────────┤
│                                            │
│ Account                                    │ ← Section
│ ┌──────────────────────────────────────┐  │
│ │ 👤 John Smith                        │  │
│ │ john.smith@honua.io              ▸  │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ Maps                                       │
│ ┌──────────────────────────────────────┐  │
│ │ Default Basemap                  ▸  │  │
│ │ Topographic                          │  │
│ └──────────────────────────────────────┘  │
│ ┌──────────────────────────────────────┐  │
│ │ Units                            ▸  │  │
│ │ Metric                               │  │
│ └──────────────────────────────────────┘  │
│ ┌──────────────────────────────────────┐  │
│ │ Rotate Map            [Toggle ON]    │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ GPS                                        │
│ ┌──────────────────────────────────────┐  │
│ │ Accuracy Threshold                ▸ │  │
│ │ ±5 meters                            │  │
│ └──────────────────────────────────────┘  │
│ ┌──────────────────────────────────────┐  │
│ │ External GNSS                     ▸ │  │
│ │ Not connected                        │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ Sync                                       │
│ ┌──────────────────────────────────────┐  │
│ │ Auto-sync             [Toggle ON]    │  │
│ └──────────────────────────────────────┘  │
│ ┌──────────────────────────────────────┐  │
│ │ Wi-Fi Only            [Toggle ON]    │  │
│ └──────────────────────────────────────┘  │
│ ┌──────────────────────────────────────┐  │
│ │ Sync Interval                     ▸ │  │
│ │ Every 30 minutes                     │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ AI & Voice (Phase 2)                      │
│ ┌──────────────────────────────────────┐  │
│ │ Smart Suggestions     [Toggle ON]    │  │
│ └──────────────────────────────────────┘  │
│ ┌──────────────────────────────────────┐  │
│ │ Voice Input           [Toggle ON]    │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ About                                      │
│ ┌──────────────────────────────────────┐  │
│ │ Version 1.0.0                        │  │
│ │ Help & Feedback                   ▸ │  │
│ │ Privacy Policy                    ▸ │  │
│ └──────────────────────────────────────┘  │
│                                            │
│ ┌──────────────────────────────────────┐  │
│ │        🚪 Sign Out                   │  │ ← Destructive Action
│ └──────────────────────────────────────┘  │
│                                            │
└────────────────────────────────────────────┘
```

---

## 4. Component Library

### 4.1 Buttons

**Primary Button:**
```
┌─────────────────────┐
│   Save Feature  ✓   │  ← Primary Blue, White text, Bold
└─────────────────────┘
Padding: 12pt vertical, 24pt horizontal
Border Radius: 8pt
```

**Secondary Button:**
```
┌─────────────────────┐
│      Cancel         │  ← White bg, Primary Blue text, Border
└─────────────────────┘
Padding: 12pt vertical, 24pt horizontal
Border: 1pt Primary Blue
Border Radius: 8pt
```

**Destructive Button:**
```
┌─────────────────────┐
│  Delete Feature  ✗  │  ← Error Red, White text
└─────────────────────┘
```

**Icon Button:**
```
 [ 🔍 ]  ← Round, 44x44pt, Icon only
```

---

### 4.2 Form Inputs

**Text Input:**
```
Label Text
┌────────────────────────────────┐
│ Placeholder text...      🎤   │  ← Voice icon (optional)
└────────────────────────────────┘
Helper text or validation message

States:
- Default:  Border #DEE2E6
- Focus:    Border #0066CC, 2pt
- Error:    Border #DC3545, Red helper text
- Disabled: Background #F8F9FA, Gray text
```

**Dropdown:**
```
Dropdown Label
┌────────────────────────────────┐
│ Selected Option          ▼    │
└────────────────────────────────┘

Modal Picker (iOS) or Native Dropdown (Android)
```

**Number Input with Stepper:**
```
Height (feet)
┌──────────────────────────┐
│  [-]    35.0    [+]  📏 │  ← Stepper + Measure tool
└──────────────────────────┘
```

**Radio Button Group:**
```
Condition
┌────┬────┬────┬────┐
│Good│Fair│Poor│N/A │
└────┴────┴────┴────┘
  ✓                     ← Checkmark in selected
```

**Checkbox:**
```
☑ Include in report
☐ Send notification
```

**Date Picker:**
```
Inspection Date
┌────────────────────────────────┐
│ Nov 5, 2025             📅    │  ← Tappable, opens native picker
└────────────────────────────────┘
```

---

### 4.3 Map Components

**Feature Marker (Point):**
```
    📍  ← Icon (customizable)
  POLE-1247  ← Label (optional, zoom-dependent)
```

**GPS Cursor:**
```
     ▲
    ╱ ╲  ← Blue triangle pointing north
   ╱   ╲
  ───────
    ( )   ← Accuracy circle (semi-transparent blue)
```

**Measurement Line:**
```
  ●─────────●  ← Dashed line with distance label
     125.3m
```

**Cluster Marker:**
```
   ┌──────┐
   │  15  │  ← Number of clustered features
   └──────┘
```

---

### 4.4 Cards

**Collection Card:**
```
┌──────────────────────────────────────┐
│ 📍 Collection Name           125 ▸  │  ← Icon, Name, Count, Chevron
│ Subtitle or metadata          🟢    │  ← Status indicator
│ Additional info row                  │
└──────────────────────────────────────┘

Elevation: Level 1
Border Radius: 8pt
Padding: 16pt
Margin: 8pt vertical
```

**Feature Detail Card:**
```
┌──────────────────────────────────────┐
│ POLE-1247                            │  ← Feature ID
│ Utility Pole                         │  ← Feature Type
├──────────────────────────────────────┤
│ Height: 35 ft                        │  ← Attributes
│ Condition: Good                      │
│ Last Inspection: Nov 1, 2025         │
├──────────────────────────────────────┤
│ [📷 Photos (2)] [🗺️ View on Map]    │  ← Actions
└──────────────────────────────────────┘
```

---

### 4.5 Status Indicators

**Sync Status Badge:**
```
🟢 Synced        ← Green dot, no pending changes
🟡 Pending (3)   ← Yellow dot, with count
🔴 Offline       ← Red dot, not synced
🔵 Syncing...    ← Blue dot, animated
```

**GPS Accuracy Badge:**
```
📍 ±1.2m  🟢     ← Good (<5m) - Green
📍 ±12.5m 🟡     ← Fair (5-15m) - Yellow
📍 ±45.0m 🔴     ← Poor (>15m) - Red
```

**Loading Spinner:**
```
  ⟳  ← Animated, Primary Blue
Loading...
```

---

### 4.6 Modals & Dialogs

**Alert Dialog:**
```
┌───────────────────────────────────┐
│                                   │
│          ⚠️ Warning              │  ← Icon
│                                   │
│  Are you sure you want to        │
│  delete this feature?            │  ← Message
│                                   │
│  This action cannot be undone.   │  ← Subtitle
│                                   │
│  ┌──────────┐  ┌──────────────┐  │
│  │ Cancel   │  │ Delete ✓     │  │  ← Actions
│  └──────────┘  └──────────────┘  │
└───────────────────────────────────┘

Elevation: Level 3
Border Radius: 12pt
Background: Semi-transparent overlay
```

**Bottom Sheet (iOS) / Modal (Android):**
```
──────────────── (Drag handle)

Filter Features

☐ Utility Poles
☑ Street Trees
☐ Fire Hydrants

Distance: 500m  [────●────]

        [Apply Filters]
```

---

## 5. Interaction Patterns

### 5.1 Gestures

**Map Gestures:**
- **Tap:** Select feature
- **Double Tap:** Zoom in
- **Two-finger Tap:** Zoom out
- **Pan:** Move map
- **Pinch:** Zoom in/out
- **Two-finger Rotate:** Rotate map (if enabled)
- **Long Press:** Drop pin / Create feature

**List Gestures:**
- **Tap:** Open detail
- **Swipe Left:** Quick actions (Edit, Delete)
- **Swipe Right:** Alternative actions (Share, Duplicate)
- **Pull to Refresh:** Sync data

**Photo Gallery:**
- **Tap:** Full screen view
- **Swipe:** Navigate photos
- **Pinch:** Zoom photo
- **Long Press:** Show options menu

---

### 5.2 Transitions & Animations

**Screen Transitions:**
- **Push:** Slide from right (iOS) / Material motion (Android)
- **Modal:** Slide from bottom
- **Tab Switch:** Fade + subtle slide
- Duration: 300ms, ease-in-out curve

**Micro-interactions:**
- **Button Press:** Scale down to 0.95, 100ms
- **Toggle Switch:** Slide + color change, 200ms
- **Loading States:** Fade in spinner after 300ms delay
- **Success Feedback:** Checkmark animation + haptic

**Map Animations:**
- **Zoom:** Smooth ease-in-out, 500ms
- **Pan to Feature:** Ease-in-out with slight overshoot, 800ms
- **Marker Appear:** Scale from 0 to 1, 200ms
- **Cluster Expand:** Markers spread out in arc, 400ms

---

### 5.3 Feedback Mechanisms

**Visual Feedback:**
- Button press: Darker shade
- Selection: Highlight background
- Error: Shake animation + red border
- Success: Checkmark + green flash

**Haptic Feedback:**
- Button tap: Light impact
- Toggle switch: Selection change
- Error: Notification feedback
- Success: Success feedback
- Long press: Heavy impact

**Audio Feedback:**
- Voice command recognized: Subtle beep
- Photo captured: Shutter sound (if not muted)
- Sync complete: Success chime
- Error: Alert sound

---

### 5.4 Empty States

**No Data:**
```
┌────────────────────────────────────────┐
│                                        │
│           📭                           │
│                                        │
│      No features collected yet         │
│                                        │
│  Tap the [+] button to create your    │
│  first feature                         │
│                                        │
│      [📍 Create Feature]               │
│                                        │
└────────────────────────────────────────┘
```

**No Network:**
```
┌────────────────────────────────────────┐
│                                        │
│           📡                           │
│                                        │
│         No internet connection         │
│                                        │
│  Don't worry! You can still collect   │
│  data offline. Changes will sync when │
│  you're back online.                  │
│                                        │
│      [✓ Got it]                        │
│                                        │
└────────────────────────────────────────┘
```

**No Search Results:**
```
┌────────────────────────────────────────┐
│                                        │
│           🔍                           │
│                                        │
│      No features found                 │
│                                        │
│  Try adjusting your search or filters │
│                                        │
│      [Clear Filters]                   │
│                                        │
└────────────────────────────────────────┘
```

---

## 6. Accessibility

### 6.1 VoiceOver/TalkBack Support

**All interactive elements must have:**
- Descriptive labels (not just icons)
- Accessibility hints for non-obvious actions
- Proper focus order (top to bottom, left to right)
- Grouping of related elements

**Example:**
```xml
<Button
  Text="Save"
  AccessibilityLabel="Save feature"
  AccessibilityHint="Saves the current feature and returns to map"
  AccessibilityRole="Button" />
```

---

### 6.2 Text Scaling

**Support system text size settings:**
- Minimum: 12pt (at smallest setting)
- Maximum: 28pt (at largest setting)
- Test all UIs at 200% text scale

**Layout adjustments:**
- Use flexible layouts (not fixed heights)
- Multi-line text wrapping
- Scroll views where needed

---

### 6.3 Color Contrast

**WCAG AA Compliance:**
- Text on background: 4.5:1 minimum
- Large text (18pt+): 3:1 minimum
- Interactive elements: 3:1 against adjacent colors

**Never rely on color alone:**
- Use icons + color for status
- Use text labels + color
- Provide texture/patterns as backup

**Example:**
```
🟢 Online (with text)  ← Good
🟢                     ← Bad (color only)
```

---

### 6.4 Touch Targets

**Minimum Size:**
- 44x44 pt (iOS)
- 48x48 dp (Android)
- Applies to all tappable elements

**Spacing:**
- Minimum 8pt between adjacent targets
- Prefer 16pt+ for frequently used controls

---

## 7. Responsive Design

### 7.1 Breakpoints

**Phone (Portrait):**
- Width: 320-428 pt
- Layout: Single column, tab bar bottom

**Phone (Landscape):**
- Width: 568-926 pt
- Layout: Optional split view (if space allows)

**Tablet (Portrait):**
- Width: 768-834 pt
- Layout: Split view (master/detail)

**Tablet (Landscape):**
- Width: 1024-1366 pt
- Layout: Two-column, sidebar navigation

---

### 7.2 Adaptive Layouts

**Map Screen:**
- Phone: Full-screen map, floating controls
- Tablet: Map + side panel for feature list

**Feature Form:**
- Phone: Full-screen modal
- Tablet: Modal centered, 600pt max width

**Collections List:**
- Phone: Single column cards
- Tablet: Two-column grid

---

## 8. Dark Mode

### 8.1 Color Palette (Dark)

```
Background:      #000000 → #1C1C1E (True black → Dark gray)
Surface:         #FFFFFF → #2C2C2E (Cards, elevated)
Text Primary:    #212529 → #FFFFFF
Text Secondary:  #6C757D → #98989D
Border:          #DEE2E6 → #38383A

Primary Blue:    #0066CC → #0A84FF (Lighter for contrast)
Success Green:   #28A745 → #30D158
Warning Orange:  #FD7E14 → #FF9F0A
Error Red:       #DC3545 → #FF453A
```

### 8.2 Map in Dark Mode

- Basemap: Dark topographic or satellite
- Feature colors: Lighter/brighter versions
- GPS cursor: Brighter blue
- Labels: White text with dark halo

---

## 9. Platform-Specific Guidelines

### 9.1 iOS

**Navigation:**
- Large title navigation bars
- Swipe back gesture from left edge
- Pull-down to dismiss modals

**Components:**
- SF Symbols for icons
- Native date/time pickers
- Segmented controls for filters

---

### 9.2 Android

**Navigation:**
- Material toolbar
- Back button in navigation bar
- FAB for primary action

**Components:**
- Material icons
- Native pickers and dialogs
- Chips for tags/filters

---

## 10. Implementation Notes

### 10.1 .NET MAUI Specifics

**XAML Styling:**
```xml
<Application.Resources>
  <ResourceDictionary>
    <!-- Colors -->
    <Color x:Key="PrimaryBlue">#0066CC</Color>
    <Color x:Key="SuccessGreen">#28A745</Color>

    <!-- Styles -->
    <Style x:Key="H1" TargetType="Label">
      <Setter Property="FontSize" Value="28"/>
      <Setter Property="FontAttributes" Value="Bold"/>
    </Style>

    <Style x:Key="PrimaryButton" TargetType="Button">
      <Setter Property="BackgroundColor" Value="{StaticResource PrimaryBlue}"/>
      <Setter Property="TextColor" Value="White"/>
      <Setter Property="CornerRadius" Value="8"/>
      <Setter Property="Padding" Value="24,12"/>
    </Style>
  </ResourceDictionary>
</Application.Resources>
```

**Platform-specific:**
```xml
<OnPlatform x:TypeArguments="Thickness">
  <On Platform="iOS" Value="0,20,0,0"/> <!-- Safe area inset -->
  <On Platform="Android" Value="0"/>
</OnPlatform>
```

---

## 11. Next Steps

### Design Deliverables

1. ✅ UI/UX Specification (this document)
2. ⏳ High-fidelity mockups (Figma/Sketch)
3. ⏳ Interactive prototype
4. ⏳ Component library (.NET MAUI implementation)
5. ⏳ Accessibility audit checklist
6. ⏳ User testing plan

### Development Handoff

**Assets Needed:**
- Icon set (SF Symbols on iOS, Material Icons on Android)
- Map marker icons (SVG)
- Splash screen graphics
- App icon (multiple sizes)

**Documentation:**
- Component API documentation
- Interaction flow diagrams
- Animation specifications
- Accessibility guidelines

---

## Appendix A: Icon Reference

**Tab Bar Icons:**
- 🗺️ Map: `map.fill`
- 📍 Features: `location.fill`
- 📋 Tasks: `checklist`
- 🔄 Sync: `arrow.triangle.2.circlepath`
- ⚙️ Settings: `gearshape.fill`

**Action Icons:**
- ➕ Add: `plus.circle.fill`
- 📷 Photo: `camera.fill`
- 🎤 Voice: `mic.fill`
- 📏 Measure: `ruler.fill`
- 🧭 Compass: `location.north.fill`

**Status Icons:**
- 🟢 Online: `circle.fill` (green)
- 🔴 Offline: `circle.fill` (red)
- ⚠️ Warning: `exclamationmark.triangle.fill`
- ✓ Success: `checkmark.circle.fill`

---

**Document Status:** ✅ Complete
**Next Review:** After Phase 1 prototype
**Approvals Needed:** UX Designer, Product Manager, Engineering Lead
