// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Tests.Models;

public class AIAssistantModelsTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public void NaturalLanguageSearchRequest_ShouldSerializeCorrectly()
    {
        // Arrange
        var request = new NaturalLanguageSearchRequest
        {
            Query = "show me all WMS services in Pacific region",
            MaxResults = 20,
            IncludeExplanation = true
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<NaturalLanguageSearchRequest>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Query.Should().Be("show me all WMS services in Pacific region");
        deserialized.MaxResults.Should().Be(20);
        deserialized.IncludeExplanation.Should().BeTrue();
    }

    [Fact]
    public void SmartSuggestion_ShouldSerializeWithAllProperties()
    {
        // Arrange
        var suggestion = new SmartSuggestion
        {
            Value = "EPSG:4326",
            Label = "WGS 84",
            Reason = "Most common global CRS",
            Confidence = 95,
            Recommended = true
        };

        // Act
        var json = JsonSerializer.Serialize(suggestion, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SmartSuggestion>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Value.Should().Be("EPSG:4326");
        deserialized.Confidence.Should().Be(95);
        deserialized.Recommended.Should().BeTrue();
    }

    [Fact]
    public void GenerateMetadataRequest_ShouldIncludeContext()
    {
        // Arrange
        var request = new GenerateMetadataRequest
        {
            ItemType = "service",
            Fields = new List<string> { "title", "abstract", "keywords" },
            Context = new Dictionary<string, string>
            {
                ["serviceType"] = "WMS",
                ["region"] = "Pacific"
            },
            ExistingMetadata = new Dictionary<string, string>
            {
                ["id"] = "pacific-wms"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<GenerateMetadataRequest>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ItemType.Should().Be("service");
        deserialized.Fields.Should().HaveCount(3);
        deserialized.Context.Should().ContainKey("serviceType");
        deserialized.ExistingMetadata.Should().ContainKey("id");
    }

    [Fact]
    public void TroubleshootResponse_ShouldDeserializeWithSolutions()
    {
        // Arrange
        var json = """
        {
            "diagnosis": "Database connection failure",
            "solutions": [
                {
                    "title": "Check connection string",
                    "steps": ["Verify host", "Verify port", "Test credentials"],
                    "expectedResult": "Service should connect",
                    "likelihood": 90
                }
            ],
            "documentation": [
                {
                    "title": "Database Setup",
                    "url": "https://docs.example.com/database",
                    "description": "How to configure database"
                }
            ],
            "confidence": 85
        }
        """;

        // Act
        var response = JsonSerializer.Deserialize<TroubleshootResponse>(json, _jsonOptions);

        // Assert
        response.Should().NotBeNull();
        response!.Diagnosis.Should().Be("Database connection failure");
        response.Solutions.Should().HaveCount(1);
        response.Solutions[0].Steps.Should().HaveCount(3);
        response.Documentation.Should().HaveCount(1);
        response.Confidence.Should().Be(85);
    }

    [Fact]
    public void AIChatRequest_ShouldIncludeHistory()
    {
        // Arrange
        var request = new AIChatRequest
        {
            Message = "How do I create a layer?",
            History = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatRole.User,
                    Content = "Hello",
                    Timestamp = DateTimeOffset.UtcNow
                },
                new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = "Hi! How can I help?",
                    Timestamp = DateTimeOffset.UtcNow
                }
            },
            Context = new Dictionary<string, string>
            {
                ["page"] = "layers"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AIChatRequest>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Message.Should().Be("How do I create a layer?");
        deserialized.History.Should().HaveCount(2);
        deserialized.History[0].Role.Should().Be(ChatRole.User);
        deserialized.History[1].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public void AIChatResponse_ShouldIncludeSuggestedActions()
    {
        // Arrange
        var response = new AIChatResponse
        {
            Message = "Click the Create Layer button",
            SuggestedActions = new List<SuggestedAction>
            {
                new SuggestedAction
                {
                    Label = "Create Layer",
                    ActionType = ActionType.Create,
                    Target = "/layers/new",
                    Parameters = new Dictionary<string, string>
                    {
                        ["serviceId"] = "test-service"
                    }
                }
            },
            FollowUpQuestions = new List<string>
            {
                "What type of geometry will the layer have?"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AIChatResponse>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.SuggestedActions.Should().HaveCount(1);
        deserialized.SuggestedActions[0].ActionType.Should().Be(ActionType.Create);
        deserialized.FollowUpQuestions.Should().HaveCount(1);
    }

    [Fact]
    public void AICapabilitiesResponse_ShouldIncludeFeatures()
    {
        // Arrange
        var response = new AICapabilitiesResponse
        {
            Available = true,
            Features = new List<string>
            {
                AIFeature.NaturalLanguageSearch,
                AIFeature.SmartSuggestions,
                AIFeature.MetadataGeneration
            },
            Model = "claude-3-sonnet",
            Message = "AI assistant is ready"
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AICapabilitiesResponse>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Available.Should().BeTrue();
        deserialized.Features.Should().HaveCount(3);
        deserialized.Model.Should().Be("claude-3-sonnet");
    }

    [Fact]
    public void SearchResultItem_ShouldIncludeRelevanceScore()
    {
        // Arrange
        var item = new SearchResultItem
        {
            Id = "service-1",
            Type = "service",
            Title = "Pacific WMS",
            Description = "WMS service for Pacific region",
            Relevance = 95,
            MatchReason = "Matched service type and region",
            Url = "/services/service-1"
        };

        // Act
        var json = JsonSerializer.Serialize(item, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SearchResultItem>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Relevance.Should().Be(95);
        deserialized.MatchReason.Should().Contain("service type");
    }

    [Fact]
    public void AIFeature_Constants_ShouldHaveCorrectValues()
    {
        // Assert
        AIFeature.NaturalLanguageSearch.Should().Be("natural-language-search");
        AIFeature.SmartSuggestions.Should().Be("smart-suggestions");
        AIFeature.MetadataGeneration.Should().Be("metadata-generation");
        AIFeature.Troubleshooting.Should().Be("troubleshooting");
        AIFeature.Chat.Should().Be("chat");
    }

    [Fact]
    public void SuggestionType_Constants_ShouldHaveCorrectValues()
    {
        // Assert
        SuggestionType.CRS.Should().Be("crs");
        SuggestionType.Style.Should().Be("style");
        SuggestionType.ServiceType.Should().Be("servicetype");
        SuggestionType.DataSource.Should().Be("datasource");
        SuggestionType.Config.Should().Be("config");
    }

    [Fact]
    public void ChatRole_Constants_ShouldHaveCorrectValues()
    {
        // Assert
        ChatRole.User.Should().Be("user");
        ChatRole.Assistant.Should().Be("assistant");
    }

    [Fact]
    public void ActionType_Constants_ShouldHaveCorrectValues()
    {
        // Assert
        ActionType.Navigate.Should().Be("navigate");
        ActionType.Create.Should().Be("create");
        ActionType.Edit.Should().Be("edit");
        ActionType.Delete.Should().Be("delete");
        ActionType.Apply.Should().Be("apply");
    }
}
