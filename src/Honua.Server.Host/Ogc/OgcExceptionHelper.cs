// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Http;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Helper for creating OGC-compliant exception responses for WMS and WFS services.
/// This class now delegates to ApiErrorResponse.OgcXml for consistency across the application.
/// </summary>
[Obsolete("Use ApiErrorResponse.OgcXml instead for new code. This class is maintained for backward compatibility.")]
internal static class OgcExceptionHelper
{
    /// <summary>
    /// Creates a WMS ServiceExceptionReport response.
    /// </summary>
    /// <param name="code">The exception code (e.g., "InvalidParameterValue", "MissingParameterValue").</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="version">The WMS version (default: "1.3.0").</param>
    /// <returns>An IResult containing the XML exception response.</returns>
    public static IResult CreateWmsException(string code, string message, string version = "1.3.0")
    {
        return ApiErrorResponse.OgcXml.WmsException(code, message, version);
    }

    /// <summary>
    /// Creates a WFS ExceptionReport response (OWS Common format).
    /// </summary>
    /// <param name="code">The exception code (e.g., "InvalidParameterValue", "OperationParsingFailed").</param>
    /// <param name="locator">The parameter or location that caused the error (optional).</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="version">The exception report version (default: "2.0.0").</param>
    /// <returns>An IResult containing the XML exception response.</returns>
    public static IResult CreateWfsException(string code, string? locator, string message, string version = "2.0.0")
    {
        return ApiErrorResponse.OgcXml.WfsException(code, locator, message, version);
    }
}
