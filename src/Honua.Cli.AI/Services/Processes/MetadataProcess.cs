// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Processes.Steps.Metadata;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Process builder for metadata extraction and STAC publishing workflow.
/// Orchestrates 3 steps: extract metadata → generate STAC → publish.
/// </summary>
public static class MetadataProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("HonuaMetadata");

        // Add all 3 steps
        var extractStep = builder.AddStepFromType<ExtractMetadataStep>();
        var generateStacStep = builder.AddStepFromType<GenerateStacItemStep>();
        var publishStep = builder.AddStepFromType<PublishStacStep>();

        // Wire event routing

        // Start: external event → extract metadata
        builder
            .OnInputEvent("StartMetadataExtraction")
            .SendEventTo(new ProcessFunctionTargetBuilder(extractStep, "ExtractMetadata"));

        // Extract → Generate STAC
        extractStep
            .OnEvent("MetadataExtracted")
            .SendEventTo(new ProcessFunctionTargetBuilder(generateStacStep, "GenerateStacItem"));

        // Generate STAC → Publish
        generateStacStep
            .OnEvent("StacGenerated")
            .SendEventTo(new ProcessFunctionTargetBuilder(publishStep, "PublishStac"));

        // Error handling
        extractStep
            .OnEvent("ExtractionFailed")
            .StopProcess();

        generateStacStep
            .OnEvent("StacGenerationFailed")
            .StopProcess();

        publishStep
            .OnEvent("PublishFailed")
            .StopProcess();

        return builder;
    }
}
