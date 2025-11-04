using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

internal sealed class GcloudCliEmulator : IGcloudCli
{
    private readonly FakeGcsApiClient _gcsApi;

    public GcloudCliEmulator(FakeGcsApiClient gcsApi)
    {
        _gcsApi = gcsApi ?? throw new ArgumentNullException(nameof(gcsApi));
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        if (arguments.Length >= 4 &&
            arguments[0] == "storage" &&
            arguments[1] == "buckets" &&
            arguments[2] == "update")
        {
            var bucketName = arguments[3];

            if (arguments.Contains("--versioning"))
            {
                var bucket = await _gcsApi.GetBucketAsync(bucketName, cancellationToken).ConfigureAwait(false);
                bucket.Versioning ??= new FakeGcsApiClient.BucketDocument.VersioningDocument();
                bucket.Versioning.Enabled = true;
                await _gcsApi.UpdateBucketAsync(bucket, cancellationToken).ConfigureAwait(false);
            }

            var corsFilePath = GetOptionValue(arguments, "--cors-file");
            if (!string.IsNullOrEmpty(corsFilePath))
            {
                var json = await File.ReadAllTextAsync(corsFilePath!, cancellationToken).ConfigureAwait(false);
                var rules = JsonSerializer.Deserialize<List<CorsRuleDocument>>(json) ?? new List<CorsRuleDocument>();

                var bucket = await _gcsApi.GetBucketAsync(bucketName, cancellationToken).ConfigureAwait(false);
                bucket.Cors = new List<FakeGcsApiClient.BucketDocument.CorsData>();
                foreach (var rule in rules)
                {
                    bucket.Cors.Add(new FakeGcsApiClient.BucketDocument.CorsData
                    {
                        Origin = rule.Origins,
                        Method = rule.Methods,
                        ResponseHeader = rule.ResponseHeaders,
                        MaxAgeSeconds = rule.MaxAgeSeconds
                    });
                }

                await _gcsApi.UpdateBucketAsync(bucket, cancellationToken).ConfigureAwait(false);
            }

            return string.Empty;
        }

        throw new InvalidOperationException($"GCloud emulator received unsupported command: {string.Join(' ', arguments)}");
    }

    private static string? GetOptionValue(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index > -1 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private sealed class CorsRuleDocument
    {
        [JsonPropertyName("origin")]
        public IList<string> Origins { get; set; } = new List<string>();

        [JsonPropertyName("method")]
        public IList<string> Methods { get; set; } = new List<string>();

        [JsonPropertyName("responseHeader")]
        public IList<string> ResponseHeaders { get; set; } = new List<string>();

        [JsonPropertyName("maxAgeSeconds")]
        public int MaxAgeSeconds { get; set; }
    }
}

internal sealed class FakeGcsApiClient : IDisposable
{
    private static readonly HttpMethod PatchMethod = new("PATCH");
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, BucketDocument> _bucketCache = new(StringComparer.OrdinalIgnoreCase);

    public FakeGcsApiClient(Uri baseUri)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = baseUri ?? throw new ArgumentNullException(nameof(baseUri)),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task CreateBucketAsync(string projectId, string bucketName, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new BucketDocument
        {
            Name = bucketName
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/storage/v1/b?project={projectId}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        _bucketCache[bucketName] = new BucketDocument
        {
            Name = bucketName,
            Versioning = new BucketDocument.VersioningDocument { Enabled = false },
            Cors = new List<BucketDocument.CorsData>()
        };
    }

    public async Task<BucketDocument> GetBucketAsync(string bucketName, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/storage/v1/b/{bucketName}?projection=full", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var document = await JsonSerializer.DeserializeAsync<BucketDocument>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            throw new InvalidOperationException($"Failed to deserialize bucket document for {bucketName}");
        }

        if (_bucketCache.TryGetValue(bucketName, out var cached))
        {
            if (cached.Versioning is not null)
            {
                document.Versioning = new BucketDocument.VersioningDocument
                {
                    Enabled = cached.Versioning.Enabled
                };
            }

            if (cached.Cors is not null)
            {
                document.Cors = cached.Cors
                    .Select(rule => new BucketDocument.CorsData
                    {
                        Origin = new List<string>(rule.Origin),
                        Method = new List<string>(rule.Method),
                        ResponseHeader = new List<string>(rule.ResponseHeader),
                        MaxAgeSeconds = rule.MaxAgeSeconds
                    })
                    .ToList();
            }
        }

        return document;
    }

    public async Task UpdateBucketAsync(BucketDocument document, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(document);
        using var request = new HttpRequestMessage(PatchMethod, $"/storage/v1/b/{document.Name}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        _bucketCache[document.Name] = document;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException($"GCS emulator request failed: {response.StatusCode} {content}");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    internal sealed class BucketDocument
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("versioning")]
        public VersioningDocument? Versioning { get; set; }

        [JsonPropertyName("cors")]
        public List<CorsData>? Cors { get; set; }

        internal sealed class VersioningDocument
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }
        }

        internal sealed class CorsData
        {
            [JsonPropertyName("origin")]
            public IList<string> Origin { get; set; } = new List<string>();

            [JsonPropertyName("method")]
            public IList<string> Method { get; set; } = new List<string>();

            [JsonPropertyName("responseHeader")]
            public IList<string> ResponseHeader { get; set; } = new List<string>();

            [JsonPropertyName("maxAgeSeconds")]
            public int MaxAgeSeconds { get; set; }
        }
    }
}
