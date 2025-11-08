using System;

namespace Honua.Server.Core.Tests.Shared.TestConfiguration;

/// <summary>
/// Centralized configuration for database testing across the Honua.Server test suite.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a unified way to control which database providers are enabled during test execution,
/// supporting three testing modes: Fast (SQLite only), Standard (SQLite + PostgreSQL + MySQL), and Full (all providers).
/// </para>
///
/// <para><b>Environment Variables:</b></para>
/// <list type="bullet">
/// <item><c>HONUA_DATABASE_TEST_MODE</c>: Set to "fast", "standard", or "full" (default: "fast")</item>
/// <item><c>HONUA_ENABLE_POSTGRES_TESTS</c>: Set to "1" or "true" to enable PostgreSQL tests</item>
/// <item><c>HONUA_ENABLE_MYSQL_TESTS</c>: Set to "1" or "true" to enable MySQL tests</item>
/// <item><c>HONUA_ENABLE_SQLSERVER_TESTS</c>: Set to "1" or "true" to enable SQL Server tests</item>
/// <item><c>HONUA_ENABLE_DUCKDB_TESTS</c>: Set to "1" or "true" to enable DuckDB tests</item>
/// </list>
///
/// <para><b>Usage in Tests:</b></para>
/// <code>
/// public class MyDatabaseTests
/// {
///     [Fact]
///     public void TestSomething()
///     {
///         if (!DatabaseTestConfiguration.IsPostgresEnabled)
///         {
///             throw new SkipException("PostgreSQL tests disabled");
///         }
///         // Test implementation...
///     }
/// }
/// </code>
/// </remarks>
public static class DatabaseTestConfiguration
{
    private static readonly Lazy<DatabaseTestMode> _mode = new(() => DetermineTestMode());

    /// <summary>
    /// Gets the current database test mode (Fast, Standard, or Full).
    /// </summary>
    public static DatabaseTestMode Mode => _mode.Value;

    /// <summary>
    /// Gets whether SQLite tests are enabled. SQLite is always enabled in all modes.
    /// </summary>
    public static bool IsSqliteEnabled => true;

    /// <summary>
    /// Gets whether PostgreSQL tests are enabled.
    /// Enabled in Standard and Full modes, or when HONUA_ENABLE_POSTGRES_TESTS=1.
    /// </summary>
    public static bool IsPostgresEnabled =>
        Mode >= DatabaseTestMode.Standard ||
        IsProviderExplicitlyEnabled("HONUA_ENABLE_POSTGRES_TESTS");

    /// <summary>
    /// Gets whether MySQL tests are enabled.
    /// Enabled in Standard and Full modes, or when HONUA_ENABLE_MYSQL_TESTS=1.
    /// </summary>
    public static bool IsMySqlEnabled =>
        Mode >= DatabaseTestMode.Standard ||
        IsProviderExplicitlyEnabled("HONUA_ENABLE_MYSQL_TESTS");

    /// <summary>
    /// Gets whether SQL Server tests are enabled.
    /// Enabled in Full mode only, or when HONUA_ENABLE_SQLSERVER_TESTS=1.
    /// </summary>
    public static bool IsSqlServerEnabled =>
        Mode == DatabaseTestMode.Full ||
        IsProviderExplicitlyEnabled("HONUA_ENABLE_SQLSERVER_TESTS");

    /// <summary>
    /// Gets whether DuckDB tests are enabled.
    /// Enabled in Full mode only, or when HONUA_ENABLE_DUCKDB_TESTS=1.
    /// DuckDB is an embedded database (file-based), so no Docker required.
    /// </summary>
    public static bool IsDuckDbEnabled =>
        Mode == DatabaseTestMode.Full ||
        IsProviderExplicitlyEnabled("HONUA_ENABLE_DUCKDB_TESTS");

    /// <summary>
    /// Gets a human-readable description of the current test mode configuration.
    /// </summary>
    public static string GetConfigurationSummary()
    {
        return $"""
            Database Test Configuration:
            - Mode: {Mode}
            - SQLite: {(IsSqliteEnabled ? "Enabled" : "Disabled")} (always enabled)
            - PostgreSQL: {(IsPostgresEnabled ? "Enabled" : "Disabled")}
            - MySQL: {(IsMySqlEnabled ? "Enabled" : "Disabled")}
            - SQL Server: {(IsSqlServerEnabled ? "Enabled" : "Disabled")}
            - DuckDB: {(IsDuckDbEnabled ? "Enabled" : "Disabled")}

            To change mode: Set HONUA_DATABASE_TEST_MODE=fast|standard|full
            To enable specific provider: Set HONUA_ENABLE_<PROVIDER>_TESTS=1
            """;
    }

    private static DatabaseTestMode DetermineTestMode()
    {
        var modeEnv = Environment.GetEnvironmentVariable("HONUA_DATABASE_TEST_MODE");

        if (string.IsNullOrWhiteSpace(modeEnv))
        {
            return DatabaseTestMode.Fast; // Default to fast mode
        }

        return modeEnv.Trim().ToLowerInvariant() switch
        {
            "fast" => DatabaseTestMode.Fast,
            "standard" => DatabaseTestMode.Standard,
            "full" => DatabaseTestMode.Full,
            _ => DatabaseTestMode.Fast // Default fallback
        };
    }

    private static bool IsProviderExplicitlyEnabled(string environmentVariable)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
