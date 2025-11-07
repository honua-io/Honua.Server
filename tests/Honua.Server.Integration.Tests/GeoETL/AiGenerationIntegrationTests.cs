// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.ETL.AI;
using Honua.Server.Integration.Tests.GeoETL.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoETL;

/// <summary>
/// Integration tests for AI-powered workflow generation (with mocked OpenAI)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "GeoETL")]
[Trait("Category", "AI")]
public class AiGenerationIntegrationTests : GeoEtlIntegrationTestBase
{
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Register mock AI service
        services.AddSingleton<IGeoEtlAiService, MockOpenAiService>();
    }

    [Fact]
    public async Task GenerateWorkflow_WithBufferPrompt_ShouldCreateValidWorkflow()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var prompt = "Buffer buildings by 50 meters and export to geopackage";

        // Act
        var result = await aiService.GenerateWorkflowAsync(prompt, TestTenantId, TestUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Workflow);
        Assert.NotNull(result.Explanation);
        Assert.True(result.Confidence > 0);

        // Verify workflow structure
        Assert.NotEmpty(result.Workflow.Nodes);
        Assert.Contains(result.Workflow.Nodes, n => n.Type.Contains("buffer"));
        Assert.Contains(result.Workflow.Nodes, n => n.Type.Contains("geopackage"));
    }

    [Fact]
    public async Task GenerateWorkflow_WithIntersectionPrompt_ShouldCreateMultiSourceWorkflow()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var prompt = "Intersect parcels with flood zones";

        // Act
        var result = await aiService.GenerateWorkflowAsync(prompt, TestTenantId, TestUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Workflow);

        // Should have multiple source nodes for intersection
        var sourceNodes = result.Workflow.Nodes.Where(n => n.Type.StartsWith("data_source")).ToList();
        Assert.True(sourceNodes.Count >= 2, "Intersection workflow should have at least 2 sources");

        var intersectionNode = result.Workflow.Nodes.FirstOrDefault(n => n.Type.Contains("intersection"));
        Assert.NotNull(intersectionNode);
    }

    [Fact]
    public async Task GenerateWorkflow_WithUnionPrompt_ShouldCreateUnionWorkflow()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var prompt = "Union all features and export to geojson";

        // Act
        var result = await aiService.GenerateWorkflowAsync(prompt, TestTenantId, TestUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Workflow);
        Assert.Contains(result.Workflow.Nodes, n => n.Type.Contains("union"));
    }

    [Fact]
    public async Task GenerateWorkflow_WithValidation_ShouldValidateWorkflow()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var prompt = "Buffer features by 100 meters";

        // Act
        var result = await aiService.GenerateWorkflowAsync(
            prompt,
            TestTenantId,
            TestUserId,
            validateWorkflow: true
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Workflow);

        // Validate the generated workflow
        var validation = await WorkflowEngine.ValidateAsync(result.Workflow);
        WorkflowAssertions.AssertWorkflowValidationSuccess(validation);
    }

    [Fact]
    public async Task GenerateWorkflow_ThenExecute_ShouldSucceed()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var prompt = "Load features and create 50 meter buffer";

        // Act - Generate
        var result = await aiService.GenerateWorkflowAsync(prompt, TestTenantId, TestUserId);

        Assert.True(result.Success);
        Assert.NotNull(result.Workflow);

        // Modify workflow to use actual data source
        var sourceNode = result.Workflow.Nodes.FirstOrDefault(n => n.Type.StartsWith("data_source"));
        if (sourceNode != null && sourceNode.Type == "data_source.postgis")
        {
            // Replace with file source for testing
            sourceNode.Type = "data_source.file";
            sourceNode.Parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                ["geojson"] = FeatureGenerator.CreateGeoJsonFromPoints(10)
            };
        }

        // Act - Execute
        var run = await ExecuteWorkflowAsync(result.Workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
    }

    [Fact]
    public async Task ExplainWorkflow_WithValidWorkflow_ShouldProvideExplanation()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var workflow = WorkflowBuilder.CreateBufferWorkflow(TestTenantId, TestUserId);

        // Act
        var result = await aiService.ExplainWorkflowAsync(workflow);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Explanation);
        Assert.NotEmpty(result.Explanation);
        Assert.True(result.Confidence > 0);
    }

    [Fact]
    public async Task SuggestImprovements_WithValidWorkflow_ShouldProvidesuggestions()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);

        // Act
        var result = await aiService.SuggestImprovementsAsync(workflow);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Explanation);
        // Mock service provides warnings as suggestions
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task AiService_IsAvailable_ShouldReturnTrue()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();

        // Act & Assert
        Assert.True(aiService.IsAvailable);
    }

    [Fact]
    public async Task GeneratedWorkflow_WithComplexPrompt_ShouldHaveMetadata()
    {
        // Arrange
        var aiService = ServiceProvider.GetRequiredService<IGeoEtlAiService>();
        var prompt = "Create a comprehensive buffer and intersection analysis workflow";

        // Act
        var result = await aiService.GenerateWorkflowAsync(prompt, TestTenantId, TestUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Workflow);

        // Check metadata
        Assert.NotNull(result.Workflow.Metadata);
        Assert.NotEmpty(result.Workflow.Metadata.Name);
        Assert.NotEmpty(result.Workflow.Metadata.Description);
        Assert.Contains(result.Workflow.Metadata.Tags, t => t == "AI-generated");
    }
}
