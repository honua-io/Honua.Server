namespace Honua.Server.Core.Tests.Shared.TestConfiguration;

/// <summary>
/// Defines the database testing mode to control which database providers are enabled during test execution.
/// </summary>
/// <remarks>
/// <para><b>Test Mode Selection Strategy:</b></para>
/// <list type="bullet">
/// <item><b>Fast (default):</b> Use for local development and CI/CD pull request checks. SQLite only, no Docker required. Fastest execution (2-5 seconds per test class).</item>
/// <item><b>Standard:</b> Use for merge/integration testing. Covers SQLite + PostgreSQL + MySQL. Docker required. Moderate execution (10-20 seconds per test class).</item>
/// <item><b>Full:</b> Use for release validation and nightly builds. Tests all database providers including SQL Server and DuckDB. Docker required. Comprehensive coverage (30-45 seconds per test class).</item>
/// </list>
///
/// <para><b>Configuration:</b></para>
/// <para>Set the <c>HONUA_DATABASE_TEST_MODE</c> environment variable to <c>fast</c>, <c>standard</c>, or <c>full</c>.</para>
/// <para>Individual providers can be controlled with specific environment variables (e.g., <c>HONUA_ENABLE_POSTGRES_TESTS=1</c>).</para>
///
/// <para><b>CI/CD Recommendations:</b></para>
/// <list type="bullet">
/// <item>PR Checks: FAST mode (2-3 minutes total)</item>
/// <item>Merge to main: STANDARD mode (5-10 minutes total)</item>
/// <item>Release/Nightly: FULL mode (15-20 minutes total)</item>
/// </list>
/// </remarks>
public enum DatabaseTestMode
{
    /// <summary>
    /// Fast mode: SQLite only. No Docker required. Best for local development and PR checks.
    /// </summary>
    Fast,

    /// <summary>
    /// Standard mode: SQLite + PostgreSQL + MySQL. Docker required. Best for integration testing.
    /// </summary>
    Standard,

    /// <summary>
    /// Full mode: All database providers (SQLite, PostgreSQL, MySQL, SQL Server, DuckDB). Docker required. Best for release validation.
    /// </summary>
    Full
}
