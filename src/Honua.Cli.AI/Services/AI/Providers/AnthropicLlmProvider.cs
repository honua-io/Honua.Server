// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// LLM provider implementation for Anthropic Claude (Claude 3.5 Sonnet, etc.).
/// </summary>
public sealed class AnthropicLlmProvider : LlmProviderBase
{
    private readonly AnthropicOptions _options;
    private readonly AnthropicClient _client;

    public override string ProviderName => "Anthropic";
    public override string DefaultModel => _options.DefaultModel;

    public AnthropicLlmProvider(LlmProviderOptions options, ILogger<AnthropicLlmProvider>? logger = null)
        : base(options, logger ?? NullLogger<AnthropicLlmProvider>.Instance)
    {
        _options = options.Anthropic;

        if (_options.ApiKey.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Anthropic API key is required. Set it in configuration or environment variable ANTHROPIC_API_KEY.");
        }

        _client = new AnthropicClient(new APIAuthentication(_options.ApiKey));
    }

    protected override async Task<LlmResponse> CompleteInternalAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var messages = new List<Message>();

        // Add conversation history if provided
        if (request.ConversationHistory is not null)
        {
            foreach (var msg in request.ConversationHistory)
            {
                messages.Add(new Message
                {
                    Role = msg.Role.ToLowerInvariant() switch
                    {
                        "user" => RoleType.User,
                        "assistant" => RoleType.Assistant,
                        _ => RoleType.User
                    },
                    Content = new List<ContentBase> { new TextContent { Text = msg.Content } }
                });
            }
        }

        // Add current user prompt
        messages.Add(new Message
        {
            Role = RoleType.User,
            Content = new List<ContentBase> { new TextContent { Text = request.UserPrompt } }
        });

        // Create message parameters
        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = _options.DefaultModel,
            MaxTokens = request.MaxTokens ?? 4096,
            Temperature = (decimal?)request.Temperature,
            Stream = false
        };

        // Add system prompt if provided
        if (request.SystemPrompt.HasValue())
        {
            parameters.System = new List<SystemMessage>
            {
                new SystemMessage(request.SystemPrompt)
            };
        }

        // Call Anthropic API
        var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        if (response is null)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = "No response received from Anthropic"
            };
        }

        // Extract text content from response
        var content = string.Empty;
        if (response.Content is not null)
        {
            foreach (var contentBlock in response.Content)
            {
                if (contentBlock is TextContent textContent)
                {
                    content += textContent.Text;
                }
            }
        }

        return new LlmResponse
        {
            Content = content,
            Model = response.Model ?? DefaultModel,
            TotalTokens = response.Usage?.InputTokens + response.Usage?.OutputTokens,
            Success = true
        };
    }

    public override async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Note: Streaming does not support retry logic due to C# yield return limitations with try-catch.
        // Rate limit handling is only supported in CompleteAsync method.
        var messages = new List<Message>();

        // Add conversation history if provided
        if (request.ConversationHistory is not null)
        {
            foreach (var msg in request.ConversationHistory)
            {
                messages.Add(new Message
                {
                    Role = msg.Role.ToLowerInvariant() switch
                    {
                        "user" => RoleType.User,
                        "assistant" => RoleType.Assistant,
                        _ => RoleType.User
                    },
                    Content = new List<ContentBase> { new TextContent { Text = msg.Content } }
                });
            }
        }

        // Add current user prompt
        messages.Add(new Message
        {
            Role = RoleType.User,
            Content = new List<ContentBase> { new TextContent { Text = request.UserPrompt } }
        });

        // Create message parameters
        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = _options.DefaultModel,
            MaxTokens = request.MaxTokens ?? 4096,
            Temperature = (decimal?)request.Temperature,
            Stream = true
        };

        // Add system prompt if provided
        if (request.SystemPrompt.HasValue())
        {
            parameters.System = new List<SystemMessage>
            {
                new SystemMessage(request.SystemPrompt)
            };
        }

        // Stream from Anthropic API
        await foreach (var streamResponse in _client.Messages.StreamClaudeMessageAsync(parameters, cancellationToken))
        {
            if (streamResponse is not null)
            {
                var content = string.Empty;
                var isFinal = false;

                // Handle different event types
                if (streamResponse.Delta is not null)
                {
                    if (streamResponse.Delta.Text is not null)
                    {
                        content = streamResponse.Delta.Text;
                    }
                }

                if (streamResponse.Type == "message_stop")
                {
                    isFinal = true;
                }

                if (!content.IsNullOrEmpty() || isFinal)
                {
                    yield return new LlmStreamChunk
                    {
                        Content = content,
                        IsFinal = isFinal,
                        TokenCount = null
                    };
                }
            }
        }
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Anthropic SDK doesn't provide a model listing API
        // Return common Claude models
        return await Task.FromResult<IReadOnlyList<string>>(new[]
        {
            "claude-3-5-sonnet-20241022",
            "claude-3-5-sonnet-20240620",
            "claude-3-opus-20240229",
            "claude-3-sonnet-20240229",
            "claude-3-haiku-20240307"
        });
    }
}
