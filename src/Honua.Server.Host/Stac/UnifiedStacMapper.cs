// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Http;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Stac;

/// <summary>
/// Maps Honua metadata to STAC collections and items for both vector and raster datasets.
/// Provides unified STAC support across all data types.
/// </summary>
/// <remarks>
/// <para><strong>XSS Prevention - Defense in Depth:</strong></para>
/// <para>
/// This mapper implements TWO layers of XSS protection:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <strong>Layer 1: StacTextSanitizer (Primary Defense)</strong> - All user-provided text fields
///       (layer.Title, layer.Description, keywords, provider names, URLs, AdditionalProperties, etc.)
///       are sanitized using StacTextSanitizer.Sanitize() which:
///       - HTML-encodes all content using WebUtility.HtmlEncode
///       - Validates for dangerous patterns (script tags, event handlers, javascript: URIs)
///       - Throws InvalidOperationException if dangerous patterns are detected
///       - Validates and sanitizes URLs to prevent javascript:, data:text/html, and vbscript: URIs
///       - Prevents overwriting reserved STAC fields via AdditionalProperties
///     </description>
///   </item>
///   <item>
///     <description>
///       <strong>Layer 2: SecureOutputSanitizationFilter (Secondary Defense)</strong> - A global MVC filter
///       registered in ServiceCollectionExtensions.cs that automatically sanitizes ALL ObjectResult responses
///       before serialization. This provides defense-in-depth protection even if the primary sanitization is bypassed.
///       The filter recursively HTML-encodes string properties and removes script tags/event handlers.
///     </description>
///   </item>
/// </list>
/// <para>
/// <strong>Why Two Layers?</strong>
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       StacTextSanitizer provides fail-fast validation at the mapping layer, rejecting dangerous content early
///     </description>
///   </item>
///   <item>
///     <description>
///       SecureOutputSanitizationFilter provides automatic protection for all responses, even if a code path
///       bypasses the mapper or new fields are added without sanitization
///     </description>
///   </item>
///   <item>
///     <description>
///       This defense-in-depth approach ensures XSS protection is maintained even during refactoring or when
///       new developers add features without being aware of the security requirements
///     </description>
///   </item>
/// </list>
/// <para>
/// <strong>Testing:</strong> See UnifiedStacMapperSecurityTests.cs for comprehensive XSS prevention tests.
/// </para>
/// </remarks>
internal static class UnifiedStacMapper
{
    /// <summary>
    /// Creates a STAC Collection from a layer definition with STAC metadata.
    /// </summary>
    public static JsonElement CreateCollectionFromLayer(
        LayerDefinition layer,
        ServiceDefinition service,
        HttpRequest request)
    {
        Guard.NotNull(layer);
        Guard.NotNull(service);
        Guard.NotNull(request);

        var stac = layer.Stac;
        if (stac?.Enabled != true)
        {
            throw new InvalidOperationException($"Layer '{layer.Id}' does not have STAC enabled.");
        }

        var collectionId = stac.CollectionId ?? layer.Id;

        var collection = new Dictionary<string, object>
        {
            ["type"] = "Collection",
            ["stac_version"] = "1.0.0",
            ["id"] = StacTextSanitizer.Sanitize(collectionId, allowEmpty: false)!,
            ["title"] = StacTextSanitizer.Sanitize(layer.Title ?? layer.Id, allowEmpty: false)!,
            ["description"] = StacTextSanitizer.Sanitize(
                layer.Description ?? $"STAC collection for {layer.Title ?? layer.Id}",
                allowEmpty: false)!,
            ["license"] = StacTextSanitizer.Sanitize(stac.License ?? "proprietary", allowEmpty: false)!,
        };

        // Keywords
        if (layer.Keywords?.Count > 0)
        {
            collection["keywords"] = layer.Keywords
                .Select(k => StacTextSanitizer.Sanitize(k, allowEmpty: false)!)
                .ToList();
        }

        // Providers
        if (stac.Providers?.Count > 0)
        {
            collection["providers"] = stac.Providers.Select(p => new Dictionary<string, object>
            {
                ["name"] = StacTextSanitizer.Sanitize(p.Name, allowEmpty: false)!,
                ["description"] = StacTextSanitizer.Sanitize(p.Description ?? "", allowEmpty: true)!,
                ["roles"] = p.Roles?.Select(r => StacTextSanitizer.Sanitize(r, allowEmpty: false)!).ToList()
                    ?? new List<string>(),
                ["url"] = StacTextSanitizer.SanitizeUrl(p.Url ?? "")
            }).ToList();
        }

        // Extent
        var extent = CreateExtent(layer.Extent);
        collection["extent"] = extent;

        // Links - using RequestLinkHelper for consistent URL generation
        var links = new List<Dictionary<string, object>>
        {
            new() {
                ["rel"] = "self",
                ["type"] = "application/json",
                ["href"] = request.GenerateCollectionLink(collectionId, "/stac/collections")
            },
            new() {
                ["rel"] = "root",
                ["type"] = "application/json",
                ["href"] = request.GenerateRootLink("/stac")
            },
            new() {
                ["rel"] = "items",
                ["type"] = "application/geo+json",
                ["href"] = request.BuildAbsoluteUrl($"/stac/collections/{Uri.EscapeDataString(collectionId)}/items")
            }
        };

        // Add layer-specific links
        if (layer.Links?.Count > 0)
        {
            foreach (var link in layer.Links)
            {
                links.Add(new Dictionary<string, object>
                {
                    ["rel"] = StacTextSanitizer.Sanitize(link.Rel ?? "related", allowEmpty: false)!,
                    ["href"] = StacTextSanitizer.SanitizeUrl(link.Href),
                    ["type"] = StacTextSanitizer.Sanitize(link.Type ?? "text/html", allowEmpty: false)!,
                    ["title"] = StacTextSanitizer.Sanitize(link.Title ?? "", allowEmpty: true)!
                });
            }
        }

        collection["links"] = links;

        // Collection assets (metadata, thumbnails, etc.)
        if (stac.Assets?.Count > 0)
        {
            collection["assets"] = CreateAssetsDictionary(stac.Assets);
        }

        // Item assets (template for item-level assets)
        if (stac.ItemAssets?.Count > 0)
        {
            collection["item_assets"] = CreateAssetsDictionary(stac.ItemAssets);
        }

        // Summaries
        if (stac.Summaries?.Count > 0)
        {
            collection["summaries"] = new Dictionary<string, object>(stac.Summaries);
        }

        // STAC extensions
        if (stac.StacExtensions?.Count > 0)
        {
            collection["stac_extensions"] = stac.StacExtensions.ToList();
        }

        // Additional properties
        if (stac.AdditionalProperties?.Count > 0)
        {
            var sanitizedProps = StacTextSanitizer.ValidateAdditionalProperties(stac.AdditionalProperties);
            foreach (var kvp in sanitizedProps)
            {
                if (!collection.ContainsKey(kvp.Key))
                {
                    collection[kvp.Key] = kvp.Value;
                }
            }
        }

        return JsonSerializer.SerializeToElement(collection);
    }

    /// <summary>
    /// Creates a STAC Collection from a raster dataset definition with STAC metadata.
    /// </summary>
    public static JsonElement CreateCollectionFromRaster(
        RasterDatasetDefinition raster,
        HttpRequest request)
    {
        Guard.NotNull(raster);
        Guard.NotNull(request);

        var stac = raster.Stac;
        if (stac?.Enabled != true)
        {
            throw new InvalidOperationException($"Raster dataset '{raster.Id}' does not have STAC enabled.");
        }

        var collectionId = stac.CollectionId ?? raster.Id;

        var collection = new Dictionary<string, object>
        {
            ["type"] = "Collection",
            ["stac_version"] = "1.0.0",
            ["id"] = StacTextSanitizer.Sanitize(collectionId, allowEmpty: false)!,
            ["title"] = StacTextSanitizer.Sanitize(raster.Title ?? raster.Id, allowEmpty: false)!,
            ["description"] = StacTextSanitizer.Sanitize(
                raster.Description ?? $"STAC collection for {raster.Title ?? raster.Id}",
                allowEmpty: false)!,
            ["license"] = StacTextSanitizer.Sanitize(stac.License ?? "proprietary", allowEmpty: false)!,
        };

        // Keywords
        if (raster.Keywords?.Count > 0)
        {
            collection["keywords"] = raster.Keywords
                .Select(k => StacTextSanitizer.Sanitize(k, allowEmpty: false)!)
                .ToList();
        }

        // Providers
        if (stac.Providers?.Count > 0)
        {
            collection["providers"] = stac.Providers.Select(p => new Dictionary<string, object>
            {
                ["name"] = StacTextSanitizer.Sanitize(p.Name, allowEmpty: false)!,
                ["description"] = StacTextSanitizer.Sanitize(p.Description ?? "", allowEmpty: true)!,
                ["roles"] = p.Roles?.Select(r => StacTextSanitizer.Sanitize(r, allowEmpty: false)!).ToList()
                    ?? new List<string>(),
                ["url"] = StacTextSanitizer.SanitizeUrl(p.Url ?? "")
            }).ToList();
        }

        // Extent (for rasters, this would come from raster.Extent if available)
        var extent = CreateExtent(raster.Extent);
        collection["extent"] = extent;

        // Links - using RequestLinkHelper for consistent URL generation
        var links = new List<Dictionary<string, object>>
        {
            new() {
                ["rel"] = "self",
                ["type"] = "application/json",
                ["href"] = request.GenerateCollectionLink(collectionId, "/stac/collections")
            },
            new() {
                ["rel"] = "root",
                ["type"] = "application/json",
                ["href"] = request.GenerateRootLink("/stac")
            },
            new() {
                ["rel"] = "items",
                ["type"] = "application/geo+json",
                ["href"] = request.BuildAbsoluteUrl($"/stac/collections/{Uri.EscapeDataString(collectionId)}/items")
            }
        };

        // Note: RasterDatasetDefinition does not have Links property in current model

        collection["links"] = links;

        // Collection assets
        if (stac.Assets?.Count > 0)
        {
            collection["assets"] = CreateAssetsDictionary(stac.Assets);
        }

        // Item assets
        if (stac.ItemAssets?.Count > 0)
        {
            collection["item_assets"] = CreateAssetsDictionary(stac.ItemAssets);
        }

        // Summaries
        if (stac.Summaries?.Count > 0)
        {
            collection["summaries"] = new Dictionary<string, object>(stac.Summaries);
        }

        // STAC extensions
        if (stac.StacExtensions?.Count > 0)
        {
            collection["stac_extensions"] = stac.StacExtensions.ToList();
        }

        // Additional properties
        if (stac.AdditionalProperties?.Count > 0)
        {
            var sanitizedProps = StacTextSanitizer.ValidateAdditionalProperties(stac.AdditionalProperties);
            foreach (var kvp in sanitizedProps)
            {
                if (!collection.ContainsKey(kvp.Key))
                {
                    collection[kvp.Key] = kvp.Value;
                }
            }
        }

        return JsonSerializer.SerializeToElement(collection);
    }

    /// <summary>
    /// Creates a STAC Item from feature data using layer STAC configuration.
    /// </summary>
    public static JsonElement CreateItemFromFeature(
        string featureId,
        Dictionary<string, object> featureProperties,
        object? geometry,
        LayerDefinition layer,
        HttpRequest request)
    {
        Guard.NotNull(layer);
        Guard.NotNull(request);

        var stac = layer.Stac;
        if (stac?.Enabled != true)
        {
            throw new InvalidOperationException($"Layer '{layer.Id}' does not have STAC enabled.");
        }

        var collectionId = stac.CollectionId ?? layer.Id;

        // Generate item ID using template if provided
        var itemId = GenerateItemId(stac.ItemIdTemplate, featureId, featureProperties);

        var item = new Dictionary<string, object>
        {
            ["type"] = "Feature",
            ["stac_version"] = "1.0.0",
            ["id"] = itemId,
            ["collection"] = collectionId,
            ["geometry"] = geometry ?? new { },
            ["properties"] = new Dictionary<string, object>(featureProperties),
        };

        // Links - using RequestLinkHelper for consistent URL generation
        var links = new List<Dictionary<string, object>>
        {
            new() {
                ["rel"] = "self",
                ["type"] = "application/geo+json",
                ["href"] = request.GenerateItemLink(collectionId, itemId, "/stac/collections")
            },
            new() {
                ["rel"] = "collection",
                ["type"] = "application/json",
                ["href"] = request.GenerateCollectionLink(collectionId, "/stac/collections")
            },
            new() {
                ["rel"] = "root",
                ["type"] = "application/json",
                ["href"] = request.GenerateRootLink("/stac")
            }
        };

        item["links"] = links;

        // Item assets from template
        if (stac.ItemAssets?.Count > 0)
        {
            var assets = new Dictionary<string, object>();
            foreach (var (key, assetDef) in stac.ItemAssets)
            {
                var asset = new Dictionary<string, object>
                {
                    ["title"] = StacTextSanitizer.Sanitize(assetDef.Title, allowEmpty: false)!,
                    ["type"] = StacTextSanitizer.Sanitize(assetDef.Type, allowEmpty: false)!
                };

                if (!assetDef.Description.IsNullOrEmpty())
                {
                    asset["description"] = StacTextSanitizer.Sanitize(assetDef.Description, allowEmpty: true)!;
                }

                if (assetDef.Roles?.Count > 0)
                {
                    asset["roles"] = assetDef.Roles
                        .Select(r => StacTextSanitizer.Sanitize(r, allowEmpty: false)!)
                        .ToList();
                }

                // Generate href using item ID if template provided
                if (!assetDef.Href.IsNullOrEmpty())
                {
                    var href = assetDef.Href.Replace("{itemId}", itemId);
                    asset["href"] = StacTextSanitizer.SanitizeUrl(href);
                }

                if (assetDef.AdditionalProperties?.Count > 0)
                {
                    var sanitizedProps = StacTextSanitizer.ValidateAdditionalProperties(assetDef.AdditionalProperties);
                    foreach (var kvp in sanitizedProps)
                    {
                        asset[kvp.Key] = kvp.Value;
                    }
                }

                assets[key] = asset;
            }
            item["assets"] = assets;
        }

        // STAC extensions
        if (stac.StacExtensions?.Count > 0)
        {
            item["stac_extensions"] = stac.StacExtensions.ToList();
        }

        return JsonSerializer.SerializeToElement(item);
    }

    /// <summary>
    /// Generates an item ID from a template and feature properties.
    /// </summary>
    private static string GenerateItemId(string? template, string defaultId, Dictionary<string, object> properties)
    {
        if (template.IsNullOrEmpty())
        {
            return defaultId;
        }

        var result = template;
        foreach (var kvp in properties)
        {
            var placeholder = $"{{{kvp.Key}}}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, kvp.Value?.ToString() ?? "");
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a STAC extent object from Honua extent metadata.
    /// </summary>
    private static Dictionary<string, object> CreateExtent(LayerExtentDefinition? extent)
    {
        var stacExtent = new Dictionary<string, object>
        {
            ["spatial"] = new Dictionary<string, object>
            {
                ["bbox"] = extent?.Bbox != null && extent.Bbox.Count > 0
                    ? extent.Bbox.Select(b => b.Select(v => (object)v).ToList()).ToList()
                    : new List<List<object>> { new() { -180.0, -90.0, 180.0, 90.0 } }
            },
            ["temporal"] = new Dictionary<string, object>
            {
                ["interval"] = extent?.Temporal != null && extent.Temporal.Count > 0
                    ? extent.Temporal.Select(i => new List<object?> { i.Start, i.End }).ToList()
                    : new List<List<object?>> { new() { null, null } }
            }
        };

        return stacExtent;
    }

    /// <summary>
    /// Creates STAC assets dictionary from asset definitions.
    /// </summary>
    private static Dictionary<string, object> CreateAssetsDictionary(IReadOnlyDictionary<string, StacAssetDefinition> assetDefs)
    {
        var assets = new Dictionary<string, object>();

        foreach (var (key, assetDef) in assetDefs)
        {
            var asset = new Dictionary<string, object>
            {
                ["title"] = StacTextSanitizer.Sanitize(assetDef.Title, allowEmpty: false)!,
                ["type"] = StacTextSanitizer.Sanitize(assetDef.Type, allowEmpty: false)!
            };

            if (!assetDef.Description.IsNullOrEmpty())
            {
                asset["description"] = StacTextSanitizer.Sanitize(assetDef.Description, allowEmpty: true)!;
            }

            if (!assetDef.Href.IsNullOrEmpty())
            {
                asset["href"] = StacTextSanitizer.SanitizeUrl(assetDef.Href);
            }

            if (assetDef.Roles?.Count > 0)
            {
                asset["roles"] = assetDef.Roles
                    .Select(r => StacTextSanitizer.Sanitize(r, allowEmpty: false)!)
                    .ToList();
            }

            if (assetDef.AdditionalProperties?.Count > 0)
            {
                var sanitizedProps = StacTextSanitizer.ValidateAdditionalProperties(assetDef.AdditionalProperties);
                foreach (var kvp in sanitizedProps)
                {
                    asset[kvp.Key] = kvp.Value;
                }
            }

            assets[key] = asset;
        }

        return assets;
    }
}
