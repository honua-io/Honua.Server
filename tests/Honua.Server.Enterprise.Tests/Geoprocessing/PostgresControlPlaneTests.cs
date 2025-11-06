using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

[Collection("SharedPostgres")]
public class PostgresControlPlaneTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private string _connectionString;
    private readonly Mock<IProcessRegistry> _mockRegistry;
    private readonly Mock<ITierExecutor> _mockTierExecutor;
    private PostgresControlPlane _controlPlane;

    public PostgresControlPlaneTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
        _mockRegistry = new Mock<IProcessRegistry>();
        _mockTierExecutor = new Mock<ITierExecutor>();
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new Xunit.SkipException("PostgreSQL test container is not available");
        }

        _connectionString = _fixture.ConnectionString;

        _controlPlane = new PostgresControlPlane(
            _connectionString,
            _mockRegistry.Object,
            _mockTierExecutor.Object,
            NullLogger<PostgresControlPlane>.Instance);

        // Run migrations to set up test database
        await TestDatabaseHelper.RunMigrationsAsync(_connectionString);
    }

    public async Task DisposeAsync()
    {
        if (_fixture.IsAvailable)
        {
            // Cleanup test data
            await TestDatabaseHelper.CleanupAsync(_connectionString);
        }
    }

    [Fact]
    public async Task AdmitAsync_ValidRequest_ShouldAdmit()
    {
        // Arrange
        var processId = "buffer";
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var processDefinition = CreateTestProcessDefinition(processId);
        _mockRegistry.Setup(r => r.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        var request = new ProcessExecutionRequest
        {
            ProcessId = processId,
            TenantId = tenantId,
            UserId = userId,
            Inputs = new Dictionary<string, object>
            {
                ["geometry"] = "POINT(0 0)",
                ["distance"] = 100
            },
            Mode = ExecutionMode.Auto
        };

        // Act
        var decision = await _controlPlane.AdmitAsync(request);

        // Assert
        decision.Should().NotBeNull();
        decision.Admitted.Should().BeTrue();
        decision.DenialReasons.Should().BeEmpty();
        decision.SelectedTier.Should().Be(ProcessExecutionTier.NTS);
        decision.Request.Should().Be(request);
    }

    [Fact]
    public async Task AdmitAsync_ProcessNotFound_ShouldDeny()
    {
        // Arrange
        var processId = "nonexistent";
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockRegistry.Setup(r => r.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessDefinition?)null);

        var request = new ProcessExecutionRequest
        {
            ProcessId = processId,
            TenantId = tenantId,
            UserId = userId,
            Inputs = new Dictionary<string, object>()
        };

        // Act
        var decision = await _controlPlane.AdmitAsync(request);

        // Assert
        decision.Admitted.Should().BeFalse();
        decision.DenialReasons.Should().Contain($"Process '{processId}' not found");
    }

    [Fact]
    public async Task AdmitAsync_DisabledProcess_ShouldDeny()
    {
        // Arrange
        var processId = "buffer";
        var baseDef = CreateTestProcessDefinition(processId);
        var processDefinition = new ProcessDefinition
        {
            Id = baseDef.Id,
            Title = baseDef.Title,
            Version = baseDef.Version,
            Description = baseDef.Description,
            Category = baseDef.Category,
            Keywords = baseDef.Keywords,
            Inputs = baseDef.Inputs,
            OutputFormats = baseDef.OutputFormats,
            ExecutionConfig = baseDef.ExecutionConfig,
            Enabled = false,
            Links = baseDef.Links
        };

        _mockRegistry.Setup(r => r.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        var request = new ProcessExecutionRequest
        {
            ProcessId = processId,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        // Act
        var decision = await _controlPlane.AdmitAsync(request);

        // Assert
        decision.Admitted.Should().BeFalse();
        decision.DenialReasons.Should().Contain($"Process '{processId}' is disabled");
    }

    [Fact]
    public async Task EnqueueAsync_ValidRequest_ShouldCreateProcessRun()
    {
        // Arrange
        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "test@example.com",
            Inputs = new Dictionary<string, object>
            {
                ["geometry"] = "POINT(0 0)",
                ["distance"] = 100
            }
        };

        var decision = await _controlPlane.AdmitAsync(request);

        // Act
        var processRun = await _controlPlane.EnqueueAsync(decision);

        // Assert
        processRun.Should().NotBeNull();
        processRun.JobId.Should().StartWith("job-");
        processRun.ProcessId.Should().Be("buffer");
        processRun.TenantId.Should().Be(request.TenantId);
        processRun.UserId.Should().Be(request.UserId);
        processRun.Status.Should().Be(ProcessRunStatus.Pending);

        // Verify it was saved to database
        var retrieved = await _controlPlane.GetJobStatusAsync(processRun.JobId, request.TenantId);
        retrieved.Should().NotBeNull();
        retrieved!.JobId.Should().Be(processRun.JobId);
    }

    [Fact]
    public async Task GetJobStatusAsync_ExistingJob_ShouldReturnStatus()
    {
        // Arrange
        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        var decision = await _controlPlane.AdmitAsync(request);
        var processRun = await _controlPlane.EnqueueAsync(decision);

        // Act
        var status = await _controlPlane.GetJobStatusAsync(processRun.JobId, request.TenantId);

        // Assert
        status.Should().NotBeNull();
        status!.JobId.Should().Be(processRun.JobId);
        status.Status.Should().Be(ProcessRunStatus.Pending);
    }

    [Fact]
    public async Task CancelJobAsync_PendingJob_ShouldCancel()
    {
        // Arrange
        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        var decision = await _controlPlane.AdmitAsync(request);
        var processRun = await _controlPlane.EnqueueAsync(decision);

        // Act
        var cancelled = await _controlPlane.CancelJobAsync(processRun.JobId, request.TenantId, "Test cancellation");

        // Assert
        cancelled.Should().BeTrue();

        var status = await _controlPlane.GetJobStatusAsync(processRun.JobId, request.TenantId);
        status!.Status.Should().Be(ProcessRunStatus.Cancelled);
        status.CancellationReason.Should().Be("Test cancellation");
    }

    [Fact]
    public async Task QueryRunsAsync_WithTenantFilter_ShouldReturnOnlyTenantJobs()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        // Create jobs for tenant1
        for (int i = 0; i < 3; i++)
        {
            var request = new ProcessExecutionRequest
            {
                ProcessId = "buffer",
                TenantId = tenant1,
                UserId = Guid.NewGuid(),
                Inputs = new Dictionary<string, object>()
            };
            var decision = await _controlPlane.AdmitAsync(request);
            await _controlPlane.EnqueueAsync(decision);
        }

        // Create jobs for tenant2
        for (int i = 0; i < 2; i++)
        {
            var request = new ProcessExecutionRequest
            {
                ProcessId = "buffer",
                TenantId = tenant2,
                UserId = Guid.NewGuid(),
                Inputs = new Dictionary<string, object>()
            };
            var decision = await _controlPlane.AdmitAsync(request);
            await _controlPlane.EnqueueAsync(decision);
        }

        // Act
        var result = await _controlPlane.QueryRunsAsync(new ProcessRunQuery
        {
            TenantId = tenant1,
            Limit = 100
        });

        // Assert
        result.Runs.Should().HaveCount(3);
        result.Runs.Should().OnlyContain(r => r.TenantId == tenant1);
    }

    [Fact]
    public async Task RecordCompletionAsync_ShouldUpdateJobStatus()
    {
        // Arrange
        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        var decision = await _controlPlane.AdmitAsync(request);
        var processRun = await _controlPlane.EnqueueAsync(decision);

        var result = new ProcessResult
        {
            JobId = processRun.JobId,
            ProcessId = "buffer",
            Status = ProcessRunStatus.Completed,
            Success = true,
            Output = new Dictionary<string, object> { ["result"] = "success" },
            FeaturesProcessed = 100
        };

        // Act
        await _controlPlane.RecordCompletionAsync(
            processRun.JobId,
            result,
            ProcessExecutionTier.NTS,
            TimeSpan.FromSeconds(5));

        // Assert
        var status = await _controlPlane.GetJobStatusAsync(processRun.JobId, request.TenantId);
        status!.Status.Should().Be(ProcessRunStatus.Completed);
        status.DurationMs.Should().Be(5000);
        status.ExecutedTier.Should().Be(ProcessExecutionTier.NTS);
        status.FeaturesProcessed.Should().Be(100);
    }

    private static ProcessDefinition CreateTestProcessDefinition(string processId)
    {
        return new ProcessDefinition
        {
            Id = processId,
            Title = $"{processId} Operation",
            Description = $"Test {processId} operation",
            Version = "1.0.0",
            Category = "vector",
            Inputs = new List<ProcessParameter>
            {
                new()
                {
                    Name = "geometry",
                    Title = "Input Geometry",
                    Type = "geometry",
                    Required = true
                },
                new()
                {
                    Name = "distance",
                    Title = "Distance",
                    Type = "number",
                    Required = true,
                    MinValue = 0
                }
            },
            Output = new ProcessOutput
            {
                Type = "geometry",
                Description = "Buffered geometry"
            },
            OutputFormats = new List<string> { "geojson" },
            ExecutionConfig = new ProcessExecutionConfig
            {
                SupportedTiers = new List<ProcessExecutionTier>
                {
                    ProcessExecutionTier.NTS,
                    ProcessExecutionTier.PostGIS,
                    ProcessExecutionTier.CloudBatch
                },
                DefaultTier = ProcessExecutionTier.NTS,
                EstimatedDurationSeconds = 10
            },
            Enabled = true
        };
    }
}

/// <summary>
/// Helper class for test database operations
/// </summary>
public static class TestDatabaseHelper
{
    public static async Task RunMigrationsAsync(string connectionString)
    {
        using var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // Create process_runs table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS process_runs (
                -- Identification
                job_id VARCHAR(100) PRIMARY KEY,
                process_id VARCHAR(100) NOT NULL,
                tenant_id VARCHAR(100) NOT NULL,
                user_id UUID NOT NULL,
                user_email VARCHAR(500),

                -- Status & Timing
                status VARCHAR(50) NOT NULL DEFAULT 'pending',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                started_at TIMESTAMPTZ,
                completed_at TIMESTAMPTZ,
                duration_ms BIGINT,
                queue_wait_ms BIGINT,

                -- Execution
                executed_tier VARCHAR(20),
                worker_id VARCHAR(200),
                cloud_batch_job_id VARCHAR(500),
                priority INTEGER NOT NULL DEFAULT 5,
                progress INTEGER NOT NULL DEFAULT 0,
                progress_message TEXT,

                -- Inputs & Outputs
                inputs JSONB NOT NULL,
                output JSONB,
                response_format VARCHAR(50) NOT NULL DEFAULT 'geojson',
                output_url TEXT,
                output_size_bytes BIGINT,

                -- Error Handling
                error_message TEXT,
                error_details TEXT,
                retry_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 3,
                cancellation_reason TEXT,

                -- Resource Usage & Billing
                peak_memory_mb BIGINT,
                cpu_time_ms BIGINT,
                features_processed BIGINT,
                input_size_mb DECIMAL(10,2),
                compute_cost DECIMAL(12,4),
                storage_cost DECIMAL(12,4),
                total_cost DECIMAL(12,4),

                -- Provenance & Audit
                ip_address VARCHAR(50),
                user_agent VARCHAR(500),
                api_surface VARCHAR(50) NOT NULL DEFAULT 'OGC',
                client_id VARCHAR(100),
                tags TEXT[],
                metadata JSONB,

                -- Notifications
                webhook_url TEXT,
                notify_email BOOLEAN NOT NULL DEFAULT false,
                webhook_sent_at TIMESTAMPTZ,
                webhook_response_status INTEGER,

                -- Constraints
                CONSTRAINT chk_progress CHECK (progress >= 0 AND progress <= 100),
                CONSTRAINT chk_priority CHECK (priority >= 1 AND priority <= 10),
                CONSTRAINT chk_status CHECK (status IN ('pending', 'running', 'completed', 'failed', 'cancelled', 'timeout'))
            );");

        // Create process_catalog table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS process_catalog (
                process_id VARCHAR(100) PRIMARY KEY,
                title VARCHAR(500) NOT NULL,
                description TEXT,
                version VARCHAR(20) NOT NULL DEFAULT '1.0.0',
                category VARCHAR(50) NOT NULL DEFAULT 'vector',
                keywords TEXT[],

                -- Configuration (stored as JSONB for flexibility)
                inputs_schema JSONB NOT NULL,
                output_schema JSONB,
                output_formats TEXT[] NOT NULL DEFAULT ARRAY['geojson'],
                execution_config JSONB NOT NULL,

                -- Links and metadata
                links JSONB,
                metadata JSONB,

                -- Status
                enabled BOOLEAN NOT NULL DEFAULT true,
                registered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

                -- Implementation
                implementation_class VARCHAR(500)
            );");

        // Create indexes
        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_process_runs_tenant_created ON process_runs(tenant_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_process_runs_status ON process_runs(status, created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_process_catalog_enabled ON process_catalog(category, process_id) WHERE enabled = true;
        ");

        // Create dequeue function used by control plane
        await connection.ExecuteAsync(@"
            CREATE OR REPLACE FUNCTION dequeue_process_run()
            RETURNS TABLE(
                job_id VARCHAR,
                process_id VARCHAR,
                tenant_id VARCHAR(100),
                inputs JSONB
            )
            LANGUAGE plpgsql
            AS $$
            DECLARE
                selected_job RECORD;
            BEGIN
                SELECT * INTO selected_job
                FROM process_runs
                WHERE status = 'pending'
                ORDER BY priority DESC, created_at ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED;

                IF selected_job IS NULL THEN
                    RETURN;
                END IF;

                UPDATE process_runs
                SET
                    status = 'running',
                    started_at = NOW(),
                    progress = 0,
                    progress_message = 'Job started',
                    queue_wait_ms = EXTRACT(EPOCH FROM (NOW() - created_at)) * 1000
                WHERE process_runs.job_id = selected_job.job_id;

                RETURN QUERY
                SELECT
                    selected_job.job_id,
                    selected_job.process_id,
                    selected_job.tenant_id,
                    selected_job.inputs;
            END;
            $$;
        ");
    }

    public static async Task CleanupAsync(string connectionString)
    {
        using var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync("DELETE FROM process_runs");
        await connection.ExecuteAsync("DELETE FROM process_catalog");
    }
}
