using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Honua.Server.Core.Tests.Data.Data.Sqlite;

internal static class SpatialiteTestHelper
{
    private static readonly string[] DefaultSpatialiteNames =
    {
        "mod_spatialite",
        "mod_spatialite.dll",
        "mod_spatialite.so",
        "mod_spatialite.dylib"
    };

    private static bool _probeCompleted;
    private static bool _available;
    private static string? _resolvedPath;

    public static bool EnsureAvailable(out string? reason)
    {
        if (_probeCompleted)
        {
            reason = _available ? null : BuildFailureMessage();
            return _available;
        }

        var candidates = EnumerateCandidates();
        foreach (var candidate in candidates)
        {
            if (TryLoadCandidate(candidate))
            {
                _resolvedPath = candidate;
                _available = true;
                _probeCompleted = true;
                reason = null;
                return true;
            }
        }

        _probeCompleted = true;
        _available = false;
        reason = BuildFailureMessage();
        return false;
    }

    public static string BuildConnectionString(string dataSource)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Pooling = false
        };

        if (EnsureAvailable(out _) && !string.IsNullOrWhiteSpace(_resolvedPath))
        {
            builder["SpatiaLiteExtensionPath"] = _resolvedPath;
        }

        return builder.ConnectionString;
    }

    public static void ConfigureConnection(SqliteConnection connection)
    {
        if (!EnsureAvailable(out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        connection.EnableExtensions();
        connection.LoadExtension(_resolvedPath ?? "mod_spatialite");

        using var init = connection.CreateCommand();
        init.CommandText = "SELECT InitSpatialMetaData(1);";
        init.ExecuteNonQuery();
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        var env = Environment.GetEnvironmentVariable("SPATIALITE_EXTENSION_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            yield return env;
        }

        foreach (var candidate in DefaultSpatialiteNames)
        {
            yield return candidate;
        }
    }

    private static bool TryLoadCandidate(string candidate)
    {
        try
        {
            using var connection = new SqliteConnection("Data Source=:memory:;Pooling=false;");
            connection.Open();
            connection.EnableExtensions();
            connection.LoadExtension(candidate);

            using var init = connection.CreateCommand();
            init.CommandText = "SELECT InitSpatialMetaData(1);";
            init.ExecuteNonQuery();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (SqliteException ex) when (IsModuleMissing(ex))
        {
            return false;
        }
    }

    private static bool IsModuleMissing(SqliteException ex)
    {
        return ex.SqliteErrorCode == 1 &&
               (ex.Message.Contains("no such module", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("unable to load", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildFailureMessage()
    {
        return "SpatiaLite extension (mod_spatialite) could not be loaded. Install SpatiaLite or set SPATIALITE_EXTENSION_PATH.";
    }
}
