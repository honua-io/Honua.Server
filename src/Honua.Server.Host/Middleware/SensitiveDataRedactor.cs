// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Service for redacting sensitive data from logs to prevent credential leakage.
/// </summary>
public sealed class SensitiveDataRedactor
{
    private const string RedactedValue = "***REDACTED***";
    private readonly SensitiveDataRedactionOptions options;
    private readonly HashSet<string> sensitiveFieldsLower;
    private readonly List<Regex> sensitivePatterns;

    public SensitiveDataRedactor(SensitiveDataRedactionOptions options)
    {
        this.options = Guard.NotNull(options);

        // Pre-process field names to lowercase for case-insensitive matching
        this.sensitiveFieldsLower = new HashSet<string>(
            this.options.SensitiveFieldNames.Select(f => f.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        // Compile regex patterns for efficient matching
        this.sensitivePatterns = this.options.SensitiveFieldPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();
    }

    /// <summary>
    /// Redacts sensitive values from a dictionary of key-value pairs (headers, query params, etc.).
    /// </summary>
    public IDictionary<string, string>? RedactDictionary(IDictionary<string, string>? data)
    {
        if (data.IsNullOrEmpty())
            return data;

        var redacted = new Dictionary<string, string>(data.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in data)
        {
            redacted[key] = IsSensitiveField(key) ? RedactedValue : value;
        }

        return redacted;
    }

    /// <summary>
    /// Redacts sensitive values from a dictionary with multiple values per key.
    /// </summary>
    public IDictionary<string, IEnumerable<string>>? RedactMultiValueDictionary(
        IDictionary<string, IEnumerable<string>>? data)
    {
        if (data.IsNullOrEmpty())
            return data;

        var redacted = new Dictionary<string, IEnumerable<string>>(
            data.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (key, values) in data)
        {
            redacted[key] = IsSensitiveField(key)
                ? new[] { RedactedValue }
                : values;
        }

        return redacted;
    }

    /// <summary>
    /// Redacts sensitive fields from a JSON string.
    /// </summary>
    public string RedactJson(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return jsonContent;

        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var redacted = RedactJsonElement(document.RootElement);
            return JsonSerializer.Serialize(redacted, JsonSerializerOptionsRegistry.Web);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return as-is (it's not valid JSON)
            return jsonContent;
        }
    }

    /// <summary>
    /// Redacts sensitive query string parameters from a query string.
    /// </summary>
    public string RedactQueryString(string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return queryString;

        // Remove leading '?' if present
        var query = queryString.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
            return queryString;

        var parameters = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var redactedParams = new List<string>(parameters.Length);

        foreach (var param in parameters)
        {
            var parts = param.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0];
                var value = parts[1];

                if (IsSensitiveField(key))
                {
                    redactedParams.Add($"{key}={RedactedValue}");
                }
                else
                {
                    redactedParams.Add(param);
                }
            }
            else
            {
                redactedParams.Add(param);
            }
        }

        var result = string.Join("&", redactedParams);
        return queryString.StartsWith('?') ? $"?{result}" : result;
    }

    /// <summary>
    /// Checks if a field name is considered sensitive.
    /// </summary>
    public bool IsSensitiveField(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return false;

        var lowerFieldName = fieldName.ToLowerInvariant();

        // Check exact matches
        if (this.sensitiveFieldsLower.Contains(lowerFieldName))
            return true;

        // Check regex patterns
        foreach (var pattern in this.sensitivePatterns)
        {
            if (pattern.IsMatch(fieldName))
                return true;
        }

        return false;
    }

    private object RedactJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    if (IsSensitiveField(property.Name))
                    {
                        obj[property.Name] = RedactedValue;
                    }
                    else
                    {
                        obj[property.Name] = RedactJsonElement(property.Value);
                    }
                }
                return obj;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(RedactJsonElement)
                    .ToList();

            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                if (element.TryGetDouble(out var doubleValue))
                    return doubleValue;
                return element.GetRawText();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null!;

            default:
                return element.GetRawText();
        }
    }
}

/// <summary>
/// Configuration options for sensitive data redaction.
/// </summary>
public sealed class SensitiveDataRedactionOptions
{
    /// <summary>
    /// List of field names that should be redacted (case-insensitive).
    /// </summary>
    public HashSet<string> SensitiveFieldNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // Authentication & Authorization
        "authorization",
        "auth",
        "authenticate",
        "bearer",
        "cookie",
        "set-cookie",

        // API Keys & Tokens
        "api_key",
        "apikey",
        "api-key",
        "x-api-key",
        "access_token",
        "accesstoken",
        "access-token",
        "refresh_token",
        "refreshtoken",
        "refresh-token",
        "token",
        "auth_token",
        "authtoken",
        "auth-token",
        "id_token",
        "idtoken",
        "id-token",
        "session_token",
        "sessiontoken",
        "session-token",
        "csrf_token",
        "csrftoken",
        "csrf-token",
        "x-csrf-token",

        // Passwords & Secrets
        "password",
        "passwd",
        "pwd",
        "pass",
        "secret",
        "secret_key",
        "secretkey",
        "secret-key",
        "client_secret",
        "clientsecret",
        "client-secret",
        "private_key",
        "privatekey",
        "private-key",
        "encryption_key",
        "encryptionkey",
        "encryption-key",

        // Credentials
        "credentials",
        "credential",
        "key",
        "apiSecret",
        "api_secret",
        "api-secret",

        // Common sensitive fields
        "ssn",
        "social_security_number",
        "credit_card",
        "creditcard",
        "card_number",
        "cardnumber",
        "cvv",
        "pin",
        "security_code",
        "securitycode",

        // OAuth & OIDC
        "code",
        "state",
        "nonce",
        "code_verifier",
        "code_challenge",
    };

    /// <summary>
    /// Regex patterns for matching sensitive field names.
    /// </summary>
    public List<string> SensitiveFieldPatterns { get; set; } = new()
    {
        @".*password.*",
        @".*secret.*",
        @".*token.*",
        @".*api[_-]?key.*",
        @".*auth.*",
        @".*credential.*",
        @".*private[_-]?key.*",
        @".*access[_-]?key.*",
    };

    /// <summary>
    /// Whether to enable JSON body redaction (default: true).
    /// May impact performance for large request/response bodies.
    /// </summary>
    public bool RedactJsonBodies { get; set; } = true;

    /// <summary>
    /// Whether to enable query string redaction (default: true).
    /// </summary>
    public bool RedactQueryStrings { get; set; } = true;

    /// <summary>
    /// Whether to enable header redaction (default: true).
    /// </summary>
    public bool RedactHeaders { get; set; } = true;
}
