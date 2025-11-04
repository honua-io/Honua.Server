// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Documentation;

namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for auto-generating documentation.
/// Provides AI with capabilities to create API docs, user guides, and deployment documentation.
/// Delegates to specialized documentation services.
/// </summary>
public sealed class DocumentationPlugin
{
    private readonly ApiDocumentationService _apiDocumentationService;
    private readonly UserGuideService _userGuideService;
    private readonly ExampleRequestService _exampleRequestService;
    private readonly DataModelDocumentationService _dataModelDocumentationService;
    private readonly DeploymentGuideService _deploymentGuideService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentationPlugin"/> class.
    /// </summary>
    public DocumentationPlugin()
    {
        _apiDocumentationService = new ApiDocumentationService();
        _userGuideService = new UserGuideService();
        _exampleRequestService = new ExampleRequestService();
        _dataModelDocumentationService = new DataModelDocumentationService();
        _deploymentGuideService = new DeploymentGuideService();
    }

    /// <summary>
    /// Generates OpenAPI/Swagger documentation from metadata configuration.
    /// </summary>
    /// <param name="metadataConfig">Metadata configuration as JSON</param>
    /// <returns>JSON containing OpenAPI specification and Swagger UI setup</returns>
    [KernelFunction, Description("Generates OpenAPI/Swagger documentation from metadata configuration")]
    public string GenerateApiDocs(
        [Description("Metadata configuration as JSON")] string metadataConfig)
    {
        return _apiDocumentationService.GenerateApiDocs(metadataConfig);
    }

    /// <summary>
    /// Creates user-facing guides and tutorials.
    /// </summary>
    /// <param name="deploymentInfo">Deployment information as JSON</param>
    /// <returns>JSON containing various user guide sections</returns>
    [KernelFunction, Description("Creates user-facing guides and tutorials")]
    public string CreateUserGuide(
        [Description("Deployment information as JSON")] string deploymentInfo = "{\"environment\":\"development\",\"url\":\"http://localhost:5000\"}")
    {
        return _userGuideService.CreateUserGuide(deploymentInfo);
    }

    /// <summary>
    /// Generates example API requests in multiple formats.
    /// </summary>
    /// <param name="endpoints">List of endpoints as JSON array</param>
    /// <returns>JSON containing examples in multiple formats</returns>
    [KernelFunction, Description("Generates example API requests in multiple formats")]
    public string GenerateExampleRequests(
        [Description("List of endpoints as JSON array")] string endpoints = "[\"/\",\"/conformance\",\"/collections\"]")
    {
        return _exampleRequestService.GenerateExampleRequests(endpoints);
    }

    /// <summary>
    /// Documents data model and schema.
    /// </summary>
    /// <param name="schemaInfo">Schema information as JSON</param>
    /// <returns>JSON containing markdown documentation and generation tools</returns>
    [KernelFunction, Description("Documents data model and schema")]
    public string DocumentDataModel(
        [Description("Schema information as JSON")] string schemaInfo)
    {
        return _dataModelDocumentationService.DocumentDataModel(schemaInfo);
    }

    /// <summary>
    /// Creates deployment guides for various platforms.
    /// </summary>
    /// <param name="infrastructure">Infrastructure details as JSON</param>
    /// <returns>JSON containing deployment guides for multiple platforms</returns>
    [KernelFunction, Description("Creates deployment guides for various platforms")]
    public string CreateDeploymentGuide(
        [Description("Infrastructure details as JSON")] string infrastructure = "{\"platform\":\"docker\",\"environment\":\"production\"}")
    {
        return _deploymentGuideService.CreateDeploymentGuide(infrastructure);
    }
}
