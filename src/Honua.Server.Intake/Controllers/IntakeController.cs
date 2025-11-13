// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Intake.Controllers;

/// <summary>
/// REST API endpoints for the AI-powered intake system.
/// </summary>
[ApiController]
[Route("api/intake")]
[Produces("application/json")]
public sealed class IntakeController : ControllerBase
{
    private readonly IIntakeAgent intakeAgent;
    private readonly IManifestGenerator manifestGenerator;
    private readonly IRegistryProvisioner registryProvisioner;
    private readonly IBuildDeliveryService buildDeliveryService;
    private readonly ILogger<IntakeController> logger;

    public IntakeController(
        IIntakeAgent intakeAgent,
        IManifestGenerator manifestGenerator,
        IRegistryProvisioner registryProvisioner,
        IBuildDeliveryService buildDeliveryService,
        ILogger<IntakeController> logger)
    {
        this.intakeAgent = intakeAgent ?? throw new ArgumentNullException(nameof(intakeAgent));
        this.manifestGenerator = manifestGenerator ?? throw new ArgumentNullException(nameof(manifestGenerator));
        this.registryProvisioner = registryProvisioner ?? throw new ArgumentNullException(nameof(registryProvisioner));
        this.buildDeliveryService = buildDeliveryService ?? throw new ArgumentNullException(nameof(buildDeliveryService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts a new intake conversation with the AI agent.
    /// </summary>
    /// <param name="request">Optional request with customer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conversation response with initial greeting.</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConversationResponse>> StartConversation(
        [FromBody] StartConversationRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogInformation("Starting new intake conversation");

            var response = await this.intakeAgent.StartConversationAsync(request?.CustomerId, cancellationToken);

            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to start conversation");
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to start conversation" });
        }
    }

    /// <summary>
    /// Sends a message to the AI agent in an ongoing conversation.
    /// </summary>
    /// <param name="request">Message request containing conversation ID and user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AI response and extracted requirements if intake is complete.</returns>
    [HttpPost("message")]
    [ProducesResponseType(typeof(IntakeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IntakeResponse>> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return this.BadRequest(new { error = "ConversationId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return this.BadRequest(new { error = "Message is required" });
        }

        try
        {
            this.logger.LogInformation("Processing message for conversation {ConversationId}", request.ConversationId);

            var response = await this.intakeAgent.ProcessMessageAsync(
                request.ConversationId,
                request.Message,
                cancellationToken);

            return this.Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            this.logger.LogWarning(ex, "Conversation {ConversationId} not found", request.ConversationId);
            return this.NotFound(new { error = $"Conversation {request.ConversationId} not found" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to process message for conversation {ConversationId}", request.ConversationId);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to process message" });
        }
    }

    /// <summary>
    /// Retrieves conversation history.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conversation record with message history.</returns>
    [HttpGet("conversations/{conversationId}")]
    [ProducesResponseType(typeof(ConversationRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConversationRecord>> GetConversation(
        [FromRoute] string conversationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await this.intakeAgent.GetConversationAsync(conversationId, cancellationToken);

            if (conversation == null)
            {
                return this.NotFound(new { error = $"Conversation {conversationId} not found" });
            }

            return this.Ok(conversation);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to retrieve conversation {ConversationId}", conversationId);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve conversation" });
        }
    }

    /// <summary>
    /// Triggers a build from a completed intake conversation.
    /// </summary>
    /// <param name="request">Build trigger request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Build trigger response with build ID and status.</returns>
    [HttpPost("build")]
    [ProducesResponseType(typeof(TriggerBuildResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TriggerBuildResponse>> TriggerBuild(
        [FromBody] TriggerBuildRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return this.BadRequest(new { error = "ConversationId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return this.BadRequest(new { error = "CustomerId is required" });
        }

        try
        {
            this.logger.LogInformation("Triggering build for conversation {ConversationId}, customer {CustomerId}",
                request.ConversationId, request.CustomerId);

            // Get conversation and requirements
            var conversation = await this.intakeAgent.GetConversationAsync(request.ConversationId, cancellationToken);
            if (conversation == null)
            {
                return this.NotFound(new { error = $"Conversation {request.ConversationId} not found" });
            }

            // Use override requirements if provided, otherwise extract from conversation
            BuildRequirements? requirements = request.RequirementsOverride;
            if (requirements == null && !string.IsNullOrWhiteSpace(conversation.RequirementsJson))
            {
                requirements = System.Text.Json.JsonSerializer.Deserialize<BuildRequirements>(conversation.RequirementsJson);
            }

            if (requirements == null)
            {
                return this.BadRequest(new { error = "No requirements available. Complete the intake conversation first." });
            }

            // Generate manifest
            var manifest = await this.manifestGenerator.GenerateAsync(requirements, request.BuildName, cancellationToken);

            // Provision registry access
            var registryType = DetermineRegistryType(requirements.CloudProvider);
            var registryResult = await this.registryProvisioner.ProvisionAsync(
                request.CustomerId,
                registryType,
                cancellationToken);

            if (!registryResult.Success)
            {
                this.logger.LogError("Failed to provision registry for customer {CustomerId}: {Error}",
                    request.CustomerId, registryResult.ErrorMessage);
                return this.StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Failed to provision container registry",
                    details = registryResult.ErrorMessage,
                });
            }

            // Trigger build delivery
            var buildId = Guid.NewGuid().ToString();
            var cacheKey = new BuildCacheKey
            {
                CustomerId = request.CustomerId,
                BuildName = manifest.Name,
                Version = "latest",
                Architecture = manifest.Architecture,
            };

            this.logger.LogInformation("Triggering build delivery for {BuildId}", buildId);

            // Note: In a real implementation, this would trigger an async build process
            // For now, we just return success with the manifest
            var response = new TriggerBuildResponse
            {
                Success = true,
                BuildId = buildId,
                Manifest = manifest,
                RegistryResult = registryResult,
                TriggeredAt = DateTimeOffset.UtcNow,
            };

            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to trigger build for conversation {ConversationId}", request.ConversationId);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to trigger build" });
        }
    }

    /// <summary>
    /// Gets the status of a build.
    /// </summary>
    /// <param name="buildId">The build identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Build status information.</returns>
    [HttpGet("builds/{buildId}/status")]
    [ProducesResponseType(typeof(BuildStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BuildStatusResponse>> GetBuildStatus(
        [FromRoute] string buildId,
        CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogInformation("Retrieving status for build {BuildId}", buildId);

            // Note: In a real implementation, this would query a build tracking service
            // For now, return a placeholder response
            var response = new BuildStatusResponse
            {
                BuildId = buildId,
                Status = "pending",
                Progress = 0,
                CurrentStage = "Queued for building",
                StartedAt = DateTimeOffset.UtcNow,
            };

            await Task.CompletedTask;
            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to retrieve build status for {BuildId}", buildId);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve build status" });
        }
    }

    private static RegistryType DetermineRegistryType(string cloudProvider)
    {
        return cloudProvider.ToLowerInvariant() switch
        {
            "aws" => RegistryType.AwsEcr,
            "azure" => RegistryType.AzureAcr,
            "gcp" => RegistryType.GcpArtifactRegistry,
            _ => RegistryType.GitHubContainerRegistry,  // Default fallback
        };
    }
}

/// <summary>
/// Request to start a new conversation.
/// </summary>
public sealed class StartConversationRequest
{
    /// <summary>
    /// Optional customer identifier (if authenticated).
    /// </summary>
    public string? CustomerId { get; init; }
}

/// <summary>
/// Request to send a message in a conversation.
/// </summary>
public sealed class SendMessageRequest
{
    /// <summary>
    /// The conversation identifier.
    /// </summary>
    [Required]
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>
    /// The user's message.
    /// </summary>
    [Required]
    public string Message { get; init; } = string.Empty;
}
