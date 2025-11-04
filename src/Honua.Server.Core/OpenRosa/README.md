# OpenRosa/ODK Integration - MVP Implementation

## Overview

This directory contains the core OpenRosa/ODK integration for Honua, enabling mobile field data collection using ODK Collect and compatible clients.

## Implementation Status

### Completed ✅
- **Core Models** (`OpenRosaMetadata.cs`, `Models.cs`)
  - OpenRosa layer configuration metadata
  - Submission, XForm, and attachment models
  - Review workflow definitions

- **XForm Generator** (`XFormGenerator.cs`)
  - Auto-generates XForms XML from Honua layer metadata
  - Supports all major XForm input types (geopoint, geotrace, geoshape, text, number, select, upload)
  - Field-level customization (labels, hints, constraints, appearance)
  - Geometry type mapping (Point, LineString, Polygon)

- **Submission Processor** (`SubmissionProcessor.cs`)
  - Parses ODK submissions (XForm XML + attachments)
  - Dual-mode operation:
    - **Direct Mode**: Immediately publishes to production layer via `FeatureEditOrchestrator`
    - **Staged Mode**: Saves to staging table for manual review
  - Geometry parsing (ODK format → NetTopologySuite)
  - Field type conversion (string → int/double/date/etc.)

- **Configuration** (`Configuration/OpenRosaOptions.cs`)
  - HTTP Digest authentication settings
  - Attachment size/type limits
  - Staging retention policies

- **Metadata Extension**
  - `LayerDefinition.OpenRosa` property added to support per-layer OpenRosa configuration

### Pending Implementation 🚧

**High Priority** (Required for MVP):
1. **API Endpoints** (Honua.Server.Host):
   - `GET /openrosa/formList` - Returns available forms
   - `GET /openrosa/forms/{formId}` - Downloads XForm XML
   - `POST /openrosa/submission` - Accepts form submissions
   - `HEAD /openrosa/submission` - Endpoint discovery

2. **HTTP Digest Authentication Middleware**:
   - OpenRosa requires HTTP Digest (not Bearer/Basic)
   - Integrate with existing Honua authentication (`SqliteAuthRepository`, OIDC)

3. **Dependency Injection**:
   - Register `IXFormGenerator`, `ISubmissionProcessor` in DI container
   - Wire up configuration from `appsettings.json`

**Medium Priority** (Staged Mode):
4. **Submission Repository**:
   - SQLite/PostgreSQL implementation of `ISubmissionRepository`
   - Schema generation for staging tables (`{layer}_submissions`)

5. **Review UI**:
   - Admin dashboard to approve/reject staged submissions
   - Map preview of geometries
   - Attachment viewer

**Low Priority** (Enhancements):
6. **Choice Lists**:
   - External CSV support (`choices.csv`)
   - Database-backed select options
   - Cascading selects

7. **Advanced XForm Features**:
   - Repeat groups (for multi-geometry/related records)
   - Calculated fields
   - Conditional logic (`relevant` expressions)

8. **Testing**:
   - Unit tests for XForm generation
   - Integration tests for submission processing
   - ODK Collect end-to-end tests

## Quick Start

### 1. Enable OpenRosa for a Layer

Add `openrosa` configuration to your `metadata.json`:

```json
{
  "layers": [{
    "id": "tree-inventory",
    "serviceId": "field-surveys",
    "title": "Urban Tree Survey",
    "geometryType": "Point",
    "geometryField": "geometry",
    "idField": "tree_id",
    "fields": [
      {"name": "species", "dataType": "string"},
      {"name": "dbh_cm", "dataType": "int"},
      {"name": "health", "dataType": "string"}
    ],
    "openrosa": {
      "enabled": true,
      "mode": "direct",
      "formId": "tree_survey_v1",
      "formTitle": "Tree Inventory Form",
      "formVersion": "1.0.0",
      "fieldMappings": {
        "species": {
          "label": "Tree Species",
          "hint": "Use scientific name if known",
          "required": true
        },
        "dbh_cm": {
          "label": "Diameter at Breast Height (cm)",
          "hint": "Measure at 1.3m from ground",
          "constraint": ". >= 0 and . <= 500",
          "constraintMessage": "DBH must be between 0-500cm"
        },
        "health": {
          "label": "Tree Health",
          "appearance": "minimal",
          "choices": {
            "excellent": "Excellent",
            "good": "Good",
            "fair": "Fair",
            "poor": "Poor",
            "dead": "Dead"
          }
        }
      }
    }
  }]
}
```

### 2. Configure appsettings.json

```json
{
  "honua": {
    "openrosa": {
      "enabled": true,
      "baseUrl": "https://honua.example.com/openrosa",
      "digestAuth": {
        "enabled": true,
        "realm": "Honua Field Data Collection"
      },
      "maxSubmissionSizeMB": 50
    }
  }
}
```

### 3. Configure ODK Collect

1. Open ODK Collect on Android
2. Go to **Settings** → **Server**
3. Set **Platform**: `Other`
4. Set **URL**: `https://honua.example.com/openrosa`
5. Set **Username**: (Honua user account)
6. Set **Password**: (Honua password)
7. Tap **Get Blank Form** → Select "Tree Inventory Form"
8. Fill form and submit

## Architecture

```
┌─────────────────────────────────────────┐
│  ODK Collect (Android/iOS)              │
│  - Fill XForms offline                  │
│  - Capture GPS, photos                  │
│  - Submit when connected                │
└───────────────┬─────────────────────────┘
                │ HTTP Digest Auth
                │ POST /openrosa/submission
┌───────────────▼─────────────────────────┐
│  OpenRosa API Endpoints                 │
│  - FormList, Submission, Manifest       │
│  - Multipart parsing (XML + attachments)│
└───────────────┬─────────────────────────┘
                │
┌───────────────▼─────────────────────────┐
│  SubmissionProcessor                     │
│  - Parse XForm XML                      │
│  - Extract geometry (ODK → NTS)         │
│  - Validate against layer schema        │
└───────────┬───────────┬─────────────────┘
            │           │
     Direct Mode   Staged Mode
            │           │
┌───────────▼────┐  ┌───▼──────────────────┐
│FeatureEdit     │  │ SubmissionRepository │
│Orchestrator    │  │ (staging table)      │
│→ Production DB │  │ → Manual review      │
└────────────────┘  └──────────────────────┘
```

## Design Documents

- **ADR**: `docs/architecture/ADR-0002-openrosa-odk-integration.md`
- **Implementation Guide**: `docs/dev/openrosa-implementation-guide.md`

## ODK Collect Compatibility

Tested with:
- ODK Collect v2024.x (Android)
- KoBoCollect (fork of ODK Collect)

Expected to work with:
- Survey123 (in OpenRosa mode)
- QField (with OpenRosa plugin)

## References

- [OpenRosa Specification](https://docs.getodk.org/openrosa/)
- [ODK XForms Spec](https://getodk.github.io/xforms-spec/)
- [ODK Collect Documentation](https://docs.getodk.org/collect-intro/)
