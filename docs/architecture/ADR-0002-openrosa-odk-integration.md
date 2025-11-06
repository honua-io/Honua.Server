# ADR-0002: OpenRosa/ODK Integration for Mobile Data Collection

## Status
Proposed

## Context

Field data collection is a critical use case for geospatial platforms. Organizations need to:
- Collect geotagged observations in the field using mobile devices (Android/iOS)
- Work offline in areas with poor connectivity
- Sync collected data to a central server when connectivity is available
- Validate and quality-check submissions before publishing to primary datasets
- Support rich form-based data entry with photos, GPS tracks, and complex geometries
- Maintain data provenance and audit trails

**OpenRosa** is an open standard for mobile data collection originally developed by OpenDataKit (ODK). It defines:
- **XForms**: XML-based form definitions
- **REST API**: Standardized submission/retrieval endpoints
- **Authentication**: HTTP Digest authentication
- **Metadata**: Device info, timestamps, user identity

**ODK Collect** (Android) and other compatible clients (KoBoCollect, Survey123) implement OpenRosa and are widely deployed in humanitarian, conservation, and field survey domains.

Honua currently supports OGC Transactions and applyEdits for programmatic feature editing, but lacks:
- Form-based data collection workflows
- Offline-first mobile clients
- Staging/review processes for field submissions
- XForm generation from layer schemas

## Decision

### 1. Architecture: Dual-Mode Integration

Implement OpenRosa API endpoints alongside existing OGC/GeoServices REST APIs, with two operational modes:

**Mode A: Direct Publication (Simple)**
- Mobile submissions map directly to feature layers
- Data is immediately published to production collections
- Suitable for trusted field staff and low-risk data

**Mode B: Staged Review (Advanced)**
- Submissions land in a staging table (`{layer}_submissions`)
- Web-based review UI allows QA/QC before approval
- Approved submissions are promoted to production via OGC Transactions
- Rejected submissions are flagged with feedback for field staff

### 2. OpenRosa API Endpoints

Implement the OpenRosa specification under `/openrosa`:

```
GET  /openrosa/formList                    # List available forms (XForms)
GET  /openrosa/forms/{formId}              # Download XForm definition
HEAD /openrosa/submission                  # Check submission endpoint
POST /openrosa/submission                  # Submit filled form + attachments
GET  /openrosa/view/submissionList         # List submissions (for review UI)
GET  /openrosa/view/downloadSubmission     # Download submission XML + attachments
```

**Authentication**: HTTP Digest authentication (OpenRosa standard) mapped to Honua's existing Local/OIDC auth:
- Digest auth wrapper validates credentials against `SqliteAuthRepository`/OIDC
- Reuses existing roles (DataPublisher can submit, Administrator can review)
- Device credentials can be managed via CLI: `honua auth create-field-user`

### 3. XForm Generation

Auto-generate XForms from Honua layer metadata:

**Metadata Extensions** (`metadata.json`):
```json
{
  "layers": [{
    "id": "tree-inventory",
    "openrosa": {
      "enabled": true,
      "mode": "staged",
      "formId": "tree_survey_v1",
      "formTitle": "Urban Tree Inventory",
      "stagingTable": "tree_inventory_submissions",
      "reviewWorkflow": {
        "autoApprove": false,
        "notifyOnSubmission": "reviewers@example.com",
        "requiredReviewers": 1
      },
      "fieldMappings": {
        "species": {
          "label": "Tree Species",
          "hint": "Scientific name if known",
          "required": true,
          "appearance": "autocomplete",
          "choices": "species_list.csv"
        },
        "dbh_cm": {
          "label": "Diameter at Breast Height (cm)",
          "hint": "Measure 1.3m from ground",
          "constraint": ". >= 0 and . <= 500"
        },
        "photo": {
          "type": "image",
          "label": "Photo of tree",
          "required": false
        }
      }
    }
  }]
}
```

**XForm Generator Service** (`src/Honua.Server.Core/OpenRosa/XFormGenerator.cs`):
- Reads layer schema (fields, geometry type, constraints)
- Generates XForms XML with:
  - Appropriate input controls (text, number, select, geopoint, geotrace, geoshape, image)
  - Validation constraints from field metadata
  - Cascading selects for choice lists
  - Repeat groups for multi-geometry features
- Includes Honua metadata (layer ID, version, submission URL)

### 4. Data Model

**Staging Tables** (when `mode: "staged"`):
```sql
CREATE TABLE tree_inventory_submissions (
    id TEXT PRIMARY KEY,              -- UUID from device
    instance_id TEXT NOT NULL,        -- OpenRosa instanceID
    submitted_at TIMESTAMP NOT NULL,
    submitted_by TEXT NOT NULL,       -- Username from auth
    device_id TEXT,
    status TEXT NOT NULL,             -- 'pending', 'approved', 'rejected'
    reviewed_by TEXT,
    reviewed_at TIMESTAMP,
    review_notes TEXT,
    form_version TEXT NOT NULL,
    xml_data TEXT NOT NULL,           -- Original XForm instance XML
    geometry GEOMETRY,                -- Extracted geometry
    attributes JSONB,                 -- Extracted form fields as JSON
    attachments JSONB                 -- [{filename, path, content_type}]
);

CREATE INDEX idx_submissions_status ON tree_inventory_submissions(status);
CREATE INDEX idx_submissions_submitted_at ON tree_inventory_submissions(submitted_at);
```

**Attachment Storage**:
- Reuse existing Honua attachment architecture (`IAttachmentStore`)
- Photos/videos stored in `data/attachments/openrosa/{instanceId}/`
- Support S3/Azure Blob for production deployments

### 5. Submission Processing Pipeline

**POST /openrosa/submission**:

1. **Parse multipart request**:
   - Extract XML instance document
   - Extract attached media files (photos, audio, video)

2. **Validate**:
   - Check formId matches enabled layer
   - Validate XML against XForm schema
   - Check user has DataPublisher role

3. **Extract data**:
   - Parse XForm instance to field values
   - Extract geometry (geopoint → Point, geotrace → LineString, geoshape → Polygon)
   - Store attachments via `IAttachmentStore`

4. **Route by mode**:
   - **Direct mode**: Insert directly to feature table via `IFeatureRepository`
   - **Staged mode**: Insert to staging table for review

5. **Respond**: Return OpenRosa 201 Created with instanceID

### 6. Review UI (Staged Mode)

Web-based review dashboard at `/admin/openrosa/review`:

**Features**:
- List pending submissions with map preview
- Filter by date, user, layer, status
- View submission details (form fields, geometry, photos)
- Side-by-side comparison with existing features (for edits)
- Approve/reject with notes
- Bulk operations (approve all, reject all)

**Approval Action**:
```csharp
// Promote staged submission to production layer
await featureRepository.CreateAsync(new FeatureCreate {
    LayerId = "tree-inventory",
    Geometry = submission.Geometry,
    Attributes = submission.Attributes
});

// Mark submission as approved
submission.Status = "approved";
submission.ReviewedBy = currentUser;
submission.ReviewedAt = DateTime.UtcNow;
await submissionRepository.UpdateAsync(submission);

// Notify field user (optional)
await notificationService.SendAsync(submission.SubmittedBy,
    "Your tree survey submission was approved");
```

### 7. Configuration

**appsettings.json**:
```json
{
  "honua": {
    "openrosa": {
      "enabled": true,
      "baseUrl": "https://honua.example.com/openrosa",
      "digestAuth": {
        "enabled": true,
        "realm": "Honua OpenRosa",
        "nonceLifetimeMinutes": 5
      },
      "maxSubmissionSizeMB": 50,
      "allowedMediaTypes": ["image/jpeg", "image/png", "audio/mp3", "video/mp4"],
      "stagingRetentionDays": 90,
      "autoArchiveRejected": true
    }
  }
}
```

### 8. Security Considerations

**Authentication**:
- HTTP Digest auth prevents password exposure over HTTP
- Field users get restricted credentials (DataPublisher role only)
- Device binding: optionally restrict users to specific device IDs

**Authorization**:
- DataPublisher: Can submit to assigned forms
- Administrator: Can review, approve, reject all submissions
- Viewer: Read-only access to approved data

**Data Validation**:
- XForm constraints enforced server-side (don't trust client)
- Geometry validation (valid GeoJSON, within bounds)
- Attachment scanning (file type, size, malware)
- Rate limiting per user/device

**Audit Trail**:
- All submissions logged with user, device, timestamp
- Review actions tracked (who approved/rejected, when, why)
- Original XML preserved for forensics

## Implementation Plan

### Phase 1: Core OpenRosa API (Week 1-2)
- [ ] OpenRosa endpoint routing (`/openrosa/*`)
- [ ] HTTP Digest authentication middleware
- [ ] XForm generator from layer metadata
- [ ] FormList endpoint
- [ ] Submission endpoint (direct mode only)
- [ ] Attachment storage integration

### Phase 2: Staged Review (Week 3-4)
- [ ] Staging table schema generation
- [ ] Submission repository (CRUD for staged data)
- [ ] Review UI (list, view, approve/reject)
- [ ] Promotion to production layer
- [ ] Email notifications

### Phase 3: Advanced Features (Week 5-6)
- [ ] Choice list management (CSV, external datasets)
- [ ] Cascading selects
- [ ] Edit existing features (pre-populate XForm)
- [ ] Offline form caching
- [ ] Conflict resolution (optimistic locking)

### Phase 4: CLI & Tooling (Week 7)
- [ ] `honua openrosa generate-xform --layer tree-inventory`
- [ ] `honua openrosa test-submission --form tree_v1 --xml data.xml`
- [ ] `honua openrosa review approve --submission-id abc123`
- [ ] QR code generation for ODK Collect setup

## Consequences

### Positive
- **Field-ready**: Enables offline mobile data collection with proven tools (ODK Collect)
- **Standards-compliant**: OpenRosa is widely adopted, mature, well-documented
- **Flexible workflow**: Direct and staged modes support different operational needs
- **Reuses existing auth**: No new identity system, integrates with Honua RBAC
- **Quality control**: Review process improves data quality before publication
- **Open ecosystem**: Compatible with ODK, KoBoToolbox, Survey123, etc.

### Negative
- **Complexity**: New API surface, data model, and UI to maintain
- **Storage overhead**: Staging tables duplicate data temporarily
- **XForm limitations**: Complex geometries and multi-part features are awkward in XForms
- **Mobile client dependency**: Requires users to install ODK Collect (or similar)
- **Digest auth**: HTTP Digest is legacy, but required by OpenRosa spec

### Risks & Mitigations
- **Risk**: XForm generation doesn't handle all field types
  - *Mitigation*: Support manual XForm upload as fallback, document limitations
- **Risk**: Review backlog grows, blocking field teams
  - *Mitigation*: Auto-approval rules, notifications, bulk operations
- **Risk**: Geometry editing is clunky on mobile
  - *Mitigation*: Provide web-based geometry editing in review UI

## Alternatives Considered

### 1. Esri Survey123
- **Pro**: Richer form builder, better geometry editing
- **Con**: Proprietary, requires ArcGIS Online/Enterprise license, vendor lock-in

### 2. GeoODK (legacy)
- **Pro**: Geometry-focused fork of ODK
- **Con**: Project abandoned in 2018, superseded by ODK Collect with geo widgets

### 3. Custom mobile app
- **Pro**: Full control, native UX
- **Con**: Huge development cost, platform fragmentation (iOS/Android), no ecosystem

### 4. No mobile integration (use web forms)
- **Pro**: Simpler, reuse existing editing APIs
- **Con**: Requires connectivity, poor UX on mobile devices, no offline

## References

- [OpenRosa Specification](https://docs.getodk.org/openrosa/)
- [ODK XForms Spec](https://getodk.github.io/xforms-spec/)
- [ODK Collect](https://docs.getodk.org/collect-intro/)
- [JavaRosa (XForm engine)](https://github.com/getodk/javarosa)
- [Honua Authentication ADR-0001](./ADR-0001-authentication-rbac.md)

## Decision Log

- **2025-10-01**: Initial proposal
- **TBD**: Review with stakeholders
- **TBD**: Approval/rejection
