// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Intake.Services;

/// <summary>
/// Options for configuring the AI intake agent.
/// </summary>
public sealed class IntakeAgentOptions
{
    /// <summary>
    /// AI provider to use (openai or anthropic).
    /// </summary>
    public string Provider { get; init; } = "openai";

    /// <summary>
    /// OpenAI API key.
    /// </summary>
    public string? OpenAIApiKey { get; init; }

    /// <summary>
    /// OpenAI model to use (default: gpt-4-turbo-preview).
    /// </summary>
    public string OpenAIModel { get; init; } = "gpt-4-turbo-preview";

    /// <summary>
    /// Anthropic API key.
    /// </summary>
    public string? AnthropicApiKey { get; init; }

    /// <summary>
    /// Anthropic model to use (default: claude-3-opus-20240229).
    /// </summary>
    public string AnthropicModel { get; init; } = "claude-3-opus-20240229";

    /// <summary>
    /// Maximum tokens for AI response.
    /// </summary>
    public int MaxTokens { get; init; } = 2000;

    /// <summary>
    /// Temperature for AI responses (0.0 - 1.0).
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// PostgreSQL connection string for storing conversations.
    /// </summary>
    public string? ConnectionString { get; init; }
}

/// <summary>
/// Interface for the AI intake agent.
/// </summary>
public interface IIntakeAgent
{
    /// <summary>
    /// Starts a new conversation with the AI intake agent.
    /// </summary>
    /// <param name="customerId">Optional customer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conversation response with initial greeting.</returns>
    Task<ConversationResponse> StartConversationAsync(string? customerId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a user message in an ongoing conversation.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AI response and extracted requirements if intake is complete.</returns>
    Task<IntakeResponse> ProcessMessageAsync(string conversationId, string userMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves conversation history.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conversation record with message history.</returns>
    Task<ConversationRecord?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// AI-powered intake agent that guides customers through configuring their Honua server build.
/// </summary>
public sealed class IntakeAgent : IIntakeAgent
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConversationStore conversationStore;
    private readonly ILogger<IntakeAgent> logger;
    private readonly IntakeAgentOptions options;

    public IntakeAgent(
        IHttpClientFactory httpClientFactory,
        IConversationStore conversationStore,
        IOptions<IntakeAgentOptions> options,
        ILogger<IntakeAgent> logger)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task<ConversationResponse> StartConversationAsync(string? customerId = null, CancellationToken cancellationToken = default)
    {
        var conversationId = Guid.NewGuid().ToString();
        var startedAt = DateTimeOffset.UtcNow;

        this.logger.LogInformation("Starting new conversation {ConversationId} for customer {CustomerId}", conversationId, customerId ?? "anonymous");

        // Initialize conversation with system prompt
        var messages = new List<ConversationMessage>
        {
            new()
            {
                Role = "system",
                Content = SystemPrompts.CoreSystemPrompt
            }
        };

        // Store initial conversation
        await this.conversationStore.SaveConversationAsync(new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            MessagesJson = JsonSerializer.Serialize(messages),
            Status = "active",
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        }, cancellationToken);

        return new ConversationResponse
        {
            ConversationId = conversationId,
            InitialMessage = SystemPrompts.InitialGreeting,
            StartedAt = startedAt,
            CustomerId = customerId
        };
    }

    /// <inheritdoc/>
    public async Task<IntakeResponse> ProcessMessageAsync(string conversationId, string userMessage, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Processing message for conversation {ConversationId}", conversationId);

        // Load conversation history
        var conversation = await this.conversationStore.GetConversationAsync(conversationId, cancellationToken);
        if (conversation == null)
        {
            throw new InvalidOperationException($"Conversation {conversationId} not found");
        }

        var messages = JsonSerializer.Deserialize<List<ConversationMessage>>(conversation.MessagesJson)
            ?? new List<ConversationMessage>();

        // Add user message
        messages.Add(new ConversationMessage
        {
            Role = "user",
            Content = userMessage
        });

        // Call AI provider
        var (assistantMessage, functionCall) = await CallAIProviderAsync(messages, cancellationToken);

        // Add assistant response
        messages.Add(assistantMessage);

        // Check if intake is complete
        var intakeComplete = false;
        BuildRequirements? requirements = null;
        decimal? estimatedCost = null;
        Dictionary<string, decimal>? costBreakdown = null;

        if (functionCall != null && functionCall.Name == "complete_intake")
        {
            this.logger.LogInformation("Intake completed for conversation {ConversationId}", conversationId);
            intakeComplete = true;
            requirements = await ExtractRequirementsAsync(functionCall.Arguments, cancellationToken);

            if (requirements != null)
            {
                (estimatedCost, costBreakdown) = await GenerateCostEstimateAsync(requirements, cancellationToken);
            }
        }

        // Update conversation
        var updatedConversation = conversation with
        {
            MessagesJson = JsonSerializer.Serialize(messages),
            Status = intakeComplete ? "completed" : "active",
            RequirementsJson = requirements != null ? JsonSerializer.Serialize(requirements) : conversation.RequirementsJson,
            UpdatedAt = DateTimeOffset.UtcNow,
            CompletedAt = intakeComplete ? DateTimeOffset.UtcNow : null
        };

        await this.conversationStore.SaveConversationAsync(updatedConversation, cancellationToken);

        return new IntakeResponse
        {
            ConversationId = conversationId,
            Message = assistantMessage.Content ?? string.Empty,
            IntakeComplete = intakeComplete,
            Requirements = requirements,
            EstimatedMonthlyCost = estimatedCost,
            CostBreakdown = costBreakdown,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc/>
    public async Task<ConversationRecord?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        return await this.conversationStore.GetConversationAsync(conversationId, cancellationToken);
    }

    private async Task<(ConversationMessage AssistantMessage, FunctionCall? FunctionCall)> CallAIProviderAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken)
    {
        return this.options.Provider.ToLowerInvariant() switch
        {
            "openai" => await CallOpenAIAsync(messages, cancellationToken),
            "anthropic" => await CallAnthropicAsync(messages, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported AI provider: {this.options.Provider}")
        };
    }

    private async Task<(ConversationMessage, FunctionCall?)> CallOpenAIAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken)
    {
        var httpClient = this.httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.options.OpenAIApiKey}");

        var functionDef = JsonSerializer.Deserialize<JsonElement>(SystemPrompts.CompleteIntakeFunctionDefinition);

        var requestBody = new
        {
            model = this.options.OpenAIModel,
            messages = messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                function_call = m.FunctionCall,
                name = m.Name
            }).ToArray(),
            functions = new[] { functionDef },
            function_call = "auto",
            temperature = this.options.Temperature,
            max_tokens = this.options.MaxTokens
        };

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        this.logger.LogDebug("Calling OpenAI API with {MessageCount} messages", messages.Count);

        var response = await httpClient.PostAsync(new Uri("https://api.openai.com/v1/chat/completions"), content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

        if (result?.Choices == null || result.Choices.Length == 0)
        {
            throw new InvalidOperationException("OpenAI returned no choices");
        }

        var choice = result.Choices[0];
        var message = choice.Message;

        var assistantMessage = new ConversationMessage
        {
            Role = "assistant",
            Content = message?.Content,
            FunctionCall = message?.FunctionCall
        };

        return (assistantMessage, message?.FunctionCall);
    }

    private async Task<(ConversationMessage, FunctionCall?)> CallAnthropicAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken)
    {
        var httpClient = this.httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", this.options.AnthropicApiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        // Convert to Anthropic format (system message separate)
        var systemMessage = messages.FirstOrDefault(m => m.Role == "system")?.Content ?? string.Empty;
        var conversationMessages = messages.Where(m => m.Role != "system").Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToArray();

        var requestBody = new
        {
            model = this.options.AnthropicModel,
            max_tokens = this.options.MaxTokens,
            temperature = this.options.Temperature,
            system = systemMessage,
            messages = conversationMessages
        };

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        this.logger.LogDebug("Calling Anthropic API with {MessageCount} messages", messages.Count);

        var response = await httpClient.PostAsync(new Uri("https://api.anthropic.com/v1/messages"), content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AnthropicResponse>(responseJson);

        if (result?.Content == null || result.Content.Length == 0)
        {
            throw new InvalidOperationException("Anthropic returned no content");
        }

        var responseContent = result.Content[0].Text ?? string.Empty;

        // For Anthropic, we need to parse function calls from the text response
        // This is a simplified implementation - in production you'd want more robust parsing
        FunctionCall? functionCall = null;
        if (responseContent.Contains("complete_intake"))
        {
            // Extract JSON from the response (this is a simplified parser)
            var startIdx = responseContent.IndexOf('{');
            var endIdx = responseContent.LastIndexOf('}');
            if (startIdx >= 0 && endIdx > startIdx)
            {
                var jsonArgs = responseContent.Substring(startIdx, endIdx - startIdx + 1);
                functionCall = new FunctionCall
                {
                    Name = "complete_intake",
                    Arguments = jsonArgs
                };
            }
        }

        var assistantMessage = new ConversationMessage
        {
            Role = "assistant",
            Content = responseContent,
            FunctionCall = functionCall
        };

        return (assistantMessage, functionCall);
    }

    private async Task<BuildRequirements?> ExtractRequirementsAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogDebug("Extracting requirements from function arguments");

            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            if (args == null)
            {
                return null;
            }

            var requirements = new BuildRequirements
            {
                Protocols = args.ContainsKey("protocols")
                    ? JsonSerializer.Deserialize<List<string>>(args["protocols"].GetRawText()) ?? new List<string>()
                    : new List<string>(),

                Databases = args.ContainsKey("databases")
                    ? JsonSerializer.Deserialize<List<string>>(args["databases"].GetRawText()) ?? new List<string>()
                    : new List<string>(),

                CloudProvider = args.ContainsKey("cloudProvider")
                    ? args["cloudProvider"].GetString() ?? string.Empty
                    : string.Empty,

                Architecture = args.ContainsKey("architecture")
                    ? args["architecture"].GetString() ?? string.Empty
                    : string.Empty,

                Tier = args.ContainsKey("tier")
                    ? args["tier"].GetString() ?? string.Empty
                    : string.Empty,

                Load = args.ContainsKey("expectedLoad")
                    ? JsonSerializer.Deserialize<ExpectedLoad>(args["expectedLoad"].GetRawText())
                    : null,

                AdvancedFeatures = args.ContainsKey("advancedFeatures")
                    ? JsonSerializer.Deserialize<List<string>>(args["advancedFeatures"].GetRawText())
                    : null,

                Notes = args.ContainsKey("notes")
                    ? args["notes"].GetString()
                    : null
            };

            this.logger.LogInformation("Successfully extracted requirements: Tier={Tier}, Protocols={ProtocolCount}, Databases={DatabaseCount}",
                requirements.Tier, requirements.Protocols.Count, requirements.Databases.Count);

            return requirements;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to extract requirements from function arguments");
            return null;
        }

        await Task.CompletedTask;
    }

    private async Task<(decimal TotalCost, Dictionary<string, decimal> Breakdown)> GenerateCostEstimateAsync(
        BuildRequirements requirements,
        CancellationToken cancellationToken)
    {
        this.logger.LogDebug("Generating cost estimate for tier {Tier}, provider {Provider}, architecture {Architecture}",
            requirements.Tier, requirements.CloudProvider, requirements.Architecture);

        // License tier cost
        var licenseCost = requirements.Tier.ToLowerInvariant() switch
        {
            "core" => 0m,
            "pro" => 499m,
            "enterprise" => 2500m,
            "enterprise-asp" => 5000m, // Base price, actual is custom
            _ => 0m
        };

        // Infrastructure cost (monthly compute)
        var loadClassification = requirements.Load?.Classification?.ToLowerInvariant() ?? "light";
        var isArm64 = requirements.Architecture.Contains("arm64", StringComparison.OrdinalIgnoreCase);

        var infrastructureCost = (requirements.CloudProvider.ToLowerInvariant(), loadClassification, isArm64) switch
        {
            // AWS
            ("aws", "light", true) => 24m,   // t4g.medium
            ("aws", "light", false) => 40m,  // t3.medium
            ("aws", "moderate", true) => 62m,  // c7g.large
            ("aws", "moderate", false) => 96m, // c6i.large
            ("aws", "heavy", true) => 124m,  // c7g.xlarge
            ("aws", "heavy", false) => 192m, // c6i.xlarge

            // Azure
            ("azure", "light", true) => 30m,  // D2ps_v5
            ("azure", "light", false) => 45m, // D2s_v5
            ("azure", "moderate", true) => 60m,  // D4ps_v5
            ("azure", "moderate", false) => 90m, // D4s_v5
            ("azure", "heavy", true) => 120m, // D8ps_v5
            ("azure", "heavy", false) => 180m, // D8s_v5

            // GCP
            ("gcp", "light", true) => 26m,  // t2a-standard-2
            ("gcp", "light", false) => 42m, // e2-standard-2
            ("gcp", "moderate", true) => 52m,  // t2a-standard-4
            ("gcp", "moderate", false) => 84m, // e2-standard-4
            ("gcp", "heavy", true) => 104m, // t2a-standard-8
            ("gcp", "heavy", false) => 168m, // e2-standard-8

            // On-premises (estimate)
            ("on-premises", _, _) => 100m,
            (_, _, _) => 50m  // Default fallback
        };

        // Storage cost (rough estimate based on data volume)
        var storageCost = requirements.Load?.DataVolumeGb switch
        {
            null => 10m,  // Default small storage
            <= 100 => 10m,
            <= 500 => 25m,
            <= 1000 => 50m,
            _ => 100m
        };

        var totalCost = licenseCost + infrastructureCost + storageCost;

        var breakdown = new Dictionary<string, decimal>
        {
            ["license"] = licenseCost,
            ["infrastructure"] = infrastructureCost,
            ["storage"] = storageCost
        };

        this.logger.LogInformation("Cost estimate: Total=${TotalCost}, License=${LicenseCost}, Infrastructure=${InfrastructureCost}, Storage=${StorageCost}",
            totalCost, licenseCost, infrastructureCost, storageCost);

        await Task.CompletedTask;
        return (totalCost, breakdown);
    }

    #region OpenAI Response Models

    private sealed class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public OpenAIChoice[]? Choices { get; init; }
    }

    private sealed class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; init; }
    }

    private sealed class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("function_call")]
        public FunctionCall? FunctionCall { get; init; }
    }

    #endregion

    #region Anthropic Response Models

    private sealed class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public AnthropicContent[]? Content { get; init; }
    }

    private sealed class AnthropicContent
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    #endregion
}

/// <summary>
/// Interface for storing and retrieving conversation records.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Saves or updates a conversation record.
    /// </summary>
    Task SaveConversationAsync(ConversationRecord conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a conversation by ID.
    /// </summary>
    Task<ConversationRecord?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);
}
