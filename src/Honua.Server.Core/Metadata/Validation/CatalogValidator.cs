// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates catalog and server definitions.
/// </summary>
internal static class CatalogValidator
{
    /// <summary>
    /// Validates the catalog and server definitions.
    /// </summary>
    /// <param name="catalog">The catalog definition to validate.</param>
    /// <param name="server">The server definition to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when validation fails.</exception>
    public static void Validate(CatalogDefinition catalog, ServerDefinition server)
    {
        if (catalog.Id.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("Catalog id must be provided.");
        }

        // CORS configuration validation
        if (server.Cors.AllowCredentials && server.Cors.AllowAnyOrigin)
        {
            throw new InvalidDataException("CORS configuration cannot allow credentials when all origins are allowed. Specify explicit origins or disable credential forwarding.");
        }
    }
}
