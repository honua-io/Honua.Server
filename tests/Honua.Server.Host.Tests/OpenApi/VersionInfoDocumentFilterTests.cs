using Honua.Server.Host.OpenApi.Filters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Honua.Server.Host.Tests.OpenApi;

/// <summary>
/// Unit tests for <see cref="VersionInfoDocumentFilter"/>.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public class VersionInfoDocumentFilterTests
{
    [Fact]
    public void Apply_WithEnvironmentName_AddsEnvironmentToDescription()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Development");
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
        Assert.Contains("Environment: Development", document.Info.Description);
    }

    [Fact]
    public void Apply_WithBuildVersion_AddsBuildVersionToDescription()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Production", buildVersion: "1.2.3");
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
        Assert.Contains("Build: 1.2.3", document.Info.Description);
    }

    [Fact]
    public void Apply_WithBuildDate_AddsBuildDateToDescription()
    {
        // Arrange
        var buildDate = "2025-10-18";
        var filter = new VersionInfoDocumentFilter("Production", buildDate: buildDate);
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
        Assert.Contains($"Build Date: {buildDate}", document.Info.Description);
    }

    [Fact]
    public void Apply_AddsRuntimeVersionToDescription()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Production");
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
        Assert.Contains("Runtime: .NET", document.Info.Description);
    }

    [Fact]
    public void Apply_AddsCustomExtensions()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Staging", buildVersion: "2.0.0", buildDate: "2025-10-18");
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.True(document.Extensions.ContainsKey("x-api-version"));
        Assert.True(document.Extensions.ContainsKey("x-environment"));
        Assert.True(document.Extensions.ContainsKey("x-build-date"));
    }

    [Fact]
    public void Apply_WithoutBuildDate_DoesNotAddBuildDateExtension()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Production");
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.False(document.Extensions.ContainsKey("x-build-date"));
    }

    [Fact]
    public void Apply_WithNullEnvironment_UsesUnknown()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter(null!);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Environment: Unknown", document.Info.Description);
    }

    [Fact]
    public void Apply_CreatesInfoIfNull()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Development");
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
        var filter = new VersionInfoDocumentFilter("Production");
        var existingDescription = "Existing API description";
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
        Assert.Contains("Version Information:", document.Info.Description);
    }

    [Fact]
    public void Apply_UpdatesVersionFromBuildVersion()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Production", buildVersion: "2.5.1");
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Equal("2.5.1", document.Info.Version);
    }

    [Fact]
    public void Apply_AddsAssemblyVersionExtension()
    {
        // Arrange
        var filter = new VersionInfoDocumentFilter("Production");
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.True(document.Extensions.ContainsKey("x-assembly-version"));
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
