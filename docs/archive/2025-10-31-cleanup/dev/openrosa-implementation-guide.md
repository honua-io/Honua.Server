# OpenRosa/ODK Implementation Guide

## Overview

This guide provides detailed implementation guidance for integrating OpenRosa/ODK mobile data collection with Honua. See [ADR-0002](../architecture/ADR-0002-openrosa-odk-integration.md) for architectural decisions and rationale.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Layer Configuration](#layer-configuration)
3. [XForm Generation](#xform-generation)
4. [API Implementation](#api-implementation)
5. [Submission Processing](#submission-processing)
6. [Review Workflow](#review-workflow)
7. [Testing](#testing)
8. [Deployment](#deployment)

## Quick Start

### Enable OpenRosa for a Layer

```json
{
  "layers": [{
    "id": "wildlife-sightings",
    "title": "Wildlife Observations",
    "tableName": "wildlife_sightings",
    "geometryColumn": "location",
    "geometryType": "Point",
    "srid": 4326,
    "fields": [
      {"name": "id", "dataType": "integer", "primaryKey": true},
      {"name": "species", "dataType": "string", "nullable": false},
      {"name": "count", "dataType": "integer", "nullable": false},
      {"name": "observed_at", "dataType": "datetime", "nullable": false},
      {"name": "photo", "dataType": "string", "nullable": true},
      {"name": "notes", "dataType": "string", "nullable": true}
    ],
    "openrosa": {
      "enabled": true,
      "mode": "staged",
      "formId": "wildlife_v1",
      "formTitle": "Wildlife Sighting Report",
      "formVersion": "2025100101"
    }
  }]
}
```

### Generate XForm

```bash
# CLI command to generate XForm from layer metadata
honua openrosa generate-xform --layer wildlife-sightings --output forms/wildlife_v1.xml
```

### Configure ODK Collect

1. **Install ODK Collect** on Android device
2. **Configure server**:
   - Server URL: `https://honua.example.com/openrosa`
   - Username: `field-user-1`
   - Password: `<generated-password>`
3. **Get blank form**: Download `Wildlife Sighting Report`
4. **Fill and submit**: Collect data offline, submit when online

## Layer Configuration

### OpenRosa Metadata Schema

```typescript
interface LayerOpenRosaConfig {
  enabled: boolean;
  mode: "direct" | "staged";
  formId: string;               // Unique form identifier
  formTitle: string;            // Human-readable form name
  formVersion?: string;         // YYYYMMDDNN (date + sequence)
  stagingTable?: string;        // Custom staging table name

  // Field mappings override auto-generation
  fieldMappings?: {
    [fieldName: string]: {
      label?: string;           // Override field label
      hint?: string;            // Help text for field staff
      required?: boolean;       // Override nullability
      constraint?: string;      // XPath constraint expression
      relevant?: string;        // XPath relevance expression
      appearance?: string;      // Control appearance hint
      choices?: string;         // CSV file or inline choices
      choiceFilter?: string;    // Filter expression for cascading
      default?: string;         // Default value expression
    };
  };

  // Review workflow (staged mode only)
  reviewWorkflow?: {
    autoApprove?: boolean;                // Auto-approve trusted users
    autoApproveUsers?: string[];          // Whitelist of usernames
    notifyOnSubmission?: string[];        // Email/webhook URLs
    requiredReviewers?: number;           // Consensus count
    maxPendingHours?: number;             // SLA for review
  };

  // Advanced options
  options?: {
    allowUpdates?: boolean;               // Allow editing existing features
    allowDeletes?: boolean;               // Allow delete submissions
    offlineCacheDays?: number;            // How long to cache form on device
    encryptSubmissions?: boolean;         // Encrypt at rest (ODK feature)
    publicKeyPath?: string;               // RSA public key for encryption
  };
}
```

### Example: Complex Tree Inventory Form

```json
{
  "layers": [{
    "id": "tree-inventory",
    "title": "Urban Tree Inventory",
    "tableName": "trees",
    "geometryColumn": "location",
    "geometryType": "Point",
    "fields": [
      {"name": "tree_id", "dataType": "integer", "primaryKey": true},
      {"name": "species", "dataType": "string", "nullable": false},
      {"name": "genus", "dataType": "string", "nullable": true},
      {"name": "dbh_cm", "dataType": "decimal", "nullable": false},
      {"name": "height_m", "dataType": "decimal", "nullable": true},
      {"name": "health", "dataType": "string", "nullable": false},
      {"name": "photo_trunk", "dataType": "string", "nullable": true},
      {"name": "photo_canopy", "dataType": "string", "nullable": true},
      {"name": "notes", "dataType": "string", "nullable": true},
      {"name": "surveyor", "dataType": "string", "nullable": false}
    ],
    "openrosa": {
      "enabled": true,
      "mode": "staged",
      "formId": "urban_trees_v2",
      "formTitle": "Urban Tree Survey",
      "formVersion": "2025100102",
      "stagingTable": "tree_submissions",

      "fieldMappings": {
        "species": {
          "label": "Tree Species",
          "hint": "Select from list or enter common name",
          "appearance": "autocomplete",
          "choices": "species_choices.csv",
          "constraint": "string-length(.) >= 2",
          "required": true
        },
        "genus": {
          "label": "Genus (Scientific)",
          "hint": "Auto-filled from species if known",
          "relevant": "${species} != ''"
        },
        "dbh_cm": {
          "label": "Diameter at Breast Height (cm)",
          "hint": "Measure 1.3 meters from ground level",
          "constraint": ". >= 5 and . <= 500",
          "required": true
        },
        "height_m": {
          "label": "Estimated Height (meters)",
          "hint": "Use clinometer if available",
          "constraint": ". >= 1 and . <= 100"
        },
        "health": {
          "label": "Tree Health Status",
          "appearance": "minimal",
          "choices": "inline",
          "choiceValues": ["Excellent", "Good", "Fair", "Poor", "Dead", "Dying"]
        },
        "photo_trunk": {
          "type": "image",
          "label": "Photo: Trunk (required)",
          "hint": "Take close-up of bark and trunk",
          "required": true,
          "appearance": "annotate"
        },
        "photo_canopy": {
          "type": "image",
          "label": "Photo: Canopy (optional)",
          "hint": "Wide shot showing full canopy"
        },
        "notes": {
          "label": "Additional Notes",
          "appearance": "multiline",
          "hint": "Damage, context, nearby features"
        },
        "surveyor": {
          "label": "Surveyor Name",
          "default": "${username}",
          "required": true
        }
      },

      "reviewWorkflow": {
        "autoApprove": false,
        "notifyOnSubmission": ["arborist@city.gov"],
        "requiredReviewers": 1,
        "maxPendingHours": 48
      }
    }
  }]
}
```

## XForm Generation

### Auto-Generation Logic

**Field Type Mappings**:

| Honua Field Type | XForm Control | Notes |
|------------------|---------------|-------|
| `string` | `<input>` | Text input |
| `integer` | `<input>` with `type="int"` | Integer constraint |
| `decimal` | `<input>` with `type="decimal"` | Decimal constraint |
| `boolean` | `<select1>` | Yes/No choices |
| `datetime` | `<input>` with `type="dateTime"` | Date+time picker |
| `date` | `<input>` with `type="date"` | Date picker |
| Geometry (Point) | `<geopoint>` | GPS capture |
| Geometry (LineString) | `<geotrace>` | GPS track |
| Geometry (Polygon) | `<geoshape>` | GPS polygon |
| Attachment | `<upload mediatype="image/*">` | Photo/video |

### Generated XForm Example

```xml
<?xml version="1.0"?>
<h:html xmlns="http://www.w3.org/2002/xforms"
        xmlns:h="http://www.w3.org/1999/xhtml"
        xmlns:odk="http://www.opendatakit.org/xforms">
  <h:head>
    <h:title>Wildlife Sighting Report</h:title>
    <model>
      <instance>
        <data id="wildlife_v1" version="2025100101">
          <species/>
          <count/>
          <observed_at/>
          <location/>
          <photo/>
          <notes/>
          <meta>
            <instanceID/>
          </meta>
        </data>
      </instance>

      <bind nodeset="/data/species" type="string" required="true()"/>
      <bind nodeset="/data/count" type="int" required="true()" constraint=". &gt; 0"/>
      <bind nodeset="/data/observed_at" type="dateTime" required="true()"/>
      <bind nodeset="/data/location" type="geopoint" required="true()"/>
      <bind nodeset="/data/photo" type="binary"/>
      <bind nodeset="/data/notes" type="string"/>
      <bind nodeset="/data/meta/instanceID" type="string" readonly="true()"
            calculate="concat('uuid:', uuid())"/>
    </model>
  </h:head>

  <h:body>
    <input ref="/data/species">
      <label>Species</label>
      <hint>Enter species name or select from list</hint>
    </input>

    <input ref="/data/count">
      <label>Count</label>
      <hint>Number of individuals observed</hint>
    </input>

    <input ref="/data/observed_at">
      <label>Observation Time</label>
    </input>

    <geopoint ref="/data/location">
      <label>Location</label>
      <hint>Capture GPS coordinates</hint>
    </geopoint>

    <upload ref="/data/photo" mediatype="image/*">
      <label>Photo (optional)</label>
    </upload>

    <input ref="/data/notes">
      <label>Notes</label>
      <hint>Additional observations</hint>
    </input>
  </h:body>
</h:html>
```

## API Implementation

### Project Structure

```
src/Honua.Server.Core/
├── OpenRosa/
│   ├── Models/
│   │   ├── FormDefinition.cs
│   │   ├── Submission.cs
│   │   ├── SubmissionAttachment.cs
│   │   └── ReviewAction.cs
│   ├── Services/
│   │   ├── IOpenRosaFormService.cs
│   │   ├── OpenRosaFormService.cs
│   │   ├── IXFormGenerator.cs
│   │   ├── XFormGenerator.cs
│   │   ├── ISubmissionProcessor.cs
│   │   ├── SubmissionProcessor.cs
│   │   └── IReviewService.cs
│   ├── Repositories/
│   │   ├── ISubmissionRepository.cs
│   │   └── SubmissionRepository.cs
│   └── Validators/
│       └── XFormValidator.cs

src/Honua.Server.Host/
├── OpenRosa/
│   ├── OpenRosaEndpoints.cs
│   ├── DigestAuthMiddleware.cs
│   └── Models/
│       ├── FormListResponse.cs
│       └── SubmissionResponse.cs
```

### Endpoint Implementation

```csharp
// src/Honua.Server.Host/OpenRosa/OpenRosaEndpoints.cs
public static class OpenRosaEndpoints
{
    public static RouteGroupBuilder MapOpenRosaEndpoints(this RouteGroupBuilder group)
    {
        // Require Digest authentication
        group.AddEndpointFilter<DigestAuthFilter>();

        // FormList - list available forms
        group.MapGet("/formList", async (
            IOpenRosaFormService formService,
            HttpContext context,
            CancellationToken ct) =>
        {
            var username = context.User.Identity?.Name
                ?? throw new UnauthorizedAccessException();

            var forms = await formService.GetAvailableFormsAsync(username, ct);

            var formList = new FormListResponse
            {
                Forms = forms.Select(f => new FormListItem
                {
                    FormId = f.FormId,
                    Name = f.Title,
                    Version = f.Version,
                    Hash = f.Hash,
                    DownloadUrl = $"/openrosa/forms/{f.FormId}",
                    ManifestUrl = f.HasAttachments ? $"/openrosa/forms/{f.FormId}/manifest" : null
                }).ToList()
            };

            return Results.Content(formList.ToXml(), "text/xml");
        })
        .Produces<string>(200, "text/xml")
        .RequireAuthorization("DataPublisher");

        // Get form definition (XForm XML)
        group.MapGet("/forms/{formId}", async (
            string formId,
            IOpenRosaFormService formService,
            CancellationToken ct) =>
        {
            var xform = await formService.GetXFormAsync(formId, ct);

            if (xform == null)
                return Results.NotFound();

            return Results.Content(xform, "text/xml");
        })
        .Produces<string>(200, "text/xml")
        .RequireAuthorization("DataPublisher");

        // HEAD /submission - OpenRosa requires this
        group.MapHead("/submission", () =>
            Results.Ok())
        .RequireAuthorization("DataPublisher");

        // POST /submission - receive form submission
        group.MapPost("/submission", async (
            HttpRequest request,
            ISubmissionProcessor processor,
            HttpContext context,
            CancellationToken ct) =>
        {
            var username = context.User.Identity?.Name!;

            // Parse multipart form data
            if (!request.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data");

            var form = await request.ReadFormAsync(ct);
            var xmlFile = form.Files.GetFile("xml_submission_file");

            if (xmlFile == null)
                return Results.BadRequest("Missing xml_submission_file");

            using var xmlStream = xmlFile.OpenReadStream();
            var xmlDoc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, ct);

            // Extract attachments
            var attachments = form.Files
                .Where(f => f.Name != "xml_submission_file")
                .Select(f => new SubmissionAttachment
                {
                    FileName = f.FileName,
                    ContentType = f.ContentType,
                    Stream = f.OpenReadStream()
                })
                .ToList();

            // Process submission
            var result = await processor.ProcessAsync(new SubmissionRequest
            {
                XmlDocument = xmlDoc,
                Attachments = attachments,
                SubmittedBy = username,
                DeviceId = request.Headers["X-OpenRosa-DeviceID"].ToString(),
                SubmittedAt = DateTimeOffset.UtcNow
            }, ct);

            if (!result.Success)
                return Results.BadRequest(result.Error);

            // OpenRosa requires specific response format
            var response = new SubmissionResponse
            {
                Message = "Submission received",
                SubmissionId = result.InstanceId
            };

            return Results.Content(response.ToXml(), "text/xml", statusCode: 201);
        })
        .Produces<string>(201, "text/xml")
        .RequireAuthorization("DataPublisher")
        .DisableAntiforgery(); // Multipart uploads from ODK

        return group;
    }
}
```

### Submission Processor

```csharp
// src/Honua.Server.Core/OpenRosa/Services/SubmissionProcessor.cs
public class SubmissionProcessor : ISubmissionProcessor
{
    private readonly IMetadataRegistry _metadata;
    private readonly IFeatureRepository _features;
    private readonly ISubmissionRepository _submissions;
    private readonly IAttachmentStore _attachments;
    private readonly ILogger<SubmissionProcessor> _logger;

    public async Task<SubmissionResult> ProcessAsync(
        SubmissionRequest request,
        CancellationToken ct)
    {
        // 1. Extract form ID and version
        var instanceElement = request.XmlDocument.Root;
        var formId = instanceElement?.Attribute("id")?.Value
            ?? throw new InvalidOperationException("Missing form id");

        var instanceId = instanceElement
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "instanceID")
            ?.Value
            ?? throw new InvalidOperationException("Missing instanceID");

        // 2. Find layer configuration
        var layer = _metadata.Snapshot.Layers
            .FirstOrDefault(l => l.OpenRosa?.FormId == formId);

        if (layer == null)
            return SubmissionResult.Error($"Unknown form: {formId}");

        if (!layer.OpenRosa.Enabled)
            return SubmissionResult.Error($"Form {formId} is disabled");

        // 3. Parse XML to field values
        var attributes = ExtractAttributes(instanceElement, layer);
        var geometry = ExtractGeometry(instanceElement, layer);

        // 4. Validate
        var validation = ValidateSubmission(attributes, geometry, layer);
        if (!validation.IsValid)
            return SubmissionResult.Error(validation.ErrorMessage);

        // 5. Store attachments
        var storedAttachments = new List<StoredAttachment>();
        foreach (var attachment in request.Attachments)
        {
            var path = await _attachments.StoreAsync(
                $"openrosa/{instanceId}/{attachment.FileName}",
                attachment.Stream,
                attachment.ContentType,
                ct);

            storedAttachments.Add(new StoredAttachment
            {
                FileName = attachment.FileName,
                Path = path,
                ContentType = attachment.ContentType
            });
        }

        // 6. Route by mode
        if (layer.OpenRosa.Mode == "direct")
        {
            // Direct mode: insert immediately
            var feature = new FeatureCreate
            {
                LayerId = layer.Id,
                Geometry = geometry,
                Attributes = attributes
            };

            var featureId = await _features.CreateAsync(feature, ct);

            _logger.LogInformation(
                "OpenRosa direct submission: form={FormId}, instance={InstanceId}, feature={FeatureId}, user={User}",
                formId, instanceId, featureId, request.SubmittedBy);

            return SubmissionResult.Success(instanceId);
        }
        else
        {
            // Staged mode: insert to staging table
            var submission = new Submission
            {
                Id = Guid.NewGuid().ToString(),
                InstanceId = instanceId,
                FormId = formId,
                FormVersion = instanceElement.Attribute("version")?.Value,
                LayerId = layer.Id,
                SubmittedAt = request.SubmittedAt,
                SubmittedBy = request.SubmittedBy,
                DeviceId = request.DeviceId,
                Status = "pending",
                XmlData = request.XmlDocument.ToString(),
                Geometry = geometry,
                Attributes = attributes,
                Attachments = storedAttachments
            };

            await _submissions.CreateAsync(submission, ct);

            // Trigger notifications
            await NotifyReviewersAsync(layer, submission, ct);

            _logger.LogInformation(
                "OpenRosa staged submission: form={FormId}, instance={InstanceId}, user={User}",
                formId, instanceId, request.SubmittedBy);

            return SubmissionResult.Success(instanceId);
        }
    }

    private Dictionary<string, object> ExtractAttributes(
        XElement instance,
        LayerDefinition layer)
    {
        var attributes = new Dictionary<string, object>();

        foreach (var field in layer.Fields.Where(f => !f.IsGeometry))
        {
            var element = instance.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == field.Name);

            if (element == null)
                continue;

            var value = ConvertValue(element.Value, field.DataType);
            if (value != null)
                attributes[field.Name] = value;
        }

        return attributes;
    }

    private Geometry ExtractGeometry(
        XElement instance,
        LayerDefinition layer)
    {
        var geoField = layer.Fields.FirstOrDefault(f => f.IsGeometry);
        if (geoField == null)
            return null;

        var geoElement = instance.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == geoField.Name);

        if (geoElement == null)
            return null;

        var geoValue = geoElement.Value;

        // Parse based on geometry type
        return layer.GeometryType switch
        {
            "Point" => ParseGeoPoint(geoValue),
            "LineString" => ParseGeoTrace(geoValue),
            "Polygon" => ParseGeoShape(geoValue),
            _ => throw new NotSupportedException($"Geometry type {layer.GeometryType} not supported")
        };
    }

    private Point ParseGeoPoint(string value)
    {
        // ODK format: "latitude longitude altitude accuracy"
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new FormatException("Invalid geopoint format");

        var lat = double.Parse(parts[0]);
        var lon = double.Parse(parts[1]);

        return new Point(lon, lat); // GeoJSON is lon,lat
    }

    private LineString ParseGeoTrace(string value)
    {
        // ODK format: "lat1 lon1 alt1 acc1;lat2 lon2 alt2 acc2;..."
        var points = value.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pt => ParseGeoPoint(pt))
            .ToArray();

        return new LineString(points);
    }

    private Polygon ParseGeoShape(string value)
    {
        // Similar to geotrace but forms a closed ring
        var lineString = ParseGeoTrace(value);

        // Ensure ring is closed
        var coords = lineString.Coordinates.ToList();
        if (!coords.First().Equals(coords.Last()))
            coords.Add(coords.First());

        return new Polygon(new LinearRing(coords.ToArray()));
    }
}
```

## Review Workflow

### Review UI Component

```typescript
// Example React component for review dashboard
interface PendingSubmission {
  id: string;
  instanceId: string;
  formTitle: string;
  layerId: string;
  submittedAt: string;
  submittedBy: string;
  geometry: GeoJSON.Geometry;
  attributes: Record<string, any>;
  attachments: Attachment[];
}

function SubmissionReviewDashboard() {
  const [submissions, setSubmissions] = useState<PendingSubmission[]>([]);
  const [selectedSubmission, setSelectedSubmission] = useState<PendingSubmission | null>(null);

  useEffect(() => {
    fetchPendingSubmissions();
  }, []);

  async function fetchPendingSubmissions() {
    const response = await fetch('/admin/openrosa/submissions?status=pending');
    const data = await response.json();
    setSubmissions(data.submissions);
  }

  async function approveSubmission(id: string, notes: string) {
    await fetch(`/admin/openrosa/submissions/${id}/approve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ notes })
    });

    await fetchPendingSubmissions();
  }

  async function rejectSubmission(id: string, reason: string) {
    await fetch(`/admin/openrosa/submissions/${id}/reject`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason })
    });

    await fetchPendingSubmissions();
  }

  return (
    <div className="review-dashboard">
      <div className="submissions-list">
        <h2>Pending Submissions ({submissions.length})</h2>
        {submissions.map(sub => (
          <div
            key={sub.id}
            className="submission-card"
            onClick={() => setSelectedSubmission(sub)}
          >
            <h3>{sub.formTitle}</h3>
            <p>By: {sub.submittedBy}</p>
            <p>At: {new Date(sub.submittedAt).toLocaleString()}</p>
          </div>
        ))}
      </div>

      {selectedSubmission && (
        <div className="submission-detail">
          <h2>Review Submission</h2>

          <Map geometry={selectedSubmission.geometry} />

          <table>
            <tbody>
              {Object.entries(selectedSubmission.attributes).map(([key, value]) => (
                <tr key={key}>
                  <th>{key}</th>
                  <td>{value}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {selectedSubmission.attachments.map(att => (
            <div key={att.fileName}>
              <img src={att.url} alt={att.fileName} />
            </div>
          ))}

          <div className="actions">
            <button onClick={() => approveSubmission(selectedSubmission.id, '')}>
              Approve
            </button>
            <button onClick={() => rejectSubmission(selectedSubmission.id, 'Invalid data')}>
              Reject
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
```

## Testing

### Unit Tests

```csharp
// tests/Honua.Server.Core.Tests/OpenRosa/SubmissionProcessorTests.cs
public class SubmissionProcessorTests
{
    [Fact]
    public async Task ProcessAsync_DirectMode_CreatesFeatureImmediately()
    {
        // Arrange
        var metadata = CreateMetadata(mode: "direct");
        var processor = new SubmissionProcessor(
            metadata,
            featureRepository: Mock.Of<IFeatureRepository>(),
            submissionRepository: Mock.Of<ISubmissionRepository>(),
            attachmentStore: Mock.Of<IAttachmentStore>(),
            logger: Mock.Of<ILogger<SubmissionProcessor>>());

        var xml = XDocument.Parse(@"
            <data id=""wildlife_v1"" version=""1"">
              <species>Red Fox</species>
              <count>2</count>
              <location>45.5 -122.7</location>
              <meta><instanceID>uuid:12345</instanceID></meta>
            </data>");

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            Attachments = new List<SubmissionAttachment>(),
            SubmittedBy = "field-user",
            SubmittedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await processor.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("uuid:12345", result.InstanceId);

        Mock.Get(featureRepository)
            .Verify(r => r.CreateAsync(
                It.Is<FeatureCreate>(f =>
                    f.Attributes["species"].ToString() == "Red Fox" &&
                    f.Geometry is Point),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_StagedMode_CreatesSubmissionForReview()
    {
        // Arrange
        var metadata = CreateMetadata(mode: "staged");
        var submissionRepo = Mock.Of<ISubmissionRepository>();
        var processor = new SubmissionProcessor(
            metadata,
            featureRepository: Mock.Of<IFeatureRepository>(),
            submissionRepository: submissionRepo,
            attachmentStore: Mock.Of<IAttachmentStore>(),
            logger: Mock.Of<ILogger<SubmissionProcessor>>());

        var xml = XDocument.Parse(@"
            <data id=""wildlife_v1"" version=""1"">
              <species>Red Fox</species>
              <count>2</count>
              <location>45.5 -122.7</location>
              <meta><instanceID>uuid:12345</instanceID></meta>
            </data>");

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            Attachments = new List<SubmissionAttachment>(),
            SubmittedBy = "field-user",
            SubmittedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await processor.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        Mock.Get(submissionRepo)
            .Verify(r => r.CreateAsync(
                It.Is<Submission>(s =>
                    s.InstanceId == "uuid:12345" &&
                    s.Status == "pending"),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }
}
```

### Integration Tests

```csharp
// tests/Honua.Server.Core.Tests/OpenRosa/OpenRosaEndpointsTests.cs
public class OpenRosaEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    [Fact]
    public async Task FormList_ReturnsAvailableForms()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Digest", "username=\"test-user\"");

        // Act
        var response = await client.GetAsync("/openrosa/formList");

        // Assert
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);

        Assert.Contains("wildlife_v1", xml);
    }

    [Fact]
    public async Task Submission_AcceptsValidXForm()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Digest", "username=\"test-user\"");

        var content = new MultipartFormDataContent();

        var xmlContent = new StringContent(@"
            <data id=""wildlife_v1"" version=""1"">
              <species>Red Fox</species>
              <count>2</count>
              <location>45.5 -122.7</location>
              <meta><instanceID>uuid:test-123</instanceID></meta>
            </data>");
        content.Add(xmlContent, "xml_submission_file", "submission.xml");

        // Act
        var response = await client.PostAsync("/openrosa/submission", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseXml = await response.Content.ReadAsStringAsync();
        Assert.Contains("uuid:test-123", responseXml);
    }
}
```

## Deployment

### Docker Configuration

```dockerfile
# Dockerfile with OpenRosa support
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install dependencies for XForm processing
RUN apt-get update && apt-get install -y \
    libxml2-utils \
    xmlstarlet \
    && rm -rf /var/lib/apt/lists/*

COPY publish/ .

# Create directories for OpenRosa data
RUN mkdir -p /app/data/attachments/openrosa && \
    mkdir -p /app/data/forms && \
    chown -R app:app /app/data

USER app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV HONUA__OPENROSA__ENABLED=true

ENTRYPOINT ["dotnet", "Honua.Server.Host.dll"]
```

### Production Checklist

- [ ] Configure HTTPS/TLS (OpenRosa works over HTTP but production should use HTTPS)
- [ ] Set up Digest authentication realm and nonce management
- [ ] Configure attachment storage (S3/Azure Blob for production)
- [ ] Enable submission encryption (ODK feature, requires RSA key pair)
- [ ] Set up email notifications for review workflow
- [ ] Configure staging table retention policy
- [ ] Test form download from ODK Collect
- [ ] Test submission flow end-to-end
- [ ] Monitor submission processing latency
- [ ] Set up alerts for failed submissions

---

For questions or implementation support, see the [OpenRosa specification](https://docs.getodk.org/openrosa/) and [ADR-0002](../architecture/ADR-0002-openrosa-odk-integration.md).
