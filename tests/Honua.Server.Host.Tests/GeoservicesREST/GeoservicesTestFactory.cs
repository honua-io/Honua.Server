using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Shared factory helpers for constructing catalog metadata used by GeoServices unit tests.
/// Keeps the test setup aligned with the current metadata models.
/// </summary>
internal static class GeoservicesTestFactory
{
    private const string DefaultServiceId = "test_service";
    private const string DefaultLayerId = "test_layer";

    public static ServiceDefinition CreateServiceDefinition(
        int? itemLimit = null,
        string defaultCrs = "EPSG:4326",
        IEnumerable<LayerDefinition>? layers = null)
    {
        var layerArray = layers?.ToArray() ?? System.Array.Empty<LayerDefinition>();

        return new ServiceDefinition
        {
            Id = DefaultServiceId,
            Title = "Test Service",
            FolderId = "root",
            ServiceType = "FeatureServer",
            DataSourceId = "primary",
            Ogc = new OgcServiceDefinition
            {
                DefaultCrs = defaultCrs,
                ItemLimit = itemLimit
            },
            Layers = layerArray
        };
    }

    public static CatalogServiceView CreateServiceView(
        ServiceDefinition? service = null,
        IReadOnlyList<CatalogLayerView>? layers = null)
    {
        service ??= CreateServiceDefinition();

        return new CatalogServiceView
        {
            Service = service,
            FolderTitle = "Root Folder",
            FolderOrder = 0,
            Keywords = System.Array.Empty<string>(),
            Links = System.Array.Empty<LinkDefinition>(),
            Layers = layers ?? System.Array.Empty<CatalogLayerView>()
        };
    }

    public static LayerDefinition CreateLayerDefinition(
        IEnumerable<FieldDefinition>? fields = null,
        int? maxRecordCount = 1000,
        string geometryType = "esriGeometryPolygon",
        string geometryField = "shape",
        IEnumerable<string>? crs = null)
    {
        var fieldList = fields?.ToArray() ?? DefaultFields();
        var crsValues = crs?.ToArray() ?? new[] { "EPSG:4326", "EPSG:3857" };

        return new LayerDefinition
        {
            Id = DefaultLayerId,
            ServiceId = DefaultServiceId,
            Title = "Test Layer",
            GeometryType = geometryType,
            GeometryField = geometryField,
            IdField = "objectid",
            Fields = fieldList,
            Crs = crsValues,
            Query = new LayerQueryDefinition
            {
                MaxRecordCount = maxRecordCount
            }
        };
    }

    public static CatalogLayerView CreateLayerView(LayerDefinition? layer = null)
    {
        layer ??= CreateLayerDefinition();

        return new CatalogLayerView
        {
            Layer = layer,
            Keywords = System.Array.Empty<string>(),
            Themes = System.Array.Empty<string>(),
            Contacts = System.Array.Empty<CatalogContactDefinition>(),
            Links = System.Array.Empty<LinkDefinition>()
        };
    }

    public static IReadOnlyList<FieldDefinition> DefaultFields() => new[]
    {
        new FieldDefinition { Name = "objectid", DataType = "integer", Nullable = false },
        new FieldDefinition { Name = "name", DataType = "string", Nullable = true },
        new FieldDefinition { Name = "population", DataType = "integer", Nullable = true },
        new FieldDefinition { Name = "area", DataType = "double", Nullable = true }
    };
}
