// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.LocationServices.Providers.AwsLocation;

/// <summary>
/// Helper class for AWS Signature Version 4 authentication.
/// Reference: https://docs.aws.amazon.com/general/latest/gr/sigv4_signing.html
/// </summary>
internal class AwsSignatureHelper
{
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string _region;
    private readonly string _service;

    public AwsSignatureHelper(string accessKeyId, string secretAccessKey, string region, string service)
    {
        _accessKeyId = accessKeyId ?? throw new ArgumentNullException(nameof(accessKeyId));
        _secretAccessKey = secretAccessKey ?? throw new ArgumentNullException(nameof(secretAccessKey));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public Task<HttpRequestMessage> CreateSignedGetRequestAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(url);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        SignRequest(request, string.Empty);
        return Task.FromResult(request);
    }

    public Task<HttpRequestMessage> CreateSignedPostRequestAsync(
        string url,
        string jsonBody,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(url);
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        SignRequest(request, jsonBody);
        return Task.FromResult(request);
    }

    private void SignRequest(HttpRequestMessage request, string payload)
    {
        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        // Add required headers
        request.Headers.Add("X-Amz-Date", amzDate);
        request.Headers.Host = request.RequestUri!.Host;

        // Create canonical request
        var canonicalUri = request.RequestUri.AbsolutePath;
        var canonicalQueryString = GetCanonicalQueryString(request.RequestUri);
        var canonicalHeaders = GetCanonicalHeaders(request);
        var signedHeaders = GetSignedHeaders(request);
        var payloadHash = ComputeSha256Hash(payload);

        var canonicalRequest = $"{request.Method}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        // Create string to sign
        var credentialScope = $"{dateStamp}/{_region}/{_service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{ComputeSha256Hash(canonicalRequest)}";

        // Calculate signature
        var signingKey = GetSignatureKey(dateStamp);
        var signature = ComputeHmacSha256(signingKey, stringToSign);

        // Add authorization header
        var authorizationHeader = $"AWS4-HMAC-SHA256 Credential={_accessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={BytesToHex(signature)}";
        request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
    }

    private string GetCanonicalQueryString(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Query))
        {
            return string.Empty;
        }

        var queryParams = uri.Query.TrimStart('?')
            .Split('&')
            .Select(p => p.Split('='))
            .OrderBy(p => p[0])
            .Select(p => $"{Uri.EscapeDataString(p[0])}={Uri.EscapeDataString(p.Length > 1 ? p[1] : string.Empty)}");

        return string.Join("&", queryParams);
    }

    private string GetCanonicalHeaders(HttpRequestMessage request)
    {
        var headers = request.Headers
            .OrderBy(h => h.Key.ToLowerInvariant())
            .Select(h => $"{h.Key.ToLowerInvariant()}:{string.Join(",", h.Value.Select(v => v.Trim()))}\n");

        return string.Concat(headers);
    }

    private string GetSignedHeaders(HttpRequestMessage request)
    {
        var headers = request.Headers
            .Select(h => h.Key.ToLowerInvariant())
            .OrderBy(h => h);

        return string.Join(";", headers);
    }

    private byte[] GetSignatureKey(string dateStamp)
    {
        var kDate = ComputeHmacSha256(Encoding.UTF8.GetBytes($"AWS4{_secretAccessKey}"), dateStamp);
        var kRegion = ComputeHmacSha256(kDate, _region);
        var kService = ComputeHmacSha256(kRegion, _service);
        var kSigning = ComputeHmacSha256(kService, "aws4_request");
        return kSigning;
    }

    private static string ComputeSha256Hash(string data)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return BytesToHex(bytes);
    }

    private static byte[] ComputeHmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string BytesToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
