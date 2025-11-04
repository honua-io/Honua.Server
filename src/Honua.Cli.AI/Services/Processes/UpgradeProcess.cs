// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Processes.Steps.Upgrade;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Process builder for Honua upgrade workflow using blue-green deployment.
/// Orchestrates 4 steps: detect version → backup DB → create blue → switch traffic.
/// </summary>
public static class UpgradeProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("HonuaUpgrade");

        // Add all 4 steps
        var detectStep = builder.AddStepFromType<DetectCurrentVersionStep>();
        var backupStep = builder.AddStepFromType<BackupDatabaseStep>();
        var createBlueStep = builder.AddStepFromType<CreateBlueEnvironmentStep>();
        var switchTrafficStep = builder.AddStepFromType<SwitchTrafficStep>();

        // Wire event routing

        // Start: external event → detect version
        builder
            .OnInputEvent("StartUpgrade")
            .SendEventTo(new ProcessFunctionTargetBuilder(detectStep, "DetectVersion"));

        // Detect → Backup
        detectStep
            .OnEvent("VersionDetected")
            .SendEventTo(new ProcessFunctionTargetBuilder(backupStep, "BackupDatabase"));

        // Backup → Create Blue
        backupStep
            .OnEvent("BackupCompleted")
            .SendEventTo(new ProcessFunctionTargetBuilder(createBlueStep, "CreateBlueEnvironment"));

        // Create Blue → Switch Traffic
        createBlueStep
            .OnEvent("BlueEnvironmentReady")
            .SendEventTo(new ProcessFunctionTargetBuilder(switchTrafficStep, "SwitchTraffic"));

        // Error handling: failures stop the process
        detectStep
            .OnEvent("DetectionFailed")
            .StopProcess();

        backupStep
            .OnEvent("BackupFailed")
            .StopProcess();

        createBlueStep
            .OnEvent("BlueEnvironmentFailed")
            .StopProcess(); // Could trigger rollback here

        switchTrafficStep
            .OnEvent("TrafficSwitchFailed")
            .StopProcess(); // Rollback: switch back to green

        return builder;
    }
}
