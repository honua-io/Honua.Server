using System;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Query;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class MetadataQueryModelBuilderTests
{
    private readonly MetadataQueryModelBuilder _builder = new();

    [Fact]
    public void Build_ShouldSurfaceAllFieldsWithTypes()
    {
        var layer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = "roads",
            Title = "Primary Roads",
            GeometryType = "LineString",
            IdField = "road_id",
            GeometryField = "geom",
            DisplayField = "name",
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int64", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", Nullable = true },
                new FieldDefinition { Name = "speed", DataType = "double", Nullable = true },
                new FieldDefinition { Name = "is_active", DataType = "boolean", Nullable = false }
            }
        };

        var entity = _builder.Build(CreateSnapshot(layer), CreateService(layer), layer);

        entity.Fields.Should().ContainKey("road_id");
        entity.Fields.Should().ContainKey("geom");
        entity.Fields.Should().ContainKey("name");
        entity.Fields.Should().ContainKey("speed");
        entity.Fields.Should().ContainKey("is_active");

        entity.GetField("road_id").DataType.Should().Be(QueryDataType.Int64);
        entity.GetField("geom").IsGeometry.Should().BeTrue();
        entity.GetField("name").DataType.Should().Be(QueryDataType.String);
        entity.GetField("speed").DataType.Should().Be(QueryDataType.Double);
        entity.GetField("is_active").DataType.Should().Be(QueryDataType.Boolean);
    }

    [Fact]
    public void Build_ShouldDefaultUnknownTypesToString()
    {
        var layer = new LayerDefinition
        {
            Id = "assets",
            ServiceId = "assets",
            Title = "Assets",
            GeometryType = "Point",
            IdField = "asset_id",
            GeometryField = "shape",
            Fields = new[]
            {
                new FieldDefinition { Name = "asset_id", Nullable = false },
                new FieldDefinition { Name = "payload", DataType = "json", Nullable = true },
                new FieldDefinition { Name = "notes", DataType = null, Nullable = true }
            }
        };

        var entity = _builder.Build(CreateSnapshot(layer), CreateService(layer), layer);

        entity.GetField("asset_id").DataType.Should().Be(QueryDataType.Int64);
        entity.GetField("payload").DataType.Should().Be(QueryDataType.Json);
        entity.GetField("notes").DataType.Should().Be(QueryDataType.String);
    }

    private static MetadataSnapshot CreateSnapshot(LayerDefinition layer)
    {
        return new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog" },
            new[] { new FolderDefinition { Id = "root", Title = "Root" } },
            new[] { new DataSourceDefinition { Id = "stub", Provider = "stub", ConnectionString = "stub" } },
            new[] { CreateService(layer) },
            new[] { layer });
    }

    private static ServiceDefinition CreateService(LayerDefinition layer)
    {
        return new ServiceDefinition
        {
            Id = layer.ServiceId,
            Title = layer.ServiceId,
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "stub",
            Enabled = true
        };
    }
}

