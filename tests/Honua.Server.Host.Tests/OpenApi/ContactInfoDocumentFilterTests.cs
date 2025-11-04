using Honua.Server.Host.OpenApi.Filters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Honua.Server.Host.Tests.OpenApi;

/// <summary>
/// Unit tests for <see cref="ContactInfoDocumentFilter"/>.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public class ContactInfoDocumentFilterTests
{
    [Fact]
    public void Apply_WithDefaultOptions_DoesNotThrow()
    {
        // Arrange
        var filter = new ContactInfoDocumentFilter();
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
    public void Apply_WithContactInfo_SetsContactDetails()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            ContactName = "API Support Team",
            ContactEmail = "support@example.com",
            ContactUrl = new Uri("https://example.com/contact")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.NotNull(document.Info.Contact);
        Assert.Equal("API Support Team", document.Info.Contact.Name);
        Assert.Equal("support@example.com", document.Info.Contact.Email);
        Assert.Equal(new Uri("https://example.com/contact"), document.Info.Contact.Url);
    }

    [Fact]
    public void Apply_WithLicenseInfo_SetsLicenseDetails()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            LicenseName = "Apache 2.0",
            LicenseUrl = new Uri("https://www.apache.org/licenses/LICENSE-2.0")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.NotNull(document.Info.License);
        Assert.Equal("Apache 2.0", document.Info.License.Name);
        Assert.Equal(new Uri("https://www.apache.org/licenses/LICENSE-2.0"), document.Info.License.Url);
    }

    [Fact]
    public void Apply_WithTermsOfService_SetsTermsUrl()
    {
        // Arrange
        var termsUrl = new Uri("https://example.com/terms");
        var options = new ContactInfoOptions
        {
            TermsOfServiceUrl = termsUrl
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Equal(termsUrl, document.Info.TermsOfService);
    }

    [Fact]
    public void Apply_WithExternalDocs_SetsExternalDocumentation()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            ExternalDocsDescription = "Full API Documentation",
            ExternalDocsUrl = new Uri("https://docs.example.com")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.NotNull(document.ExternalDocs);
        Assert.Equal("Full API Documentation", document.ExternalDocs.Description);
        Assert.Equal(new Uri("https://docs.example.com"), document.ExternalDocs.Url);
    }

    [Fact]
    public void Apply_WithSupportEmail_AddsToDescription()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            SupportEmail = "help@example.com"
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Version = "1.0",
                Description = "Original description"
            }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Support Email:", document.Info.Description);
        Assert.Contains("help@example.com", document.Info.Description);
    }

    [Fact]
    public void Apply_WithSupportUrl_AddsToDescription()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            SupportUrl = new Uri("https://support.example.com")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Support Portal:", document.Info.Description);
    }

    [Fact]
    public void Apply_WithDocumentationUrl_AddsToDescription()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            DocumentationUrl = new Uri("https://docs.example.com")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Documentation:", document.Info.Description);
    }

    [Fact]
    public void Apply_WithIssueTrackerUrl_AddsToDescription()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            IssueTrackerUrl = new Uri("https://github.com/example/issues")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Issue Tracker:", document.Info.Description);
    }

    [Fact]
    public void Apply_WithAllSupportInfo_AddsAllToDescription()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            SupportEmail = "help@example.com",
            SupportUrl = new Uri("https://support.example.com"),
            DocumentationUrl = new Uri("https://docs.example.com"),
            IssueTrackerUrl = new Uri("https://github.com/example/issues")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Contains("Support & Resources:", document.Info.Description);
        Assert.Contains("Support Email:", document.Info.Description);
        Assert.Contains("Support Portal:", document.Info.Description);
        Assert.Contains("Documentation:", document.Info.Description);
        Assert.Contains("Issue Tracker:", document.Info.Description);
    }

    [Fact]
    public void Apply_WithSupportEmail_AddsExtension()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            SupportEmail = "support@example.com"
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.True(document.Extensions.ContainsKey("x-support-email"));
    }

    [Fact]
    public void Apply_WithSupportUrl_AddsExtension()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            SupportUrl = new Uri("https://support.example.com")
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.True(document.Extensions.ContainsKey("x-support-url"));
    }

    [Fact]
    public void Apply_WithApiStatus_AddsExtension()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            ApiStatus = "stable"
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Version = "1.0" }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.True(document.Extensions.ContainsKey("x-api-status"));
    }

    [Fact]
    public void Apply_CreatesInfoIfNull()
    {
        // Arrange
        var filter = new ContactInfoDocumentFilter();
        var document = new OpenApiDocument();
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.NotNull(document.Info);
    }

    [Fact]
    public void Apply_PreservesExistingContactInfo()
    {
        // Arrange
        var options = new ContactInfoOptions
        {
            ContactName = "New Team"
        };
        var filter = new ContactInfoDocumentFilter(options);
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Version = "1.0",
                Contact = new OpenApiContact
                {
                    Email = "existing@example.com"
                }
            }
        };
        var context = CreateDocumentFilterContext();

        // Act
        filter.Apply(document, context);

        // Assert
        Assert.Equal("New Team", document.Info.Contact.Name);
        Assert.Equal("existing@example.com", document.Info.Contact.Email);
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
