// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.AI;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL.AI;

/// <summary>
/// Tests for GeoETL prompt template generation
/// </summary>
public class GeoEtlPromptTemplatesTests
{
    [Fact]
    public void GetSystemPrompt_ShouldReturnNonEmptyPrompt()
    {
        // Act
        var prompt = GeoEtlPromptTemplates.GetSystemPrompt();

        // Assert
        Assert.NotNull(prompt);
        Assert.NotEmpty(prompt);
        Assert.Contains("GeoETL", prompt);
        Assert.Contains("Available Node Types", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ShouldIncludeAllNodeCategories()
    {
        // Act
        var prompt = GeoEtlPromptTemplates.GetSystemPrompt();

        // Assert - should mention all node categories
        Assert.Contains("Data Sources", prompt);
        Assert.Contains("Geoprocessing Operations", prompt);
        Assert.Contains("Data Sinks", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ShouldIncludeKeyNodeTypes()
    {
        // Act
        var prompt = GeoEtlPromptTemplates.GetSystemPrompt();

        // Assert - should include key node types
        Assert.Contains("data_source.postgis", prompt);
        Assert.Contains("data_source.geopackage", prompt);
        Assert.Contains("geoprocessing.buffer", prompt);
        Assert.Contains("geoprocessing.intersection", prompt);
        Assert.Contains("data_sink.geopackage", prompt);
        Assert.Contains("data_sink.geojson", prompt);
    }

    [Fact]
    public void GetSystemPrompt_ShouldIncludeResponseFormat()
    {
        // Act
        var prompt = GeoEtlPromptTemplates.GetSystemPrompt();

        // Assert
        Assert.Contains("Response Format", prompt);
        Assert.Contains("metadata", prompt);
        Assert.Contains("nodes", prompt);
        Assert.Contains("edges", prompt);
    }

    [Fact]
    public void GetFewShotExamples_ShouldReturnMultipleExamples()
    {
        // Act
        var examples = GeoEtlPromptTemplates.GetFewShotExamples();

        // Assert
        Assert.NotNull(examples);
        Assert.NotEmpty(examples);
        Assert.Contains("Example 1", examples);
        Assert.Contains("Example 2", examples);
        Assert.Contains("Example 3", examples);
    }

    [Fact]
    public void GetFewShotExamples_ShouldIncludeBufferExample()
    {
        // Act
        var examples = GeoEtlPromptTemplates.GetFewShotExamples();

        // Assert
        Assert.Contains("Buffer buildings", examples);
        Assert.Contains("geoprocessing.buffer", examples);
    }

    [Fact]
    public void GetFewShotExamples_ShouldIncludeIntersectionExample()
    {
        // Act
        var examples = GeoEtlPromptTemplates.GetFewShotExamples();

        // Assert
        Assert.Contains("parcels", examples);
        Assert.Contains("flood", examples);
        Assert.Contains("geoprocessing.intersection", examples);
    }

    [Fact]
    public void FormatUserPrompt_ShouldWrapUserRequest()
    {
        // Arrange
        var userRequest = "Buffer buildings by 50 meters";

        // Act
        var prompt = GeoEtlPromptTemplates.FormatUserPrompt(userRequest);

        // Assert
        Assert.NotNull(prompt);
        Assert.Contains(userRequest, prompt);
        Assert.Contains("Generate a GeoETL workflow", prompt);
        Assert.Contains("Return ONLY valid JSON", prompt);
    }

    [Theory]
    [InlineData("Buffer buildings by 50m")]
    [InlineData("Intersect parcels with flood zones")]
    [InlineData("Create 100m buffer around roads")]
    public void FormatUserPrompt_ShouldHandleVariousRequests(string request)
    {
        // Act
        var prompt = GeoEtlPromptTemplates.FormatUserPrompt(request);

        // Assert
        Assert.NotNull(prompt);
        Assert.Contains(request, prompt);
    }
}
