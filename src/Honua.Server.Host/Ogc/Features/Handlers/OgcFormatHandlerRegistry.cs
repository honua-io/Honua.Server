// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Ogc.Features.Handlers;

/// <summary>
/// Provides access to registered OGC Items format handlers.
/// Handlers are registered at application startup and retrieved by format type.
/// </summary>
public interface IOgcFormatHandlerRegistry
{
    /// <summary>
    /// Gets the format handler for the specified format.
    /// </summary>
    /// <param name="format">The OGC response format to get a handler for.</param>
    /// <returns>The handler for the format, or null if not registered.</returns>
    IOgcItemsFormatHandler? GetHandler(OgcSharedHandlers.OgcResponseFormat format);

    /// <summary>
    /// Determines whether a handler is registered for the specified format.
    /// </summary>
    /// <param name="format">The OGC response format to check.</param>
    /// <returns>True if a handler is registered; false otherwise.</returns>
    bool IsSupported(OgcSharedHandlers.OgcResponseFormat format);

    /// <summary>
    /// Gets all registered format handlers.
    /// </summary>
    /// <returns>A read-only collection of all registered handlers.</returns>
    IReadOnlyList<IOgcItemsFormatHandler> GetAllHandlers();

    /// <summary>
    /// Gets all supported formats.
    /// </summary>
    /// <returns>A read-only list of all supported formats.</returns>
    IReadOnlyList<OgcSharedHandlers.OgcResponseFormat> GetSupportedFormats();
}

/// <summary>
/// Default implementation of the format handler registry.
/// Handlers are registered via dependency injection during application startup.
/// </summary>
public sealed class OgcFormatHandlerRegistry : IOgcFormatHandlerRegistry
{
    private readonly IReadOnlyDictionary<OgcSharedHandlers.OgcResponseFormat, IOgcItemsFormatHandler> handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="OgcFormatHandlerRegistry"/> class.
    /// </summary>
    /// <param name="handlers">
    /// The collection of format handlers to register. Handlers are typically injected
    /// via dependency injection as an IEnumerable{IOgcItemsFormatHandler}.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if handlers is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if multiple handlers are registered for the same format.
    /// </exception>
    public OgcFormatHandlerRegistry(IEnumerable<IOgcItemsFormatHandler> handlers)
    {
        Guard.NotNull(handlers);

        var handlerMap = new Dictionary<OgcSharedHandlers.OgcResponseFormat, IOgcItemsFormatHandler>();

        foreach (var handler in handlers)
        {
            if (handler == null)
            {
                continue;
            }

            if (handlerMap.ContainsKey(handler.Format))
            {
                throw new InvalidOperationException(
                    $"Multiple handlers registered for format '{handler.Format}'. " +
                    $"Each format must have exactly one handler.");
            }

            handlerMap[handler.Format] = handler;
        }

        this.handlers = handlerMap;
    }

    /// <inheritdoc/>
    public IOgcItemsFormatHandler? GetHandler(OgcSharedHandlers.OgcResponseFormat format)
    {
        return this.handlers.TryGetValue(format, out var handler) ? handler : null;
    }

    /// <inheritdoc/>
    public bool IsSupported(OgcSharedHandlers.OgcResponseFormat format)
    {
        return this.handlers.ContainsKey(format);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IOgcItemsFormatHandler> GetAllHandlers()
    {
        return this.handlers.Values.ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<OgcSharedHandlers.OgcResponseFormat> GetSupportedFormats()
    {
        return this.handlers.Keys.ToList();
    }

    /// <summary>
    /// Creates an empty registry with no handlers registered.
    /// This is useful for testing or initialization scenarios where handlers
    /// will be added later, or when no handlers are available yet.
    /// </summary>
    /// <returns>A new registry with no handlers.</returns>
    public static IOgcFormatHandlerRegistry CreateEmpty()
    {
        return new OgcFormatHandlerRegistry(Array.Empty<IOgcItemsFormatHandler>());
    }
}

/// <summary>
/// Extension methods for working with the format handler registry.
/// </summary>
public static class OgcFormatHandlerRegistryExtensions
{
    /// <summary>
    /// Gets the handler for the specified format, throwing an exception if not found.
    /// </summary>
    /// <param name="registry">The format handler registry.</param>
    /// <param name="format">The format to get a handler for.</param>
    /// <returns>The handler for the specified format.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no handler is registered for the format.
    /// </exception>
    public static IOgcItemsFormatHandler GetRequiredHandler(
        this IOgcFormatHandlerRegistry registry,
        OgcSharedHandlers.OgcResponseFormat format)
    {
        Guard.NotNull(registry);

        var handler = registry.GetHandler(format);
        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No format handler registered for format '{format}'. " +
                $"Ensure the handler is registered in the dependency injection container.");
        }

        return handler;
    }

    /// <summary>
    /// Tries to get and validate a handler for the specified format.
    /// </summary>
    /// <param name="registry">The format handler registry.</param>
    /// <param name="format">The format to get a handler for.</param>
    /// <param name="query">The feature query to validate.</param>
    /// <param name="requestedCrs">The requested coordinate reference system.</param>
    /// <param name="context">The format context.</param>
    /// <param name="handler">
    /// When this method returns, contains the handler if found and valid; otherwise, null.
    /// </param>
    /// <param name="validationResult">
    /// When this method returns, contains the validation result.
    /// </param>
    /// <returns>True if a valid handler was found; false otherwise.</returns>
    public static bool TryGetValidatedHandler(
        this IOgcFormatHandlerRegistry registry,
        OgcSharedHandlers.OgcResponseFormat format,
        Core.Query.FeatureQuery query,
        string? requestedCrs,
        FormatContext context,
        out IOgcItemsFormatHandler? handler,
        out ValidationResult validationResult)
    {
        Guard.NotNull(registry);
        Guard.NotNull(query);
        Guard.NotNull(context);

        handler = registry.GetHandler(format);
        if (handler == null)
        {
            validationResult = ValidationResult.Failure(
                $"Format '{format}' is not supported or no handler is registered.");
            return false;
        }

        validationResult = handler.Validate(query, requestedCrs, context);
        return validationResult.IsValid;
    }
}
