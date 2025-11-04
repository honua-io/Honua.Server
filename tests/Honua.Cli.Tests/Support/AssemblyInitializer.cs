using System.Runtime.CompilerServices;

namespace Honua.Cli.Tests.Support;

/// <summary>
/// Assembly-level test initializer that runs before any tests.
/// Exports API keys from user secrets to environment variables.
/// </summary>
public static class AssemblyInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Export API keys to environment variables so they're available to:
        // 1. Child processes (dotnet run --project src/Honua.Cli)
        // 2. E2E test scripts
        // 3. Any code that reads from environment variables
        TestConfiguration.ExportApiKeysToEnvironment();
    }
}
