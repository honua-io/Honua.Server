// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request for AI-powered natural language search.
/// </summary>
public sealed class NaturalLanguageSearchRequest
{
    /// <summary>
    /// Natural language query (e.g., "show me all WMS services in the Pacific region").
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results.
    /// </summary>
    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Include explanation of how query was interpreted.
    /// </summary>
    [JsonPropertyName("includeExplanation")]
    public bool IncludeExplanation { get; set; } = true;
}

/// <summary>
/// Response from natural language search.
/// </summary>
public sealed class NaturalLanguageSearchResponse
{
    /// <summary>
    /// Interpreted search criteria.
    /// </summary>
    [JsonPropertyName("interpretation")]
    public string Interpretation { get; set; } = string.Empty;

    /// <summary>
    /// Search results.
    /// </summary>
    [JsonPropertyName("results")]
    public List<SearchResultItem> Results { get; set; } = new();

    /// <summary>
    /// Suggested refinements.
    /// </summary>
    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = new();
}

/// <summary>
/// Search result item from AI search.
/// </summary>
public sealed class SearchResultItem
{
    /// <summary>
    /// Item ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Item type (service, layer, folder).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Item title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Item description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Relevance score (0-100).
    /// </summary>
    [JsonPropertyName("relevance")]
    public int Relevance { get; set; }

    /// <summary>
    /// Why this item matched the query.
    /// </summary>
    [JsonPropertyName("matchReason")]
    public string? MatchReason { get; set; }

    /// <summary>
    /// Navigation URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
/// Request for smart suggestions.
/// </summary>
public sealed class SmartSuggestionRequest
{
    /// <summary>
    /// What to suggest (crs, style, servicetype, datasource).
    /// </summary>
    [JsonPropertyName("suggestionType")]
    public string SuggestionType { get; set; } = string.Empty;

    /// <summary>
    /// Context for suggestions.
    /// </summary>
    [JsonPropertyName("context")]
    public Dictionary<string, string> Context { get; set; } = new();
}

/// <summary>
/// Smart suggestion from AI.
/// </summary>
public sealed class SmartSuggestion
{
    /// <summary>
    /// Suggested value.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Display label.
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Why this is suggested.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Confidence (0-100).
    /// </summary>
    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }

    /// <summary>
    /// Is this the recommended choice?
    /// </summary>
    [JsonPropertyName("recommended")]
    public bool Recommended { get; set; }
}

/// <summary>
/// Request for AI to generate metadata.
/// </summary>
public sealed class GenerateMetadataRequest
{
    /// <summary>
    /// What type of item (service, layer).
    /// </summary>
    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>
    /// What fields to generate (title, abstract, keywords).
    /// </summary>
    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = new();

    /// <summary>
    /// Context about the item.
    /// </summary>
    [JsonPropertyName("context")]
    public Dictionary<string, string> Context { get; set; } = new();

    /// <summary>
    /// Existing metadata to enhance.
    /// </summary>
    [JsonPropertyName("existingMetadata")]
    public Dictionary<string, string> ExistingMetadata { get; set; } = new();
}

/// <summary>
/// Generated metadata from AI.
/// </summary>
public sealed class GenerateMetadataResponse
{
    /// <summary>
    /// Generated fields.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Explanation of what was generated.
    /// </summary>
    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }

    /// <summary>
    /// Quality score (0-100).
    /// </summary>
    [JsonPropertyName("quality")]
    public int Quality { get; set; }
}

/// <summary>
/// Request for troubleshooting help.
/// </summary>
public sealed class TroubleshootRequest
{
    /// <summary>
    /// Describe the problem.
    /// </summary>
    [JsonPropertyName("problem")]
    public string Problem { get; set; } = string.Empty;

    /// <summary>
    /// Error message if any.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// What were you trying to do?
    /// </summary>
    [JsonPropertyName("attemptedAction")]
    public string? AttemptedAction { get; set; }

    /// <summary>
    /// Related item ID (service, layer, etc.).
    /// </summary>
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    /// <summary>
    /// Item type.
    /// </summary>
    [JsonPropertyName("itemType")]
    public string? ItemType { get; set; }
}

/// <summary>
/// Troubleshooting response from AI.
/// </summary>
public sealed class TroubleshootResponse
{
    /// <summary>
    /// What the AI thinks the problem is.
    /// </summary>
    [JsonPropertyName("diagnosis")]
    public string Diagnosis { get; set; } = string.Empty;

    /// <summary>
    /// Suggested solutions (ordered by likelihood).
    /// </summary>
    [JsonPropertyName("solutions")]
    public List<Solution> Solutions { get; set; } = new();

    /// <summary>
    /// Related documentation.
    /// </summary>
    [JsonPropertyName("documentation")]
    public List<DocumentationLink> Documentation { get; set; } = new();

    /// <summary>
    /// Confidence in diagnosis (0-100).
    /// </summary>
    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }
}

/// <summary>
/// A solution to a problem.
/// </summary>
public sealed class Solution
{
    /// <summary>
    /// Solution title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Step-by-step instructions.
    /// </summary>
    [JsonPropertyName("steps")]
    public List<string> Steps { get; set; } = new();

    /// <summary>
    /// What you should see if it works.
    /// </summary>
    [JsonPropertyName("expectedResult")]
    public string? ExpectedResult { get; set; }

    /// <summary>
    /// How likely this is to fix it (0-100).
    /// </summary>
    [JsonPropertyName("likelihood")]
    public int Likelihood { get; set; }
}

/// <summary>
/// Documentation link.
/// </summary>
public sealed class DocumentationLink
{
    /// <summary>
    /// Link title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL to documentation.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Short description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Request for AI assistant chat.
/// </summary>
public sealed class AIChatRequest
{
    /// <summary>
    /// User's message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Conversation history.
    /// </summary>
    [JsonPropertyName("history")]
    public List<ChatMessage> History { get; set; } = new();

    /// <summary>
    /// Current context (what page, what item).
    /// </summary>
    [JsonPropertyName("context")]
    public Dictionary<string, string> Context { get; set; } = new();
}

/// <summary>
/// Response from AI chat.
/// </summary>
public sealed class AIChatResponse
{
    /// <summary>
    /// AI's response message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Suggested actions user can take.
    /// </summary>
    [JsonPropertyName("suggestedActions")]
    public List<SuggestedAction> SuggestedActions { get; set; } = new();

    /// <summary>
    /// Follow-up questions AI might ask.
    /// </summary>
    [JsonPropertyName("followUpQuestions")]
    public List<string> FollowUpQuestions { get; set; } = new();
}

/// <summary>
/// Chat message.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// Who sent it (user or assistant).
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Message content.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When it was sent.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Suggested action from AI.
/// </summary>
public sealed class SuggestedAction
{
    /// <summary>
    /// Action label.
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Action type (navigate, create, edit, delete).
    /// </summary>
    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Target URL or identifier.
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }

    /// <summary>
    /// Additional parameters.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// AI capability check response.
/// </summary>
public sealed class AICapabilitiesResponse
{
    /// <summary>
    /// Is AI available?
    /// </summary>
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    /// <summary>
    /// Available features.
    /// </summary>
    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = new();

    /// <summary>
    /// AI model being used.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Status message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// AI assistant feature names.
/// </summary>
public static class AIFeature
{
    public const string NaturalLanguageSearch = "natural-language-search";
    public const string SmartSuggestions = "smart-suggestions";
    public const string MetadataGeneration = "metadata-generation";
    public const string Troubleshooting = "troubleshooting";
    public const string Chat = "chat";
}

/// <summary>
/// Suggestion types.
/// </summary>
public static class SuggestionType
{
    public const string CRS = "crs";
    public const string Style = "style";
    public const string ServiceType = "servicetype";
    public const string DataSource = "datasource";
    public const string Config = "config";
}

/// <summary>
/// Chat roles.
/// </summary>
public static class ChatRole
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// Action types.
/// </summary>
public static class ActionType
{
    public const string Navigate = "navigate";
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Apply = "apply";
}
