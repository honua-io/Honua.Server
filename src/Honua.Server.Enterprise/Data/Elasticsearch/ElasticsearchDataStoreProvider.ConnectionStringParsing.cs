// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

public sealed partial class ElasticsearchDataStoreProvider
{
    private static ElasticsearchConnectionInfo ParseConnectionString(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Elasticsearch connection string is empty.");
        }

        // If the connection string looks like a URI, parse directly
        if (!connectionString.Contains('=') && !connectionString.Contains(';'))
        {
            return ParseUriConnectionString(connectionString);
        }

        return ParseKeyValueConnectionString(connectionString);
    }

    private static ElasticsearchConnectionInfo ParseUriConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid Elasticsearch endpoint URI: '{connectionString}'.");
        }

        var baseUri = new Uri(uri.GetLeftPart(UriPartial.Authority));
        var path = uri.AbsolutePath.Trim('/');
        var defaultIndex = path.IsNullOrWhiteSpace() ? null : path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        string? username = null;
        string? password = null;

        if (uri.UserInfo.HasValue())
        {
            var parts = uri.UserInfo.Split(':', 2);
            username = Uri.UnescapeDataString(parts[0]);
            if (parts.Length > 1)
            {
                password = Uri.UnescapeDataString(parts[1]);
            }
        }

        return new ElasticsearchConnectionInfo(
            EnsureTrailingSlash(baseUri),
            defaultIndex,
            username,
            password,
            ApiKey: null,
            ApiKeyId: null,
            ApiKeySecret: null,
            DisableCertificateValidation: false,
            CertificateFingerprint: null,
            Timeout: TimeSpan.FromSeconds(60));
    }

    private static ElasticsearchConnectionInfo ParseKeyValueConnectionString(string connectionString)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kvp = part.Split('=', 2);
            if (kvp.Length != 2)
            {
                continue;
            }

            values[kvp[0]] = kvp[1];
        }

        if (!values.TryGetValue("Endpoint", out var endpoint) &&
            !values.TryGetValue("Url", out endpoint) &&
            !values.TryGetValue("Host", out endpoint))
        {
            throw new InvalidOperationException("Elasticsearch connection string must include 'Endpoint'.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid Elasticsearch endpoint URI: '{endpoint}'.");
        }

        var baseUri = EnsureTrailingSlash(new Uri(uri.GetLeftPart(UriPartial.Authority)));

        values.TryGetValue("DefaultIndex", out var defaultIndex);

        values.TryGetValue("Username", out var username);
        values.TryGetValue("Password", out var password);
        values.TryGetValue("ApiKey", out var apiKey);
        values.TryGetValue("ApiKeyId", out var apiKeyId);
        values.TryGetValue("ApiKeySecret", out var apiKeySecret);
        values.TryGetValue("CertificateFingerprint", out var fingerprintRaw);
        values.TryGetValue("DisableCertificateValidation", out var disableCertRaw);
        values.TryGetValue("TimeoutSeconds", out var timeoutRaw);

        var disableCert = disableCertRaw.HasValue() &&
                          bool.TryParse(disableCertRaw, out var disable) &&
                          disable;

        var fingerprint = NormalizeFingerprint(fingerprintRaw);

        var timeout = TimeSpan.FromSeconds(60);
        if (timeoutRaw.HasValue() && int.TryParse(timeoutRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutSeconds) && timeoutSeconds > 0)
        {
            timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        return new ElasticsearchConnectionInfo(
            baseUri,
            defaultIndex.IsNullOrWhiteSpace() ? null : defaultIndex,
            username,
            password,
            apiKey.IsNullOrWhiteSpace() ? null : apiKey,
            apiKeyId.IsNullOrWhiteSpace() ? null : apiKeyId,
            apiKeySecret.IsNullOrWhiteSpace() ? null : apiKeySecret,
            disableCert,
            fingerprint,
            timeout);
    }

    private static string? NormalizeFingerprint(string? fingerprint)
    {
        if (fingerprint.IsNullOrWhiteSpace())
        {
            return null;
        }

        return fingerprint.Replace(":", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (!uri.OriginalString.EndsWith("/", StringComparison.Ordinal))
        {
            return new Uri(uri.OriginalString + "/");
        }

        return uri;
    }
}
