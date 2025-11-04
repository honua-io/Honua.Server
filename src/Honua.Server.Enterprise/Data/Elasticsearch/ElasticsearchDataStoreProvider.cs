// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

/// <summary>
/// Elasticsearch enterprise provider implementation backed by the REST API.
/// </summary>
public sealed partial class ElasticsearchDataStoreProvider : IDataStoreProvider, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    private readonly ConcurrentDictionary<string, ElasticsearchConnection> _connections = new(StringComparer.Ordinal);
    private readonly IHttpClientFactory? _httpClientFactory;
    private bool _disposed;

    private const int DefaultTermsSize = 1000;

    public const string ProviderKey = "elasticsearch";

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => ElasticsearchDataStoreCapabilities.Instance;

    /// <summary>
    /// Creates a new ElasticsearchDataStoreProvider.
    /// </summary>
    public ElasticsearchDataStoreProvider() : this(null)
    {
    }

    /// <summary>
    /// Creates a new ElasticsearchDataStoreProvider.
    /// </summary>
    /// <param name="httpClientFactory">Optional IHttpClientFactory for proper connection pooling (recommended).</param>
    public ElasticsearchDataStoreProvider(IHttpClientFactory? httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        _connections.Clear();
    }

    public Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult<byte[]?>(null);
    }

    public Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult<IDataStoreTransaction?>(null);
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);

        var connection = GetConnection(dataSource);
        using var request = new HttpRequestMessage(HttpMethod.Get, "_cluster/health?timeout=5s");
        using var response = await connection.Client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Elasticsearch health check failed with {(int)response.StatusCode} ({response.ReasonPhrase}): {content}");
        }
    }

    private ElasticsearchConnection GetConnection(DataSourceDefinition dataSource)
    {
        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        return _connections.GetOrAdd(
            dataSource.ConnectionString,
            cs =>
            {
                var info = ParseConnectionString(cs);
                return CreateConnection(info);
            });
    }

    private ElasticsearchConnection CreateConnection(ElasticsearchConnectionInfo info)
    {
        HttpClient client;

        // PERFORMANCE FIX: Use IHttpClientFactory for proper connection pooling if available
        if (_httpClientFactory != null)
        {
            client = _httpClientFactory.CreateClient("Elasticsearch");
            client.BaseAddress = EnsureTrailingSlash(info.BaseUri);
            client.Timeout = info.Timeout;
        }
        else
        {
            // Fallback: Create HttpClient with custom handler (legacy behavior)
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = true
            };

            if (info.DisableCertificateValidation)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            else if (info.CertificateFingerprint.HasValue())
            {
                var expectedFingerprint = info.CertificateFingerprint;
                handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                {
                    if (certificate is null)
                    {
                        return false;
                    }

                    var thumbprint = certificate.GetCertHashString();
                    return string.Equals(thumbprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase);
                };
            }

            client = new HttpClient(handler)
            {
                BaseAddress = EnsureTrailingSlash(info.BaseUri),
                Timeout = info.Timeout
            };
        }

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (info.Username.HasValue())
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{info.Username}:{info.Password ?? string.Empty}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
        else if (info.ApiKey.HasValue() ||
                 (info.ApiKeyId.HasValue() && info.ApiKeySecret.HasValue()))
        {
            var apiKey = info.ApiKey ?? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{info.ApiKeyId}:{info.ApiKeySecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        }

        return new ElasticsearchConnection(info, client, _httpClientFactory != null);
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private readonly record struct ElasticsearchStatisticMapping(
        string OutputName,
        StatisticType Type,
        string? FieldName,
        string? AggregationName,
        bool UseDocCount);

    private sealed class ElasticsearchConnection : IDisposable
    {
        private readonly bool _isFactoryClient;

        public ElasticsearchConnection(ElasticsearchConnectionInfo info, HttpClient client, bool isFactoryClient)
        {
            Info = info;
            Client = client ?? throw new ArgumentNullException(nameof(client));
            _isFactoryClient = isFactoryClient;
        }

        public ElasticsearchConnectionInfo Info { get; }

        public HttpClient Client { get; }

        public void Dispose()
        {
            // PERFORMANCE FIX: Only dispose HttpClient if NOT created by IHttpClientFactory
            // Factory-created clients are managed by the factory and should NOT be disposed
            if (!_isFactoryClient)
            {
                Client.Dispose();
            }
        }
    }

    private sealed record ElasticsearchConnectionInfo(
        Uri BaseUri,
        string? DefaultIndex,
        string? Username,
        string? Password,
        string? ApiKey,
        string? ApiKeyId,
        string? ApiKeySecret,
        bool DisableCertificateValidation,
        string? CertificateFingerprint,
        TimeSpan Timeout);
}
