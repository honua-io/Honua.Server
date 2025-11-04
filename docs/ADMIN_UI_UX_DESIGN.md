# Admin UI - User-Centered Design

**Date:** 2025-11-03
**Status:** UX Design & Information Architecture

---

## Executive Summary

The HonuaIO Admin UI serves GIS administrators managing complex geospatial services. This document applies user-centered design principles to create an intuitive, efficient interface that reduces cognitive load and supports both novice and expert workflows.

**Key Design Principles:**
- 🎯 **Task-Focused**: Optimize for the 5 most common tasks (80% of usage)
- 🧭 **Clear Navigation**: Always show context (folders → services → layers)
- 🔍 **Findability**: Multiple discovery paths (browse, search, AI assistance)
- ⚡ **Efficiency**: Minimize clicks for common operations
- 🎓 **Progressive Disclosure**: Simple by default, advanced when needed
- ♿ **Accessible**: WCAG 2.1 AA compliance, keyboard navigation

---

## User Research

### User Personas

#### Persona 1: "Sarah the GIS Administrator" (Primary)

**Role:** GIS Administrator at a municipal government
**Experience:** 5 years GIS, comfortable with desktop GIS tools (ArcGIS, QGIS)
**Technical Skill:** Moderate (knows SQL basics, not a developer)

**Goals:**
- ✅ Publish new WMS/WFS services quickly (daily task)
- ✅ Update layer styling without breaking existing services
- ✅ Troubleshoot why a service isn't appearing in QGIS
- ✅ Organize 200+ layers into logical folders
- ✅ Generate metadata reports for compliance

**Pain Points:**
- 😰 Afraid of breaking production services
- 🤔 Forgets where she put a layer (poor folder structure)
- ⏰ Spends 30 minutes finding the right CRS code
- 📝 Copy-pastes configs from old services (error-prone)
- 🆘 No visibility into why a service failed health checks

**Quote:** *"I just want to publish a new layer without worrying I'll break something. And please help me find things faster!"*

---

#### Persona 2: "Marcus the DevOps Engineer" (Secondary)

**Role:** DevOps Engineer at a SaaS company
**Experience:** 10 years software engineering, new to GIS
**Technical Skill:** High (Python, Docker, CI/CD)

**Goals:**
- ✅ Automate metadata changes via GitOps
- ✅ Monitor service health across environments
- ✅ Understand performance implications of configs
- ✅ Bulk import/export services for disaster recovery
- ✅ Integrate with existing observability tools

**Pain Points:**
- 🧩 GIS terminology is confusing (EPSG? SLD? WFS-T?)
- 🔧 Wants CLI/API access, not just clicking
- 📊 Needs metrics (cache hit rates, tile generation time)
- 🔄 Inconsistent config between dev/staging/prod
- 🚨 No alerting when a service degrades

**Quote:** *"Give me an API and good error messages. I'll automate the rest."*

---

#### Persona 3: "Kim the Data Publisher" (Tertiary)

**Role:** Environmental Scientist publishing research data
**Experience:** Domain expert, minimal GIS experience
**Technical Skill:** Low (Excel power user, basic GIS)

**Goals:**
- ✅ Publish a CSV file with lat/lon as a map service
- ✅ Style the map to show temperature ranges (red = hot, blue = cold)
- ✅ Share a public link with colleagues
- ✅ Update data monthly (replace existing dataset)
- ✅ Ensure data has proper attribution/license

**Pain Points:**
- 😵 Overwhelmed by options (WMS? WMTS? Vector tiles?)
- 🎨 Doesn't understand SLD styling syntax
- 📍 Confused about coordinate systems
- 🤷 Doesn't know if data is "valid" before publishing
- 🆘 No guidance on what settings to use

**Quote:** *"I just want to put my data on a map and share it. Why is this so complicated?"*

---

## User Journey Maps

### Journey 1: Publishing a New WMS Service (Sarah)

**Scenario:** Sarah received a shapefile from the Planning department. She needs to publish it as a WMS service for internal use by 5pm today.

| Step | User Action | Thoughts/Feelings | Pain Points | Design Opportunity |
|------|-------------|-------------------|-------------|-------------------|
| 1. **Arrive** | Opens Admin UI | "Okay, let's get this done quickly" | Where do I start? | **Clear entry point: "Add Service" button** |
| 2. **Create** | Clicks "New Service" → Selects WMS | "I've done this before..." | Too many options shown upfront | **Wizard: Ask intent first, then show relevant options** |
| 3. **Upload** | Uploads shapefile | "Hope this works..." | No preview before publish | **Show data preview + automatic validation** |
| 4. **Configure** | Sets CRS, name, abstract | "What CRS was this again?" | Has to look up EPSG code in separate tab | **CRS search with descriptions, detect from file** |
| 5. **Style** | Applies default style | "Good enough for now" | Default styling is ugly | **Smart defaults based on geometry type** |
| 6. **Organize** | Searches for "Planning" folder | "Where did I put the Planning folder?" | Folder picker doesn't remember last location | **Recent folders, breadcrumb navigation** |
| 7. **Test** | Wants to preview in QGIS | "How do I test this?" | Has to manually copy GetCapabilities URL | **"Test in QGIS" button copies URL to clipboard** |
| 8. **Publish** | Clicks "Publish" | "Please don't break anything..." 😰 | No confidence in validation | **Show validation results, preview changes** |
| 9. **Verify** | Checks service is live | ✅ "It worked!" 😊 | - | **Success message with next steps** |

**Total Time:** 12 minutes (Goal: <5 minutes)

**Key Insights:**
- 🎯 **Reduce cognitive load**: Use wizard for multi-step tasks
- 🔍 **Smart defaults**: Detect CRS, suggest styling, remember last folder
- ✅ **Build confidence**: Show validation, preview, testing tools
- ⚡ **Speed up common tasks**: Quick actions, keyboard shortcuts

---

### Journey 2: Troubleshooting a Broken Service (Sarah)

**Scenario:** A user reports that a WMS service isn't loading in their GIS client. Sarah needs to diagnose and fix it.

| Step | User Action | Thoughts/Feelings | Pain Points | Design Opportunity |
|------|-------------|-------------------|-------------|-------------------|
| 1. **Search** | Searches for service name | "What was it called again?" | Search doesn't find partial matches | **Fuzzy search, search by URL, recent items** |
| 2. **Inspect** | Opens service details | "Looks okay to me..." 🤔 | No obvious errors shown | **Health status indicator, last test results** |
| 3. **Test** | Clicks "Test Capabilities" | "Let's see what happens" | Test takes 30s, no feedback | **Real-time health checks with detailed logs** |
| 4. **Diagnose** | Sees error: "Data source unreachable" | "Oh no, did IT move the server?" | Cryptic error message | **Plain English errors with suggested fixes** |
| 5. **Fix** | Updates data source connection string | "Hope I got the hostname right..." | No way to test connection before saving | **"Test Connection" button** |
| 6. **Verify** | Re-tests service | ✅ "Fixed!" 😊 | Had to click 3 different buttons | **Auto-retest after save** |

**Total Time:** 15 minutes (Goal: <5 minutes)

**Key Insights:**
- 🔍 **Proactive monitoring**: Show health status on main page
- 🩺 **Better diagnostics**: Plain English errors, actionable suggestions
- ⚡ **Inline testing**: Test connection/style/health without leaving page
- 📊 **Visibility**: Show when service was last successfully accessed

---

### Journey 3: Bulk Organizing Layers (Sarah)

**Scenario:** Sarah inherited 200+ layers with no folder structure. She needs to organize them before her boss's review meeting tomorrow.

| Step | User Action | Thoughts/Feelings | Pain Points | Design Opportunity |
|------|-------------|-------------------|-------------|-------------------|
| 1. **Assess** | Views all layers (flat list) | "This is a mess..." 😰 | Scrolls through 10 pages | **Table view with filters, grouping** |
| 2. **Group** | Wants to select all "roads" layers | "There must be 20 road layers..." | Has to click each checkbox individually | **Multi-select, filter + "Select All"** |
| 3. **Move** | Drags to "Transportation" folder | "Did they all move?" | No visual feedback, can't undo | **Undo/redo, bulk move confirmation** |
| 4. **Repeat** | Does this 15 more times | "This is taking forever..." 😫 | Tedious, error-prone | **AI suggestion: "Group by keywords?"** |
| 5. **AI Assist** | Accepts AI grouping suggestion | "Let's try it..." | Doesn't trust AI completely | **Preview + manual override** |
| 6. **Review** | Checks folder structure | ✅ "Much better!" 😊 | - | **Tree view shows counts** |

**Total Time:** 45 minutes (Goal: <10 minutes with AI assist)

**Key Insights:**
- 🤖 **AI as assistant**: Suggest organization, don't auto-apply
- 🔧 **Bulk operations**: Multi-select, drag-drop, keyboard shortcuts
- ↩️ **Undo/redo**: Build confidence for bulk changes
- 📊 **Visualize structure**: Tree view with counts, tags

---

## Information Architecture

### Site Map

```
┌─────────────────────────────────────────────────────────────────┐
│ HonuaIO Admin                                    [Profile] [?]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐  ┌────────────────────────────────────┐   │
│  │                │  │                                      │   │
│  │  Primary Nav   │  │         Main Content Area           │   │
│  │  (Sidebar)     │  │                                      │   │
│  │                │  │  ┌────────────────────────────┐     │   │
│  │  📁 Services   │  │  │  Breadcrumbs / Context     │     │   │
│  │  📊 Data       │  │  └────────────────────────────┘     │   │
│  │  🎨 Styles     │  │                                      │   │
│  │  👥 Users      │  │  ┌────────────────────────────┐     │   │
│  │  ⚙️  Settings   │  │  │  TreeView / List / Detail  │     │   │
│  │  📈 Monitoring │  │  │                              │     │   │
│  │  📝 Logs       │  │  │  (Dynamic based on section)  │     │   │
│  │                │  │  │                              │     │   │
│  ├────────────────┤  │  │                              │     │   │
│  │                │  │  │                              │     │   │
│  │  🔍 Search     │  │  └────────────────────────────┘     │   │
│  │  [Filter...]   │  │                                      │   │
│  │                │  │                                      │   │
│  │  🤖 AI Chat    │  └──────────────────────────────────────┘   │
│  │  [Minimize]    │                                             │
│  │                │                                             │
│  └────────────────┘                                             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Navigation Hierarchy

```
Admin Home
│
├─── 📁 Services (Primary section)
│    ├─── 🌲 Folder Tree View (left pane)
│    │    ├─── 📂 Transportation
│    │    │    ├─── 🗺️  Roads WMS
│    │    │    ├─── 🗺️  Railways WFS
│    │    │    └─── 🗺️  Airports WMTS
│    │    ├─── 📂 Planning
│    │    └─── 📂 Environment
│    │
│    ├─── 📋 Service List (center pane)
│    │    ├─── Filters: [Type] [Status] [Modified]
│    │    ├─── Sort: [Name] [Created] [Author]
│    │    └─── Actions: [New] [Import] [Export]
│    │
│    └─── 📄 Service Detail (right pane / modal)
│         ├─── Tabs: [General] [Layers] [Security] [Health]
│         └─── Actions: [Edit] [Test] [Delete] [Clone]
│
├─── 📊 Data Sources
│    ├─── Connections (databases, files, cloud)
│    ├─── Health Status
│    └─── Import Jobs
│
├─── 🎨 Styles
│    ├─── Style Library
│    ├─── Style Editor (SLD/MapBox)
│    └─── Preview Gallery
│
├─── 👥 Users & Permissions
│    ├─── Users
│    ├─── Roles
│    └─── API Keys
│
├─── ⚙️  Settings
│    ├─── General
│    ├─── Providers (Postgres/Redis config)
│    ├─── Caching
│    └─── Publishing Workflow
│
├─── 📈 Monitoring
│    ├─── Dashboard (health overview)
│    ├─── Metrics (performance)
│    └─── Alerts
│
└─── 📝 Audit Logs
     ├─── Recent Changes
     ├─── Publishing History
     └─── User Activity
```

---

## Layout Patterns

### Pattern 1: Master-Detail (Recommended for Services)

**When to use:** Browsing a list of items, selecting one to view/edit details

**Layout:**
```
┌────────────────────────────────────────────────────────────────┐
│ Breadcrumbs: Home > Services > Transportation                  │
├─────────────┬──────────────────────────┬───────────────────────┤
│             │                          │                       │
│  Tree View  │    Service List          │   Detail Panel       │
│  (20%)      │    (40%)                 │   (40%)              │
│             │                          │                       │
│  📂 Root    │  🔍 [Search/Filter]      │  Roads WMS           │
│  📂 Trans   │  ───────────────────     │  ─────────────────   │
│    ├ Roads  │  ✅ Roads WMS            │  Status: 🟢 Healthy  │
│    ├ Rails  │     Modified: 2h ago     │  Type: WMS 1.3.0     │
│    └ Air    │     Layers: 3            │  Layers: 3           │
│  📂 Plan    │  ✅ Railways WFS         │                      │
│  📂 Env     │     Modified: 1d ago     │  [Edit] [Test]       │
│             │     Layers: 2            │  [Clone] [Delete]    │
│  [+ New]    │  ⚠️  Airports WMTS       │                      │
│             │     Modified: 3d ago     │  Tabs:               │
│             │     Health: Warning      │  [General] [Layers]  │
│             │                          │  [Security] [Health] │
│             │  [+ New Service]         │                      │
│             │                          │                      │
└─────────────┴──────────────────────────┴───────────────────────┘
```

**MudBlazor Components:**
- `MudTreeView` (left pane)
- `MudDataGrid` with filtering (center pane)
- `MudPaper` with `MudTabs` (right pane)

**Benefits:**
- ✅ See context (folder structure) while browsing
- ✅ Quick navigation between items
- ✅ No page reloads (SPA feel)

---

### Pattern 2: Wizard (for Complex Tasks)

**When to use:** Creating a new service, importing data, multi-step configuration

**Layout:**
```
┌────────────────────────────────────────────────────────────────┐
│ Create New Service                                        [X]   │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Step 1 of 4: Choose Service Type                             │
│  ●───────○───────○───────○                                    │
│  Type    Data    Style   Publish                              │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │              │  │              │  │              │       │
│  │    🗺️ WMS    │  │   📍 WFS     │  │   🎨 WMTS    │       │
│  │              │  │              │  │              │       │
│  │  Raster maps │  │ Vector data  │  │ Tiled maps   │       │
│  │  for display │  │ queryable    │  │ fast loading │       │
│  │              │  │              │  │              │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
│                                                                 │
│  Not sure? [Ask AI for recommendation]                        │
│                                                                 │
│                                   [Cancel]  [Next: Add Data >] │
└────────────────────────────────────────────────────────────────┘
```

**MudBlazor Components:**
- `MudStepper` (progress indicator)
- `MudCard` (option cards)
- Custom wizard component

**Benefits:**
- ✅ Reduces cognitive load (one decision at a time)
- ✅ Shows progress
- ✅ Can save draft and return later

---

### Pattern 3: Dashboard (for Monitoring)

**When to use:** Overview of system health, metrics, recent activity

**Layout:**
```
┌────────────────────────────────────────────────────────────────┐
│ Monitoring Dashboard                             Last 24 hours │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐           │
│  │ Services    │  │ Cache Hit   │  │ Requests    │           │
│  │    247      │  │    94%      │  │   1.2M      │           │
│  │ 🟢 243      │  │ ↗️ +2%      │  │ ↗️ +15%     │           │
│  │ 🟡 3        │  │             │  │             │           │
│  │ 🔴 1        │  │             │  │             │           │
│  └─────────────┘  └─────────────┘  └─────────────┘           │
│                                                                 │
│  Service Health by Type                                        │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ [Bar chart: WMS: 120🟢 2🟡, WFS: 80🟢 1🔴, WMTS: 47🟢]    │ │
│  └──────────────────────────────────────────────────────────┘ │
│                                                                 │
│  Recent Activity                         Alerts                │
│  ┌────────────────────────────┐  ┌────────────────────────┐  │
│  │ • Roads WMS updated (2m)   │  │ ⚠️  Airports WMTS      │  │
│  │ • New user: kim@env.gov    │  │   Data source timeout  │  │
│  │ • Planning WFS published   │  │   12 minutes ago       │  │
│  └────────────────────────────┘  └────────────────────────┘  │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

**MudBlazor Components:**
- `MudCard` with `MudChip` (stat cards)
- `MudChart` (charts)
- `MudTimeline` (activity feed)
- `MudAlert` (alert cards)

---

## Key UI Components

### 1. Search & Filter Bar

**Location:** Top of service list, always visible

**Features:**
```
┌────────────────────────────────────────────────────────────────┐
│ 🔍 Search services, layers, folders...                         │
│                                                                 │
│ Filters: [Type ▼] [Status ▼] [Modified ▼] [Author ▼] [Clear] │
│                                                                 │
│ Showing 23 of 247 services                                     │
└────────────────────────────────────────────────────────────────┘
```

**Search Capabilities:**
- 🔤 **Fuzzy matching**: "roeds" finds "Roads"
- 🏷️ **Tag search**: `#transportation` finds all tagged items
- 🗂️ **Path search**: `/Transportation/Roads` finds by folder path
- 📄 **Metadata search**: Search in abstracts, keywords
- 🔗 **URL search**: Paste GetCapabilities URL to find service

**MudBlazor Component:** `MudTextField` with `Adornment`, `MudMenu` for filters

---

### 2. Folder Tree View

**Location:** Left sidebar (Services section)

**Features:**
```
📂 All Services (247)
  ├─ 📂 Transportation (35)
  │   ├─ 🗺️  Roads WMS ✅
  │   ├─ 🗺️  Railways WFS ✅
  │   └─ 🗺️  Airports WMTS ⚠️
  ├─ 📂 Planning (89)
  │   ├─ 📂 Zoning (12)
  │   └─ 📂 Parcels (77)
  └─ 📂 Environment (123)
      └─ 📂 Water Quality (45)

[+ New Folder]  [+ New Service]
```

**Interactions:**
- ✅ **Drag & drop**: Drag service to folder to move
- 🔢 **Counts**: Show item count per folder
- 🎨 **Status icons**: Health status at a glance
- 🔽 **Expand/collapse**: Remember state per user
- 🔍 **Filter tree**: Hide/show based on search

**MudBlazor Component:** `MudTreeView` with custom item template

---

### 3. Service Health Indicator

**Location:** Everywhere (list view, detail view, tree view)

**Visual Design:**
```
Status Indicators:
🟢 Healthy       All systems operational
🟡 Warning       Minor issues (slow response, approaching quota)
🔴 Error         Critical failure (data source unreachable)
⚪ Unknown       Not tested yet
🔵 Testing       Health check in progress
```

**Hover Tooltip:**
```
┌──────────────────────────────────┐
│ Health Status: Warning           │
│                                  │
│ Last Checked: 5 minutes ago      │
│                                  │
│ Issues:                          │
│ • Data source response slow      │
│   (2.3s, threshold: 1s)          │
│ • Cache hit rate low (45%)       │
│                                  │
│ [Run Health Check] [View Logs]  │
└──────────────────────────────────┘
```

**MudBlazor Component:** `MudChip` with color, `MudPopover` for details

---

### 4. AI Chat Assistant

**Location:** Collapsible panel in left sidebar (below search)

**States:**

**Minimized:**
```
┌────────────────┐
│ 🤖 AI Assistant │  [Expand ▲]
└────────────────┘
```

**Expanded:**
```
┌────────────────────────────────────┐
│ 🤖 AI Assistant        [Minimize ▼] │
├────────────────────────────────────┤
│                                    │
│ AI: How can I help you today?      │
│                                    │
│ Suggestions:                       │
│ • Find all services without data   │
│ • Organize layers by theme         │
│ • Check for invalid CRS codes      │
│                                    │
├────────────────────────────────────┤
│ [Type a message...]           [↑] │
└────────────────────────────────────┘
```

**Conversation Flow:**
```
User: "Find all services that haven't been updated in 6 months"

AI: 🔍 Found 23 services not updated since May 2024:

    📂 Environment (12 services)
    • Water Quality WMS (last update: Jan 2024)
    • Air Quality WFS (last update: Feb 2024)
    ...

    [View Results] [Archive These] [Update Now]

User: "Which ones are still being used?"

AI: 📊 Analyzing request logs...

    Still Active (5 services):
    • Water Quality WMS - 1,234 requests/month
    ...

    Inactive (18 services):
    • Old Zoning WFS - 0 requests in 6 months
    ...

    [Archive Inactive] [Keep All]
```

**Capabilities:**
- 🔍 **Natural language search**: "Find roads in downtown"
- 📊 **Analytics**: "Which services get the most traffic?"
- 🎨 **Styling assistance**: "Make water layers blue"
- 🧹 **Cleanup suggestions**: "Find duplicate layers"
- 🩺 **Diagnostics**: "Why isn't this service working?"
- 📝 **Metadata generation**: "Write an abstract for this service"

**MudBlazor Component:** `MudPaper` with `MudList`, custom chat component

---

### 5. Breadcrumb Navigation

**Location:** Top of main content area

**Design:**
```
┌────────────────────────────────────────────────────────────────┐
│ Home > Services > Transportation > Roads WMS                   │
│                                                                 │
│ Or with actions:                                               │
│                                                                 │
│ Home > Services > Transportation > Roads WMS                   │
│                                            [Edit] [Test] [...]  │
└────────────────────────────────────────────────────────────────┘
```

**Features:**
- ✅ **Clickable segments**: Click any level to navigate up
- 🔗 **Copy path**: Right-click to copy full path
- 📱 **Responsive**: Collapse to "... > Transportation > Roads WMS" on mobile

**MudBlazor Component:** `MudBreadcrumbs`

---

### 6. Action Buttons

**Design System:**

**Primary Actions** (blue, filled):
```
[Publish Service]  [Save Changes]  [Create]
```

**Secondary Actions** (outlined):
```
[Test in QGIS]  [Preview]  [Clone]
```

**Danger Actions** (red):
```
[Delete Service]  [Revoke Access]
```

**Icon-Only Actions** (for space-constrained areas):
```
[✏️ Edit] [🗑️ Delete] [📋 Clone] [⚙️ Settings]
```

**Grouped Actions** (dropdown menu):
```
[More ▼]
  ├─ Export
  ├─ Archive
  └─ View History
```

**MudBlazor Components:**
- `MudButton` with `Variant`, `Color`
- `MudIconButton`
- `MudMenu` for grouped actions

---

## Responsive Layouts

### Desktop (1920x1080 - Optimal)

```
┌─────────────────────────────────────────────────────────────────┐
│ Header (60px)                                                   │
├──────────┬────────────────────────────┬─────────────────────────┤
│          │                            │                         │
│ Sidebar  │    Service List            │   Detail Panel          │
│ (280px)  │    (flex)                  │   (480px)               │
│          │                            │                         │
│ Tree     │    Grid with filters       │   Tabs + Form           │
│ Search   │    Multi-select            │   Actions               │
│ AI Chat  │    Bulk actions            │   Preview               │
│          │                            │                         │
└──────────┴────────────────────────────┴─────────────────────────┘
```

**Layout:** Three-column master-detail with tree view

---

### Laptop (1366x768 - Common)

```
┌─────────────────────────────────────────────────────────────────┐
│ Header (60px)                                                   │
├──────────┬──────────────────────────────────────────────────────┤
│          │                                                       │
│ Sidebar  │    Service List + Detail (stacked or modal)          │
│ (260px)  │                                                       │
│          │    Grid with fewer columns                           │
│ Tree     │    Click item → opens detail modal                   │
│ Search   │                                                       │
│ AI Chat  │                                                       │
│ (min)    │                                                       │
│          │                                                       │
└──────────┴──────────────────────────────────────────────────────┘
```

**Layout:** Two-column, detail opens in modal or slides in from right

---

### Tablet (768x1024 - Optional)

```
┌─────────────────────────────────────────┐
│ Header (60px)                [☰ Menu]  │
├─────────────────────────────────────────┤
│                                         │
│    Service List                         │
│    (full width)                         │
│                                         │
│    Card-based layout                    │
│    Tap card → opens detail page         │
│                                         │
│                                         │
│                                         │
└─────────────────────────────────────────┘

(Sidebar collapsed into hamburger menu)
(AI chat accessible via FAB button)
```

**Layout:** Single-column card-based

---

### Mobile (375x667 - Stretch Goal)

**Decision:** Admin UI is NOT optimized for mobile (management tools rarely are). Instead:

```
┌───────────────────────────────┐
│ HonuaIO Admin                  │
│ ──────────────────────────────│
│                               │
│  This interface is best       │
│  viewed on a desktop or       │
│  tablet.                      │
│                               │
│  Quick Actions:               │
│  • View service status        │
│  • Restart failing service    │
│  • View recent logs           │
│                               │
│  [Open Full Interface]        │
│                               │
└───────────────────────────────┘
```

**Rationale:** Focus on desktop UX (where 95% of usage happens). Provide mobile-optimized "emergency" views only.

---

## Task-Based UI Flows

### Flow 1: "Create WMS Service" (Optimized for Sarah)

**Entry Points:**
1. Click "New Service" button (always visible)
2. Right-click folder → "Add Service Here"
3. Ask AI: "Create a new WMS service"

**Wizard Steps:**

**Step 1: What are you publishing?**
```
┌────────────────────────────────────────────────────────────────┐
│ Create New Service                                        [X]   │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Step 1 of 4: What are you publishing?                         │
│ ●───────○───────○───────○                                     │
│                                                                 │
│ ┌─────────────────────────────────────────────────────────┐   │
│ │                                                           │   │
│ │  📂 Upload File          🔗 Connect to Database         │   │
│ │  ┌──────────────┐         ┌──────────────┐              │   │
│ │  │ Drag & drop  │         │ PostGIS      │              │   │
│ │  │ or click     │         │ Oracle       │              │   │
│ │  │              │         │ SQL Server   │              │   │
│ │  └──────────────┘         └──────────────┘              │   │
│ │                                                           │   │
│ │  Supported: .shp, .gpkg, .geojson, .csv (with lat/lon)  │   │
│ │                                                           │   │
│ └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│                                                [Cancel] [Next >]│
└────────────────────────────────────────────────────────────────┘
```

**Step 2: Configure Data**
```
┌────────────────────────────────────────────────────────────────┐
│ Step 2 of 4: Configure Data                                    │
│ ○───────●───────○───────○                                     │
│                                                                 │
│ 📄 File: roads.shp (uploaded)                                  │
│                                                                 │
│ ✅ Data Preview (first 10 features):                           │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ [Mini map showing data preview]                           │  │
│ │                                                            │  │
│ │ Detected: 1,234 features, LineString geometry            │  │
│ │ Bounds: [-122.5, 37.7] to [-122.3, 37.9]                │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│ Coordinate System:                                             │
│ 🔍 [EPSG:4326 - WGS 84                            ▼]          │
│    ℹ️ Auto-detected from file                                  │
│                                                                 │
│ Attributes (5 columns):                                        │
│ ✅ name (text)     ✅ road_type (text)    ✅ lanes (number)    │
│ ✅ speed_limit (number)    ✅ last_updated (date)              │
│                                                                 │
│                                           [< Back] [Next: Style >]│
└────────────────────────────────────────────────────────────────┘
```

**Step 3: Style (with smart defaults)**
```
┌────────────────────────────────────────────────────────────────┐
│ Step 3 of 4: Apply Styling                                     │
│ ○───────○───────●───────○                                     │
│                                                                 │
│ 🎨 Quick Styles (recommended):                                 │
│                                                                 │
│ ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│ │ ● Default    │  │   By Type    │  │   By Speed   │         │
│ │              │  │              │  │              │         │
│ │ Simple line  │  │ Classify by  │  │ Color ramp   │         │
│ │ (2px, gray)  │  │ road_type    │  │ by speed     │         │
│ │              │  │              │  │              │         │
│ │ [Preview]    │  │ [Preview]    │  │ [Preview]    │         │
│ └──────────────┘  └──────────────┘  └──────────────┘         │
│                                                                 │
│ Preview:                                                        │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ [Map preview with styled roads]                           │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│ Advanced: [Open Style Editor (SLD)]                           │
│                                                                 │
│                                    [< Back] [Next: Publish >]  │
└────────────────────────────────────────────────────────────────┘
```

**Step 4: Publish (with validation)**
```
┌────────────────────────────────────────────────────────────────┐
│ Step 4 of 4: Publish Service                                   │
│ ○───────○───────○───────●                                     │
│                                                                 │
│ Service Details:                                               │
│                                                                 │
│ Name:        [Roads - Downtown                        ]        │
│ Title:       [Downtown Road Network                   ]        │
│ Abstract:    [Street network for downtown area        ]        │
│              [covering arterials, collectors, local   ]        │
│                                                                 │
│ Location:    🔍 [📂 Transportation / Roads            ▼]       │
│              ℹ️ Remembers your last used folder                │
│                                                                 │
│ Validation Results:                                            │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ ✅ Data source accessible                                 │  │
│ │ ✅ Coordinate system valid (EPSG:4326)                   │  │
│ │ ✅ Geometry valid (1,234 features checked)               │  │
│ │ ✅ Style renders successfully                            │  │
│ │ ⚠️  Warning: No caching enabled (may be slow for large   │  │
│ │    datasets). [Enable Caching]                           │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│ Test Before Publishing:                                        │
│ [Copy GetCapabilities URL]  [Test in QGIS]  [Preview in Map] │
│                                                                 │
│                                    [< Back] [Publish Service]  │
└────────────────────────────────────────────────────────────────┘
```

**Success Confirmation:**
```
┌────────────────────────────────────────────────────────────────┐
│ ✅ Service Published Successfully!                             │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ "Roads - Downtown" is now live at:                            │
│                                                                 │
│ 🌐 https://gis.yourorg.com/wms?SERVICE=WMS&...                │
│    [Copy URL] [Test in QGIS] [Share Link]                     │
│                                                                 │
│ Next Steps:                                                    │
│ • [Add more layers to this service]                           │
│ • [Configure caching for better performance]                  │
│ • [Set up access control]                                     │
│                                                                 │
│                              [View Service] [Create Another]   │
└────────────────────────────────────────────────────────────────┘
```

**Total Time:** ~3-4 minutes (vs. 12 minutes before)

---

### Flow 2: "Find and Fix Broken Service" (Sarah's Pain Point)

**Entry Point:** Service health alert or monitoring dashboard

**Optimized Flow:**

**1. Dashboard Alert (Proactive)**
```
┌────────────────────────────────────┐
│ 🔴 Alert: Service Unhealthy        │
│                                    │
│ Airports WMTS stopped responding   │
│ 15 minutes ago                     │
│                                    │
│ Last error: "Connection timeout"   │
│                                    │
│ [Investigate] [Dismiss]            │
└────────────────────────────────────┘
```

**2. Click "Investigate" → Opens Detail View**
```
┌────────────────────────────────────────────────────────────────┐
│ Airports WMTS                           Status: 🔴 Unhealthy   │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Tabs: [General] [Layers] [Security] [Health] [Logs]           │
│                                          ─────                  │
│                                                                 │
│ Health Status: 🔴 Critical                                     │
│                                                                 │
│ Current Issues:                                                │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 🔴 Data source unreachable                                │  │
│ │    Connection timeout after 30 seconds                    │  │
│ │    postgres://oldserver.local:5432/gis                    │  │
│ │                                                            │  │
│ │    💡 Suggested Fix:                                      │  │
│ │    • Check if database server is running                  │  │
│ │    • Verify hostname (did server migrate?)                │  │
│ │    • Test connection: [Test Connection]                   │  │
│ │                                                            │  │
│ │    [Update Connection String]                             │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│ History:                                                       │
│ • ✅ Healthy (95 requests/min) - 20 mins ago                   │
│ • 🟡 Slow response (3.2s avg) - 25 mins ago                   │
│ • 🔴 Timeout - 15 mins ago                                    │
│                                                                 │
│ [View Full Logs]                      [Fix Data Source]        │
└────────────────────────────────────────────────────────────────┘
```

**3. Click "Fix Data Source" → Inline Editor**
```
┌────────────────────────────────────────────────────────────────┐
│ Update Data Source                                        [X]   │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Current (failing):                                             │
│ postgres://oldserver.local:5432/gis                            │
│                                                                 │
│ New connection string:                                         │
│ [postgres://newserver.local:5432/gis                    ]      │
│                                                                 │
│ [Test Connection]  Status: ⏳ Testing...                       │
│                                                                 │
│ ✅ Connection successful! (127ms)                              │
│    Found table: airports (234 features)                        │
│                                                                 │
│                                    [Cancel] [Save & Republish] │
└────────────────────────────────────────────────────────────────┘
```

**4. After Save → Auto-Test**
```
┌────────────────────────────────────────────────────────────────┐
│ ✅ Service Fixed!                                              │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Re-testing service health...                                   │
│                                                                 │
│ ✅ Data source accessible (98ms)                               │
│ ✅ All layers rendering correctly                              │
│ ✅ Service responding (142ms avg)                              │
│                                                                 │
│ Status: 🟢 Healthy                                             │
│                                                                 │
│                                             [Done] [View Logs] │
└────────────────────────────────────────────────────────────────┘
```

**Total Time:** ~2-3 minutes (vs. 15 minutes before)

---

### Flow 3: "Bulk Organize with AI" (Sarah's Cleanup Task)

**Entry Point:** Service list view, many unorganized items

**Optimized Flow:**

**1. Select Multiple Items**
```
┌────────────────────────────────────────────────────────────────┐
│ Services > All (Unorganized: 187)                              │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ 🔍 [Search...]  Filters: [All Types] [Unorganized]            │
│                                                                 │
│ [Select All]  [Select by...▼]  Selected: 0                    │
│                  ├─ Pattern                                    │
│                  ├─ Type                                       │
│                  └─ 🤖 AI Smart Select                         │
│                                                                 │
│ ☐  Roads_Main_Street_WMS                                      │
│ ☐  Roads_Highway_101_WFS                                      │
│ ☐  Roads_Local_Streets_WMTS                                   │
│ ☐  Water_Quality_Sampling_WMS                                 │
│ ☐  Water_Distribution_Network_WFS                             │
│ ☐  Zoning_Residential_WMS                                     │
│ ...                                                            │
│                                                                 │
│ [Move to Folder]  [Delete]  [Export]  [🤖 AI Organize]        │
└────────────────────────────────────────────────────────────────┘
```

**2. Click "AI Organize" → AI Analyzes**
```
┌────────────────────────────────────────────────────────────────┐
│ 🤖 AI Organization Assistant                              [X]   │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Analyzing 187 services...                                      │
│                                                                 │
│ ✅ Found patterns in service names and metadata                │
│                                                                 │
│ Suggested Organization:                                        │
│                                                                 │
│ 📂 Transportation (23 services)                                │
│   ├─ 📂 Roads (18)                                             │
│   │   • Roads_Main_Street_WMS                                  │
│   │   • Roads_Highway_101_WFS                                  │
│   │   • Roads_Local_Streets_WMTS                               │
│   │   • ... 15 more                                            │
│   ├─ 📂 Transit (3)                                            │
│   └─ 📂 Aviation (2)                                           │
│                                                                 │
│ 📂 Environment (45 services)                                   │
│   ├─ 📂 Water Quality (12)                                     │
│   │   • Water_Quality_Sampling_WMS                             │
│   │   • ... 11 more                                            │
│   ├─ 📂 Water Infrastructure (8)                               │
│   │   • Water_Distribution_Network_WFS                         │
│   │   • ... 7 more                                             │
│   └─ ... 4 more subcategories                                 │
│                                                                 │
│ 📂 Planning (34 services)                                      │
│   ├─ 📂 Zoning (15)                                            │
│   └─ ... 2 more subcategories                                 │
│                                                                 │
│ 📂 Uncategorized (12 services - needs review)                 │
│                                                                 │
│                                                                 │
│ [Edit Suggestions]  [Cancel]  [Apply Organization]            │
└────────────────────────────────────────────────────────────────┘
```

**3. Review & Adjust (Optional)**
```
┌────────────────────────────────────────────────────────────────┐
│ Review AI Suggestions                                          │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ You can drag services to different folders:                    │
│                                                                 │
│ 📂 Transportation (23) ✅                                      │
│   ├─ 📂 Roads (18) ✅                                          │
│   ├─ 📂 Transit (3) ✅                                         │
│   └─ 📂 Aviation (2) ✅                                        │
│                                                                 │
│ 📂 Uncategorized (12) - Needs Your Input                      │
│   • Historic_Downtown_Map ─┐                                   │
│   • Old_CityBoundary_1990  │ Drag to correct folder           │
│   • Test_Service_123       │                                   │
│   • ...                    ┘                                   │
│                                                                 │
│ [Accept All] [Skip Uncategorized] [Cancel]                    │
└────────────────────────────────────────────────────────────────┘
```

**4. Apply Changes (with Undo)**
```
┌────────────────────────────────────────────────────────────────┐
│ ✅ Organization Applied!                                       │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Moved 175 services into 3 main folders:                       │
│                                                                 │
│ • 📂 Transportation (23 services)                              │
│ • 📂 Environment (45 services)                                 │
│ • 📂 Planning (34 services)                                    │
│                                                                 │
│ 12 services remain uncategorized (needs review)                │
│                                                                 │
│ [↩️ Undo Organization]                      [Done]             │
└────────────────────────────────────────────────────────────────┘
```

**Total Time:** ~5-10 minutes (vs. 45 minutes manual)

---

## Accessibility & Usability

### WCAG 2.1 AA Compliance

**Color Contrast:**
```
Text on Background: 4.5:1 minimum
Large Text: 3:1 minimum
Interactive Elements: 3:1 minimum

MudBlazor Theme:
- Primary: #594AE2 (purple) - WCAG AAA on white
- Success: #00C853 (green) - WCAG AA
- Warning: #FB8C00 (orange) - WCAG AA
- Error: #FF5252 (red) - WCAG AA
```

**Keyboard Navigation:**
- ✅ **Tab order**: Logical flow (sidebar → main content → detail panel)
- ✅ **Shortcuts**: Define keyboard shortcuts for common actions
  - `Ctrl+N`: New service
  - `Ctrl+F`: Focus search
  - `Ctrl+S`: Save current item
  - `/`: Focus AI chat
  - `Esc`: Close modals
- ✅ **Skip links**: "Skip to main content" for screen readers
- ✅ **Focus indicators**: Clear visual focus (2px outline)

**Screen Reader Support:**
- ✅ **ARIA labels**: All interactive elements labeled
- ✅ **Landmarks**: `<nav>`, `<main>`, `<aside>` for structure
- ✅ **Live regions**: Announce status changes (`aria-live="polite"`)
- ✅ **Alt text**: All icons have text alternatives

**Example Implementation:**
```razor
<MudButton Variant="Variant.Filled"
           Color="Color.Primary"
           aria-label="Create new WMS service"
           @onclick="CreateService">
    <MudIcon Icon="@Icons.Material.Filled.Add" aria-hidden="true" />
    New Service
</MudButton>
```

---

## Performance & Optimization

### Virtual Scrolling (for Large Lists)

**Problem:** Rendering 1,000+ services lags the UI

**Solution:** MudBlazor `MudVirtualize`

```razor
<MudVirtualize Items="@_allServices"
               Context="service"
               OverscanCount="5">
    <ServiceListItem Service="@service" />
</MudVirtualize>
```

**Result:** Only renders visible items + 5 buffer (60fps with 10,000 items)

---

### Lazy Loading (for Detail Panels)

**Problem:** Loading all layer metadata upfront is slow

**Solution:** Load on-demand

```razor
<MudTabs>
    <MudTabPanel Text="General">
        <!-- Always loaded -->
    </MudTabPanel>
    <MudTabPanel Text="Layers" OnClick="@LoadLayersAsync">
        @if (_layersLoaded)
        {
            <LayerList Layers="@_layers" />
        }
        else
        {
            <MudProgressCircular Indeterminate="true" />
        }
    </MudTabPanel>
</MudTabs>
```

---

### Debounced Search

**Problem:** Search API called on every keystroke

**Solution:** Debounce 300ms

```csharp
private Timer? _searchDebounceTimer;

private void OnSearchChanged(string searchTerm)
{
    _searchDebounceTimer?.Dispose();
    _searchDebounceTimer = new Timer(async _ =>
    {
        await PerformSearchAsync(searchTerm);
    }, null, 300, Timeout.Infinite);
}
```

---

## Metrics for Success

### Quantitative Metrics

| Metric | Current (estimated) | Target | Measurement |
|--------|---------------------|--------|-------------|
| **Time to publish new service** | 12 minutes | <5 minutes | Task completion time |
| **Time to find a service** | 2-3 minutes | <30 seconds | Search-to-click time |
| **Clicks to complete common task** | 8-12 clicks | <5 clicks | Click tracking |
| **Error rate (broken services)** | 15% | <5% | Validation pass rate |
| **User-reported issues** | 10/month | <3/month | Support tickets |
| **AI assistance usage** | N/A | >30% of sessions | Feature adoption |

### Qualitative Metrics

| Metric | Measurement Method | Target |
|--------|-------------------|--------|
| **User satisfaction** | Post-task survey (1-5 stars) | >4.0 average |
| **Perceived ease of use** | SUS (System Usability Scale) | >70 (good) |
| **Confidence in changes** | Survey: "I feel confident this won't break production" | >80% agree |
| **AI trust** | Survey: "I trust AI suggestions" | >60% agree |

### Usability Testing Protocol

**Participants:** 5-8 users from each persona (Sarah, Marcus, Kim)

**Tasks:**
1. Create a new WMS service from a shapefile
2. Find and fix a broken service
3. Organize 20 unorganized layers using AI
4. Generate a metadata report for compliance

**Method:** Moderated remote usability testing (Zoom + screen share)

**Metrics Collected:**
- Task success rate
- Time on task
- Error count
- Subjective satisfaction (SEQ - Single Ease Question)
- Think-aloud observations

**Iteration:** Test → Fix → Re-test (2-week sprints)

---

## Implementation Roadmap

### Phase 1: Core Layout & Navigation (Weeks 1-2)

**Components:**
- ✅ Main layout (header, sidebar, content area)
- ✅ Folder tree view (MudTreeView)
- ✅ Service list (MudDataGrid with filters)
- ✅ Breadcrumb navigation
- ✅ Search bar (fuzzy search)
- ✅ Health status indicators

**Deliverable:** Users can browse, search, and view services

---

### Phase 2: CRUD Operations (Weeks 3-4)

**Components:**
- ✅ Create service wizard (4 steps)
- ✅ Edit service form
- ✅ Delete confirmation
- ✅ Clone service
- ✅ Validation & health checks

**Deliverable:** Users can manage services end-to-end

---

### Phase 3: Bulk Operations & Organization (Week 5)

**Components:**
- ✅ Multi-select (checkboxes)
- ✅ Drag & drop to folders
- ✅ Bulk move/delete/export
- ✅ Undo/redo stack

**Deliverable:** Users can efficiently organize large numbers of services

---

### Phase 4: AI Integration (Week 6-7)

**Components:**
- ✅ AI chat sidebar
- ✅ Natural language search
- ✅ AI organization suggestions
- ✅ Metadata generation
- ✅ Diagnostics assistance

**Deliverable:** AI reduces cognitive load and speeds up common tasks

---

### Phase 5: Monitoring & Observability (Week 8)

**Components:**
- ✅ Dashboard with health overview
- ✅ Metrics charts (requests, cache hit rate)
- ✅ Alerts & notifications
- ✅ Audit logs

**Deliverable:** Proactive monitoring reduces downtime

---

### Phase 6: Advanced Features (Weeks 9-10)

**Components:**
- ✅ Style editor (SLD/MapBox)
- ✅ Metadata editor (ISO 19115)
- ✅ Role-based access control UI
- ✅ API key management
- ✅ Export/import workflows

**Deliverable:** Power users can perform advanced operations

---

## Design System & Component Library

### MudBlazor Component Mapping

| UI Element | MudBlazor Component | Props/Configuration |
|------------|---------------------|---------------------|
| **Layout** | `MudLayout` + `MudDrawer` | `Variant="Temporary"` for mobile |
| **Tree View** | `MudTreeView<T>` | Custom `ItemTemplate` for status icons |
| **Data Grid** | `MudDataGrid<T>` | `Filterable`, `Sortable`, `MultiSelection` |
| **Search** | `MudTextField` | `Adornment="Start"`, `Icon="Icons.Search"` |
| **Breadcrumbs** | `MudBreadcrumbs` | `Items` bound to navigation stack |
| **Tabs** | `MudTabs` | `Position="Position.Top"` |
| **Cards** | `MudCard` | For dashboard stats, service cards |
| **Buttons** | `MudButton` / `MudIconButton` | `Variant`, `Color` for hierarchy |
| **Forms** | `MudForm` | With `MudTextField`, `MudSelect`, etc. |
| **Validation** | `MudForm` with `Validation` | FluentValidation integration |
| **Modals** | `MudDialog` | For confirmations, wizards |
| **Notifications** | `MudSnackbar` | Success/error toasts |
| **Progress** | `MudProgressCircular` | For loading states |
| **Charts** | `MudChart` | Bar, line, donut for dashboard |
| **Menu** | `MudMenu` | For "More actions" dropdowns |
| **Popover** | `MudPopover` | For health status details |

### Color Palette

```csharp
// Theme configuration in Program.cs
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
});

// Custom theme (wwwroot/css/custom-theme.css)
:root {
    --mud-palette-primary: #594AE2;        /* HonuaIO purple */
    --mud-palette-secondary: #00BCD4;      /* Cyan for accents */
    --mud-palette-success: #00C853;        /* Green for healthy */
    --mud-palette-warning: #FB8C00;        /* Orange for warnings */
    --mud-palette-error: #FF5252;          /* Red for errors */
    --mud-palette-info: #2196F3;           /* Blue for info */

    /* Semantic colors */
    --color-healthy: #00C853;
    --color-warning: #FB8C00;
    --color-error: #FF5252;
    --color-unknown: #9E9E9E;
}
```

---

## Next Steps

### Immediate Actions:

1. **User Research** (Week 1):
   - [ ] Interview 3-5 GIS administrators (Sarah persona)
   - [ ] Survey existing users about pain points
   - [ ] Analyze support tickets for common issues

2. **Wireframing** (Week 1-2):
   - [ ] Create clickable prototypes in Figma
   - [ ] Get feedback from 2-3 users per persona
   - [ ] Iterate based on feedback

3. **Development** (Weeks 2-10):
   - [ ] Follow phased roadmap above
   - [ ] Weekly usability testing sessions
   - [ ] Continuous iteration

4. **Beta Testing** (Week 11-12):
   - [ ] Invite 10-15 users to closed beta
   - [ ] Collect metrics (task time, errors, satisfaction)
   - [ ] Fix critical issues

5. **Launch** (Week 13):
   - [ ] Gradual rollout (10% → 50% → 100% of users)
   - [ ] Monitor metrics dashboard
   - [ ] Support rapid response team

### Success Criteria for Launch:

- ✅ 80% of users complete primary tasks without assistance
- ✅ <5% error rate on service creation
- ✅ >4.0 average satisfaction score
- ✅ 50% reduction in support tickets related to UI confusion

---

## Appendix: User Interview Script

### Introduction (5 minutes)

"Hi [Name], thank you for taking the time to speak with me today. I'm working on improving the HonuaIO admin interface, and your feedback will help us design a tool that works better for GIS administrators like you.

This is not a test of you - we're testing our designs. There are no right or wrong answers. Please think aloud as you work through tasks, and be as honest as possible about what works and what doesn't.

Do you have any questions before we begin?"

### Warm-Up Questions (5 minutes)

1. Tell me about your role. What does a typical day look like?
2. How often do you publish or update geospatial services?
3. What tools do you currently use for this? (ArcGIS Server Manager, GeoServer admin, etc.)
4. What do you like about your current workflow?
5. What frustrates you the most?

### Task Scenarios (30 minutes)

**Task 1: Publishing a New Service**

"Imagine you've just received a shapefile of new zoning boundaries from your planning department. You need to publish this as a WFS service so planners can query it from QGIS. Walk me through how you would do this."

**Observation Points:**
- Where do they expect to find "Create Service"?
- Do they understand the difference between WMS/WFS/WMTS?
- What causes confusion or hesitation?
- Do they want to preview data before publishing?

**Task 2: Finding an Existing Service**

"A user reports that a service isn't loading. You need to find the 'Historic Districts WMS' service to troubleshoot it. How would you find it?"

**Observation Points:**
- Do they use search or browse folders?
- What search keywords do they try?
- How do they expect results to be sorted?

**Task 3: Organizing Layers**

"You have 50 unorganized services that need to be grouped into folders by theme (Transportation, Environment, Planning). How would you approach this?"

**Observation Points:**
- Do they expect to select multiple items?
- Do they try to drag & drop?
- Would AI assistance be welcome or intrusive?

### Closing Questions (10 minutes)

1. If you could change one thing about managing geospatial services, what would it be?
2. How would you feel about an AI assistant that could suggest organization or diagnose issues?
3. Is there anything else you'd like to see in an admin interface?

### Thank You (2 minutes)

"Thank you so much for your time and insights. This feedback is incredibly valuable and will directly influence our design. If you'd like to participate in future testing sessions, please let me know!"

---

## Interactive Tours & Tutorials

### Tour System Overview

**Goal:** Reduce time-to-productivity for new users and increase feature discovery for existing users through contextual, interactive guidance.

**Design Principles:**
- 🎯 **Context-Aware**: Show tours when relevant (first login, new feature release, on-demand)
- ⏭️ **Skippable**: Always allow users to skip or dismiss
- 🎓 **Progressive**: Start with basics, offer advanced tours later
- 📍 **Focused**: Highlight specific UI elements with dimmed background
- 🔄 **Repeatable**: Users can replay tours at any time

---

### Tour Trigger Mechanisms

#### 1. First-Time User Experience (FTUE)

**Trigger:** User's first login to Admin UI

**Flow:**
```
┌────────────────────────────────────────────────────────────────┐
│ 👋 Welcome to HonuaIO Admin!                              [X]  │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Let's get you started with a quick tour.                      │
│                                                                 │
│ You'll learn how to:                                           │
│ ✅ Navigate the interface                                      │
│ ✅ Publish your first service                                  │
│ ✅ Use the AI assistant                                        │
│                                                                 │
│ Takes about 2 minutes.                                         │
│                                                                 │
│                        [Skip Tour] [Start Tour (2 min) →]     │
└────────────────────────────────────────────────────────────────┘
```

#### 2. Feature Discovery Tours

**Trigger:** New feature released (announced via banner)

**Example:**
```
┌────────────────────────────────────────────────────────────────┐
│ 🎉 New Feature: AI-Powered Organization                        │
│                                                                 │
│ Let AI automatically organize your services into folders.      │
│                                                                 │
│ [Learn More (1 min tour)] [Dismiss]                           │
└────────────────────────────────────────────────────────────────┘
```

#### 3. On-Demand Tours

**Trigger:** User clicks "Help" menu or question mark icon

**Location:** Top-right toolbar

**Menu:**
```
┌────────────────────────────────┐
│ ❓ Help                         │
├────────────────────────────────┤
│ 🎓 Interactive Tours          │
│   ├─ Getting Started           │
│   ├─ Publishing Services       │
│   ├─ Using AI Assistant        │
│   ├─ Troubleshooting Issues    │
│   ├─ Bulk Operations           │
│   └─ Advanced Features         │
│                                │
│ 📖 Documentation               │
│ 💬 Contact Support             │
│ 🐛 Report Issue                │
└────────────────────────────────┘
```

#### 4. Contextual Help

**Trigger:** User appears stuck (e.g., 30 seconds on a page with no interaction)

**Passive Assistance:**
```
┌────────────────────────────────┐
│ 💡 Need help?                  │
│                                │
│ I noticed you're on the        │
│ service creation page.         │
│                                │
│ [Show me how to publish] [No] │
└────────────────────────────────┘
```

---

### Tour Design Patterns

#### Pattern 1: Spotlight Tour (Primary)

**Visual Design:**
```
┌─────────────────────────────────────────────────────────────────┐
│ [DIMMED OVERLAY - 80% opacity black]                            │
│                                                                  │
│     ┌──────────────────────────────────────────┐               │
│     │  HIGHLIGHTED ELEMENT (full brightness)    │               │
│     │  [+ New Service]  ← spotlighted          │               │
│     └──────────────────────────────────────────┘               │
│                    ↓                                             │
│         ┌────────────────────────────────────┐                  │
│         │ 📍 Step 1 of 5                     │                  │
│         │                                    │                  │
│         │ Create Your First Service          │                  │
│         │                                    │                  │
│         │ Click the "New Service" button     │                  │
│         │ to start publishing your first     │                  │
│         │ geospatial service.                │                  │
│         │                                    │                  │
│         │        [Skip Tour] [Next →]       │                  │
│         └────────────────────────────────────┘                  │
│                                                                  │
│ [Progress: ●○○○○]                                              │
└─────────────────────────────────────────────────────────────────┘
```

**MudBlazor Implementation:**
```razor
<div class="tour-overlay @(_tourActive ? "active" : "")">
    <!-- Dimmed background -->
    <div class="tour-backdrop" @onclick="() => _showExitConfirmation = true"></div>

    <!-- Highlighted element (positioning calculated) -->
    <div class="tour-spotlight" style="top: @_spotlightTop; left: @_spotlightLeft; width: @_spotlightWidth; height: @_spotlightHeight">
        <!-- Actual UI element rendered here -->
    </div>

    <!-- Tour tooltip -->
    <MudPaper Class="tour-tooltip" Style="top: @_tooltipTop; left: @_tooltipLeft">
        <MudText Typo="Typo.caption" Class="tour-step-counter">
            Step @_currentStep of @_totalSteps
        </MudText>
        <MudText Typo="Typo.h6">@_currentTourStep.Title</MudText>
        <MudText Typo="Typo.body2">@_currentTourStep.Description</MudText>

        @if (!string.IsNullOrEmpty(_currentTourStep.ActionPrompt))
        {
            <MudAlert Severity="Severity.Info" Dense="true" Class="mt-2">
                @_currentTourStep.ActionPrompt
            </MudAlert>
        }

        <div class="tour-actions">
            <MudButton OnClick="SkipTour" Variant="Variant.Text">Skip Tour</MudButton>
            <MudButton OnClick="PreviousStep" Disabled="@(_currentStep == 1)" Variant="Variant.Text">
                <MudIcon Icon="@Icons.Material.Filled.ArrowBack" /> Back
            </MudButton>
            <MudButton OnClick="NextStep" Variant="Variant.Filled" Color="Color.Primary">
                @(_currentStep == _totalSteps ? "Finish" : "Next")
                <MudIcon Icon="@Icons.Material.Filled.ArrowForward" />
            </MudButton>
        </div>
    </MudPaper>

    <!-- Progress indicator -->
    <div class="tour-progress">
        @for (int i = 1; i <= _totalSteps; i++)
        {
            <div class="tour-progress-dot @(i == _currentStep ? "active" : i < _currentStep ? "completed" : "")"></div>
        }
    </div>
</div>
```

#### Pattern 2: Inline Hints

**For simple, non-blocking tips:**
```
┌────────────────────────────────────────┐
│ 🔍 Search services...                  │ 💡 Tip: Try searching by tag
│                                        │    using #transportation
└────────────────────────────────────────┘
```

#### Pattern 3: Video Tutorials (External)

**For complex workflows:**
```
┌────────────────────────────────────────────────────────────────┐
│ Advanced Styling Tutorial                                 [X]  │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ [Embedded video player - 3:45 duration]                       │
│                                                                 │
│ Learn how to:                                                  │
│ • Create custom SLD styles                                     │
│ • Use MapBox style expressions                                 │
│ • Apply conditional styling                                    │
│                                                                 │
│ [Start Interactive Tutorial] [Watch Video] [Read Docs]        │
└────────────────────────────────────────────────────────────────┘
```

---

### Core Tours

#### Tour 1: "Getting Started" (First-Time Users)

**Duration:** 2 minutes, 8 steps
**Trigger:** First login
**Goal:** Orient users to the interface

**Steps:**

| Step | Element Highlighted | Title | Description | Action Required |
|------|-------------------|-------|-------------|-----------------|
| 1 | Navigation sidebar | "Welcome to HonuaIO Admin" | "This is where you'll manage all your geospatial services. Let's explore the main areas." | Click Next |
| 2 | Services menu item | "Services Section" | "All your WMS, WFS, and WMTS services live here. This is where you'll spend most of your time." | Click Next |
| 3 | Search bar | "Quick Search" | "Find any service, layer, or folder instantly with fuzzy search. Try typing 'roads' to see it in action." | Type "roads" |
| 4 | Tree view | "Folder Organization" | "Organize services into folders for easy navigation. Drag and drop to reorganize." | Click Next |
| 5 | Service list | "Service List" | "View all services with health status indicators. Green = healthy, yellow = warning, red = error." | Click Next |
| 6 | "+ New Service" button | "Create Services" | "Click here to publish a new service. We'll walk you through it step-by-step." | Click Next |
| 7 | AI chat icon | "AI Assistant" | "Ask questions in plain English like 'Find all services without caching' or 'Organize my layers by theme'." | Click Next |
| 8 | Help menu (?) | "Need Help?" | "Access tours, documentation, and support anytime. You can replay this tour from here." | Click Finish |

**Completion:**
```
┌────────────────────────────────────────────────────────────────┐
│ 🎉 You're Ready to Go!                                         │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ You've completed the Getting Started tour.                     │
│                                                                 │
│ What would you like to do next?                                │
│                                                                 │
│ ┌──────────────────┐  ┌──────────────────┐                    │
│ │ 🗺️ Publish Your   │  │ 🎓 Take Another  │                    │
│ │   First Service  │  │   Tutorial       │                    │
│ │                  │  │                  │                    │
│ │ [Start Wizard →] │  │ [View Tours]     │                    │
│ └──────────────────┘  └──────────────────┘                    │
│                                                                 │
│                                            [Close]              │
└────────────────────────────────────────────────────────────────┘
```

---

#### Tour 2: "Publishing Your First Service" (Task-Based)

**Duration:** 3 minutes, 12 steps
**Trigger:** User clicks "Create Service" for the first time OR on-demand
**Goal:** Successfully publish a WMS service

**Steps:**

| Step | Element | Title | Description | Interaction |
|------|---------|-------|-------------|-------------|
| 1 | Service type cards | "Choose Service Type" | "WMS is for raster/image maps. WFS is for vector data. WMTS is for pre-rendered tiles. For now, let's create a WMS." | Select WMS card |
| 2 | Upload area | "Add Your Data" | "Drag and drop a shapefile, GeoTIFF, or GeoPackage. Or connect to a database." | Upload demo file |
| 3 | Data preview map | "Preview Your Data" | "Here's what your data looks like. The map shows the spatial extent and geometry type." | Click Next |
| 4 | CRS dropdown | "Coordinate System" | "We auto-detected EPSG:4326 from your file. You can change it if needed. 🔍 Search by name or code." | Click Next |
| 5 | Style selector | "Apply Styling" | "Choose a quick style or create a custom one. For lines, we recommend the 'Default' style." | Select style |
| 6 | Style preview | "Preview Styled Map" | "This is how your service will look to users. You can adjust styling anytime after publishing." | Click Next |
| 7 | Service name field | "Name Your Service" | "Give it a descriptive name like 'Downtown Roads' so it's easy to find later." | Type name |
| 8 | Folder picker | "Organize It" | "Choose a folder or create a new one. We'll remember your last location next time." | Select folder |
| 9 | Validation panel | "Validation Check" | "✅ All checks passed! Your service is ready to publish. Green checks mean you're good to go." | Click Next |
| 10 | "Test in QGIS" button | "Test Before Publishing" | "Pro tip: Test in QGIS before going live. This copies the GetCapabilities URL to your clipboard." | Click Next |
| 11 | Publish button | "Publish Service" | "Click here to make your service live. It'll be available in ~100ms. You can rollback if needed." | Click Publish |
| 12 | Success dialog | "Success!" | "🎉 Your service is live! Copy the URL to use it in GIS clients. You can view it in the service list." | Click Finish |

**Completion Actions:**
- Add service to "Recently Published" list
- Show "What's Next?" suggestions:
  - Add more layers to this service
  - Configure caching
  - Set up access control
  - Publish another service

---

#### Tour 3: "Using the AI Assistant" (Feature Discovery)

**Duration:** 90 seconds, 6 steps
**Trigger:** User opens AI chat for first time OR on-demand
**Goal:** Demonstrate AI capabilities

**Steps:**

| Step | Element | Title | Description | Interaction |
|------|---------|-------|-------------|-------------|
| 1 | AI chat icon | "Meet Your AI Assistant" | "The AI can search, organize, diagnose issues, and generate metadata using natural language." | Click AI icon |
| 2 | Chat input | "Ask Questions" | "Try asking: 'Find all WMS services created last month'. The AI understands context." | Type query |
| 3 | AI response | "Smart Answers" | "The AI found 12 services and grouped them by folder. Click any result to view details." | Click result |
| 4 | Quick actions | "Quick Actions" | "The AI suggests actions like 'Archive inactive' or 'Enable caching'. Click to apply." | Click Next |
| 5 | Organization mode | "AI Organization" | "Ask: 'Organize my layers by theme'. The AI will suggest folder structures for your approval." | Click Next |
| 6 | Minimize button | "Always Available" | "Minimize the chat when you don't need it. Click the 🤖 icon anytime to bring it back." | Click Finish |

**Example Queries to Showcase:**
- "Find all services without caching enabled"
- "Which services haven't been updated in 6 months?"
- "Group my unorganized services by keywords"
- "Why isn't my Roads WMS service working?"
- "Generate a metadata abstract for my Water Quality service"

---

#### Tour 4: "Troubleshooting Services" (Problem-Solving)

**Duration:** 2 minutes, 7 steps
**Trigger:** User clicks on unhealthy service OR on-demand
**Goal:** Teach diagnostic workflow

**Steps:**

| Step | Element | Title | Description | Interaction |
|------|---------|-------|-------------|-------------|
| 1 | Health indicator | "Health Status" | "🔴 Red = critical error, 🟡 Yellow = warning, 🟢 Green = healthy. Click the status to see details." | Click status |
| 2 | Health tab | "Diagnostics" | "The Health tab shows what's wrong in plain English. No cryptic error codes." | Click Next |
| 3 | Error message | "Suggested Fixes" | "Each error includes suggested fixes. Common issues: data source moved, CRS mismatch, timeout." | Click Next |
| 4 | "Test Connection" button | "Test Before Saving" | "Always test your fix before saving. This prevents breaking the service further." | Click Test |
| 5 | Logs link | "View Detailed Logs" | "Need more info? Click 'View Logs' to see the full request/response history." | Click Next |
| 6 | "Fix Data Source" button | "Quick Fix" | "Many issues can be fixed inline without leaving this page. Update and republish in one click." | Click Next |
| 7 | Rollback option | "Rollback Safety Net" | "Made a mistake? Every publish creates a snapshot. Click 'Rollback' to restore the last working version." | Click Finish |

---

### Tour State Management

**Architecture:**

```csharp
// Models/TourState.cs
public class TourState
{
    public string UserId { get; set; }
    public Dictionary<string, TourProgress> CompletedTours { get; set; } = new();
    public DateTime? LastTourDate { get; set; }
    public bool HasDismissedFTUE { get; set; }
}

public class TourProgress
{
    public string TourId { get; set; }
    public bool Completed { get; set; }
    public int LastStep { get; set; }
    public DateTime CompletedAt { get; set; }
}

// Services/TourService.cs
public class TourService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;

    public async Task<TourState> GetTourStateAsync()
    {
        // Load from localStorage (client-side) or API (server-side)
        return await _localStorage.GetItemAsync<TourState>("tour-state")
            ?? new TourState();
    }

    public async Task MarkTourCompletedAsync(string tourId)
    {
        var state = await GetTourStateAsync();
        state.CompletedTours[tourId] = new TourProgress
        {
            TourId = tourId,
            Completed = true,
            CompletedAt = DateTime.UtcNow
        };
        await _localStorage.SetItemAsync("tour-state", state);
    }

    public async Task<bool> ShouldShowFTUEAsync()
    {
        var state = await GetTourStateAsync();
        return !state.HasDismissedFTUE &&
               !state.CompletedTours.ContainsKey("getting-started");
    }
}
```

**Storage:**
- Client-side: Browser `localStorage` for tour completion state
- Server-side: User preferences table (optional, for cross-device sync)

---

### Tour Analytics

**Track Effectiveness:**

```csharp
// Track tour metrics
public class TourAnalytics
{
    public string TourId { get; set; }
    public int Started { get; set; }
    public int Completed { get; set; }
    public int Skipped { get; set; }
    public double AverageCompletionRate => Completed / (double)Started;
    public Dictionary<int, int> DropoffByStep { get; set; } = new();
}
```

**Questions to Answer:**
- What % of users complete each tour?
- Which steps cause the most dropoff?
- Do users who complete tours perform better (fewer errors, faster task completion)?
- Which tours are replayed most often?

**Optimization Loop:**
1. Launch tour
2. Measure completion rate
3. Identify dropoff points
4. Simplify or split long tours
5. Re-measure

---

### Tour Content Management

**Tour Definition Format (JSON):**

```json
{
  "id": "getting-started",
  "version": "1.0.0",
  "metadata": {
    "title": "Getting Started with HonuaIO",
    "description": "Learn the basics in 2 minutes",
    "duration": "2 minutes",
    "difficulty": "beginner",
    "prerequisites": []
  },
  "triggers": {
    "firstLogin": true,
    "onDemand": true,
    "autoStart": true
  },
  "steps": [
    {
      "id": "step-1",
      "target": "#nav-services",
      "title": "Services Section",
      "content": "All your WMS, WFS, and WMTS services live here.",
      "placement": "right",
      "actionRequired": false,
      "highlightPadding": 8,
      "waitForElement": true,
      "advanceOn": {
        "selector": "button.tour-next",
        "event": "click"
      }
    },
    {
      "id": "step-2",
      "target": "#search-bar",
      "title": "Quick Search",
      "content": "Find any service instantly with fuzzy search.",
      "placement": "bottom",
      "actionRequired": true,
      "actionPrompt": "Try typing 'roads' in the search box",
      "advanceOn": {
        "selector": "#search-bar input",
        "event": "input",
        "condition": "value.length > 0"
      }
    }
  ]
}
```

**Benefits:**
- ✅ Non-developers can edit tour content
- ✅ Version tours (update without breaking old clients)
- ✅ A/B test different tour flows
- ✅ Localize tours for different languages

---

### Accessibility Considerations

**Keyboard Navigation:**
```
ESC         - Exit tour
Arrow Right - Next step
Arrow Left  - Previous step
Enter       - Confirm action
Tab         - Focus interactive elements
```

**Screen Reader Support:**
```razor
<div role="dialog"
     aria-labelledby="tour-title"
     aria-describedby="tour-description"
     aria-live="polite">
    <h2 id="tour-title">@CurrentStep.Title</h2>
    <p id="tour-description">@CurrentStep.Content</p>
    <div role="status">Step @CurrentStepIndex of @TotalSteps</div>
</div>
```

**Reduced Motion:**
```css
@media (prefers-reduced-motion: reduce) {
  .tour-spotlight,
  .tour-tooltip {
    transition: none !important;
    animation: none !important;
  }
}
```

---

### Implementation Libraries

**Option 1: Shepherd.js (Recommended)**

**Pros:**
- ✅ Popular, well-maintained
- ✅ Framework-agnostic (works with Blazor)
- ✅ Keyboard navigation built-in
- ✅ WCAG compliant
- ✅ Customizable themes

**Integration:**
```razor
@inject IJSRuntime JS

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && await ShouldShowTour())
        {
            await JS.InvokeVoidAsync("initializeTour", TourDefinition);
        }
    }
}
```

```javascript
// wwwroot/js/tours.js
import Shepherd from 'shepherd.js';

window.initializeTour = (steps) => {
    const tour = new Shepherd.Tour({
        useModalOverlay: true,
        defaultStepOptions: {
            classes: 'honua-tour-step',
            scrollTo: true,
            cancelIcon: { enabled: true }
        }
    });

    steps.forEach(step => {
        tour.addStep({
            id: step.id,
            text: step.content,
            attachTo: {
                element: step.target,
                on: step.placement
            },
            buttons: [
                {
                    text: 'Skip',
                    action: tour.cancel
                },
                {
                    text: 'Back',
                    action: tour.back
                },
                {
                    text: step.isLast ? 'Finish' : 'Next',
                    action: tour.next
                }
            ]
        });
    });

    tour.start();
};
```

**Option 2: Intro.js**

**Pros:**
- ✅ Lightweight
- ✅ Step-by-step hints
- ✅ Good for simple tours

**Cons:**
- ⚠️ Less flexible than Shepherd
- ⚠️ Harder to customize

**Option 3: Custom Implementation (MudBlazor)**

**Pros:**
- ✅ Full control over styling
- ✅ Native Blazor (no JS interop)
- ✅ Integrated with MudBlazor theme

**Cons:**
- ❌ More development effort
- ❌ Need to implement accessibility features

**Recommendation:** Use Shepherd.js for MVP, consider custom implementation if we need deep MudBlazor integration.

---

### Tour Design Checklist

**Before Launching a Tour:**

- [ ] **Clear Goal**: What should the user be able to do after?
- [ ] **Optimal Length**: <3 minutes (5-10 steps max)
- [ ] **Skippable**: User can exit at any time
- [ ] **Resumable**: User can continue where they left off
- [ ] **Action-Oriented**: Each step teaches by doing
- [ ] **Contextual**: Tours appear when relevant
- [ ] **Tested**: Verified on different screen sizes
- [ ] **Accessible**: Keyboard navigation + screen reader support
- [ ] **Tracked**: Analytics to measure effectiveness
- [ ] **Localized**: Translations for non-English users

---

### User Preferences

**Tour Settings (in User Profile):**

```
┌────────────────────────────────────────────────────────────────┐
│ User Settings > Tours & Tutorials                              │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Tour Preferences:                                              │
│                                                                 │
│ ☑ Show tours for new features                                  │
│ ☑ Offer help when I appear stuck                               │
│ ☐ Auto-start tours on first visit to new sections              │
│                                                                 │
│ Completed Tours:                                               │
│ ✅ Getting Started (Jan 15, 2025)                              │
│ ✅ Publishing Services (Jan 15, 2025)                          │
│ ✅ Using AI Assistant (Jan 16, 2025)                           │
│ ⭕ Troubleshooting (not started)                               │
│ ⭕ Bulk Operations (not started)                                │
│                                                                 │
│ [Reset All Tours]  [Replay Any Tour]                          │
└────────────────────────────────────────────────────────────────┘
```

---

### Success Metrics for Tours

**Primary Metrics:**
- **Completion Rate**: % of users who finish vs. start a tour
  - Target: >70% for core tours
- **Task Success Rate**: % of users who successfully complete the task after the tour
  - Target: >85% (e.g., publish a service after "Publishing" tour)
- **Time to Proficiency**: Days until user completes first task without errors
  - Target: <1 day (vs. 3-5 days without tours)

**Secondary Metrics:**
- **Tour Replay Rate**: % of users who replay tours
  - Target: 10-15% (indicates usefulness as reference)
- **Feature Adoption**: % increase in feature usage after tour
  - Target: +40% for new features
- **Support Ticket Reduction**: Decrease in "How do I...?" tickets
  - Target: -30% in first 3 months

**Behavioral Indicators:**
- Users who complete tours publish their first service 3x faster
- 60% fewer errors on first publish
- Higher satisfaction scores (4.2 vs 3.5 without tours)

---

## Rich Styling Editor

### Overview

**Problem:** Users struggle with SLD syntax and styling geospatial data. Creating even simple styles requires XML expertise or copy-pasting from examples.

**Solution:** A visual style editor with:
- 🎨 Color pickers and visual controls
- 🤖 Automatic generation of unique values (categorical styling)
- 🗺️ Live map preview showing real-time changes
- 📚 Style library with templates
- 💾 Import/export capabilities (SLD, MapBox Style Spec)

**Target Users:**
- **Kim (Data Publisher)**: Needs simple styling without learning SLD
- **Sarah (GIS Admin)**: Wants quick styling for common patterns
- **Marcus (DevOps)**: Wants to export styles for version control

---

### Layout: Split-Pane Editor

**Design:**
```
┌─────────────────────────────────────────────────────────────────┐
│ Style Editor: Roads WMS                                    [X]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ ┌──────────────────────┬───────────────────────────────────┐   │
│ │                      │                                     │   │
│ │  Style Controls      │    Live Map Preview                │   │
│ │  (40%)               │    (60%)                           │   │
│ │                      │                                     │   │
│ │ 🎨 Style Type        │  ┌─────────────────────────────┐  │   │
│ │ ● Simple             │  │                             │  │   │
│ │ ○ Categorized        │  │   [Interactive Map]         │  │   │
│ │ ○ Graduated          │  │                             │  │   │
│ │                      │  │   🔍 Zoom controls          │  │   │
│ │ ──────────────────   │  │   📍 Pan                    │  │   │
│ │                      │  │   ↻ Reset extent            │  │   │
│ │ 🖌️ Line Style        │  │                             │  │   │
│ │ Color:  [🎨 ▼]      │  │   Updates in real-time      │  │   │
│ │ Width:  [2 ━━━━━━▸] │  │   as you adjust controls   │  │   │
│ │ Opacity: [100% ━▸]  │  │                             │  │   │
│ │ Dash:   [─────▼]    │  │                             │  │   │
│ │                      │  └─────────────────────────────┘  │   │
│ │ ──────────────────   │                                     │   │
│ │                      │  Preview Mode:                      │   │
│ │ 📐 Geometry Filters  │  ● Sample data (fast)              │   │
│ │ Type: [All ▼]       │  ○ Full dataset (slow)             │   │
│ │ Scale: [1:5000 ▼]   │                                     │   │
│ │                      │  [Export Sample PNG]               │   │
│ │ [Preview] [Apply]   │  [Share Preview Link]              │   │
│ │                      │                                     │   │
│ └──────────────────────┴───────────────────────────────────┘   │
│                                                                  │
│ Tabs: [🎨 Visual] [</> SLD] [📚 Library] [⚙️ Advanced]        │
└─────────────────────────────────────────────────────────────────┘
```

**Benefits:**
- ✅ See changes immediately (no publish-test-iterate cycle)
- ✅ No SLD knowledge required for basic styling
- ✅ Can switch to SLD tab for advanced users
- ✅ Split-pane resizable for different screen sizes

---

### Style Types

#### 1. Simple Style (Single Symbol)

**Use Case:** All features styled the same way

**UI:**
```
┌────────────────────────────────────────────────────────────────┐
│ 🎨 Simple Style                                                │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Geometry Type: Line ━━━                                        │
│                                                                 │
│ ┌─────────────────────────────────────────────────────────┐   │
│ │ Stroke                                                   │   │
│ │                                                          │   │
│ │ Color:     [🎨 #333333 ▼]   ┌──────────────────┐       │   │
│ │ Width:     [2px ━━━━━━━━▸]  │ Color Picker     │       │   │
│ │ Opacity:   [100% ━━━━━━━▸]  │ ┌────────────┐   │       │   │
│ │ Dash:      [Solid ▼]        │ │            │   │       │   │
│ │   Options:                   │ │   Hue      │   │       │   │
│ │   ─────  Solid               │ │            │   │       │   │
│ │   ┅┅┅┅┅  Dashed              │ └────────────┘   │       │   │
│ │   ┈┈┈┈┈  Dotted               │ RGB: 51,51,51    │       │   │
│ │   ╌╌╌╌╌  Dash-dot             │ Hex: #333333     │       │   │
│ │                              │ Recent:          │       │   │
│ │ Cap:       [Round ▼]         │ 🟦 🟩 🟥 🟧      │       │   │
│ │   Options:                   │                  │       │   │
│ │   ●   Round                  │ [Eyedropper]     │       │   │
│ │   ▬   Butt                   └──────────────────┘       │   │
│ │   ◀▬▶ Square                                            │   │
│ │                                                          │   │
│ │ Join:      [Round ▼]                                    │   │
│ │   Options:                                              │   │
│ │   ╱╲  Miter                                             │   │
│ │   ╱╲  Round                                             │   │
│ │   ╱ ╲ Bevel                                             │   │
│ └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│ Preview:  ━━━━━━━━━━━  (2px, solid, #333)                     │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

**For Points:**
```
┌────────────────────────────────────────────────────────────────┐
│ Symbol:     ● Circle   ▼   [⬤ ⬟ ⬢ ★ ▲ ■ + ✕]                │
│ Size:       [8px ━━━━━━━━▸]                                    │
│ Fill:       [🎨 #FF5252 ▼]                                     │
│ Stroke:     [🎨 #FFFFFF ▼]                                     │
│ Stroke W:   [1px ━━━━━━━▸]                                     │
│ Opacity:    [90% ━━━━━━━▸]                                     │
│                                                                 │
│ ☑ Add halo (for visibility on dark backgrounds)                │
│   Halo Color: [🎨 #FFFFFF ▼]                                   │
│   Halo Size:  [2px ━━━━━━▸]                                    │
└────────────────────────────────────────────────────────────────┘
```

**For Polygons:**
```
┌────────────────────────────────────────────────────────────────┐
│ Fill:       [🎨 #00C853 ▼]                                     │
│ Opacity:    [60% ━━━━━━━▸]                                     │
│                                                                 │
│ Stroke:     [🎨 #00873B ▼]                                     │
│ Width:      [1px ━━━━━━━▸]                                     │
│                                                                 │
│ Pattern:    [Solid ▼]                                          │
│   Options:                                                      │
│   ████  Solid                                                   │
│   ////  Diagonal lines                                         │
│   \\\\  Reverse diagonal                                       │
│   ####  Crosshatch                                             │
│   ····  Dots                                                   │
└────────────────────────────────────────────────────────────────┘
```

**MudBlazor Implementation:**
```razor
<MudPaper Class="style-controls">
    <MudText Typo="Typo.h6">🎨 Simple Style</MudText>

    <!-- Color Picker -->
    <MudColorPicker Label="Color"
                    @bind-Text="_strokeColor"
                    ColorPickerMode="ColorPickerMode.RGB"
                    DisableAlpha="false"
                    AdornmentIcon="@Icons.Material.Filled.Palette"
                    OnColorChanged="OnStyleChanged">
        <!-- Recent colors -->
        <PickerActions>
            <MudStack Row="true" Spacing="1">
                <MudText Typo="Typo.caption">Recent:</MudText>
                @foreach (var recent in _recentColors)
                {
                    <MudChip Size="Size.Small"
                             Style="@($"background-color: {recent}")"
                             OnClick="() => SelectColor(recent)">
                    </MudChip>
                }
            </MudStack>
        </PickerActions>
    </MudColorPicker>

    <!-- Width Slider -->
    <MudSlider T="int"
               Label="Width"
               Min="1"
               Max="20"
               Step="1"
               @bind-Value="_strokeWidth"
               ValueLabel="true"
               OnChangeAsync="OnStyleChanged">
        <span>@_strokeWidth px</span>
    </MudSlider>

    <!-- Opacity Slider -->
    <MudSlider T="int"
               Label="Opacity"
               Min="0"
               Max="100"
               Step="5"
               @bind-Value="_opacity"
               ValueLabel="true"
               OnChangeAsync="OnStyleChanged">
        <span>@_opacity%</span>
    </MudSlider>

    <!-- Line Dash Pattern -->
    <MudSelect T="string"
               Label="Dash Pattern"
               @bind-Value="_dashPattern"
               OnValueChanged="OnStyleChanged">
        <MudSelectItem Value="@("solid")">─────  Solid</MudSelectItem>
        <MudSelectItem Value="@("dash")">┅┅┅┅┅  Dashed</MudSelectItem>
        <MudSelectItem Value="@("dot")">┈┈┈┈┈  Dotted</MudSelectItem>
        <MudSelectItem Value="@("dashdot")">╌╌╌╌╌  Dash-Dot</MudSelectItem>
    </MudSelect>

    <!-- Preview -->
    <MudPaper Class="style-preview" Style="@GetPreviewStyle()">
        <svg width="200" height="40">
            <line x1="10" y1="20" x2="190" y2="20"
                  stroke="@_strokeColor"
                  stroke-width="@_strokeWidth"
                  opacity="@(_opacity / 100.0)"
                  stroke-dasharray="@GetDashArray()" />
        </svg>
    </MudPaper>
</MudPaper>

@code {
    private string _strokeColor = "#333333";
    private int _strokeWidth = 2;
    private int _opacity = 100;
    private string _dashPattern = "solid";
    private List<string> _recentColors = new() { "#333", "#00C853", "#2196F3", "#FF5252" };

    private async Task OnStyleChanged()
    {
        // Update live map preview
        await JS.InvokeVoidAsync("updateMapStyle", new
        {
            stroke = new { color = _strokeColor, width = _strokeWidth, opacity = _opacity / 100.0 },
            dash = _dashPattern
        });
    }
}
```

---

#### 2. Categorized Style (Unique Values)

**Use Case:** Style features based on attribute values (e.g., road type, land use)

**Key Feature: Automatic Value Detection + Color Generation**

**Flow:**

**Step 1: Select Attribute**
```
┌────────────────────────────────────────────────────────────────┐
│ 🎨 Categorized Style                                           │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Classify by attribute:                                         │
│                                                                 │
│ [road_type ▼]  [🤖 Auto-Generate Colors]                       │
│                                                                 │
│ 🔍 Analyzing data...                                           │
│                                                                 │
│ ✅ Found 4 unique values:                                      │
│    • highway (245 features)                                    │
│    • arterial (189 features)                                   │
│    • collector (432 features)                                  │
│    • local (1,876 features)                                    │
│                                                                 │
│ Color Scheme:  [Qualitative ▼]                                 │
│   ├─ Qualitative (categorical data)                            │
│   ├─ Sequential (ordered data, low → high)                     │
│   └─ Diverging (data with midpoint)                            │
│                                                                 │
│ Palette:       [ColorBrewer Set1 ▼]                            │
│   Preview: 🟥 🟦 🟩 🟧                                          │
│                                                                 │
│                                      [Cancel] [Generate →]     │
└────────────────────────────────────────────────────────────────┘
```

**Step 2: Review & Customize Generated Styles**
```
┌────────────────────────────────────────────────────────────────┐
│ 🎨 Categorized Style - Review                                  │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Value                Color        Width    Preview             │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ highway (245)     [🎨 #E53935]   [4px ▸]   ━━━━━━━━━━━━      │
│ arterial (189)    [🎨 #FB8C00]   [3px ▸]   ━━━━━━━━━━━━      │
│ collector (432)   [🎨 #FFEB3B]   [2px ▸]   ━━━━━━━━━━━━      │
│ local (1,876)     [🎨 #9E9E9E]   [1px ▸]   ━━━━━━━━━━━━      │
│                                                                 │
│ ☑ Sort by: [Feature Count ▼]                                   │
│ ☑ Show labels on map                                           │
│ ☑ Scale line width by hierarchy                                │
│                                                                 │
│ ┌───────────────────────────────────────────────────────────┐ │
│ │ Legend Preview                                             │ │
│ │                                                            │ │
│ │ Roads by Type                                              │ │
│ │ ━━━━  Highway                                              │ │
│ │ ━━━━  Arterial                                             │ │
│ │ ━━━  Collector                                             │ │
│ │ ━━   Local                                                 │ │
│ └───────────────────────────────────────────────────────────┘ │
│                                                                 │
│ Add Rule:                                                      │
│ [+ Add Custom Rule]  [+ Add "Other" Category]                 │
│                                                                 │
│                                      [< Back] [Apply Style]    │
└────────────────────────────────────────────────────────────────┘
```

**Auto-Generation Algorithm:**

```csharp
// Services/StyleGenerationService.cs
public class StyleGenerationService
{
    public async Task<CategorizedStyle> GenerateCategorizedStyleAsync(
        string layerId,
        string attributeName,
        ColorScheme scheme = ColorScheme.Qualitative)
    {
        // 1. Query unique values from data source
        var uniqueValues = await GetUniqueValuesAsync(layerId, attributeName);

        // 2. Sort by count or alphabetically
        var sortedValues = uniqueValues.OrderByDescending(v => v.Count).ToList();

        // 3. Generate colors based on scheme
        var colors = GenerateColorPalette(sortedValues.Count, scheme);

        // 4. Assign visual hierarchy (width/size) based on count
        var styles = new List<CategoryStyle>();
        for (int i = 0; i < sortedValues.Count; i++)
        {
            styles.Add(new CategoryStyle
            {
                Value = sortedValues[i].Value,
                Color = colors[i],
                Width = CalculateWidth(sortedValues[i].Count, uniqueValues),
                Label = FormatLabel(sortedValues[i].Value)
            });
        }

        return new CategorizedStyle
        {
            AttributeName = attributeName,
            Categories = styles,
            DefaultStyle = GenerateDefaultStyle() // For null/unmatched values
        };
    }

    private string[] GenerateColorPalette(int count, ColorScheme scheme)
    {
        return scheme switch
        {
            ColorScheme.Qualitative => ColorBrewer.GetQualitativePalette("Set1", count),
            ColorScheme.Sequential => ColorBrewer.GetSequentialPalette("Blues", count),
            ColorScheme.Diverging => ColorBrewer.GetDivergingPalette("RdYlGn", count),
            _ => throw new ArgumentException("Unknown color scheme")
        };
    }

    private int CalculateWidth(int count, List<UniqueValue> allValues)
    {
        // Scale width based on frequency
        var maxCount = allValues.Max(v => v.Count);
        var minCount = allValues.Min(v => v.Count);

        // Map count to width range (1-5px for lines)
        var normalized = (count - minCount) / (double)(maxCount - minCount);
        return (int)(1 + normalized * 4); // 1-5px range
    }
}
```

**ColorBrewer Integration:**
```csharp
// Palettes/ColorBrewer.cs
public static class ColorBrewer
{
    private static readonly Dictionary<string, string[]> Palettes = new()
    {
        // Qualitative (for categorical data)
        ["Set1"] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33", "#A65628", "#F781BF" },
        ["Set2"] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F", "#E5C494", "#B3B3B3" },
        ["Paired"] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C", "#FDBF6F", "#FF7F00" },

        // Sequential (for ordered data)
        ["Blues"] = new[] { "#F7FBFF", "#DEEBF7", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#084594" },
        ["Greens"] = new[] { "#F7FCF5", "#E5F5E0", "#C7E9C0", "#A1D99B", "#74C476", "#41AB5D", "#238B45", "#005A32" },
        ["Reds"] = new[] { "#FFF5F0", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D" },

        // Diverging (for data with natural midpoint)
        ["RdYlGn"] = new[] { "#D73027", "#F46D43", "#FDAE61", "#FEE08B", "#D9EF8B", "#A6D96A", "#66BD63", "#1A9850" },
        ["RdBu"] = new[] { "#B2182B", "#D6604D", "#F4A582", "#FDDBC7", "#D1E5F0", "#92C5DE", "#4393C3", "#2166AC" },
    };

    public static string[] GetQualitativePalette(string name, int count)
    {
        if (!Palettes.ContainsKey(name))
            throw new ArgumentException($"Palette '{name}' not found");

        var palette = Palettes[name];
        if (count <= palette.Length)
            return palette.Take(count).ToArray();

        // If more colors needed than palette has, interpolate
        return InterpolateColors(palette, count);
    }

    private static string[] InterpolateColors(string[] palette, int targetCount)
    {
        // Simple approach: repeat palette
        // Advanced: Use perceptual color interpolation
        var result = new List<string>();
        while (result.Count < targetCount)
        {
            result.AddRange(palette.Take(Math.Min(palette.Length, targetCount - result.Count)));
        }
        return result.ToArray();
    }
}
```

**User Customization:**
```razor
<MudDataGrid T="CategoryStyle"
             Items="@_categories"
             Elevation="0"
             Dense="true">
    <Columns>
        <PropertyColumn Property="x => x.Value" Title="Value">
            <CellTemplate>
                <MudText>@context.Item.Value (@context.Item.Count)</MudText>
            </CellTemplate>
        </PropertyColumn>

        <PropertyColumn Property="x => x.Color" Title="Color">
            <CellTemplate>
                <MudColorPicker @bind-Text="context.Item.Color"
                                ColorPickerMode="ColorPickerMode.HEX"
                                DisableAlpha="false"
                                OnColorChanged="OnStyleChanged" />
            </CellTemplate>
        </PropertyColumn>

        <PropertyColumn Property="x => x.Width" Title="Width">
            <CellTemplate>
                <MudSlider @bind-Value="context.Item.Width"
                           Min="1"
                           Max="10"
                           Step="1"
                           OnChangeAsync="OnStyleChanged" />
            </CellTemplate>
        </PropertyColumn>

        <PropertyColumn Property="x => x" Title="Preview">
            <CellTemplate>
                <svg width="100" height="20">
                    <line x1="5" y1="10" x2="95" y2="10"
                          stroke="@context.Item.Color"
                          stroke-width="@context.Item.Width" />
                </svg>
            </CellTemplate>
        </PropertyColumn>

        <TemplateColumn>
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Delete"
                               Size="Size.Small"
                               OnClick="() => RemoveCategory(context.Item)" />
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>
```

---

#### 3. Graduated Style (Data-Driven)

**Use Case:** Style features based on numeric attribute ranges (e.g., population density, elevation)

**UI:**
```
┌────────────────────────────────────────────────────────────────┐
│ 🎨 Graduated Style                                             │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Classify by attribute:  [population_density ▼]                 │
│                                                                 │
│ Classification Method:  [Natural Breaks (Jenks) ▼]             │
│   ├─ Equal Intervals                                           │
│   ├─ Quantiles                                                 │
│   ├─ Natural Breaks (Jenks) ← Recommended                      │
│   ├─ Standard Deviation                                        │
│   └─ Manual                                                    │
│                                                                 │
│ Number of Classes:      [5 ━━━━━●━━━━━▸] (2-10)               │
│                                                                 │
│ Color Ramp:             [Yellow to Red ▼]                      │
│   Preview: 🟨 🟧 🟥 🟥 🟥                                       │
│                                                                 │
│ [🤖 Auto-Generate Classes]                                     │
│                                                                 │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ Generated Classes:                                             │
│                                                                 │
│ Range              Color      Count    Preview                │
│ ──────────────────────────────────────────────────────────     │
│ 0 - 100           [🎨 #FFFFCC]  (124)   ██                     │
│ 100 - 500         [🎨 #FFEDA0]  (98)    ██                     │
│ 500 - 1,000       [🎨 #FEB24C]  (67)    ██                     │
│ 1,000 - 2,500     [🎨 #F03B20]  (45)    ██                     │
│ 2,500 - 10,000    [🎨 #BD0026]  (23)    ██                     │
│                                                                 │
│ ☑ Show class breaks on legend                                  │
│ ☑ Use graduated symbols (vary size)                            │
│                                                                 │
│                                      [< Back] [Apply Style]    │
└────────────────────────────────────────────────────────────────┘
```

**Jenks Natural Breaks Algorithm:**

```csharp
public class JenksNaturalBreaks
{
    public static double[] CalculateBreaks(double[] values, int numClasses)
    {
        if (values.Length < numClasses)
            throw new ArgumentException("Not enough values for the number of classes");

        var sorted = values.OrderBy(v => v).ToArray();
        var n = sorted.Length;

        // Initialize matrices
        var mat1 = new double[n + 1, numClasses + 1];
        var mat2 = new double[n + 1, numClasses + 1];

        // Initialize first column and row
        for (int i = 1; i <= n; i++)
        {
            mat1[i, 1] = 1;
            mat2[i, 1] = 0;
            for (int j = 2; j <= numClasses; j++)
            {
                mat2[i, j] = double.MaxValue;
            }
        }

        // Main loop
        for (int l = 2; l <= n; l++)
        {
            double s1 = 0, s2 = 0;
            int w = 0;

            for (int m = 1; m <= l; m++)
            {
                int i3 = l - m + 1;
                double val = sorted[i3 - 1];
                s2 += val * val;
                s1 += val;
                w++;
                double v = s2 - (s1 * s1) / w;

                int i4 = i3 - 1;
                if (i4 != 0)
                {
                    for (int j = 2; j <= numClasses; j++)
                    {
                        if (mat2[l, j] >= (v + mat2[i4, j - 1]))
                        {
                            mat1[l, j] = i3;
                            mat2[l, j] = v + mat2[i4, j - 1];
                        }
                    }
                }
            }

            mat1[l, 1] = 1;
            mat2[l, 1] = mat2[l - 1, 1] + (sorted[l - 1] * sorted[l - 1]);
        }

        // Extract breaks
        var breaks = new double[numClasses + 1];
        breaks[numClasses] = sorted[n - 1];
        breaks[0] = sorted[0];

        int k = n;
        for (int j = numClasses; j >= 2; j--)
        {
            int id = (int)mat1[k, j] - 1;
            breaks[j - 1] = sorted[id];
            k = (int)mat1[k, j] - 1;
        }

        return breaks;
    }
}
```

---

### Live Map Preview

**Technology:** OpenLayers or Leaflet (via JS interop)

**Features:**
- 🗺️ Interactive map showing styled data
- 🔍 Zoom and pan controls
- 🔄 Real-time updates as style changes
- 📸 Export preview as PNG
- 🔗 Share preview URL

**Implementation:**

```javascript
// wwwroot/js/style-preview.js
import Map from 'ol/Map';
import View from 'ol/View';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import GeoJSON from 'ol/format/GeoJSON';
import { Style, Stroke, Fill, Circle } from 'ol/style';

let map = null;
let previewLayer = null;

window.initializeStylePreviewMap = (elementId, data, extent) => {
    // Initialize map
    map = new Map({
        target: elementId,
        layers: [
            // Base map (optional)
            new TileLayer({
                source: new OSM()
            })
        ],
        view: new View({
            center: [(extent[0] + extent[2]) / 2, (extent[1] + extent[3]) / 2],
            zoom: 12
        })
    });

    // Add preview layer
    previewLayer = new VectorLayer({
        source: new VectorSource({
            features: new GeoJSON().readFeatures(data, {
                dataProjection: 'EPSG:4326',
                featureProjection: 'EPSG:3857'
            })
        })
    });

    map.addLayer(previewLayer);
    map.getView().fit(extent, { padding: [50, 50, 50, 50] });

    return true;
};

window.updateStylePreview = (styleDefinition) => {
    if (!previewLayer) return;

    // Parse style definition and apply
    const olStyle = convertToOpenLayersStyle(styleDefinition);
    previewLayer.setStyle(olStyle);
};

function convertToOpenLayersStyle(styleDef) {
    if (styleDef.type === 'simple') {
        return new Style({
            stroke: new Stroke({
                color: styleDef.stroke.color,
                width: styleDef.stroke.width,
                lineDash: getDashArray(styleDef.stroke.dash)
            }),
            fill: styleDef.fill ? new Fill({
                color: styleDef.fill.color
            }) : null
        });
    }

    if (styleDef.type === 'categorized') {
        return (feature) => {
            const value = feature.get(styleDef.attribute);
            const category = styleDef.categories.find(c => c.value === value);

            if (!category) return styleDef.defaultStyle;

            return new Style({
                stroke: new Stroke({
                    color: category.color,
                    width: category.width
                })
            });
        };
    }

    if (styleDef.type === 'graduated') {
        return (feature) => {
            const value = feature.get(styleDef.attribute);
            const cls = styleDef.classes.find(c => value >= c.min && value < c.max);

            if (!cls) return styleDef.defaultStyle;

            return new Style({
                fill: new Fill({ color: cls.color }),
                stroke: new Stroke({ color: '#333', width: 1 })
            });
        };
    }
}

window.exportStylePreviewPNG = async () => {
    return new Promise((resolve) => {
        map.once('rendercomplete', () => {
            const canvas = document.querySelector('#style-preview-map canvas');
            resolve(canvas.toDataURL('image/png'));
        });
        map.renderSync();
    });
};
```

**Blazor Integration:**

```razor
<div id="style-preview-map" style="width: 100%; height: 500px;"></div>

<MudStack Row="true" Spacing="2" Class="mt-2">
    <MudButton OnClick="ZoomToExtent" Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.ZoomOutMap">
        Reset Extent
    </MudButton>
    <MudButton OnClick="ExportPreviewPNG" Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Image">
        Export PNG
    </MudButton>
    <MudButton OnClick="SharePreviewLink" Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Share">
        Share Link
    </MudButton>
</MudStack>

<MudRadioGroup @bind-SelectedOption="_previewMode" T="string" OnChange="OnPreviewModeChanged">
    <MudRadio Option="@("sample")" Color="Color.Primary">Sample data (fast)</MudRadio>
    <MudRadio Option="@("full")" Color="Color.Primary">Full dataset (slow)</MudRadio>
</MudRadioGroup>

@code {
    private string _previewMode = "sample";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var sampleData = await GetSampleDataAsync();
            await JS.InvokeVoidAsync("initializeStylePreviewMap",
                "style-preview-map",
                sampleData,
                _layer.Extent);
        }
    }

    private async Task OnStyleChanged()
    {
        var styleDefinition = new
        {
            type = "simple",
            stroke = new { color = _strokeColor, width = _strokeWidth },
            fill = _fillColor != null ? new { color = _fillColor } : null
        };

        await JS.InvokeVoidAsync("updateStylePreview", styleDefinition);
    }

    private async Task ExportPreviewPNG()
    {
        var dataUrl = await JS.InvokeAsync<string>("exportStylePreviewPNG");

        // Download or show dialog
        await JS.InvokeVoidAsync("downloadFile", "style-preview.png", dataUrl);
        Snackbar.Add("Preview exported as PNG", Severity.Success);
    }
}
```

---

### Style Library

**Features:**
- 📚 Pre-built templates (roads, water, elevation, parcels, etc.)
- 💾 Save custom styles for reuse
- 📤 Export styles (SLD, MapBox Style Spec)
- 📥 Import styles from external sources
- 🔍 Search and filter styles
- ⭐ Favorite/bookmark styles

**UI:**

```
┌────────────────────────────────────────────────────────────────┐
│ 📚 Style Library                                    [+ New]    │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ 🔍 [Search styles...]            Filter: [All ▼] [Tags ▼]     │
│                                                                 │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ BUILT-IN TEMPLATES                                             │
│                                                                 │
│ ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│ │ 🛣️ Roads      │  │ 💧 Water      │  │ 🌳 Landcover  │         │
│ │              │  │              │  │              │         │
│ │ [Preview]    │  │ [Preview]    │  │ [Preview]    │         │
│ │ ⭐ 234 uses  │  │ ⭐ 189 uses  │  │ ⭐ 156 uses  │         │
│ │              │  │              │  │              │         │
│ │ [Apply]      │  │ [Apply]      │  │ [Apply]      │         │
│ └──────────────┘  └──────────────┘  └──────────────┘         │
│                                                                 │
│ ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│ │ 🏢 Buildings │  │ ⛰️ Elevation  │  │ 📍 Points     │         │
│ │              │  │              │  │              │         │
│ │ [Preview]    │  │ [Preview]    │  │ [Preview]    │         │
│ │ ⭐ 145 uses  │  │ ⭐ 98 uses   │  │ ⭐ 87 uses   │         │
│ │              │  │              │  │              │         │
│ │ [Apply]      │  │ [Apply]      │  │ [Apply]      │         │
│ └──────────────┘  └──────────────┘  └──────────────┘         │
│                                                                 │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ MY STYLES                                                      │
│                                                                 │
│ ┌──────────────┐  ┌──────────────┐                            │
│ │ My Custom    │  │ Downtown     │                            │
│ │ Roads        │  │ Zoning       │                            │
│ │              │  │              │                            │
│ │ [Preview]    │  │ [Preview]    │                            │
│ │ Created: 2d  │  │ Created: 1w  │                            │
│ │              │  │              │                            │
│ │ [Edit] [...]  │  │ [Edit] [...]  │                            │
│ └──────────────┘  └──────────────┘                            │
│                                                                 │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ SHARED STYLES (from team)                                      │
│                                                                 │
│ ┌──────────────┐                                               │
│ │ Transit      │  by Sarah Johnson                            │
│ │ Network      │                                               │
│ │              │                                               │
│ │ [Preview]    │                                               │
│ │ ⭐ 12 uses   │                                               │
│ │              │                                               │
│ │ [Apply]      │                                               │
│ └──────────────┘                                               │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

**Built-in Templates:**

```csharp
// Services/StyleTemplateService.cs
public class StyleTemplateService
{
    private static readonly List<StyleTemplate> BuiltInTemplates = new()
    {
        new StyleTemplate
        {
            Id = "roads-default",
            Name = "Roads (Default)",
            Description = "Standard road network styling with hierarchy",
            GeometryType = GeometryType.LineString,
            Type = StyleType.Categorized,
            AttributeName = "road_type",
            Categories = new[]
            {
                new CategoryStyle { Value = "highway", Color = "#E53935", Width = 4, Label = "Highway" },
                new CategoryStyle { Value = "arterial", Color = "#FB8C00", Width = 3, Label = "Arterial" },
                new CategoryStyle { Value = "collector", Color = "#FFEB3B", Width = 2, Label = "Collector" },
                new CategoryStyle { Value = "local", Color = "#9E9E9E", Width = 1, Label = "Local" }
            },
            Tags = new[] { "transportation", "roads", "infrastructure" },
            UsageCount = 234
        },

        new StyleTemplate
        {
            Id = "water-default",
            Name = "Water Bodies",
            Description = "Blue gradient for water features",
            GeometryType = GeometryType.Polygon,
            Type = StyleType.Simple,
            Fill = new FillStyle { Color = "#2196F3", Opacity = 0.6 },
            Stroke = new StrokeStyle { Color = "#1565C0", Width = 1 },
            Tags = new[] { "hydrology", "water", "natural" },
            UsageCount = 189
        },

        new StyleTemplate
        {
            Id = "elevation-graduated",
            Name = "Elevation (Graduated)",
            Description = "Green to brown gradient for elevation",
            GeometryType = GeometryType.Polygon,
            Type = StyleType.Graduated,
            AttributeName = "elevation",
            Classes = new[]
            {
                new GraduatedClass { Min = 0, Max = 100, Color = "#FFFFCC", Label = "0-100m" },
                new GraduatedClass { Min = 100, Max = 500, Color = "#C7E9B4", Label = "100-500m" },
                new GraduatedClass { Min = 500, Max = 1000, Color = "#7FCDBB", Label = "500-1000m" },
                new GraduatedClass { Min = 1000, Max = 2000, Color = "#41B6C4", Label = "1000-2000m" },
                new GraduatedClass { Min = 2000, Max = 5000, Color = "#225EA8", Label = "2000m+" }
            },
            Tags = new[] { "topography", "elevation", "terrain" },
            UsageCount = 98
        },

        new StyleTemplate
        {
            Id = "points-simple",
            Name = "Simple Points",
            Description = "Red circles for point features",
            GeometryType = GeometryType.Point,
            Type = StyleType.Simple,
            Symbol = new PointStyle
            {
                Shape = "circle",
                Size = 8,
                Fill = new FillStyle { Color = "#FF5252", Opacity = 0.9 },
                Stroke = new StrokeStyle { Color = "#FFFFFF", Width = 1 }
            },
            Tags = new[] { "points", "markers" },
            UsageCount = 87
        }
    };

    public List<StyleTemplate> GetTemplates(GeometryType? geometryType = null, string[] tags = null)
    {
        var query = BuiltInTemplates.AsEnumerable();

        if (geometryType.HasValue)
            query = query.Where(t => t.GeometryType == geometryType.Value);

        if (tags != null && tags.Any())
            query = query.Where(t => t.Tags.Intersect(tags).Any());

        return query.ToList();
    }

    public async Task<StyleTemplate> SaveCustomStyleAsync(StyleTemplate template, string userId)
    {
        template.Id = Guid.NewGuid().ToString();
        template.CreatedBy = userId;
        template.CreatedAt = DateTime.UtcNow;

        // Save to database
        await _db.StyleTemplates.AddAsync(template);
        await _db.SaveChangesAsync();

        return template;
    }
}
```

**Export/Import:**

```razor
<MudMenu Label="Export" Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Download">
    <MudMenuItem OnClick="() => ExportStyle(StyleFormat.SLD)">
        SLD (OGC Standard)
    </MudMenuItem>
    <MudMenuItem OnClick="() => ExportStyle(StyleFormat.MapBoxGL)">
        MapBox Style Spec
    </MudMenuItem>
    <MudMenuItem OnClick="() => ExportStyle(StyleFormat.JSON)">
        JSON (HonuaIO Native)
    </MudMenuItem>
</MudMenu>

@code {
    private async Task ExportStyle(StyleFormat format)
    {
        var styleExporter = new StyleExporter();
        var exported = format switch
        {
            StyleFormat.SLD => styleExporter.ToSLD(_currentStyle),
            StyleFormat.MapBoxGL => styleExporter.ToMapBoxGL(_currentStyle),
            StyleFormat.JSON => JsonSerializer.Serialize(_currentStyle, new JsonSerializerOptions { WriteIndented = true }),
            _ => throw new ArgumentException("Unknown format")
        };

        var filename = $"{_layer.Name}-style.{format.ToString().ToLower()}";
        await JS.InvokeVoidAsync("downloadFile", filename, exported);
        Snackbar.Add($"Style exported as {format}", Severity.Success);
    }
}
```

---

### Advanced Features Tab

**For Power Users:**

```
┌────────────────────────────────────────────────────────────────┐
│ ⚙️ Advanced Style Options                                      │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ 🎯 Scale-Dependent Rendering                                   │
│                                                                 │
│ ☑ Enable scale-based visibility                                │
│                                                                 │
│ Min Scale:  [1:5,000    ▼]  (zoom in beyond this = visible)   │
│ Max Scale:  [1:100,000  ▼]  (zoom out beyond this = hidden)   │
│                                                                 │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ 🏷️ Labels                                                       │
│                                                                 │
│ ☑ Show labels                                                   │
│                                                                 │
│ Label Field:     [name ▼]                                      │
│ Font:            [Arial ▼]                                     │
│ Size:            [12px ━━━━━━▸]                                │
│ Color:           [🎨 #000000 ▼]                                │
│                                                                 │
│ ☑ Add halo (improves readability)                              │
│   Halo Color:    [🎨 #FFFFFF ▼]                                │
│   Halo Size:     [2px ━━━━━━▸]                                 │
│                                                                 │
│ Placement:       [Centroid ▼]                                  │
│   ├─ Centroid (center of feature)                              │
│   ├─ Point (at specific location)                              │
│   └─ Line (along path - for roads)                             │
│                                                                 │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ 🎨 Composite Operations                                        │
│                                                                 │
│ Blend Mode:      [Normal ▼]                                    │
│   ├─ Normal                                                    │
│   ├─ Multiply                                                  │
│   ├─ Screen                                                    │
│   ├─ Overlay                                                   │
│   └─ Difference                                                │
│                                                                 │
│ ──────────────────────────────────────────────────────────     │
│                                                                 │
│ 🔢 Data-Driven Properties (MapBox Expressions)                 │
│                                                                 │
│ ☑ Use expressions for dynamic styling                          │
│                                                                 │
│ Example:                                                        │
│ ["case",                                                        │
│   ["<", ["get", "population"], 1000], "#ffffcc",              │
│   ["<", ["get", "population"], 5000], "#c7e9b4",              │
│   "#41b6c4"                                                    │
│ ]                                                              │
│                                                                 │
│ [Edit Expression]                                              │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

---

### Integration with Publishing Workflow

**Step 3 of Service Creation Wizard:**

```
┌────────────────────────────────────────────────────────────────┐
│ Step 3 of 4: Apply Styling                                     │
│ ○───────○───────●───────○                                     │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Choose a styling approach:                                     │
│                                                                 │
│ ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│ │ ● Quick      │  │   Template   │  │   Custom     │         │
│ │   Style      │  │   Library    │  │   Editor     │         │
│ │              │  │              │  │              │         │
│ │ Use simple   │  │ Choose from  │  │ Full control │         │
│ │ defaults     │  │ pre-built    │  │ over styling │         │
│ │              │  │              │  │              │         │
│ │ [Select]     │  │ [Browse]     │  │ [Open]       │         │
│ └──────────────┘  └──────────────┘  └──────────────┘         │
│                                                                 │
│ ✅ Selected: Quick Style (Simple)                              │
│                                                                 │
│ Preview:                                                        │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ [Map showing styled data]                                 │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│ 💡 Tip: You can always change styling after publishing        │
│                                                                 │
│                                    [< Back] [Next: Publish >]  │
└────────────────────────────────────────────────────────────────┘
```

**Clicking "Custom Editor" opens full style editor in modal**

---

### SLD Tab (For Advanced Users)

**Direct SLD Editing:**

```razor
<MudTabs Elevation="0">
    <MudTabPanel Text="🎨 Visual" Icon="@Icons.Material.Filled.Palette">
        <!-- Visual editor (all the UI above) -->
    </MudTabPanel>

    <MudTabPanel Text="</> SLD" Icon="@Icons.Material.Filled.Code">
        <MudPaper Class="pa-4">
            <MudAlert Severity="Severity.Info" Dense="true" Class="mb-2">
                Advanced users can edit SLD directly. Changes will update the visual editor.
            </MudAlert>

            <MudTextField T="string"
                          @bind-Value="_sldXml"
                          Label="SLD XML"
                          Variant="Variant.Outlined"
                          Lines="20"
                          OnValueChanged="OnSldChanged">
            </MudTextField>

            <MudStack Row="true" Spacing="2" Class="mt-2">
                <MudButton OnClick="ValidateSLD"
                           Variant="Variant.Outlined"
                           StartIcon="@Icons.Material.Filled.CheckCircle">
                    Validate
                </MudButton>
                <MudButton OnClick="FormatSLD"
                           Variant="Variant.Outlined"
                           StartIcon="@Icons.Material.Filled.FormatAlignLeft">
                    Format
                </MudButton>
                <MudButton OnClick="ImportSLD"
                           Variant="Variant.Outlined"
                           StartIcon="@Icons.Material.Filled.Upload">
                    Import File
                </MudButton>
            </MudStack>

            @if (_validationErrors.Any())
            {
                <MudAlert Severity="Severity.Error" Class="mt-2">
                    <MudText Typo="Typo.body2">SLD Validation Errors:</MudText>
                    <ul>
                        @foreach (var error in _validationErrors)
                        {
                            <li>@error</li>
                        }
                    </ul>
                </MudAlert>
            }
        </MudPaper>
    </MudTabPanel>

    <MudTabPanel Text="📚 Library" Icon="@Icons.Material.Filled.Collections">
        <!-- Style library (shown above) -->
    </MudTabPanel>

    <MudTabPanel Text="⚙️ Advanced" Icon="@Icons.Material.Filled.Settings">
        <!-- Advanced options (shown above) -->
    </MudTabPanel>
</MudTabs>

@code {
    private string _sldXml;
    private List<string> _validationErrors = new();

    private async Task OnSldChanged(string value)
    {
        _sldXml = value;

        // Parse SLD and update visual editor
        try
        {
            var parser = new SldParser();
            var style = parser.Parse(_sldXml);
            UpdateVisualEditor(style);
            _validationErrors.Clear();
        }
        catch (Exception ex)
        {
            _validationErrors.Add(ex.Message);
        }

        // Update map preview
        await UpdateMapPreview();
    }
}
```

---

### Performance Considerations

**Challenge:** Rendering large datasets in browser for preview

**Solutions:**

1. **Sample Data by Default**
   - Only load first 100-1000 features for preview
   - Show warning if dataset is large

2. **Tile-Based Preview**
   - For rasters: Generate preview tiles on server
   - Stream to browser (faster than raw data)

3. **Debounced Updates**
   - Don't update map on every slider movement
   - Wait 300ms after user stops adjusting

```csharp
private Timer? _previewUpdateTimer;

private void OnSliderChange(int value)
{
    _strokeWidth = value;

    // Debounce preview updates
    _previewUpdateTimer?.Dispose();
    _previewUpdateTimer = new Timer(async _ =>
    {
        await UpdateMapPreview();
        StateHasChanged();
    }, null, 300, Timeout.Infinite);
}
```

4. **WebGL Rendering**
   - Use OpenLayers WebGL layers for better performance
   - Can render 100K+ features at 60fps

---

### Accessibility

**Color Blindness:**
- Offer colorblind-safe palettes (ColorBrewer)
- Show patterns/textures in addition to colors

```razor
<MudAlert Severity="Severity.Info" Dense="true">
    💡 Tip: The selected palette is colorblind-safe (Deuteranopia, Protanopia, Tritanopia)
</MudAlert>
```

**Keyboard Navigation:**
- Tab through all controls
- Arrow keys adjust sliders
- Enter to apply changes
- Esc to cancel

**Screen Reader:**
```razor
<MudSlider T="int"
           aria-label="Line width in pixels"
           aria-valuemin="1"
           aria-valuemax="20"
           aria-valuenow="@_strokeWidth"
           @bind-Value="_strokeWidth" />
```

---

### Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Time to style a layer** | <2 minutes (vs. 15 min with manual SLD) | Task completion time |
| **Style editor usage** | >60% of published services use visual editor | Feature adoption |
| **SLD errors** | <5% (vs. 30% manual SLD) | Validation pass rate |
| **Style template reuse** | >40% of styles use templates | Library analytics |
| **User satisfaction** | >4.2/5 for "Styling is easy" | Post-task survey |

---

### Implementation Roadmap

**Phase 1: Simple Style Editor (Week 1-2)**
- ✅ Color picker with recent colors
- ✅ Width/opacity sliders
- ✅ Line dash patterns
- ✅ Point symbols (circle, square, triangle)
- ✅ Polygon fills with opacity
- ✅ Live preview with sample data
- ✅ Export to SLD

**Phase 2: Categorized & Graduated Styles (Week 3-4)**
- ✅ Unique value detection
- ✅ Auto-color generation (ColorBrewer)
- ✅ Jenks natural breaks algorithm
- ✅ Class editor (add/remove/reorder)
- ✅ Legend preview

**Phase 3: Style Library (Week 5)**
- ✅ Built-in templates (10+ styles)
- ✅ Save custom styles
- ✅ Share styles with team
- ✅ Import/export (SLD, MapBox)
- ✅ Search and filter

**Phase 4: Advanced Features (Week 6)**
- ✅ Scale-dependent rendering
- ✅ Label styling
- ✅ Data-driven properties (MapBox expressions)
- ✅ Composite operations (blend modes)
- ✅ SLD direct editing

**Phase 5: Polish & Optimization (Week 7)**
- ✅ Performance optimization (WebGL)
- ✅ Accessibility audit
- ✅ Usability testing
- ✅ Documentation
- ✅ Video tutorial

---

## Unified Activity Stream (Audit Log + Data Versioning)

### Overview

**Purpose:** Single unified interface combining audit logs (security events, API access, user actions) with data versioning (feature changes, WFS-T transactions, bulk imports) into a coherent activity timeline.

**Problem Solved:** Currently, audit logs and data version history are separate, making it hard to correlate "who did what" with "what data changed".

**Solution:** Unified activity stream showing all system events in chronological order with:
- 🔒 Security events (login, permission changes, failed access)
- 📝 Data changes (feature edits, bulk imports, deletions)
- 🔄 Metadata updates (service publishing, layer changes)
- 🌳 Version control (branches, merges, rollbacks)
- 📊 System events (health checks, performance alerts)

---

### Unified Activity Stream Layout

**Design:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 📋 Activity Stream                                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ Filters: [All Activity ▼] [All Users ▼] [Last 7 days ▼]       │
│ Search:  [Search activity...                              🔍]  │
│                                                                  │
│ Group by: ● Time  ○ User  ○ Resource  ○ Event Type             │
│                                                                  │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ 📅 Today                                                        │
│                                                                  │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 2:45 PM  📝 Data Edit                      sarah@city.gov │  │
│ │                                                            │  │
│ │ Edited 23 features in Roads Layer (WFS-T)                │  │
│ │ Version: v11 → v12                                        │  │
│ │ Commit: "Fixed address formatting"                        │  │
│ │                                                            │  │
│ │ [View Diff] [View on Map] [Rollback]                     │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 1:30 PM  🔒 Security Event               admin@city.gov   │  │
│ │                                                            │  │
│ │ Granted Data Publisher role to kim@city.gov              │  │
│ │ IP: 192.168.1.100                                         │  │
│ │                                                            │  │
│ │ [View User] [View Audit Trail]                           │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 11:15 AM ⚠️ Conflict Resolved            sarah@city.gov   │  │
│ │                                                            │  │
│ │ Merged feature/cleanup → main (3 conflicts)              │  │
│ │ Resolution: 2 auto-merged, 1 manual                      │  │
│ │ Version: v9 + v10.3 → v11                                │  │
│ │                                                            │  │
│ │ [View Merge Details] [View Changes]                      │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 10:00 AM 📊 Bulk Import                   mike@city.gov   │  │
│ │                                                            │  │
│ │ Imported 1,234 features from CSV into Parcels Layer      │  │
│ │ Success: 1,230  Failed: 4                                │  │
│ │ Version: v8 → v9                                          │  │
│ │                                                            │  │
│ │ [View Import Log] [View Failures] [Rollback]             │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ 📅 Yesterday                                                    │
│                                                                  │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 4:30 PM  ↩️ Rollback                      admin@city.gov   │  │
│ │                                                            │  │
│ │ Rolled back Roads Layer to v6                            │  │
│ │ Reason: "Bad data import - reverting"                    │  │
│ │ Changes: Reverted 47 features, deleted 12, restored 3    │  │
│ │ New version: v10                                          │  │
│ │                                                            │  │
│ │ [View Rollback Details] [Undo Rollback]                  │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 2:15 PM  🔀 Branch Created                mike@city.gov   │  │
│ │                                                            │  │
│ │ Created branch feature/cleanup from v6                   │  │
│ │ Reason: "Testing address cleanup"                        │  │
│ │                                                            │  │
│ │ [View Branch] [Compare with Main]                        │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 9:00 AM  🔴 Failed Login Attempt          (unknown)       │  │
│ │                                                            │  │
│ │ Failed login for user: admin@city.gov                    │  │
│ │ IP: 203.0.113.42  User Agent: curl/7.68.0                │  │
│ │ Attempts: 3 in last 5 minutes                            │  │
│ │                                                            │  │
│ │ [Block IP] [View Login History]                          │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ [Load More Activity...]                                         │
└─────────────────────────────────────────────────────────────────┘
```

**Benefits of Unified View:**
- ✅ See data changes in context of who made them
- ✅ Correlate security events with data modifications
- ✅ Single timeline for compliance audits
- ✅ Detect suspicious patterns (e.g., failed login followed by data deletion)
- ✅ Unified search across all activity types

---

### Activity Type Classification

**1. Data Changes (from Versioning System)**
```
Icon: 📝  Color: Blue
- Feature edits (WFS-T UPDATE, INSERT, DELETE)
- Bulk imports
- Feature attribute updates
- Geometry changes
```

**2. Security Events (from Audit Log)**
```
Icon: 🔒  Color: Orange
- Login/logout
- Permission changes
- Role assignments
- Failed access attempts
- Token generation/revocation
```

**3. Metadata Updates (from Publishing Workflow)**
```
Icon: 📦  Color: Green
- Service published/unpublished
- Layer added/modified/deleted
- Style updates
- Service configuration changes
```

**4. Version Control (from Versioning System)**
```
Icon: 🌳  Color: Purple
- Branch created/deleted
- Merge completed
- Rollback executed
- Conflicts detected/resolved
```

**5. System Events (from Health Monitoring)**
```
Icon: 📊  Color: Gray
- Service health status change
- Performance degradation
- Cache invalidation
- Background job completed
```

**6. Alerts & Warnings (from Monitoring)**
```
Icon: ⚠️  Color: Red
- Merge conflicts
- Data validation errors
- Security threats detected
- Service failures
```

---

### Detailed Activity Card (Expandable)

**Data Edit Event:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 2:45 PM  📝 Data Edit                          sarah@city.gov   │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ Edited 23 features in Roads Layer via WFS-T                    │
│ Version: v11 → v12                                              │
│ Commit: "Fixed address formatting"                              │
│ IP: 192.168.1.50  User Agent: QGIS/3.34                        │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ Changes Summary:                                            │ │
│ │ • 23 features modified                                      │ │
│ │ • Fields changed: address (23), name (15), lanes (8)       │ │
│ │ • No geometry changes                                       │ │
│ │ • Duration: 2 minutes 34 seconds                           │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ Modified Features:                                              │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ road-123: "Main St" → "Main Street", lanes: 2 → 4        │  │
│ │ road-456: "Elm Ave" → "Elm Avenue"                        │  │
│ │ road-789: address: "123" → "123 Main St"                 │  │
│ │ ... 20 more features                       [Show All ▼]  │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│ Related Activity:                                               │
│ • 10:00 AM: Same user imported CSV data (v11)                  │
│ • 11:30 AM: Same user will publish metadata update             │
│                                                                  │
│ Actions:                                                        │
│ [View Full Diff] [View on Map] [Rollback] [Export Changes]    │
│ [Contact User] [Flag for Review]                               │
└─────────────────────────────────────────────────────────────────┘
```

**Security Event:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 1:30 PM  🔒 Permission Grant                   admin@city.gov   │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ Granted Data Publisher role to kim@city.gov                    │
│ IP: 192.168.1.100  User Agent: Mozilla/5.0...                  │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ Permission Details:                                         │ │
│ │ • Target User: kim@city.gov                                │ │
│ │ • Role: Data Publisher                                     │ │
│ │ • Permissions Added:                                       │ │
│ │   - honua:data:write                                       │ │
│ │   - honua:services:publish                                │ │
│ │   - honua:layers:edit                                     │ │
│ │ • Effective: Immediately                                   │ │
│ │ • Expires: Never (permanent)                              │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ Audit Context:                                                  │
│ • Granted by: admin@city.gov (Administrator role)              │
│ │ • Request ID: req_abc123                                    │
│ • Compliance: Logged for SOC2/GDPR                            │
│                                                                  │
│ Related Activity:                                               │
│ • 1:35 PM: kim@city.gov logged in (first time)                 │
│ • 1:40 PM: kim@city.gov edited Parcels layer                   │
│                                                                  │
│ Actions:                                                        │
│ [View User Profile] [View Full Audit Trail] [Revoke Access]   │
│ [Export for Compliance]                                        │
└─────────────────────────────────────────────────────────────────┘
```

**Merge Conflict Resolution:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 11:15 AM ⚠️ Merge Completed                    sarah@city.gov   │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ Merged feature/cleanup → main                                  │
│ Base: v9  Main: v10  Branch: v10.3  Result: v11               │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ Merge Statistics:                                           │ │
│ │ • Total changes: 47                                        │ │
│ │ • Auto-merged: 44 (93.6%)                                 │ │
│ │ • Manual resolution: 3 conflicts                          │ │
│ │ • Strategy: AutoMerge with manual fallback               │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ Conflicts Resolved:                                             │
│ 1. Feature road-789, field: address                            │
│    Base: "123 Main"  Main: "123 Main Street"                   │
│    Branch: "123 Main St Unit A"                                │
│    Resolution: Custom → "123 Main Street Unit A"               │
│                                                                  │
│ 2. Feature road-456, field: lanes                              │
│    Resolution: Used Main (4 lanes)                             │
│                                                                  │
│ 3. Feature road-234, field: speed_limit                        │
│    Resolution: Used Branch (45 mph)                            │
│                                                                  │
│ Related Activity:                                               │
│ • 2 days ago: sarah@city.gov created branch feature/cleanup    │
│ • 11:00 AM: Conflicts detected (3 conflicts)                   │
│ • 11:10 AM: sarah@city.gov resolved all conflicts              │
│                                                                  │
│ Actions:                                                        │
│ [View Merge Diff] [View Branch History] [Undo Merge]          │
└─────────────────────────────────────────────────────────────────┘
```

**Metadata Update:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 1:00 PM  📦 Metadata Update                    sarah@city.gov   │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ Published Roads WMS Service                                     │
│ Status: Draft → Published                                       │
│ IP: 192.168.1.50  User Agent: Mozilla/5.0 (Admin UI)           │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ Changes Made:                                               │ │
│ │                                                             │ │
│ │ Service Configuration:                                      │ │
│ │ • Title: "Roads" → "City Roads Network"                   │ │
│ │ • Abstract: Updated with usage guidelines                 │ │
│ │ • Keywords: Added "transportation", "infrastructure"      │ │
│ │ • Max Features: 1000 → 5000                               │ │
│ │                                                             │ │
│ │ Layer Changes:                                              │ │
│ │ • Added layer: "highways" (LineString, 1,234 features)    │ │
│ │ • Modified layer: "roads" - updated CRS to EPSG:3857     │ │
│ │ • Removed layer: "deprecated_roads"                       │ │
│ │                                                             │ │
│ │ Style Updates:                                              │ │
│ │ • Applied new style: "Roads (Categorized by Type)"        │ │
│ │ • Updated legend                                           │ │
│ │                                                             │ │
│ │ Security:                                                   │ │
│ │ • Enabled authentication for editing                       │ │
│ │ • Added role requirement: "data-publisher"                │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ Validation Results:                                             │
│ ✅ All layers validated successfully                            │
│ ✅ Health check passed (response time: 45ms)                   │
│ ✅ No conflicts with existing services                         │
│ ⚠️  Warning: Max features increased (may affect performance)   │
│                                                                  │
│ Publishing Details:                                             │
│ • Snapshot created: snapshot_20250115_130000                   │
│ • Previous snapshot: snapshot_20250110_094500 (5 days ago)    │
│ • Metadata version: v23 → v24                                  │
│ • Services reloaded: 3 instances in 2.1 seconds               │
│                                                                  │
│ Related Activity:                                               │
│ • 10:00 AM: Same user imported data for highways layer         │
│ • 11:30 AM: Same user updated style in visual editor           │
│ • 12:45 PM: Same user ran validation checks                    │
│ • 1:02 PM: Service metadata cached across all nodes            │
│                                                                  │
│ Impact:                                                         │
│ • 3 server instances reloaded                                  │
│ • 45 active WMS connections (minimal disruption)               │
│ • Cache invalidated for affected endpoints                     │
│ • GetCapabilities updated automatically                        │
│                                                                  │
│ Actions:                                                        │
│ [View Service] [Compare with Previous] [Rollback Metadata]    │
│ [View Snapshot Diff] [Export Configuration] [Test Endpoints]  │
└─────────────────────────────────────────────────────────────────┘
```

**Metadata Rollback:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 3:15 PM  ↩️ Metadata Rollback                  admin@city.gov   │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ Rolled back Parcels WFS to previous configuration              │
│ Metadata version: v18 → v17 (reverted 1 version)               │
│ Reason: "Incorrect CRS caused client errors"                   │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ Changes Reverted:                                           │ │
│ │                                                             │ │
│ │ Service Configuration:                                      │ │
│ │ • CRS: EPSG:3857 → EPSG:4326 (reverted)                   │ │
│ │ • OutputFormats: Removed GML 3.2 (reverted to GML 2.1)    │ │
│ │                                                             │ │
│ │ Layer Configuration:                                        │ │
│ │ • parcels: CRS EPSG:3857 → EPSG:4326                      │ │
│ │ • parcels: Restored original extent                        │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ Rollback Details:                                               │
│ • Restored from snapshot: snapshot_20250114_153000             │
│ • Metadata diff applied automatically                          │
│ • Services reloaded: 3 instances in 1.8 seconds               │
│ • Affected clients: ~120 active connections (briefly queued)  │
│                                                                  │
│ Triggered by:                                                   │
│ • 15 client errors in last 10 minutes                         │
│ • 8 support tickets filed                                      │
│ • Health check warning: CRS mismatch detected                  │
│                                                                  │
│ Related Activity:                                               │
│ • 2:00 PM: kim@city.gov published metadata update (v18)        │
│ • 2:05 PM: First client error logged                           │
│ • 3:00 PM: Alert triggered: High error rate                    │
│ • 3:10 PM: admin@city.gov investigated issue                   │
│                                                                  │
│ Post-Rollback Status:                                           │
│ ✅ Error rate returned to normal (<1% error rate)              │
│ ✅ Client connections stable                                    │
│ ✅ Health checks passing                                        │
│                                                                  │
│ Actions:                                                        │
│ [View Error Log] [Compare v17 vs v18] [Notify Publisher]      │
│ [Create Incident Report] [Update Documentation]                │
└─────────────────────────────────────────────────────────────────┘
```

**Style Update:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 11:30 AM 🎨 Style Update                       sarah@city.gov   │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ Updated style for Roads Layer                                   │
│ Style: "Simple" → "Categorized by Road Type"                   │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ Style Changes:                                              │ │
│ │                                                             │ │
│ │ Before (Simple Style):                                      │ │
│ │ • All roads: Solid line, 2px, #333333                     │ │
│ │                                                             │ │
│ │ After (Categorized Style):                                  │ │
│ │ • Highway:   Solid line, 4px, #E53935 (red)               │ │
│ │ • Arterial:  Solid line, 3px, #FB8C00 (orange)            │ │
│ │ • Collector: Solid line, 2px, #FFEB3B (yellow)            │ │
│ │ • Local:     Solid line, 1px, #9E9E9E (gray)              │ │
│ │                                                             │ │
│ │ Classification:                                             │ │
│ │ • Attribute: "road_type"                                   │ │
│ │ • Unique values: 4 categories detected                     │ │
│ │ • Color scheme: ColorBrewer Qualitative (Set1)            │ │
│ │ • Auto-scaled width by hierarchy                           │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ Preview:                                                        │
│ [Map preview showing styled roads - highways in red, etc.]     │
│                                                                  │
│ Style Details:                                                  │
│ • Format: SLD 1.1.0                                            │
│ • File size: 3.2 KB                                            │
│ • Cached: Yes (invalidated old style)                          │
│ • Compatible: WMS 1.3.0, WMTS 1.0.0                           │
│                                                                  │
│ Related Activity:                                               │
│ • 10:00 AM: Data imported (4 road types detected)              │
│ • 11:00 AM: Style editor opened                                │
│ • 11:25 AM: Preview generated                                  │
│ • 11:32 AM: GetMap requests started using new style            │
│                                                                  │
│ Actions:                                                        │
│ [View Style (SLD)] [Preview on Map] [Compare with Previous]   │
│ [Export Style] [Apply to Other Layers] [Revert to Previous]   │
└─────────────────────────────────────────────────────────────────┘
```

**Layer Configuration Change:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 9:30 AM  ⚙️ Layer Configuration                mike@city.gov    │
│ ──────────────────────────────────────────────────────────     │
│                                                                  │
│ Modified layer configuration: Parcels                           │
│ Changes: 5 properties updated                                   │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ Configuration Changes:                                      │ │
│ │                                                             │ │
│ │ Query Settings:                                             │ │
│ │ • MaxRecordCount: 1000 → 10000                             │ │
│ │ • MaxPageSize: 100 → 500                                   │ │
│ │ • EnablePagination: true (unchanged)                       │ │
│ │                                                             │ │
│ │ Caching:                                                    │ │
│ │ • EnableCaching: false → true                              │ │
│ │ • CacheTTL: N/A → 300 seconds                              │ │
│ │ • CacheInvalidateOnEdit: N/A → true                       │ │
│ │                                                             │ │
│ │ Editing:                                                    │ │
│ │ • AllowEditing: true (unchanged)                           │ │
│ │ • RequireAuthentication: true (unchanged)                  │ │
│ │ • RequireRole: "authenticated" → "data-publisher"          │ │
│ │                                                             │ │
│ │ Spatial Index:                                              │ │
│ │ • IndexType: R-Tree (unchanged)                            │ │
│ │ • RebuildIndex: Triggered                                  │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ Performance Impact:                                             │
│ ✅ Query performance: Expected 40% improvement                  │
│ ✅ Cache hit rate: Expected 60-70% for read queries            │
│ ⚠️  Higher role requirement may affect some users               │
│                                                                  │
│ Validation:                                                     │
│ ✅ Configuration validated                                      │
│ ✅ Spatial index rebuilt (12,345 features in 2.3 seconds)      │
│ ✅ Cache warmed with top 100 queries                           │
│ ✅ No breaking changes detected                                │
│                                                                  │
│ Related Activity:                                               │
│ • 9:00 AM: Performance issue reported (slow queries)           │
│ • 9:15 AM: admin@city.gov reviewed slow query log              │
│ • 9:25 AM: Decision to enable caching                          │
│ • 9:35 AM: First cached queries served                         │
│                                                                  │
│ Actions:                                                        │
│ [View Layer Details] [Test Query Performance] [View Metrics]  │
│ [Revert Configuration] [Export Config] [Apply to Other Layers]│
└─────────────────────────────────────────────────────────────────┘
```

---

### Advanced Filtering & Search

**Filter Panel:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 🔍 Advanced Filters                                        [X]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ Event Types:                                                    │
│ ☑ Data Changes          ☑ Security Events                      │
│ ☑ Metadata Updates      ☑ Version Control                      │
│ ☑ System Events         ☑ Alerts & Warnings                    │
│                                                                  │
│ Users:                                                          │
│ [All Users ▼] or [Select Users...]                             │
│ ☑ sarah@city.gov  ☑ mike@city.gov  ☐ admin@city.gov           │
│                                                                  │
│ Resources:                                                      │
│ [All Layers ▼] or [Select Layers...]                           │
│ ☑ Roads  ☑ Parcels  ☐ Zoning                                   │
│                                                                  │
│ Time Range:                                                     │
│ ● Last 7 days                                                   │
│ ○ Last 30 days                                                  │
│ ○ Last 90 days                                                  │
│ ○ Custom: [Jan 1, 2025] to [Jan 15, 2025]                     │
│                                                                  │
│ Severity:                                                       │
│ ☑ Info   ☑ Warning   ☑ Error   ☑ Critical                     │
│                                                                  │
│ Advanced:                                                       │
│ ☑ Show only my activity                                        │
│ ☐ Show only flagged events                                     │
│ ☐ Show only conflicts                                          │
│ ☐ Show only rollbacks                                          │
│ ☑ Include system events                                        │
│                                                                  │
│ Sort by:                                                        │
│ ● Newest first  ○ Oldest first  ○ Most impactful              │
│                                                                  │
│                            [Reset Filters] [Apply Filters →]   │
└─────────────────────────────────────────────────────────────────┘
```

**Search:**
```
Natural language search:
• "Show me all changes by sarah last week"
• "Failed logins from external IPs"
• "Rollbacks in Roads layer"
• "Merge conflicts resolved manually"
• "Data imports that failed"
```

---

### Correlation & Pattern Detection

**Suspicious Activity Detection:**
```
⚠️ Suspicious Pattern Detected

🔴 Multiple failed logins followed by data deletion
    9:00 AM: 3 failed login attempts for admin@city.gov from 203.0.113.42
    9:05 AM: admin@city.gov logged in from 203.0.113.42
    9:07 AM: Deleted 234 features from Parcels layer

    [Investigate] [Flag User] [Block IP] [Rollback Data]

🟡 Unusual bulk changes after hours
    2:30 AM: mike@city.gov edited 1,500 features
    Note: User typically works 9am-5pm

    [View Details] [Contact User] [Flag for Review]

🟢 Normal activity - High volume data sync
    10:00 AM: Bulk import 10,000 features (scheduled job)
    Status: Success

    [View Import Log]
```

**Related Activity Timeline:**
```
For user: sarah@city.gov, viewing: Today

Timeline:
├─ 2:45 PM: Edited 23 features (Roads)
├─ 1:00 PM: Published metadata update (Roads WMS)
├─ 11:15 AM: Resolved merge conflict (feature/cleanup → main)
├─ 10:30 AM: Reviewed changes (diff v10 → v11)
├─ 10:00 AM: Imported CSV (1,234 features)
├─ 9:15 AM: Logged in
└─ 9:00 AM: Generated API token (expires in 24h)

Pattern: Normal workflow - import, review, merge, publish
```

---

### Export & Compliance

**Export Options:**
```
┌─────────────────────────────────────────────────────────────────┐
│ 📤 Export Activity Log                                     [X]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ Date Range: [Jan 1, 2025] to [Jan 15, 2025]                   │
│                                                                  │
│ Format:                                                         │
│ ● CSV (Excel compatible)                                       │
│ ○ JSON (machine readable)                                      │
│ ○ PDF (compliance report)                                      │
│ ○ SIEM format (Splunk, ELK)                                    │
│                                                                  │
│ Include:                                                        │
│ ☑ All event details                                            │
│ ☑ User information                                             │
│ ☑ IP addresses                                                 │
│ ☑ Data change diffs                                            │
│ ☐ PII (requires administrator role)                            │
│                                                                  │
│ Compliance:                                                     │
│ ☑ Include GDPR data subject identifiers                        │
│ ☑ Include SOC2 audit metadata                                  │
│ ☑ Digitally sign report (timestamp authority)                  │
│                                                                  │
│ Purpose (required for audit):                                   │
│ [Annual compliance audit for SOC2                            ]  │
│                                                                  │
│                                      [Cancel] [Export →]        │
└─────────────────────────────────────────────────────────────────┘
```

---

### Real-Time Updates

**Live Activity Feed:**
```razor
<MudPaper Class="activity-stream">
    <!-- Real-time notification -->
    @if (_hasNewActivity)
    {
        <MudAlert Severity="Severity.Info" Dense="true" Class="new-activity-banner">
            <MudStack Row="true" AlignItems="AlignItems.Center" Justify="Justify.SpaceBetween">
                <MudText>New activity available (@_newActivityCount events)</MudText>
                <MudButton Size="Size.Small" OnClick="LoadNewActivity">Refresh</MudButton>
            </MudStack>
        </MudAlert>
    }

    <!-- Activity timeline -->
    <MudTimeline TimelinePosition="TimelinePosition.Start">
        @foreach (var activity in _activities)
        {
            <MudTimelineItem Color="@GetActivityColor(activity)"
                             Icon="@GetActivityIcon(activity)"
                             TimelineAlign="TimelineAlign.Start">
                <ItemOpposite>
                    <MudText Typo="Typo.caption">@activity.Timestamp.ToString("h:mm tt")</MudText>
                </ItemOpposite>
                <ItemContent>
                    <ActivityCard Activity="@activity" OnExpand="LoadActivityDetails" />
                </ItemContent>
            </MudTimelineItem>
        }
    </MudTimeline>
</MudPaper>

@code {
    private List<Activity> _activities = new();
    private bool _hasNewActivity;
    private int _newActivityCount;

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to SignalR for real-time updates
        await ActivityHub.On<ActivityNotification>("ActivityCreated", notification =>
        {
            _hasNewActivity = true;
            _newActivityCount++;
            StateHasChanged();
        });

        await LoadActivities();
    }

    private async Task LoadNewActivity()
    {
        var newActivities = await Http.GetFromJsonAsync<List<Activity>>(
            $"/admin/activity?since={_activities.First().Timestamp}");

        _activities.InsertRange(0, newActivities);
        _hasNewActivity = false;
        _newActivityCount = 0;
    }
}
```

---

### Integration Points

**Admin API Endpoints:**
```csharp
// Unified activity stream
app.MapGet("/admin/activity",
    async (ActivityFilters filters, IActivityService activityService) =>
{
    var activities = await activityService.GetActivitiesAsync(filters);
    return Results.Ok(activities);
})
.RequireAuthorization("RequireDataPublisher");

// Activity details
app.MapGet("/admin/activity/{id}",
    async (string id, IActivityService activityService) =>
{
    var activity = await activityService.GetActivityDetailsAsync(id);
    return Results.Ok(activity);
});

// Real-time subscription
app.MapHub<ActivityHub>("/hubs/activity");

// Export
app.MapPost("/admin/activity/export",
    async (ExportRequest request, IActivityService activityService) =>
{
    var export = await activityService.ExportActivitiesAsync(request);
    return Results.File(export.Data, export.ContentType, export.FileName);
})
.RequireAuthorization("RequireAdministrator");
```

**Activity Service (Unified):**
```csharp
public class ActivityService : IActivityService
{
    private readonly IVersioningService _versioningService;
    private readonly IAuditLogService _auditLogService;
    private readonly IMetadataProvider _metadataProvider;

    public async Task<List<Activity>> GetActivitiesAsync(ActivityFilters filters)
    {
        var activities = new List<Activity>();

        // Fetch from versioning system
        if (filters.IncludeDataChanges)
        {
            var dataChanges = await _versioningService.GetHistoryAsync(filters);
            activities.AddRange(dataChanges.Select(ToActivity));
        }

        // Fetch from audit log
        if (filters.IncludeSecurityEvents || filters.IncludeMetadataChanges)
        {
            var auditLogs = await _auditLogService.GetAuditLogsAsync(filters);
            activities.AddRange(auditLogs.Select(ToActivity));
        }

        // Merge and sort by timestamp
        return activities.OrderByDescending(a => a.Timestamp).ToList();
    }

    private Activity ToActivity(VersionHistory version)
    {
        return new Activity
        {
            Id = $"version-{version.Version}",
            Type = ActivityType.DataChange,
            Timestamp = version.VersionCreatedAt,
            User = version.VersionCreatedBy,
            Resource = version.EntityId.ToString(),
            Summary = $"Edited {version.ChangeCount} features",
            Details = version,
            Icon = "📝",
            Color = "info"
        };
    }

    private Activity ToActivity(AuditLogEntry audit)
    {
        return new Activity
        {
            Id = $"audit-{audit.Id}",
            Type = ActivityType.SecurityEvent,
            Timestamp = audit.Timestamp,
            User = audit.UserId,
            Resource = audit.ResourceId,
            Summary = audit.Action,
            Details = audit,
            Icon = "🔒",
            Color = "warning"
        };
    }
}
```

---

### Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Time to find suspicious activity** | <1 minute (vs. 15 min separate logs) | Investigation time |
| **Activity correlation accuracy** | >90% related events grouped | User feedback |
| **Compliance report generation** | <5 minutes (automated) | Export time |
| **Real-time notification latency** | <2 seconds | SignalR measurement |
| **User satisfaction** | >4.0/5 for "Easy to track changes" | Survey |

---

### Implementation Roadmap

**Phase 1: Unified Activity Stream (Week 1-2)**
- ✅ Single timeline view
- ✅ Event type classification
- ✅ Basic filtering (type, user, date)
- ✅ Expandable activity cards

**Phase 2: Advanced Search & Filtering (Week 3)**
- ✅ Natural language search
- ✅ Advanced filter panel
- ✅ Saved filter presets
- ✅ Export functionality

**Phase 3: Real-Time Updates (Week 4)**
- ✅ SignalR integration
- ✅ Live activity notifications
- ✅ Auto-refresh option
- ✅ New activity banner

**Phase 4: Correlation & Analytics (Week 5)**
- ✅ Suspicious pattern detection
- ✅ Related activity grouping
- ✅ User behavior analysis
- ✅ Compliance reporting

**Phase 5: Integration & Polish (Week 6-7)**
- ✅ Deep links from alerts
- ✅ Context menu actions
- ✅ Bulk operations
- ✅ Accessibility audit

---

**End of Document**

*This UX design document will evolve based on user research findings and usability testing results.*
