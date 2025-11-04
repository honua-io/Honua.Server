// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// HTTP raster source provider with support for Cloud Optimized GeoTIFF (COG) range requests.
/// </summary>
public sealed class HttpRasterSourceProvider : IRasterSourceProvider
{
    private readonly HttpClient _httpClient;

    public HttpRasterSourceProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string ProviderKey => "http";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            throw new ArgumentException($"Invalid HTTP URI: {uri}", nameof(uri));
        }

        var response = await _httpClient.GetAsync(parsedUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            throw new ArgumentException($"Invalid HTTP URI: {uri}", nameof(uri));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, parsedUri);

        // Add Range header for COG optimization
        if (length.HasValue)
        {
            var end = offset + length.Value - 1;
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, end);
        }
        else
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, null);
        }

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        // HTTP 206 Partial Content is expected for range requests, but fall back to 200 OK
        if (response.StatusCode != System.Net.HttpStatusCode.PartialContent &&
            response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}
