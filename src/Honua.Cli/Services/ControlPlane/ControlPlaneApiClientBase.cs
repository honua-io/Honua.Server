// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.ControlPlane;

/// <summary>
/// Base class for control plane API clients providing common HTTP operations.
/// </summary>
/// <remarks>
/// This base class handles:
/// <list type="bullet">
/// <item>HTTP client creation with authentication</item>
/// <item>Common GET/POST/PATCH/DELETE patterns</item>
/// <item>JSON serialization with standard options</item>
/// <item>Response streaming for large payloads</item>
/// <item>404 handling (returns null vs exception)</item>
/// </list>
/// Derived clients implement specific endpoints and business logic.
/// </remarks>
public abstract class ControlPlaneApiClientBase
{
    /// <summary>
    /// Standard JSON serializer options for control plane API communication.
    /// </summary>
    protected static readonly JsonSerializerOptions DefaultSerializerOptions = Honua.Server.Core.Utilities.JsonHelper.CreateOptions(
        writeIndented: false,
        camelCase: true,
        caseInsensitive: true,
        ignoreNullValues: true,
        maxDepth: 64
    );

    protected readonly IHttpClientFactory HttpClientFactory;

    protected ControlPlaneApiClientBase(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// Creates an HTTP client configured for the control plane connection.
    /// </summary>
    /// <param name="connection">Control plane connection configuration.</param>
    /// <param name="namedClient">Optional named client from HTTP client factory.</param>
    /// <param name="timeout">Optional timeout override (defaults to 30 seconds).</param>
    /// <returns>Configured HTTP client.</returns>
    protected HttpClient CreateClient(
        ControlPlaneConnection connection,
        string? namedClient = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var client = namedClient.IsNullOrWhiteSpace()
            ? HttpClientFactory.CreateClient()
            : HttpClientFactory.CreateClient(namedClient);

        client.BaseAddress = connection.BaseUri;
        client.Timeout = timeout ?? TimeSpan.FromSeconds(30);

        if (connection.BearerToken.HasValue())
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", connection.BearerToken);
        }

        return client;
    }

    /// <summary>
    /// POST to an endpoint with optional JSON payload, returns JsonDocument.
    /// </summary>
    /// <param name="connection">Control plane connection.</param>
    /// <param name="endpoint">API endpoint path (e.g., "/admin/metadata/reload").</param>
    /// <param name="payload">Optional payload (object to serialize or string JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response as JsonDocument.</returns>
    protected async Task<JsonDocument> PostAsJsonDocumentAsync(
        ControlPlaneConnection connection,
        string endpoint,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var client = CreateClient(connection);
        HttpContent? content = null;

        if (payload != null)
        {
            var json = payload is string str
                ? str
                : JsonSerializer.Serialize(payload, DefaultSerializerOptions);
            content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await client.PostAsync(endpoint, content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// POST to an endpoint and deserialize response to typed object.
    /// </summary>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="connection">Control plane connection.</param>
    /// <param name="endpoint">API endpoint path.</param>
    /// <param name="payload">Optional payload (object to serialize or string JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response or null.</returns>
    protected async Task<TResponse?> PostAsync<TResponse>(
        ControlPlaneConnection connection,
        string endpoint,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var client = CreateClient(connection);
        HttpContent? content = null;

        if (payload != null)
        {
            var json = payload is string str
                ? str
                : JsonSerializer.Serialize(payload, DefaultSerializerOptions);
            content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await client.PostAsync(endpoint, content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<TResponse>(
            stream,
            DefaultSerializerOptions,
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// GET from an endpoint and return JsonDocument.
    /// Returns null if 404 Not Found.
    /// </summary>
    /// <param name="connection">Control plane connection.</param>
    /// <param name="endpoint">API endpoint path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response as JsonDocument, or null if not found.</returns>
    protected async Task<JsonDocument?> GetAsJsonDocumentAsync(
        ControlPlaneConnection connection,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var client = CreateClient(connection);

        using var response = await client.GetAsync(endpoint, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// GET from an endpoint and deserialize to typed object.
    /// Returns null if 404 Not Found.
    /// </summary>
    /// <typeparam name="T">Response type.</typeparam>
    /// <param name="connection">Control plane connection.</param>
    /// <param name="endpoint">API endpoint path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response or null if not found.</returns>
    protected async Task<T?> GetAsync<T>(
        ControlPlaneConnection connection,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var client = CreateClient(connection);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            DefaultSerializerOptions,
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// PATCH to an endpoint with optional JSON payload, returns JsonDocument.
    /// </summary>
    /// <param name="connection">Control plane connection.</param>
    /// <param name="endpoint">API endpoint path.</param>
    /// <param name="payload">Optional payload (object to serialize or string JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response as JsonDocument.</returns>
    protected async Task<JsonDocument> PatchAsJsonDocumentAsync(
        ControlPlaneConnection connection,
        string endpoint,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var client = CreateClient(connection);
        HttpContent? content = null;

        if (payload != null)
        {
            var json = payload is string str
                ? str
                : JsonSerializer.Serialize(payload, DefaultSerializerOptions);
            content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await client.PatchAsync(endpoint, content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// DELETE an endpoint resource and return JsonDocument.
    /// Returns null if 404 Not Found.
    /// </summary>
    /// <param name="connection">Control plane connection.</param>
    /// <param name="endpoint">API endpoint path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response as JsonDocument, or null if not found.</returns>
    protected async Task<JsonDocument?> DeleteAsJsonDocumentAsync(
        ControlPlaneConnection connection,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var client = CreateClient(connection);

        using var response = await client.DeleteAsync(endpoint, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// DELETE an endpoint resource (no response expected).
    /// </summary>
    /// <param name="connection">Control plane connection.</param>
    /// <param name="endpoint">API endpoint path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task DeleteAsync(
        ControlPlaneConnection connection,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var client = CreateClient(connection);

        using var response = await client.DeleteAsync(endpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
