// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Deployment;

/// <summary>
/// File-based deployment state store
/// Stores state in JSON files on filesystem
/// Thread-safe with file locking
/// </summary>
public class FileStateStore : IDeploymentStateStore
{
    private readonly string _stateDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileStateStore(string stateDirectory)
    {
        _stateDirectory = stateDirectory;
        // Use WebIndented as base and add custom enum converter for deployment state
        _jsonOptions = new JsonSerializerOptions(JsonSerializerOptionsRegistry.WebIndented)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        // Ensure state directory exists
        FileOperationHelper.EnsureDirectoryExists(_stateDirectory);
    }

    public async Task<Deployment> CreateDeploymentAsync(
        string environment,
        string commit,
        string branch = "main",
        string initiatedBy = "system",
        bool autoRollback = true,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = new Deployment
            {
                Id = GenerateDeploymentId(environment),
                Environment = environment,
                Commit = commit,
                Branch = branch,
                State = DeploymentState.Pending,
                Health = DeploymentHealth.Unknown,
                SyncStatus = SyncStatus.OutOfSync,
                StartedAt = DateTime.UtcNow,
                InitiatedBy = initiatedBy,
                AutoRollback = autoRollback
            };

            deployment.StateHistory.Add(new StateTransition
            {
                To = DeploymentState.Pending,
                Timestamp = DateTime.UtcNow,
                Message = "Deployment created"
            });

            // Load current state
            var state = await LoadEnvironmentStateInternalAsync(environment, cancellationToken);

            // Set as current deployment
            state.CurrentDeployment = deployment;
            state.LastUpdated = DateTime.UtcNow;

            // Add to history
            state.History.Insert(0, new DeploymentSummary
            {
                Id = deployment.Id,
                Commit = commit,
                State = DeploymentState.Pending,
                StartedAt = deployment.StartedAt,
                InitiatedBy = initiatedBy
            });

            // Keep only recent history
            if (state.History.Count > 50)
            {
                state.History = state.History.Take(50).ToList();
            }

            await SaveEnvironmentStateAsync(environment, state, cancellationToken);

            return deployment;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Deployment?> GetDeploymentAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        // Extract environment from deployment ID (format: env-timestamp)
        var parts = deploymentId.Split('-');
        if (parts.Length < 2)
            return null;

        var environment = parts[0];
        var state = await LoadEnvironmentStateInternalAsync(environment, cancellationToken);

        if (state.CurrentDeployment?.Id == deploymentId)
            return state.CurrentDeployment;

        if (state.LastSuccessfulDeployment?.Id == deploymentId)
            return state.LastSuccessfulDeployment;

        return null;
    }

    public async Task TransitionAsync(
        string deploymentId,
        DeploymentState newState,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = await GetDeploymentAsync(deploymentId, cancellationToken);
            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            var previousState = deployment.State;
            deployment.State = newState;
            deployment.UpdatedAt = DateTime.UtcNow;

            // Record state transition
            deployment.StateHistory.Add(new StateTransition
            {
                From = previousState,
                To = newState,
                Timestamp = DateTime.UtcNow,
                Message = message
            });

            // Update completed timestamp if terminal state
            if (newState == DeploymentState.Completed ||
                newState == DeploymentState.Failed ||
                newState == DeploymentState.RolledBack)
            {
                deployment.CompletedAt = DateTime.UtcNow;

                // Update last successful deployment
                if (newState == DeploymentState.Completed)
                {
                    var state = await LoadEnvironmentStateInternalAsync(deployment.Environment, cancellationToken);
                    state.LastSuccessfulDeployment = deployment;
                    state.DeployedCommit = deployment.Commit;
                    state.Health = DeploymentHealth.Healthy;
                    state.SyncStatus = SyncStatus.Synced;
                    await SaveEnvironmentStateAsync(deployment.Environment, state, cancellationToken);
                }
            }

            await UpdateDeploymentAsync(deployment, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateHealthAsync(
        string deploymentId,
        DeploymentHealth health,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = await GetDeploymentAsync(deploymentId, cancellationToken);
            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            deployment.Health = health;
            deployment.UpdatedAt = DateTime.UtcNow;

            await UpdateDeploymentAsync(deployment, cancellationToken);

            // Update environment health
            var state = await LoadEnvironmentStateInternalAsync(deployment.Environment, cancellationToken);
            state.Health = health;
            state.LastUpdated = DateTime.UtcNow;
            await SaveEnvironmentStateAsync(deployment.Environment, state, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSyncStatusAsync(
        string deploymentId,
        SyncStatus syncStatus,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = await GetDeploymentAsync(deploymentId, cancellationToken);
            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            deployment.SyncStatus = syncStatus;
            deployment.UpdatedAt = DateTime.UtcNow;

            await UpdateDeploymentAsync(deployment, cancellationToken);

            // Update environment sync status
            var state = await LoadEnvironmentStateInternalAsync(deployment.Environment, cancellationToken);
            state.SyncStatus = syncStatus;
            state.LastUpdated = DateTime.UtcNow;
            await SaveEnvironmentStateAsync(deployment.Environment, state, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetPlanAsync(
        string deploymentId,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = await GetDeploymentAsync(deploymentId, cancellationToken);
            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            deployment.Plan = plan;
            deployment.UpdatedAt = DateTime.UtcNow;

            await UpdateDeploymentAsync(deployment, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddValidationResultAsync(
        string deploymentId,
        ValidationResult result,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = await GetDeploymentAsync(deploymentId, cancellationToken);
            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            deployment.ValidationResults.Add(result);
            deployment.UpdatedAt = DateTime.UtcNow;

            await UpdateDeploymentAsync(deployment, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetBackupIdAsync(
        string deploymentId,
        string backupId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = await GetDeploymentAsync(deploymentId, cancellationToken);
            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            deployment.BackupId = backupId;
            deployment.UpdatedAt = DateTime.UtcNow;

            await UpdateDeploymentAsync(deployment, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task FailDeploymentAsync(
        string deploymentId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deployment = await GetDeploymentAsync(deploymentId, cancellationToken);
            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            deployment.State = DeploymentState.Failed;
            deployment.Health = DeploymentHealth.Unhealthy;
            deployment.ErrorMessage = errorMessage;
            deployment.CompletedAt = DateTime.UtcNow;
            deployment.UpdatedAt = DateTime.UtcNow;

            deployment.StateHistory.Add(new StateTransition
            {
                From = deployment.State,
                To = DeploymentState.Failed,
                Timestamp = DateTime.UtcNow,
                Message = errorMessage
            });

            await UpdateDeploymentAsync(deployment, cancellationToken);

            // Update environment health
            var state = await LoadEnvironmentStateInternalAsync(deployment.Environment, cancellationToken);
            state.Health = DeploymentHealth.Degraded;
            state.LastUpdated = DateTime.UtcNow;
            await SaveEnvironmentStateAsync(deployment.Environment, state, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Deployment?> GetCurrentDeploymentAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        var state = await LoadEnvironmentStateInternalAsync(environment, cancellationToken);
        return state.CurrentDeployment;
    }

    public async Task<Deployment?> GetLastSuccessfulDeploymentAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        var state = await LoadEnvironmentStateInternalAsync(environment, cancellationToken);
        return state.LastSuccessfulDeployment;
    }

    public async Task<EnvironmentState> GetEnvironmentStateAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        return await LoadEnvironmentStateInternalAsync(environment, cancellationToken);
    }

    public async Task<List<DeploymentSummary>> GetDeploymentHistoryAsync(
        string environment,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var state = await LoadEnvironmentStateInternalAsync(environment, cancellationToken);
        return state.History.Take(limit).ToList();
    }

    public Task<List<string>> ListEnvironmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var files = Directory.GetFiles(_stateDirectory, "*.json");
        var result = files
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(name => !name.IsNullOrEmpty())
            .ToList();
        return Task.FromResult(result);
    }

    // Private helper methods

    private async Task<EnvironmentState> LoadEnvironmentStateInternalAsync(
        string environment,
        CancellationToken cancellationToken)
    {
        var filePath = GetStateFilePath(environment);

        if (!FileOperationHelper.FileExists(filePath))
        {
            return new EnvironmentState
            {
                Environment = environment,
                LastUpdated = DateTime.UtcNow
            };
        }

        var json = await FileOperationHelper.SafeReadAllTextAsync(filePath, cancellationToken: cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<EnvironmentState>(json, _jsonOptions)
            ?? new EnvironmentState { Environment = environment };
    }

    private async Task SaveEnvironmentStateAsync(
        string environment,
        EnvironmentState state,
        CancellationToken cancellationToken)
    {
        var filePath = GetStateFilePath(environment);
        state.LastUpdated = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await FileOperationHelper.SafeWriteAllTextAsync(filePath, json, createDirectory: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateDeploymentAsync(
        Deployment deployment,
        CancellationToken cancellationToken)
    {
        var state = await LoadEnvironmentStateInternalAsync(deployment.Environment, cancellationToken);

        if (state.CurrentDeployment?.Id == deployment.Id)
        {
            state.CurrentDeployment = deployment;
        }

        if (state.LastSuccessfulDeployment?.Id == deployment.Id)
        {
            state.LastSuccessfulDeployment = deployment;
        }

        // Update history entry
        var historyEntry = state.History.FirstOrDefault(h => h.Id == deployment.Id);
        if (historyEntry != null)
        {
            historyEntry.State = deployment.State;
            historyEntry.CompletedAt = deployment.CompletedAt;
            historyEntry.Duration = deployment.Duration;
        }

        state.LastUpdated = DateTime.UtcNow;
        await SaveEnvironmentStateAsync(deployment.Environment, state, cancellationToken);
    }

    private string GetStateFilePath(string environment)
    {
        return Path.Combine(_stateDirectory, $"{environment}.json");
    }

    private static string GenerateDeploymentId(string environment)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return $"{environment}-{timestamp}";
    }
}
