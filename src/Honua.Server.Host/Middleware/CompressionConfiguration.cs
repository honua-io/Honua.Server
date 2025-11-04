// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Configuration for response compression to reduce bandwidth usage.
/// Implements Brotli (preferred) and Gzip compression.
/// </summary>
public static class CompressionConfiguration
{
    public static IServiceCollection AddHonuaResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            // Enable compression for HTTPS (mitigates BREACH attack with random padding)
            options.EnableForHttps = true;

            // Add compression providers (Brotli first, then Gzip)
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            // MIME types to compress
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                // OGC/geospatial formats
                "application/geo+json",
                "application/vnd.geo+json",
                "application/gml+xml",
                "application/vnd.ogc.wfs+xml",
                "application/vnd.ogc.wms+xml",
                "application/vnd.ogc.wmts+xml",

                // OpenRosa/XForms
                "text/xml",
                "application/xhtml+xml",

                // GeoServices
                "application/x-esri-model-definition+json",

                // Common web formats
                "application/json",
                "text/plain",
                "text/css",
                "text/javascript",
                "application/javascript",
                "text/csv",

                // Image formats (SVG only - raster already compressed)
                "image/svg+xml"
            });
        });

        // Configure Brotli compression
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            // Quality 4 = good balance between compression ratio and speed
            // (1 = fastest, 11 = best compression)
            options.Level = CompressionLevel.Optimal;
        });

        // Configure Gzip compression (fallback for older clients)
        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        return services;
    }
}
