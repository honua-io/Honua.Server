// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure;
using Azure.DigitalTwins.Core;
using Honua.Integration.Azure.Services;

namespace Honua.Integration.Azure.Tests.Mocks;

/// <summary>
/// Mock implementation of IAzureDigitalTwinsClient for testing.
/// </summary>
public class MockAzureDigitalTwinsClient : IAzureDigitalTwinsClient
{
    private readonly Dictionary<string, BasicDigitalTwin> _twins = new();
    private readonly Dictionary<string, Dictionary<string, BasicRelationship>> _relationships = new();
    private readonly Dictionary<string, DigitalTwinsModelData> _models = new();

    public Task<Response<BasicDigitalTwin>> CreateOrReplaceDigitalTwinAsync(
        string twinId,
        BasicDigitalTwin twin,
        ETag? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        _twins[twinId] = twin;
        return Task.FromResult(Response.FromValue(twin, CreateMockResponse()));
    }

    public Task<Response<BasicDigitalTwin>> GetDigitalTwinAsync(
        string twinId,
        CancellationToken cancellationToken = default)
    {
        if (_twins.TryGetValue(twinId, out var twin))
        {
            return Task.FromResult(Response.FromValue(twin, CreateMockResponse()));
        }

        throw new RequestFailedException(404, "Twin not found");
    }

    public Task<Response<BasicDigitalTwin>> UpdateDigitalTwinAsync(
        string twinId,
        string jsonPatch,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        if (!_twins.ContainsKey(twinId))
        {
            throw new RequestFailedException(404, "Twin not found");
        }

        // Simplified: Just return the existing twin
        return Task.FromResult(Response.FromValue(_twins[twinId], CreateMockResponse()));
    }

    public Task<Response> DeleteDigitalTwinAsync(
        string twinId,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        _twins.Remove(twinId);
        _relationships.Remove(twinId);
        return Task.FromResult(CreateMockResponse());
    }

    public AsyncPageable<BasicDigitalTwin> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        // Simplified: Return all twins
        return new MockAsyncPageable<BasicDigitalTwin>(_twins.Values);
    }

    public Task<Response<BasicRelationship>> CreateOrReplaceRelationshipAsync(
        string twinId,
        string relationshipId,
        BasicRelationship relationship,
        ETag? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        if (!_relationships.ContainsKey(twinId))
        {
            _relationships[twinId] = new Dictionary<string, BasicRelationship>();
        }

        _relationships[twinId][relationshipId] = relationship;
        return Task.FromResult(Response.FromValue(relationship, CreateMockResponse()));
    }

    public Task<Response<BasicRelationship>> GetRelationshipAsync(
        string twinId,
        string relationshipId,
        CancellationToken cancellationToken = default)
    {
        if (_relationships.TryGetValue(twinId, out var rels) &&
            rels.TryGetValue(relationshipId, out var relationship))
        {
            return Task.FromResult(Response.FromValue(relationship, CreateMockResponse()));
        }

        throw new RequestFailedException(404, "Relationship not found");
    }

    public Task<Response> DeleteRelationshipAsync(
        string twinId,
        string relationshipId,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        if (_relationships.TryGetValue(twinId, out var rels))
        {
            rels.Remove(relationshipId);
        }

        return Task.FromResult(CreateMockResponse());
    }

    public AsyncPageable<BasicRelationship> GetRelationshipsAsync(
        string twinId,
        string? relationshipName = null,
        CancellationToken cancellationToken = default)
    {
        if (_relationships.TryGetValue(twinId, out var rels))
        {
            var filtered = relationshipName != null
                ? rels.Values.Where(r => r.Name == relationshipName)
                : rels.Values;

            return new MockAsyncPageable<BasicRelationship>(filtered);
        }

        return new MockAsyncPageable<BasicRelationship>(Enumerable.Empty<BasicRelationship>());
    }

    public Task<Response<DigitalTwinsModelData[]>> CreateModelsAsync(
        IEnumerable<string> models,
        CancellationToken cancellationToken = default)
    {
        var modelDataList = new List<DigitalTwinsModelData>();

        foreach (var modelJson in models)
        {
            // Simplified: Create basic model data
            var modelData = DigitalTwinsModelData.DeserializeDigitalTwinsModelData(
                System.Text.Json.JsonDocument.Parse(modelJson).RootElement);

            _models[modelData.Id] = modelData;
            modelDataList.Add(modelData);
        }

        return Task.FromResult(Response.FromValue(modelDataList.ToArray(), CreateMockResponse()));
    }

    public Task<Response<DigitalTwinsModelData>> GetModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        if (_models.TryGetValue(modelId, out var model))
        {
            return Task.FromResult(Response.FromValue(model, CreateMockResponse()));
        }

        throw new RequestFailedException(404, "Model not found");
    }

    public AsyncPageable<DigitalTwinsModelData> GetModelsAsync(
        GetModelsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return new MockAsyncPageable<DigitalTwinsModelData>(_models.Values);
    }

    public Task<Response> DeleteModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        _models.Remove(modelId);
        return Task.FromResult(CreateMockResponse());
    }

    public Task<Response> DecommissionModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        // Simplified: Just mark as exists
        if (!_models.ContainsKey(modelId))
        {
            throw new RequestFailedException(404, "Model not found");
        }

        return Task.FromResult(CreateMockResponse());
    }

    private static Response CreateMockResponse()
    {
        return new MockResponse(200);
    }

    private class MockResponse : Response
    {
        public MockResponse(int status)
        {
            Status = status;
        }

        public override int Status { get; }
        public override string ReasonPhrase => "OK";
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = Guid.NewGuid().ToString();

        public override void Dispose() { }

        protected override bool ContainsHeader(string name) => false;
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => Enumerable.Empty<HttpHeader>();
        protected override bool TryGetHeader(string name, out string? value)
        {
            value = null;
            return false;
        }
        protected override bool TryGetHeaderValues(string name, out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }
    }

    private class MockAsyncPageable<T> : AsyncPageable<T>
    {
        private readonly IEnumerable<T> _items;

        public MockAsyncPageable(IEnumerable<T> items)
        {
            _items = items;
        }

        public override async IAsyncEnumerable<Page<T>> AsPages(
            string? continuationToken = null,
            int? pageSizeHint = null)
        {
            await Task.CompletedTask;
            yield return new MockPage<T>(_items);
        }
    }

    private class MockPage<T> : Page<T>
    {
        private readonly IEnumerable<T> _items;

        public MockPage(IEnumerable<T> items)
        {
            _items = items;
        }

        public override IReadOnlyList<T> Values => _items.ToList();
        public override string? ContinuationToken => null;
        public override Response GetRawResponse() => new MockResponse(200);
    }
}
