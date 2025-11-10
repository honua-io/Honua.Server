# IFC Import Implementation - Quick Start Guide

**Target Audience:** Developers implementing Phase 1.3
**Estimated Time:** 5-6 weeks
**Prerequisites:** Phase 1.1 (Apache AGE) recommended but not required for basic features

---

## Week 1: Setup & Basic Parsing

### Step 1.1: Add NuGet Packages

Edit `/src/Honua.Server.Core/Honua.Server.Core.csproj`:

```xml
<ItemGroup>
  <!-- IFC Support -->
  <PackageReference Include="Xbim.Essentials" Version="6.0.521" />
  <PackageReference Include="Xbim.Geometry.Engine.Interop" Version="6.0.521" />
</ItemGroup>
```

Run:
```bash
cd /home/user/Honua.Server/src/Honua.Server.Core
dotnet restore
dotnet build
```

### Step 1.2: Download Sample Files

Create test data directory:
```bash
mkdir -p /home/user/Honua.Server/test-data/ifc-samples
cd /home/user/Honua.Server/test-data/ifc-samples

# Download sample files from buildingSMART
wget https://github.com/buildingSMART/Sample-Test-Files/raw/master/IFC%202x3/Duplex_A_20110907.ifc
wget https://github.com/buildingSMART/Sample-Test-Files/raw/master/IFC%204.0/Duplex_A_20110907.ifc
```

### Step 1.3: Implement Basic File Opening

Update `/src/Honua.Server.Core/Services/IfcImportService.cs`:

```csharp
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

public async Task<IfcValidationResult> ValidateIfcAsync(
    Stream ifcFileStream,
    CancellationToken cancellationToken = default)
{
    var result = new IfcValidationResult
    {
        FileSizeBytes = ifcFileStream.Length
    };

    try
    {
        using (var model = IfcStore.Open(ifcFileStream, null, -1))
        {
            result.IsValid = true;
            result.SchemaVersion = model.SchemaVersion.ToString();
            result.EntityCount = model.Instances.Count;

            // Detect file format
            result.FileFormat = "STEP"; // Default

            _logger.LogInformation(
                "IFC validation successful: Schema={Schema}, Entities={Count}",
                result.SchemaVersion, result.EntityCount);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "IFC validation failed");
        result.IsValid = false;
        result.Errors.Add(ex.Message);
    }

    return result;
}
```

### Step 1.4: Test Basic Parsing

Create test:
```csharp
// /tests/Honua.Server.Core.Tests/Services/IfcImportServiceTests.cs

[Fact]
public async Task ValidateIfcAsync_ValidFile_ReturnsSuccess()
{
    // Arrange
    var service = new IfcImportService(_logger);
    var filePath = "test-data/ifc-samples/Duplex_A_20110907.ifc";

    using var stream = File.OpenRead(filePath);

    // Act
    var result = await service.ValidateIfcAsync(stream);

    // Assert
    Assert.True(result.IsValid);
    Assert.NotEmpty(result.SchemaVersion);
    Assert.True(result.EntityCount > 0);
}
```

---

## Week 2: Metadata Extraction

### Step 2.1: Implement Project Metadata Extraction

```csharp
public async Task<IfcProjectMetadata> ExtractMetadataAsync(
    Stream ifcFileStream,
    CancellationToken cancellationToken = default)
{
    var metadata = new IfcProjectMetadata();

    using (var model = IfcStore.Open(ifcFileStream, null, -1))
    {
        metadata.SchemaVersion = model.SchemaVersion.ToString();

        // Extract project information
        var project = model.Instances.FirstOrDefault<IIfcProject>();
        if (project != null)
        {
            metadata.ProjectName = project.Name?.ToString();
            metadata.Description = project.Description?.ToString();
            metadata.Phase = project.Phase?.ToString();
        }

        // Extract site information
        var site = model.Instances.FirstOrDefault<IIfcSite>();
        if (site != null)
        {
            metadata.SiteName = site.Name?.ToString();

            // Extract geolocation
            if (site.RefLatitude != null && site.RefLongitude != null)
            {
                metadata.SiteLocation = new SiteLocation
                {
                    Latitude = ConvertCompoundAngleToDecimal(site.RefLatitude),
                    Longitude = ConvertCompoundAngleToDecimal(site.RefLongitude),
                    Elevation = site.RefElevation
                };
            }
        }

        // Extract building information
        var buildings = model.Instances.OfType<IIfcBuilding>();
        metadata.BuildingNames = buildings
            .Select(b => b.Name?.ToString() ?? "Unnamed")
            .ToList();

        // Extract units
        var project = model.Instances.FirstOrDefault<IIfcProject>();
        if (project?.UnitsInContext?.Units != null)
        {
            foreach (var unit in project.UnitsInContext.Units)
            {
                if (unit is IIfcSIUnit siUnit)
                {
                    switch (siUnit.UnitType)
                    {
                        case Xbim.Ifc4.Interfaces.IfcUnitEnum.LENGTHUNIT:
                            metadata.LengthUnit = siUnit.Name.ToString();
                            break;
                        case Xbim.Ifc4.Interfaces.IfcUnitEnum.AREAUNIT:
                            metadata.AreaUnit = siUnit.Name.ToString();
                            break;
                        case Xbim.Ifc4.Interfaces.IfcUnitEnum.VOLUMEUNIT:
                            metadata.VolumeUnit = siUnit.Name.ToString();
                            break;
                    }
                }
            }
        }

        // Count elements
        metadata.TotalSpatialElements = model.Instances
            .OfType<IIfcSpatialElement>().Count();
        metadata.TotalBuildingElements = model.Instances
            .OfType<IIfcBuildingElement>().Count();
    }

    return metadata;
}

private double ConvertCompoundAngleToDecimal(IIfcCompoundPlaneAngleMeasure angle)
{
    var values = angle.AsDoubleList();
    double degrees = values.Count > 0 ? values[0] : 0;
    double minutes = values.Count > 1 ? values[1] : 0;
    double seconds = values.Count > 2 ? values[2] : 0;

    return degrees + (minutes / 60.0) + (seconds / 3600.0);
}
```

### Step 2.2: Test Metadata Extraction

```bash
curl -X POST http://localhost:5000/api/ifc/metadata \
  -F "file=@test-data/ifc-samples/Duplex_A_20110907.ifc"
```

---

## Week 3: Property & Geometry Extraction

### Step 3.1: Implement Property Extraction

```csharp
private Dictionary<string, object> ExtractProperties(IIfcProduct product)
{
    var properties = new Dictionary<string, object>
    {
        ["GlobalId"] = product.GlobalId.ToString(),
        ["Name"] = product.Name?.ToString() ?? "",
        ["Description"] = product.Description?.ToString() ?? "",
        ["ObjectType"] = product.ObjectType?.ToString() ?? ""
    };

    // Extract property sets
    var propertySets = product.IsDefinedBy
        .OfType<IIfcRelDefinesByProperties>()
        .Select(r => r.RelatingPropertyDefinition)
        .OfType<IIfcPropertySet>();

    foreach (var pset in propertySets)
    {
        var psetName = pset.Name?.ToString() ?? "Unknown";

        foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
        {
            var key = $"{psetName}.{prop.Name}";
            var value = prop.NominalValue?.Value;
            if (value != null)
            {
                properties[key] = value;
            }
        }
    }

    // Extract quantities
    var quantities = product.IsDefinedBy
        .OfType<IIfcRelDefinesByProperties>()
        .Select(r => r.RelatingPropertyDefinition)
        .OfType<IIfcElementQuantity>();

    foreach (var qset in quantities)
    {
        var qsetName = qset.Name?.ToString() ?? "Quantities";

        foreach (var quantity in qset.Quantities)
        {
            var key = $"{qsetName}.{quantity.Name}";

            switch (quantity)
            {
                case IIfcQuantityLength length:
                    properties[key] = length.LengthValue;
                    break;
                case IIfcQuantityArea area:
                    properties[key] = area.AreaValue;
                    break;
                case IIfcQuantityVolume volume:
                    properties[key] = volume.VolumeValue;
                    break;
                case IIfcQuantityCount count:
                    properties[key] = count.CountValue;
                    break;
                case IIfcQuantityWeight weight:
                    properties[key] = weight.WeightValue;
                    break;
            }
        }
    }

    return properties;
}
```

### Step 3.2: Implement Basic Geometry Extraction

```csharp
using Xbim.Common.Geometry;
using Xbim.ModelGeometry.Scene;

private async Task<Geometry?> ExtractGeometry(
    IIfcProduct product,
    IfcImportOptions options)
{
    try
    {
        var context = new Xbim3DModelContext(product.Model);
        context.CreateContext();

        var productShape = context.ShapeInstancesOf(product).FirstOrDefault();
        if (productShape == null)
            return null;

        var transform = productShape.Transformation;

        // Extract bounding box for now (simplified)
        var bbox = productShape.BoundingBox;

        // Apply transformation
        var min = transform.Transform(bbox.Min);
        var max = transform.Transform(bbox.Max);

        // Create 2D polygon from bounding box (simplified geometry)
        var factory = new GeometryFactory();
        var coords = new[]
        {
            new Coordinate(min.X, min.Y),
            new Coordinate(max.X, min.Y),
            new Coordinate(max.X, max.Y),
            new Coordinate(min.X, max.Y),
            new Coordinate(min.X, min.Y)
        };

        var polygon = factory.CreatePolygon(coords);

        // Apply georeferencing if specified
        if (options.GeoReference != null)
        {
            polygon = ApplyCoordinateTransform(polygon, options.GeoReference);
        }

        return polygon;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to extract geometry for {GlobalId}",
            product.GlobalId);
        return null;
    }
}

private Geometry ApplyCoordinateTransform(
    Geometry geometry,
    CoordinateTransformOptions transform)
{
    // Apply scale, offset, and rotation
    var affineTransform = new NetTopologySuite.Geometries.Utilities.AffineTransformation();

    // Scale
    affineTransform.Scale(transform.Scale, transform.Scale);

    // Rotate (if needed)
    if (transform.RotationDegrees != 0)
    {
        var radians = transform.RotationDegrees * Math.PI / 180.0;
        affineTransform.Rotate(radians);
    }

    // Translate
    affineTransform.Translate(transform.OffsetX, transform.OffsetY);

    var transformed = affineTransform.Transform(geometry);
    transformed.SRID = transform.TargetSrid;

    return transformed;
}
```

---

## Week 4: Feature Creation & Relationships

### Step 4.1: Create Features in Database

You'll need to integrate with Honua's existing feature service:

```csharp
// Assuming you have IFeatureService injected
private readonly IFeatureService _featureService;

private async Task<Guid> CreateFeature(
    string serviceId,
    string layerId,
    IIfcProduct product,
    Geometry? geometry,
    Dictionary<string, object> properties,
    CancellationToken cancellationToken)
{
    var feature = new Feature
    {
        Id = Guid.NewGuid(),
        ServiceId = serviceId,
        LayerId = layerId,
        Geometry = geometry,
        Properties = properties
    };

    // Add IFC-specific properties
    feature.Properties["ifc_global_id"] = product.GlobalId.ToString();
    feature.Properties["ifc_type"] = product.GetType().Name;
    feature.Properties["ifc_entity_label"] = product.EntityLabel;

    await _featureService.CreateFeatureAsync(feature, cancellationToken);

    return feature.Id;
}
```

### Step 4.2: Extract and Store Relationships

```csharp
private async Task<int> ExtractAndCreateRelationships(
    IIfcProduct product,
    Guid featureId,
    CancellationToken cancellationToken)
{
    int relationshipsCreated = 0;

    // Spatial containment
    var spatialContainer = product.ContainedInStructure
        .FirstOrDefault()?.RelatingStructure;

    if (spatialContainer != null)
    {
        // Create LOCATED_IN relationship
        // This requires Apache AGE integration from Phase 1.1
        // await CreateGraphRelationship(
        //     featureId,
        //     "LOCATED_IN",
        //     spatialContainer.GlobalId.ToString()
        // );
        relationshipsCreated++;
    }

    // Element connections
    foreach (var connection in product.ConnectedTo)
    {
        foreach (var relatedElement in connection.RelatedElements)
        {
            // Create CONNECTS_TO relationship
            // await CreateGraphRelationship(
            //     featureId,
            //     "CONNECTS_TO",
            //     relatedElement.GlobalId.ToString()
            // );
            relationshipsCreated++;
        }
    }

    return relationshipsCreated;
}
```

---

## Week 5: Full Import Implementation

### Step 5.1: Complete Import Workflow

```csharp
public async Task<IfcImportResult> ImportIfcFileAsync(
    Stream ifcFileStream,
    IfcImportOptions options,
    CancellationToken cancellationToken = default)
{
    var result = new IfcImportResult
    {
        ImportJobId = Guid.NewGuid(),
        StartTime = DateTime.UtcNow
    };

    try
    {
        _logger.LogInformation("Starting IFC import job {JobId}", result.ImportJobId);

        using (var model = IfcStore.Open(ifcFileStream, null, -1))
        {
            // Extract project metadata
            result.ProjectMetadata = await ExtractProjectMetadata(model, cancellationToken);

            // Get building elements to import
            var elements = model.Instances.OfType<IIfcBuildingElement>();

            // Apply entity filter if specified
            if (options.EntityTypeFilter?.Any() == true)
            {
                elements = elements.Where(e =>
                    options.EntityTypeFilter.Contains(e.GetType().Name));
            }

            // Apply max entities limit if specified
            if (options.MaxEntities.HasValue)
            {
                elements = elements.Take(options.MaxEntities.Value);
            }

            // Process each element
            foreach (var element in elements)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Extract geometry
                    var geometry = options.ImportGeometry
                        ? await ExtractGeometry(element, options)
                        : null;

                    // Extract properties
                    var properties = options.ImportProperties
                        ? ExtractProperties(element)
                        : new Dictionary<string, object>();

                    // Create Honua feature
                    var featureId = await CreateFeature(
                        options.TargetServiceId!,
                        options.TargetLayerId!,
                        element,
                        geometry,
                        properties,
                        cancellationToken);

                    result.FeaturesCreated++;

                    // Track entity type
                    var entityType = element.GetType().Name;
                    if (!result.EntityTypeCounts.ContainsKey(entityType))
                        result.EntityTypeCounts[entityType] = 0;
                    result.EntityTypeCounts[entityType]++;

                    // Extract relationships
                    if (options.ImportRelationships)
                    {
                        var relCount = await ExtractAndCreateRelationships(
                            element,
                            featureId,
                            cancellationToken);

                        result.RelationshipsCreated += relCount;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing entity {EntityLabel}",
                        element.EntityLabel);

                    result.Warnings.Add(new ImportWarning
                    {
                        EntityId = element.EntityLabel.ToString(),
                        EntityType = element.GetType().Name,
                        Message = ex.Message,
                        Severity = WarningSeverity.Medium
                    });
                }
            }

            // Calculate project extent
            result.ProjectExtent = CalculateProjectExtent(model);
        }

        _logger.LogInformation(
            "IFC import job {JobId} completed: {FeaturesCreated} features created",
            result.ImportJobId, result.FeaturesCreated);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fatal error during IFC import job {JobId}",
            result.ImportJobId);

        result.Errors.Add(new ImportError
        {
            Message = ex.Message,
            StackTrace = ex.StackTrace,
            IsFatal = true
        });
    }
    finally
    {
        result.EndTime = DateTime.UtcNow;
    }

    return result;
}

private BoundingBox3D CalculateProjectExtent(IModel model)
{
    var bbox = new BoundingBox3D
    {
        MinX = double.MaxValue,
        MinY = double.MaxValue,
        MinZ = double.MaxValue,
        MaxX = double.MinValue,
        MaxY = double.MinValue,
        MaxZ = double.MinValue
    };

    var context = new Xbim3DModelContext(model);
    context.CreateContext();

    foreach (var shape in context.ShapeInstances())
    {
        var box = shape.BoundingBox;
        var transform = shape.Transformation;

        var min = transform.Transform(box.Min);
        var max = transform.Transform(box.Max);

        bbox.MinX = Math.Min(bbox.MinX, min.X);
        bbox.MinY = Math.Min(bbox.MinY, min.Y);
        bbox.MinZ = Math.Min(bbox.MinZ, min.Z);
        bbox.MaxX = Math.Max(bbox.MaxX, max.X);
        bbox.MaxY = Math.Max(bbox.MaxY, max.Y);
        bbox.MaxZ = Math.Max(bbox.MaxZ, max.Z);
    }

    return bbox;
}
```

---

## Week 6: Testing & Optimization

### Step 6.1: Integration Tests

```csharp
[Theory]
[InlineData("Duplex_A_20110907.ifc", 2847)]
[InlineData("AC-20-Smiley-West-10-IFC2x3.ifc", 532)]
public async Task ImportIfcFileAsync_RealFiles_CreatesFeatures(
    string fileName,
    int expectedMinFeatures)
{
    // Arrange
    var service = CreateService();
    var filePath = Path.Combine("test-data/ifc-samples", fileName);
    var options = new IfcImportOptions
    {
        TargetServiceId = "test-service",
        TargetLayerId = "test-layer",
        ImportGeometry = true,
        ImportProperties = true
    };

    // Act
    using var stream = File.OpenRead(filePath);
    var result = await service.ImportIfcFileAsync(stream, options);

    // Assert
    Assert.True(result.Success);
    Assert.True(result.FeaturesCreated >= expectedMinFeatures);
    Assert.NotNull(result.ProjectExtent);
}
```

### Step 6.2: Performance Testing

```bash
# Benchmark script
#!/bin/bash

echo "IFC Import Performance Test"
echo "==========================="

for file in test-data/ifc-samples/*.ifc; do
    echo ""
    echo "Testing: $(basename $file)"
    echo "Size: $(du -h $file | cut -f1)"

    time curl -X POST http://localhost:5000/api/ifc/import \
        -F "file=@$file" \
        -F "targetServiceId=benchmark" \
        -F "targetLayerId=test" \
        -F "maxEntities=1000"
done
```

### Step 6.3: Register Service in DI

Edit `/src/Honua.Server.Core/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddHonuaCoreServices(
    this IServiceCollection services)
{
    // ... existing services ...

    // IFC Import Service
    services.AddScoped<IIfcImportService, IfcImportService>();

    return services;
}
```

---

## Common Issues & Solutions

### Issue 1: Geometry Engine Not Found

**Error:** `Unable to load Xbim.Geometry.Engine.Interop`

**Solution:**
- Ensure you've installed `Xbim.Geometry.Engine.Interop` package
- Check that native libraries are copied to output directory
- On Linux, install required libraries: `apt-get install libgdiplus`

### Issue 2: Large File Memory Issues

**Error:** OutOfMemoryException

**Solution:**
```csharp
// Use memory-mapped files for large IFC files
var tempFile = Path.GetTempFileName();
await using (var fileStream = File.Create(tempFile))
{
    await ifcFileStream.CopyToAsync(fileStream);
}

using var model = IfcStore.Open(tempFile, null, -1);
// Process...
File.Delete(tempFile);
```

### Issue 3: Coordinate System Mismatch

**Error:** Features in wrong location

**Solution:**
```csharp
// Always specify georeferencing explicitly
var options = new IfcImportOptions
{
    GeoReference = new CoordinateTransformOptions
    {
        TargetSrid = 4326, // WGS84
        OffsetX = -122.4194, // San Francisco longitude
        OffsetY = 37.7749,   // San Francisco latitude
        OffsetZ = 10.0,      // Elevation offset
        Scale = 0.0001       // Convert meters to degrees (approx)
    }
};
```

---

## Checklist

### Week 1
- [ ] Install Xbim.Essentials NuGet package
- [ ] Download sample IFC files
- [ ] Implement basic file validation
- [ ] Create unit tests for validation

### Week 2
- [ ] Implement metadata extraction
- [ ] Test with multiple IFC versions
- [ ] Add API endpoint tests
- [ ] Document metadata structure

### Week 3
- [ ] Implement property extraction
- [ ] Implement geometry extraction
- [ ] Test coordinate transformations
- [ ] Optimize geometry processing

### Week 4
- [ ] Integrate with Honua feature service
- [ ] Implement relationship extraction
- [ ] Test with Apache AGE (if available)
- [ ] Add batch processing

### Week 5
- [ ] Complete full import workflow
- [ ] Add error handling
- [ ] Implement progress tracking
- [ ] Test with large files

### Week 6
- [ ] Write integration tests
- [ ] Performance benchmarking
- [ ] Documentation
- [ ] Code review

---

## Resources

- **Xbim Documentation:** https://docs.xbim.net/
- **Xbim Examples:** https://github.com/xBimTeam/XbimEssentials/tree/master/Xbim.Essentials.Tests
- **IFC Specification:** https://standards.buildingsmart.org/IFC/RELEASE/IFC4/ADD2_TC1/HTML/
- **Sample Files:** https://github.com/buildingSMART/Sample-Test-Files
- **buildingSMART Forum:** https://forums.buildingsmart.org/

---

**Ready to implement? Start with Week 1 and work through systematically!**
