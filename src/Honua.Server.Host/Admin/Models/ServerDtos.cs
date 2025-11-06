// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// CORS configuration DTO
/// </summary>
public sealed class CorsConfigurationDto
{
    public bool Enabled { get; set; }
    public bool AllowAnyOrigin { get; set; }
    public List<string> AllowedOrigins { get; set; } = new();
    public bool AllowAnyMethod { get; set; }
    public List<string> AllowedMethods { get; set; } = new();
    public bool AllowAnyHeader { get; set; }
    public List<string> AllowedHeaders { get; set; } = new();
    public List<string> ExposedHeaders { get; set; } = new();
    public bool AllowCredentials { get; set; }
    public int? MaxAge { get; set; }
}

/// <summary>
/// Request to update CORS configuration
/// </summary>
public sealed class UpdateCorsConfigurationRequest
{
    public required CorsConfigurationDto Cors { get; set; }
}
