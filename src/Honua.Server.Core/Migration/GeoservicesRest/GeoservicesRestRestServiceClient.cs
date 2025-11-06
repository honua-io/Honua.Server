// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRestServiceClient : IGeoservicesRestServiceClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public GeoservicesRestServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<GeoservicesRestFeatureServiceInfo> GetServiceAsync(Uri serviceUri, string? token = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(serviceUri);

        var requestUri = AppendQuery(serviceUri,
            new KeyValuePair<string, string?>("f", "json"),
            new KeyValuePair<string, string?>("token", token));
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, requestUri, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var service = await JsonSerializer.DeserializeAsync<GeoservicesRestFeatureServiceInfo>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (service is null)
        {
            throw new InvalidDataException($"Feature service response from '{requestUri}' was empty.");
        }

        return service;
    }

    public async Task<GeoservicesRestLayerInfo> GetLayerAsync(Uri layerUri, string? token = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layerUri);

        var requestUri = AppendQuery(layerUri,
            new KeyValuePair<string, string?>("f", "json"),
            new KeyValuePair<string, string?>("token", token));
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, requestUri, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var layer = await JsonSerializer.DeserializeAsync<GeoservicesRestLayerInfo>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (layer is null)
        {
            throw new InvalidDataException($"Layer response from '{requestUri}' was empty.");
        }

        return layer;
    }

    public async Task<GeoservicesRestQueryResult> QueryAsync(Uri layerUri, GeoservicesRestQueryParameters parameters, string? token = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layerUri);
        Guard.NotNull(parameters);

        var queryValues = new Dictionary<string, string?>(parameters.GetValues(), StringComparer.OrdinalIgnoreCase);
        if (token.HasValue())
        {
            queryValues["token"] = token;
        }

        var requestUri = BuildQueryUri(layerUri, queryValues);
        using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, requestUri, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<GeoservicesRestQueryResult>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            throw new InvalidDataException($"Query response from '{requestUri}' was empty.");
        }

        return result;
    }

    public async Task<GeoservicesRestIdQueryResult> QueryObjectIdsAsync(Uri layerUri, GeoservicesRestQueryParameters parameters, string? token = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layerUri);
        Guard.NotNull(parameters);

        parameters.ReturnIdsOnly = true;
        parameters.ReturnCountOnly = null;
        parameters.OutFields = string.Empty;
        parameters.ReturnGeometry = false;
        parameters.SetAdditionalParameter("outFields", null);

        var queryValues = new Dictionary<string, string?>(parameters.GetValues(), StringComparer.OrdinalIgnoreCase);
        if (token.HasValue())
        {
            queryValues["token"] = token;
        }

        var requestUri = BuildQueryUri(layerUri, queryValues);
        using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, requestUri, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<GeoservicesRestIdQueryResult>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            throw new InvalidDataException($"ID query response from '{requestUri}' was empty.");
        }

        return result;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, Uri requestUri, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendFormat(CultureInfo.InvariantCulture, "Geoservices REST a.k.a. Esri REST request to '{0}' failed with status {1} ({2})", requestUri, (int)response.StatusCode, response.ReasonPhrase);

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (body.HasValue())
            {
                builder.Append(": ");
                builder.Append(body);
            }
        }
        catch
        {
            // Ignore body read errors
        }

        throw new InvalidOperationException(builder.ToString());
    }

    private static Uri BuildQueryUri(Uri baseUri, IReadOnlyDictionary<string, string?> parameters)
    {
        var builder = new StringBuilder(baseUri.ToString());
        var separator = baseUri.ToString().Contains('?', StringComparison.Ordinal) ? '&' : '?';
        foreach (var parameter in parameters)
        {
            if (parameter.Value.IsNullOrEmpty())
            {
                continue;
            }

            builder.Append(separator);
            separator = '&';
            builder.Append(Uri.EscapeDataString(parameter.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(parameter.Value));
        }

        return new Uri(builder.ToString(), UriKind.Absolute);
    }

    private static Uri AppendQuery(Uri baseUri, params KeyValuePair<string, string?>[] parameters)
    {
        if (parameters is null || parameters.Length == 0)
        {
            return baseUri;
        }

        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            dictionary[parameter.Key] = parameter.Value;
        }

        return BuildQueryUri(baseUri, dictionary);
    }
}
