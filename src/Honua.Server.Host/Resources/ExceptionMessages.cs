// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Localization;

namespace Honua.Server.Host.Resources;

/// <summary>
/// Helper class for generating localized exception messages using resource files.
/// Provides strongly-typed methods for common exception messages with proper formatting.
/// </summary>
public static class ExceptionMessages
{
    /// <summary>
    /// Gets a localized message for a feature not found error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="layerId">Optional layer identifier.</param>
    /// <returns>Localized error message.</returns>
    public static string FeatureNotFound(
        IStringLocalizer<SharedResources> localizer,
        string featureId,
        string? layerId = null)
    {
        return layerId == null
            ? localizer["FeatureNotFound", featureId].Value
            : localizer["FeatureNotFoundInLayer", featureId, layerId].Value;
    }

    /// <summary>
    /// Gets a localized message for a layer not found error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="layerId">Optional layer identifier.</param>
    /// <returns>Localized error message.</returns>
    public static string LayerNotFound(
        IStringLocalizer<SharedResources> localizer,
        string? layerId = null)
    {
        return layerId == null
            ? localizer["LayerNotFound"].Value
            : localizer["LayerNotFoundWithId", layerId].Value;
    }

    /// <summary>
    /// Gets a localized message for a collection not found error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="collectionId">The collection identifier.</param>
    /// <returns>Localized error message.</returns>
    public static string CollectionNotFound(
        IStringLocalizer<SharedResources> localizer,
        string collectionId)
    {
        return localizer["CollectionNotFound", collectionId].Value;
    }

    /// <summary>
    /// Gets a localized message for invalid bounding box error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="bbox">The invalid bounding box value.</param>
    /// <returns>Localized error message.</returns>
    public static string InvalidBoundingBox(
        IStringLocalizer<SharedResources> localizer,
        string bbox)
    {
        return localizer["InvalidBoundingBox", bbox].Value;
    }

    /// <summary>
    /// Gets a localized message for invalid CRS error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="crs">The CRS identifier.</param>
    /// <returns>Localized error message.</returns>
    public static string InvalidCrs(
        IStringLocalizer<SharedResources> localizer,
        string crs)
    {
        return localizer["InvalidCrs", crs].Value;
    }

    /// <summary>
    /// Gets a localized message for invalid format error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="format">The format name.</param>
    /// <returns>Localized error message.</returns>
    public static string InvalidFormat(
        IStringLocalizer<SharedResources> localizer,
        string format)
    {
        return localizer["InvalidFormat", format].Value;
    }

    /// <summary>
    /// Gets a localized message for service unavailable error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string ServiceUnavailable(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["ServiceUnavailable"].Value;
    }

    /// <summary>
    /// Gets a localized message for invalid parameters error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string InvalidParameters(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["InvalidParameters"].Value;
    }

    /// <summary>
    /// Gets a localized message for invalid geometry error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string InvalidGeometry(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["InvalidGeometry"].Value;
    }

    /// <summary>
    /// Gets a localized message for method not allowed error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="method">The HTTP method.</param>
    /// <returns>Localized error message.</returns>
    public static string MethodNotAllowed(
        IStringLocalizer<SharedResources> localizer,
        string method)
    {
        return localizer["MethodNotAllowed", method].Value;
    }

    /// <summary>
    /// Gets a localized message for unsupported media type error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <param name="mediaType">The media type.</param>
    /// <returns>Localized error message.</returns>
    public static string UnsupportedMediaType(
        IStringLocalizer<SharedResources> localizer,
        string mediaType)
    {
        return localizer["UnsupportedMediaType", mediaType].Value;
    }

    /// <summary>
    /// Gets a localized message for data store error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string DataStoreError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["DataStoreError"].Value;
    }

    /// <summary>
    /// Gets a localized message for cache error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string CacheError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["CacheError"].Value;
    }

    /// <summary>
    /// Gets a localized message for metadata error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string MetadataError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["MetadataError"].Value;
    }

    /// <summary>
    /// Gets a localized message for query error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string QueryError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["QueryError"].Value;
    }

    /// <summary>
    /// Gets a localized message for raster error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string RasterError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["RasterError"].Value;
    }

    /// <summary>
    /// Gets a localized message for serialization error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string SerializationError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["SerializationError"].Value;
    }

    /// <summary>
    /// Gets a localized message for unauthorized access error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string UnauthorizedAccess(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["UnauthorizedAccess"].Value;
    }

    /// <summary>
    /// Gets a localized message for forbidden access error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string Forbidden(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["Forbidden"].Value;
    }

    /// <summary>
    /// Gets a localized message for too many requests error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string TooManyRequests(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["TooManyRequests"].Value;
    }

    /// <summary>
    /// Gets a localized message for internal server error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string InternalServerError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["InternalServerError"].Value;
    }

    /// <summary>
    /// Gets a localized message for not implemented error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string NotImplemented(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["NotImplemented"].Value;
    }

    /// <summary>
    /// Gets a localized message for bad request error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string BadRequest(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["BadRequest"].Value;
    }

    /// <summary>
    /// Gets a localized message for conflict error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string ConflictError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["ConflictError"].Value;
    }

    /// <summary>
    /// Gets a localized message for gone error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string GoneError(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["GoneError"].Value;
    }

    /// <summary>
    /// Gets a localized message for precondition failed error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string PreconditionFailed(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["PreconditionFailed"].Value;
    }

    /// <summary>
    /// Gets a localized message for payload too large error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string PayloadTooLarge(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["PayloadTooLarge"].Value;
    }

    /// <summary>
    /// Gets a localized message for URI too long error.
    /// </summary>
    /// <param name="localizer">The string localizer instance.</param>
    /// <returns>Localized error message.</returns>
    public static string UriTooLong(IStringLocalizer<SharedResources> localizer)
    {
        return localizer["UriTooLong"].Value;
    }
}
