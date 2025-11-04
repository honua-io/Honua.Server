// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Processes.Steps.GitOps;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Process builder for GitOps configuration workflow.
/// Orchestrates 3 steps: validate config → sync → monitor drift.
/// </summary>
public static class GitOpsProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("HonuaGitOps");

        // Add all 3 steps
        var validateStep = builder.AddStepFromType<ValidateGitConfigStep>();
        var syncStep = builder.AddStepFromType<SyncConfigStep>();
        var monitorStep = builder.AddStepFromType<MonitorDriftStep>();

        // Wire event routing

        // Start: external event → validate config
        builder
            .OnInputEvent("StartGitOps")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateStep, "ValidateConfig"));

        // Validate → Sync
        validateStep
            .OnEvent("ConfigValid")
            .SendEventTo(new ProcessFunctionTargetBuilder(syncStep, "SyncConfig"));

        // Sync → Monitor Drift
        syncStep
            .OnEvent("ConfigSynced")
            .SendEventTo(new ProcessFunctionTargetBuilder(monitorStep, "MonitorDrift"));

        // Error handling
        validateStep
            .OnEvent("ValidationFailed")
            .StopProcess();

        syncStep
            .OnEvent("SyncFailed")
            .StopProcess();

        monitorStep
            .OnEvent("MonitoringFailed")
            .StopProcess();

        return builder;
    }
}
