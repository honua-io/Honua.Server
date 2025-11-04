using Honua.Server.Host.OpenApi.Filters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Honua.Server.Host.Tests.OpenApi;

/// <summary>
/// Unit tests for <see cref="DeprecationInfoDocumentFilter"/>.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public class DeprecationInfoDocumentFilterTests
{
    [Fact]
    public void Apply_WithDefaultOptions_DoesNotThrow()
    {
        // Arrange
        var filter = new DeprecationInfoDocumentFilter();
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act & Assert
        var exception = Record.Exception(() => filter.Apply(document, context));
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WhenDeprecated_AddsDeprecationNotice()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = true
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Version = "1.0",
                Description = "Test API"
            }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("DEPRECATION NOTICE", document.Info.Description);
        Assert.True(document.Extensions.ContainsKey("x-deprecated"));
    }

    [Fact]
    public void Apply_WithSunsetDate_AddsSunsetDateToDescription()
    {
        // Arrange
        var sunsetDate = DateTimeOffset.Parse("2026-12-31");
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = true,
            SunsetDate = sunsetDate
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Sunset Date: 2026-12-31", document.Info.Description);
        Assert.True(document.Extensions.ContainsKey("x-sunset-date"));
    }

    [Fact]
    public void Apply_WithReplacementVersion_AddsReplacementInfo()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = true,
            ReplacementVersion = "v2.0"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Replacement Version: v2.0", document.Info.Description);
        Assert.True(document.Extensions.ContainsKey("x-replacement-version"));
    }

    [Fact]
    public void Apply_WithMigrationGuideUrl_AddsMigrationGuideLink()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = true,
            MigrationGuideUrl = "https://docs.example.com/migration"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Migration Guide:", document.Info.Description);
        Assert.Contains("https://docs.example.com/migration", document.Info.Description);
    }

    [Fact]
    public void Apply_WithDeprecationReason_AddsReason()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = true,
            DeprecationReason = "Security vulnerabilities in legacy authentication"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Reason:", document.Info.Description);
        Assert.Contains("Security vulnerabilities", document.Info.Description);
    }

    [Fact]
    public void Apply_WhenNotDeprecated_WithStability_AddsStabilityInfo()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = false,
            Stability = "beta"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("API Stability: beta", document.Info.Description);
        Assert.True(document.Extensions.ContainsKey("x-stability"));
    }

    [Fact]
    public void Apply_WithChangelogUrl_AddsChangelogLink()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            ChangelogUrl = "https://github.com/example/CHANGELOG.md"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Changelog:", document.Info.Description);
        Assert.True(document.Extensions.ContainsKey("x-changelog"));
    }

    [Fact]
    public void Apply_WithAllDeprecationInfo_AddsAllDetails()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = true,
            SunsetDate = DateTimeOffset.Parse("2026-12-31"),
            ReplacementVersion = "v3.0",
            MigrationGuideUrl = "https://docs.example.com/migration",
            DeprecationReason = "Major breaking changes",
            ChangelogUrl = "https://github.com/example/CHANGELOG.md"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("DEPRECATION NOTICE", document.Info.Description);
        Assert.Contains("Sunset Date: 2026-12-31", document.Info.Description);
        Assert.Contains("Replacement Version: v3.0", document.Info.Description);
        Assert.Contains("Migration Guide:", document.Info.Description);
        Assert.Contains("Reason: Major breaking changes", document.Info.Description);
        Assert.Contains("Changelog:", document.Info.Description);

        Assert.True(document.Extensions.ContainsKey("x-deprecated"));
        Assert.True(document.Extensions.ContainsKey("x-sunset-date"));
        Assert.True(document.Extensions.ContainsKey("x-replacement-version"));
        Assert.True(document.Extensions.ContainsKey("x-changelog"));
    }

    [Fact]
    public void Apply_WhenNotDeprecated_DoesNotAddDeprecationExtensions()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = false,
            Stability = "stable"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.False(document.Extensions.ContainsKey("x-deprecated"));
        Assert.False(document.Extensions.ContainsKey("x-sunset-date"));
        Assert.False(document.Extensions.ContainsKey("x-replacement-version"));
    }

    [Fact]
    public void Apply_CreatesInfoIfNull()
    {
        // Arrange
        var filter = new DeprecationInfoDocumentFilter();
        var document = new OpenApiDocument();
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.NotNull(document.Info);
    }

    [Fact]
    public void Apply_PreservesExistingDescription()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            IsDeprecated = true
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var existingDescription = "Original API description";
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Version = "1.0",
                Description = existingDescription
            }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains(existingDescription, document.Info.Description);
        Assert.Contains("DEPRECATION NOTICE", document.Info.Description);
    }

    [Fact]
    public void Apply_WithStableApi_AddsStabilityExtension()
    {
        // Arrange
        var options = new DeprecationInfoOptions
        {
            Stability = "stable"
        };
        var filter = new DeprecationInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.True(document.Extensions.ContainsKey("x-stability"));
    }

    // Helper methods
    private static DocumentFilterContext CreateDocumentFilterContext()
    {
        var schemaRepository = new SchemaRepository();
        var schemaGenerator = new SchemaGenerator(new SchemaGeneratorOptions(), new Moq.Mock<ISerializerDataContractResolver>().Object);
        var apiDescriptions = new List<Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription>();

        return new DocumentFilterContext(
            apiDescriptions,
            schemaGenerator,
            schemaRepository);
    }
}
