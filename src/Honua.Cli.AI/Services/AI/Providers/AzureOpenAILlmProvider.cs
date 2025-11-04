// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Chat;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// LLM provider implementation for Azure OpenAI Service.
/// Uses Azure-hosted OpenAI models with enterprise features (SLA, VNet, compliance).
/// </summary>
public sealed class AzureOpenAILlmProvider : LlmProviderBase
{
    private readonly AzureOpenAIOptions _options;
    private readonly ChatClient _chatClient;

    public override string ProviderName => "AzureOpenAI";
    public override string DefaultModel => _options.DefaultModel;

    public AzureOpenAILlmProvider(LlmProviderOptions options, ILogger<AzureOpenAILlmProvider>? logger = null)
        : base(options, logger ?? NullLogger<AzureOpenAILlmProvider>.Instance)
    {
        _options = options.Azure;

        if (_options.EndpointUrl.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Azure OpenAI endpoint URL is required. Set it in configuration (LlmProvider:Azure:EndpointUrl).");
        }

        if (_options.ApiKey.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Azure OpenAI API key is required. Set it in configuration (LlmProvider:Azure:ApiKey) or use Managed Identity.");
        }

        if (_options.DeploymentName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Azure OpenAI deployment name is required. Set it in configuration (LlmProvider:Azure:DeploymentName).");
        }

        // Create Azure OpenAI client with API key
        var client = new AzureOpenAIClient(
            new Uri(_options.EndpointUrl),
            new global::Azure.AzureKeyCredential(_options.ApiKey));

        _chatClient = client.GetChatClient(_options.DeploymentName);
    }

    protected override async Task<LlmResponse> CompleteInternalAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>();

        // Add system prompt if provided
        if (request.SystemPrompt.HasValue())
        {
            messages.Add(new SystemChatMessage(request.SystemPrompt));
        }

        // Add conversation history if provided
        if (request.ConversationHistory is not null)
        {
            foreach (var msg in request.ConversationHistory)
            {
                messages.Add(msg.Role.ToLowerInvariant() switch
                {
                    "system" => new SystemChatMessage(msg.Content),
                    "user" => new UserChatMessage(msg.Content),
                    "assistant" => new AssistantChatMessage(msg.Content),
                    _ => throw new ArgumentException($"Unknown role: {msg.Role}")
                });
            }
        }

        // Add current user prompt
        messages.Add(new UserChatMessage(request.UserPrompt));

        // Create completion options
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxTokens,
            Temperature = (float?)request.Temperature
        };

        // Call Azure OpenAI API
        var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);

        if (completion?.Value is null)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = "No response received from Azure OpenAI"
            };
        }

        return new LlmResponse
        {
            Content = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty,
            Model = completion.Value.Model,
            TotalTokens = completion.Value.Usage.TotalTokenCount,
            Success = true
        };
    }

    public override async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Note: Streaming does not support retry logic due to C# yield return limitations with try-catch.
        // Rate limit handling is only supported in CompleteAsync method.
        var messages = new List<ChatMessage>();

        // Add system prompt if provided
        if (request.SystemPrompt.HasValue())
        {
            messages.Add(new SystemChatMessage(request.SystemPrompt));
        }

        // Add conversation history if provided
        if (request.ConversationHistory is not null)
        {
            foreach (var msg in request.ConversationHistory)
            {
                messages.Add(msg.Role.ToLowerInvariant() switch
                {
                    "system" => new SystemChatMessage(msg.Content),
                    "user" => new UserChatMessage(msg.Content),
                    "assistant" => new AssistantChatMessage(msg.Content),
                    _ => throw new ArgumentException($"Unknown role: {msg.Role}")
                });
            }
        }

        // Add current user prompt
        messages.Add(new UserChatMessage(request.UserPrompt));

        // Create completion options
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxTokens,
            Temperature = (float?)request.Temperature
        };

        // Stream from Azure OpenAI API
        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!contentPart.Text.IsNullOrEmpty())
                {
                    yield return new LlmStreamChunk
                    {
                        Content = contentPart.Text,
                        IsFinal = false,
                        TokenCount = null
                    };
                }
            }

            // Check if this is the final chunk
            if (update.FinishReason.HasValue)
            {
                yield return new LlmStreamChunk
                {
                    Content = string.Empty,
                    IsFinal = true,
                    TokenCount = update.Usage?.TotalTokenCount
                };
            }
        }
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Azure OpenAI uses deployments, not direct model access
        // Return the configured deployment name
        return await Task.FromResult<IReadOnlyList<string>>(new[]
        {
            _options.DeploymentName ?? _options.DefaultModel
        });
    }
}
