// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request to authenticate and obtain an access token.
/// </summary>
public sealed record LoginRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("expiration")]
    public double ExpirationMinutes { get; init; } = 480; // 8 hours default

    [JsonPropertyName("f")]
    public string Format { get; init; } = "json";
}

/// <summary>
/// Response from token endpoint containing access token and expiration.
/// </summary>
public sealed record TokenResponse
{
    [JsonPropertyName("token")]
    public required string Token { get; init; }

    [JsonPropertyName("expires")]
    public long Expires { get; init; }

    [JsonPropertyName("ssl")]
    public bool Ssl { get; init; }

    [JsonPropertyName("passwordInfo")]
    public PasswordInfo? PasswordInfo { get; init; }

    /// <summary>
    /// Converts Unix timestamp to DateTimeOffset.
    /// </summary>
    public DateTimeOffset ExpiresAt => DateTimeOffset.FromUnixTimeMilliseconds(Expires);
}

/// <summary>
/// Password expiration information.
/// </summary>
public sealed record PasswordInfo
{
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("daysRemaining")]
    public int? DaysRemaining { get; init; }
}

/// <summary>
/// Error response from authentication endpoint.
/// </summary>
public sealed record AuthErrorResponse
{
    [JsonPropertyName("error")]
    public required AuthError Error { get; init; }
}

public sealed record AuthError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("details")]
    public List<string> Details { get; init; } = new();
}
