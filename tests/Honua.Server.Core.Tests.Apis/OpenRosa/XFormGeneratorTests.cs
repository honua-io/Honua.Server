using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.OpenRosa;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.OpenRosa;

[Trait("Category", "Unit")]
public class XFormGeneratorTests
{
    private static readonly XNamespace H = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace XF = "http://www.w3.org/2002/xforms";
    private static readonly XNamespace ODK = "http://www.opendatakit.org/xforms";

    private readonly XFormGenerator _generator = new();

    [Fact]
    public void Generate_ShouldCreateValidXFormForPointLayer()
    {
        // Arrange
        var layer = CreatePointLayer();
        var baseUrl = "https://honua.example.com";

        // Act
        var xform = _generator.Generate(layer, baseUrl);

        // Assert
        xform.Should().NotBeNull();
        xform.FormId.Should().Be("tree_survey_v1");
        xform.Version.Should().Be("1.0.0");
        xform.Xml.Should().NotBeNull();
        xform.Hash.Should().NotBeNullOrEmpty();

        var doc = xform.Xml;
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(H + "html");
    }

    [Fact]
    public void Generate_ShouldIncludeModelWithInstance()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var model = xform.Xml.Descendants(XF + "model").FirstOrDefault();

        // Assert
        model.Should().NotBeNull();
        var instance = model!.Element(XF + "instance");
        instance.Should().NotBeNull();

        var data = instance!.Descendants("data").FirstOrDefault();
        data.Should().NotBeNull();
        data!.Attribute("id")?.Value.Should().Be("tree_survey_v1");
    }

    [Fact]
    public void Generate_ShouldIncludeGeometryField()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var instance = xform.Xml.Descendants(XF + "instance").FirstOrDefault();
        var geometryElement = instance!.Descendants("geometry").FirstOrDefault();

        // Assert
        geometryElement.Should().NotBeNull();
    }

    [Fact]
    public void Generate_ShouldIncludeAllFields()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var instance = xform.Xml.Descendants(XF + "instance").FirstOrDefault();
        var data = instance!.Descendants("data").FirstOrDefault();

        // Assert
        data!.Element("species").Should().NotBeNull();
        data.Element("dbh_cm").Should().NotBeNull();
        data.Element("health").Should().NotBeNull();
    }

    [Fact]
    public void Generate_ShouldCreateGeopointInputForPointLayer()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var body = xform.Xml.Descendants(H + "body").FirstOrDefault();
        var geopointInput = body!.Descendants(H + "input")
            .FirstOrDefault(e => e.Attribute("ref")?.Value == "/data/geometry");

        // Assert
        geopointInput.Should().NotBeNull();
        var bind = xform.Xml.Descendants(XF + "bind")
            .FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/geometry");
        bind.Should().NotBeNull();
        bind!.Attribute("type")?.Value.Should().Be("geopoint");
    }

    [Fact]
    public void Generate_ShouldCreateGeoshapeInputForPolygonLayer()
    {
        // Arrange
        var layer = CreatePolygonLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var body = xform.Xml.Descendants(H + "body").FirstOrDefault();
        var geoshapeInput = body!.Descendants(H + "input")
            .FirstOrDefault(e => e.Attribute("ref")?.Value == "/data/boundary");

        // Assert
        geoshapeInput.Should().NotBeNull();
        var bind = xform.Xml.Descendants(XF + "bind")
            .FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/boundary");
        bind.Should().NotBeNull();
        bind!.Attribute("type")?.Value.Should().Be("geoshape");
    }

    [Fact]
    public void Generate_ShouldCreateGeotraceInputForLineStringLayer()
    {
        // Arrange
        var layer = CreateLineStringLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var bind = xform.Xml.Descendants(XF + "bind")
            .FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/path");

        // Assert
        bind.Should().NotBeNull();
        bind!.Attribute("type")?.Value.Should().Be("geotrace");
    }

    [Fact]
    public void Generate_ShouldApplyFieldMappingLabels()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var body = xform.Xml.Descendants(H + "body").FirstOrDefault();
        var speciesInput = body!.Descendants(H + "input")
            .FirstOrDefault(e => e.Attribute("ref")?.Value == "/data/species");

        // Assert
        speciesInput.Should().NotBeNull();
        var label = speciesInput!.Element(H + "label");
        label.Should().NotBeNull();
        label!.Value.Should().Be("Tree Species");
    }

    [Fact]
    public void Generate_ShouldApplyFieldMappingHints()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var body = xform.Xml.Descendants(H + "body").FirstOrDefault();
        var speciesInput = body!.Descendants(H + "input")
            .FirstOrDefault(e => e.Attribute("ref")?.Value == "/data/species");

        // Assert
        speciesInput.Should().NotBeNull();
        var hint = speciesInput!.Element(H + "hint");
        hint.Should().NotBeNull();
        hint!.Value.Should().Be("Use scientific name if known");
    }

    [Fact]
    public void Generate_ShouldApplyRequiredConstraint()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var bind = xform.Xml.Descendants(XF + "bind")
            .FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/species");

        // Assert
        bind.Should().NotBeNull();
        bind!.Attribute("required")?.Value.Should().Be("true()");
    }

    [Fact]
    public void Generate_ShouldApplyCustomConstraint()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var bind = xform.Xml.Descendants(XF + "bind")
            .FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/dbh_cm");

        // Assert
        bind.Should().NotBeNull();
        bind!.Attribute("constraint")?.Value.Should().Be(". >= 0 and . <= 500");
        bind.Attribute(ODK + "constraintMsg")?.Value.Should().Be("DBH must be between 0-500cm");
    }

    [Fact]
    public void Generate_ShouldCreateSelectOneForChoices()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var body = xform.Xml.Descendants(H + "body").FirstOrDefault();
        var selectOne = body!.Descendants(H + "select1")
            .FirstOrDefault(e => e.Attribute("ref")?.Value == "/data/health");

        // Assert
        selectOne.Should().NotBeNull();
        selectOne!.Attribute("appearance")?.Value.Should().Be("minimal");

        var items = selectOne.Elements(H + "item").ToList();
        items.Should().HaveCount(5);
        items[0].Element(H + "label")?.Value.Should().Be("Excellent");
        items[0].Element(H + "value")?.Value.Should().Be("excellent");
    }

    [Fact]
    public void Generate_ShouldIncludeMetadata()
    {
        // Arrange
        var layer = CreatePointLayer();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var instance = xform.Xml.Descendants(XF + "instance").FirstOrDefault();
        var meta = instance!.Descendants("meta").FirstOrDefault();

        // Assert
        meta.Should().NotBeNull();
        meta!.Element("instanceID").Should().NotBeNull();
    }

    [Fact]
    public void Generate_ShouldSetCorrectInputTypesForDataTypes()
    {
        // Arrange
        var layer = CreateLayerWithVariousDataTypes();

        // Act
        var xform = _generator.Generate(layer, "https://test.com");
        var binds = xform.Xml.Descendants(XF + "bind").ToList();

        // Assert
        binds.FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/text_field")
            ?.Attribute("type")?.Value.Should().Be("string");

        binds.FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/int_field")
            ?.Attribute("type")?.Value.Should().Be("int");

        binds.FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/double_field")
            ?.Attribute("type")?.Value.Should().Be("decimal");

        binds.FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/date_field")
            ?.Attribute("type")?.Value.Should().Be("date");

        binds.FirstOrDefault(b => b.Attribute("nodeset")?.Value == "/data/datetime_field")
            ?.Attribute("type")?.Value.Should().Be("dateTime");
    }

    private static LayerDefinition CreatePointLayer()
    {
        return new LayerDefinition
        {
            Id = "tree-inventory",
            ServiceId = "field-surveys",
            Title = "Urban Tree Survey",
            GeometryType = "Point",
            GeometryField = "geometry",
            IdField = "tree_id",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "tree_id", DataType = "string" },
                new() { Name = "species", DataType = "string" },
                new() { Name = "dbh_cm", DataType = "int" },
                new() { Name = "health", DataType = "string" }
            },
            OpenRosa = new OpenRosaLayerDefinition
            {
                Enabled = true,
                Mode = "direct",
                FormId = "tree_survey_v1",
                FormTitle = "Tree Inventory Form",
                FormVersion = "1.0.0",
                FieldMappings = new Dictionary<string, OpenRosaFieldMappingDefinition>
                {
                    ["species"] = new()
                    {
                        Label = "Tree Species",
                        Hint = "Use scientific name if known",
                        Required = true
                    },
                    ["dbh_cm"] = new()
                    {
                        Label = "Diameter at Breast Height (cm)",
                        Hint = "Measure at 1.3m from ground",
                        Constraint = ". >= 0 and . <= 500",
                        ConstraintMessage = "DBH must be between 0-500cm"
                    },
                    ["health"] = new()
                    {
                        Label = "Tree Health",
                        Appearance = "minimal",
                        Choices = new Dictionary<string, string>
                        {
                            ["excellent"] = "Excellent",
                            ["good"] = "Good",
                            ["fair"] = "Fair",
                            ["poor"] = "Poor",
                            ["dead"] = "Dead"
                        }
                    }
                }
            }
        };
    }

    private static LayerDefinition CreatePolygonLayer()
    {
        return new LayerDefinition
        {
            Id = "parcels",
            ServiceId = "cadastre",
            Title = "Land Parcels",
            GeometryType = "Polygon",
            GeometryField = "boundary",
            IdField = "parcel_id",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "parcel_id", DataType = "string" },
                new() { Name = "owner", DataType = "string" }
            },
            OpenRosa = new OpenRosaLayerDefinition
            {
                Enabled = true,
                FormId = "parcel_form",
                FormVersion = "1.0.0"
            }
        };
    }

    private static LayerDefinition CreateLineStringLayer()
    {
        return new LayerDefinition
        {
            Id = "trails",
            ServiceId = "recreation",
            Title = "Hiking Trails",
            GeometryType = "LineString",
            GeometryField = "path",
            IdField = "trail_id",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "trail_id", DataType = "string" },
                new() { Name = "name", DataType = "string" }
            },
            OpenRosa = new OpenRosaLayerDefinition
            {
                Enabled = true,
                FormId = "trail_form",
                FormVersion = "1.0.0"
            }
        };
    }

    private static LayerDefinition CreateLayerWithVariousDataTypes()
    {
        return new LayerDefinition
        {
            Id = "datatypes",
            ServiceId = "test",
            Title = "Data Types Test",
            GeometryType = "Point",
            GeometryField = "location",
            IdField = "id",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "string" },
                new() { Name = "text_field", DataType = "string" },
                new() { Name = "int_field", DataType = "int" },
                new() { Name = "double_field", DataType = "double" },
                new() { Name = "date_field", DataType = "date" },
                new() { Name = "datetime_field", DataType = "datetime" }
            },
            OpenRosa = new OpenRosaLayerDefinition
            {
                Enabled = true,
                FormId = "datatypes_form",
                FormVersion = "1.0.0"
            }
        };
    }
}
