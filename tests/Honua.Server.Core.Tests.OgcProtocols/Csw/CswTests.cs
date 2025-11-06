using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Csw;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Csw;

/// <summary>
/// Unit tests for CSW 2.0.2 (Catalog Service for the Web) implementation.
/// Tests conformance to OGC CSW specification.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CswTests
{
    private static readonly XNamespace Csw = "http://www.opengis.net/cat/csw/2.0.2";
    private static readonly XNamespace Ows = "http://www.opengis.net/ows";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    [Fact]
    public async Task GetCapabilities_ReturnsValidResponse()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetCapabilities");

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var content = await GetResultContentAsync(result);
        content.Should().Contain("Capabilities");
        content.Should().Contain("ServiceIdentification");
        content.Should().Contain("OperationsMetadata");

        var doc = XDocument.Parse(content);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Csw + "Capabilities");
        doc.Root.Attribute("version")?.Value.Should().Be("2.0.2");
    }

    [Fact]
    public async Task GetCapabilities_IncludesAllOperations()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetCapabilities");

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var operations = doc.Descendants(Ows + "Operation")
            .Select(op => op.Attribute("name")?.Value)
            .ToList();

        operations.Should().Contain("GetCapabilities");
        operations.Should().Contain("DescribeRecord");
        operations.Should().Contain("GetRecords");
        operations.Should().Contain("GetRecordById");
        operations.Should().Contain("GetDomain");
    }

    [Fact]
    public async Task GetRecords_ReturnsPaginatedResults()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetRecords", new Dictionary<string, StringValues>
        {
            ["startPosition"] = "1",
            ["maxRecords"] = "5"
        });

        catalog.Records = CreateMockRecords(10);

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var searchResults = doc.Descendants(Csw + "SearchResults").FirstOrDefault();
        searchResults.Should().NotBeNull();
        searchResults!.Attribute("numberOfRecordsMatched")?.Value.Should().Be("10");
        searchResults.Attribute("numberOfRecordsReturned")?.Value.Should().Be("5");
        searchResults.Attribute("nextRecord")?.Value.Should().Be("6");
    }

    [Fact]
    public async Task GetRecords_WithNoResults_ReturnsEmptySearchResults()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetRecords");
        catalog.Records = new List<CatalogDiscoveryRecord>();

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var searchResults = doc.Descendants(Csw + "SearchResults").FirstOrDefault();
        searchResults.Should().NotBeNull();
        searchResults!.Attribute("numberOfRecordsMatched")?.Value.Should().Be("0");
        searchResults.Attribute("numberOfRecordsReturned")?.Value.Should().Be("0");
    }

    [Fact]
    public async Task GetRecordById_WithValidId_ReturnsRecord()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetRecordById", new Dictionary<string, StringValues>
        {
            ["id"] = "test-record-1"
        });

        catalog.Records = new List<CatalogDiscoveryRecord> { CreateMockRecord("test-record-1", "Test Record") };

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Csw + "GetRecordByIdResponse");

        var record = doc.Descendants(Csw + "Record").FirstOrDefault();
        record.Should().NotBeNull();
        record!.Element(Dc + "identifier")?.Value.Should().Be("test-record-1");
        record.Element(Dc + "title")?.Value.Should().Be("Test Record");
    }

    [Fact]
    public async Task GetRecordById_WithInvalidId_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetRecordById", new Dictionary<string, StringValues>
        {
            ["id"] = "non-existent"
        });

        catalog.Records = new List<CatalogDiscoveryRecord>();

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Ows + "ExceptionReport");

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("InvalidParameterValue");
    }

    [Fact]
    public async Task GetRecordById_WithMissingId_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetRecordById");

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("MissingParameterValue");
    }

    [Fact]
    public async Task DescribeRecord_ReturnsSchemaDefinition()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("DescribeRecord");

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Csw + "DescribeRecordResponse");
        doc.Descendants(Csw + "SchemaComponent").Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetDomain_ReturnsPropertyDomains()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetDomain");

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Csw + "GetDomainResponse");
        doc.Descendants(Csw + "DomainValues").Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvalidServiceParameter_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetCapabilities");
        context.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["service"] = "WFS",
            ["request"] = "GetCapabilities"
        });

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("InvalidParameterValue");
    }

    [Fact]
    public async Task MissingRequestParameter_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("");
        context.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["service"] = "CSW"
        });

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("MissingParameterValue");
    }

    [Fact]
    public async Task GetRecords_RespectsMaxRecordLimit()
    {
        // Arrange
        var (context, metadataRegistry, catalog) = CreateTestContext("GetRecords", new Dictionary<string, StringValues>
        {
            ["maxRecords"] = "1000" // Exceeds limit of 100
        });

        catalog.Records = CreateMockRecords(150);

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var searchResults = doc.Descendants(Csw + "SearchResults").FirstOrDefault();
        searchResults.Should().NotBeNull();
        // Should return at most 100 records (MaxRecordLimit)
        int.Parse(searchResults!.Attribute("numberOfRecordsReturned")!.Value).Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetRecords_POST_XMLRequest_ParsesSuccessfully()
    {
        // Arrange
        var xmlRequest = """
            <?xml version="1.0" encoding="UTF-8"?>
            <csw:GetRecords xmlns:csw="http://www.opengis.net/cat/csw/2.0.2"
                            service="CSW"
                            version="2.0.2"
                            resultType="results"
                            startPosition="1"
                            maxRecords="10">
              <csw:Query typeNames="csw:Record">
                <csw:ElementSetName>full</csw:ElementSetName>
              </csw:Query>
            </csw:GetRecords>
            """;

        var (context, metadataRegistry, catalog) = CreateTestPostContext(xmlRequest);
        catalog.Records = CreateMockRecords(5);

        // Act
        var result = await CswHandlers.HandleAsync(context, metadataRegistry, catalog, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        // Should not be an exception
        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().BeNull("XML POST request should be parsed successfully");

        // Should be a GetRecordsResponse
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Csw + "GetRecordsResponse");

        var searchResults = doc.Descendants(Csw + "SearchResults").FirstOrDefault();
        searchResults.Should().NotBeNull();
        searchResults!.Attribute("numberOfRecordsMatched")?.Value.Should().Be("5");
    }

    private static (HttpContext context, IMetadataRegistry registry, TestCatalogService catalog) CreateTestContext(
        string requestType,
        Dictionary<string, StringValues>? additionalParams = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.PathBase = "";

        var queryParams = new Dictionary<string, StringValues>
        {
            ["service"] = "CSW",
            ["request"] = requestType
        };

        if (additionalParams != null)
        {
            foreach (var param in additionalParams)
            {
                queryParams[param.Key] = param.Value;
            }
        }

        context.Request.Query = new QueryCollection(queryParams);

        var metadata = new MetadataSnapshot(
            new CatalogDefinition { Id = "test", Title = "Test Catalog", Description = "Test" },
            Array.Empty<FolderDefinition>(),
            Array.Empty<DataSourceDefinition>(),
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>(),
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>(),
            new ServerDefinition()
        );

        var metadataRegistry = new TestMetadataRegistry(metadata);
        var catalog = new TestCatalogService();

        var services = new ServiceCollection();
        services.AddSingleton<IMetadataRegistry>(metadataRegistry);
        services.AddSingleton<ICatalogProjectionService>(catalog);
        context.RequestServices = services.BuildServiceProvider();

        return (context, metadataRegistry, catalog);
    }

    private static (HttpContext context, IMetadataRegistry registry, TestCatalogService catalog) CreateTestPostContext(
        string xmlBody)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.PathBase = "";
        context.Request.ContentType = "application/xml";

        // Write XML to request body
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(xmlBody);
        context.Request.Body = new System.IO.MemoryStream(bodyBytes);

        var metadata = new MetadataSnapshot(
            new CatalogDefinition { Id = "test", Title = "Test Catalog", Description = "Test" },
            Array.Empty<FolderDefinition>(),
            Array.Empty<DataSourceDefinition>(),
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>(),
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>(),
            new ServerDefinition()
        );

        var metadataRegistry = new TestMetadataRegistry(metadata);
        var catalog = new TestCatalogService();

        var services = new ServiceCollection();
        services.AddSingleton<IMetadataRegistry>(metadataRegistry);
        services.AddSingleton<ICatalogProjectionService>(catalog);
        context.RequestServices = services.BuildServiceProvider();

        return (context, metadataRegistry, catalog);
    }

    private sealed class TestMetadataRegistry : IMetadataRegistry
    {
        private readonly MetadataSnapshot _snapshot;

        public TestMetadataRegistry(MetadataSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public MetadataSnapshot Snapshot => _snapshot;
        public bool IsInitialized => true;
        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => new ValueTask<MetadataSnapshot>(_snapshot);
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Microsoft.Extensions.Primitives.IChangeToken GetChangeToken() => throw new NotImplementedException();
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }
    }

    private sealed class TestCatalogService : ICatalogProjectionService
    {
        public List<CatalogDiscoveryRecord> Records { get; set; } = new();

        public CatalogProjectionSnapshot GetSnapshot() => throw new NotImplementedException();
        public IReadOnlyList<CatalogGroupView> GetGroups() => throw new NotImplementedException();
        public CatalogGroupView? GetGroup(string groupId) => throw new NotImplementedException();
        public CatalogServiceView? GetService(string serviceId) => throw new NotImplementedException();
        public CatalogDiscoveryRecord? GetRecord(string recordId) => Records.FirstOrDefault(r => r.Id == recordId);
        public IReadOnlyList<CatalogDiscoveryRecord> Search(string? query, string? groupId = null, int limit = 100, int offset = 0) => Records.Skip(offset).Take(limit).ToList();
        public Task WarmupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose()
        {
        }
    }

    private static List<CatalogDiscoveryRecord> CreateMockRecords(int count)
    {
        var records = new List<CatalogDiscoveryRecord>();
        for (int i = 1; i <= count; i++)
        {
            records.Add(CreateMockRecord($"record-{i}", $"Test Record {i}"));
        }
        return records;
    }

    private static CatalogDiscoveryRecord CreateMockRecord(string id, string title)
    {
        return new CatalogDiscoveryRecord
        {
            Id = id,
            Title = title,
            GroupId = "test-group",
            GroupTitle = "Test Group",
            ServiceId = "test-service",
            ServiceTitle = "Test Service",
            ServiceType = "WFS",
            LayerId = "test-layer",
            Summary = $"Summary for {title}",
            Keywords = new[] { "test", "geospatial" },
            Themes = Array.Empty<string>(),
            Contacts = Array.Empty<CatalogContactDefinition>(),
            Links = Array.Empty<LinkDefinition>(),
            SpatialExtent = new CatalogSpatialExtentDefinition
            {
                Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } }
            }
        };
    }

    private static async Task<string> GetResultContentAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.RequestServices = serviceProvider;
        var responseBody = new System.IO.MemoryStream();
        context.Response.Body = responseBody;

        await result.ExecuteAsync(context);

        responseBody.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new System.IO.StreamReader(responseBody);
        return await reader.ReadToEndAsync();
    }
}
