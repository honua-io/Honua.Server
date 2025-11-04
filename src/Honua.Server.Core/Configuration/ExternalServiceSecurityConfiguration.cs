// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration for external service authentication credentials.
/// Allows multiple services to reference shared authentication profiles.
/// NOTE: CORS configuration is handled via metadata.json (server.cors section), not here.
/// </summary>
public sealed class ExternalServiceSecurityConfiguration
{
    public static ExternalServiceSecurityConfiguration Default => new();

    /// <summary>
    /// Named security profiles that can be referenced by external service configurations.
    /// </summary>
    public Dictionary<string, SecurityProfile> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Security profile containing authentication credentials for external services.
/// </summary>
public sealed class SecurityProfile
{
    /// <summary>
    /// Type of authentication (e.g., "token", "api-key", "oauth2").
    /// </summary>
    public string Type { get; init; } = "token";

    /// <summary>
    /// Authentication token or API key.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Additional authentication parameters (e.g., username, client_id, etc.).
    /// </summary>
    public Dictionary<string, string>? Parameters { get; init; }

    /// <summary>
    /// Optional description of this security profile.
    /// </summary>
    public string? Description { get; init; }
}
