using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Services;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Example application demonstrating Azure AI integration

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Load configuration from appsettings.json and environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

// Register Azure AI services
builder.Services.AddAzureAI(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks()
    .AddAzureAIHealthChecks();

var host = builder.Build();

// Example: Run pattern search
await RunPatternSearchExample(host.Services);

// Example: Check service health
await RunHealthCheckExample(host.Services);

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

static async Task RunPatternSearchExample(IServiceProvider services)
{
    Console.WriteLine("=== Pattern Search Example ===\n");

    var knowledgeStore = services.GetRequiredService<AzureAISearchKnowledgeStore>();
    var llmFactory = services.GetRequiredService<ILlmProviderFactory>();

    // Search for deployment patterns
    var requirements = new DeploymentRequirements
    {
        DataVolumeGb = 500,
        ConcurrentUsers = 1000,
        CloudProvider = "aws",
        Region = "us-west-2"
    };

    Console.WriteLine($"Searching for patterns matching:");
    Console.WriteLine($"  Data Volume: {requirements.DataVolumeGb}GB");
    Console.WriteLine($"  Concurrent Users: {requirements.ConcurrentUsers}");
    Console.WriteLine($"  Cloud: {requirements.CloudProvider}");
    Console.WriteLine($"  Region: {requirements.Region}\n");

    try
    {
        var matches = await knowledgeStore.SearchPatternsAsync(requirements);

        if (matches.Count == 0)
        {
            Console.WriteLine("No matching patterns found.");
            Console.WriteLine("This is expected if the knowledge base is empty.");
            Console.WriteLine("Patterns are added through the approval workflow.\n");
            return;
        }

        Console.WriteLine($"Found {matches.Count} matching pattern(s):\n");

        foreach (var match in matches)
        {
            Console.WriteLine($"Pattern: {match.PatternName}");
            Console.WriteLine($"  Success Rate: {match.SuccessRate:P1}");
            Console.WriteLine($"  Deployments: {match.DeploymentCount}");
            Console.WriteLine($"  Cloud: {match.CloudProvider}");
            Console.WriteLine($"  Match Score: {match.Score:F2}");
            Console.WriteLine($"  Configuration: {match.ConfigurationJson}");
            Console.WriteLine();
        }

        // Use LLM to explain the best match
        var provider = llmFactory.CreateProvider("azure");
        var topMatch = matches[0];

        var llmRequest = new LlmRequest
        {
            SystemPrompt = "You are a helpful deployment consultant. Explain deployment patterns concisely.",
            UserPrompt = $@"Based on this deployment pattern that had {topMatch.SuccessRate:P0} success rate
over {topMatch.DeploymentCount} deployments:

{topMatch.ConfigurationJson}

Explain why this is a good match for {requirements.DataVolumeGb}GB data
with {requirements.ConcurrentUsers} concurrent users on {requirements.CloudProvider}.",
            Temperature = 0.2,
            MaxTokens = 500
        };

        Console.WriteLine("LLM Explanation:");
        var response = await provider.CompleteAsync(llmRequest);

        if (response.Success)
        {
            Console.WriteLine(response.Content);
            Console.WriteLine($"\n(Used {response.TotalTokens} tokens)");
        }
        else
        {
            Console.WriteLine($"Error: {response.ErrorMessage}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during search: {ex.Message}");
        Console.WriteLine("Make sure Azure AI Search index is created and contains patterns.");
    }

    Console.WriteLine();
}

static async Task RunHealthCheckExample(IServiceProvider services)
{
    Console.WriteLine("=== Health Check Example ===\n");

    var healthCheckService = services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

    var result = await healthCheckService.CheckHealthAsync();

    Console.WriteLine($"Overall Status: {result.Status}");
    Console.WriteLine($"Total Duration: {result.TotalDuration.TotalMilliseconds}ms\n");

    foreach (var entry in result.Entries)
    {
        Console.WriteLine($"{entry.Key}:");
        Console.WriteLine($"  Status: {entry.Value.Status}");
        Console.WriteLine($"  Description: {entry.Value.Description ?? "(none)"}");

        if (entry.Value.Data.Count > 0)
        {
            Console.WriteLine("  Data:");
            foreach (var data in entry.Value.Data)
            {
                Console.WriteLine($"    {data.Key}: {data.Value}");
            }
        }

        if (entry.Value.Exception != null)
        {
            Console.WriteLine($"  Error: {entry.Value.Exception.Message}");
        }

        Console.WriteLine();
    }
}
