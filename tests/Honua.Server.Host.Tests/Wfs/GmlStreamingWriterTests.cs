using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Serialization;
using Honua.Server.Host.Ogc;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

[Trait("Category", "Unit")]
[Trait("Feature", "WFS")]
[Trait("Speed", "Fast")]
public class GmlStreamingWriterTests
{
    [Fact]
    public async Task WriteCollectionAsync_WithLockMetadata_WritesExpectedEnvelopeAndAttributes()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "parcels",
            ServiceId = "svc",
            Title = "Parcels",
            GeometryType = "esriGeometryPoint",
            IdField = "OBJECTID",
            GeometryField = "Shape",
            Fields = new[]
            {
                new FieldDefinition { Name = "OBJECTID", DataType = "esriFieldTypeInteger" },
                new FieldDefinition { Name = "Name", DataType = "esriFieldTypeString" },
                new FieldDefinition { Name = "Shape", DataType = "esriFieldTypeGeometry" }
            }
        };

        var first = CreateFeature(1, "Alpha", 10, 20, srid: 3857);
        var second = CreateFeature(2, "Beta", 11.5, 21.25, srid: 3857);

        var context = new StreamingWriterContext
        {
            ServiceId = layer.ServiceId,
            TargetWkid = 3857,
            ReturnGeometry = true,
            TotalCount = 5,
            ExpectedFeatureCount = 2,
            Options = new Dictionary<string, object?>
            {
                ["lockId"] = "LOCK-123"
            }
        };

        var stream = new MemoryStream();
        var writer = new GmlStreamingWriter(NullLogger<GmlStreamingWriter>.Instance);

        // Act
        await writer.WriteCollectionAsync(
            stream,
            ToAsyncEnumerable(first, second),
            layer,
            context);

        stream.Position = 0;
        var gml = Encoding.UTF8.GetString(stream.ToArray());

        // Assert
        gml.Should().Contain("numberMatched=\"5\"");
        gml.Should().Contain("numberReturned=\"2\"");
        gml.Should().Contain("lockId=\"LOCK-123\"");
        gml.Should().Contain("<gml:boundedBy>");
        gml.Should().Contain("urn:ogc:def:crs:EPSG::3857");
        gml.Should().Contain("<gml:lowerCorner>10 20</gml:lowerCorner>");
        gml.Should().Contain("<gml:upperCorner>11.5 21.25</gml:upperCorner>");
        gml.Should().Contain("gml:id=\"parcels.1\"");
        gml.Should().Contain("gml:id=\"parcels.2\"");
        gml.Should().Contain("<tns:Name>Alpha</tns:Name>");
        gml.Should().Contain("<tns:Name>Beta</tns:Name>");
    }

    private static FeatureRecord CreateFeature(int id, string name, double x, double y, int srid)
    {
        var geometry = new Point(x, y) { SRID = srid };
        return new FeatureRecord(new Dictionary<string, object?>
        {
            ["OBJECTID"] = id,
            ["Name"] = name,
            ["Shape"] = geometry
        });
    }

    private static async IAsyncEnumerable<FeatureRecord> ToAsyncEnumerable(params FeatureRecord[] records)
    {
        foreach (var record in records)
        {
            yield return record;
            await Task.Yield();
        }
    }
}
