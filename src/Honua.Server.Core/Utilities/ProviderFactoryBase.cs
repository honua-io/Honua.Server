// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Utilities;

/// <summary>
/// Base class for provider factory implementations that use string-based provider selection.
/// Provides consistent pattern for provider registration, lookup, and error handling.
/// </summary>
/// <typeparam name="TProvider">The provider interface type.</typeparam>
public abstract class ProviderFactoryBase<TProvider> where TProvider : class
{
    private readonly Dictionary<string, Func<TProvider>> _providerFactories;
    private readonly Dictionary<string, HashSet<string>> _aliasMap;
    private readonly string _providerTypeName;

    protected ProviderFactoryBase()
    {
        _providerFactories = new Dictionary<string, Func<TProvider>>(StringComparer.OrdinalIgnoreCase);
        _aliasMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        _providerTypeName = typeof(TProvider).Name;
    }

    /// <summary>
    /// Registers a provider with a primary key and optional aliases.
    /// </summary>
    /// <param name="providerKey">The primary provider key (case-insensitive).</param>
    /// <param name="factory">Factory function to create the provider instance.</param>
    /// <param name="aliases">Optional aliases for the provider (case-insensitive).</param>
    protected void RegisterProvider(string providerKey, Func<TProvider> factory, params string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Provider key cannot be null or whitespace", nameof(providerKey));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        var normalizedKey = NormalizeProviderName(providerKey);
        _providerFactories[normalizedKey] = factory;

        // Register aliases that point to the primary key
        if (aliases is not null && aliases.Length > 0)
        {
            foreach (var alias in aliases.Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                var normalizedAlias = NormalizeProviderName(alias);
                if (!_aliasMap.ContainsKey(normalizedKey))
                {
                    _aliasMap[normalizedKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                _aliasMap[normalizedKey].Add(normalizedAlias);

                // Map alias to primary key
                _providerFactories[normalizedAlias] = factory;
            }
        }
    }

    /// <summary>
    /// Registers a provider instance directly (singleton pattern).
    /// </summary>
    /// <param name="providerKey">The primary provider key.</param>
    /// <param name="instance">The provider instance.</param>
    /// <param name="aliases">Optional aliases.</param>
    protected void RegisterProviderInstance(string providerKey, TProvider instance, params string[] aliases)
    {
        if (instance is null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        RegisterProvider(providerKey, () => instance, aliases);
    }

    /// <summary>
    /// Creates or retrieves a provider instance by name.
    /// </summary>
    /// <param name="providerName">The provider name (case-insensitive).</param>
    /// <returns>The provider instance.</returns>
    /// <exception cref="ArgumentException">If provider name is null or whitespace.</exception>
    /// <exception cref="NotSupportedException">If the provider is not registered.</exception>
    public TProvider CreateProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name must be specified.", nameof(providerName));
        }

        var normalizedName = NormalizeProviderName(providerName);

        if (!_providerFactories.TryGetValue(normalizedName, out var factory))
        {
            var availableProviders = GetAvailableProviders();
            var providersMessage = availableProviders.Length > 0
                ? $"Supported providers: {string.Join(", ", availableProviders)}"
                : "No providers are currently registered.";

            throw new NotSupportedException(
                $"{_providerTypeName} provider '{providerName}' is not supported. {providersMessage}");
        }

        try
        {
            return factory();
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Failed to create {_providerTypeName} provider '{providerName}'. See inner exception for details.",
                ex);
        }
    }

    /// <summary>
    /// Tries to create a provider, returning null if not found.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <returns>The provider instance, or null if not found.</returns>
    public TProvider? TryCreateProvider(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        var normalizedName = NormalizeProviderName(providerName);

        if (!_providerFactories.TryGetValue(normalizedName, out var factory))
        {
            return null;
        }

        try
        {
            return factory();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a provider is registered.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <returns>True if the provider is registered.</returns>
    public bool IsProviderSupported(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return false;
        }

        return _providerFactories.ContainsKey(NormalizeProviderName(providerName));
    }

    /// <summary>
    /// Gets all registered provider keys (primary keys only, not aliases).
    /// </summary>
    public string[] GetAvailableProviders()
    {
        // Return only primary keys, not aliases
        var aliasedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var aliases in _aliasMap.Values)
        {
            foreach (var alias in aliases)
            {
                aliasedKeys.Add(alias);
            }
        }

        return _providerFactories.Keys
            .Where(key => !aliasedKeys.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets all registered provider names including aliases.
    /// </summary>
    public string[] GetAllProviderNames()
    {
        return _providerFactories.Keys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Normalizes a provider name for case-insensitive comparison.
    /// Override this to customize normalization logic.
    /// </summary>
    /// <param name="providerName">The provider name to normalize.</param>
    /// <returns>The normalized provider name.</returns>
    protected virtual string NormalizeProviderName(string providerName)
    {
        return providerName.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the provider type name for error messages.
    /// </summary>
    protected string ProviderTypeName => _providerTypeName;
}

/// <summary>
/// Base class for provider factories that use dependency injection via IServiceProvider.
/// Provides keyed service resolution with consistent error handling.
/// </summary>
/// <typeparam name="TProvider">The provider interface type.</typeparam>
public abstract class DependencyInjectionProviderFactoryBase<TProvider> where TProvider : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _providerTypeName;

    protected DependencyInjectionProviderFactoryBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _providerTypeName = typeof(TProvider).Name;
    }

    /// <summary>
    /// Creates a provider using keyed service resolution.
    /// </summary>
    /// <param name="providerName">The provider name (service key).</param>
    /// <returns>The provider instance.</returns>
    public TProvider CreateProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name must be specified.", nameof(providerName));
        }

        var normalizedName = NormalizeProviderName(providerName);

        try
        {
            return Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions
                .GetRequiredKeyedService<TProvider>(_serviceProvider, normalizedName);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            throw new NotSupportedException(
                $"No {_providerTypeName} provider registered for '{providerName}'.",
                ex);
        }
    }

    /// <summary>
    /// Tries to create a provider, returning null if not found.
    /// </summary>
    public TProvider? TryCreateProvider(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        var normalizedName = NormalizeProviderName(providerName);

        try
        {
            return Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions
                .GetKeyedService<TProvider>(_serviceProvider, normalizedName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes a provider name for lookup.
    /// Override to customize normalization.
    /// </summary>
    protected virtual string NormalizeProviderName(string providerName)
    {
        return providerName.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the service provider for derived classes.
    /// </summary>
    protected IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets the provider type name for error messages.
    /// </summary>
    protected string ProviderTypeName => _providerTypeName;
}
