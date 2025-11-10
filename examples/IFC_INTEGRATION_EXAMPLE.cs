// Example: IFC Import Implementation with Xbim.Essentials
// This file shows how to integrate Xbim.Essentials for real IFC parsing
// NOTE: This is example code - not yet integrated into the project

/*
 * PREREQUISITES:
 * Add these NuGet packages to Honua.Server.Core.csproj:
 *
 * <PackageReference Include="Xbim.Essentials" Version="6.0.521" />
 * <PackageReference Include="Xbim.Geometry.Engine.Interop" Version="6.0.521" />
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;

// Once Xbim is added, uncomment these:
// using Xbim.Ifc;
// using Xbim.Ifc4.Interfaces;
// using Xbim.ModelGeometry.Scene;
// using Xbim.Common.Geometry;

namespace Honua.Server.Examples.Ifc;

/// <summary>
/// Example implementation showing how to use Xbim.Essentials for IFC import
/// </summary>
public class XbimIfcImportExample
{
    /// <summary>
    /// Example 1: Open an IFC file and extract basic metadata
    /// </summary>
    public async Task<Dictionary<string, object>> ExtractBasicMetadataAsync(Stream ifcStream)
    {
        var metadata = new Dictionary<string, object>();

        // This is how you'd use Xbim once the package is installed:
        /*
        using (var model = IfcStore.Open(ifcStream, null, -1))
        {
            // Extract schema version
            metadata["SchemaVersion"] = model.SchemaVersion.ToString();
            metadata["EntityCount"] = model.Instances.Count;

            // Get project information
            var project = model.Instances.FirstOrDefault<IIfcProject>();
            if (project != null)
            {
                metadata["ProjectName"] = project.Name?.ToString() ?? "Unnamed";
                metadata["ProjectDescription"] = project.Description?.ToString() ?? "";
                metadata["Phase"] = project.Phase?.ToString() ?? "";
            }

            // Get site information
            var site = model.Instances.FirstOrDefault<IIfcSite>();
            if (site != null)
            {
                metadata["SiteName"] = site.Name?.ToString() ?? "Unnamed";

                // Extract geolocation
                if (site.RefLatitude != null && site.RefLongitude != null)
                {
                    metadata["Latitude"] = ConvertCompoundAngleToDecimal(site.RefLatitude);
                    metadata["Longitude"] = ConvertCompoundAngleToDecimal(site.RefLongitude);
                    metadata["Elevation"] = site.RefElevation ?? 0.0;
                }
            }

            // Get building information
            var buildings = model.Instances.OfType<IIfcBuilding>();
            metadata["BuildingCount"] = buildings.Count();
            metadata["BuildingNames"] = buildings.Select(b => b.Name?.ToString() ?? "Unnamed").ToList();

            // Count element types
            metadata["WallCount"] = model.Instances.OfType<IIfcWall>().Count();
            metadata["DoorCount"] = model.Instances.OfType<IIfcDoor>().Count();
            metadata["WindowCount"] = model.Instances.OfType<IIfcWindow>().Count();
            metadata["SlabCount"] = model.Instances.OfType<IIfcSlab>().Count();
            metadata["ColumnCount"] = model.Instances.OfType<IIfcColumn>().Count();
            metadata["BeamCount"] = model.Instances.OfType<IIfcBeam>().Count();
        }
        */

        return await Task.FromResult(metadata);
    }

    /// <summary>
    /// Example 2: Extract properties from IFC elements
    /// </summary>
    public Dictionary<string, object> ExtractPropertiesFromElement(object ifcElement)
    {
        var properties = new Dictionary<string, object>();

        /*
        if (ifcElement is IIfcProduct product)
        {
            // Basic properties
            properties["GlobalId"] = product.GlobalId;
            properties["Name"] = product.Name?.ToString() ?? "";
            properties["Description"] = product.Description?.ToString() ?? "";
            properties["ObjectType"] = product.ObjectType?.ToString() ?? "";
            properties["Tag"] = product.Tag?.ToString() ?? "";

            // Extract property sets
            var propertyDefinitions = product.IsDefinedBy
                .OfType<IIfcRelDefinesByProperties>()
                .Select(r => r.RelatingPropertyDefinition);

            foreach (var propertyDef in propertyDefinitions)
            {
                if (propertyDef is IIfcPropertySet pset)
                {
                    var psetName = pset.Name?.ToString() ?? "Unknown";

                    foreach (var prop in pset.HasProperties)
                    {
                        if (prop is IIfcPropertySingleValue singleValue)
                        {
                            var key = $"{psetName}.{singleValue.Name}";
                            var value = singleValue.NominalValue?.Value;
                            if (value != null)
                            {
                                properties[key] = value;
                            }
                        }
                    }
                }
            }

            // Extract quantities
            var quantities = product.IsDefinedBy
                .OfType<IIfcRelDefinesByProperties>()
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcElementQuantity>();

            foreach (var quantitySet in quantities)
            {
                var qsetName = quantitySet.Name?.ToString() ?? "Quantities";

                foreach (var quantity in quantitySet.Quantities)
                {
                    var key = $"{qsetName}.{quantity.Name}";

                    if (quantity is IIfcQuantityLength length)
                        properties[key] = length.LengthValue;
                    else if (quantity is IIfcQuantityArea area)
                        properties[key] = area.AreaValue;
                    else if (quantity is IIfcQuantityVolume volume)
                        properties[key] = volume.VolumeValue;
                }
            }
        }
        */

        return properties;
    }

    /// <summary>
    /// Example 3: Extract 3D geometry and convert to triangle mesh
    /// </summary>
    public async Task<Geometry?> ExtractGeometryAsync(object ifcElement)
    {
        /*
        if (ifcElement is IIfcProduct product)
        {
            // Use Xbim.Geometry to generate mesh
            var context = new Xbim3DModelContext(product.Model);
            context.CreateContext();

            var productShape = context.ShapeInstancesOf(product).FirstOrDefault();
            if (productShape != null)
            {
                var geometry = context.ShapeGeometry(productShape);

                // Extract vertices and triangles
                var vertices = new List<Coordinate>();
                var triangles = new List<int>();

                using (var stream = new MemoryStream(geometry.ShapeData))
                using (var reader = new BinaryReader(stream))
                {
                    // Read vertices
                    var vertexCount = reader.ReadInt32();
                    for (int i = 0; i < vertexCount; i++)
                    {
                        var x = reader.ReadSingle();
                        var y = reader.ReadSingle();
                        var z = reader.ReadSingle();
                        vertices.Add(new CoordinateZ(x, y, z));
                    }

                    // Read triangle indices
                    var indexCount = reader.ReadInt32();
                    for (int i = 0; i < indexCount; i++)
                    {
                        triangles.Add(reader.ReadInt32());
                    }
                }

                // Convert to NetTopologySuite geometry
                // For now, create a bounding box polygon
                var envelope = new Envelope();
                foreach (var vertex in vertices)
                {
                    envelope.ExpandToInclude(vertex);
                }

                var factory = new GeometryFactory();
                var coords = new[]
                {
                    new CoordinateZ(envelope.MinX, envelope.MinY, vertices.Min(v => v.Z)),
                    new CoordinateZ(envelope.MaxX, envelope.MinY, vertices.Min(v => v.Z)),
                    new CoordinateZ(envelope.MaxX, envelope.MaxY, vertices.Max(v => v.Z)),
                    new CoordinateZ(envelope.MinX, envelope.MaxY, vertices.Max(v => v.Z)),
                    new CoordinateZ(envelope.MinX, envelope.MinY, vertices.Min(v => v.Z))
                };

                return factory.CreatePolygon(coords);
            }
        }
        */

        return await Task.FromResult<Geometry?>(null);
    }

    /// <summary>
    /// Example 4: Extract spatial relationships
    /// </summary>
    public Dictionary<string, List<string>> ExtractRelationships(object ifcElement)
    {
        var relationships = new Dictionary<string, List<string>>();

        /*
        if (ifcElement is IIfcObjectDefinition objDef)
        {
            // Aggregation relationships (contains)
            var aggregates = objDef.IsDecomposedBy
                .SelectMany(rel => rel.RelatedObjects)
                .Select(obj => obj.GlobalId.ToString())
                .ToList();

            if (aggregates.Any())
                relationships["CONTAINS"] = aggregates;

            // Spatial containment
            if (objDef is IIfcProduct product)
            {
                var spatialRels = product.ContainedInStructure
                    .Select(rel => rel.RelatingStructure.GlobalId.ToString())
                    .ToList();

                if (spatialRels.Any())
                    relationships["LOCATED_IN"] = spatialRels;

                // Connected elements
                var connections = product.ConnectedTo
                    .SelectMany(rel => rel.RelatedElements)
                    .Select(elem => elem.GlobalId.ToString())
                    .ToList();

                if (connections.Any())
                    relationships["CONNECTS_TO"] = connections;
            }
        }
        */

        return relationships;
    }

    /// <summary>
    /// Example 5: Full import workflow
    /// </summary>
    public async Task<ImportResult> ImportIfcFileAsync(
        Stream ifcStream,
        string targetServiceId,
        string targetLayerId)
    {
        var result = new ImportResult
        {
            JobId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow
        };

        try
        {
            /*
            using (var model = IfcStore.Open(ifcStream, null, -1))
            {
                // Extract project metadata
                result.Metadata = await ExtractBasicMetadataAsync(ifcStream);

                // Get all building elements
                var buildingElements = model.Instances.OfType<IIfcBuildingElement>();

                foreach (var element in buildingElements)
                {
                    try
                    {
                        // Extract properties
                        var properties = ExtractPropertiesFromElement(element);

                        // Extract geometry
                        var geometry = await ExtractGeometryAsync(element);

                        // Extract relationships
                        var relationships = ExtractRelationships(element);

                        // Create Honua feature
                        // var featureId = await CreateFeatureInHonua(
                        //     targetServiceId,
                        //     targetLayerId,
                        //     element.GetType().Name,
                        //     geometry,
                        //     properties
                        // );

                        result.FeaturesCreated++;

                        // Track entity type
                        var entityType = element.GetType().Name;
                        if (!result.EntityTypeCounts.ContainsKey(entityType))
                            result.EntityTypeCounts[entityType] = 0;
                        result.EntityTypeCounts[entityType]++;

                        // Create graph relationships (if Apache AGE is available)
                        // foreach (var (relType, targetIds) in relationships)
                        // {
                        //     result.RelationshipsCreated += await CreateGraphRelationships(
                        //         element.GlobalId.ToString(),
                        //         relType,
                        //         targetIds
                        //     );
                        // }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Error processing {element.GlobalId}: {ex.Message}");
                    }
                }

                // Calculate bounding box
                // result.BoundingBox = CalculateBoundingBox(model);
            }
            */

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    #region Helper Methods

    /// <summary>
    /// Convert IFC compound angle (degrees, minutes, seconds) to decimal degrees
    /// </summary>
    private double ConvertCompoundAngleToDecimal(object compoundAngle)
    {
        /*
        if (compoundAngle is IIfcCompoundPlaneAngleMeasure angle)
        {
            var values = angle.AsString.Split(' ').Select(double.Parse).ToArray();
            double degrees = values.Length > 0 ? values[0] : 0;
            double minutes = values.Length > 1 ? values[1] : 0;
            double seconds = values.Length > 2 ? values[2] : 0;

            return degrees + (minutes / 60.0) + (seconds / 3600.0);
        }
        */
        return 0.0;
    }

    #endregion

    #region Result Classes

    public class ImportResult
    {
        public Guid JobId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int FeaturesCreated { get; set; }
        public int RelationshipsCreated { get; set; }
        public Dictionary<string, int> EntityTypeCounts { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public TimeSpan Duration => EndTime - StartTime;
    }

    #endregion
}

// ============================================================================
// USAGE EXAMPLE
// ============================================================================

public class UsageExample
{
    public async Task DemonstrateUsage()
    {
        var importer = new XbimIfcImportExample();

        // Example 1: Extract metadata
        using (var fileStream = File.OpenRead("sample.ifc"))
        {
            var metadata = await importer.ExtractBasicMetadataAsync(fileStream);
            Console.WriteLine($"Project: {metadata["ProjectName"]}");
            Console.WriteLine($"Buildings: {metadata["BuildingCount"]}");
            Console.WriteLine($"Walls: {metadata["WallCount"]}");
        }

        // Example 2: Full import
        using (var fileStream = File.OpenRead("building.ifc"))
        {
            var result = await importer.ImportIfcFileAsync(
                fileStream,
                "my-service",
                "buildings-layer"
            );

            Console.WriteLine($"Import completed: {result.Success}");
            Console.WriteLine($"Features created: {result.FeaturesCreated}");
            Console.WriteLine($"Duration: {result.Duration.TotalSeconds}s");

            foreach (var (entityType, count) in result.EntityTypeCounts)
            {
                Console.WriteLine($"  {entityType}: {count}");
            }
        }
    }
}

// ============================================================================
// CURL EXAMPLES FOR API TESTING
// ============================================================================

/*
# Test 1: Validate an IFC file
curl -X POST http://localhost:5000/api/ifc/validate \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@path/to/sample.ifc"

# Test 2: Extract metadata
curl -X POST http://localhost:5000/api/ifc/metadata \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@path/to/building.ifc"

# Test 3: Import IFC file
curl -X POST http://localhost:5000/api/ifc/import \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@path/to/office.ifc" \
  -F "targetServiceId=downtown" \
  -F "targetLayerId=buildings" \
  -F "importGeometry=true" \
  -F "importProperties=true" \
  -F "importRelationships=true"

# Test 4: Get supported versions
curl http://localhost:5000/api/ifc/versions
*/
