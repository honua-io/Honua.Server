using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Query;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class ODataFilterParserTests
{
    private readonly QueryEntityDefinition _entityDefinition;

    public ODataFilterParserTests()
    {
        var service = new ServiceDefinition
        {
            Id = "roads",
            Title = "Roads",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "stub",
            Enabled = true
        };

        var layer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = "roads",
            Title = "Roads",
            GeometryType = "LineString",
            IdField = "road_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int64", Nullable = false },
                new FieldDefinition { Name = "status", DataType = "string", Nullable = true },
                new FieldDefinition { Name = "speed", DataType = "double", Nullable = true },
                new FieldDefinition { Name = "location", DataType = "geometry", Nullable = true }
            }
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog" },
            new[] { new FolderDefinition { Id = "root" } },
            new[] { new DataSourceDefinition { Id = "stub", Provider = "stub", ConnectionString = "stub" } },
            new[] { service },
            new[] { layer });

        var builder = new MetadataQueryModelBuilder();
        var entityDef = builder.Build(snapshot, snapshot.Services.Single(), snapshot.Layers.Single());

        // Add location field to entity definition for spatial tests
        var fields = new Dictionary<string, QueryFieldDefinition>(entityDef.Fields, StringComparer.OrdinalIgnoreCase);
        fields["location"] = new QueryFieldDefinition
        {
            Name = "location",
            DataType = QueryDataType.Geometry,
            Nullable = true,
            IsGeometry = true,
            IsKey = false
        };
        _entityDefinition = new QueryEntityDefinition(entityDef.Id, entityDef.Name, fields);
    }

    [Fact]
    public void Parse_ShouldTranslateEqualityFilter()
    {
        var clause = ParseFilter("status eq 'open'");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Operator.Should().Be(QueryBinaryOperator.Equal);
        ((QueryFieldReference)binary.Left).Name.Should().Be("status");
        ((QueryConstant)binary.Right).Value.Should().Be("open");

        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        sql.Should().Be("t.\"status\" = @filter_0");
        parameters.Should().ContainKey("filter_0").WhoseValue.Should().Be("open");
    }

    [Fact]
    public void Parse_ShouldTranslateGreaterThanFilter()
    {
        var clause = ParseFilter("speed gt 25");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        binary.Operator.Should().Be(QueryBinaryOperator.GreaterThan);

        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        sql.Should().Be("t.\"speed\" > @filter_0");
        parameters.Should().ContainKey("filter_0").WhoseValue.Should().Be(25d);
    }

    [Fact]
    public void Parse_ShouldTranslateCompositeFilters()
    {
        var clause = ParseFilter("status eq null or speed lt 20");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        filter.Expression.Should().BeOfType<QueryBinaryExpression>();

        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        sql.Should().Be("(t.\"status\" IS NULL) OR (t.\"speed\" < @filter_0)");
        parameters.Should().ContainKey("filter_0").WhoseValue.Should().Be(20d);
    }



    [Fact]
    public void Parse_ShouldTranslateNotExpressions()
    {
        var clause = ParseFilter("not (status eq 'open')");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        filter.Expression.Should().BeOfType<QueryUnaryExpression>();

        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        sql.Should().Be("NOT (t.\"status\" = @filter_0)");
        parameters.Should().ContainKey("filter_0").WhoseValue.Should().Be("open");
    }

    [Fact]
    public void Parse_ShouldTranslateGeoDistanceFunction()
    {
        var clause = ParseFilter("geo.distance(location, geography'POINT(-122.33 47.61)') lt 1000");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Verify the filter expression structure
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Operator.Should().Be(QueryBinaryOperator.LessThan);

        // Left side should be the geo.distance function
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("geo.distance");
        function.Arguments.Should().HaveCount(2);

        // First argument should be field reference
        function.Arguments[0].Should().BeOfType<QueryFieldReference>();
        ((QueryFieldReference)function.Arguments[0]).Name.Should().Be("location");

        // Second argument should be geometry constant
        function.Arguments[1].Should().BeOfType<QueryConstant>();
        var constant = (QueryConstant)function.Arguments[1];
        constant.Value.Should().BeOfType<QueryGeometryValue>();

        // Right side should be numeric constant
        binary.Right.Should().BeOfType<QueryConstant>();
        ((QueryConstant)binary.Right).Value.Should().Be(1000d);
    }

    [Fact]
    public void Parse_ShouldTranslateGeoLengthFunction()
    {
        var clause = ParseFilter("geo.length(geom) gt 5000");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Verify the filter expression structure
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Operator.Should().Be(QueryBinaryOperator.GreaterThan);

        // Left side should be the geo.length function
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("geo.length");
        function.Arguments.Should().HaveCount(1);

        // Argument should be field reference
        function.Arguments[0].Should().BeOfType<QueryFieldReference>();
        ((QueryFieldReference)function.Arguments[0]).Name.Should().Be("geom");

        // Right side should be numeric constant
        binary.Right.Should().BeOfType<QueryConstant>();
        ((QueryConstant)binary.Right).Value.Should().Be(5000d);
    }

    [Fact]
    public void Parse_ShouldTranslateGeoIntersectsFunction()
    {
        var clause = ParseFilter("geo.intersects(location, geography'POLYGON((-122.5 47.5, -122.0 47.5, -122.0 48.0, -122.5 48.0, -122.5 47.5))')");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Verify the filter expression structure
        filter.Expression.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)filter.Expression!;
        function.Name.Should().Be("geo.intersects");
        function.Arguments.Should().HaveCount(2);

        // First argument should be field reference
        function.Arguments[0].Should().BeOfType<QueryFieldReference>();
        ((QueryFieldReference)function.Arguments[0]).Name.Should().Be("location");

        // Second argument should be geometry constant
        function.Arguments[1].Should().BeOfType<QueryConstant>();
        var constant = (QueryConstant)function.Arguments[1];
        constant.Value.Should().BeOfType<QueryGeometryValue>();
    }

    [Fact]
    public void Parse_ShouldCaptureGeometrySrid()
    {
        var clause = ParseFilter("geo.distance(location, geography'SRID=4326;POINT(-122 47)') lt 1000");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        var function = Assert.IsType<QueryFunctionExpression>(binary.Left);
        var geometryConstant = Assert.IsType<QueryConstant>(function.Arguments[1]);
        var geometry = Assert.IsType<QueryGeometryValue>(geometryConstant.Value);

        geometry.Srid.Should().Be(4326);
        geometry.WellKnownText.Should().Be("POINT (-122 47)");
    }

    private static FilterClause ParseFilter(string filter)
    {
        var model = BuildModel();
        var parser = new ODataQueryOptionParser(
            model,
            (IEdmStructuredType)model.FindType("Queries.Road"),
            (IEdmNavigationSource)model.EntityContainer.FindEntitySet("roads"),
            new Dictionary<string, string> { ["$filter"] = filter });
        return parser.ParseFilter();
    }

    private static IEdmModel BuildModel()
    {
        var model = new EdmModel();
        var entityType = new EdmEntityType("Queries", "Road");
        entityType.AddStructuralProperty("road_id", EdmPrimitiveTypeKind.Int64);
        entityType.AddStructuralProperty("status", EdmPrimitiveTypeKind.String);
        entityType.AddStructuralProperty("speed", EdmPrimitiveTypeKind.Double);
        entityType.AddStructuralProperty("geom", EdmCoreModel.Instance.GetSpatial(EdmPrimitiveTypeKind.GeometryLineString, isNullable: true));
        entityType.AddStructuralProperty("location", EdmCoreModel.Instance.GetSpatial(EdmPrimitiveTypeKind.GeographyPoint, isNullable: true));
        model.AddElement(entityType);

        var container = new EdmEntityContainer("Queries", "Container");
        model.AddElement(container);

        container.AddEntitySet("roads", entityType);
        return model;
    }
}



