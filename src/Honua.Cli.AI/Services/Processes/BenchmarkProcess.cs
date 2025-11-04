// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Processes.Steps.Benchmark;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Process builder for performance benchmarking workflow.
/// Orchestrates 4 steps: setup → run → analyze → report.
/// </summary>
public static class BenchmarkProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("HonuaBenchmark");

        // Add all 4 steps
        var setupStep = builder.AddStepFromType<SetupBenchmarkStep>();
        var runStep = builder.AddStepFromType<RunBenchmarkStep>();
        var analyzeStep = builder.AddStepFromType<AnalyzeResultsStep>();
        var reportStep = builder.AddStepFromType<GenerateReportStep>();

        // Wire event routing

        // Start: external event → setup
        builder
            .OnInputEvent("StartBenchmark")
            .SendEventTo(new ProcessFunctionTargetBuilder(setupStep, "SetupBenchmark"));

        // Setup → Run
        setupStep
            .OnEvent("SetupCompleted")
            .SendEventTo(new ProcessFunctionTargetBuilder(runStep, "RunBenchmark"));

        // Run → Analyze
        runStep
            .OnEvent("BenchmarkCompleted")
            .SendEventTo(new ProcessFunctionTargetBuilder(analyzeStep, "AnalyzeResults"));

        // Analyze → Report
        analyzeStep
            .OnEvent("AnalysisCompleted")
            .SendEventTo(new ProcessFunctionTargetBuilder(reportStep, "GenerateReport"));

        // Error handling
        setupStep
            .OnEvent("SetupFailed")
            .StopProcess();

        runStep
            .OnEvent("BenchmarkFailed")
            .StopProcess();

        analyzeStep
            .OnEvent("AnalysisFailed")
            .StopProcess();

        reportStep
            .OnEvent("ReportFailed")
            .StopProcess();

        return builder;
    }
}
