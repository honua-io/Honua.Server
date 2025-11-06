using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Honua.Server.Core.Tests.Shared.TestBases;

/// <summary>
/// Base class for OGC protocol tests (WFS, WMS, WMTS, WCS, etc.)
/// Provides common test patterns to reduce duplication across protocol test suites.
/// </summary>
/// <remarks>
/// <para>
/// This base class consolidates ~400 lines of duplicated test code across
/// WFS, WMS, WMTS, and WCS test suites by providing reusable patterns for:
/// </para>
/// <list type="bullet">
/// <item>GetCapabilities validation</item>
/// <item>Service metadata assertions</item>
/// <item>Operation support verification</item>
/// <item>Exception/error handling</item>
/// <item>XML response parsing</item>
/// </list>
/// </remarks>
public abstract class OgcProtocolTestBase<TFactory> : IClassFixture<TFactory>
    where TFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets the test factory for creating HTTP clients.
    /// </summary>
    protected TFactory Factory { get; }

    /// <summary>
    /// Gets the HTTP client for making requests to the test server.
    /// </summary>
    protected HttpClient Client { get; }

    /// <summary>
    /// Gets the base endpoint for this OGC service (e.g., "/wfs", "/wms").
    /// </summary>
    protected abstract string ServiceEndpoint { get; }

    /// <summary>
    /// Gets the expected service type identifier (e.g., "WFS", "WMS").
    /// </summary>
    protected abstract string ServiceType { get; }

    /// <summary>
    /// Gets the OGC specification version (e.g., "2.0.0", "1.3.0").
    /// </summary>
    protected abstract string ServiceVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OgcProtocolTestBase{TFactory}"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    protected OgcProtocolTestBase(TFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    #region Common GetCapabilities Tests

    /// <summary>
    /// Asserts that GetCapabilities returns a valid response with service identification.
    /// </summary>
    protected async Task AssertValidGetCapabilitiesAsync()
    {
        // Act
        var response = await Client.GetAsync($"{ServiceEndpoint}?service={ServiceType}&request=GetCapabilities");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue("GetCapabilities should succeed");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty("GetCapabilities should return content");

        var xml = XDocument.Parse(content);
        xml.Root.Should().NotBeNull("GetCapabilities should return valid XML");
    }

    /// <summary>
    /// Asserts that the service identification section contains required metadata.
    /// </summary>
    protected void AssertServiceIdentification(XDocument capabilities, string expectedTitle = null)
    {
        var serviceId = capabilities.Root
            ?.Element(XName.Get("ServiceIdentification", "http://www.opengis.net/ows/1.1"));

        serviceId.Should().NotBeNull("GetCapabilities should include ServiceIdentification");

        var title = serviceId?.Element(XName.Get("Title", "http://www.opengis.net/ows/1.1"))?.Value;
        title.Should().NotBeNullOrEmpty("ServiceIdentification should have Title");

        if (expectedTitle != null)
        {
            title.Should().Contain(expectedTitle, "Title should match expected value");
        }

        var serviceType = serviceId?.Element(XName.Get("ServiceType", "http://www.opengis.net/ows/1.1"))?.Value;
        serviceType.Should().Be(ServiceType, $"ServiceType should be {ServiceType}");
    }

    /// <summary>
    /// Asserts that required operations are listed in the capabilities document.
    /// </summary>
    protected void AssertRequiredOperations(XDocument capabilities, params string[] requiredOperations)
    {
        var operationsMetadata = capabilities.Root
            ?.Element(XName.Get("OperationsMetadata", "http://www.opengis.net/ows/1.1"));

        operationsMetadata.Should().NotBeNull("GetCapabilities should include OperationsMetadata");

        foreach (var requiredOp in requiredOperations)
        {
            var operation = operationsMetadata?
                .Elements(XName.Get("Operation", "http://www.opengis.net/ows/1.1"))
                .FirstOrDefault(op => op.Attribute("name")?.Value == requiredOp);

            operation.Should().NotBeNull($"{ServiceType} should support {requiredOp} operation");
        }
    }

    /// <summary>
    /// Asserts that the service provider section contains contact information.
    /// </summary>
    protected void AssertServiceProvider(XDocument capabilities)
    {
        var serviceProvider = capabilities.Root
            ?.Element(XName.Get("ServiceProvider", "http://www.opengis.net/ows/1.1"));

        serviceProvider.Should().NotBeNull("GetCapabilities should include ServiceProvider");

        var providerName = serviceProvider?.Element(XName.Get("ProviderName", "http://www.opengis.net/ows/1.1"))?.Value;
        providerName.Should().NotBeNullOrEmpty("ServiceProvider should have ProviderName");
    }

    #endregion

    #region Common Exception Handling Tests

    /// <summary>
    /// Asserts that an invalid request parameter returns an OGC exception.
    /// </summary>
    protected async Task AssertInvalidParameterReturnsExceptionAsync(
        string parameterName,
        string invalidValue,
        string expectedExceptionCode = "InvalidParameterValue")
    {
        // Act
        var response = await Client.GetAsync(
            $"{ServiceEndpoint}?service={ServiceType}&request=GetCapabilities&{parameterName}={invalidValue}");

        // Assert - OGC services should return 200 with exception XML, not HTTP error codes
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ExceptionReport", "Invalid parameter should return OGC ExceptionReport");
        content.Should().Contain(expectedExceptionCode, $"Exception should be {expectedExceptionCode}");
    }

    /// <summary>
    /// Asserts that a missing required parameter returns an OGC exception.
    /// </summary>
    protected async Task AssertMissingParameterReturnsExceptionAsync(
        string request,
        string expectedExceptionCode = "MissingParameterValue")
    {
        // Act - omit required SERVICE parameter
        var response = await Client.GetAsync($"{ServiceEndpoint}?request={request}");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ExceptionReport", "Missing parameter should return OGC ExceptionReport");
        content.Should().Contain(expectedExceptionCode, $"Exception should be {expectedExceptionCode}");
    }

    /// <summary>
    /// Asserts that an invalid operation name returns an OGC exception.
    /// </summary>
    protected async Task AssertInvalidOperationReturnsExceptionAsync()
    {
        // Act
        var response = await Client.GetAsync(
            $"{ServiceEndpoint}?service={ServiceType}&request=InvalidOperation");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ExceptionReport", "Invalid operation should return OGC ExceptionReport");
        content.Should().Contain("OperationNotSupported", "Exception should be OperationNotSupported");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Parses an XML response and validates it's well-formed.
    /// </summary>
    protected async Task<XDocument> ParseXmlResponseAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue("Response should succeed");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty("Response should have content");

        var xml = XDocument.Parse(content);
        xml.Root.Should().NotBeNull("Response should be valid XML");

        return xml;
    }

    /// <summary>
    /// Gets a test layer/feature type name if available, or skips the test.
    /// Override in derived classes to provide protocol-specific test data.
    /// </summary>
    protected virtual string GetTestLayerName()
    {
        return "test_layer";
    }

    /// <summary>
    /// Makes a GetCapabilities request and returns the parsed XML document.
    /// </summary>
    protected async Task<XDocument> GetCapabilitiesAsync()
    {
        var response = await Client.GetAsync(
            $"{ServiceEndpoint}?service={ServiceType}&request=GetCapabilities&version={ServiceVersion}");

        return await ParseXmlResponseAsync(response);
    }

    #endregion

    #region Common Compliance Tests

    /// <summary>
    /// Tests that GetCapabilities without version parameter works (uses latest version).
    /// </summary>
    [Fact]
    public virtual async Task GetCapabilities_WithoutVersion_ShouldSucceed()
    {
        // Act
        var response = await Client.GetAsync($"{ServiceEndpoint}?service={ServiceType}&request=GetCapabilities");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue("GetCapabilities without version should succeed");

        var xml = await ParseXmlResponseAsync(response);
        var version = xml.Root?.Attribute("version")?.Value;
        version.Should().NotBeNullOrEmpty("Response should include version attribute");
    }

    /// <summary>
    /// Tests that GetCapabilities is case-insensitive for request parameter.
    /// </summary>
    [Theory]
    [InlineData("GetCapabilities")]
    [InlineData("getcapabilities")]
    [InlineData("GETCAPABILITIES")]
    public virtual async Task GetCapabilities_WithDifferentCase_ShouldSucceed(string requestCase)
    {
        // Act
        var response = await Client.GetAsync($"{ServiceEndpoint}?service={ServiceType}&request={requestCase}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue($"GetCapabilities with case '{requestCase}' should succeed");
    }

    /// <summary>
    /// Tests that invalid service parameter returns appropriate error.
    /// </summary>
    [Fact]
    public virtual async Task Request_WithInvalidService_ShouldReturnException()
    {
        await AssertInvalidParameterReturnsExceptionAsync("service", "InvalidService", "InvalidParameterValue");
    }

    #endregion
}
