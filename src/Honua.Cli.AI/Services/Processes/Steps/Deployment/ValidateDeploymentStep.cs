// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using Honua.Cli.AI.Services.Guardrails;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Npgsql;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Validates deployment with health checks and OGC endpoint tests.
/// </summary>
public class ValidateDeploymentStep : KernelProcessStep<DeploymentState>, IProcessStepTimeout
{
    private readonly ILogger<ValidateDeploymentStep> _logger;
    private readonly IDeploymentGuardrailMonitor _guardrailMonitor;
    private readonly IDeploymentMetricsProvider _metricsProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private DeploymentState _state = new();

    /// <summary>
    /// Validation includes health checks, endpoint tests, and database connectivity.
    /// Default timeout: 10 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);

    public ValidateDeploymentStep(
        ILogger<ValidateDeploymentStep> logger,
        IDeploymentGuardrailMonitor guardrailMonitor,
        IDeploymentMetricsProvider metricsProvider,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _guardrailMonitor = guardrailMonitor ?? throw new ArgumentNullException(nameof(guardrailMonitor));
        _metricsProvider = metricsProvider ?? throw new ArgumentNullException(nameof(metricsProvider));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ValidateDeployment")]
    public async Task ValidateDeploymentAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating deployment {DeploymentId}", _state.DeploymentId);

        _state.Status = "ValidatingDeployment";

        try
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Run health checks
            await RunHealthChecks(cancellationToken);

            // Validate OGC endpoints
            await ValidateOGCEndpoints(cancellationToken);

            // Test database connectivity
            await TestDatabaseConnection(cancellationToken);

            if (_state.GuardrailDecision is not null)
            {
                var metrics = await _metricsProvider.GetMetricsAsync(_state, _state.GuardrailDecision, cancellationToken)
                    .ConfigureAwait(false);
                await _guardrailMonitor.EvaluateAsync(_state.GuardrailDecision, metrics, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Guardrail evaluation for {DeploymentId}: CPU={Cpu}cores Memory={Memory}GiB ColdStarts/hr={ColdStarts} Backlog={Backlog}",
                    _state.DeploymentId,
                    metrics.CpuUtilization,
                    metrics.MemoryUtilizationGb,
                    metrics.ColdStartsPerHour,
                    metrics.QueueBacklog);
            }

            _logger.LogInformation("Deployment validation successful for {DeploymentId}", _state.DeploymentId);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DeploymentValidated",
                Data = _state
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Deployment validation cancelled for {DeploymentId}", _state.DeploymentId);
            _state.Status = "Cancelled";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ValidationCancelled",
                Data = new { _state.DeploymentId }
            });
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment validation failed for {DeploymentId}", _state.DeploymentId);
            _state.Status = "ValidationFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ValidationFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
    }

    private async Task RunHealthChecks(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running health checks");

        try
        {
            var endpoint = GetApplicationEndpoint();
            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogWarning("No application endpoint found, skipping health checks");
                return;
            }

            var healthUrl = $"{endpoint}/health";
            _logger.LogInformation("Checking health endpoint: {HealthUrl}", healthUrl);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Add authentication headers if available
            AddAuthenticationHeaders(httpClient);

            // Retry health check up to 10 times (application might be starting)
            var maxRetries = 10;
            var retryDelay = TimeSpan.FromSeconds(10);

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await httpClient.GetAsync(healthUrl, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogInformation("Health check passed: {StatusCode}", response.StatusCode);
                        return;
                    }

                    // Handle authentication errors specially
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError("Health check failed with 401 Unauthorized. Authentication credentials may be missing or invalid.");
                        throw new InvalidOperationException(
                            "Health check failed due to authentication error. " +
                            "Ensure API key or bearer token is configured in deployment state.");
                    }

                    _logger.LogWarning("Health check returned {StatusCode}, retrying ({Attempt}/{MaxRetries})",
                        response.StatusCode, i + 1, maxRetries);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Health check failed, retrying ({Attempt}/{MaxRetries})",
                        i + 1, maxRetries);
                }

                if (i < maxRetries - 1)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }

            throw new InvalidOperationException($"Health check failed after {maxRetries} attempts");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run health checks");
            throw;
        }
    }

    private async Task ValidateOGCEndpoints(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating OGC API endpoints");

        try
        {
            var endpoint = GetApplicationEndpoint();
            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogWarning("No application endpoint found, skipping OGC endpoint validation");
                return;
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Add authentication headers if available
            AddAuthenticationHeaders(httpClient);

            // Test landing page
            _logger.LogInformation("Testing OGC API landing page");
            var landingPageUrl = $"{endpoint}/";
            var landingPageResponse = await httpClient.GetAsync(landingPageUrl, cancellationToken);

            if (landingPageResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError("OGC API landing page returned 401 Unauthorized. Authentication credentials may be missing or invalid.");
                throw new InvalidOperationException(
                    "OGC API validation failed due to authentication error. " +
                    "Ensure API key or bearer token is configured in deployment state.");
            }

            landingPageResponse.EnsureSuccessStatusCode();
            _logger.LogInformation("OGC API landing page OK");

            // Test conformance endpoint
            _logger.LogInformation("Testing OGC API conformance endpoint");
            var conformanceUrl = $"{endpoint}/conformance";
            var conformanceResponse = await httpClient.GetAsync(conformanceUrl, cancellationToken);

            if (conformanceResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError("OGC API conformance endpoint returned 401 Unauthorized.");
                throw new InvalidOperationException(
                    "OGC API conformance validation failed due to authentication error.");
            }

            conformanceResponse.EnsureSuccessStatusCode();
            _logger.LogInformation("OGC API conformance endpoint OK");

            // Test collections endpoint
            _logger.LogInformation("Testing OGC API collections endpoint");
            var collectionsUrl = $"{endpoint}/collections";
            var collectionsResponse = await httpClient.GetAsync(collectionsUrl, cancellationToken);

            if (collectionsResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError("OGC API collections endpoint returned 401 Unauthorized.");
                throw new InvalidOperationException(
                    "OGC API collections validation failed due to authentication error.");
            }

            collectionsResponse.EnsureSuccessStatusCode();
            var collectionsContent = await collectionsResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("OGC API collections endpoint OK: {Length} bytes", collectionsContent.Length);

            _logger.LogInformation("All OGC API endpoints validated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate OGC endpoints");
            throw;
        }
    }

    private async Task TestDatabaseConnection(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Testing database connection");

        try
        {
            string? dbEndpoint = null;
            if (_state.InfrastructureOutputs?.TryGetValue("database_endpoint", out dbEndpoint) != true || string.IsNullOrEmpty(dbEndpoint))
            {
                _logger.LogWarning("No database endpoint found, skipping database connection test");
                return;
            }

            // Detect database type from infrastructure outputs or state
            var dbType = _state.InfrastructureOutputs?.GetValueOrDefault("database_type", "postgresql")?.ToLowerInvariant() ?? "postgresql";
            var isPostgres = dbType.Contains("postgres");

            // Build connection string
            var connectionString = BuildConnectionString(dbEndpoint);

            // Test connection with retries
            var maxRetries = 10;
            var retryDelay = TimeSpan.FromSeconds(10);

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync(cancellationToken);

                    _logger.LogInformation("Database connection successful");

                    // Only test PostGIS if we're using PostgreSQL
                    if (isPostgres)
                    {
                        // Probe for PostGIS extension before querying
                        await using var probeCmd = new NpgsqlCommand(
                            "SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'postgis');", connection);
                        var hasPostGIS = (bool?)await probeCmd.ExecuteScalarAsync(cancellationToken) ?? false;

                        if (hasPostGIS)
                        {
                            // Test PostGIS installation (only if extension is installed)
                            await using var cmd = new NpgsqlCommand("SELECT PostGIS_version();", connection);
                            var postgisVersion = await cmd.ExecuteScalarAsync(cancellationToken);

                            _logger.LogInformation("PostGIS version: {Version}", postgisVersion);

                            // Test a simple spatial query
                            await using var spatialCmd = new NpgsqlCommand(
                                "SELECT ST_AsText(ST_GeomFromText('POINT(0 0)', 4326));", connection);
                            var result = await spatialCmd.ExecuteScalarAsync(cancellationToken);

                            _logger.LogInformation("Spatial query test successful: {Result}", result);
                        }
                        else
                        {
                            _logger.LogInformation("PostGIS extension not installed, skipping spatial query tests");
                        }
                    }
                    else
                    {
                        // For non-PostgreSQL databases, just test basic connectivity
                        await using var cmd = new NpgsqlCommand("SELECT 1;", connection);
                        await cmd.ExecuteScalarAsync(cancellationToken);
                        _logger.LogInformation("Database connectivity test successful");
                    }

                    return;
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    _logger.LogWarning(ex, "Database connection failed, retrying ({Attempt}/{MaxRetries})",
                        i + 1, maxRetries);
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }

            throw new InvalidOperationException($"Database connection failed after {maxRetries} attempts");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test database connection");
            throw;
        }
    }

    private string BuildConnectionString(string dbEndpoint)
    {
        // Extract host from endpoint (might be in format "host:port" or just "host")
        var host = dbEndpoint.Split(':')[0];
        var port = dbEndpoint.Contains(':') ? dbEndpoint.Split(':')[1] : "5432";

        // Get password from infrastructure outputs - try multiple possible keys used by various IaC tools
        var password = _state.InfrastructureOutputs?.GetValueOrDefault("db_password", null)
            ?? _state.InfrastructureOutputs?.GetValueOrDefault("rds_password", null)
            ?? _state.InfrastructureOutputs?.GetValueOrDefault("database_master_password", null)
            ?? _state.InfrastructureOutputs?.GetValueOrDefault("postgres_password", null)
            ?? "";

        if (string.IsNullOrEmpty(password))
        {
            _logger.LogError("Database password not found in state or infrastructure outputs. " +
                "Check that state.DatabasePassword is set or infrastructure outputs contain 'db_password', 'rds_password', or 'database_master_password'.");
            throw new InvalidOperationException("Database password not found in deployment state or infrastructure outputs");
        }

        // Get database name from state or use default
        var database = _state.InfrastructureOutputs?.GetValueOrDefault("database_name", "honua") ?? "honua";
        var username = _state.InfrastructureOutputs?.GetValueOrDefault("database_username", "honua_admin") ?? "honua_admin";

        // SSL configuration: Enable Trust Server Certificate by default for RDS/Azure SQL compatibility
        // This is required for managed database services that use self-signed or private CA certificates
        var trustServerCert = _state.InfrastructureOutputs?.GetValueOrDefault("database_trust_server_certificate", "true");
        var sslMode = _state.InfrastructureOutputs?.GetValueOrDefault("database_ssl_mode", "Require") ?? "Require";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate={trustServerCert}";
    }

    private string GetApplicationEndpoint()
    {
        // Try to get load balancer endpoint first
        var lbEndpoint = _state.InfrastructureOutputs?.GetValueOrDefault("load_balancer_endpoint", "");
        if (!string.IsNullOrEmpty(lbEndpoint))
        {
            // Check if endpoint already includes protocol
            if (lbEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                lbEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return lbEndpoint;
            }

            // Try HTTPS first (modern default), fall back to HTTP only if explicitly disabled
            // Most production load balancers use HTTPS
            var sslDisabled = _state.InfrastructureOutputs?.GetValueOrDefault("load_balancer_ssl_enabled", "true") ?? "true";
            var protocol = sslDisabled.Equals("false", StringComparison.OrdinalIgnoreCase) ? "http" : "https";
            return $"{protocol}://{lbEndpoint}";
        }

        // Try application_url from infrastructure outputs
        var appUrl = _state.InfrastructureOutputs?.GetValueOrDefault("application_url", "");
        if (!string.IsNullOrEmpty(appUrl))
        {
            return appUrl;
        }

        // Surface error - do not fabricate hostnames
        _logger.LogError(
            "No load balancer endpoint or application URL found in infrastructure outputs. " +
            "Validation cannot proceed. Ensure infrastructure outputs include 'load_balancer_endpoint' or 'application_url'.");

        throw new InvalidOperationException(
            "Missing required infrastructure output: 'load_balancer_endpoint' or 'application_url' not found in deployment state");
    }

    private void AddAuthenticationHeaders(HttpClient httpClient)
    {
        // Try to get authentication credentials from state first
        var apiKey = _state.ApiKey;
        var bearerToken = _state.BearerToken;

        // Fallback to environment variables if not in state
        if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(bearerToken))
        {
            apiKey = _state.InfrastructureOutputs?.GetValueOrDefault("api_key", null)
                ?? Environment.GetEnvironmentVariable("HONUA_API_KEY");

            bearerToken = _state.InfrastructureOutputs?.GetValueOrDefault("bearer_token", null)
                ?? Environment.GetEnvironmentVariable("HONUA_BEARER_TOKEN");
        }

        // Add authentication header if credentials are available
        if (!string.IsNullOrEmpty(bearerToken))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
            _logger.LogInformation("Added Bearer token authentication to HTTP client");
        }
        else if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            _logger.LogInformation("Added API key authentication to HTTP client");
        }
        else
        {
            _logger.LogWarning(
                "No authentication credentials found in deployment state or environment variables. " +
                "Health checks and OGC endpoint validation may fail on protected APIs. " +
                "Set ApiKey or BearerToken in deployment state, or HONUA_API_KEY/HONUA_BEARER_TOKEN environment variables.");
        }
    }

}
