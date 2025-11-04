using System.ComponentModel.DataAnnotations;
using Honua.Server.Host.VectorTiles;
using Honua.Server.Host.Raster;
using Honua.Server.Core.Import;
using Xunit;

namespace Honua.Server.Host.Tests.Validation;

[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class RequestValidationTests
{
    [Fact]
    public void VectorTilePreseedRequest_ValidRequest_PassesValidation()
    {
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 10,
            Datetime = "2024-01-01T00:00:00Z",
            Overwrite = false
        };

        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void VectorTilePreseedRequest_MissingServiceId_FailsValidation()
    {
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 10
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("ServiceId"));
    }

    [Fact]
    public void VectorTilePreseedRequest_InvalidServiceIdCharacters_FailsValidation()
    {
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "invalid service@name",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 10
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("ServiceId"));
    }

    [Fact]
    public void VectorTilePreseedRequest_MinZoomGreaterThanMaxZoom_FailsValidation()
    {
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 15,
            MaxZoom = 10
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r =>
            r.MemberNames.Contains("MinZoom") || r.MemberNames.Contains("MaxZoom"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(23)]
    [InlineData(100)]
    public void VectorTilePreseedRequest_InvalidZoomLevel_FailsValidation(int invalidZoom)
    {
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = invalidZoom,
            MaxZoom = 10
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void VectorTilePreseedRequest_InvalidDatetime_FailsValidation()
    {
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 10,
            Datetime = "not a valid date"
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("Datetime"));
    }

    [Fact]
    public void RasterTilePreseedRequest_ValidRequest_PassesValidation()
    {
        var request = new RasterTilePreseedRequest(new[] { "dataset1", "dataset2" })
        {
            MinZoom = 0,
            MaxZoom = 10,
            Format = "image/png",
            TileSize = 256
        };

        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void RasterTilePreseedRequest_InvalidFormat_FailsValidation()
    {
        var request = new RasterTilePreseedRequest(new[] { "dataset1" })
        {
            Format = "image/gif" // Not in allowed list
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("Format"));
    }

    [Theory]
    [InlineData(100)] // Not a standard size
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8192)] // Too large
    public void RasterTilePreseedRequest_InvalidTileSize_FailsValidation(int invalidSize)
    {
        var request = new RasterTilePreseedRequest(new[] { "dataset1" })
        {
            TileSize = invalidSize
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("TileSize"));
    }

    [Fact]
    public void DataIngestionRequest_ValidRequest_PassesValidation()
    {
        var request = new DataIngestionRequest(
            ServiceId: "test-service",
            LayerId: "test-layer",
            SourcePath: "/tmp/test.geojson",
            WorkingDirectory: "/tmp/work",
            SourceFileName: "test.geojson",
            ContentType: "application/geo+json",
            Overwrite: false
        );

        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void DataIngestionRequest_InvalidServiceId_FailsValidation()
    {
        var request = new DataIngestionRequest(
            ServiceId: "invalid service!",
            LayerId: "test-layer",
            SourcePath: "/tmp/test.geojson",
            WorkingDirectory: "/tmp/work",
            SourceFileName: null,
            ContentType: null,
            Overwrite: false
        );

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("ServiceId"));
    }

    [Fact]
    public void DataIngestionRequest_ExcessivelyLongPath_FailsValidation()
    {
        var longPath = new string('a', 5000); // 5000 chars - exceeds 4096 limit

        var request = new DataIngestionRequest(
            ServiceId: "test-service",
            LayerId: "test-layer",
            SourcePath: longPath,
            WorkingDirectory: "/tmp/work",
            SourceFileName: null,
            ContentType: null,
            Overwrite: false
        );

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("SourcePath"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DataIngestionRequest_MissingRequiredField_FailsValidation(string? invalidValue)
    {
        // ServiceId is required
        var request = new DataIngestionRequest(
            ServiceId: invalidValue ?? "",
            LayerId: "test-layer",
            SourcePath: "/tmp/test.geojson",
            WorkingDirectory: "/tmp/work",
            SourceFileName: null,
            ContentType: null,
            Overwrite: false
        );

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }
}
