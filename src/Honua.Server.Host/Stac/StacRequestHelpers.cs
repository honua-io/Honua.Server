// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Query;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Stac;

internal static class StacRequestHelpers
{
    public static bool IsStacEnabled(IHonuaConfigurationService configurationService)
    {
        return configurationService.Current.Services.Stac.Enabled;
    }

    /// <summary>
    /// Builds the base URI for STAC endpoints using validated host headers.
    /// SECURITY FIX: Uses RequestLinkHelper to safely resolve the host, which validates
    /// forwarded headers from trusted proxies to prevent host header injection attacks.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The base URI for STAC endpoints.</returns>
    public static Uri BuildBaseUri(HttpRequest request)
    {
        // SECURITY FIX: Use RequestLinkHelper which validates forwarded headers
        // from trusted proxies to prevent host header injection attacks (CWE-290)
        // Instead of directly using request.Host which can be spoofed
        var baseUrl = request.BuildAbsoluteUrl("/");

        // Parse the validated base URL
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        // Fallback: Build URI from request (should not happen if RequestLinkHelper works correctly)
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
        var builder = new UriBuilder(request.Scheme, request.Host.Host)
        {
            Path = pathBase,
            Port = request.Host.Port ?? (request.Scheme.EqualsIgnoreCase("https") ? 443 : 80)
        };

        return builder.Uri;
    }

    /// <summary>
    /// Formats STAC validation errors into a user-friendly message.
    /// Used by both StacCollectionService and StacItemService to ensure consistent error formatting.
    /// </summary>
    public static string FormatValidationErrors(IReadOnlyList<StacValidationError> errors)
    {
        if (errors.Count == 0)
        {
            return "Validation failed with no specific errors.";
        }

        if (errors.Count == 1)
        {
            return errors[0].ToString();
        }

        // For multiple errors, format as numbered list
        var message = new StringBuilder("Validation failed with the following errors:\n");
        for (int i = 0; i < errors.Count; i++)
        {
            message.Append($"{i + 1}. {errors[i]}\n");
        }
        return message.ToString().TrimEnd();
    }

    /// <summary>
    /// Attempts to extract the "id" field from a STAC JSON object.
    /// Returns false if the id is missing, not a string, or whitespace.
    /// </summary>
    public static bool TryGetId(JsonObject json, out string? id)
    {
        id = null;
        try
        {
            var node = json["id"];
            if (node is null || node.GetValueKind() != JsonValueKind.String)
            {
                return false;
            }

            id = node.GetValue<string>();
            return !string.IsNullOrWhiteSpace(id);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalizes a limit parameter for STAC searches.
    /// Applies default limit (10) and enforces maximum limit (1000).
    /// Delegates to QueryParameterHelper for consistent parsing logic.
    /// </summary>
    public static int NormalizeLimit(int? limit)
    {
        const int defaultLimit = 10;
        const int maxLimit = 1000;

        if (!limit.HasValue || limit.Value <= 0)
        {
            return defaultLimit;
        }

        return Math.Min(limit.Value, maxLimit);
    }

    /// <summary>
    /// Parses a limit parameter from a string value for STAC searches.
    /// Applies default limit (10) and enforces maximum limit (1000).
    /// </summary>
    public static (int Value, string? Error) ParseLimit(string? raw)
    {
        const int defaultLimit = 10;
        const int maxLimit = 1000;

        var (limit, error) = QueryParameterHelper.ParseLimit(raw, serviceMax: maxLimit, layerMax: null, fallback: defaultLimit);
        return (limit ?? defaultLimit, error);
    }
}
