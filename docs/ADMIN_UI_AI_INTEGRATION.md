# Admin UI AI Integration

**Date:** 2025-11-03
**Status:** Proposed

---

## Executive Summary

Integrate HonuaIO's existing AI capabilities (via `Honua.Cli.AI`) into the Admin UI to provide intelligent assistance for GIS administrators. This will leverage existing investments in Microsoft Semantic Kernel, Azure OpenAI, and Anthropic Claude.

---

## Existing AI Capabilities

### Current AI Stack (from `Honua.Cli.AI`)

| Technology | Version | Purpose |
|-----------|---------|---------|
| **Microsoft Semantic Kernel** | 1.66.0 | AI orchestration framework |
| **Azure OpenAI** | 2.5.0-beta.1 | LLM capabilities (GPT-4, GPT-3.5) |
| **Anthropic Claude** | 5.6.0 | Alternative LLM (Claude 3.5 Sonnet) |
| **Semantic Kernel Agents** | 1.66.0 | Multi-agent orchestration |
| **Azure AI Search** | 11.6.0 | Vector search, RAG |

### Existing AI Services

- `CloudDiscoveryService` - AI-powered cloud resource discovery
- `ApiDocumentationService` - Auto-generate API documentation
- `DeploymentAnalysisService` - Analyze deployment configurations
- `LlmAgentSelectionService` - Route queries to appropriate agents
- `HierarchicalTaskDecomposer` - Break down complex tasks
- Various configuration generation services (Terraform, Kubernetes, Docker Compose)

---

## AI-Powered Admin UI Features

### Phase 1: AI Assistant (MVP)

#### 1. **Natural Language Search**

Allow admins to search using natural language instead of filters:

```
User: "Show me all WMS services created in the last 30 days"
AI: Executes query and displays results

User: "Which layers have caching disabled?"
AI: Filters layer list

User: "Find services in the 'Water Resources' folder"
AI: Navigates to folder and shows services
```

**Implementation:**

```razor
@* ServiceSearch.razor *@
@inject HttpClient Http  // Configured with BearerTokenDelegatingHandler

<MudTextField @bind-Value="_searchQuery"
              Label="Ask me anything about your services..."
              Adornment="Adornment.End"
              AdornmentIcon="@Icons.Material.Filled.Psychology"
              OnAdornmentClick="HandleAISearchAsync" />

@code {
    private async Task HandleAISearchAsync()
    {
        // HttpClient automatically includes bearer token via delegating handler
        var result = await Http.PostAsJsonAsync("/admin/ai/search", new
        {
            Query = _searchQuery,
            Context = "services" // or "layers", "folders"
        });

        if (result.IsSuccessStatusCode)
        {
            var searchResult = await result.Content.ReadFromJsonAsync<AISearchResult>();
            _services = searchResult.Results;
        }
    }
}
```

**Note:** `HttpClient` is configured with `BearerTokenDelegatingHandler` (see ADMIN_UI_ARCHITECTURE.md) which automatically includes the user's access token and handles token refresh.

---

#### 2. **Metadata Generation from Data**

Upload a file → AI suggests layer metadata:

```
User uploads: rivers.shp

AI suggests:
- Title: "River Networks"
- Abstract: "Polyline dataset representing major river systems. Contains
  attributes for river name, length, flow rate, and basin identifier."
- Keywords: ["rivers", "hydrology", "water resources", "polyline"]
- CRS: EPSG:4326
- Suggested style: Blue lines, width based on flow rate
```

**UI Flow:**

```razor
<MudFileUpload T="IBrowserFile" OnFilesChanged="HandleFileUploadAsync">
    <ButtonTemplate>
        <MudButton Variant="Variant.Filled" Color="Color.Primary">
            Upload File & Generate Metadata
        </MudButton>
    </ButtonTemplate>
</MudFileUpload>

@if (_aiSuggestions != null)
{
    <MudPaper Class="pa-4 mt-4">
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.AutoAwesome" />
            AI Suggestions
        </MudText>

        <MudTextField @bind-Value="_aiSuggestions.Title" Label="Title" />
        <MudTextField @bind-Value="_aiSuggestions.Abstract"
                      Label="Abstract"
                      Lines="3" />
        <MudChipSet>
            @foreach (var keyword in _aiSuggestions.Keywords)
            {
                <MudChip>@keyword</MudChip>
            }
        </MudChipSet>

        <MudButton OnClick="AcceptSuggestionsAsync">Accept All</MudButton>
        <MudButton OnClick="RejectSuggestionsAsync">Regenerate</MudButton>
    </MudPaper>
}
```

---

#### 3. **Configuration Assistant**

Conversational help for complex configurations:

```
User: "How do I configure caching for this WMS service?"

AI: "I'll help you set up caching for your WMS service. I recommend:

1. Enable tile caching (reduces load by 80%)
2. Set cache TTL to 3600 seconds (1 hour)
3. Use disk cache for large datasets
4. Enable cache seeding for frequently accessed areas

Would you like me to apply these settings?"

User: "Yes"

AI: [Applies configuration]
```

**Chat Interface:**

```razor
<MudPaper Class="pa-4" Style="height: 600px; overflow-y: auto;">
    @foreach (var message in _chatHistory)
    {
        <div class="@GetMessageClass(message.Role)">
            @if (message.Role == "assistant")
            {
                <MudIcon Icon="@Icons.Material.Filled.SmartToy" />
            }
            else
            {
                <MudIcon Icon="@Icons.Material.Filled.Person" />
            }
            <MudText>@message.Content</MudText>

            @if (message.SuggestedActions?.Any() == true)
            {
                <MudButtonGroup>
                    @foreach (var action in message.SuggestedActions)
                    {
                        <MudButton OnClick="@(() => ExecuteActionAsync(action))">
                            @action.Label
                        </MudButton>
                    }
                </MudButtonGroup>
            }
        </div>
    }
</MudPaper>

<MudTextField @bind-Value="_userInput"
              Label="Ask me anything..."
              OnKeyDown="HandleEnterKey" />
```

---

### Phase 2: Advanced AI Features

#### 4. **Intelligent Error Detection**

AI analyzes configurations and finds issues:

```
AI: "⚠️ I noticed some potential issues:

1. Layer 'highways' references a non-existent style 'road_style'
   → Suggested fix: Create style or use 'default_line'

2. Service 'water_services' has 15 layers without caching
   → Performance impact: High
   → Suggested fix: Enable caching with 1-hour TTL

3. Data source 'legacy_db' connection string contains hardcoded password
   → Security risk: High
   → Suggested fix: Use environment variable or Key Vault

Would you like me to fix these automatically?"
```

**Implementation:**

```csharp
// Admin API endpoint
app.MapPost("/admin/ai/analyze-service/{serviceId}", async (
    string serviceId,
    IAIAnalysisService aiService,
    IMutableMetadataProvider metadataProvider) =>
{
    var snapshot = await metadataProvider.LoadAsync();
    var service = snapshot.Services.FirstOrDefault(s => s.Id == serviceId);

    if (service == null)
        return Results.NotFound();

    // AI analyzes the service configuration
    var analysis = await aiService.AnalyzeServiceAsync(service, snapshot);

    return Results.Ok(new
    {
        Issues = analysis.Issues.Select(i => new
        {
            i.Severity,
            i.Message,
            i.SuggestedFix,
            i.AutoFixAvailable
        }),
        Recommendations = analysis.Recommendations,
        Score = analysis.QualityScore
    });
})
.RequireAuthorization("RequireDataPublisher");
```

---

#### 5. **Style Generation**

AI generates styles based on data characteristics:

```
User: "Create a style for this population density layer"

AI analyzes data:
- Type: Polygon
- Attribute: population_density (numeric)
- Range: 0 - 15000 people/km²

AI generates:
- Choropleth map with 5 classes
- Color scheme: Yellow (low) → Red (high)
- Natural breaks classification
- Labels for high-density areas
```

**UI:**

```razor
<MudButton OnClick="GenerateStyleAsync"
           StartIcon="@Icons.Material.Filled.AutoAwesome">
    Generate Style with AI
</MudButton>

@if (_generatedStyle != null)
{
    <MudPaper Class="pa-4">
        <MudText Typo="Typo.h6">Generated Style Preview</MudText>

        @* Show style preview *@
        <StylePreview Style="@_generatedStyle" />

        <MudText Typo="Typo.body2" Class="mt-2">
            @_generatedStyle.Explanation
        </MudText>

        <MudButtonGroup Class="mt-2">
            <MudButton OnClick="ApplyStyleAsync">Apply Style</MudButton>
            <MudButton OnClick="RegenerateStyleAsync">Try Different Style</MudButton>
            <MudButton OnClick="EditStyleAsync">Edit Manually</MudButton>
        </MudButtonGroup>
    </MudPaper>
}
```

---

#### 6. **Query Optimization Suggestions**

AI analyzes layer usage and suggests optimizations:

```
AI: "I analyzed your 'parcels' layer performance:

📊 Statistics:
- 2.3M features
- Average query time: 4.2 seconds
- 80% of queries filter by 'owner_name'

💡 Recommendations:
1. Add B-tree index on 'owner_name' → Expected 75% faster
2. Enable spatial index (currently disabled) → Expected 60% faster
3. Partition table by municipality → Expected 40% faster for local queries

Estimated improvement: 3x faster queries

Would you like me to generate the SQL for these optimizations?"
```

---

#### 7. **Bulk Operations Assistant**

AI helps with bulk changes:

```
User: "Update all WMS services to use EPSG:3857 as default CRS"

AI: "I found 23 WMS services. Here's what I'll do:

Services to update:
- water_services (currently EPSG:4326)
- transportation_wms (currently EPSG:4326)
- ... (21 more)

Changes:
- Set default CRS to EPSG:3857
- Keep existing supported CRS list
- Update capabilities document

Backup: Version snapshot 'pre-crs-update' will be created

Proceed?"

User: "Yes"

AI: [Executes bulk update with progress bar]
```

---

### Phase 3: Predictive & Proactive Features

#### 8. **Usage Prediction**

AI predicts resource needs:

```
AI: "📈 Usage Forecast for Next 30 Days:

Based on historical patterns, I predict:
- 2.5x increase in 'weather_radar' layer requests (hurricane season)
- Peak usage: Sept 15-20 (3,000 req/min expected)

🛡️ Recommended Actions:
1. Pre-seed cache for weather_radar tiles (zoom levels 5-10)
2. Increase cache TTL to 30 minutes during peak
3. Consider CDN for this layer

Would you like me to schedule these optimizations?"
```

---

#### 9. **Automated Documentation**

AI generates user-facing documentation:

```
User: "Generate documentation for water_services"

AI creates:
- User guide with screenshots
- API examples (WMS GetMap, GetFeatureInfo)
- Common use cases
- Troubleshooting tips
- Links to related services

Output formats:
- Markdown
- HTML
- PDF
```

---

## Architecture

### Option A: Direct Integration (Recommended for Phase 1)

**Blazor UI** ←→ **Admin API** ←→ **AI Service (Semantic Kernel)**

```
┌─────────────────────────────────────────────────┐
│ Blazor Admin UI                                 │
│  - AI Chat component                            │
│  - AI-powered search                            │
│  - Suggestion cards                             │
└────────────┬────────────────────────────────────┘
             │ HTTP POST /admin/ai/*
             ↓
┌─────────────────────────────────────────────────┐
│ Admin API (new endpoints)                       │
│  POST /admin/ai/search                          │
│  POST /admin/ai/analyze-service/{id}            │
│  POST /admin/ai/generate-metadata               │
│  POST /admin/ai/chat                            │
└────────────┬────────────────────────────────────┘
             │ uses
             ↓
┌─────────────────────────────────────────────────┐
│ AI Services (from Honua.Cli.AI)                │
│  - Semantic Kernel                              │
│  - Azure OpenAI / Anthropic                     │
│  - Agent orchestration                          │
└────────────┬────────────────────────────────────┘
             │ queries
             ↓
┌─────────────────────────────────────────────────┐
│ Metadata Provider + Database                    │
└─────────────────────────────────────────────────┘
```

**Pros:**
- Simple architecture
- Reuses existing AI services
- Low latency

**Cons:**
- AI processing happens in API process (CPU/memory usage)
- May need to scale API for AI workloads

---

### Option B: Separate AI Service (Recommended for Phase 2+)

**Blazor UI** ←→ **Admin API** ←→ **AI Service (separate)** ←→ **Metadata**

```
┌─────────────────────────────────────────────────┐
│ Blazor Admin UI                                 │
└────────────┬────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────┐
│ Admin API                                       │
└────────────┬────────────────────────────────────┘
             │ HTTP POST /ai/*
             ↓
┌─────────────────────────────────────────────────┐
│ Honua.AI.Service (separate service)            │
│  - Hosts Semantic Kernel                        │
│  - Agent orchestration                          │
│  - Caching (Redis)                              │
│  - Rate limiting per user                       │
└────────────┬────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────┐
│ Azure OpenAI / Anthropic / Ollama (local)      │
└─────────────────────────────────────────────────┘
```

**Pros:**
- Independent scaling
- Isolate AI costs/usage
- Can use different auth (API keys for AI service)
- Easier to A/B test different models

**Cons:**
- More complex deployment
- Network latency between services

---

## API Design

### AI Search Endpoint

```csharp
// POST /admin/ai/search
public record AISearchRequest(
    string Query,                    // "Show all WMS services created last month"
    string Context,                  // "services", "layers", "folders"
    Dictionary<string, object>? Filters = null  // Additional context
);

public record AISearchResponse(
    List<object> Results,            // Filtered/searched items
    string GeneratedQuery,           // SQL or filter expression used
    string Explanation,              // "I found 12 WMS services created between..."
    List<string> SuggestedRefinements  // ["Filter by folder", "Show only cached services"]
);

// Implementation example
app.MapPost("/admin/ai/search", async (
    AISearchRequest request,
    HttpContext httpContext,
    IAISearchService aiService,
    IMutableMetadataProvider metadataProvider) =>
{
    // AI service receives the user's bearer token via HttpContext
    var userToken = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

    // Load metadata (respects user's authorization)
    var snapshot = await metadataProvider.LoadAsync();

    // AI analyzes query and generates search
    var results = await aiService.SearchAsync(request.Query, request.Context, snapshot);

    return Results.Ok(results);
})
.RequireAuthorization("RequireDataPublisher");  // Uses bearer token auth
```

### Metadata Generation Endpoint

```csharp
// POST /admin/ai/generate-metadata
public record GenerateMetadataRequest(
    string FileUrl,                  // Uploaded file URL
    string? ExistingMetadata = null  // Optional: existing metadata to enhance
);

public record GenerateMetadataResponse(
    string Title,
    string Abstract,
    List<string> Keywords,
    string SuggestedCRS,
    BoundingBox SuggestedExtent,
    StyleSuggestion? SuggestedStyle,
    float ConfidenceScore            // 0.0 - 1.0
);
```

### Chat/Assistant Endpoint

```csharp
// POST /admin/ai/chat
public record AIChatRequest(
    string Message,
    string? ConversationId = null,   // For multi-turn conversations
    Dictionary<string, object>? Context = null  // Current page context
);

public record AIChatResponse(
    string Message,
    List<SuggestedAction>? Actions = null,
    string? ConversationId = null
);

public record SuggestedAction(
    string Id,
    string Label,
    string ActionType,               // "navigate", "apply_config", "execute_query"
    Dictionary<string, object> Parameters
);
```

---

## Security & Cost Considerations

### Authentication & Authorization

**All AI endpoints use the same bearer token authentication as the rest of the Admin API:**

```csharp
// AI endpoints require admin authentication via bearer token
app.MapPost("/admin/ai/chat", HandleChatAsync)
    .RequireAuthorization("RequireAdministrator")  // or RequireDataPublisher for some features
    .RequireRateLimiting("ai-operations");

app.MapPost("/admin/ai/search", HandleAISearchAsync)
    .RequireAuthorization("RequireDataPublisher")  // Read-only, less restrictive
    .RequireRateLimiting("ai-operations");

app.MapPost("/admin/ai/generate-metadata", HandleGenerateMetadataAsync)
    .RequireAuthorization("RequireDataPublisher")
    .RequireRateLimiting("ai-operations");

app.MapPost("/admin/ai/analyze-service/{id}", HandleAnalyzeServiceAsync)
    .RequireAuthorization("RequireAdministrator")  // Can suggest config changes
    .RequireRateLimiting("ai-operations");
```

**Bearer Token Flow:**

1. **User authenticates** via OIDC, SAML, or Local mode
2. **Token exchange** produces OAuth 2.1 bearer token with scope `honua-control-plane`
3. **Admin UI** includes `Authorization: Bearer {token}` header on all AI endpoint calls
4. **AI service** validates token using same JWT validation as other admin endpoints
5. **AI assistant uses user's access token** when calling back to the admin API for data

**Important Security Points:**

- ✅ **Same authentication** as other admin endpoints (no separate AI auth)
- ✅ **User's token is passed through** - AI acts on behalf of the authenticated user
- ✅ **Respects authorization policies** - AI can't access data the user can't access
- ✅ **Audit trail preserved** - AI operations logged with user identity
- ✅ **Token refresh handled** - UI refreshes tokens before AI calls (same as other endpoints)

**AI Service Calling Other Admin Endpoints:**

When the AI assistant needs to fetch additional data from the control plane, it uses the user's access token:

```csharp
// AISearchService.cs
public class AISearchService : IAISearchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<AISearchResponse> SearchAsync(
        string query,
        string context,
        MetadataSnapshot snapshot)
    {
        // If AI needs to fetch more detailed data, use user's token
        var httpClient = _httpClientFactory.CreateClient("AdminApi");

        // Extract bearer token from current request
        var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization
            .ToString().Replace("Bearer ", "");

        if (!string.IsNullOrEmpty(token))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // AI can now call other admin endpoints on behalf of the user
            // Example: Fetch detailed layer information
            var layerDetails = await httpClient.GetFromJsonAsync<LayerDto>(
                $"/admin/metadata/layers/{layerId}");
        }

        // Perform AI search logic...
    }
}
```

**Why This Matters:**
- AI assistant respects the same access controls as the user
- If user can only see certain services/layers, AI also only sees those
- Audit logs show user identity, not a "system" or "AI" user
- No need for separate AI service account credentials

### Rate Limiting

**Protect against excessive AI costs and quota exhaustion:**

```csharp
// Rate limiting configuration
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ai-operations", opt =>
    {
        opt.PermitLimit = 20;           // 20 AI requests
        opt.Window = TimeSpan.FromMinutes(1);  // per minute
        opt.QueueLimit = 5;             // Queue up to 5 requests
    });
});
```

### Cost Management

```csharp
// Cost tracking service
public class AICostTrackingService
{
    public async Task TrackUsageAsync(
        string userId,
        string model,
        int inputTokens,
        int outputTokens)
    {
        var cost = CalculateCost(model, inputTokens, outputTokens);

        await _db.AiUsage.AddAsync(new AiUsageRecord
        {
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCost = cost
        });

        await _db.SaveChangesAsync();

        // Check quota
        var monthlyUsage = await GetMonthlyUsageAsync(userId);
        if (monthlyUsage > _options.MonthlyQuotaPerUser)
        {
            throw new QuotaExceededException($"User {userId} exceeded monthly AI quota");
        }
    }

    private decimal CalculateCost(string model, int input, int output)
    {
        // GPT-4 pricing (as of 2024)
        return model switch
        {
            "gpt-4" => (input * 0.03m / 1000) + (output * 0.06m / 1000),
            "gpt-3.5-turbo" => (input * 0.001m / 1000) + (output * 0.002m / 1000),
            "claude-3-sonnet" => (input * 0.003m / 1000) + (output * 0.015m / 1000),
            _ => 0m
        };
    }
}
```

### Budget Alerts

```csharp
// Alert when AI costs exceed threshold
if (monthlyAICost > 100m)
{
    await _notificationService.SendAsync(new
    {
        Type = "AIBudgetAlert",
        Message = $"AI costs this month: ${monthlyAICost:F2} (limit: $100)",
        Severity = "Warning"
    });
}
```

---

## User Experience

### AI Indicator

Show when AI is processing:

```razor
<MudPaper Class="ai-assistant">
    @if (_aiProcessing)
    {
        <MudProgressCircular Indeterminate="true" Size="Size.Small" />
        <MudText Typo="Typo.caption">AI is thinking...</MudText>
    }
    else
    {
        <MudIcon Icon="@Icons.Material.Filled.Psychology" Color="Color.Primary" />
        <MudText Typo="Typo.caption">AI Assistant ready</MudText>
    }
</MudPaper>
```

### Confidence Indicators

Show AI confidence for suggestions:

```razor
<MudChip Color="@GetConfidenceColor(suggestion.Confidence)">
    @GetConfidenceLabel(suggestion.Confidence)
</MudChip>

@code {
    private Color GetConfidenceColor(float confidence) => confidence switch
    {
        >= 0.9f => Color.Success,
        >= 0.7f => Color.Info,
        >= 0.5f => Color.Warning,
        _ => Color.Error
    };

    private string GetConfidenceLabel(float confidence) => confidence switch
    {
        >= 0.9f => "High confidence",
        >= 0.7f => "Medium confidence",
        >= 0.5f => "Low confidence",
        _ => "Very uncertain"
    };
}
```

### AI Transparency

Always show what the AI did:

```razor
<MudExpansionPanel Text="How did AI generate this?">
    <MudText Typo="Typo.body2">
        <strong>Model:</strong> GPT-4<br/>
        <strong>Prompt:</strong> "Analyze shapefile and suggest layer metadata..."<br/>
        <strong>Data analyzed:</strong> 1,247 features, 5 attributes<br/>
        <strong>Confidence:</strong> 92%<br/>
        <strong>Processing time:</strong> 2.3 seconds<br/>
        <strong>Cost:</strong> $0.02
    </MudText>
</MudExpansionPanel>
```

---

## Configuration

### appsettings.json

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "AzureOpenAI",  // or "Anthropic", "Ollama"
    "Models": {
      "Chat": "gpt-4",
      "Embeddings": "text-embedding-ada-002",
      "Analysis": "gpt-4"
    },
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "{{vault:ai-api-key}}",  // From Key Vault
      "DeploymentName": "gpt-4"
    },
    "RateLimits": {
      "RequestsPerMinute": 20,
      "TokensPerDay": 100000
    },
    "CostLimits": {
      "MonthlyBudget": 500.00,
      "PerUserMonthlyLimit": 10.00
    },
    "Features": {
      "NaturalLanguageSearch": true,
      "MetadataGeneration": true,
      "ConfigurationAssistant": true,
      "ErrorDetection": true,
      "StyleGeneration": true
    }
  }
}
```

---

## Implementation Roadmap

### Phase 1 (Weeks 1-2)
- [ ] Add AI admin endpoints to Admin API
- [ ] Implement natural language search
- [ ] Add AI chat component to UI
- [ ] Integrate with existing Semantic Kernel setup

### Phase 2 (Weeks 3-4)
- [ ] Metadata generation from uploaded files
- [ ] Configuration assistant (chat-based help)
- [ ] Cost tracking and quota management

### Phase 3 (Weeks 5-6)
- [ ] Intelligent error detection
- [ ] Style generation
- [ ] Query optimization suggestions

### Phase 4 (Weeks 7-8)
- [ ] Bulk operations assistant
- [ ] Usage prediction
- [ ] Automated documentation generation

---

## Pricing Estimate

**Assumptions:**
- 10 active admins
- 50 AI requests/admin/day
- Average 500 tokens input, 1000 tokens output per request
- Using GPT-4 Turbo

**Monthly Cost:**
```
10 admins × 50 requests/day × 30 days = 15,000 requests/month

Input:  15,000 × 500 tokens × $0.01/1K = $75
Output: 15,000 × 1000 tokens × $0.03/1K = $450

Total: ~$525/month
```

**Cost Reduction Strategies:**
- Use GPT-3.5-turbo for simple queries → ~$50/month (90% reduction)
- Use Ollama for local/free inference → $0/month
- Cache common queries → 30-50% reduction
- Implement user quotas → Control costs per user

---

## References

- [Microsoft Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Azure OpenAI Best Practices](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/system-message)
- [Anthropic Claude API](https://docs.anthropic.com/claude/reference/getting-started-with-the-api)

---

**Next Steps:**

Want me to start implementing Phase 1 (AI search + chat component)?
