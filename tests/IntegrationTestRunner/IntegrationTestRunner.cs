using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.IntegrationTestRunner;

/// <summary>
/// Simple runner to start Honua server with seed data for integration tests.
/// This runner:
/// 1. Creates a minimal metadata.json configuration
/// 2. Starts the Honua server on http://localhost:5005
/// 3. Loads seed data using the CLI
/// 4. Keeps server running for external integration tests (QGIS, pystac-client, etc.)
/// </summary>
public class IntegrationTestRunner
{
    private const string BaseUrl = "http://localhost:5005";
    private const string MetadataProvider = "sqlite"; // Use in-memory SQLite for quick setup

    public static async Task Main(string[] args)
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<IntegrationTestRunner>();

        logger.LogInformation("Starting Honua Integration Test Runner...");
        logger.LogInformation("Server will run at: {BaseUrl}", BaseUrl);

        // Create temporary directory for test configuration
        var tempDir = Path.Combine(Path.GetTempPath(), $"honua-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        logger.LogInformation("Using temporary directory: {TempDir}", tempDir);

        try
        {
            // Create minimal metadata.json
            var metadataPath = Path.Combine(tempDir, "metadata.json");
            await CreateMinimalMetadataAsync(metadataPath);
            logger.LogInformation("Created metadata configuration: {MetadataPath}", metadataPath);

            // Start Honua server
            var serverProcess = StartHonuaServer(metadataPath, logger);

            // Wait for server to start
            await Task.Delay(5000);
            logger.LogInformation("Server should be running. Check health endpoint...");

            // Load seed data
            await LoadSeedDataAsync(logger);

            logger.LogInformation("");
            logger.LogInformation("===========================================");
            logger.LogInformation("Honua server is running at {BaseUrl}", BaseUrl);
            logger.LogInformation("===========================================");
            logger.LogInformation("");
            logger.LogInformation("You can now run integration tests:");
            logger.LogInformation("  Python: HONUA_API_BASE_URL={BaseUrl} pytest tests/python -v", BaseUrl);
            logger.LogInformation("  QGIS:   docker run --rm --network host -e HONUA_QGIS_BASE_URL={BaseUrl} ...", BaseUrl);
            logger.LogInformation("");
            logger.LogInformation("Press Ctrl+C to stop the server...");
            logger.LogInformation("");

            // Wait for Ctrl+C
            var tcs = new TaskCompletionSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tcs.SetResult();
            };

            await tcs.Task;

            logger.LogInformation("Shutting down...");
            serverProcess?.Kill();
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private static async Task CreateMinimalMetadataAsync(string metadataPath)
    {
        // Create a minimal metadata.json that will be populated by seed data loader
        var metadata = new
        {
            server = new
            {
                allowedHosts = new[] { "*" }
            },
            catalog = new
            {
                id = "integration-test-catalog",
                title = "Integration Test Catalog",
                description = "Catalog for integration testing with seed data"
            },
            services = Array.Empty<object>(),
            layers = Array.Empty<object>(),
            styles = Array.Empty<object>(),
            rasterDatasets = Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json);
    }

    private static Process? StartHonuaServer(string metadataPath, ILogger logger)
    {
        logger.LogInformation("Starting Honua server...");

        var projectPath = FindProjectPath();
        if (projectPath == null)
        {
            logger.LogError("Could not find Honua.Server.Host project");
            return null;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --urls {BaseUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["HONUA_ALLOW_QUICKSTART"] = "true",
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["honua__metadata__provider"] = "sqlite",
                ["honua__metadata__connectionString"] = "Data Source=:memory:;Mode=Memory;Cache=Shared",
                ["honua__authentication__mode"] = "QuickStart",
                ["honua__rateLimiting__enabled"] = "false",
                ["honua__openApi__enabled"] = "false"
            }
        };

        var process = Process.Start(psi);

        if (process != null)
        {
            // Log output asynchronously
            Task.Run(async () =>
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        logger.LogInformation("[SERVER] {Line}", line);
                    }
                }
            });

            Task.Run(async () =>
            {
                while (!process.StandardError.EndOfStream)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        logger.LogError("[SERVER] {Line}", line);
                    }
                }
            });
        }

        return process;
    }

    private static async Task LoadSeedDataAsync(ILogger logger)
    {
        logger.LogInformation("Loading seed data...");

        var seedDataScript = FindFile("load-all-seed-data.sh");
        if (seedDataScript == null)
        {
            logger.LogWarning("Could not find load-all-seed-data.sh script. Skipping seed data load.");
            logger.LogInformation("You can manually load seed data using:");
            logger.LogInformation("  ./tests/TestData/seed-data/load-all-seed-data.sh");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = seedDataScript,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["HONUA_BASE_URL"] = BaseUrl
            }
        };

        var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                logger.LogInformation("Seed data loaded successfully");
            }
            else
            {
                logger.LogError("Failed to load seed data: {Error}", error);
            }
        }
    }

    private static string? FindProjectPath()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            var projectPath = Path.Combine(current, "src", "Honua.Server.Host", "Honua.Server.Host.csproj");
            if (File.Exists(projectPath))
            {
                return projectPath;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        return null;
    }

    private static string? FindFile(string fileName)
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            var filePath = Path.Combine(current, "tests", "TestData", "seed-data", fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        return null;
    }
}
