// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Secrets;
using Honua.Cli.AI.Services.Execution.Executors;
using Honua.Cli.AI.Services.Planning;
using Honua.Cli.AI.Services.Rollback;
using Honua.Cli.AI.Services.Validation;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Execution;

/// <summary>
/// Executes approved execution plans with comprehensive safety checks and rollback support.
/// </summary>
public sealed class PlanExecutor : IPlanExecutor
{
    private readonly IPlanValidator _validator;
    private readonly ISnapshotManager _snapshotManager;
    private readonly ILogger<PlanExecutor> _logger;
    private readonly Dictionary<StepType, IStepExecutor> _executors;

    public PlanExecutor(
        IPlanValidator validator,
        ISnapshotManager snapshotManager,
        ILogger<PlanExecutor> logger)
    {
        _validator = validator;
        _snapshotManager = snapshotManager;
        _logger = logger;

        // Initialize step executors
        _executors = InitializeExecutors();
    }

    private Dictionary<StepType, IStepExecutor> InitializeExecutors()
    {
        var executors = new List<IStepExecutor>
        {
            new CreateIndexExecutor(),
            new VacuumAnalyzeExecutor()
            // Additional executors can be added here as they're implemented
        };

        return executors.ToDictionary(e => e.SupportedStepType);
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionPlan plan,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var stepResults = new List<StepResult>();
        string? snapshotId = null;

        try
        {
            _logger.LogInformation("Starting execution of plan {PlanId}: {Title}", plan.Id, plan.Title);
            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.PlanStarted,
                Message = $"Starting execution of plan: {plan.Title}",
                PlanId = plan.Id
            });

            // Validate plan before execution
            var validationResult = await _validator.ValidateAsync(plan, cancellationToken);
            if (!validationResult.IsValid)
            {
                _logger.LogError("Plan validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return new ExecutionResult
                {
                    Plan = plan,
                    Success = false,
                    StepResults = stepResults,
                    ErrorMessage = $"Plan validation failed: {string.Join(", ", validationResult.Errors)}",
                    StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow
                };
            }

            // Create snapshot before execution
            if (plan.Risk.Level >= RiskLevel.Medium)
            {
                _logger.LogInformation("Creating snapshot before executing plan");
                snapshotId = await _snapshotManager.CreateSnapshotAsync(
                    context.WorkspacePath,
                    plan.Id,
                    cancellationToken);

                context.LogEvent(new ExecutionEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Type = ExecutionEventType.SnapshotCreated,
                    Message = $"Created snapshot: {snapshotId}",
                    PlanId = plan.Id,
                    Metadata = new Dictionary<string, object> { ["SnapshotId"] = snapshotId }
                });
            }

            // Update plan status
            plan.Status = PlanStatus.Applying;
            plan.AppliedAt = DateTime.UtcNow;

            // Execute steps in order
            foreach (var step in plan.Steps.OrderBy(s => s.StepNumber))
            {
                // Check dependencies
                if (step.DependsOn.Any())
                {
                    var dependencyResults = stepResults
                        .Where(r => step.DependsOn.Contains(r.Step.StepNumber))
                        .ToList();

                    if (dependencyResults.Any(r => !r.Success))
                    {
                        _logger.LogWarning("Skipping step {StepNumber} due to failed dependencies", step.StepNumber);
                        stepResults.Add(new StepResult
                        {
                            Step = step,
                            Success = false,
                            ErrorMessage = "Dependency failed",
                            StartedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow
                        });
                        step.Status = StepStatus.Skipped;
                        continue;
                    }
                }

                // Execute step
                var stepResult = await ExecuteStepAsync(step, plan, context, cancellationToken);
                stepResults.Add(stepResult);

                // Check if we should continue on error
                if (!stepResult.Success && !context.ContinueOnError)
                {
                    _logger.LogError("Step {StepNumber} failed, aborting execution", step.StepNumber);
                    break;
                }
            }

            // Determine overall success
            var success = stepResults.All(r => r.Success);
            plan.Status = success ? PlanStatus.Completed : PlanStatus.Failed;

            var completedAt = DateTime.UtcNow;
            context.LogEvent(new ExecutionEvent
            {
                Timestamp = completedAt,
                Type = success ? ExecutionEventType.PlanCompleted : ExecutionEventType.PlanFailed,
                Message = success
                    ? $"Plan execution completed successfully in {(completedAt - startedAt).TotalSeconds:F2}s"
                    : "Plan execution failed",
                PlanId = plan.Id
            });

            _logger.LogInformation("Plan execution {Status} in {Duration}s",
                success ? "completed" : "failed",
                (completedAt - startedAt).TotalSeconds);

            return new ExecutionResult
            {
                Plan = plan,
                Success = success,
                StepResults = stepResults,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                SnapshotId = snapshotId,
                ErrorMessage = success ? null : "One or more steps failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during plan execution");
            plan.Status = PlanStatus.Failed;

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.PlanFailed,
                Message = $"Plan execution failed with exception: {ex.Message}",
                PlanId = plan.Id
            });

            return new ExecutionResult
            {
                Plan = plan,
                Success = false,
                StepResults = stepResults,
                ErrorMessage = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                SnapshotId = snapshotId
            };
        }
    }

    private async Task<StepResult> ExecuteStepAsync(
        PlanStep step,
        ExecutionPlan plan,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Executing step {StepNumber}: {Description}",
                step.StepNumber, step.Description);

            step.Status = StepStatus.Running;
            step.StartedAt = startedAt;

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = startedAt,
                Type = ExecutionEventType.StepStarted,
                Message = $"Step {step.StepNumber}: {step.Description}",
                PlanId = plan.Id,
                StepNumber = step.StepNumber
            });

            // Execute the step based on its type
            var output = await ExecuteStepOperationAsync(step, plan, context, cancellationToken);

            var completedAt = DateTime.UtcNow;
            step.Status = StepStatus.Completed;
            step.CompletedAt = completedAt;

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = completedAt,
                Type = ExecutionEventType.StepCompleted,
                Message = $"Step {step.StepNumber} completed in {(completedAt - startedAt).TotalSeconds:F2}s",
                PlanId = plan.Id,
                StepNumber = step.StepNumber
            });

            _logger.LogInformation("Step {StepNumber} completed successfully", step.StepNumber);

            return new StepResult
            {
                Step = step,
                Success = true,
                Output = output,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step {StepNumber} failed: {Message}",
                step.StepNumber, ex.Message);

            var completedAt = DateTime.UtcNow;
            step.Status = StepStatus.Failed;
            step.CompletedAt = completedAt;
            step.ErrorMessage = ex.Message;

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = completedAt,
                Type = ExecutionEventType.StepFailed,
                Message = $"Step {step.StepNumber} failed: {ex.Message}",
                PlanId = plan.Id,
                StepNumber = step.StepNumber
            });

            return new StepResult
            {
                Step = step,
                Success = false,
                ErrorMessage = ex.Message,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }
    }

    private async Task<string> ExecuteStepOperationAsync(
        PlanStep step,
        ExecutionPlan plan,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing {StepType} operation: {Operation}",
            step.Type, step.Operation);

        // Check if we have a dedicated executor for this step type
        if (_executors.TryGetValue(step.Type, out var executor))
        {
            _logger.LogDebug("Using dedicated executor {ExecutorType} for step {StepNumber}",
                executor.GetType().Name, step.StepNumber);

            // Execute using the dedicated executor
            var executionResult = await executor.ExecuteAsync(step, context, cancellationToken);

            if (!executionResult.Success)
            {
                throw new InvalidOperationException(
                    executionResult.ErrorMessage ?? $"Step execution failed: {step.Description}");
            }

            return executionResult.Output ?? $"Step completed: {step.Description}";
        }

        // Fallback for step types without dedicated executors
        _logger.LogWarning("No dedicated executor found for step type {StepType}. Using fallback execution.",
            step.Type);

        return await ExecuteStepFallbackAsync(step, context, cancellationToken);
    }

    /// <summary>
    /// Fallback execution for step types that don't have dedicated executors yet.
    /// This provides basic functionality while executors are being implemented.
    /// </summary>
    private async Task<string> ExecuteStepFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing fallback for step type {StepType}", step.Type);

        // Provide basic execution for common step types
        return step.Type switch
        {
            StepType.CreateStatistics => await ExecuteCreateStatisticsFallbackAsync(step, context, cancellationToken),
            StepType.DropIndex => await ExecuteDropIndexFallbackAsync(step, context, cancellationToken),
            StepType.UpdateConfig => await ExecuteUpdateConfigFallbackAsync(step, context, cancellationToken),
            StepType.CreateTable => await ExecuteCreateTableFallbackAsync(step, context, cancellationToken),
            StepType.AlterTable => await ExecuteAlterTableFallbackAsync(step, context, cancellationToken),
            StepType.Backup => await ExecuteBackupFallbackAsync(step, context, cancellationToken),
            StepType.Restore => await ExecuteRestoreFallbackAsync(step, context, cancellationToken),
            StepType.Custom => await ExecuteCustomFallbackAsync(step, context, cancellationToken),
            _ => throw new NotImplementedException(
                $"Step type '{step.Type}' does not have a dedicated executor or fallback implementation. " +
                $"Please implement IStepExecutor for this step type.")
        };
    }

    /// <summary>
    /// Fallback execution for CREATE STATISTICS operations.
    /// </summary>
    private async Task<string> ExecuteCreateStatisticsFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing CREATE STATISTICS fallback: {Operation}", step.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "CREATE STATISTICS" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Create statistics: {step.Description}",
                Operations = new List<string> { "CREATE STATISTICS" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(step.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return $"Statistics created successfully: {step.Description}";
    }

    /// <summary>
    /// Fallback execution for DROP INDEX operations.
    /// </summary>
    private async Task<string> ExecuteDropIndexFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing DROP INDEX fallback: {Operation}", step.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "DROP INDEX" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Drop index: {step.Description}",
                Operations = new List<string> { "DROP INDEX" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(step.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return $"Index dropped successfully: {step.Description}";
    }

    /// <summary>
    /// Fallback execution for UPDATE CONFIG operations.
    /// </summary>
    private async Task<string> ExecuteUpdateConfigFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing UPDATE CONFIG fallback: {Operation}", step.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.Admin,
                    AllowedOperations = new List<string> { "ALTER SYSTEM", "SET" }
                },
                Duration = TimeSpan.FromMinutes(5),
                Purpose = $"Update configuration: {step.Description}",
                Operations = new List<string> { "ALTER SYSTEM", "SET" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(step.Operation, conn)
        {
            CommandTimeout = 300 // 5 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return $"Configuration updated successfully: {step.Description}";
    }

    /// <summary>
    /// Fallback execution for CREATE TABLE operations.
    /// </summary>
    private async Task<string> ExecuteCreateTableFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing CREATE TABLE fallback: {Operation}", step.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "CREATE TABLE" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Create table: {step.Description}",
                Operations = new List<string> { "CREATE TABLE" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(step.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return $"Table created successfully: {step.Description}";
    }

    /// <summary>
    /// Fallback execution for ALTER TABLE operations.
    /// </summary>
    private async Task<string> ExecuteAlterTableFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing ALTER TABLE fallback: {Operation}", step.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "ALTER TABLE" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Alter table: {step.Description}",
                Operations = new List<string> { "ALTER TABLE" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(step.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return $"Table altered successfully: {step.Description}";
    }

    /// <summary>
    /// Fallback execution for BACKUP operations.
    /// </summary>
    private async Task<string> ExecuteBackupFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing BACKUP fallback: {Operation}", step.Operation);

        // Backup operations typically involve executing pg_dump or similar tools
        // For now, we'll use the snapshot manager approach
        var snapshotId = await _snapshotManager.CreateSnapshotAsync(
            context.WorkspacePath,
            $"backup_{DateTime.UtcNow:yyyyMMddHHmmss}",
            cancellationToken);

        return $"Backup created successfully: {snapshotId}";
    }

    /// <summary>
    /// Fallback execution for RESTORE operations.
    /// </summary>
    private async Task<string> ExecuteRestoreFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing RESTORE fallback: {Operation}", step.Operation);

        // Parse the operation to extract snapshot ID
        // Expected format: "RESTORE FROM snapshot_id" or JSON
        var snapshotId = step.Operation.Replace("RESTORE FROM", "", StringComparison.OrdinalIgnoreCase).Trim();

        await _snapshotManager.RestoreSnapshotAsync(
            snapshotId,
            cancellationToken);

        return $"Restore completed successfully from snapshot: {snapshotId}";
    }

    /// <summary>
    /// Fallback execution for CUSTOM operations.
    /// </summary>
    private async Task<string> ExecuteCustomFallbackAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing CUSTOM fallback: {Operation}", step.Operation);

        // Custom operations should be SQL statements
        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "CUSTOM" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Execute custom operation: {step.Description}",
                Operations = new List<string> { "CUSTOM" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(step.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return $"Custom operation completed: {step.Description}" +
               (result != null ? $"\nResult: {result}" : "");
    }

    /// <summary>
    /// Legacy rollback method without execution context.
    /// </summary>
    /// <remarks>
    /// This method is obsolete and cannot execute rollback operations without an execution context.
    /// Use the overload that accepts IExecutionContext instead.
    /// </remarks>
    [Obsolete("Use RollbackAsync(ExecutionPlan plan, IExecutionContext context, CancellationToken cancellationToken) instead")]
    public Task<RollbackResult> RollbackAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        _logger.LogError(
            "RollbackAsync called without execution context for plan {PlanId}. This method is obsolete.",
            plan.Id);

        // Return a failed result instead of throwing, making it easier for callers to handle
        return Task.FromResult(new RollbackResult
        {
            Success = false,
            StepResults = new List<RollbackStepResult>(),
            ErrorMessage = "RollbackAsync requires IExecutionContext parameter to execute rollback operations. " +
                          "Please use the overload: RollbackAsync(ExecutionPlan plan, IExecutionContext context, CancellationToken cancellationToken)",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Rolls back a partially executed or failed plan with execution context.
    /// </summary>
    public async Task<RollbackResult> RollbackAsync(
        ExecutionPlan plan,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var stepResults = new List<RollbackStepResult>();

        try
        {
            _logger.LogInformation("Starting rollback of plan {PlanId}", plan.Id);

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = startedAt,
                Type = ExecutionEventType.RollbackStarted,
                Message = $"Starting rollback for plan: {plan.Title}",
                PlanId = plan.Id
            });

            if (plan.RollbackPlan == null)
            {
                _logger.LogWarning("No rollback plan available for plan {PlanId}", plan.Id);
                return new RollbackResult
                {
                    Success = false,
                    StepResults = stepResults,
                    ErrorMessage = "No rollback plan available",
                    StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow
                };
            }

            _logger.LogInformation("Executing {StepCount} rollback steps for plan {PlanId}",
                plan.RollbackPlan.Steps.Count, plan.Id);

            // Execute rollback steps in order (they're already in reverse order in the plan)
            foreach (var rollbackStep in plan.RollbackPlan.Steps)
            {
                var stepStartedAt = DateTime.UtcNow;

                try
                {
                    _logger.LogInformation("Executing rollback step: {Description}",
                        rollbackStep.Description);

                    // Execute the rollback operation
                    var output = await ExecuteRollbackStepAsync(rollbackStep, plan, context, cancellationToken);

                    _logger.LogInformation("Rollback step completed: {Description}", rollbackStep.Description);

                    stepResults.Add(new RollbackStepResult
                    {
                        Step = rollbackStep,
                        Success = true,
                        StartedAt = stepStartedAt,
                        CompletedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Rollback step failed: {Description}",
                        rollbackStep.Description);

                    stepResults.Add(new RollbackStepResult
                    {
                        Step = rollbackStep,
                        Success = false,
                        ErrorMessage = ex.Message,
                        StartedAt = stepStartedAt,
                        CompletedAt = DateTime.UtcNow
                    });

                    // Continue with remaining rollback steps even if one fails
                    // to attempt maximum recovery
                }
            }

            var success = stepResults.All(r => r.Success);
            if (success)
            {
                plan.Status = PlanStatus.RolledBack;
                _logger.LogInformation("Rollback completed successfully for plan {PlanId}", plan.Id);
            }
            else
            {
                var failedCount = stepResults.Count(r => !r.Success);
                _logger.LogWarning("Rollback completed with {FailedCount} failed steps for plan {PlanId}",
                    failedCount, plan.Id);
            }

            var completedAt = DateTime.UtcNow;
            context.LogEvent(new ExecutionEvent
            {
                Timestamp = completedAt,
                Type = ExecutionEventType.RollbackCompleted,
                Message = success
                    ? $"Rollback completed successfully in {(completedAt - startedAt).TotalSeconds:F2}s"
                    : $"Rollback completed with failures in {(completedAt - startedAt).TotalSeconds:F2}s",
                PlanId = plan.Id
            });

            return new RollbackResult
            {
                Success = success,
                StepResults = stepResults,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed with exception for plan {PlanId}", plan.Id);

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.RollbackCompleted,
                Message = $"Rollback failed with exception: {ex.Message}",
                PlanId = plan.Id
            });

            return new RollbackResult
            {
                Success = false,
                StepResults = stepResults,
                ErrorMessage = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Executes a single rollback step operation.
    /// </summary>
    private async Task<string> ExecuteRollbackStepAsync(
        RollbackStep rollbackStep,
        ExecutionPlan plan,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing rollback operation: {Operation}", rollbackStep.Operation);

        // Parse the rollback operation to determine the type
        // Rollback operations are typically SQL statements or commands that undo forward steps

        // Common rollback patterns:
        // - DROP INDEX <name>
        // - ANALYZE <table> (to update stats after rollback)
        // - VACUUM <table>
        // - ALTER TABLE <table> DROP COLUMN <column>
        // - DROP TABLE <table>
        // - Custom SQL statements

        var operation = rollbackStep.Operation.Trim().ToUpperInvariant();

        if (operation.StartsWith("DROP INDEX"))
        {
            // This is a rollback for CreateIndex
            return await ExecuteDropIndexRollbackAsync(rollbackStep, context, cancellationToken);
        }
        else if (operation.StartsWith("ANALYZE") || operation.StartsWith("VACUUM"))
        {
            // Stats/cleanup operation - these are typically safe and informational
            return await ExecuteAnalyzeRollbackAsync(rollbackStep, context, cancellationToken);
        }
        else if (operation.StartsWith("DROP TABLE"))
        {
            // Drop table rollback
            return await ExecuteDropTableRollbackAsync(rollbackStep, context, cancellationToken);
        }
        else if (operation.StartsWith("ALTER TABLE"))
        {
            // Alter table rollback
            return await ExecuteAlterTableRollbackAsync(rollbackStep, context, cancellationToken);
        }
        else if (operation.StartsWith("CREATE INDEX"))
        {
            // Recreate index that was dropped
            return await ExecuteCreateIndexRollbackAsync(rollbackStep, context, cancellationToken);
        }
        else
        {
            // Generic SQL rollback - execute as-is with appropriate credentials
            _logger.LogWarning("Executing generic SQL rollback operation: {Operation}",
                rollbackStep.Operation);

            return await ExecuteGenericSqlRollbackAsync(rollbackStep, context, cancellationToken);
        }
    }

    /// <summary>
    /// Executes a DROP INDEX rollback operation.
    /// </summary>
    private async Task<string> ExecuteDropIndexRollbackAsync(
        RollbackStep rollbackStep,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing DROP INDEX rollback: {Operation}", rollbackStep.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "DROP INDEX" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Rollback: Drop index - {rollbackStep.Description}",
                Operations = new List<string> { "DROP INDEX" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(rollbackStep.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("DROP INDEX rollback completed successfully: {Description}",
            rollbackStep.Description);

        return $"Rollback completed: {rollbackStep.Description}";
    }

    /// <summary>
    /// Executes an ANALYZE/VACUUM rollback operation.
    /// </summary>
    private async Task<string> ExecuteAnalyzeRollbackAsync(
        RollbackStep rollbackStep,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing ANALYZE/VACUUM rollback: {Operation}", rollbackStep.Operation);

        // ANALYZE and VACUUM operations during rollback update statistics
        // They're typically safe maintenance operations
        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "ANALYZE", "VACUUM" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Rollback: Update statistics - {rollbackStep.Description}",
                Operations = new List<string> { "ANALYZE", "VACUUM" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(rollbackStep.Operation, conn)
        {
            CommandTimeout = 1800 // 30 minutes for large tables
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("ANALYZE/VACUUM rollback completed successfully: {Description}",
            rollbackStep.Description);

        return $"Rollback completed: {rollbackStep.Description}";
    }

    /// <summary>
    /// Executes a DROP TABLE rollback operation.
    /// </summary>
    private async Task<string> ExecuteDropTableRollbackAsync(
        RollbackStep rollbackStep,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing DROP TABLE rollback: {Operation}", rollbackStep.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "DROP TABLE" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Rollback: Drop table - {rollbackStep.Description}",
                Operations = new List<string> { "DROP TABLE" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(rollbackStep.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("DROP TABLE rollback completed successfully: {Description}",
            rollbackStep.Description);

        return $"Rollback completed: {rollbackStep.Description}";
    }

    /// <summary>
    /// Executes an ALTER TABLE rollback operation.
    /// </summary>
    private async Task<string> ExecuteAlterTableRollbackAsync(
        RollbackStep rollbackStep,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing ALTER TABLE rollback: {Operation}", rollbackStep.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "ALTER TABLE" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Rollback: Alter table - {rollbackStep.Description}",
                Operations = new List<string> { "ALTER TABLE" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(rollbackStep.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("ALTER TABLE rollback completed successfully: {Description}",
            rollbackStep.Description);

        return $"Rollback completed: {rollbackStep.Description}";
    }

    /// <summary>
    /// Executes a CREATE INDEX rollback operation (recreates a dropped index).
    /// </summary>
    private async Task<string> ExecuteCreateIndexRollbackAsync(
        RollbackStep rollbackStep,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing CREATE INDEX rollback: {Operation}", rollbackStep.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "CREATE INDEX" }
                },
                Duration = TimeSpan.FromMinutes(30),
                Purpose = $"Rollback: Recreate index - {rollbackStep.Description}",
                Operations = new List<string> { "CREATE INDEX" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(rollbackStep.Operation, conn)
        {
            CommandTimeout = 1800 // 30 minutes for index creation
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("CREATE INDEX rollback completed successfully: {Description}",
            rollbackStep.Description);

        return $"Rollback completed: {rollbackStep.Description}";
    }

    /// <summary>
    /// Executes a generic SQL rollback operation.
    /// </summary>
    private async Task<string> ExecuteGenericSqlRollbackAsync(
        RollbackStep rollbackStep,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing generic SQL rollback: {Operation}", rollbackStep.Operation);

        var connectionString = await context.GetCredentialAsync(
            new CredentialRequirement
            {
                SecretRef = "database",
                Scope = new AccessScope
                {
                    Level = AccessLevel.DDL,
                    AllowedOperations = new List<string> { "GENERIC" }
                },
                Duration = TimeSpan.FromMinutes(10),
                Purpose = $"Rollback: Generic SQL operation - {rollbackStep.Description}",
                Operations = new List<string> { "GENERIC" }
            },
            cancellationToken);

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new Npgsql.NpgsqlCommand(rollbackStep.Operation, conn)
        {
            CommandTimeout = 600 // 10 minutes
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Generic SQL rollback completed successfully: {Description}",
            rollbackStep.Description);

        return $"Rollback completed: {rollbackStep.Description}";
    }

    public async Task<SimulationResult> SimulateAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating plan {PlanId}: {Title}", plan.Id, plan.Title);

        var stepSimulations = new List<StepSimulation>();
        var warnings = new List<string>();
        var errors = new List<string>();

        // Validate plan
        var validationResult = await _validator.ValidateAsync(plan, cancellationToken);
        if (!validationResult.IsValid)
        {
            errors.AddRange(validationResult.Errors);
        }
        if (validationResult.Warnings.Any())
        {
            warnings.AddRange(validationResult.Warnings);
        }

        // Simulate each step
        foreach (var step in plan.Steps.OrderBy(s => s.StepNumber))
        {
            var prerequisites = new List<string>();

            // Check dependencies
            if (step.DependsOn.Any())
            {
                var dependencySteps = plan.Steps
                    .Where(s => step.DependsOn.Contains(s.StepNumber))
                    .Select(s => $"Step {s.StepNumber}: {s.Description}");
                prerequisites.AddRange(dependencySteps);
            }

            // Check if step requires downtime
            if (step.RequiresDowntime)
            {
                warnings.Add($"Step {step.StepNumber} requires downtime");
            }

            // Check if step modifies data
            if (step.ModifiesData && !step.IsReversible)
            {
                warnings.Add($"Step {step.StepNumber} modifies data and is not reversible");
            }

            stepSimulations.Add(new StepSimulation
            {
                Step = step,
                CanExecute = true, // For now, assume all validated steps can execute
                Prerequisites = prerequisites
            });
        }

        var success = !errors.Any();

        _logger.LogInformation("Simulation completed: {Success}, {WarningCount} warnings, {ErrorCount} errors",
            success, warnings.Count, errors.Count);

        return new SimulationResult
        {
            Success = success,
            StepSimulations = stepSimulations,
            Warnings = warnings,
            Errors = errors
        };
    }
}
