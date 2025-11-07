// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Dapper;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Stores;
using Honua.Server.Enterprise.ETL.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoETL;

/// <summary>
/// Base class for GeoETL integration tests providing common infrastructure
/// </summary>
public abstract class GeoEtlIntegrationTestBase : IAsyncLifetime
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected string ConnectionString { get; private set; } = string.Empty;
    protected string TestDataDirectory { get; private set; } = string.Empty;
    protected string OutputDirectory { get; private set; } = string.Empty;
    protected ILoggerFactory LoggerFactory { get; private set; } = null!;

    // Test context
    protected Guid TestTenantId { get; private set; }
    protected Guid TestUserId { get; private set; }
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    // Services
    protected IWorkflowEngine WorkflowEngine => ServiceProvider.GetRequiredService<IWorkflowEngine>();
    protected IWorkflowStore WorkflowStore => ServiceProvider.GetRequiredService<IWorkflowStore>();
    protected IWorkflowNodeRegistry NodeRegistry => ServiceProvider.GetRequiredService<IWorkflowNodeRegistry>();
    protected IWorkflowTemplateRepository TemplateRepository => ServiceProvider.GetRequiredService<IWorkflowTemplateRepository>();

    public virtual async Task InitializeAsync()
    {
        // Setup test IDs
        TestTenantId = Guid.NewGuid();
        TestUserId = Guid.NewGuid();
        CancellationTokenSource = new CancellationTokenSource();

        // Setup directories
        TestDataDirectory = Path.Combine(Path.GetTempPath(), "honua_geoetl_test_" + Guid.NewGuid().ToString("N"));
        OutputDirectory = Path.Combine(TestDataDirectory, "output");
        Directory.CreateDirectory(TestDataDirectory);
        Directory.CreateDirectory(OutputDirectory);

        // Setup logging
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Setup PostgreSQL connection
        ConnectionString = await GetConnectionStringAsync();

        // Setup database
        await SetupDatabaseAsync();

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Seed test data
        await SeedTestDataAsync();
    }

    protected virtual async Task<string> GetConnectionStringAsync()
    {
        // Try to use existing test container or create a new one
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "honua_test";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "testpass";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Register GeoETL services
        services.AddSingleton<IWorkflowStore>(sp =>
            new PostgresWorkflowStore(
                ConnectionString,
                sp.GetRequiredService<ILogger<PostgresWorkflowStore>>()
            )
        );

        services.AddSingleton<IWorkflowNodeRegistry, WorkflowNodeRegistry>();
        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();

        // Register template repository
        var templateDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "src", "Honua.Server.Enterprise", "ETL", "Templates", "Library"
        );
        services.AddSingleton<IWorkflowTemplateRepository>(sp =>
            new JsonWorkflowTemplateRepository(
                templateDir,
                sp.GetRequiredService<ILogger<JsonWorkflowTemplateRepository>>()
            )
        );

        // Register all workflow nodes
        RegisterWorkflowNodes(services);

        // Allow for custom service registration
        ConfigureAdditionalServices(services);
    }

    protected virtual void RegisterWorkflowNodes(IServiceCollection services)
    {
        // This would normally be done by AddGeoEtl, but we're doing it manually here
        // Register data source nodes
        services.AddTransient<DataSourceNodes.FileDataSourceNode>();
        services.AddTransient<DataSourceNodes.PostGisDataSourceNode>();
        services.AddTransient<GdalDataSourceNodes.GeoPackageDataSourceNode>();
        services.AddTransient<GdalDataSourceNodes.ShapefileDataSourceNode>();
        services.AddTransient<GdalDataSourceNodes.KmlDataSourceNode>();
        services.AddTransient<GdalDataSourceNodes.CsvGeometryDataSourceNode>();
        services.AddTransient<GdalDataSourceNodes.GpxDataSourceNode>();
        services.AddTransient<GdalDataSourceNodes.GmlDataSourceNode>();
        services.AddTransient<GdalDataSourceNodes.WfsDataSourceNode>();

        // Register data sink nodes
        services.AddTransient<DataSinkNodes.GeoJsonExportNode>();
        services.AddTransient<DataSinkNodes.OutputNode>();
        services.AddTransient<DataSinkNodes.PostGisDataSinkNode>();
        services.AddTransient<GdalDataSinkNodes.GeoPackageDataSinkNode>();
        services.AddTransient<GdalDataSinkNodes.ShapefileDataSinkNode>();
        services.AddTransient<GdalDataSinkNodes.CsvGeometryDataSinkNode>();
        services.AddTransient<GdalDataSinkNodes.GpxDataSinkNode>();
        services.AddTransient<GdalDataSinkNodes.GmlDataSinkNode>();

        // Register geoprocessing nodes
        services.AddTransient<GeoprocessingNode>();
    }

    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Override in derived classes to add additional services
    }

    protected virtual async Task SetupDatabaseAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Enable extensions
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS postgis;");
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

        // Run GeoETL migrations
        await RunGeoEtlMigrationsAsync(connection);
    }

    protected virtual async Task RunGeoEtlMigrationsAsync(NpgsqlConnection connection)
    {
        // Create GeoETL tables
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS geoetl_workflows (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                tenant_id UUID NOT NULL,
                version INTEGER NOT NULL DEFAULT 1,
                metadata JSONB NOT NULL DEFAULT '{}',
                parameters JSONB NOT NULL DEFAULT '{}',
                nodes JSONB NOT NULL DEFAULT '[]',
                edges JSONB NOT NULL DEFAULT '[]',
                is_published BOOLEAN NOT NULL DEFAULT false,
                is_deleted BOOLEAN NOT NULL DEFAULT false,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                created_by UUID NOT NULL,
                updated_by UUID
            );

            CREATE INDEX IF NOT EXISTS idx_geoetl_workflows_tenant ON geoetl_workflows(tenant_id) WHERE is_deleted = false;
            CREATE INDEX IF NOT EXISTS idx_geoetl_workflows_published ON geoetl_workflows(is_published) WHERE is_deleted = false;
        ");

        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS geoetl_workflow_runs (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                workflow_id UUID NOT NULL REFERENCES geoetl_workflows(id),
                tenant_id UUID NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'Pending',
                started_at TIMESTAMPTZ,
                completed_at TIMESTAMPTZ,
                triggered_by UUID,
                trigger_type VARCHAR(50) NOT NULL DEFAULT 'Manual',
                parameter_values JSONB,
                features_processed BIGINT,
                bytes_read BIGINT,
                bytes_written BIGINT,
                peak_memory_mb INTEGER,
                cpu_time_ms BIGINT,
                compute_cost_usd DECIMAL(10,4),
                storage_cost_usd DECIMAL(10,4),
                output_locations JSONB,
                error_message TEXT,
                error_stack TEXT,
                input_datasets TEXT[],
                output_datasets TEXT[],
                state JSONB,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_geoetl_runs_workflow ON geoetl_workflow_runs(workflow_id);
            CREATE INDEX IF NOT EXISTS idx_geoetl_runs_tenant ON geoetl_workflow_runs(tenant_id);
            CREATE INDEX IF NOT EXISTS idx_geoetl_runs_status ON geoetl_workflow_runs(status);
        ");

        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS geoetl_node_runs (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                workflow_run_id UUID NOT NULL REFERENCES geoetl_workflow_runs(id) ON DELETE CASCADE,
                node_id VARCHAR(255) NOT NULL,
                node_type VARCHAR(255) NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'Pending',
                started_at TIMESTAMPTZ,
                completed_at TIMESTAMPTZ,
                duration_ms BIGINT,
                features_processed BIGINT,
                error_message TEXT,
                geoprocessing_run_id UUID,
                output JSONB,
                retry_count INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_geoetl_node_runs_workflow_run ON geoetl_node_runs(workflow_run_id);
        ");
    }

    protected virtual async Task SeedTestDataAsync()
    {
        // Create test tenant and user data if needed
        // Override in derived classes for specific test data
        await Task.CompletedTask;
    }

    protected async Task<WorkflowRun> ExecuteWorkflowAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? parameters = null,
        IProgress<WorkflowProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var options = new WorkflowExecutionOptions
        {
            TenantId = workflow.TenantId,
            UserId = TestUserId,
            ParameterValues = parameters,
            ProgressCallback = progress
        };

        return await WorkflowEngine.ExecuteAsync(workflow, options, cancellationToken);
    }

    protected string GetTestFilePath(string fileName)
    {
        return Path.Combine(TestDataDirectory, fileName);
    }

    protected string GetOutputFilePath(string fileName)
    {
        return Path.Combine(OutputDirectory, fileName);
    }

    protected async Task CreateTestGeoJsonFileAsync(string fileName, int featureCount = 10)
    {
        var features = new List<string>();
        for (int i = 0; i < featureCount; i++)
        {
            var lon = -122.0 + (i * 0.01);
            var lat = 37.0 + (i * 0.01);
            features.Add($@"
            {{
                ""type"": ""Feature"",
                ""geometry"": {{
                    ""type"": ""Point"",
                    ""coordinates"": [{lon}, {lat}]
                }},
                ""properties"": {{
                    ""id"": {i},
                    ""name"": ""Feature {i}""
                }}
            }}");
        }

        var geojson = $@"{{
            ""type"": ""FeatureCollection"",
            ""features"": [{string.Join(",", features)}]
        }}";

        await File.WriteAllTextAsync(GetTestFilePath(fileName), geojson);
    }

    public virtual async Task DisposeAsync()
    {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();

        // Clean up test directories
        if (Directory.Exists(TestDataDirectory))
        {
            try
            {
                Directory.Delete(TestDataDirectory, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        // Dispose service provider
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        LoggerFactory?.Dispose();

        await Task.CompletedTask;
    }
}
