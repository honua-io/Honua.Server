// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Planning;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Validation;

/// <summary>
/// Validates execution plans for safety, correctness, and security.
/// </summary>
public sealed class PlanValidator : IPlanValidator
{
    private readonly ILogger<PlanValidator> _logger;
    private static readonly HashSet<string> DangerousOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP TABLE",
        "DROP DATABASE",
        "DROP INDEX",
        "TRUNCATE",
        "DELETE FROM",
        "ALTER TABLE DROP",
        "DROP SCHEMA"
    };

    public PlanValidator(ILogger<PlanValidator> logger)
    {
        _logger = logger;
    }

    public Task<ValidationResult> ValidateAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Validating plan {PlanId}: {Title}", plan.Id, plan.Title);

        var errors = new List<string>();
        var warnings = new List<string>();
        var info = new List<string>();
        var checks = new List<ValidationCheck>();

        // Check 1: Plan must be approved
        var approvalCheck = ValidateApproval(plan);
        checks.Add(approvalCheck);
        if (approvalCheck.Result == ValidationCheckResult.Failed)
        {
            errors.Add(approvalCheck.Message ?? "Plan not approved");
        }

        // Check 2: Plan must have steps
        var stepsCheck = ValidateSteps(plan);
        checks.Add(stepsCheck);
        if (stepsCheck.Result == ValidationCheckResult.Failed)
        {
            errors.Add(stepsCheck.Message ?? "No steps defined");
        }

        // Check 3: Step dependencies must be valid
        var dependenciesCheck = ValidateDependencies(plan);
        checks.Add(dependenciesCheck);
        if (dependenciesCheck.Result == ValidationCheckResult.Failed)
        {
            errors.Add(dependenciesCheck.Message ?? "Invalid step dependencies");
        }
        else if (dependenciesCheck.Result == ValidationCheckResult.Warning)
        {
            warnings.Add(dependenciesCheck.Message ?? "Dependency warning");
        }

        // Check 4: Credentials must be specified for steps that need them
        var credentialsCheck = ValidateCredentials(plan);
        checks.Add(credentialsCheck);
        if (credentialsCheck.Result == ValidationCheckResult.Failed)
        {
            errors.Add(credentialsCheck.Message ?? "Missing credentials");
        }
        else if (credentialsCheck.Result == ValidationCheckResult.Warning)
        {
            warnings.Add(credentialsCheck.Message ?? "Credential warning");
        }

        // Check 5: Risk assessment must match actual operations
        var riskCheck = ValidateRiskAssessment(plan);
        checks.Add(riskCheck);
        if (riskCheck.Result == ValidationCheckResult.Warning)
        {
            warnings.Add(riskCheck.Message ?? "Risk assessment warning");
        }

        // Check 6: Dangerous operations must be flagged
        var dangerousOpsCheck = ValidateDangerousOperations(plan);
        checks.Add(dangerousOpsCheck);
        if (dangerousOpsCheck.Result == ValidationCheckResult.Failed)
        {
            errors.Add(dangerousOpsCheck.Message ?? "Dangerous operations not allowed");
        }
        else if (dangerousOpsCheck.Result == ValidationCheckResult.Warning)
        {
            warnings.Add(dangerousOpsCheck.Message ?? "Dangerous operations present");
        }

        // Check 7: Rollback plan should exist for risky changes
        var rollbackCheck = ValidateRollbackPlan(plan);
        checks.Add(rollbackCheck);
        if (rollbackCheck.Result == ValidationCheckResult.Warning)
        {
            warnings.Add(rollbackCheck.Message ?? "No rollback plan");
        }

        // Check 8: Environment must be specified
        var envCheck = ValidateEnvironment(plan);
        checks.Add(envCheck);
        if (envCheck.Result == ValidationCheckResult.Warning)
        {
            warnings.Add(envCheck.Message ?? "Environment not specified");
        }

        var isValid = errors.Count == 0;

        _logger.LogDebug("Validation completed: {IsValid}, {ErrorCount} errors, {WarningCount} warnings",
            isValid, errors.Count, warnings.Count);

        return Task.FromResult(new ValidationResult
        {
            IsValid = isValid,
            Errors = errors,
            Warnings = warnings,
            Info = info,
            Checks = checks
        });
    }

    private ValidationCheck ValidateApproval(ExecutionPlan plan)
    {
        if (plan.Status == PlanStatus.Pending)
        {
            return new ValidationCheck
            {
                Name = "Approval",
                Result = ValidationCheckResult.Failed,
                Message = "Plan must be approved before execution"
            };
        }

        if (plan.Status == PlanStatus.Rejected)
        {
            return new ValidationCheck
            {
                Name = "Approval",
                Result = ValidationCheckResult.Failed,
                Message = "Plan has been rejected"
            };
        }

        return new ValidationCheck
        {
            Name = "Approval",
            Result = ValidationCheckResult.Passed,
            Message = $"Plan approved at {plan.ApprovedAt:yyyy-MM-dd HH:mm:ss}"
        };
    }

    private ValidationCheck ValidateSteps(ExecutionPlan plan)
    {
        if (plan.Steps == null || plan.Steps.Count == 0)
        {
            return new ValidationCheck
            {
                Name = "Steps",
                Result = ValidationCheckResult.Failed,
                Message = "Plan has no steps defined"
            };
        }

        // Check for duplicate step numbers
        var duplicateSteps = plan.Steps
            .GroupBy(s => s.StepNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateSteps.Any())
        {
            return new ValidationCheck
            {
                Name = "Steps",
                Result = ValidationCheckResult.Failed,
                Message = $"Duplicate step numbers: {string.Join(", ", duplicateSteps)}"
            };
        }

        return new ValidationCheck
        {
            Name = "Steps",
            Result = ValidationCheckResult.Passed,
            Message = $"{plan.Steps.Count} steps defined"
        };
    }

    private ValidationCheck ValidateDependencies(ExecutionPlan plan)
    {
        var stepNumbers = new HashSet<int>(plan.Steps.Select(s => s.StepNumber));

        foreach (var step in plan.Steps)
        {
            // Check if all dependencies exist
            var invalidDeps = step.DependsOn.Where(d => !stepNumbers.Contains(d)).ToList();
            if (invalidDeps.Any())
            {
                return new ValidationCheck
                {
                    Name = "Dependencies",
                    Result = ValidationCheckResult.Failed,
                    Message = $"Step {step.StepNumber} has invalid dependencies: {string.Join(", ", invalidDeps)}"
                };
            }

            // Check for circular dependencies (basic check)
            if (step.DependsOn.Contains(step.StepNumber))
            {
                return new ValidationCheck
                {
                    Name = "Dependencies",
                    Result = ValidationCheckResult.Failed,
                    Message = $"Step {step.StepNumber} has circular dependency on itself"
                };
            }

            // Warn if dependency is on a later step
            var forwardDeps = step.DependsOn.Where(d => d > step.StepNumber).ToList();
            if (forwardDeps.Any())
            {
                return new ValidationCheck
                {
                    Name = "Dependencies",
                    Result = ValidationCheckResult.Warning,
                    Message = $"Step {step.StepNumber} depends on later steps: {string.Join(", ", forwardDeps)}"
                };
            }
        }

        return new ValidationCheck
        {
            Name = "Dependencies",
            Result = ValidationCheckResult.Passed,
            Message = "All dependencies are valid"
        };
    }

    private ValidationCheck ValidateCredentials(ExecutionPlan plan)
    {
        // Check if any steps require database access but no credentials are specified
        var requiresDbSteps = plan.Steps.Where(s =>
            s.Type == StepType.CreateIndex ||
            s.Type == StepType.CreateStatistics ||
            s.Type == StepType.AlterTable ||
            s.Type == StepType.VacuumAnalyze).ToList();

        if (requiresDbSteps.Any() && (plan.CredentialsRequired == null || plan.CredentialsRequired.Count == 0))
        {
            return new ValidationCheck
            {
                Name = "Credentials",
                Result = ValidationCheckResult.Warning,
                Message = $"{requiresDbSteps.Count} steps require database access but no credentials specified"
            };
        }

        // Check credential scopes match operations
        foreach (var cred in plan.CredentialsRequired ?? Enumerable.Empty<CredentialRequirement>())
        {
            if (cred.Duration > TimeSpan.FromHours(1))
            {
                return new ValidationCheck
                {
                    Name = "Credentials",
                    Result = ValidationCheckResult.Warning,
                    Message = $"Credential '{cred.SecretRef}' has long duration: {cred.Duration.TotalMinutes:F0} minutes"
                };
            }
        }

        return new ValidationCheck
        {
            Name = "Credentials",
            Result = ValidationCheckResult.Passed,
            Message = $"{plan.CredentialsRequired?.Count ?? 0} credentials specified"
        };
    }

    private ValidationCheck ValidateRiskAssessment(ExecutionPlan plan)
    {
        // Check if risk level matches operations
        var hasDangerousOps = plan.Steps.Any(s =>
            !s.IsReversible ||
            s.ModifiesData ||
            s.RequiresDowntime);

        if (hasDangerousOps && plan.Risk.Level == RiskLevel.Low)
        {
            return new ValidationCheck
            {
                Name = "Risk Assessment",
                Result = ValidationCheckResult.Warning,
                Message = "Plan has dangerous operations but risk is marked as Low"
            };
        }

        var hasIrreversibleOps = plan.Steps.Any(s => !s.IsReversible);
        if (hasIrreversibleOps && plan.Risk.AllChangesReversible)
        {
            return new ValidationCheck
            {
                Name = "Risk Assessment",
                Result = ValidationCheckResult.Warning,
                Message = "Plan marked as fully reversible but contains irreversible steps"
            };
        }

        return new ValidationCheck
        {
            Name = "Risk Assessment",
            Result = ValidationCheckResult.Passed,
            Message = $"Risk level: {plan.Risk.Level}"
        };
    }

    private ValidationCheck ValidateDangerousOperations(ExecutionPlan plan)
    {
        var dangerousSteps = plan.Steps
            .Where(s => DangerousOperations.Any(op =>
                s.Operation.Contains(op, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (dangerousSteps.Any())
        {
            // For now, just warn about dangerous operations
            return new ValidationCheck
            {
                Name = "Dangerous Operations",
                Result = ValidationCheckResult.Warning,
                Message = $"Plan contains {dangerousSteps.Count} dangerous operation(s): " +
                         $"{string.Join(", ", dangerousSteps.Select(s => $"Step {s.StepNumber}"))}"
            };
        }

        return new ValidationCheck
        {
            Name = "Dangerous Operations",
            Result = ValidationCheckResult.Passed,
            Message = "No dangerous operations detected"
        };
    }

    private ValidationCheck ValidateRollbackPlan(ExecutionPlan plan)
    {
        if (plan.Risk.Level >= RiskLevel.Medium && plan.RollbackPlan == null)
        {
            return new ValidationCheck
            {
                Name = "Rollback Plan",
                Result = ValidationCheckResult.Warning,
                Message = $"Plan has {plan.Risk.Level} risk but no rollback plan defined"
            };
        }

        if (plan.RollbackPlan != null && (plan.RollbackPlan.Steps == null || plan.RollbackPlan.Steps.Count == 0))
        {
            return new ValidationCheck
            {
                Name = "Rollback Plan",
                Result = ValidationCheckResult.Warning,
                Message = "Rollback plan exists but has no steps"
            };
        }

        return new ValidationCheck
        {
            Name = "Rollback Plan",
            Result = ValidationCheckResult.Passed,
            Message = plan.RollbackPlan != null
                ? $"Rollback plan with {plan.RollbackPlan.Steps?.Count ?? 0} steps"
                : "No rollback plan (not required for low-risk changes)"
        };
    }

    private ValidationCheck ValidateEnvironment(ExecutionPlan plan)
    {
        if (plan.Environment.IsNullOrEmpty())
        {
            return new ValidationCheck
            {
                Name = "Environment",
                Result = ValidationCheckResult.Warning,
                Message = "Environment not specified"
            };
        }

        if (plan.Environment.Equals("production", StringComparison.OrdinalIgnoreCase) &&
            plan.Risk.Level < RiskLevel.Medium)
        {
            return new ValidationCheck
            {
                Name = "Environment",
                Result = ValidationCheckResult.Warning,
                Message = "Production environment but risk marked as Low - consider reviewing"
            };
        }

        return new ValidationCheck
        {
            Name = "Environment",
            Result = ValidationCheckResult.Passed,
            Message = $"Target environment: {plan.Environment}"
        };
    }
}
