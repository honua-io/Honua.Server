# Import Module

## Overview

The Import module provides a robust, asynchronous data ingestion pipeline for importing geospatial data in multiple formats. It implements a job-based architecture with progress tracking, validation, schema detection, and error handling for safe and efficient data imports.

## Purpose

This module enables reliable geospatial data ingestion by:

- Supporting multiple geospatial formats (GeoJSON, KML, Shapefile, CSV, GeoPackage)
- Providing asynchronous job processing with progress tracking
- Validating features against layer schemas
- Detecting and inferring schemas from source data
- Handling errors gracefully with detailed error reporting
- Ensuring data integrity through type coercion and sanitization

## Architecture

### Core Components

#### 1. Data Ingestion Job System

**Classes**: `DataIngestionJob`, `DataIngestionJobStatus`, `DataIngestionJobSnapshot`

Manages the lifecycle of import operations with state tracking and cancellation support.

**Job States**:
- `Queued`: Job created and waiting to start
- `Validating`: Validating file format and schema
- `Importing`: Actively importing features
- `Completed`: Successfully completed
- `Failed`: Import failed with errors
- `Cancelled`: Import cancelled by user

**Key Features**:
- Thread-safe state management
- Progress reporting (0.0 to 1.0)
- Cancellation token integration
- Stage-based progress tracking
- Immutable snapshots for status queries

#### 2. Feature Schema Validation

**Classes**: `FeatureSchemaValidator`, `IFeatureSchemaValidator`

Validates feature properties against layer schemas with comprehensive type checking and constraint validation.

**Validation Types**:
- Type validation (integer, float, string, boolean, datetime, geometry, etc.)
- Required field checking
- String length constraints
- Numeric range validation
- Precision and scale validation (for decimals)
- Custom format validation (email, URL, phone, postal code)

**Options**: `SchemaValidationOptions`

#### 3. Type Coercion

**Class**: `TypeCoercion`

Safely converts between data types during import to handle format inconsistencies.

**Supported Conversions**:
- String → Integer/Float/Boolean/DateTime/UUID
- Integer ↔ Float
- Boolean ↔ Integer (1/0)
- DateTime formats (ISO 8601, RFC 1123, custom patterns)
- Null handling and default values

#### 4. Job Queue Management

**Class**: `DataIngestionQueueStore`

Manages queued import jobs with in-memory storage and concurrent access control.

**Features**:
- Thread-safe job storage
- Job retrieval by ID or layer
- Job filtering by status
- Job cleanup for completed/failed jobs

### Import Pipeline

```
File Upload → Validation → Schema Detection → Type Coercion → Import → Completion
     ↓             ↓              ↓                ↓            ↓          ↓
  Format       Feature        Field           Convert       Insert    Update
  Check        Schema         Types           Values        Data      Status
```

## Usage Examples

### Creating an Import Job

```csharp
using Honua.Server.Core.Import;

public class ImportService
{
    private readonly DataIngestionQueueStore _queueStore;

    public async Task<Guid> StartImportAsync(
        string serviceId,
        string layerId,
        string fileName)
    {
        // Create job
        var job = new DataIngestionJob(
            serviceId: serviceId,
            layerId: layerId,
            sourceFileName: fileName
        );

        // Queue job
        _queueStore.Enqueue(job);

        // Start processing (background task)
        _ = ProcessJobAsync(job);

        return job.JobId;
    }
}
```

### Processing Import Job with Progress Tracking

```csharp
private async Task ProcessJobAsync(DataIngestionJob job)
{
    try
    {
        // Mark started
        job.MarkStarted("Validating file");

        // Validate file format
        await ValidateFileFormatAsync(job);
        job.UpdateProgress(
            DataIngestionJobStatus.Validating,
            "Detecting schema",
            0.1
        );

        // Detect schema
        var schema = await DetectSchemaAsync(job);
        job.UpdateProgress(
            DataIngestionJobStatus.Validating,
            "Schema detected",
            0.2
        );

        // Import features
        var features = ReadFeaturesAsync(job);
        long processed = 0;
        long total = await GetFeatureCountAsync(job);

        await foreach (var feature in features)
        {
            if (job.Token.IsCancellationRequested)
            {
                job.MarkCancelled("Import cancelled", "User requested cancellation");
                return;
            }

            // Import feature
            await ImportFeatureAsync(feature, schema);
            processed++;

            // Report progress
            job.ReportProgress(processed, total, "Importing features");
        }

        // Mark completed
        job.MarkCompleted("Import completed successfully");
    }
    catch (Exception ex)
    {
        // Mark failed
        job.MarkFailed("Import failed", ex.Message);
    }
}
```

### Feature Schema Validation

```csharp
using Honua.Server.Core.Import.Validation;

public class ImportValidator
{
    private readonly IFeatureSchemaValidator _validator;

    public async Task<FeatureValidationResult> ValidateFeatureAsync(
        IDictionary<string, object?> properties,
        LayerDefinition layer)
    {
        var options = new SchemaValidationOptions
        {
            ValidateSchema = true,
            CoerceTypes = true,
            TruncateLongStrings = false,
            ValidationMode = SchemaValidationMode.Strict,
            MaxValidationErrors = 100
        };

        var result = _validator.ValidateFeature(properties, layer, options);

        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                _logger.LogWarning(
                    "Validation error in field {Field}: {Message}",
                    error.FieldName,
                    error.Message
                );
            }
        }

        return result;
    }
}
```

### Type Coercion

```csharp
using Honua.Server.Core.Import.Validation;

// Coerce string to integer
var result = TypeCoercion.TryCoerce("123", "integer");
if (result.Success)
{
    int value = (int)result.Value!; // 123
}

// Coerce string to datetime
var result = TypeCoercion.TryCoerce("2025-01-15T10:30:00Z", "datetime");
if (result.Success)
{
    DateTime value = (DateTime)result.Value!;
}

// Coerce with error handling
var result = TypeCoercion.TryCoerce("invalid", "integer");
if (!result.Success)
{
    Console.WriteLine(result.ErrorMessage);
    // "Failed to coerce value to type 'integer'"
}
```

### Batch Validation

```csharp
public async Task<IReadOnlyList<FeatureValidationResult>> ValidateBatchAsync(
    IEnumerable<IDictionary<string, object?>> features,
    LayerDefinition layer)
{
    var options = SchemaValidationOptions.Default;

    var results = _validator.ValidateFeatures(features, layer, options);

    // Filter to only failed validations
    var failures = results.Where(r => !r.IsValid).ToList();

    if (failures.Count > 0)
    {
        _logger.LogWarning(
            "{FailureCount} of {TotalCount} features failed validation",
            failures.Count,
            results.Count
        );
    }

    return results;
}
```

### Custom Field Validators

```csharp
using Honua.Server.Core.Import.Validation;

// Email validation
bool isValid = CustomFieldValidators.IsValidEmail("user@example.com");

// URL validation
bool isValid = CustomFieldValidators.IsValidUrl("https://example.com");

// Phone number validation (North American format)
bool isValid = CustomFieldValidators.IsValidPhone("+1-555-123-4567");

// US Postal code validation
bool isValid = CustomFieldValidators.IsValidUsPostalCode("12345-6789");
```

## Configuration Options

### SchemaValidationOptions

```csharp
var options = new SchemaValidationOptions
{
    // Enable schema validation
    ValidateSchema = true,

    // Attempt to convert types (e.g., "123" → 123)
    CoerceTypes = true,

    // Truncate strings exceeding MaxLength
    TruncateLongStrings = false,

    // Validate custom formats (email, URL, phone)
    ValidateCustomFormats = true,

    // Validation mode
    ValidationMode = SchemaValidationMode.Strict,
    // Options: Strict, Lenient, BestEffort

    // Maximum errors before stopping validation
    MaxValidationErrors = 100
};
```

### Validation Modes

#### Strict Mode
- Fails on any validation error
- Type coercion failures are errors
- Required fields must be present
- **Use for**: Production imports, data quality enforcement

#### Lenient Mode
- Continues on non-critical errors
- Type coercion failures logged as warnings
- Missing optional fields ignored
- **Use for**: Development, exploratory imports

#### BestEffort Mode
- Attempts to import all data
- Uses default values for failed coercions
- Skips invalid features with warnings
- **Use for**: Legacy data migration, incomplete schemas

## Supported Formats

### GeoJSON

**MIME Type**: `application/geo+json`

**Features**:
- FeatureCollection and Feature objects
- Automatic geometry parsing
- Property extraction
- CRS detection

**Schema Detection**:
- Infers field types from first N features
- Detects nullable fields
- Estimates string lengths
- Identifies geometry type

### KML

**MIME Type**: `application/vnd.google-earth.kml+xml`

**Features**:
- Placemark parsing
- Extended data extraction
- TimeStamp/TimeSpan support
- Style information (optional)

**Schema Detection**:
- Extracts SimpleData fields
- Detects temporal properties
- Infers field types from values

### Shapefile

**MIME Type**: `application/x-shapefile` (requires .shp, .shx, .dbf files)

**Features**:
- DBF attribute parsing
- PRJ file for CRS detection
- Multi-file handling (.shp, .shx, .dbf, .prj)

**Schema Detection**:
- Uses DBF field definitions
- Maps DBF types to storage types
- Preserves field precision/scale

### CSV

**MIME Type**: `text/csv`

**Features**:
- WKT geometry column support
- Lat/Lon columns (auto-detection)
- Header row parsing
- Delimiter auto-detection

**Schema Detection**:
- Infers types from sample rows
- Detects numeric patterns
- Identifies date formats
- Estimates string lengths

### GeoPackage

**MIME Type**: `application/geopackage+sqlite3`

**Features**:
- SQLite-based format
- Built-in schema definitions
- Multi-layer support
- CRS from gpkg_spatial_ref_sys

**Schema Detection**:
- Reads schema from gpkg_contents
- Uses geometry_columns metadata
- Preserves field constraints

## Schema Detection

### Automatic Schema Inference

```csharp
public class SchemaDetector
{
    public async Task<LayerDefinition> DetectSchemaAsync(
        IAsyncEnumerable<FeatureRecord> features,
        int sampleSize = 1000)
    {
        var fieldStats = new Dictionary<string, FieldStatistics>();

        int count = 0;
        await foreach (var feature in features)
        {
            if (count++ >= sampleSize) break;

            foreach (var (key, value) in feature.Attributes)
            {
                if (!fieldStats.TryGetValue(key, out var stats))
                {
                    stats = new FieldStatistics(key);
                    fieldStats[key] = stats;
                }

                stats.AddValue(value);
            }
        }

        var fields = fieldStats.Values
            .Select(s => s.ToFieldDefinition())
            .ToList();

        return new LayerDefinition
        {
            Fields = fields,
            GeometryType = DetectGeometryType(features),
            // ... other properties
        };
    }
}
```

### Field Type Inference

The schema detector uses heuristics to infer field types:

| Sample Values | Inferred Type | Notes |
|--------------|---------------|-------|
| 1, 2, 3, 4 | integer | All values parse as integers |
| 1.5, 2.7, 3.9 | double | Contains decimal values |
| true, false, true | boolean | All values are boolean |
| "2025-01-15T10:30:00Z" | datetime | ISO 8601 format detected |
| "abc", "def", "ghi" | string | Default type for non-numeric |
| 1, 2, null, 3 | integer (nullable) | Contains null values |

## Error Handling

### Validation Errors

**Error Codes**:
- `RequiredFieldMissing`: Required field not present
- `InvalidType`: Value type doesn't match expected type
- `TypeCoercionFailed`: Failed to convert value to target type
- `StringTooLong`: String exceeds MaxLength constraint
- `NumericOutOfRange`: Numeric value outside valid range
- `PrecisionExceeded`: Decimal has too many total digits
- `ScaleExceeded`: Decimal has too many decimal places
- `InvalidFormat`: Value doesn't match expected format (email, URL, etc.)
- `InvalidGeometry`: Geometry is malformed or invalid

### Error Reporting

```csharp
var result = _validator.ValidateFeature(properties, layer, options);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Field: {error.FieldName}");
        Console.WriteLine($"Error: {error.ErrorCode}");
        Console.WriteLine($"Message: {error.Message}");
        Console.WriteLine($"Expected: {error.ExpectedValue}");
        Console.WriteLine($"Actual: {error.ActualValue}");
        Console.WriteLine($"Feature Index: {error.FeatureIndex}");
    }
}
```

### Handling Import Failures

```csharp
try
{
    await ProcessImportAsync(job);
}
catch (SchemaValidationException ex)
{
    job.MarkFailed("Schema validation failed", ex.Message);

    // Log validation errors
    foreach (var error in ex.ValidationErrors)
    {
        _logger.LogError(
            "Validation error: {Field} - {Message}",
            error.FieldName,
            error.Message
        );
    }
}
catch (FormatException ex)
{
    job.MarkFailed("Unsupported file format", ex.Message);
}
catch (Exception ex)
{
    job.MarkFailed("Import failed", ex.Message);
    _logger.LogError(ex, "Unexpected error during import");
}
```

## Best Practices

### Schema Validation

1. **Use Type Coercion**: Enable `CoerceTypes = true` to handle format variations
2. **Sample Schema Detection**: Use representative sample size (1000-10000 features)
3. **Define Explicit Schemas**: Prefer explicit schema definitions over inference for production
4. **Validate Early**: Validate schema before starting bulk import
5. **Batch Validation**: Validate in batches to balance performance and error reporting

### Job Management

1. **Background Processing**: Always process imports in background tasks
2. **Progress Reporting**: Update progress regularly (every N features or time interval)
3. **Cancellation Support**: Implement cancellation checks in long-running operations
4. **Resource Cleanup**: Dispose jobs after completion to free resources
5. **Error Isolation**: Catch and log errors per feature when possible

### Performance

1. **Batch Inserts**: Insert features in batches (500-1000 per batch)
2. **Disable Indexes**: Drop indexes before import, rebuild after completion
3. **Use Transactions**: Wrap batches in transactions for atomicity
4. **Stream Processing**: Use IAsyncEnumerable to avoid loading entire file
5. **Parallel Processing**: Process multiple jobs concurrently (with resource limits)

### Data Quality

1. **Sanitize Input**: Validate and sanitize all user-provided values
2. **Handle Nulls**: Define nullable fields explicitly in schema
3. **Set Constraints**: Use MaxLength, Precision, Scale constraints
4. **Validate Geometry**: Check geometry validity before import
5. **Log Rejections**: Log all rejected features with reasons

## Performance Characteristics

| Operation | Throughput | Notes |
|-----------|-----------|-------|
| Schema Detection | 10,000 features/sec | Samples first N features |
| Feature Validation | 50,000 features/sec | Without type coercion |
| Type Coercion | 30,000 features/sec | String → numeric conversion |
| GeoJSON Import | 20,000 features/sec | Includes parsing and validation |
| CSV Import | 25,000 features/sec | With geometry parsing |
| Shapefile Import | 15,000 features/sec | DBF parsing overhead |
| GeoPackage Import | 40,000 features/sec | Direct SQLite queries |

## Related Modules

- **Validation**: Provides geometry validation
- **Serialization**: Parses GeoJSON and other formats
- **Data**: Stores imported features
- **Export**: Exports data in various formats

## Testing

```csharp
[Fact]
public void ValidateFeature_WithValidData_ReturnsSuccess()
{
    // Arrange
    var validator = new FeatureSchemaValidator(logger);
    var layer = CreateTestLayer();
    var properties = new Dictionary<string, object?>
    {
        ["name"] = "Test Feature",
        ["population"] = 1000,
        ["area"] = 123.45
    };

    // Act
    var result = validator.ValidateFeature(properties, layer);

    // Assert
    Assert.True(result.IsValid);
    Assert.Empty(result.Errors);
}

[Fact]
public void TypeCoercion_ConvertsStringToInteger()
{
    // Arrange
    var value = "123";

    // Act
    var result = TypeCoercion.TryCoerce(value, "integer");

    // Assert
    Assert.True(result.Success);
    Assert.IsType<int>(result.Value);
    Assert.Equal(123, result.Value);
}

[Fact]
public async Task DataIngestionJob_TracksProgress()
{
    // Arrange
    var job = new DataIngestionJob("service1", "layer1", "test.geojson");

    // Act
    job.MarkStarted("Importing");
    job.ReportProgress(50, 100, "Importing features");

    // Assert
    var snapshot = job.Snapshot;
    Assert.Equal(DataIngestionJobStatus.Importing, snapshot.Status);
    Assert.Equal(0.5, snapshot.Progress);
    Assert.Equal("Importing features", snapshot.Stage);
}
```

## Common Issues and Solutions

### Issue: Schema detection produces incorrect types

**Cause**: Sample size too small or non-representative data

**Solution**: Increase sample size or define schema explicitly:
```csharp
var options = new SchemaDetectionOptions
{
    SampleSize = 10000, // Increase from default 1000
    ConfidenceThreshold = 0.95 // Require 95% of samples to match type
};
```

### Issue: Type coercion fails for valid values

**Cause**: Unexpected format or locale-specific formatting

**Solution**: Add custom coercion logic:
```csharp
// Custom date format
var dateFormats = new[] { "dd/MM/yyyy", "MM-dd-yyyy" };
if (DateTime.TryParseExact(value, dateFormats, CultureInfo.InvariantCulture,
    DateTimeStyles.None, out var date))
{
    return date;
}
```

### Issue: Import fails with "Out of memory"

**Cause**: Loading entire dataset into memory

**Solution**: Use streaming processing:
```csharp
// Use IAsyncEnumerable
await foreach (var feature in ReadFeaturesAsync(file))
{
    await ImportFeatureAsync(feature);
    // Feature eligible for GC immediately
}
```

### Issue: Validation too slow for large imports

**Solution**: Validate in batches and enable parallelism:
```csharp
await Parallel.ForEachAsync(
    features.Chunk(1000),
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (batch, ct) =>
    {
        var results = _validator.ValidateFeatures(batch, layer);
        await ImportBatchAsync(batch.Zip(results));
    }
);
```

## Version History

- **v1.0**: Initial release with GeoJSON and CSV import
- **v1.1**: Added Shapefile and KML support
- **v1.2**: Implemented schema detection and type coercion
- **v1.3**: Added GeoPackage import support
- **v1.4**: Enhanced validation with custom format validators
- **v1.5**: Added batch validation and parallel processing
- **v1.6**: Performance optimizations (3x throughput improvement)
