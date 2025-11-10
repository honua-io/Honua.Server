// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

/// <summary>
/// Docker container-based OData endpoint tests.
/// These tests use a real Honua Server instance in a Docker container to avoid
/// WebApplicationFactory limitations with OData v8.x route registration.
/// </summary>
[Collection("ODataContainer")]
public class ODataContainerTests
{
    private readonly ODataContainerFixture _fixture;

    public ODataContainerTests(ODataContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ServiceDocument_ShouldExposeEntitySet()
    {
        // Arrange & Act
        var response = await _fixture.Client.GetAsync("/odata");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();

        // Service document should list entity sets
        content.Should().Contain("value");
        content.Should().Contain("url");
    }

    [Fact]
    public async Task MetadataEndpoint_ShouldReturnEdmModel()
    {
        // Arrange & Act
        var response = await _fixture.Client.GetAsync("/odata/$metadata");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        // Metadata should be valid EDMX
        content.Should().Contain("<?xml version=\"1.0\"");
        content.Should().Contain("<edmx:Edmx");
        content.Should().Contain("EntitySet");
    }

    [Fact]
    public async Task ODataEndpoints_ShouldHonorQueryOptions()
    {
        // Arrange - Assuming "Roads" entity set exists based on test metadata
        var queryUrl = "/odata/Roads?$top=5&$select=name";

        // Act
        var response = await _fixture.Client.GetAsync(queryUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<ODataResponse<RoadEntity>>();
        json.Should().NotBeNull();
        json!.Value.Should().NotBeNull();
        json.Value.Should().HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportFiltering()
    {
        // Arrange
        var filterUrl = "/odata/Roads?$filter=status eq 'active'";

        // Act
        var response = await _fixture.Client.GetAsync(filterUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ODataResponse<RoadEntity>>();
        json.Should().NotBeNull();
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportOrdering()
    {
        // Arrange
        var orderUrl = "/odata/Roads?$orderby=name desc&$top=10";

        // Act
        var response = await _fixture.Client.GetAsync(orderUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ODataResponse<RoadEntity>>();
        json.Should().NotBeNull();
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportCounting()
    {
        // Arrange
        var countUrl = "/odata/Roads/$count";

        // Act
        var response = await _fixture.Client.GetAsync(countUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var countText = await response.Content.ReadAsStringAsync();
        int.TryParse(countText, out var count).Should().BeTrue();
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportPagination()
    {
        // Arrange
        var skipUrl = "/odata/Roads?$top=5&$skip=2";

        // Act
        var response = await _fixture.Client.GetAsync(skipUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ODataResponse<RoadEntity>>();
        json.Should().NotBeNull();
        json!.Value.Should().NotBeNull();
        json.Value.Should().HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportComplexFiltering()
    {
        // Arrange - Test AND operation in filter
        var complexFilterUrl = "/odata/Roads?$filter=status eq 'active' and length_km gt 10";

        // Act
        var response = await _fixture.Client.GetAsync(complexFilterUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ODataResponse<RoadEntity>>();
        json.Should().NotBeNull();
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportCombinedQueryOptions()
    {
        // Arrange - Test multiple query options together
        var combinedUrl = "/odata/Roads?$filter=status eq 'active'&$orderby=name&$top=10&$select=name,status";

        // Act
        var response = await _fixture.Client.GetAsync(combinedUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ODataResponse<RoadEntity>>();
        json.Should().NotBeNull();
        json!.Value.Should().HaveCountLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task ODataEndpoints_ShouldReturnCountInResponse()
    {
        // Arrange - Test $count=true query option
        var countInlineUrl = "/odata/Roads?$count=true&$top=5";

        // Act
        var response = await _fixture.Client.GetAsync(countInlineUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("@odata.count");
    }

    [Fact]
    public async Task ODataEndpoints_ShouldHandleInvalidFilter()
    {
        // Arrange - Test error handling with invalid filter
        var invalidFilterUrl = "/odata/Roads?$filter=invalid_field eq 'test'";

        // Act
        var response = await _fixture.Client.GetAsync(invalidFilterUrl);

        // Assert
        // Should return 400 Bad Request or 500 Internal Server Error
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportSelectWithMultipleFields()
    {
        // Arrange - Test selecting multiple specific fields
        var selectUrl = "/odata/Roads?$select=name,status,length_km&$top=5";

        // Act
        var response = await _fixture.Client.GetAsync(selectUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<ODataResponse<RoadEntity>>();
        json.Should().NotBeNull();
        json!.Value.Should().NotBeNull();
    }

    // Helper classes for JSON deserialization
    private class ODataResponse<T>
    {
        public T[]? Value { get; set; }
    }

    private class RoadEntity
    {
        public int RoadId { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public double? LengthKm { get; set; }
    }
}
