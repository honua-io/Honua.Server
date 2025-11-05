# Honua Form Editor - Design Document
## Web-Based Form Designer for Mobile Data Collection

**Version:** 1.0
**Date:** November 2025
**Status:** Design Phase
**Platform:** Web Admin Frontend (React/Vue/Blazor)

---

## Executive Summary

The **Honua Form Editor** is a web-based drag-and-drop form designer that enables administrators to create and manage data collection forms for the Honua Field mobile app. It provides an intuitive visual interface for defining field schemas, validation rules, conditional logic, and mobile UI widgets.

### Key Features

✅ **Drag-and-Drop Builder** - Visual form construction
✅ **Rich Field Types** - Text, numbers, dates, choices, photos, geometry
✅ **Conditional Logic** - Show/hide fields based on values
✅ **Validation Rules** - Required fields, patterns, ranges, custom rules
✅ **Mobile Preview** - See exactly how the form looks on mobile
✅ **Version Control** - Track changes and roll back
✅ **Multi-Language** - Support for internationalized forms
✅ **Templates** - Pre-built forms for common use cases

### User Benefits

**For Administrators:**
- Create forms without coding
- Rapid prototyping (minutes, not hours)
- No mobile app rebuild required
- Version control and audit trail

**For Field Workers:**
- Forms tailored to their workflows
- Less training needed (familiar patterns)
- Fewer errors (better validation)
- Faster data collection

---

## Table of Contents

1. [Competitive Analysis](#competitive-analysis)
2. [User Personas](#user-personas)
3. [User Interface Design](#user-interface-design)
4. [Form Schema Model](#form-schema-model)
5. [Field Types](#field-types)
6. [Conditional Logic](#conditional-logic)
7. [Validation Rules](#validation-rules)
8. [Mobile Preview](#mobile-preview)
9. [Templates](#templates)
10. [Version Control](#version-control)
11. [Technical Architecture](#technical-architecture)
12. [API Integration](#api-integration)
13. [Implementation Roadmap](#implementation-roadmap)
14. [Success Metrics](#success-metrics)

---

## Competitive Analysis

### Overview

The form builder market is crowded with both general-purpose (Google Forms, Typeform) and GIS-specific (Survey123, Fulcrum) solutions. This analysis identifies strengths, weaknesses, and opportunities for Honua's Form Editor.

---

### Competitor 1: Esri Survey123 Designer

**Type:** GIS-Specific Form Builder
**Pricing:** $500-1,500/user/year (part of ArcGIS)
**Platform:** Web + Desktop (Connect app)

#### Capabilities

**Field Types:** ✅ Excellent
- Text, number, date/time, choice (select_one, select_multiple)
- Geopoint, geotrace, geoshape
- Image, video, audio, signature
- Barcode/QR scanner
- Repeating groups (begin repeat)
- Hidden fields, calculate fields

**Validation:** ✅ Strong
- Required fields
- Constraints (min/max, regex patterns)
- Relevant expressions (show/hide based on conditions)
- Custom XLSForm constraints

**Skip Logic:** ✅ Excellent
- `relevant` expressions with complex conditions
- Cascading selects
- Jump logic to different sections
- Supports AND/OR logic

**Mobile Preview:** ⚠️ Limited
- Preview in Survey123 field app (must install)
- No web-based preview
- No device frame simulation

**Templates:** ✅ Good
- Pre-built templates for common surveys
- Template gallery
- Can save custom templates

**Version Control:** ❌ Weak
- Manual version tracking
- No built-in version history
- No rollback capability

**Unique Features:**
- XLSForm Excel-based authoring
- Inbox for collaborative editing
- S123 Connect desktop app for complex forms
- Integration with ArcGIS ecosystem

#### Strengths
✅ Industry standard for GIS surveys
✅ Powerful XLSForm syntax
✅ Excellent GIS field type support
✅ Strong validation and logic

#### Weaknesses
❌ Expensive ($500-1,500/year)
❌ Steep learning curve (XLSForm syntax)
❌ Limited web preview
❌ No version control
❌ Requires ArcGIS ecosystem

**Score: 8/10** (Excellent features, but expensive and complex)

---

### Competitor 2: Fulcrum Form Builder

**Type:** GIS Field Data Collection
**Pricing:** $49-99/user/month
**Platform:** Web

#### Capabilities

**Field Types:** ✅ Excellent
- Text, numeric, choice (single/multiple)
- Date, time, datetime
- Photo, video, audio, signature
- Location (point, GPS)
- Barcode/RFID scanner
- Section headers, repeatable sections
- Calculated fields (JavaScript)

**Validation:** ✅ Strong
- Required fields
- Min/max values, min/max length
- Numeric ranges
- Regex patterns
- Custom JavaScript validation

**Skip Logic:** ✅ Strong
- Data events (onChange, onLoad)
- JavaScript-based conditions
- Show/hide fields dynamically
- Complex business logic via scripts

**Mobile Preview:** ✅ Good
- Web-based preview
- Shows iOS/Android appearance
- Interactive preview
- Test validation in preview

**Templates:** ✅ Good
- Template library
- Industry-specific templates
- Can clone existing forms

**Version Control:** ⚠️ Limited
- Manual versioning
- Activity log shows changes
- No automatic version snapshots

**Unique Features:**
- AI-powered FastFill (auto-suggest)
- Robust API and webhooks
- Custom apps via App Designer
- JavaScript scripting for advanced logic

#### Strengths
✅ Powerful and flexible
✅ Strong API and integrations
✅ AI features (FastFill)
✅ Good mobile preview

#### Weaknesses
❌ Expensive ($49-99/month)
❌ JavaScript required for advanced features
❌ No automatic version control
❌ Steeper learning curve

**Score: 8.5/10** (Most powerful, but expensive)

---

### Competitor 3: QFieldCloud Form Designer

**Type:** Open-Source GIS Data Collection
**Pricing:** Free (open-source) or $25/user/month (cloud)
**Platform:** QGIS Desktop (form design), Web (cloud management)

#### Capabilities

**Field Types:** ✅ Good
- Text, numeric, date, datetime
- Choice (dropdown, radio, checkbox)
- Photo attachments
- Geometry (point, line, polygon)
- Relations (1:N)

**Validation:** ⚠️ Limited
- Required fields
- Min/max constraints
- No regex validation
- Limited custom validation

**Skip Logic:** ⚠️ Limited
- Basic conditional visibility (via QGIS expressions)
- No advanced skip logic
- Limited cascading

**Mobile Preview:** ❌ None
- No web preview
- Must use QField mobile app
- WYSIWYG only in QGIS Desktop

**Templates:** ❌ None
- No template library
- Must create from scratch in QGIS
- Can share QGIS projects

**Version Control:** ❌ None
- Manual version tracking
- No built-in history
- Git-based if using project files

**Unique Features:**
- Open-source (free)
- Full QGIS integration
- Works offline
- Strong GIS capabilities

#### Strengths
✅ Free and open-source
✅ Strong GIS capabilities
✅ Full QGIS ecosystem
✅ Works offline

#### Weaknesses
❌ Requires QGIS Desktop for form design
❌ No web-based form editor
❌ Limited validation and logic
❌ No preview or templates
❌ Poor user experience for non-GIS users

**Score: 5/10** (Good for QGIS users, poor UX for everyone else)

---

### Competitor 4: Google Forms

**Type:** General-Purpose Form Builder
**Pricing:** Free (or $6-18/user/month for Workspace)
**Platform:** Web

#### Capabilities

**Field Types:** ⚠️ Limited
- Short answer, paragraph
- Multiple choice, checkboxes, dropdown
- Linear scale, date, time
- File upload
- ❌ No signature, barcode, location

**Validation:** ⚠️ Basic
- Required fields
- Text validation (email, URL, number, regex)
- Length limits
- Custom error messages

**Skip Logic:** ✅ Good
- Section-based branching
- "Go to section based on answer"
- Simple conditional logic

**Mobile Preview:** ⚠️ Limited
- Preview mode shows mobile responsive
- No device frame
- No iOS/Android-specific preview

**Templates:** ✅ Excellent
- Huge template gallery
- Community templates
- Easy customization

**Version Control:** ⚠️ Limited
- Edit history (view only)
- Restore previous versions
- No compare or branch

**Unique Features:**
- Integration with Google Sheets
- Real-time collaboration
- Auto-save
- Quiz mode with scoring

#### Strengths
✅ Free and easy to use
✅ Excellent templates
✅ Real-time collaboration
✅ Familiar interface

#### Weaknesses
❌ No GIS field types
❌ Limited validation
❌ No advanced field types (signature, barcode)
❌ Simple skip logic only

**Score: 6/10** (Great for general forms, poor for field GIS)

---

### Competitor 5: Microsoft Forms

**Type:** General-Purpose Form Builder
**Pricing:** Free (or $5-22/user/month for Microsoft 365)
**Platform:** Web

#### Capabilities

**Field Types:** ⚠️ Limited
- Text, choice, rating, date
- File upload
- Ranking, Likert scale
- ❌ No signature, location, barcode

**Validation:** ⚠️ Basic
- Required fields
- Restrictions (number range, text length)
- Custom error messages

**Skip Logic:** ✅ Good
- Branching rules
- "Show question based on answer"
- Section jumps

**Mobile Preview:** ⚠️ Limited
- Responsive preview
- No device frames
- No platform-specific preview

**Templates:** ✅ Good
- Template library
- Recommended templates
- Themes and branding

**Version Control:** ❌ None
- No version history
- No restore capability

**Unique Features:**
- Integration with Microsoft 365
- AI-powered suggestions
- Quiz mode
- Real-time analytics

#### Strengths
✅ Free (with Microsoft 365)
✅ Simple and intuitive
✅ Good skip logic
✅ AI suggestions

#### Weaknesses
❌ No GIS capabilities
❌ Limited field types
❌ No version control
❌ Basic validation only

**Score: 5.5/10** (Good for general use, not for GIS)

---

### Competitor 6: Typeform

**Type:** Conversational Form Builder
**Pricing:** $25-83/month (per account, not per user)
**Platform:** Web

#### Capabilities

**Field Types:** ✅ Good
- Short/long text, email, phone, number
- Multiple choice, picture choice
- Date, rating, opinion scale
- File upload, payment integration
- ❌ No location, signature (limited)

**Validation:** ✅ Strong
- Required fields
- Min/max values
- Regex patterns
- Custom validation rules

**Skip Logic:** ✅ Excellent
- Logic jumps (branching)
- Calculator (computed values)
- Hidden fields
- Advanced conditional logic

**Mobile Preview:** ✅ Excellent
- Live preview mode
- Mobile responsive design
- Beautiful interface

**Templates:** ✅ Excellent
- 500+ templates
- Industry-specific
- Highly customizable

**Version Control:** ⚠️ Limited
- Duplicate forms
- Activity log
- No automatic versioning

**Unique Features:**
- Conversational UI (one question per screen)
- Beautiful design
- Video and GIF support
- Integration marketplace

#### Strengths
✅ Beautiful and engaging
✅ Excellent user experience
✅ Strong logic and validation
✅ Great templates

#### Weaknesses
❌ No GIS capabilities
❌ Expensive for teams
❌ No signature or location fields
❌ Not designed for field work

**Score: 7/10** (Excellent UX, but not for GIS)

---

### Competitor 7: JotForm

**Type:** General-Purpose Form Builder
**Pricing:** Free or $34-99/month
**Platform:** Web

#### Capabilities

**Field Types:** ✅ Excellent
- 100+ field types
- Text, email, phone, address
- File upload, signature, payment
- Date, time, appointment picker
- Geolocation widget
- Rating, scale, matrix

**Validation:** ✅ Excellent
- Required fields
- Min/max length, value
- Regex patterns
- Custom validation rules
- Conditional required

**Skip Logic:** ✅ Excellent
- Show/hide conditions
- Skip logic
- Calculated fields
- Form calculation widget

**Mobile Preview:** ✅ Good
- Mobile preview mode
- Responsive forms
- Mobile app testing

**Templates:** ✅ Excellent
- 10,000+ templates
- Industry-specific
- Highly customizable

**Version Control:** ⚠️ Limited
- Clone forms
- No automatic versioning
- Activity logs

**Unique Features:**
- 100+ integrations
- Payment processing
- PDF generation
- Conditional emails
- Approval workflows

#### Strengths
✅ Huge field type library
✅ Excellent validation and logic
✅ Massive template library
✅ Strong integrations

#### Weaknesses
❌ Limited GIS capabilities
❌ Can be overwhelming (feature bloat)
❌ No true version control
❌ Not optimized for offline field work

**Score: 7.5/10** (Very powerful, but not GIS-focused)

---

### Competitor 8: KoBoToolbox

**Type:** Humanitarian Data Collection
**Pricing:** Free (open-source)
**Platform:** Web

#### Capabilities

**Field Types:** ✅ Good
- Text, number, date, time
- Select one/multiple
- Photo, audio, video, file
- GPS location, geopoint, geotrace, geoshape
- Barcode scanner
- Acknowledge, note, calculate

**Validation:** ✅ Strong
- Required fields
- Constraints (min/max, regex)
- Relevant conditions
- XLSForm syntax

**Skip Logic:** ✅ Strong
- Relevant expressions
- Cascading selects
- Complex conditions (AND/OR)

**Mobile Preview:** ⚠️ Limited
- Preview in KoBoCollect app
- No web preview
- Must deploy to test

**Templates:** ✅ Good
- Template library
- Humanitarian-focused
- Can share templates

**Version Control:** ✅ Good
- Automatic version tracking
- Can deploy specific versions
- Version history

**Unique Features:**
- Free and open-source
- Humanitarian focus
- XLSForm compatible
- Offline data collection
- Multi-language support

#### Strengths
✅ Free and open-source
✅ Strong validation and logic
✅ Good version control
✅ XLSForm compatible
✅ Humanitarian focus

#### Weaknesses
❌ Basic UI/UX
❌ Limited web preview
❌ XLSForm learning curve
❌ Not as polished as commercial tools

**Score: 7/10** (Excellent for humanitarian work, free)

---

## Competitive Comparison Matrix

| Feature | Survey123 | Fulcrum | QField | Google Forms | MS Forms | Typeform | JotForm | KoBo | **Honua** |
|---------|-----------|---------|--------|--------------|----------|----------|---------|------|-----------|
| **Pricing** | $500-1500/yr | $49-99/mo | Free-$25/mo | Free | Free | $25-83/mo | $34-99/mo | Free | **$0-50/mo** |
| **Field Types** | 9/10 | 9/10 | 7/10 | 5/10 | 4/10 | 7/10 | 10/10 | 8/10 | **9/10** |
| **Validation** | 9/10 | 9/10 | 5/10 | 5/10 | 4/10 | 8/10 | 9/10 | 9/10 | **9/10** |
| **Skip Logic** | 10/10 | 9/10 | 4/10 | 6/10 | 6/10 | 9/10 | 9/10 | 9/10 | **10/10** |
| **Mobile Preview** | 4/10 | 7/10 | 0/10 | 5/10 | 4/10 | 8/10 | 6/10 | 3/10 | **10/10** |
| **Templates** | 7/10 | 7/10 | 0/10 | 9/10 | 6/10 | 10/10 | 10/10 | 7/10 | **8/10** |
| **Version Control** | 2/10 | 4/10 | 0/10 | 5/10 | 0/10 | 3/10 | 2/10 | 7/10 | **9/10** |
| **Ease of Use** | 5/10 | 6/10 | 3/10 | 10/10 | 9/10 | 10/10 | 7/10 | 5/10 | **9/10** |
| **GIS Features** | 10/10 | 9/10 | 10/10 | 0/10 | 0/10 | 0/10 | 3/10 | 8/10 | **10/10** |
| **Offline Support** | 10/10 | 9/10 | 10/10 | 0/10 | 0/10 | 0/10 | 2/10 | 10/10 | **10/10** |
| **API/Integrations** | 7/10 | 10/10 | 5/10 | 6/10 | 6/10 | 8/10 | 9/10 | 6/10 | **9/10** |
| **TOTAL** | 73/100 | 79/100 | 44/100 | 51/100 | 39/100 | 63/100 | 67/100 | 72/100 | **93/100** |

---

## Market Gaps & Opportunities

### Gap 1: No Excellent Web-Based GIS Form Editor

**Finding:**
- Survey123: Excel-based (XLSForm), steep learning curve
- Fulcrum: Good, but expensive ($49-99/mo)
- QField: Requires QGIS Desktop
- Others: No GIS capabilities

**Opportunity:**
✅ Honua can provide **intuitive drag-and-drop** GIS form editor
✅ Better UX than Survey123
✅ More affordable than Fulcrum
✅ Web-based (no desktop software required)

---

### Gap 2: Poor Mobile Preview

**Finding:**
- Survey123: Must install mobile app to preview
- QField: No preview at all
- Others: Generic responsive preview, no device frames

**Opportunity:**
✅ Honua provides **interactive mobile preview**
✅ iOS and Android device frames
✅ Test validation and logic in browser
✅ No app installation required

---

### Gap 3: Weak Version Control

**Finding:**
- Most tools have NO version control
- Manual versioning only
- No rollback or compare

**Opportunity:**
✅ Honua provides **automatic version control**
✅ Compare versions side-by-side
✅ Rollback to any version
✅ Full audit trail

---

### Gap 4: Limited Templates

**Finding:**
- GIS tools (Survey123, QField) have limited templates
- General tools (Typeform, JotForm) have many, but not GIS-specific

**Opportunity:**
✅ Honua provides **GIS-specific templates**
✅ Industry-focused (utilities, environmental, construction)
✅ Easy to customize
✅ Community template sharing

---

### Gap 5: Complex or No Validation

**Finding:**
- Survey123: Powerful but complex (XLSForm syntax)
- Google/MS Forms: Too basic
- QField: Very limited

**Opportunity:**
✅ Honua provides **visual validation builder**
✅ Powerful but intuitive
✅ No coding required
✅ Test validation in preview

---

### Gap 6: Limited Skip Logic UI

**Finding:**
- Survey123: Text-based expressions
- Google/MS Forms: Section-based only
- QField: Minimal

**Opportunity:**
✅ Honua provides **visual skip logic builder**
✅ Drag-and-drop conditions
✅ Preview logic flow
✅ No coding required

---

## Honua Competitive Advantages

### 1. Best-in-Class Mobile Preview ⭐
- Interactive preview with device frames
- Test validation, skip logic, and conditionals
- No app installation required
- Saves hours of testing time

### 2. Automatic Version Control ⭐
- Every publish creates new version
- Compare versions side-by-side
- Rollback to any previous version
- Full audit trail

### 3. Intuitive Visual Builder ⭐
- Drag-and-drop field placement
- No coding or Excel required
- Visual validation and logic builders
- Faster than Survey123, easier than XLSForm

### 4. GIS-Specific + General Purpose ⭐
- All the GIS field types (point, line, polygon)
- Plus general field types (text, choice, photo)
- Best of both worlds

### 5. Fair Pricing ⭐
- Free tier for small teams
- $25-50/user/month (vs $500-1500/yr for Survey123)
- No per-form fees (vs Typeform, JotForm)

### 6. Integrated with Honua Ecosystem ⭐
- Seamless integration with Honua Server
- Same authentication and permissions
- Real-time sync to mobile apps
- No separate platform needed

---

## Key Differentiators Summary

| Feature | Competitors | Honua Form Editor |
|---------|-------------|-------------------|
| **Form Design** | XLSForm syntax (complex) or basic drag-and-drop | **Visual drag-and-drop + advanced features** |
| **Mobile Preview** | Limited or requires app | **Interactive web preview with device frames** |
| **Version Control** | Manual or none | **Automatic versioning with compare/rollback** |
| **Skip Logic** | Text expressions or basic | **Visual builder with complex conditions** |
| **Validation** | Basic or coding required | **Visual builder with testing** |
| **Templates** | Limited or generic | **GIS-specific industry templates** |
| **Pricing** | $500-1500/yr or $50-99/mo | **$0-50/mo (3-10x cheaper)** |
| **Ease of Use** | Moderate to complex | **Intuitive for non-technical users** |

---

## Conclusion: Market Positioning

**Honua Form Editor positions as:**

> **"The easiest, most powerful GIS form builder at the fairest price"**

**Target Users:**
- GIS administrators frustrated with Survey123 complexity
- Organizations wanting cheaper alternative to Fulcrum
- Teams needing better UX than QField
- Anyone needing GIS + general form capabilities

**Winning Strategy:**
1. **Easier than Survey123** - Visual builder vs XLSForm
2. **Cheaper than Fulcrum** - $25-50/mo vs $49-99/mo
3. **Better UX than QField** - Web-based, no QGIS required
4. **More powerful than Google/MS Forms** - GIS capabilities
5. **Unique features** - Mobile preview, version control, skip logic builder

**Result:** Best-in-class GIS form editor that's both powerful and accessible.

---

## User Personas

### Persona 1: Sarah - GIS Administrator

**Demographics:**
- Age: 35
- Role: GIS Coordinator at municipal utility company
- Experience: 10 years in GIS, proficient with ArcGIS
- Technical Skills: High (SQL, scripting, GIS platforms)

**Goals:**
- Create inspection forms for field technicians
- Standardize data collection across teams
- Reduce data quality issues
- Integrate with existing GIS systems

**Pain Points:**
- Current form builder (Esri Survey123) is complex
- Changes require republishing entire form
- Limited conditional logic
- Expensive licensing

**How Form Editor Helps:**
- Intuitive drag-and-drop interface
- Live preview of mobile appearance
- Instant deployment (no republishing)
- Advanced conditional logic
- Cost-effective solution

---

### Persona 2: Marcus - Environmental Project Manager

**Demographics:**
- Age: 42
- Role: Project manager at environmental consulting firm
- Experience: 15 years in environmental science
- Technical Skills: Medium (Excel, basic databases)

**Goals:**
- Create custom forms for wildlife surveys
- Adapt forms mid-project as needs change
- Ensure data consistency across field staff
- Quick turnaround (same-day form changes)

**Pain Points:**
- Relies on IT department for form changes
- Long turnaround time (days to weeks)
- Forms don't match field workflow
- Can't preview how forms look on mobile

**How Form Editor Helps:**
- Self-service form creation (no IT needed)
- Make changes and deploy instantly
- Mobile preview shows exact appearance
- Templates for common survey types

---

## User Interface Design

### Overview Layout

```
┌────────────────────────────────────────────────────────────────┐
│ Honua Admin                    [Save Draft] [Preview] [Publish]│
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐  ┌────────────────────────┐  ┌─────────────┐ │
│  │             │  │                        │  │             │ │
│  │   Field     │  │    Canvas              │  │ Properties  │ │
│  │   Palette   │  │    (Form Builder)      │  │   Panel     │ │
│  │             │  │                        │  │             │ │
│  │  [Text]     │  │  ┌──────────────────┐  │  │ Field Name: │ │
│  │  [Number]   │  │  │ Asset ID         │  │  │ asset_id    │ │
│  │  [Date]     │  │  │ [text field]     │  │  │             │ │
│  │  [Dropdown] │  │  └──────────────────┘  │  │ Label:      │ │
│  │  [Photo]    │  │                        │  │ Asset ID    │ │
│  │  [Location] │  │  ┌──────────────────┐  │  │             │ │
│  │  [Signature]│  │  │ Inspection Date  │  │  │ Required:   │ │
│  │             │  │  │ [date picker]    │  │  │ ☑ Yes       │ │
│  │  + Add      │  │  └──────────────────┘  │  │             │ │
│  │    Section  │  │                        │  │ Default:    │ │
│  │             │  │  [+ Add Field]         │  │ Today       │ │
│  │             │  │                        │  │             │ │
│  └─────────────┘  └────────────────────────┘  └─────────────┘ │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

### Three-Panel Layout

**Left Panel: Field Palette (250px)**
- List of available field types
- Drag fields to canvas
- Collapsible sections (Basic, Advanced, Media, Location)
- Search/filter field types

**Center Panel: Canvas (Flexible)**
- Drop zone for fields
- Visual representation of form
- Reorder fields via drag-and-drop
- Click to select/edit field
- Add sections/groups
- Mobile device frame preview

**Right Panel: Properties Panel (300px)**
- Field-specific properties
- General settings (name, label, placeholder)
- Validation rules
- Conditional logic
- Mobile widget settings
- Help text and tooltips

---

---

### Responsive Design: Adaptive Layouts

The form editor adapts across **desktop, tablet, and mobile** devices to provide optimal editing experience at any screen size.

#### Desktop View (> 1200px) - Three Panel Layout

**Best For:** Primary form editing, complex forms
**Layout:** Field Palette (250px) | Canvas (flexible) | Properties Panel (300px)

```
┌────────────────────────────────────────────────────────────────┐
│  [Field Palette]  |    [Canvas]         |  [Properties Panel] │
│     250px         |    Flexible         |       300px         │
└────────────────────────────────────────────────────────────────┘
```

---

#### Tablet View (768px - 1200px) - Collapsible Panels

**Best For:** Field edits, form review, moderate editing
**Layout:** Collapsible sidebars with tabs

**Portrait Mode:**
```
┌──────────────────────────────┐
│  ☰ Palette  [Canvas]  ⚙️ Props │  ← Toggle buttons
├──────────────────────────────┤
│                              │
│        Canvas Area           │
│     (Full Width)             │
│                              │
│  ┌────────────────────────┐  │
│  │ Asset ID               │  │
│  │ [text field]           │  │
│  └────────────────────────┘  │
│                              │
│  ┌────────────────────────┐  │
│  │ Asset Type             │  │
│  │ [dropdown]             │  │
│  └────────────────────────┘  │
│                              │
│  [+ Add Field]               │
│                              │
└──────────────────────────────┘
```

**When Palette/Properties Open (Bottom Sheet):**
```
┌──────────────────────────────┐
│        Canvas Area           │
│     (Partially Visible)      │
├──────────────────────────────┤
│ ┌──────────────────────────┐ │  ← Bottom sheet (60% height)
│ │ Field Palette         [X]│ │
│ ├──────────────────────────┤ │
│ │  [Text Field]            │ │
│ │  [Number]                │ │
│ │  [Date]                  │ │
│ │  [Dropdown]              │ │
│ │  [Photo]                 │ │
│ │  ...                     │ │
│ └──────────────────────────┘ │
└──────────────────────────────┘
```

**Landscape Mode:**
```
┌────────────────────────────────────────────────────────┐
│  ☰ [Toggle]  |     Canvas Area      | ⚙️ Props [Toggle] │
│              |                      |                  │
│  [Palette]   |  ┌────────────────┐  |  [Properties]    │
│  (Drawer)    |  │ Asset ID       │  |  (Drawer)        │
│              |  │ [text field]   │  |                  │
│              |  └────────────────┘  |                  │
│              |  [+ Add Field]      |                  │
└────────────────────────────────────────────────────────┘
```

---

#### Mobile View (< 768px) - Single Panel + Modal/Bottom Sheets

**Best For:** Quick edits, urgent changes, form review
**Layout:** Full-screen canvas with modals for palette and properties

```
┌─────────────────────────┐
│ ☰  Pole Inspection  ⚙️  │  ← Nav bar
├─────────────────────────┤
│                         │
│  Canvas Area            │
│  (Full Screen)          │
│                         │
│  ┌───────────────────┐  │
│  │ Asset ID *        │  │  ← Field card
│  │ [text field]      │  │
│  │ [⚙️] [🗑️]          │  │
│  └───────────────────┘  │
│                         │
│  ┌───────────────────┐  │
│  │ Asset Type *      │  │
│  │ [Utility Pole ▼]  │  │
│  │ [⚙️] [🗑️]          │  │
│  └───────────────────┘  │
│                         │
│  [+ Add Field]          │  ← Opens field palette
│                         │
├─────────────────────────┤
│ [Save] [Preview] [Pub]  │  ← Action bar
└─────────────────────────┘
```

**Field Palette (Full-Screen Modal):**
```
┌─────────────────────────┐
│ ← Select Field Type     │
├─────────────────────────┤
│ [Search fields...]      │
├─────────────────────────┤
│  Basic Fields           │
│  ─────────────          │
│  📝 Text Field          │  ← Tap to add
│  📄 Text Area           │
│  🔢 Number              │
│  📅 Date                │
│  ...                    │
│                         │
│  Media Fields           │
│  ─────────────          │
│  📷 Photo               │
│  🖊️ Signature            │
│  ...                    │
└─────────────────────────┘
```

**Field Properties (Bottom Sheet):**
```
┌─────────────────────────┐
│        Canvas           │
│     (Dimmed 50%)        │
├─────────────────────────┤  ← Drag handle
│ ═══ Field Properties    │
├─────────────────────────┤
│ Field Type: Text        │
│                         │
│ Label:                  │
│ [Asset ID_________]     │
│                         │
│ ☑ Required field        │
│ ☐ Read-only             │
│                         │
│ [Validation]  [Logic]   │  ← Tabs
│                         │
│ [Save]     [Cancel]     │
└─────────────────────────┘
```

---

### Responsive Behavior Summary

| Screen Size | Layout | Palette | Properties | Canvas | Best Use |
|-------------|--------|---------|------------|--------|----------|
| **Desktop** (>1200px) | 3-panel | Always visible | Always visible | 50-60% width | Primary editing |
| **Tablet Portrait** (768-1200px) | Canvas + toggles | Bottom sheet | Bottom sheet | Full width | Field editing, review |
| **Tablet Landscape** (768-1200px) | Canvas + drawers | Left drawer | Right drawer | 60% width | Moderate editing |
| **Mobile** (<768px) | Single panel | Full-screen modal | Bottom sheet | Full screen | Quick edits only |

---

### Interaction Patterns by Device

#### Desktop (Mouse + Keyboard)
- **Drag-and-drop:** Fields from palette to canvas
- **Right-click:** Context menu (edit, delete, duplicate)
- **Keyboard shortcuts:** Ctrl+S (save), Ctrl+Z (undo), Delete (remove field)
- **Hover states:** Show field actions on hover

#### Tablet (Touch)
- **Long press:** Show context menu
- **Tap and hold:** Drag to reorder fields
- **Swipe:** Navigate between tabs (validation, logic, widget)
- **Pinch to zoom:** Canvas zoom (useful for complex forms)
- **Two-finger scroll:** Canvas navigation

#### Mobile (Touch)
- **Tap:** Select field, open properties
- **Drag handles:** Explicit drag handles for reordering (⋮ icon)
- **Bottom sheet:** Swipe up/down to expand/collapse properties
- **Full-screen modals:** For palette and complex dialogs
- **Fixed action bar:** Always visible save/preview/publish buttons

---

### Responsive UI Components

#### Collapsible Field Palette (Tablet/Mobile)

```tsx
// Toggle button on tablet
<Button
  icon="☰"
  onClick={() => setPaletteVisible(true)}
  aria-label="Open field palette"
/>

// Bottom sheet on tablet (portrait)
<BottomSheet
  isOpen={paletteVisible}
  height="60vh"
  onClose={() => setPaletteVisible(false)}
>
  <FieldPalette onFieldSelect={handleFieldAdd} />
</BottomSheet>

// Full-screen modal on mobile
<Modal
  isOpen={paletteVisible}
  fullScreen
  onClose={() => setPaletteVisible(false)}
>
  <FieldPalette onFieldSelect={handleFieldAdd} />
</Modal>
```

#### Responsive Field Actions

**Desktop:** Always visible on hover
```
┌──────────────────────────────────┐
│  Asset ID *                      │
│  [text field]                    │
│  [⋮ Move] [⚙️ Edit] [🗑️ Delete]   │  ← Always visible on hover
└──────────────────────────────────┘
```

**Tablet:** Tap to show actions bar
```
┌──────────────────────────────────┐
│  Asset ID *              [•••]   │  ← Tap for menu
│  [text field]                    │
└──────────────────────────────────┘
```

**Mobile:** Tap field card to open properties
```
┌──────────────────────────────────┐
│  Asset ID *                      │  ← Entire card is tappable
│  [text field]                    │
│  Tap to edit                     │
└──────────────────────────────────┘
```

---

### Mobile-Specific Features

#### 1. Simplified Field Palette
- **Search-first:** Search bar at top for quick field finding
- **Recent fields:** Show last 5 used field types
- **Favorites:** Star frequently used fields
- **Categorized accordion:** Collapsible categories (Basic, Advanced, Media)

#### 2. Touch-Optimized Drag-and-Drop
- **Large drag handles:** 48×48px minimum touch targets
- **Haptic feedback:** Vibration on pick up, drop, reorder
- **Visual feedback:** Shimmer/highlight drop zones
- **Cancel zone:** Swipe far right to cancel drag

#### 3. Streamlined Properties Panel
- **Tabs instead of accordion:** Horizontal swipeable tabs
- **Essential properties first:** Label, required, placeholder
- **"Show advanced" toggle:** Hide complex features by default
- **Inline editing:** Edit label directly in canvas preview

#### 4. Offline-First Autosave
- **Auto-save every 30 seconds** (via IndexedDB)
- **Offline indicator:** Show sync status
- **Conflict resolution:** If edited on multiple devices

---

### Accessibility Considerations

#### Touch Target Sizes
- **Minimum:** 44×44px (iOS), 48×48dp (Android)
- **Recommended:** 56×56px for primary actions
- **Spacing:** 8px minimum between touch targets

#### Screen Reader Support
- **ARIA labels:** All interactive elements
- **Role annotations:** `role="button"`, `role="region"`
- **Focus management:** Proper tab order
- **Announcements:** "Field added", "Validation error added"

#### Keyboard Navigation
- **Tab order:** Logical flow through form editor
- **Shortcuts:** Documented and customizable
- **Focus indicators:** Visible focus rings
- **Skip links:** Jump to canvas, palette, properties

---

### Performance Optimizations for Mobile

#### Lazy Loading
- **Field palette:** Load field types on-demand
- **Preview rendering:** Only render visible fields
- **Image optimization:** Compress uploaded icons

#### Virtualization
- **Canvas:** Virtualize long forms (render only visible fields)
- **Field palette:** Virtual scrolling for 27+ field types

#### Bundle Splitting
- **Core editor:** ~200KB gzipped
- **Field types:** Lazy load per type (~10KB each)
- **Preview:** Separate bundle (~50KB)

---

### Top Navigation Bar

```
┌────────────────────────────────────────────────────────────────┐
│ ← Back to Forms    Form: Utility Pole Inspection    v1.2       │
│                                                                 │
│ [Save Draft]  [Preview on Mobile]  [Publish]  [⋮ More]         │
└────────────────────────────────────────────────────────────────┘
```

**Actions:**
- **Save Draft:** Save without publishing (auto-save every 30s)
- **Preview on Mobile:** Show mobile preview
- **Publish:** Deploy to mobile apps
- **More Menu:**
  - Export as JSON
  - Import from JSON
  - Duplicate form
  - View version history
  - Delete form

---

### Field Palette (Left Panel)

**Basic Fields:**
```
┌─────────────────────┐
│ Basic Fields        │
├─────────────────────┤
│ 📝 Text Field       │
│ 📄 Text Area        │
│ 🔢 Number           │
│ 📅 Date             │
│ 🕐 Date & Time      │
│ ☑️ Checkbox          │
│ 🔘 Radio Buttons    │
│ 📋 Dropdown         │
└─────────────────────┘
```

**Advanced Fields:**
```
┌─────────────────────┐
│ Advanced Fields     │
├─────────────────────┤
│ 📧 Email            │
│ 📞 Phone Number     │
│ 🔗 URL              │
│ 🔢 Calculated       │
│ 📊 Range/Slider     │
│ ⭐ Rating           │
│ 🔤 Barcode/QR       │
└─────────────────────┘
```

**Media Fields:**
```
┌─────────────────────┐
│ Media & Files       │
├─────────────────────┤
│ 📷 Photo            │
│ 🎥 Video            │
│ 🎤 Audio            │
│ 🖊️ Signature         │
│ ✏️ Sketch            │
│ 📎 File Attachment  │
└─────────────────────┘
```

**Location Fields:**
```
┌─────────────────────┐
│ Location & Geometry │
├─────────────────────┤
│ 📍 Point (GPS)      │
│ 📏 Line             │
│ ▭ Polygon           │
│ 🗺️ Address           │
└─────────────────────┘
```

**Layout Elements:**
```
┌─────────────────────┐
│ Layout              │
├─────────────────────┤
│ 📦 Section Header   │
│ ━━ Divider          │
│ 💬 Help Text        │
│ 🔁 Repeating Group  │
└─────────────────────┘
```

---

### Canvas (Center Panel)

**Empty State:**
```
┌────────────────────────────────────────┐
│                                        │
│         📱 Start Building Your Form     │
│                                        │
│    Drag fields from the left palette   │
│         or click below to start        │
│                                        │
│         [+ Add Your First Field]       │
│                                        │
│                                        │
└────────────────────────────────────────┘
```

**With Fields:**
```
┌────────────────────────────────────────┐
│  Section: Asset Information            │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│                                        │
│  Asset ID *                            │
│  [text field]                          │
│  [⋮ Move] [⚙️ Edit] [🗑️ Delete]         │
│                                        │
│  Asset Type *                          │
│  [Utility Pole ▼]                      │
│  [⋮ Move] [⚙️ Edit] [🗑️ Delete]         │
│                                        │
│  Installation Date                     │
│  [MM/DD/YYYY]                          │
│  [⋮ Move] [⚙️ Edit] [🗑️ Delete]         │
│                                        │
│  + Add Field                           │
│                                        │
│  Section: Inspection Details           │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│                                        │
│  Condition Rating *                    │
│  ⭐⭐⭐⭐⭐                              │
│  [⋮ Move] [⚙️ Edit] [🗑️ Delete]         │
│                                        │
│  + Add Field                           │
│                                        │
│  + Add Section                         │
└────────────────────────────────────────┘
```

**Field Interactions:**
- **Click field:** Select and show properties
- **Drag handle (⋮):** Reorder fields
- **Edit (⚙️):** Quick edit inline
- **Delete (🗑️):** Remove field (with confirmation)
- **Hover:** Show field actions

---

### Properties Panel (Right Panel)

**General Properties:**
```
┌─────────────────────────────────────┐
│ Field Properties                    │
├─────────────────────────────────────┤
│                                     │
│ Field Type: Text Field              │
│ (Cannot change after creation)      │
│                                     │
│ Internal Name:                      │
│ [asset_id________________]          │
│ (Used in database, no spaces)       │
│                                     │
│ Label: *                            │
│ [Asset ID_______________]           │
│ (Shown to users)                    │
│                                     │
│ Placeholder:                        │
│ [e.g., POLE-12345_______]           │
│                                     │
│ Help Text:                          │
│ [Unique identifier for_____]        │
│ [this asset______________]          │
│                                     │
│ ☑ Required field                    │
│ ☐ Read-only                         │
│ ☐ Hidden by default                 │
│                                     │
└─────────────────────────────────────┘
```

**Validation Tab:**
```
┌─────────────────────────────────────┐
│ Validation Rules                    │
├─────────────────────────────────────┤
│                                     │
│ ☑ Minimum length: [5___]            │
│ ☑ Maximum length: [50__]            │
│ ☐ Pattern (regex):                  │
│   [___________________]             │
│   Example: ^[A-Z]{4}-\d{5}$         │
│                                     │
│ ☐ Custom validation:                │
│   [JavaScript expression__]         │
│                                     │
│ Error Message:                      │
│ [Asset ID must be 5-50_]            │
│ [characters__________]              │
│                                     │
└─────────────────────────────────────┘
```

**Conditional Logic Tab:**
```
┌─────────────────────────────────────┐
│ Conditional Logic                   │
├─────────────────────────────────────┤
│                                     │
│ Show this field when:               │
│                                     │
│ [asset_type         ▼]              │
│ [equals             ▼]              │
│ [Utility Pole       ▼]              │
│                                     │
│ + Add Condition (AND/OR)            │
│                                     │
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━   │
│                                     │
│ Set default value when:             │
│                                     │
│ [inspection_type    ▼]              │
│ [equals             ▼]              │
│ [Routine            ▼]              │
│                                     │
│ Default to: [Good___________]       │
│                                     │
│ + Add Rule                          │
│                                     │
└─────────────────────────────────────┘
```

**Mobile Widget Tab:**
```
┌─────────────────────────────────────┐
│ Mobile Display Settings             │
├─────────────────────────────────────┤
│                                     │
│ Widget Type:                        │
│ ⦿ Single-line text input            │
│ ○ Multi-line text area              │
│ ○ Barcode scanner                   │
│                                     │
│ Keyboard Type:                      │
│ [Default            ▼]              │
│ Options: Default, Numeric, Email,   │
│          Phone, URL                 │
│                                     │
│ Autocomplete:                       │
│ ☑ Enable autocomplete               │
│ ☐ Show recent values                │
│ ☐ Enable AI suggestions             │
│                                     │
│ Width:                              │
│ ⦿ Full width                        │
│ ○ Half width                        │
│ ○ Third width                       │
│                                     │
└─────────────────────────────────────┘
```

---

### Mobile Preview Modal

```
┌────────────────────────────────────────────────────────┐
│  Mobile Preview                            [X] Close   │
├────────────────────────────────────────────────────────┤
│                                                        │
│    ┌──────────────────────┐                           │
│    │  ◉ ◉ ◉    12:34 PM  │  [iPhone 14▼] [Android▼] │
│    ├──────────────────────┤                           │
│    │                      │                           │
│    │  Utility Pole        │                           │
│    │  Inspection Form     │                           │
│    │                      │                           │
│    │  Asset ID *          │                           │
│    │  ┌────────────────┐  │                           │
│    │  │ POLE-12345     │  │                           │
│    │  └────────────────┘  │                           │
│    │                      │                           │
│    │  Asset Type *        │                           │
│    │  ┌────────────────┐  │                           │
│    │  │ Utility Pole  ▼│  │                           │
│    │  └────────────────┘  │                           │
│    │                      │                           │
│    │  Installation Date   │                           │
│    │  ┌────────────────┐  │                           │
│    │  │ 01/15/2023     │  │                           │
│    │  └────────────────┘  │                           │
│    │                      │                           │
│    │  [Continue]          │                           │
│    │                      │                           │
│    └──────────────────────┘                           │
│                                                        │
│  [⟨ Previous Field]  [Next Field ⟩]                   │
│                                                        │
└────────────────────────────────────────────────────────┘
```

**Preview Features:**
- Switch between iPhone and Android rendering
- Navigate through form fields
- Interactive (can fill out form)
- Shows validation errors
- Tests conditional logic
- Displays exact mobile appearance

---

## Form Schema Model

### JSON Schema Format

```json
{
  "id": "utility-pole-inspection-v1",
  "version": "1.2.0",
  "title": "Utility Pole Inspection",
  "description": "Standard inspection form for utility poles",
  "created_at": "2025-01-15T10:30:00Z",
  "updated_at": "2025-03-20T14:15:00Z",
  "created_by": "sarah.admin@utility.com",
  "status": "published",
  "geometry_type": "point",
  "sections": [
    {
      "id": "asset-info",
      "title": "Asset Information",
      "description": "Basic asset identification",
      "collapsed": false,
      "fields": [
        {
          "id": "asset_id",
          "type": "text",
          "label": "Asset ID",
          "placeholder": "e.g., POLE-12345",
          "help_text": "Unique identifier for this asset",
          "required": true,
          "read_only": false,
          "validation": {
            "min_length": 5,
            "max_length": 50,
            "pattern": "^[A-Z]{4}-\\d{5}$",
            "error_message": "Asset ID must follow format: POLE-12345"
          },
          "widget": {
            "type": "text_field",
            "keyboard_type": "default",
            "autocomplete": true,
            "width": "full"
          }
        },
        {
          "id": "asset_type",
          "type": "choice",
          "label": "Asset Type",
          "required": true,
          "choices": [
            { "value": "utility_pole", "label": "Utility Pole" },
            { "value": "transformer", "label": "Transformer" },
            { "value": "junction_box", "label": "Junction Box" }
          ],
          "widget": {
            "type": "dropdown"
          }
        },
        {
          "id": "installation_date",
          "type": "date",
          "label": "Installation Date",
          "required": false,
          "default_value": null,
          "widget": {
            "type": "date_picker",
            "min_date": "1950-01-01",
            "max_date": "today"
          }
        }
      ]
    },
    {
      "id": "inspection-details",
      "title": "Inspection Details",
      "fields": [
        {
          "id": "condition_rating",
          "type": "integer",
          "label": "Condition Rating",
          "required": true,
          "validation": {
            "min": 1,
            "max": 5
          },
          "widget": {
            "type": "rating",
            "icon": "star",
            "max_rating": 5
          }
        },
        {
          "id": "issues_found",
          "type": "text",
          "label": "Issues Found",
          "required": false,
          "widget": {
            "type": "text_area",
            "rows": 4
          },
          "conditional": {
            "show_when": {
              "field": "condition_rating",
              "operator": "less_than",
              "value": 4
            }
          }
        },
        {
          "id": "photos",
          "type": "photo",
          "label": "Inspection Photos",
          "required": true,
          "validation": {
            "min_count": 2,
            "max_count": 10
          },
          "widget": {
            "type": "photo_gallery",
            "quality": "high",
            "max_size_mb": 5
          }
        },
        {
          "id": "inspector_signature",
          "type": "signature",
          "label": "Inspector Signature",
          "required": true,
          "widget": {
            "type": "signature_pad"
          }
        }
      ]
    }
  ],
  "calculated_fields": [
    {
      "id": "inspection_score",
      "expression": "condition_rating * 20",
      "label": "Inspection Score (%)"
    }
  ],
  "submission_rules": {
    "require_location": true,
    "require_timestamp": true,
    "allow_offline": true
  }
}
```

---

## Field Types

### Basic Field Types

#### 1. Text Field
```json
{
  "type": "text",
  "validation": {
    "min_length": 0,
    "max_length": 255,
    "pattern": "regex",
    "custom": "javascript expression"
  },
  "widget": {
    "type": "text_field",
    "keyboard_type": "default|numeric|email|phone|url",
    "autocomplete": true,
    "capitalization": "none|words|sentences|characters"
  }
}
```

**Use Cases:** Names, IDs, short descriptions

---

#### 2. Text Area
```json
{
  "type": "text",
  "widget": {
    "type": "text_area",
    "rows": 4,
    "max_length": 5000
  }
}
```

**Use Cases:** Notes, comments, long descriptions

---

#### 3. Number
```json
{
  "type": "integer" | "double",
  "validation": {
    "min": 0,
    "max": 1000,
    "step": 1
  },
  "widget": {
    "type": "number_field|slider",
    "keyboard_type": "numeric",
    "decimal_places": 2
  }
}
```

**Use Cases:** Counts, measurements, quantities

---

#### 4. Date
```json
{
  "type": "date",
  "validation": {
    "min_date": "1950-01-01",
    "max_date": "today"
  },
  "default_value": "today",
  "widget": {
    "type": "date_picker",
    "format": "MM/DD/YYYY"
  }
}
```

**Use Cases:** Installation dates, inspection dates

---

#### 5. Date & Time
```json
{
  "type": "datetime",
  "default_value": "now",
  "widget": {
    "type": "datetime_picker",
    "format": "MM/DD/YYYY hh:mm A"
  }
}
```

**Use Cases:** Timestamps, scheduled events

---

#### 6. Choice (Single)
```json
{
  "type": "choice",
  "choices": [
    { "value": "good", "label": "Good" },
    { "value": "fair", "label": "Fair" },
    { "value": "poor", "label": "Poor" }
  ],
  "widget": {
    "type": "dropdown|radio|segmented_control",
    "allow_other": true
  }
}
```

**Use Cases:** Categories, conditions, statuses

---

#### 7. Multi-Choice
```json
{
  "type": "multi_choice",
  "choices": [...],
  "validation": {
    "min_selections": 1,
    "max_selections": 3
  },
  "widget": {
    "type": "checkbox|multi_select"
  }
}
```

**Use Cases:** Multiple issues, features present

---

### Advanced Field Types

#### 8. Boolean (Checkbox)
```json
{
  "type": "boolean",
  "default_value": false,
  "widget": {
    "type": "checkbox|switch|toggle"
  }
}
```

**Use Cases:** Yes/No questions, feature flags

---

#### 9. Email
```json
{
  "type": "email",
  "validation": {
    "pattern": "email",
    "verify": true
  },
  "widget": {
    "keyboard_type": "email"
  }
}
```

**Use Cases:** Contact information

---

#### 10. Phone Number
```json
{
  "type": "phone",
  "validation": {
    "country_code": "US",
    "format": "(###) ###-####"
  },
  "widget": {
    "keyboard_type": "phone"
  }
}
```

**Use Cases:** Emergency contacts, callbacks

---

#### 11. URL
```json
{
  "type": "url",
  "validation": {
    "pattern": "url",
    "allowed_schemes": ["http", "https"]
  },
  "widget": {
    "keyboard_type": "url"
  }
}
```

**Use Cases:** Reference links, documentation

---

#### 12. Calculated Field
```json
{
  "type": "calculated",
  "expression": "field_a + field_b * 2",
  "data_type": "number",
  "read_only": true
}
```

**Use Cases:** Totals, scores, derived values

---

#### 13. Rating
```json
{
  "type": "integer",
  "widget": {
    "type": "rating",
    "icon": "star|heart|thumb",
    "max_rating": 5
  }
}
```

**Use Cases:** Condition ratings, satisfaction scores

---

#### 14. Barcode/QR Code
```json
{
  "type": "text",
  "widget": {
    "type": "barcode_scanner",
    "formats": ["QR", "Code128", "EAN13"]
  }
}
```

**Use Cases:** Asset tags, inventory codes

---

### Media Field Types

#### 15. Photo
```json
{
  "type": "photo",
  "validation": {
    "min_count": 1,
    "max_count": 10,
    "max_size_mb": 5
  },
  "widget": {
    "type": "photo_gallery",
    "quality": "high|medium|low",
    "allow_camera": true,
    "allow_library": true
  }
}
```

**Use Cases:** Documentation, damage photos

---

#### 16. Video
```json
{
  "type": "video",
  "validation": {
    "max_duration_seconds": 60,
    "max_size_mb": 50
  }
}
```

**Use Cases:** Inspections, training

---

#### 17. Audio
```json
{
  "type": "audio",
  "validation": {
    "max_duration_seconds": 300
  }
}
```

**Use Cases:** Voice notes, interviews

---

#### 18. Signature
```json
{
  "type": "signature",
  "required": true,
  "widget": {
    "type": "signature_pad",
    "background_color": "white",
    "pen_color": "black"
  }
}
```

**Use Cases:** Approvals, acknowledgments

---

#### 19. Sketch/Drawing
```json
{
  "type": "sketch",
  "widget": {
    "type": "drawing_canvas",
    "tools": ["pen", "line", "circle", "text"]
  }
}
```

**Use Cases:** Damage diagrams, site plans

---

### Location Field Types

#### 20. Point (GPS)
```json
{
  "type": "point",
  "required": true,
  "validation": {
    "accuracy_threshold_meters": 10
  },
  "widget": {
    "type": "gps_capture",
    "allow_manual": true,
    "averaging": true
  }
}
```

**Use Cases:** Asset location, sample points

---

#### 21. Line
```json
{
  "type": "line",
  "widget": {
    "type": "line_capture",
    "mode": "streaming|vertex"
  }
}
```

**Use Cases:** Roads, pipelines, boundaries

---

#### 22. Polygon
```json
{
  "type": "polygon",
  "widget": {
    "type": "polygon_capture",
    "min_vertices": 3
  }
}
```

**Use Cases:** Property boundaries, zones

---

#### 23. Address
```json
{
  "type": "address",
  "widget": {
    "type": "address_autocomplete",
    "geocode": true
  }
}
```

**Use Cases:** Service addresses, locations

---

### Layout Elements

#### 24. Section Header
```json
{
  "type": "section",
  "title": "Inspection Details",
  "description": "Complete the inspection checklist",
  "collapsed": false
}
```

**Use Cases:** Organizing long forms

---

#### 25. Divider
```json
{
  "type": "divider",
  "style": "line|space"
}
```

**Use Cases:** Visual separation

---

#### 26. Help Text / Info
```json
{
  "type": "info",
  "text": "Instructions for completing this section",
  "style": "info|warning|error"
}
```

**Use Cases:** Instructions, warnings

---

#### 27. Repeating Group
```json
{
  "type": "repeating_group",
  "title": "Equipment",
  "min_repeats": 1,
  "max_repeats": 10,
  "fields": [
    { "id": "equipment_type", "type": "choice", ... },
    { "id": "equipment_count", "type": "integer", ... }
  ]
}
```

**Use Cases:** Multiple related items

---

## Conditional Logic

### Visibility Rules

**Show/Hide Fields Based on Conditions:**

```json
{
  "id": "repair_notes",
  "type": "text",
  "conditional": {
    "show_when": {
      "field": "condition_rating",
      "operator": "less_than",
      "value": 3
    }
  }
}
```

**Supported Operators:**
- `equals`, `not_equals`
- `greater_than`, `greater_than_or_equal`
- `less_than`, `less_than_or_equal`
- `contains`, `not_contains`
- `starts_with`, `ends_with`
- `is_empty`, `is_not_empty`
- `in`, `not_in` (for arrays)

---

### Multiple Conditions (AND/OR)

```json
{
  "conditional": {
    "show_when": {
      "operator": "AND",
      "conditions": [
        {
          "field": "asset_type",
          "operator": "equals",
          "value": "utility_pole"
        },
        {
          "field": "condition_rating",
          "operator": "less_than",
          "value": 3
        }
      ]
    }
  }
}
```

---

### Default Value Rules

**Set Default Based on Other Fields:**

```json
{
  "id": "priority",
  "type": "choice",
  "default_rules": [
    {
      "when": {
        "field": "condition_rating",
        "operator": "equals",
        "value": 1
      },
      "set_to": "urgent"
    },
    {
      "when": {
        "field": "condition_rating",
        "operator": "equals",
        "value": 2
      },
      "set_to": "high"
    }
  ]
}
```

---

### Cascading Dropdowns

**Second Dropdown Values Depend on First:**

```json
{
  "id": "city",
  "type": "choice",
  "choices_depend_on": {
    "field": "state",
    "mapping": {
      "CA": ["Los Angeles", "San Francisco", "San Diego"],
      "NY": ["New York City", "Buffalo", "Rochester"],
      "TX": ["Houston", "Austin", "Dallas"]
    }
  }
}
```

---

### Skip Logic

**Jump to Different Sections:**

```json
{
  "id": "requires_follow_up",
  "type": "boolean",
  "label": "Requires follow-up inspection?",
  "skip_logic": {
    "when_true": "goto_section:follow_up_details",
    "when_false": "goto_section:completion"
  }
}
```

---

## Validation Rules

### Built-in Validation Types

**1. Required Field**
```json
{
  "required": true,
  "validation": {
    "error_message": "This field is required"
  }
}
```

**2. Length Constraints**
```json
{
  "validation": {
    "min_length": 5,
    "max_length": 100,
    "error_message": "Must be between 5 and 100 characters"
  }
}
```

**3. Numeric Range**
```json
{
  "validation": {
    "min": 0,
    "max": 100,
    "error_message": "Value must be between 0 and 100"
  }
}
```

**4. Pattern Matching (Regex)**
```json
{
  "validation": {
    "pattern": "^[A-Z]{2,3}-\\d{4,6}$",
    "error_message": "Must match format: AB-1234 or ABC-123456"
  }
}
```

**5. Choice Count**
```json
{
  "type": "multi_choice",
  "validation": {
    "min_selections": 1,
    "max_selections": 3,
    "error_message": "Select 1-3 options"
  }
}
```

**6. File Size/Count**
```json
{
  "type": "photo",
  "validation": {
    "min_count": 2,
    "max_count": 10,
    "max_size_mb": 5,
    "error_message": "Upload 2-10 photos, max 5MB each"
  }
}
```

**7. Date Range**
```json
{
  "type": "date",
  "validation": {
    "min_date": "1950-01-01",
    "max_date": "today",
    "error_message": "Date must be between 1950 and today"
  }
}
```

**8. GPS Accuracy**
```json
{
  "type": "point",
  "validation": {
    "accuracy_threshold_meters": 10,
    "error_message": "GPS accuracy must be better than 10 meters"
  }
}
```

---

### Custom Validation

**JavaScript Expressions:**

```json
{
  "validation": {
    "custom": {
      "expression": "parseFloat(value) % 5 === 0",
      "error_message": "Value must be divisible by 5"
    }
  }
}
```

**Cross-Field Validation:**

```json
{
  "id": "end_date",
  "validation": {
    "custom": {
      "expression": "Date.parse(value) > Date.parse(form.start_date)",
      "error_message": "End date must be after start date"
    }
  }
}
```

---

### Warning vs. Error

**Allow Override with Warning:**

```json
{
  "validation": {
    "warning": {
      "condition": "parseFloat(value) > 90",
      "message": "Value is unusually high. Are you sure?",
      "allow_override": true
    },
    "error": {
      "condition": "parseFloat(value) > 100",
      "message": "Value cannot exceed 100"
    }
  }
}
```

---

## Mobile Preview

### Preview Modes

**1. Device Frames**
- iPhone 14 Pro (iOS)
- Samsung Galaxy S23 (Android)
- iPad Pro 11" (Tablet)

**2. Orientation**
- Portrait (default)
- Landscape

**3. Interaction**
- **View Only:** See appearance
- **Interactive:** Fill out form, test validation
- **Conditional Logic Test:** Verify show/hide rules

---

### Preview Features

**Live Updates:**
- Changes to form reflected immediately
- No need to refresh

**Navigation:**
- Step through fields sequentially
- Jump to specific field
- Test scroll behavior

**Validation Testing:**
- Submit with errors to see validation messages
- Test required fields
- Test pattern matching

**Conditional Logic Testing:**
- Change field values to trigger conditions
- Verify fields show/hide correctly
- Test cascading dropdowns

**Photo/Media Testing:**
- Placeholder images
- Test gallery layout
- Verify file size limits

**Accessibility Testing:**
- Screen reader labels
- Touch target sizes (min 44x44 pts)
- Color contrast

---

## Templates

### Pre-Built Form Templates

**1. Asset Inspection**
```
- Asset ID (text, required)
- Asset Type (dropdown, required)
- Inspection Date (date, default: today)
- Condition Rating (1-5 stars)
- Issues Found (text area, conditional)
- Photos (2-10 images)
- Inspector Signature (required)
```

**2. Environmental Survey**
```
- Survey Location (GPS point)
- Survey Date/Time (datetime, default: now)
- Species Observed (multi-choice)
- Count (number)
- Habitat Type (dropdown)
- Weather Conditions (multi-choice)
- Notes (text area)
- Photos (optional)
```

**3. Damage Assessment**
```
- Incident ID (auto-generated)
- Location (GPS + address)
- Damage Type (multi-choice)
- Severity (1-5 rating)
- Affected Area (polygon)
- Estimated Cost (currency)
- Photos (required, 3-20)
- Description (text area)
```

**4. Site Survey**
```
- Site Name (text)
- Site Boundary (polygon)
- Site Type (dropdown)
- Access Points (repeating group)
- Utilities Present (multi-choice)
- Soil Type (dropdown)
- Photos (optional)
- Sketch (drawing)
```

**5. Maintenance Work Order**
```
- Work Order Number (text, read-only)
- Asset ID (barcode scanner)
- Issue Type (dropdown)
- Priority (radio: Low/Medium/High/Urgent)
- Work Performed (text area)
- Parts Used (repeating group)
- Time Spent (duration)
- Before/After Photos
- Technician Signature
```

---

### Template Customization

Users can:
- Start from template
- Add/remove fields
- Modify validation rules
- Adjust conditional logic
- Save as new template
- Share templates with team

---

## Version Control

### Version History

**Automatic Versioning:**
- Every publish creates new version
- Semantic versioning: MAJOR.MINOR.PATCH
- MAJOR: Breaking changes (field removed/renamed)
- MINOR: New fields added
- PATCH: Validation/UI tweaks

**Version List View:**
```
┌────────────────────────────────────────────────────────┐
│ Version History: Utility Pole Inspection              │
├────────────────────────────────────────────────────────┤
│                                                        │
│ v1.2.0  Published  2025-03-20  Sarah Admin            │
│ ├─ Added "Repair Priority" field                      │
│ ├─ Updated validation on "Asset ID"                   │
│ └─ [View] [Restore] [Compare]                         │
│                                                        │
│ v1.1.0  Published  2025-02-15  Sarah Admin            │
│ ├─ Added "Inspector Signature" field                  │
│ └─ [View] [Restore] [Compare]                         │
│                                                        │
│ v1.0.0  Published  2025-01-15  Sarah Admin            │
│ ├─ Initial version                                    │
│ └─ [View] [Restore] [Compare]                         │
│                                                        │
│ Draft   Unsaved    2025-03-25  You                    │
│ ├─ Working on v1.3.0                                  │
│ └─ [Continue Editing]                                 │
│                                                        │
└────────────────────────────────────────────────────────┘
```

---

### Version Comparison

**Side-by-Side Diff:**
```
┌─────────────────────────┬─────────────────────────┐
│ v1.1.0                  │ v1.2.0 (Current)        │
├─────────────────────────┼─────────────────────────┤
│ Asset ID                │ Asset ID                │
│ Asset Type              │ Asset Type              │
│ Installation Date       │ Installation Date       │
│ Condition Rating        │ Condition Rating        │
│                         │ + Repair Priority (NEW) │
│ Photos                  │ Photos                  │
│ Inspector Signature     │ Inspector Signature     │
└─────────────────────────┴─────────────────────────┘
```

**Change Log:**
- Fields added (green)
- Fields removed (red)
- Fields modified (yellow)
- Validation changes
- Conditional logic changes

---

### Rollback

**Restore Previous Version:**
- Select version from history
- Click "Restore"
- Creates new version (doesn't delete current)
- Mobile apps get update on next sync

---

### Branching (Future Feature)

**Draft Branches:**
- Work on experimental changes
- Don't affect published version
- Merge when ready
- Useful for testing major redesigns

---

## Technical Architecture

### Frontend Technology Stack

**Framework Options:**

**Option 1: React + TypeScript (Recommended)**
- React 18 with TypeScript
- React DnD for drag-and-drop
- React Hook Form for form state
- TailwindCSS for styling
- Zustand or Redux for state management

**Option 2: Vue 3 + TypeScript**
- Vue 3 Composition API
- VueDraggable for drag-and-drop
- Pinia for state management
- Tailwind CSS

**Option 3: Blazor Server/WASM**
- C# / .NET (aligns with backend)
- MudBlazor or Radzen components
- Native integration with Honua Server

**Recommended: React + TypeScript** (industry standard, rich ecosystem)

---

### Component Architecture

```
src/
├── components/
│   ├── FormEditor/
│   │   ├── Canvas/
│   │   │   ├── Canvas.tsx
│   │   │   ├── FieldRenderer.tsx
│   │   │   ├── DraggableField.tsx
│   │   │   └── DropZone.tsx
│   │   ├── FieldPalette/
│   │   │   ├── FieldPalette.tsx
│   │   │   ├── FieldTypeCard.tsx
│   │   │   └── FieldSearch.tsx
│   │   ├── PropertiesPanel/
│   │   │   ├── PropertiesPanel.tsx
│   │   │   ├── GeneralProperties.tsx
│   │   │   ├── ValidationTab.tsx
│   │   │   ├── ConditionalTab.tsx
│   │   │   └── MobileWidgetTab.tsx
│   │   ├── MobilePreview/
│   │   │   ├── MobilePreview.tsx
│   │   │   ├── iOSFrame.tsx
│   │   │   └── AndroidFrame.tsx
│   │   └── FormEditor.tsx (main component)
│   ├── FieldTypes/
│   │   ├── TextField.tsx
│   │   ├── NumberField.tsx
│   │   ├── DateField.tsx
│   │   ├── ChoiceField.tsx
│   │   ├── PhotoField.tsx
│   │   └── ... (one component per field type)
│   └── Common/
│       ├── Button.tsx
│       ├── Input.tsx
│       ├── Select.tsx
│       └── Modal.tsx
├── hooks/
│   ├── useFormEditor.ts
│   ├── useFieldDragDrop.ts
│   ├── useValidation.ts
│   └── useConditionalLogic.ts
├── services/
│   ├── formService.ts (API calls)
│   ├── validationService.ts
│   └── schemaValidator.ts
├── store/
│   ├── formEditorStore.ts
│   └── types.ts
└── utils/
    ├── schemaBuilder.ts
    ├── schemaValidator.ts
    └── mobilePreviewRenderer.ts
```

---

### State Management

**Form Editor State:**

```typescript
interface FormEditorState {
  // Form metadata
  formId: string;
  title: string;
  description: string;
  version: string;
  status: 'draft' | 'published';

  // Form structure
  sections: Section[];
  fields: Field[];

  // Editor state
  selectedFieldId: string | null;
  isDirty: boolean;
  lastSaved: Date;

  // Preview state
  previewMode: 'ios' | 'android' | 'tablet';
  previewOrientation: 'portrait' | 'landscape';

  // Actions
  addField: (field: Field) => void;
  updateField: (id: string, updates: Partial<Field>) => void;
  removeField: (id: string) => void;
  reorderFields: (sourceIndex: number, destIndex: number) => void;
  setSelectedField: (id: string | null) => void;
  saveDraft: () => Promise<void>;
  publish: () => Promise<void>;
}
```

---

### API Endpoints

**Form CRUD:**
```
GET    /api/forms                    List all forms
GET    /api/forms/{id}               Get form details
POST   /api/forms                    Create new form
PUT    /api/forms/{id}               Update form (draft)
DELETE /api/forms/{id}               Delete form
POST   /api/forms/{id}/publish       Publish form version
GET    /api/forms/{id}/versions      Get version history
POST   /api/forms/{id}/restore/{ver} Restore version
```

**Templates:**
```
GET    /api/form-templates           List templates
GET    /api/form-templates/{id}      Get template
POST   /api/forms/from-template/{id} Create from template
```

**Validation:**
```
POST   /api/forms/validate           Validate form schema
```

**Export/Import:**
```
GET    /api/forms/{id}/export        Export as JSON
POST   /api/forms/import             Import from JSON
```

---

### Database Schema

**forms table:**
```sql
CREATE TABLE forms (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(255) NOT NULL,
    description TEXT,
    geometry_type VARCHAR(50), -- 'point', 'line', 'polygon', 'any'
    status VARCHAR(20) DEFAULT 'draft', -- 'draft', 'published', 'archived'
    version VARCHAR(20) NOT NULL,
    schema JSONB NOT NULL, -- Full form schema
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    created_by UUID REFERENCES users(id),
    organization_id UUID REFERENCES organizations(id),
    UNIQUE(organization_id, title, version)
);

CREATE INDEX idx_forms_org ON forms(organization_id);
CREATE INDEX idx_forms_status ON forms(status);
CREATE INDEX idx_forms_schema ON forms USING gin(schema);
```

**form_versions table:**
```sql
CREATE TABLE form_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    form_id UUID REFERENCES forms(id) ON DELETE CASCADE,
    version VARCHAR(20) NOT NULL,
    schema JSONB NOT NULL,
    published_at TIMESTAMP,
    published_by UUID REFERENCES users(id),
    change_log TEXT,
    UNIQUE(form_id, version)
);

CREATE INDEX idx_form_versions_form ON form_versions(form_id);
```

**form_templates table:**
```sql
CREATE TABLE form_templates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    category VARCHAR(100), -- 'inspection', 'survey', 'assessment', etc.
    schema JSONB NOT NULL,
    is_public BOOLEAN DEFAULT false,
    created_by UUID REFERENCES users(id),
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_form_templates_category ON form_templates(category);
```

---

## API Integration

### Mobile App Sync

**Form Schema Distribution:**

```
Mobile App                          Honua Server
     │                                    │
     │ GET /api/mobile/forms              │
     ├───────────────────────────────────>│
     │                                    │
     │ List of available forms            │
     │<───────────────────────────────────┤
     │ [{id, title, version, updated}]    │
     │                                    │
     │ GET /api/mobile/forms/{id}/schema  │
     ├───────────────────────────────────>│
     │                                    │
     │ Full form schema JSON              │
     │<───────────────────────────────────┤
     │                                    │
     │ (Cache locally in SQLite)          │
     │                                    │
```

**Version Check:**
```typescript
// Mobile app checks for updates
const localVersion = await db.getFormVersion(formId);
const serverVersion = await api.getFormVersion(formId);

if (serverVersion > localVersion) {
  // Download new schema
  const schema = await api.getFormSchema(formId);
  await db.updateFormSchema(formId, schema);
}
```

---

### Form Rendering on Mobile

**Dynamic Form Builder:**

```csharp
// Mobile app renders form from schema
public class DynamicFormBuilder
{
    public View BuildForm(FormSchema schema)
    {
        var stackLayout = new StackLayout();

        foreach (var section in schema.Sections)
        {
            // Add section header
            stackLayout.Children.Add(new Label
            {
                Text = section.Title,
                StyleClass = new[] { "SectionHeader" }
            });

            foreach (var field in section.Fields)
            {
                // Create widget based on field type
                var widget = CreateWidget(field);
                stackLayout.Children.Add(widget);
            }
        }

        return stackLayout;
    }

    private View CreateWidget(Field field)
    {
        return field.Type switch
        {
            "text" => new Entry
            {
                Placeholder = field.Placeholder,
                Keyboard = GetKeyboardType(field.Widget.KeyboardType)
            },
            "choice" => new Picker
            {
                ItemsSource = field.Choices.Select(c => c.Label).ToList()
            },
            "date" => new DatePicker
            {
                MinimumDate = field.Validation.MinDate,
                MaximumDate = field.Validation.MaxDate
            },
            "photo" => new PhotoGalleryView
            {
                MaxPhotos = field.Validation.MaxCount
            },
            // ... other types
            _ => throw new NotSupportedException($"Field type {field.Type} not supported")
        };
    }
}
```

---

## Implementation Roadmap

### Phase 1: Core Editor (2 months)

**Sprint 1-2: Basic UI (4 weeks)**
- [ ] Set up React + TypeScript project
- [ ] Create three-panel layout
- [ ] Implement field palette with basic field types
- [ ] Add canvas with drag-and-drop
- [ ] Build properties panel (basic properties only)
- [ ] Implement form save/load

**Deliverables:**
- Basic form editor with text, number, date fields
- Save/load forms as JSON
- No validation or conditional logic yet

---

### Phase 2: Advanced Features (2 months)

**Sprint 3: Validation & Conditionals (2 weeks)**
- [ ] Add validation rules UI
- [ ] Implement conditional logic builder
- [ ] Add calculated fields
- [ ] Build validation testing

**Sprint 4: Advanced Field Types (2 weeks)**
- [ ] Add photo, signature, sketch fields
- [ ] Add location/geometry fields
- [ ] Add repeating groups
- [ ] Build field type registry

**Deliverables:**
- All field types supported
- Validation and conditional logic working
- Advanced form capabilities

---

### Phase 3: Preview & Templates (1 month)

**Sprint 5: Mobile Preview (2 weeks)**
- [ ] Build mobile preview modal
- [ ] Create iOS and Android device frames
- [ ] Implement interactive preview
- [ ] Add conditional logic testing in preview

**Sprint 6: Templates & Version Control (2 weeks)**
- [ ] Create template system
- [ ] Build 5 pre-built templates
- [ ] Implement version history
- [ ] Add version comparison and rollback

**Deliverables:**
- Mobile preview functional
- Template library available
- Version control working

---

### Phase 4: Production Ready (1 month)

**Sprint 7: Polish & Testing (2 weeks)**
- [ ] UI/UX refinements
- [ ] Performance optimization
- [ ] Comprehensive testing
- [ ] Bug fixes

**Sprint 8: Integration & Deployment (2 weeks)**
- [ ] API integration with Honua Server
- [ ] Mobile app schema sync
- [ ] Documentation
- [ ] Launch to production

**Deliverables:**
- Production-ready form editor
- Mobile app integration complete
- User documentation

---

**Total Timeline: 6 months**

---

## Success Metrics

### User Adoption

**Target Metrics:**
- 80% of organizations create at least 1 custom form
- 50% create 3+ forms
- Average time to create first form: < 15 minutes

---

### Form Complexity

**Track:**
- Average fields per form: 10-20
- Forms with conditional logic: 40%
- Forms with validation rules: 80%
- Forms using advanced field types: 60%

---

### Editor Performance

**Targets:**
- Form editor load time: < 2 seconds
- Drag-and-drop latency: < 50ms
- Save operation: < 1 second
- Preview render: < 500ms

---

### Mobile Integration

**Targets:**
- Schema sync time: < 5 seconds per form
- Mobile form render time: < 1 second
- Zero schema validation errors in production
- Mobile form submission success rate: > 99%

---

### User Satisfaction

**Measure:**
- Net Promoter Score (NPS): > 50
- Feature request conversion: > 30%
- Support tickets for form editor: < 5% of total
- User rating: 4.5+ stars

---

## Conclusion

The **Honua Form Editor** empowers administrators to create rich, intelligent data collection forms without coding. By providing an intuitive drag-and-drop interface, robust validation, conditional logic, and seamless mobile integration, it becomes a critical tool in the Honua ecosystem.

### Key Benefits

✅ **Self-Service:** Admins create forms without IT support
✅ **Rapid Deployment:** Changes go live instantly
✅ **Mobile-Optimized:** Forms designed for field use
✅ **Intelligent:** Validation, conditionals, AI integration
✅ **Scalable:** Handles simple to complex forms

### Next Steps

1. **Review and approve** this design document
2. **Prioritize** in Honua Server roadmap
3. **Allocate resources** (2 frontend developers)
4. **Begin Sprint 1** (Basic UI)
5. **Launch Beta** in 3 months
6. **Production** in 6 months

---

**The Form Editor transforms Honua from a data collection platform into a comprehensive, customizable field GIS solution.** 🚀

**Document Status:** Ready for Review
**Prepared By:** Honua Engineering Team
**Date:** November 2025
**Next Review:** After stakeholder feedback
