using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Tests.Shared;
using Honua.Server.Host.OpenApi.Filters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public class GeoservicesRestEditingTests : IClassFixture<GeoservicesEditingFixture>
{
    private readonly GeoservicesEditingFixture _fixture;

    public GeoservicesRestEditingTests(GeoservicesEditingFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    [Fact]
    public async Task ApplyEdits_ShouldAddUpdateAndDelete()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new
        {
            adds = new[]
            {
                new
                {
                    attributes = new { name = "SE Stark St", status = "planned" },
                    geometry = new
                    {
                        paths = new[]
                        {
                            new[]
                            {
                                new[] { -122.51, 45.51 },
                                new[] { -122.49, 45.52 }
                            }
                        },
                        spatialReference = new { wkid = 4326 }
                    }
                }
            },
            updates = new[]
            {
                new
                {
                    attributes = new { road_id = 1, status = "closed" }
                }
            },
            deletes = "2"
        };

        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/applyEdits?f=json", payload);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        root.GetProperty("addResults").EnumerateArray().Should().ContainSingle(result => result.GetProperty("success").GetBoolean());
        root.GetProperty("updateResults").EnumerateArray().Should().ContainSingle(result => result.GetProperty("success").GetBoolean());
        root.GetProperty("deleteResults").EnumerateArray().Should().ContainSingle(result => result.GetProperty("success").GetBoolean());

        var repository = _fixture.GetRepository();
        repository.Features.Should().HaveCount(3);
        repository.Features.Should().Contain(feature => Convert.ToInt32(feature.Attributes["road_id"]) == 1 && string.Equals(Convert.ToString(feature.Attributes["status"]), "closed", StringComparison.OrdinalIgnoreCase));
        repository.Features.Should().Contain(feature => Convert.ToInt32(feature.Attributes["road_id"]) != 2);
    }

    [Fact]
    public async Task ApplyEdits_ReturnEditMoment_ShouldIncludeTimestamp()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new
        {
            adds = new[]
            {
                new
                {
                    attributes = new { name = "SE Madison St", status = "open" }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/applyEdits?f=json&returnEditMoment=true", payload);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        root.GetProperty("addResults").EnumerateArray().Should().ContainSingle(result => result.GetProperty("success").GetBoolean());
        root.TryGetProperty("editMoment", out var editMomentElement).Should().BeTrue();
        editMomentElement.GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplyEdits_WithGlobalIds_ShouldResolveIdentifiers()
    {
        _fixture.ResetRepository();
        var repository = _fixture.GetRepository();
        var existing = repository.Features;

        var updateFeature = existing[0];
        var deleteFeature = existing[1];

        var updateGlobalId = Convert.ToString(updateFeature.Attributes["globalId"], CultureInfo.InvariantCulture);
        var deleteGlobalId = Convert.ToString(deleteFeature.Attributes["globalId"], CultureInfo.InvariantCulture);

        var payload = new
        {
            updates = new[]
            {
                new
                {
                    attributes = new { globalId = updateGlobalId, status = "maintenance" }
                }
            },
            deletes = deleteGlobalId
        };

        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/applyEdits?f=json&useGlobalIds=true", payload);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        var updateResult = root.GetProperty("updateResults").EnumerateArray().Should().ContainSingle().Subject;
        updateResult.GetProperty("success").GetBoolean().Should().BeTrue();
        updateResult.GetProperty("globalId").GetString().Should().Be(updateGlobalId);

        var deleteResult = root.GetProperty("deleteResults").EnumerateArray().Should().ContainSingle().Subject;
        deleteResult.GetProperty("success").GetBoolean().Should().BeTrue();
        deleteResult.GetProperty("globalId").GetString().Should().Be(deleteGlobalId);

        var refreshed = _fixture.GetRepository();
        refreshed.Features.Should().Contain(feature =>
            string.Equals(Convert.ToString(feature.Attributes["globalId"], CultureInfo.InvariantCulture), updateGlobalId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Convert.ToString(feature.Attributes["status"], CultureInfo.InvariantCulture), "maintenance", StringComparison.OrdinalIgnoreCase));

        refreshed.Features.Should().NotContain(feature =>
            string.Equals(Convert.ToString(feature.Attributes["globalId"], CultureInfo.InvariantCulture), deleteGlobalId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyEdits_StreamedRequest_ShouldProcessChunkedPayload()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new
        {
            adds = new[]
            {
                new
                {
                    attributes = new { name = "SW Market St", status = "open" },
                    geometry = new
                    {
                        paths = new[]
                        {
                            new[]
                            {
                                new[] { -122.690, 45.520 },
                                new[] { -122.688, 45.522 }
                            }
                        },
                        spatialReference = new { wkid = 4326 }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var source = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var chunkedStream = new NonSeekableReadOnlyStream(source);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/rest/services/transportation/roads/FeatureServer/0/applyEdits?f=json");
        request.Headers.TransferEncodingChunked = true;
        var content = new StreamContent(chunkedStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Content = content;

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        root.GetProperty("addResults").EnumerateArray().Should().ContainSingle(result => result.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task QueryKml_ShouldIncludeStyleDefinition()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/rest/services/transportation/roads/FeatureServer/0/query?where=1=1&f=kml&returnGeometry=true");
        response.EnsureSuccessStatusCode();

        var kml = await response.Content.ReadAsStringAsync();
        kml.Contains("<Style", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        kml.Contains("<styleUrl", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public async Task AddFeatures_ShouldReturnAddResults()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new
        {
            features = new[]
            {
                new
                {
                    attributes = new { name = "NW Everett St", status = "open" },
                    geometry = new
                    {
                        paths = new[]
                        {
                            new[]
                            {
                                new[] { -122.55, 45.52 },
                                new[] { -122.53, 45.54 }
                            }
                        },
                        spatialReference = new { wkid = 4326 }
                    }
                }
            },
            rollbackOnFailure = true
        };

        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/addFeatures?f=json", payload);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var addResults = document.RootElement.GetProperty("addResults").EnumerateArray().ToArray();
        addResults.Should().HaveCount(1);
        addResults[0].GetProperty("success").GetBoolean().Should().BeTrue();
        addResults[0].TryGetProperty("objectId", out var objectId).Should().BeTrue();
        objectId.GetInt32().Should().BeGreaterThan(0);
        addResults[0].TryGetProperty("globalId", out var globalIdElement).Should().BeTrue();
        Guid.TryParse(globalIdElement.GetString(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateFeatures_ShouldModifyAttributes()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new
        {
            features = new[]
            {
                new
                {
                    attributes = new { road_id = 3, status = "planned" }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/updateFeatures?f=json", payload);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var results = document.RootElement.GetProperty("updateResults").EnumerateArray().ToArray();
        results.Should().HaveCount(1);
        results[0].GetProperty("success").GetBoolean().Should().BeTrue();

        var repository = _fixture.GetRepository();
        repository.Features.Should().Contain(feature => Convert.ToInt32(feature.Attributes["road_id"]) == 3 && string.Equals(Convert.ToString(feature.Attributes["status"]), "planned", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateFeatures_UseGlobalIds_ShouldModifyFeature()
    {
        _fixture.ResetRepository();
        var repository = _fixture.GetRepository();
        var target = repository.Features.First();
        var globalId = Convert.ToString(target.Attributes["globalId"], CultureInfo.InvariantCulture);

        var payload = new
        {
            features = new[]
            {
                new
                {
                    attributes = new { globalId, status = "closed" }
                }
            }
        };

        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/updateFeatures?f=json&useGlobalIds=true", payload);
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, responseBody);

        using var document = JsonDocument.Parse(responseBody);
        var results = document.RootElement.GetProperty("updateResults").EnumerateArray().ToArray();
        results.Should().HaveCount(1);
        results[0].GetProperty("success").GetBoolean().Should().BeTrue();
        results[0].TryGetProperty("globalId", out var globalIdElement).Should().BeTrue();
        globalIdElement.GetString().Should().Be(globalId);

        var refreshed = _fixture.GetRepository();
        refreshed.Features.Should().Contain(feature =>
            string.Equals(Convert.ToString(feature.Attributes["globalId"], CultureInfo.InvariantCulture), globalId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Convert.ToString(feature.Attributes["status"]), "closed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteFeatures_UseGlobalIds_ShouldRemoveFeature()
    {
        _fixture.ResetRepository();
        var repository = _fixture.GetRepository();
        var target = repository.Features.First();
        var globalId = Convert.ToString(target.Attributes["globalId"], CultureInfo.InvariantCulture);

        var payload = new
        {
            objectIds = new[] { globalId }
        };

        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/deleteFeatures?f=json&useGlobalIds=true", payload);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var results = document.RootElement.GetProperty("deleteResults").EnumerateArray().ToArray();
        results.Should().HaveCount(1);
        results[0].GetProperty("success").GetBoolean().Should().BeTrue();
        results[0].TryGetProperty("globalId", out var globalIdElement).Should().BeTrue();
        globalIdElement.GetString().Should().Be(globalId);

        var refreshed = _fixture.GetRepository();
        refreshed.Features.Should().NotContain(feature =>
            string.Equals(Convert.ToString(feature.Attributes["globalId"], CultureInfo.InvariantCulture), globalId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteFeatures_ShouldRemoveRequestedIds()
    {
        _fixture.ResetRepository();
        var client = _fixture.CreateAuthenticatedClient();

        var payload = new
        {
            objectIds = new[] { 1, 2 }
        };

        var response = await client.PostAsJsonAsync("/rest/services/transportation/roads/FeatureServer/0/deleteFeatures?f=json", payload);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var results = document.RootElement.GetProperty("deleteResults").EnumerateArray().ToArray();
        results.Should().HaveCount(2);
        results.Should().OnlyContain(result => result.GetProperty("success").GetBoolean());

        var repository = _fixture.GetRepository();
        repository.Features
            .Select(feature => Convert.ToInt32(feature.Attributes["road_id"]))
            .Should()
            .NotContain(id => id == 1 || id == 2);
    }

    [Fact]
    public async Task Attachments_Lifecycle_ShouldSucceed()
    {
        _fixture.ResetRepository();
        _fixture.ResetAttachments();
        var client = _fixture.CreateAuthenticatedClient();
        const string basePath = "/rest/services/transportation/roads/FeatureServer/0";
        const string serviceId = "roads";
        const string layerId = "roads-primary";
        const string featureId = "1";

        // Initial query should show no attachments
        using (var queryResponse = await client.GetAsync($"{basePath}/queryAttachments?f=json&objectIds={featureId}"))
        {
            queryResponse.EnsureSuccessStatusCode();
            using var queryDocument = await JsonDocument.ParseAsync(await queryResponse.Content.ReadAsStreamAsync());
            var groups = queryDocument.RootElement.GetProperty("attachmentGroups").EnumerateArray().ToArray();
            groups.Should().ContainSingle();
            groups[0].GetProperty("objectId").GetInt32().Should().Be(int.Parse(featureId, CultureInfo.InvariantCulture));
            groups[0].GetProperty("attachmentInfos").GetArrayLength().Should().Be(0);
        }

        // Add attachment
        var addBytes = Encoding.UTF8.GetBytes("Initial attachment payload");
        using var addContent = new MultipartFormDataContent();
        addContent.Add(new StringContent(featureId), "objectId");
        var addFileContent = new ByteArrayContent(addBytes);
        addFileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        addContent.Add(addFileContent, "attachment", "initial.txt");

        using var addResponse = await client.PostAsync($"{basePath}/addAttachment?f=json", addContent);
        addResponse.EnsureSuccessStatusCode();
        using var addDocument = await JsonDocument.ParseAsync(await addResponse.Content.ReadAsStreamAsync());
        var addResult = addDocument.RootElement.GetProperty("addAttachmentResult");
        addResult.GetProperty("success").GetBoolean().Should().BeTrue();
        var attachmentObjectId = addResult.GetProperty("id").GetInt32();
        var attachmentGlobalId = addResult.GetProperty("globalId").GetString();
        attachmentObjectId.Should().BeGreaterThan(0);
        Guid.TryParse(attachmentGlobalId, out _).Should().BeTrue();

        var attachmentRepository = _fixture.GetAttachmentRepository();
        var descriptors = await attachmentRepository.ListByFeatureAsync(serviceId, layerId, featureId);
        descriptors.Should().ContainSingle();
        descriptors[0].AttachmentObjectId.Should().Be(attachmentObjectId);
        descriptors[0].ServiceId.Should().Be(serviceId);
        descriptors[0].LayerId.Should().Be(layerId);
        descriptors[0].Name.Should().Be("initial.txt");

        // Query should now reflect attachment
        using (var queryResponse = await client.GetAsync($"{basePath}/queryAttachments?f=json&objectIds={featureId}"))
        {
            queryResponse.EnsureSuccessStatusCode();
            using var queryDocument = await JsonDocument.ParseAsync(await queryResponse.Content.ReadAsStreamAsync());
            var attachmentInfos = queryDocument.RootElement
                .GetProperty("attachmentGroups")
                .EnumerateArray().Single()
                .GetProperty("attachmentInfos")
                .EnumerateArray().ToArray();
            attachmentInfos.Should().ContainSingle();
            attachmentInfos[0].GetProperty("id").GetInt32().Should().Be(attachmentObjectId);
            attachmentInfos[0].GetProperty("name").GetString().Should().Be("initial.txt");
        }

        // Download should return original payload
        using (var downloadResponse = await client.GetAsync($"{basePath}/{featureId}/attachments/{attachmentObjectId}"))
        {
            downloadResponse.EnsureSuccessStatusCode();
            var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
            downloadedBytes.Should().Equal(addBytes);
        }

        // Update attachment
        var updatedBytes = Encoding.UTF8.GetBytes("Updated attachment payload");
        using var updateContent = new MultipartFormDataContent();
        updateContent.Add(new StringContent(featureId), "objectId");
        updateContent.Add(new StringContent(attachmentObjectId.ToString(CultureInfo.InvariantCulture)), "attachmentId");
        var updateFileContent = new ByteArrayContent(updatedBytes);
        updateFileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        updateContent.Add(updateFileContent, "attachment", "updated.txt");

        using var updateResponse = await client.PostAsync($"{basePath}/updateAttachment?f=json", updateContent);
        updateResponse.EnsureSuccessStatusCode();
        using var updateDocument = await JsonDocument.ParseAsync(await updateResponse.Content.ReadAsStreamAsync());
        var updateResult = updateDocument.RootElement.GetProperty("updateAttachmentResult");
        updateResult.GetProperty("success").GetBoolean().Should().BeTrue();
        updateResult.GetProperty("id").GetInt32().Should().Be(attachmentObjectId);

        descriptors = await attachmentRepository.ListByFeatureAsync(serviceId, layerId, featureId);
        descriptors.Should().ContainSingle();
        descriptors[0].Name.Should().Be("updated.txt");

        using (var downloadResponse = await client.GetAsync($"{basePath}/{featureId}/attachments/{attachmentObjectId}"))
        {
            downloadResponse.EnsureSuccessStatusCode();
            var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
            downloadedBytes.Should().Equal(updatedBytes);
        }

        // Delete attachment
        using var deleteContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("objectId", featureId),
            new KeyValuePair<string, string>("attachmentIds", attachmentObjectId.ToString(CultureInfo.InvariantCulture))
        });

        using var deleteResponse = await client.PostAsync($"{basePath}/deleteAttachments?f=json", deleteContent);
        deleteResponse.EnsureSuccessStatusCode();
        using var deleteDocument = await JsonDocument.ParseAsync(await deleteResponse.Content.ReadAsStreamAsync());
        var deleteResults = deleteDocument.RootElement.GetProperty("deleteAttachmentResults").EnumerateArray().ToArray();
        deleteResults.Should().ContainSingle();
        deleteResults[0].GetProperty("success").GetBoolean().Should().BeTrue();

        descriptors = await attachmentRepository.ListByFeatureAsync(serviceId, layerId, featureId);
        descriptors.Should().BeEmpty();

        using (var queryResponse = await client.GetAsync($"{basePath}/queryAttachments?f=json&objectIds={featureId}"))
        {
            queryResponse.EnsureSuccessStatusCode();
            using var queryDocument = await JsonDocument.ParseAsync(await queryResponse.Content.ReadAsStreamAsync());
            var attachmentInfos = queryDocument.RootElement
                .GetProperty("attachmentGroups")
                .EnumerateArray().Single()
                .GetProperty("attachmentInfos")
                .EnumerateArray().ToArray();
            attachmentInfos.Should().BeEmpty();
        }
    }

    private sealed class NonSeekableReadOnlyStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableReadOnlyStream(Stream inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _inner.ReadAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

public sealed class GeoservicesEditingFixture : HonuaTestWebApplicationFactory
{
    private readonly string _attachmentRoot;

    public GeoservicesEditingFixture()
    {
        _attachmentRoot = Path.Combine(RootPath, "attachments");
        Directory.CreateDirectory(_attachmentRoot);
    }

    internal InMemoryEditableFeatureRepository GetRepository() => Services.GetRequiredService<InMemoryEditableFeatureRepository>();

    internal TestAttachmentRepository GetAttachmentRepository() => Services.GetRequiredService<TestAttachmentRepository>();

    public void ResetRepository()
    {
        var repository = GetRepository();
        repository.Reset();
    }

    public void ResetAttachments()
    {
        var repository = GetAttachmentRepository();
        repository.Reset();

        if (Directory.Exists(_attachmentRoot))
        {
            Directory.Delete(_attachmentRoot, recursive: true);
        }

        Directory.CreateDirectory(_attachmentRoot);
    }

    protected override string GetMetadataJson()
    {
        return BuildMetadata();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // Override configuration service to add attachment configuration
        services.RemoveAll<IHonuaConfigurationService>();
        services.AddSingleton<IHonuaConfigurationService>(_ => new HonuaConfigurationService(new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = MetadataPath
            },
            Attachments = new AttachmentConfiguration
            {
                DefaultMaxSizeMiB = 25,
                Profiles = new Dictionary<string, AttachmentStorageProfileConfiguration>(StringComparer.OrdinalIgnoreCase)
                {
                    ["local"] = new AttachmentStorageProfileConfiguration
                    {
                        Provider = "filesystem",
                        FileSystem = new AttachmentFileSystemStorageConfiguration
                        {
                            RootPath = _attachmentRoot
                        }
                    }
                }
            }
        }));

        // GeoservicesREST editing requires InMemoryEditableFeatureRepository
        services.AddSingleton<InMemoryEditableFeatureRepository>();
        services.AddSingleton<IFeatureRepository>(sp => sp.GetRequiredService<InMemoryEditableFeatureRepository>());

        // Attachment support
        services.RemoveAll<IFeatureAttachmentRepository>();
        services.AddSingleton<TestAttachmentRepository>();
        services.AddSingleton<IFeatureAttachmentRepository>(sp => sp.GetRequiredService<TestAttachmentRepository>());
    }

    protected override void ConfigureNoOpServices(IServiceCollection services)
    {
        base.ConfigureNoOpServices(services);

        // Replace StubFeatureRepository (added by base) with InMemoryEditableFeatureRepository
        // This must be done here because ConfigureNoOpServices runs after ConfigureServices
        services.RemoveAll<IFeatureRepository>();
        services.AddSingleton<IFeatureRepository>(sp => sp.GetRequiredService<InMemoryEditableFeatureRepository>());
    }


    private static string BuildMetadata()
    {
        var metadataJson = HonuaWebApplicationFactory.SampleMetadata;
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        var rootNode = JsonNode.Parse(metadataJson) as JsonObject;
        if (rootNode is null)
        {
            return metadataJson;
        }

        if (!rootNode.TryGetPropertyValue("layers", out var layersNode))
        {
            return rootNode.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
        }

        if (layersNode is not JsonArray layers || layers.Count == 0)
        {
            return rootNode.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
        }

        var layerElement = layers[0];
        if (layerElement is not JsonObject layer)
        {
            return rootNode.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
        }

        const string globalIdField = "globalId";

        var query = layer["query"] as JsonObject ?? new JsonObject();
        query["maxRecordCount"] = 100;
        layer["query"] = query;

        var fields = layer["fields"] as JsonArray ?? new JsonArray();
        if (!fields.OfType<JsonObject>().Any(field => string.Equals(field?["name"]?.GetValue<string>(), globalIdField, StringComparison.OrdinalIgnoreCase)))
        {
            fields.Add(new JsonObject
            {
                ["name"] = globalIdField,
                ["dataType"] = "string",
                ["nullable"] = false
            });
            layer["fields"] = fields;
        }

        var editing = layer["editing"] as JsonObject ?? new JsonObject();
        var capabilities = editing["capabilities"] as JsonObject ?? new JsonObject();
        capabilities["allowAdd"] = true;
        capabilities["allowUpdate"] = true;
        capabilities["allowDelete"] = true;
        capabilities["requireAuthentication"] = true;
        capabilities["allowedRoles"] = new JsonArray();
        editing["capabilities"] = capabilities;

        var constraints = editing["constraints"] as JsonObject ?? new JsonObject();
        constraints["immutableFields"] = new JsonArray("road_id", globalIdField);
        constraints["requiredFields"] = new JsonArray("name");
        constraints["defaultValues"] = new JsonObject { ["status"] = "planned" };
        editing["constraints"] = constraints;

        layer["editing"] = editing;

        layer["attachments"] = new JsonObject
        {
            ["enabled"] = true,
            ["storageProfileId"] = "local",
            ["maxSizeMiB"] = 25,
            ["allowedContentTypes"] = new JsonArray("text/plain", "application/octet-stream"),
            ["disallowedContentTypes"] = new JsonArray(),
            ["requireGlobalIds"] = false,
            ["returnPresignedUrls"] = false
        };

        return rootNode.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
    }
}
