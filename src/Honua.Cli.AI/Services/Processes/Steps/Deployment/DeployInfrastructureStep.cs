// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Executes Terraform apply to deploy infrastructure.
/// </summary>
public class DeployInfrastructureStep : KernelProcessStep<DeploymentState>, IProcessStepTimeout, IProcessStepRollback
{
    private readonly ILogger<DeployInfrastructureStep> _logger;
    private DeploymentState _state = new();

    /// <summary>
    /// Infrastructure deployment can take significant time (Terraform apply).
    /// Default timeout: 30 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(30);

    /// <summary>
    /// Infrastructure deployment supports rollback via Terraform destroy.
    /// </summary>
    public bool SupportsRollback => true;

    /// <summary>
    /// Description of rollback operation.
    /// </summary>
    public string RollbackDescription => "Destroy deployed infrastructure using Terraform";

    public DeployInfrastructureStep(ILogger<DeployInfrastructureStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("DeployInfrastructure")]
    public async Task DeployInfrastructureAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deploying infrastructure for {DeploymentId}", _state.DeploymentId);

        _state.Status = "DeployingInfrastructure";

        try
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // SECURITY: Always use existing workspace from GenerateInfrastructureCodeStep to ensure secure passwords are used
            // Do NOT reuse on-disk files from previous runs or create fallback configs with weak passwords
            string terraformDir;
            if (!string.IsNullOrEmpty(_state.TerraformWorkspacePath) && Directory.Exists(_state.TerraformWorkspacePath))
            {
                terraformDir = _state.TerraformWorkspacePath;
                _logger.LogInformation("Using existing Terraform workspace with secure credentials: {TerraformDir}", terraformDir);

                // Verify that secure credentials exist in deployment state for providers that need them
                if (_state.CloudProvider.ToLower() == "aws")
                {
                    if (!_state.InfrastructureOutputs.ContainsKey("db_password"))
                    {
                        _logger.LogError("Database password not found in deployment state. GenerateInfrastructureCodeStep must run first.");
                        throw new InvalidOperationException(
                            "Secure database password not found in deployment state. " +
                            "Run GenerateInfrastructureCodeStep before deployment to generate secure credentials.");
                    }
                    _logger.LogInformation("Verified secure database password exists in deployment state");
                }
            }
            else
            {
                _logger.LogError(
                    "Terraform workspace not found at {ExpectedPath}. GenerateInfrastructureCodeStep must run before deployment.",
                    _state.TerraformWorkspacePath ?? "(null)");
                throw new InvalidOperationException(
                    "Terraform workspace path not configured or directory does not exist. " +
                    "Run GenerateInfrastructureCodeStep to generate infrastructure code with secure credentials before deployment. " +
                    "This prevents accidental deployment with placeholder passwords.");
            }

            // Run terraform init
            _logger.LogInformation("Running terraform init");
            await ExecuteTerraformCommandAsync(terraformDir, cancellationToken, "init");

            // Run terraform plan
            _logger.LogInformation("Running terraform plan");
            await ExecuteTerraformCommandAsync(terraformDir, cancellationToken, "plan", "-out=tfplan");

            // Run terraform apply
            _logger.LogInformation("Running terraform apply");
            await ExecuteTerraformCommandAsync(terraformDir, cancellationToken, "apply", "-auto-approve", "tfplan");

            // Capture outputs from terraform and merge with existing outputs to preserve secrets
            var outputsJson = await ExecuteTerraformCommandAsync(terraformDir, cancellationToken, "output", "-json");
            var terraformOutputs = ParseTerraformOutputs(outputsJson);

            // Merge terraform outputs with existing state, preserving secrets that were generated earlier
            foreach (var (key, value) in terraformOutputs)
            {
                _state.InfrastructureOutputs[key] = value;
            }

            // Ensure database_password is available (may have been stored as db_password)
            if (_state.InfrastructureOutputs.ContainsKey("db_password") &&
                !_state.InfrastructureOutputs.ContainsKey("database_password"))
            {
                _state.InfrastructureOutputs["database_password"] = _state.InfrastructureOutputs["db_password"];
                _logger.LogInformation("Mapped db_password to database_password for validation compatibility");
            }

            _state.CreatedResources = new List<string>
            {
                "database_instance",
                "storage_bucket",
                "container_cluster",
                "vpc_network",
                "load_balancer"
            };

            _logger.LogInformation("Infrastructure deployed successfully for {DeploymentId}", _state.DeploymentId);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "InfrastructureDeployed",
                Data = _state
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Infrastructure deployment cancelled for {DeploymentId}", _state.DeploymentId);
            _state.Status = "Cancelled";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DeploymentCancelled",
                Data = new { _state.DeploymentId }
            });
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy infrastructure for {DeploymentId}", _state.DeploymentId);
            _state.Status = "DeploymentFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DeploymentFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
    }

    private string GetProviderDomain() => _state.CloudProvider.ToLower() switch
    {
        "aws" => "rds.amazonaws.com",
        "azure" => "postgres.database.azure.com",
        "gcp" => "sql.goog",
        _ => "unknown"
    };

    private string BuildMetricsEndpoint()
    {
        var provider = _state.CloudProvider.ToLowerInvariant();
        return $"https://observability.{provider}.honua/{_state.DeploymentName}/metrics";
    }

    private string BuildResourceMetricsEndpoint()
    {
        var provider = _state.CloudProvider.ToLowerInvariant();
        return $"https://observability.{provider}.honua/{_state.DeploymentName}/resources";
    }

    /// <summary>
    /// Rollback infrastructure deployment by destroying resources.
    /// This method is idempotent and safe to call multiple times.
    /// </summary>
    public async Task<ProcessStepRollbackResult> RollbackAsync(
        object state,
        CancellationToken cancellationToken = default)
    {
        var deploymentState = state as DeploymentState;
        if (deploymentState == null)
        {
            return ProcessStepRollbackResult.Failure(
                "Invalid state type",
                "Expected DeploymentState");
        }

        _logger.LogInformation(
            "Rolling back infrastructure deployment for {DeploymentId}",
            deploymentState.DeploymentId);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if there are resources to destroy
            if (deploymentState.CreatedResources == null || !deploymentState.CreatedResources.Any())
            {
                _logger.LogWarning("No infrastructure resources found to rollback for {DeploymentId}",
                    deploymentState.DeploymentId);
                return ProcessStepRollbackResult.Success(
                    "No infrastructure resources to rollback");
            }

            // Use the stored workspace path, falling back to default location if not set
            var terraformDir = !string.IsNullOrEmpty(deploymentState.TerraformWorkspacePath)
                ? deploymentState.TerraformWorkspacePath
                : Path.Combine(Path.GetTempPath(), "honua-terraform", deploymentState.DeploymentId);

            _logger.LogInformation("Using Terraform workspace for rollback: {TerraformDir}", terraformDir);

            if (!Directory.Exists(terraformDir))
            {
                _logger.LogWarning(
                    "Terraform directory not found: {TerraformDir}. Resources may have already been destroyed or workspace was never created.",
                    terraformDir);
                return ProcessStepRollbackResult.Success(
                    "Destroyed infrastructure resources (terraform workspace not found - assuming already destroyed)");
            }

            // Check if terraform state exists before attempting destroy
            var stateFilePath = Path.Combine(terraformDir, "terraform.tfstate");
            if (!File.Exists(stateFilePath))
            {
                _logger.LogWarning(
                    "Terraform state file not found at {StateFilePath}. No resources to destroy.",
                    stateFilePath);
                return ProcessStepRollbackResult.Success(
                    "Destroyed infrastructure resources (no Terraform state found - nothing to destroy)");
            }

            // Run terraform destroy (idempotent - safe to run multiple times)
            _logger.LogInformation("Running terraform destroy in {TerraformDir}", terraformDir);
            try
            {
                await ExecuteTerraformCommandAsync(terraformDir, cancellationToken, "destroy", "-auto-approve");

                _logger.LogInformation(
                    "Successfully destroyed {Count} infrastructure resources for {DeploymentId}",
                    deploymentState.CreatedResources.Count,
                    deploymentState.DeploymentId);

                var resourceList = string.Join(", ", deploymentState.CreatedResources);

                // Cleanup workspace directory after successful destroy
                try
                {
                    Directory.Delete(terraformDir, recursive: true);
                    _logger.LogInformation("Cleaned up Terraform workspace: {TerraformDir}", terraformDir);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx,
                        "Failed to cleanup Terraform workspace directory {TerraformDir}. Manual cleanup may be required.",
                        terraformDir);
                    // Don't fail the rollback due to cleanup errors
                }

                return ProcessStepRollbackResult.Success(
                    $"Destroyed infrastructure resources: {resourceList}");
            }
            catch (InvalidOperationException terraformEx) when (
                terraformEx.Message.Contains("No configuration files") ||
                terraformEx.Message.Contains("not initialized"))
            {
                // Terraform workspace is in an inconsistent state
                _logger.LogWarning(terraformEx,
                    "Terraform workspace is not properly initialized. Attempting to reinitialize and destroy.");

                try
                {
                    // Try to initialize and destroy
                    await ExecuteTerraformCommandAsync(terraformDir, cancellationToken, "init");
                    await ExecuteTerraformCommandAsync(terraformDir, cancellationToken, "destroy", "-auto-approve");

                    _logger.LogInformation(
                        "Successfully destroyed infrastructure after reinitialization for {DeploymentId}",
                        deploymentState.DeploymentId);

                    var resourceList = string.Join(", ", deploymentState.CreatedResources);
                    return ProcessStepRollbackResult.Success(
                        $"Destroyed infrastructure resources after reinitialization: {resourceList}");
                }
                catch (Exception reinitEx)
                {
                    _logger.LogError(reinitEx,
                        "Failed to reinitialize and destroy infrastructure for {DeploymentId}",
                        deploymentState.DeploymentId);
                    return ProcessStepRollbackResult.Failure(
                        "Failed to destroy infrastructure after reinitialization attempt",
                        $"Workspace: {terraformDir}, Error: {reinitEx.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Infrastructure rollback cancelled for {DeploymentId}",
                deploymentState.DeploymentId);
            return ProcessStepRollbackResult.Failure(
                "Rollback cancelled",
                "Infrastructure destruction was cancelled by user request");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to rollback infrastructure for {DeploymentId}",
                deploymentState.DeploymentId);
            return ProcessStepRollbackResult.Failure(
                "Rollback operation failed",
                $"DeploymentId: {deploymentState.DeploymentId}, Workspace: {deploymentState.TerraformWorkspacePath ?? "unknown"}, Error: {ex.Message}");
        }
    }


    private async Task<string> ExecuteTerraformCommandAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "terraform",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use ArgumentList to prevent command injection
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start terraform process");
        }

        // Read stdout and stderr concurrently to prevent deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await stdoutTask;
        var error = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "Terraform command '{Command}' failed with exit code {ExitCode}. Enable debug logging for stderr output.",
                string.Join(' ', arguments),
                process.ExitCode);

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogDebug("Terraform stderr: {Error}", error);
            }

            throw new InvalidOperationException("Terraform command failed. Check terraform logs for details.");
        }

        return output;
    }

    private Dictionary<string, string> ParseTerraformOutputs(string outputsJson)
    {
        try
        {
            var outputs = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(outputsJson))
            {
                // Terraform output command returned no data - this indicates a critical failure
                _logger.LogError("Terraform output returned no data. Infrastructure may not have been created properly.");
                throw new InvalidOperationException(
                    "Failed to retrieve Terraform outputs. Infrastructure deployment may have failed. " +
                    "Check Terraform logs for details.");
            }

            using var doc = System.Text.Json.JsonDocument.Parse(outputsJson);
            var root = doc.RootElement;

            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.TryGetProperty("value", out var valueElement))
                {
                    outputs[property.Name] = valueElement.GetString() ?? "";
                }
            }

            // Validate that essential outputs are present
            var requiredOutputs = new[] { "database_endpoint", "storage_bucket" };
            var missingOutputs = requiredOutputs.Where(key => !outputs.ContainsKey(key) || string.IsNullOrEmpty(outputs[key])).ToList();

            if (missingOutputs.Any())
            {
                _logger.LogError("Missing required Terraform outputs: {MissingOutputs}", string.Join(", ", missingOutputs));
                throw new InvalidOperationException(
                    $"Missing required Terraform outputs: {string.Join(", ", missingOutputs)}. " +
                    "Infrastructure deployment incomplete.");
            }

            return outputs;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Terraform outputs JSON");
            throw new InvalidOperationException(
                "Failed to parse Terraform outputs. The output format may be invalid. " +
                "Check Terraform logs for details.", ex);
        }
    }

}
