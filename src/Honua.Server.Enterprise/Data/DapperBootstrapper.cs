// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Data;
using Dapper;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Honua.Server.Enterprise.Data;

/// <summary>
/// Centralizes Dapper configuration for the enterprise data access layer.
/// Ensures snake_case columns map to PascalCase properties.
/// </summary>
internal static class DapperBootstrapper
{
    private static int _initialized;

    #pragma warning disable CA2255
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void InitializeModule()
    {
        EnsureConfigured();
    }
    #pragma warning restore CA2255

    public static void EnsureConfigured()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
