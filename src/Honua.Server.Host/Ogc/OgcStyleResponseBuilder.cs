// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Helper for building style response payloads in OGC API handlers.
/// </summary>
public static class OgcStyleResponseBuilder
{
    /// <summary>
    /// Creates an SLD (Styled Layer Descriptor) file response.
    /// </summary>
    /// <param name="style">The style definition to convert to SLD</param>
    /// <param name="title">The title to use in the SLD document</param>
    /// <param name="geometryType">The geometry type for the style</param>
    /// <param name="fileName">Optional filename (defaults to "{styleId}.sld")</param>
    /// <returns>IResult containing the SLD file response</returns>
    public static IResult CreateSldFileResponse(
        StyleDefinition style,
        string title,
        string? geometryType,
        string? fileName = null)
    {
        Guard.NotNull(style);

        var sldXml = StyleFormatConverter.CreateSld(style, title, geometryType);
        var sldBytes = Encoding.UTF8.GetBytes(sldXml);

        var effectiveFileName = fileName ?? $"{style.Id}.sld";

        return Results.File(
            sldBytes,
            contentType: "application/vnd.ogc.sld+xml",
            fileDownloadName: effectiveFileName);
    }
}
