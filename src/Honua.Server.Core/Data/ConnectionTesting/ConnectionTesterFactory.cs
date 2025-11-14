// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Data.ConnectionTesting;

/// <summary>
/// Factory for creating connection testers based on provider type.
/// Uses dependency injection to resolve the appropriate tester implementation.
/// </summary>
public sealed class ConnectionTesterFactory
{
    private readonly Dictionary<string, IConnectionTester> _testers;

    /// <summary>
    /// Initializes a new instance of ConnectionTesterFactory with all registered testers.
    /// </summary>
    /// <param name="testers">All available connection testers from DI.</param>
    public ConnectionTesterFactory(IEnumerable<IConnectionTester> testers)
    {
        ArgumentNullException.ThrowIfNull(testers);

        // Build lookup dictionary with case-insensitive provider names
        _testers = testers.ToDictionary(
            t => t.ProviderType,
            t => t,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a connection tester for the specified provider type.
    /// </summary>
    /// <param name="providerType">The provider type (e.g., "postgis", "mysql", "sqlserver", "mongodb").</param>
    /// <returns>The connection tester if found, otherwise null.</returns>
    public IConnectionTester? GetTester(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return null;
        }

        return _testers.TryGetValue(providerType, out var tester) ? tester : null;
    }

    /// <summary>
    /// Gets all supported provider types.
    /// </summary>
    /// <returns>Collection of supported provider type names.</returns>
    public IReadOnlyCollection<string> GetSupportedProviders()
    {
        return _testers.Keys;
    }

    /// <summary>
    /// Checks if a provider type is supported.
    /// </summary>
    /// <param name="providerType">The provider type to check.</param>
    /// <returns>True if the provider is supported, false otherwise.</returns>
    public bool IsProviderSupported(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return false;
        }

        return _testers.ContainsKey(providerType);
    }
}
