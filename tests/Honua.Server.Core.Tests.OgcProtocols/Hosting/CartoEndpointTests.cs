using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Host;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class CartoEndpointTests : IClassFixture<HonuaWebApplicationFactory>, IAsyncLifetime
{
    private readonly HonuaWebApplicationFactory _factory;
    private string? _originalMetadata;
    private string? _databasePath;

    public CartoEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_ShouldListDatasets()
    {
        var client = _factory.CreateAuthenticatedClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/carto/api/v3/datasets");

        payload.GetProperty("count").GetInt32().Should().BeGreaterThan(0);
        var datasets = payload.GetProperty("datasets").EnumerateArray().ToArray();
        datasets.Should().Contain(item => item.GetProperty("id").GetString() == "roads.roads-primary");
    }

    [Fact]
    public async Task DatasetDetail_ShouldExposeFieldsAndCount()
    {
        var client = _factory.CreateAuthenticatedClient();

        var detail = await client.GetFromJsonAsync<JsonElement>("/carto/api/v3/datasets/roads.roads-primary");

        detail.GetProperty("fields").EnumerateArray().Should().Contain(field =>
            field.GetProperty("name").GetString() == "name" &&
            field.GetProperty("type").GetString() == "string");

        detail.GetProperty("recordCount").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SqlGet_ShouldReturnRows()
    {
        var client = _factory.CreateAuthenticatedClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/carto/api/v3/sql?q=SELECT+%2A+FROM+roads.roads-primary+LIMIT+2");

        payload.GetProperty("total_rows").GetInt64().Should().BeGreaterThan(0);
        payload.GetProperty("rows").EnumerateArray().Should().NotBeEmpty();
        payload.GetProperty("fields").EnumerateObject().Should().Contain(field =>
            field.Name.Equals("geom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SqlPost_ShouldReturnCount()
    {
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.SendAsync(CreateSqlPostRequest("SELECT count(*) FROM roads.roads-primary"));
        var payloadText = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Carto SQL count query failed: {payloadText}");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("rows").EnumerateArray().First().GetProperty("count").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SqlGet_ShouldHonorWhereClause()
    {
        var client = _factory.CreateAuthenticatedClient();

        var idField = await ResolveIdFieldAsync(client);
        var seedQuery = $"SELECT {idField} FROM roads.roads-primary LIMIT 1";
        var seedPayload = await client.GetFromJsonAsync<JsonElement>($"/carto/api/v3/sql?q={Uri.EscapeDataString(seedQuery)}");
        var firstRow = seedPayload.GetProperty("rows").EnumerateArray().First();
        var idValueElement = firstRow.GetProperty(idField);
        var literal = BuildSqlLiteral(idValueElement);

        var filteredQuery = $"SELECT {idField} FROM roads.roads-primary WHERE {idField} = {literal}";
        var payload = await client.GetFromJsonAsync<JsonElement>($"/carto/api/v3/sql?q={Uri.EscapeDataString(filteredQuery)}");

        var rows = payload.GetProperty("rows").EnumerateArray().ToArray();
        rows.Should().NotBeEmpty();

        foreach (var row in rows)
        {
            var value = row.GetProperty(idField);
            AreEqual(value, idValueElement).Should().BeTrue();
        }
    }

    [Fact]
    public async Task SqlPost_ShouldSupportOrderBy()
    {
        var client = _factory.CreateAuthenticatedClient();

        var idField = await ResolveIdFieldAsync(client);
        var query = $"SELECT {idField} FROM roads.roads-primary ORDER BY {idField} DESC LIMIT 5";
        var response = await client.SendAsync(CreateSqlPostRequest(query));
        var payloadText = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Carto SQL aggregate query failed: {payloadText}");

        using var aggregateDocument = JsonDocument.Parse(payloadText);
        var payload = aggregateDocument.RootElement;
        var rows = payload.GetProperty("rows").EnumerateArray().ToArray();
        rows.Should().NotBeEmpty();

        double? previousNumber = null;
        string? previousString = null;

        foreach (var row in rows)
        {
            var value = row.GetProperty(idField);
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    var number = value.GetDouble();
                    if (previousNumber.HasValue)
                    {
                        number.Should().BeLessThanOrEqualTo(previousNumber.Value);
                    }
                    previousNumber = number;
                    break;
                case JsonValueKind.String:
                    var text = value.GetString();
                    if (previousString is not null)
                    {
                        string.Compare(text, previousString, StringComparison.Ordinal).Should().BeLessThanOrEqualTo(0);
                    }
                    previousString = text;
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected JSON type for ORDER BY verification: {value.ValueKind}");
            }
        }
    }

    [Fact]
    public async Task SqlGet_InvalidOrderBy_ShouldReturnBadRequest()
    {
        var client = _factory.CreateAuthenticatedClient();
        var query = "SELECT * FROM roads.roads-primary ORDER BY does_not_exist";
        var response = await client.GetAsync($"/carto/api/v3/sql?q={Uri.EscapeDataString(query)}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("error").GetString().Should().Contain("ORDER BY field");
    }

    [Fact]
    public async Task SqlGet_ShouldSupportInClause()
    {
        var client = _factory.CreateAuthenticatedClient();

        var idField = await ResolveIdFieldAsync(client);
        var seedQuery = $"SELECT {idField} FROM roads.roads-primary LIMIT 2";
        var seedPayload = await client.GetFromJsonAsync<JsonElement>($"/carto/api/v3/sql?q={Uri.EscapeDataString(seedQuery)}");
        var seedRows = seedPayload.GetProperty("rows").EnumerateArray().ToArray();
        seedRows.Should().HaveCountGreaterThan(0);

        var literals = seedRows.Select(row => BuildSqlLiteral(row.GetProperty(idField))).ToArray();
        var inClause = string.Join(",", literals);

        var query = $"SELECT {idField} FROM roads.roads-primary WHERE {idField} IN ({inClause}) ORDER BY {idField}";
        var payload = await client.GetFromJsonAsync<JsonElement>($"/carto/api/v3/sql?q={Uri.EscapeDataString(query)}");

        var rows = payload.GetProperty("rows").EnumerateArray().ToArray();
        rows.Should().HaveCount(seedRows.Length);
    }

    [Fact]
    public async Task SqlGet_ShouldSupportLikeClause()
    {
        var client = _factory.CreateAuthenticatedClient();

        var seedQuery = "SELECT name FROM roads.roads-primary LIMIT 1";
        var seedPayload = await client.GetFromJsonAsync<JsonElement>($"/carto/api/v3/sql?q={Uri.EscapeDataString(seedQuery)}");
        var firstRow = seedPayload.GetProperty("rows").EnumerateArray().First();
        var name = firstRow.GetProperty("name").GetString();
        name.Should().NotBeNullOrEmpty();

        var prefix = name!.Length >= 3 ? name[..3] : name;
        var pattern = $"{prefix.Replace("'", "''")}%";

        var query = $"SELECT name FROM roads.roads-primary WHERE name LIKE '{pattern}' LIMIT 5";
        var payload = await client.GetFromJsonAsync<JsonElement>($"/carto/api/v3/sql?q={Uri.EscapeDataString(query)}");

        var rows = payload.GetProperty("rows").EnumerateArray().ToArray();
        rows.Should().NotBeEmpty();

        foreach (var row in rows)
        {
            var value = row.GetProperty("name").GetString();
            value.Should().NotBeNull();
            value!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        }
    }

    [Fact]
    public async Task SqlPost_ShouldReturnGroupedCounts()
    {
        var client = _factory.CreateAuthenticatedClient();
        var groupField = await ResolveGroupFieldAsync(client);

        var query = $"SELECT {groupField} AS group_label, COUNT(*) AS total_count FROM roads.roads-primary GROUP BY {groupField} ORDER BY total_count DESC LIMIT 3";
        var response = await client.SendAsync(CreateSqlPostRequest(query));
        var payloadText = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Carto SQL aggregate query failed: {payloadText}");

        using var payloadDocument = JsonDocument.Parse(payloadText);
        var payload = payloadDocument.RootElement;
        var fields = payload.GetProperty("fields").EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
        fields.Should().ContainKey("group_label");
        fields.Should().ContainKey("total_count");

        var rows = payload.GetProperty("rows").EnumerateArray().ToArray();
        rows.Should().NotBeEmpty();

        foreach (var row in rows)
        {
            row.GetProperty("total_count").GetInt64().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SqlPost_ShouldComputeNumericAggregates()
    {
        var client = _factory.CreateAuthenticatedClient();
        var numericField = await ResolveNumericFieldAsync(client);

        var query = $"SELECT SUM({numericField}) AS total_sum, AVG({numericField}) AS average_value FROM roads.roads-primary";
        var response = await client.SendAsync(CreateSqlPostRequest(query));
        var payloadText = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Carto SQL aggregate query failed: {payloadText}");

        using var payloadDocument = JsonDocument.Parse(payloadText);
        var payload = payloadDocument.RootElement;

        var fields = payload.GetProperty("fields").EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
        fields.Should().ContainKey("total_sum");
        fields.Should().ContainKey("average_value");

        var row = payload.GetProperty("rows").EnumerateArray().First();
        row.TryGetProperty("total_sum", out var sumElement).Should().BeTrue();
        row.TryGetProperty("average_value", out var avgElement).Should().BeTrue();

        sumElement.ValueKind.Should().Be(JsonValueKind.Number);
        avgElement.ValueKind.Should().Be(JsonValueKind.Number);
        avgElement.GetDouble().Should().BeGreaterThan(0);
    }

    private static async Task<string> ResolveIdFieldAsync(HttpClient client)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>("/carto/api/v3/datasets/roads.roads-primary");
        foreach (var field in detail.GetProperty("fields").EnumerateArray())
        {
            if (string.Equals(field.GetProperty("type").GetString(), "id", StringComparison.OrdinalIgnoreCase))
            {
                return field.GetProperty("name").GetString() ?? throw new InvalidOperationException("ID field name missing.");
            }
        }

        throw new InvalidOperationException("Dataset does not define an ID field.");
    }

    private static async Task<string> ResolveGroupFieldAsync(HttpClient client)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>("/carto/api/v3/datasets/roads.roads-primary");
        foreach (var field in detail.GetProperty("fields").EnumerateArray())
        {
            var name = field.GetProperty("name").GetString();
            var type = field.GetProperty("type").GetString();

            if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "geom", StringComparison.OrdinalIgnoreCase))
            {
                return name!;
            }
        }

        throw new InvalidOperationException("Unable to locate a string field for grouping.");
    }

    private static async Task<string> ResolveNumericFieldAsync(HttpClient client)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>("/carto/api/v3/datasets/roads.roads-primary");
        foreach (var field in detail.GetProperty("fields").EnumerateArray())
        {
            var name = field.GetProperty("name").GetString();
            var type = field.GetProperty("type").GetString();

            if (string.Equals(type, "number", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "id", StringComparison.OrdinalIgnoreCase))
            {
                return name!;
            }
        }

        throw new InvalidOperationException("Unable to locate a numeric field for aggregation.");
    }

    private static string BuildSqlLiteral(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => $"'{element.GetString()?.Replace("'", "''", StringComparison.Ordinal) ?? string.Empty}'",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "NULL",
            _ => throw new InvalidOperationException($"Unsupported JSON literal for SQL WHERE clause: {element.ValueKind}")
        };
    }

    private static HttpRequestMessage CreateSqlPostRequest(string sql)
    {
        var payload = JsonSerializer.Serialize(new { q = sql });
        return new HttpRequestMessage(HttpMethod.Post, "/carto/api/v3/sql")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private static bool AreEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Number => Math.Abs(left.GetDouble() - right.GetDouble()) < double.Epsilon,
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.True => right.ValueKind == JsonValueKind.True,
            JsonValueKind.False => right.ValueKind == JsonValueKind.False,
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
    }

    public async Task InitializeAsync()
    {
        _originalMetadata = File.ReadAllText(_factory.MetadataPath);
        _databasePath = Path.Combine(Path.GetTempPath(), $"honua-carto-{Guid.NewGuid():N}.db");

        var sampleDb = Path.Combine(TestEnvironment.SolutionRoot, "samples", "ogc", "ogc-sample.db");
        File.Copy(sampleDb, _databasePath, overwrite: true);

        var metadataNode = JsonNode.Parse(_originalMetadata!) ?? throw new InvalidOperationException("Metadata parse failed.");
        var dataSources = metadataNode["dataSources"]?.AsArray();
        if (dataSources is null || dataSources.Count == 0)
        {
            throw new InvalidOperationException("Sample metadata does not define data sources.");
        }

        dataSources[0]!["connectionString"] = $"Data Source={_databasePath}";
        var updatedMetadata = metadataNode.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
        File.WriteAllText(_factory.MetadataPath, updatedMetadata);

        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
        await registry.ReloadAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_originalMetadata is not null)
        {
            File.WriteAllText(_factory.MetadataPath, _originalMetadata);
            using var scope = _factory.Services.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
            await registry.ReloadAsync().ConfigureAwait(false);
        }

        if (_databasePath is not null)
        {
            try
            {
                File.Delete(_databasePath);
            }
            catch
            {
            }
        }
    }
}
