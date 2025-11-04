using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
public sealed class GeoservicesEditingServiceTests
{
    private readonly IFeatureEditOrchestrator _orchestrator = Substitute.For<IFeatureEditOrchestrator>();
    private readonly IFeatureRepository _repository = Substitute.For<IFeatureRepository>();
    private readonly ILogger<GeoservicesEditingService> _logger = Substitute.For<ILogger<GeoservicesEditingService>>();
    private readonly GeoservicesEditingService _service;

    public GeoservicesEditingServiceTests()
    {
        _service = new GeoservicesEditingService(_orchestrator, _repository, _logger);
    }

    [Fact]
    public async Task ExecuteEditsAsync_DeleteWithGlobalId_ResolvesObjectId()
    {
        // Arrange
        const string globalId = "2c5f5667-9f4c-4c4f-9e5f-3a0a9b099999";
        var serviceView = BuildServiceView();
        var layerView = BuildLayerView();

        _repository.QueryAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<FeatureQuery>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ToAsyncEnumerable(CreateFeatureRecord(globalId, "101")));

        _orchestrator.ExecuteAsync(Arg.Any<FeatureEditBatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var batch = callInfo.Arg<FeatureEditBatch>();
                var result = FeatureEditCommandResult.CreateSuccess(batch.Commands[0], "101");
                return Task.FromResult(new FeatureEditBatchResult(new[] { result }));
            });

        using var document = JsonDocument.Parse($@"{{""deletes"":[""{globalId}""]}}");
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;
        request.QueryString = new QueryString("?useGlobalIds=true");

        // Act
        var execution = await _service.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            request,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "deletes" },
            includeAdds: false,
            includeUpdates: false,
            includeDeletes: true,
            cancellationToken: CancellationToken.None);

        // Assert
        execution.HasOperations.Should().BeTrue();
        execution.DeleteResults.Should().HaveCount(1);
        execution.DeleteResults[0].Should().BeOfType<Dictionary<string, object?>>();
        var payload = (Dictionary<string, object?>)execution.DeleteResults[0];
        payload["success"].Should().Be(true);
        payload["objectId"].Should().Be("101");
        payload["globalId"].Should().Be(globalId);
    }

    [Fact]
    public async Task ExecuteEditsAsync_DeleteWithUnknownGlobalId_ReturnsFailure()
    {
        // Arrange
        const string globalId = "2c5f5667-9f4c-4c4f-9e5f-3a0a9b080808";
        var serviceView = BuildServiceView();
        var layerView = BuildLayerView();

        _repository.QueryAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<FeatureQuery>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ToAsyncEnumerable());

        using var document = JsonDocument.Parse($@"{{""deletes"":[""{globalId}""]}}");
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;
        request.QueryString = new QueryString("?useGlobalIds=true");

        // Act
        var execution = await _service.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            request,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "deletes" },
            includeAdds: false,
            includeUpdates: false,
            includeDeletes: true,
            cancellationToken: CancellationToken.None);

        // Assert
        execution.HasOperations.Should().BeFalse();
        execution.DeleteResults.Should().HaveCount(1);
        var payload = (Dictionary<string, object?>)execution.DeleteResults[0];
        payload["success"].Should().Be(false);
        payload["objectId"].Should().Be(globalId);
        payload["globalId"].Should().Be(globalId);
        payload["error"].Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteEditsAsync_UpdateWithGlobalIdOnly_ResolvesObjectId()
    {
        // Arrange
        const string globalId = "d5f26304-5d3a-4bf7-bd03-94d321111111";
        var serviceView = BuildServiceView();
        var layerView = BuildLayerView();

        _repository.QueryAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<FeatureQuery>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ToAsyncEnumerable(CreateFeatureRecord(globalId, "202")));

        _orchestrator.ExecuteAsync(Arg.Any<FeatureEditBatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var batch = callInfo.Arg<FeatureEditBatch>();
                var result = FeatureEditCommandResult.CreateSuccess(batch.Commands[0], "202");
                return Task.FromResult(new FeatureEditBatchResult(new[] { result }));
            });

        using var document = JsonDocument.Parse($@"{{""updates"":[{{""attributes"":{{""globalId"":""{globalId}"",""name"":""Updated""}}}}]}}");
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;
        request.QueryString = new QueryString("?useGlobalIds=true");

        // Act
        var execution = await _service.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            request,
            Array.Empty<string>(),
            new[] { "updates" },
            Array.Empty<string>(),
            includeAdds: false,
            includeUpdates: true,
            includeDeletes: false,
            cancellationToken: CancellationToken.None);

        // Assert
        execution.HasOperations.Should().BeTrue();
        execution.UpdateResults.Should().HaveCount(1);
        var payload = (Dictionary<string, object?>)execution.UpdateResults[0];
        payload["success"].Should().Be(true);
        payload["objectId"].Should().Be("202");
        payload["globalId"].Should().Be(globalId);
    }

    [Fact]
    public async Task ParsePayloadAsync_AllowsChunkedRequestBody()
    {
        // Arrange
        var controllerType = typeof(GeoservicesRESTFeatureServerController);
        var parseMethod = controllerType.GetMethod("ParsePayloadAsync", BindingFlags.NonPublic | BindingFlags.Static);
        parseMethod.Should().NotBeNull();

        using var innerStream = new MemoryStream(Encoding.UTF8.GetBytes(@"{""adds"":[]}"));
        var body = new NonSeekableStream(innerStream);

        var context = new DefaultHttpContext();
        context.Request.Body = body;

        // Act
        var task = (Task<JsonDocument?>)parseMethod!.Invoke(null, new object[] { context.Request, CancellationToken.None })!;
        using var document = await task;

        // Assert
        document.Should().NotBeNull();
        document!.RootElement.TryGetProperty("adds", out _).Should().BeTrue();
    }

    private static CatalogServiceView BuildServiceView()
    {
        var layer = BuildLayerDefinition();
        var service = GeoservicesTestFactory.CreateServiceDefinition(layers: new[] { layer });
        return GeoservicesTestFactory.CreateServiceView(service, new[] { GeoservicesTestFactory.CreateLayerView(layer) });
    }

    private static CatalogLayerView BuildLayerView()
    {
        var layer = BuildLayerDefinition();
        return GeoservicesTestFactory.CreateLayerView(layer);
    }

    private static LayerDefinition BuildLayerDefinition()
    {
        var fields = new[]
        {
            new FieldDefinition { Name = "objectid", DataType = "integer", Nullable = false },
            new FieldDefinition { Name = "globalId", DataType = "guid", Nullable = false },
            new FieldDefinition { Name = "name", DataType = "string", Nullable = true }
        };

        return GeoservicesTestFactory.CreateLayerDefinition(
            fields: fields,
            geometryField: "shape",
            geometryType: "esriGeometryPolygon");
    }

    private static FeatureRecord CreateFeatureRecord(string globalId, string objectId)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["globalId"] = globalId,
            ["objectid"] = objectId
        };

        return new FeatureRecord(attributes);
    }

    private static async IAsyncEnumerable<FeatureRecord> ToAsyncEnumerable(params FeatureRecord[] records)
    {
        foreach (var record in records)
        {
            yield return record;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task ExecuteEditsAsync_UpdateWithVersion_PassesVersionToCommand()
    {
        // Arrange
        const string featureId = "101";
        const long version = 5;
        var serviceView = BuildServiceView();
        var layerView = BuildLayerView();

        UpdateFeatureCommand? capturedCommand = null;
        _orchestrator.ExecuteAsync(Arg.Any<FeatureEditBatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var batch = callInfo.Arg<FeatureEditBatch>();
                capturedCommand = batch.Commands[0] as UpdateFeatureCommand;
                var result = FeatureEditCommandResult.CreateSuccess(batch.Commands[0], featureId, version + 1);
                return Task.FromResult(new FeatureEditBatchResult(new[] { result }));
            });

        using var document = JsonDocument.Parse($@"{{
            ""updates"": [
                {{
                    ""attributes"": {{ ""objectid"": ""{featureId}"", ""name"": ""Updated"" }},
                    ""version"": {version}
                }}
            ]
        }}");
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var execution = await _service.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            request,
            Array.Empty<string>(),
            new[] { "updates" },
            Array.Empty<string>(),
            includeAdds: false,
            includeUpdates: true,
            includeDeletes: false,
            cancellationToken: CancellationToken.None);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Version.Should().Be(version);
        execution.UpdateResults.Should().HaveCount(1);
        var payload = (Dictionary<string, object?>)execution.UpdateResults[0];
        payload["success"].Should().Be(true);
        payload["version"].Should().Be(version + 1);
    }

    [Fact]
    public async Task ExecuteEditsAsync_ConcurrentUpdateWithVersionConflict_ReturnsError409()
    {
        // Arrange
        const string featureId = "101";
        const long clientVersion = 5;
        var serviceView = BuildServiceView();
        var layerView = BuildLayerView();

        _orchestrator.ExecuteAsync(Arg.Any<FeatureEditBatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var batch = callInfo.Arg<FeatureEditBatch>();
                var error = new FeatureEditError("version_conflict",
                    "Concurrency conflict for Feature '101'. The resource has been modified by another user. Expected version: 5, Actual version: 6.",
                    new Dictionary<string, string?>
                    {
                        ["entityId"] = featureId,
                        ["entityType"] = "Feature",
                        ["expectedVersion"] = clientVersion.ToString(),
                        ["actualVersion"] = "6"
                    });
                var result = FeatureEditCommandResult.CreateFailure(batch.Commands[0], error);
                return Task.FromResult(new FeatureEditBatchResult(new[] { result }));
            });

        using var document = JsonDocument.Parse($@"{{
            ""updates"": [
                {{
                    ""attributes"": {{ ""objectid"": ""{featureId}"", ""name"": ""Updated"" }},
                    ""version"": {clientVersion}
                }}
            ]
        }}");
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var execution = await _service.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            request,
            Array.Empty<string>(),
            new[] { "updates" },
            Array.Empty<string>(),
            includeAdds: false,
            includeUpdates: true,
            includeDeletes: false,
            cancellationToken: CancellationToken.None);

        // Assert
        execution.UpdateResults.Should().HaveCount(1);
        var payload = (Dictionary<string, object?>)execution.UpdateResults[0];
        payload["success"].Should().Be(false);
        payload.Should().ContainKey("error");

        var error = (Dictionary<string, object?>)payload["error"]!;
        error["code"].Should().Be(409); // Esri error code for conflict
        ((string)error["description"]!).Should().Contain("modified by another user");
        error.Should().ContainKey("details");
    }

    [Fact]
    public async Task ExecuteEditsAsync_AddFeature_ReturnsVersionInResponse()
    {
        // Arrange
        const string featureId = "101";
        const long initialVersion = 1;
        var serviceView = BuildServiceView();
        var layerView = BuildLayerView();

        _orchestrator.ExecuteAsync(Arg.Any<FeatureEditBatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var batch = callInfo.Arg<FeatureEditBatch>();
                var result = FeatureEditCommandResult.CreateSuccess(batch.Commands[0], featureId, initialVersion);
                return Task.FromResult(new FeatureEditBatchResult(new[] { result }));
            });

        using var document = JsonDocument.Parse(@"{
            ""adds"": [
                {
                    ""attributes"": { ""name"": ""New Feature"" }
                }
            ]
        }");
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var execution = await _service.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            request,
            new[] { "adds" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeAdds: true,
            includeUpdates: false,
            includeDeletes: false,
            cancellationToken: CancellationToken.None);

        // Assert
        execution.AddResults.Should().HaveCount(1);
        var payload = (Dictionary<string, object?>)execution.AddResults[0];
        payload["success"].Should().Be(true);
        payload["objectId"].Should().Be(featureId);
        payload["version"].Should().Be(initialVersion);
    }

    [Fact]
    public async Task ExecuteEditsAsync_BatchUpdateWithRollback_StopsOnVersionConflict()
    {
        // Arrange
        var serviceView = BuildServiceView();
        var layerView = BuildLayerView();

        _orchestrator.ExecuteAsync(Arg.Any<FeatureEditBatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var batch = callInfo.Arg<FeatureEditBatch>();
                var results = new List<FeatureEditCommandResult>();

                // First update succeeds
                results.Add(FeatureEditCommandResult.CreateSuccess(batch.Commands[0], "101", 6));

                // Second update fails with version conflict
                var error = new FeatureEditError("version_conflict",
                    "Concurrency conflict detected.",
                    new Dictionary<string, string?>
                    {
                        ["entityId"] = "102",
                        ["expectedVersion"] = "3",
                        ["actualVersion"] = "4"
                    });
                results.Add(FeatureEditCommandResult.CreateFailure(batch.Commands[1], error));

                // Third update should be aborted (batch_aborted)
                var abortError = new FeatureEditError("batch_aborted", "Batch aborted due to earlier failure.");
                results.Add(FeatureEditCommandResult.CreateFailure(batch.Commands[2], abortError));

                return Task.FromResult(new FeatureEditBatchResult(results));
            });

        using var document = JsonDocument.Parse(@"{
            ""updates"": [
                { ""attributes"": { ""objectid"": ""101"", ""name"": ""Feature 1"" }, ""version"": 5 },
                { ""attributes"": { ""objectid"": ""102"", ""name"": ""Feature 2"" }, ""version"": 3 },
                { ""attributes"": { ""objectid"": ""103"", ""name"": ""Feature 3"" }, ""version"": 2 }
            ],
            ""rollbackOnFailure"": true
        }");
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var execution = await _service.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            request,
            Array.Empty<string>(),
            new[] { "updates" },
            Array.Empty<string>(),
            includeAdds: false,
            includeUpdates: true,
            includeDeletes: false,
            cancellationToken: CancellationToken.None);

        // Assert
        execution.UpdateResults.Should().HaveCount(3);

        // First succeeded
        var result1 = (Dictionary<string, object?>)execution.UpdateResults[0];
        result1["success"].Should().Be(true);

        // Second failed with version conflict
        var result2 = (Dictionary<string, object?>)execution.UpdateResults[1];
        result2["success"].Should().Be(false);
        var error2 = (Dictionary<string, object?>)result2["error"]!;
        error2["code"].Should().Be(409);

        // Third aborted
        var result3 = (Dictionary<string, object?>)execution.UpdateResults[2];
        result3["success"].Should().Be(false);
        var error3 = (Dictionary<string, object?>)result3["error"]!;
        error3["code"].Should().Be(500); // batch_aborted maps to 500
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
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
