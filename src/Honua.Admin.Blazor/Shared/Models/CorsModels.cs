// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// CORS configuration for the admin UI
/// </summary>
public sealed class CorsConfiguration
{
    /// <summary>Whether CORS is enabled</summary>
    public bool Enabled { get; set; }

    /// <summary>Allow requests from any origin (*)</summary>
    public bool AllowAnyOrigin { get; set; }

    /// <summary>List of allowed origins (supports wildcards like https://*.example.com)</summary>
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>Allow any HTTP method</summary>
    public bool AllowAnyMethod { get; set; }

    /// <summary>List of allowed HTTP methods</summary>
    public List<string> AllowedMethods { get; set; } = new();

    /// <summary>Allow any header</summary>
    public bool AllowAnyHeader { get; set; }

    /// <summary>List of allowed headers</summary>
    public List<string> AllowedHeaders { get; set; } = new();

    /// <summary>Headers to expose to the browser</summary>
    public List<string> ExposedHeaders { get; set; } = new();

    /// <summary>Allow credentials (cookies, authorization headers)</summary>
    public bool AllowCredentials { get; set; }

    /// <summary>Preflight cache duration in seconds</summary>
    public int? MaxAge { get; set; }

    /// <summary>
    /// Gets the default CORS configuration (disabled)
    /// </summary>
    public static CorsConfiguration GetDefault()
    {
        return new CorsConfiguration
        {
            Enabled = false,
            AllowAnyOrigin = false,
            AllowAnyMethod = true,
            AllowAnyHeader = true,
            AllowCredentials = false,
            MaxAge = 86400 // 24 hours
        };
    }
}

/// <summary>
/// Request to update CORS configuration
/// </summary>
public sealed class UpdateCorsRequest
{
    public required CorsConfiguration Cors { get; set; }
}

/// <summary>
/// Response with current CORS configuration
/// </summary>
public sealed class CorsConfigurationResponse
{
    public required CorsConfiguration Cors { get; set; }
}
