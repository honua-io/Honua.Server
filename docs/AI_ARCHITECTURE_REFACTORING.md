# AI Architecture Refactoring - Extract Shared Core

**Date:** 2025-11-03
**Status:** Proposed Architecture
**Issue:** Admin UI needs AI services, but they're currently in CLI-specific project

---

## Problem Statement

The Admin UI needs to reference AI functionality for:
- Natural language metadata search ("Find all WMS services without caching")
- AI-assisted organization (auto-group services by keywords)
- Metadata generation (create abstracts, keywords)
- Diagnostics assistance ("Why isn't this service working?")
- Smart suggestions (health check recommendations)

**Current Architecture:**
```
Honua.Cli.AI/
  ├─ Services/AI/             ← LLM providers (OpenAI, Azure, Anthropic, etc.)
  ├─ Services/Plugins/        ← MetadataPlugin, DiagnosticsPlugin, etc.
  ├─ Services/Execution/      ← CLI-specific (Terraform, Docker execution)
  └─ Services/VectorSearch/   ← Vector search providers
```

**Problem:** Admin UI cannot reference `Honua.Cli.AI` (layering violation - UI shouldn't depend on CLI).

---

## Proposed Solution: Extract AI Core

Create a new shared library `Honua.AI.Core` that contains reusable AI services.

### New Project Structure

```
Honua.AI.Core/                          ← NEW: Shared AI functionality
  ├─ Providers/
  │   ├─ ILlmProvider.cs               ← LLM abstraction
  │   ├─ IEmbeddingProvider.cs         ← Embedding abstraction
  │   ├─ ILlmProviderRouter.cs         ← Smart routing (cost/latency)
  │   ├─ OpenAI/
  │   │   ├─ OpenAILlmProvider.cs      ← OpenAI implementation
  │   │   └─ OpenAIEmbeddingProvider.cs
  │   ├─ Azure/
  │   │   ├─ AzureOpenAILlmProvider.cs
  │   │   └─ AzureOpenAIEmbeddingProvider.cs
  │   ├─ Anthropic/
  │   │   ├─ AnthropicLlmProvider.cs
  │   │   └─ AnthropicEmbeddingProvider.cs
  │   ├─ Ollama/
  │   │   └─ OllamaLlmProvider.cs      ← Local LLM support
  │   └─ LocalAI/
  │       └─ LocalAILlmProvider.cs
  │
  ├─ Guards/
  │   ├─ IInputGuard.cs                ← Prevent prompt injection
  │   ├─ IOutputGuard.cs               ← Prevent data leakage
  │   ├─ LlmInputGuard.cs
  │   └─ LlmOutputGuard.cs
  │
  ├─ VectorSearch/
  │   ├─ IVectorSearchProvider.cs      ← Vector DB abstraction
  │   ├─ PostgresVectorSearchProvider.cs (uses pgvector)
  │   ├─ AzureVectorSearchProvider.cs   (uses Azure AI Search)
  │   └─ InMemoryVectorSearchProvider.cs (dev/testing)
  │
  ├─ Services/
  │   ├─ MetadataGenerationService.cs  ← Generate abstracts, keywords
  │   ├─ SemanticSearchService.cs      ← Natural language search
  │   ├─ OrganizationSuggestionService.cs ← Auto-organize services
  │   └─ DiagnosticsAssistantService.cs ← Troubleshooting help
  │
  ├─ Configuration/
  │   ├─ LlmProviderOptions.cs         ← Provider configuration
  │   └─ AIServiceCollectionExtensions.cs ← DI setup
  │
  └─ Models/
      ├─ ChatMessage.cs                ← Chat conversation model
      ├─ CompletionRequest.cs
      ├─ CompletionResponse.cs
      └─ EmbeddingResponse.cs

Honua.Cli.AI/                          ← REFACTORED: CLI-specific AI
  ├─ References: Honua.AI.Core         ← Uses shared core
  ├─ Services/Execution/               ← CLI-specific execution
  │   ├─ TerraformExecutionPlugin.cs
  │   ├─ DockerExecutionPlugin.cs
  │   └─ PlanExecutor.cs
  ├─ Services/Migration/               ← CLI-specific migration
  │   ├─ MigrationPlanner.cs
  │   └─ ArcGISServiceAnalyzer.cs
  └─ Services/Deployment/              ← CLI-specific deployment
      ├─ DeploymentGuardrailValidator.cs
      └─ CloudDeploymentPlugin.cs

Honua.Server.Host/                     ← Admin API endpoints
  ├─ References: Honua.AI.Core         ← Uses shared core
  ├─ Admin/
  │   └─ AIEndpoints.cs                ← NEW: AI endpoints
  │       ├─ POST /admin/ai/chat
  │       ├─ POST /admin/ai/search
  │       ├─ POST /admin/ai/generate-metadata
  │       ├─ POST /admin/ai/suggest-organization
  │       └─ POST /admin/ai/diagnose

Honua.Admin.UI/                        ← Blazor Admin UI
  ├─ References: None (calls REST API) ← No direct AI reference
  ├─ Components/
  │   └─ AIChatPanel.razor             ← Calls /admin/ai/* endpoints
  └─ Services/
      └─ AIClientService.cs            ← HttpClient wrapper
```

---

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────────┐
│ Presentation Layer                                              │
│                                                                  │
│ Honua.Admin.UI (Blazor)              Honua.Cli (Console)       │
│         │                                      │                │
│         └────────── HTTP ────────────┐         │                │
│                                       ↓         ↓                │
└───────────────────────────────────────┼─────────┼────────────────┘
                                        │         │
┌───────────────────────────────────────┼─────────┼────────────────┐
│ API Layer                             │         │                │
│                                       ↓         │                │
│ Honua.Server.Host/Admin/AIEndpoints.cs         │                │
│                                                 │                │
└─────────────────────────────────────────────────┼────────────────┘
                                                  │
┌─────────────────────────────────────────────────┼────────────────┐
│ Business Logic Layer                            │                │
│                                                 ↓                │
│         ┌────────────────────────────────────────────────┐      │
│         │       Honua.AI.Core (Shared)                   │      │
│         │  • LLM Providers                               │      │
│         │  • Embedding Providers                         │      │
│         │  • Vector Search                               │      │
│         │  • Metadata Generation                         │      │
│         │  • Semantic Search                             │      │
│         │  • Organization Suggestions                    │      │
│         │  • Diagnostics Assistant                       │      │
│         └────────────┬───────────────────────┬──────────┘      │
│                      │                       │                  │
│         ┌────────────┴──────────┐   ┌────────┴──────────┐      │
│         │ Honua.Cli.AI          │   │ (Future: Other    │      │
│         │ (CLI-specific)        │   │  consumers)       │      │
│         │ • Execution plugins   │   │                   │      │
│         │ • Migration tools     │   │                   │      │
│         │ • Deployment guards   │   │                   │      │
│         └───────────────────────┘   └───────────────────┘      │
└─────────────────────────────────────────────────────────────────┘
                                  │
┌─────────────────────────────────┼───────────────────────────────┐
│ External Services Layer         │                               │
│                                 ↓                               │
│  OpenAI API    Azure OpenAI    Anthropic    Ollama (local)     │
└─────────────────────────────────────────────────────────────────┘
```

---

## Admin UI → AI Service Flow

**Correct Architecture: UI calls REST API (bearer token auth)**

```
┌────────────────────────────────────────────────────────────────┐
│ 1. User types in AI chat                                       │
│    "Find all WMS services without caching"                     │
└────────────┬───────────────────────────────────────────────────┘
             │
             ↓
┌────────────────────────────────────────────────────────────────┐
│ 2. Blazor Component (AIChatPanel.razor)                        │
│    await AIClientService.SendChatMessageAsync(message)         │
└────────────┬───────────────────────────────────────────────────┘
             │
             ↓
┌────────────────────────────────────────────────────────────────┐
│ 3. HttpClient (with bearer token)                              │
│    POST /admin/ai/chat                                         │
│    Authorization: Bearer {oauth-token}                         │
│    { "message": "Find all WMS services without caching" }      │
└────────────┬───────────────────────────────────────────────────┘
             │
             ↓
┌────────────────────────────────────────────────────────────────┐
│ 4. Admin API Endpoint (AIEndpoints.cs)                         │
│    app.MapPost("/admin/ai/chat", HandleChatAsync)              │
│        .RequireAuthorization("RequireDataPublisher")           │
└────────────┬───────────────────────────────────────────────────┘
             │
             ↓
┌────────────────────────────────────────────────────────────────┐
│ 5. AI Service (Honua.AI.Core)                                  │
│    SemanticSearchService.SearchServicesAsync(query, user)      │
│    - Calls LLM provider                                        │
│    - Queries metadata provider                                 │
│    - Returns structured results                                │
└────────────┬───────────────────────────────────────────────────┘
             │
             ↓
┌────────────────────────────────────────────────────────────────┐
│ 6. Response back to UI                                         │
│    { "results": [ { "id": "...", "name": "..." } ] }          │
└────────────────────────────────────────────────────────────────┘
```

**Key Points:**
- ✅ Admin UI **never directly references** `Honua.AI.Core`
- ✅ All AI calls go through `/admin/ai/*` REST API endpoints
- ✅ Bearer token authentication on all requests
- ✅ API uses AI services on behalf of authenticated user
- ✅ Consistent with architecture (UI always uses HttpClient + SignalR)

---

## Migration Plan

### Phase 1: Create Honua.AI.Core Project

**Week 1: Extract Core Abstractions**

1. **Create new project:**
   ```bash
   dotnet new classlib -n Honua.AI.Core -o src/Honua.AI.Core
   ```

2. **Move core interfaces:**
   - `ILlmProvider.cs`
   - `IEmbeddingProvider.cs`
   - `ILlmProviderRouter.cs`
   - `IVectorSearchProvider.cs`
   - `IInputGuard.cs` / `IOutputGuard.cs`

3. **Move provider implementations:**
   - `Services/AI/Providers/*` → `Honua.AI.Core/Providers/`
   - Keep: OpenAI, Azure, Anthropic, Ollama, LocalAI
   - Remove CLI-specific wrappers

4. **Move guards:**
   - `Services/Guards/*` → `Honua.AI.Core/Guards/`

5. **Add dependencies:**
   ```xml
   <PackageReference Include="Azure.AI.OpenAI" />
   <PackageReference Include="OpenAI" />
   <PackageReference Include="Anthropic.SDK" />
   <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
   <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
   <PackageReference Include="Polly" /> <!-- For retry policies -->
   ```

**Week 2: Extract Vector Search**

6. **Move vector search providers:**
   - `Services/VectorSearch/*` → `Honua.AI.Core/VectorSearch/`
   - Keep provider-agnostic code
   - Update to use new namespace

**Week 3: Create Core Services**

7. **Create shared AI services:**

```csharp
// Honua.AI.Core/Services/ISemanticSearchService.cs
public interface ISemanticSearchService
{
    Task<SemanticSearchResult> SearchServicesAsync(
        string naturalLanguageQuery,
        MetadataSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

// Honua.AI.Core/Services/IMetadataGenerationService.cs
public interface IMetadataGenerationService
{
    Task<GeneratedMetadata> GenerateAbstractAsync(
        ServiceDefinition service,
        CancellationToken cancellationToken = default);

    Task<List<string>> GenerateKeywordsAsync(
        ServiceDefinition service,
        CancellationToken cancellationToken = default);
}

// Honua.AI.Core/Services/IOrganizationSuggestionService.cs
public interface IOrganizationSuggestionService
{
    Task<OrganizationSuggestion> SuggestFolderStructureAsync(
        List<ServiceDefinition> services,
        CancellationToken cancellationToken = default);
}

// Honua.AI.Core/Services/IDiagnosticsAssistantService.cs
public interface IDiagnosticsAssistantService
{
    Task<DiagnosticSuggestion> DiagnoseServiceIssueAsync(
        ServiceDefinition service,
        HealthCheckResult healthCheckResult,
        CancellationToken cancellationToken = default);
}
```

8. **Implement services:**
   - Extract relevant logic from existing plugins
   - Create focused, single-purpose services
   - Add unit tests

---

### Phase 2: Update Honua.Cli.AI to Reference Core

**Week 4: Refactor CLI Project**

9. **Add reference:**
   ```xml
   <ProjectReference Include="..\Honua.AI.Core\Honua.AI.Core.csproj" />
   ```

10. **Remove duplicated code:**
    - Delete `Services/AI/Providers/*` (now in Core)
    - Delete `Services/Guards/*` (now in Core)
    - Update using statements

11. **Update plugins to use Core services:**
    ```csharp
    // Before (Honua.Cli.AI)
    using Honua.Cli.AI.Services.AI;

    // After (Honua.Cli.AI)
    using Honua.AI.Core.Providers;
    using Honua.AI.Core.Services;
    ```

12. **Keep CLI-specific services:**
    - Execution plugins (Terraform, Docker, etc.)
    - Migration tools
    - Deployment guardrails
    - Plan executors

---

### Phase 3: Add AI Endpoints to Admin API

**Week 5: Create Admin AI Endpoints**

13. **Create endpoint file:**

```csharp
// Honua.Server.Host/Admin/AIEndpoints.cs
using Honua.AI.Core.Services;
using Honua.AI.Core.Providers;

public static class AIEndpoints
{
    public static RouteGroupBuilder MapAIEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/chat", HandleChatAsync)
            .RequireAuthorization("RequireDataPublisher")
            .RequireRateLimiting("ai-operations");

        group.MapPost("/search", HandleSemanticSearchAsync)
            .RequireAuthorization("RequireDataPublisher")
            .RequireRateLimiting("ai-operations");

        group.MapPost("/generate-metadata", HandleGenerateMetadataAsync)
            .RequireAuthorization("RequireDataPublisher")
            .RequireRateLimiting("ai-operations");

        group.MapPost("/suggest-organization", HandleSuggestOrganizationAsync)
            .RequireAuthorization("RequireAdministrator")
            .RequireRateLimiting("ai-operations");

        group.MapPost("/diagnose", HandleDiagnoseAsync)
            .RequireAuthorization("RequireDataPublisher")
            .RequireRateLimiting("ai-operations");

        return group;
    }

    private static async Task<IResult> HandleChatAsync(
        ChatRequest request,
        ISemanticSearchService searchService,
        IMetadataGenerationService metadataService,
        IDiagnosticsAssistantService diagnosticsService,
        IMutableMetadataProvider metadataProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Parse user intent from natural language
        var intent = await DetermineIntentAsync(request.Message);

        return intent switch
        {
            ChatIntent.Search => await HandleSearchIntentAsync(
                request.Message, searchService, metadataProvider, cancellationToken),

            ChatIntent.Diagnose => await HandleDiagnoseIntentAsync(
                request.Message, diagnosticsService, metadataProvider, cancellationToken),

            ChatIntent.Generate => await HandleGenerateIntentAsync(
                request.Message, metadataService, metadataProvider, cancellationToken),

            _ => Results.Ok(new ChatResponse
            {
                Message = "I'm not sure how to help with that. Try asking about finding services, diagnosing issues, or generating metadata."
            })
        };
    }

    private static async Task<IResult> HandleSemanticSearchAsync(
        SemanticSearchRequest request,
        ISemanticSearchService searchService,
        IMutableMetadataProvider metadataProvider,
        CancellationToken cancellationToken)
    {
        var snapshot = await metadataProvider.LoadAsync(cancellationToken);

        var result = await searchService.SearchServicesAsync(
            request.Query,
            snapshot,
            cancellationToken);

        return Results.Ok(new SemanticSearchResponse
        {
            Query = request.Query,
            Results = result.Matches.Select(m => new SearchResultItem
            {
                ServiceId = m.Service.Id,
                ServiceName = m.Service.Name,
                Relevance = m.Score,
                Explanation = m.Explanation
            }).ToList()
        });
    }

    private static async Task<IResult> HandleGenerateMetadataAsync(
        GenerateMetadataRequest request,
        IMetadataGenerationService metadataService,
        IMutableMetadataProvider metadataProvider,
        CancellationToken cancellationToken)
    {
        var snapshot = await metadataProvider.LoadAsync(cancellationToken);
        var service = snapshot.Services.FirstOrDefault(s => s.Id == request.ServiceId);

        if (service == null)
            return Results.NotFound();

        var metadata = await metadataService.GenerateAbstractAsync(service, cancellationToken);
        var keywords = await metadataService.GenerateKeywordsAsync(service, cancellationToken);

        return Results.Ok(new GeneratedMetadataResponse
        {
            Abstract = metadata.Abstract,
            Keywords = keywords,
            Purpose = metadata.Purpose
        });
    }

    private static async Task<IResult> HandleSuggestOrganizationAsync(
        OrganizationRequest request,
        IOrganizationSuggestionService organizationService,
        IMutableMetadataProvider metadataProvider,
        CancellationToken cancellationToken)
    {
        var snapshot = await metadataProvider.LoadAsync(cancellationToken);

        var services = request.ServiceIds?.Any() == true
            ? snapshot.Services.Where(s => request.ServiceIds.Contains(s.Id)).ToList()
            : snapshot.Services.ToList();

        var suggestion = await organizationService.SuggestFolderStructureAsync(
            services,
            cancellationToken);

        return Results.Ok(new OrganizationSuggestionResponse
        {
            Folders = suggestion.Folders.Select(f => new FolderSuggestion
            {
                Name = f.Name,
                ServiceIds = f.Services.Select(s => s.Id).ToList(),
                Rationale = f.Rationale
            }).ToList(),
            Confidence = suggestion.Confidence
        });
    }

    private static async Task<IResult> HandleDiagnoseAsync(
        DiagnoseRequest request,
        IDiagnosticsAssistantService diagnosticsService,
        IMetadataHealthCheckService healthCheckService,
        IMutableMetadataProvider metadataProvider,
        CancellationToken cancellationToken)
    {
        var snapshot = await metadataProvider.LoadAsync(cancellationToken);
        var service = snapshot.Services.FirstOrDefault(s => s.Id == request.ServiceId);

        if (service == null)
            return Results.NotFound();

        // Run health check if not provided
        var healthCheck = request.HealthCheckResult
            ?? await healthCheckService.CheckServiceHealthAsync(service);

        var diagnostic = await diagnosticsService.DiagnoseServiceIssueAsync(
            service,
            healthCheck,
            cancellationToken);

        return Results.Ok(new DiagnosticResponse
        {
            Issue = diagnostic.Issue,
            PossibleCauses = diagnostic.PossibleCauses,
            SuggestedFixes = diagnostic.SuggestedFixes.Select(f => new FixSuggestion
            {
                Description = f.Description,
                Steps = f.Steps,
                Confidence = f.Confidence
            }).ToList()
        });
    }
}
```

14. **Register endpoints:**

```csharp
// Honua.Server.Host/Program.cs
var adminGroup = app.MapGroup("/admin")
    .RequireAuthorization()
    .WithOpenApi();

var aiGroup = adminGroup.MapGroup("/ai")
    .WithTags("AI Assistant");

aiGroup.MapAIEndpoints();
```

15. **Add rate limiting for AI endpoints:**

```csharp
// Honua.Server.Host/Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ai-operations", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10; // 10 AI requests per minute per user
        opt.QueueLimit = 2;
    });
});
```

---

### Phase 4: Implement Admin UI AI Client

**Week 6: Create UI Components**

16. **Create AI client service:**

```csharp
// Honua.Admin.UI/Services/AIClientService.cs
public class AIClientService
{
    private readonly HttpClient _http;

    public AIClientService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ChatResponse> SendChatMessageAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/admin/ai/chat",
            new ChatRequest { Message = message },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
    }

    public async Task<SemanticSearchResponse> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/admin/ai/search",
            new SemanticSearchRequest { Query = query },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SemanticSearchResponse>(cancellationToken);
    }

    public async Task<GeneratedMetadataResponse> GenerateMetadataAsync(
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/admin/ai/generate-metadata",
            new GenerateMetadataRequest { ServiceId = serviceId },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GeneratedMetadataResponse>(cancellationToken);
    }

    public async Task<OrganizationSuggestionResponse> SuggestOrganizationAsync(
        List<string>? serviceIds = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/admin/ai/suggest-organization",
            new OrganizationRequest { ServiceIds = serviceIds },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrganizationSuggestionResponse>(cancellationToken);
    }

    public async Task<DiagnosticResponse> DiagnoseAsync(
        string serviceId,
        HealthCheckResult? healthCheckResult = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/admin/ai/diagnose",
            new DiagnoseRequest
            {
                ServiceId = serviceId,
                HealthCheckResult = healthCheckResult
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiagnosticResponse>(cancellationToken);
    }
}
```

17. **Register in DI:**

```csharp
// Honua.Admin.UI/Program.cs
builder.Services.AddScoped<AIClientService>();
```

18. **Update AI chat component:**

```razor
@* Honua.Admin.UI/Components/AIChatPanel.razor *@
@inject AIClientService AIClient
@inject ISnackbar Snackbar

<MudPaper Class="ai-chat-panel">
    <MudText Typo="Typo.h6">🤖 AI Assistant</MudText>

    <div class="chat-messages">
        @foreach (var message in _messages)
        {
            <div class="chat-message @message.Role">
                <MudText Typo="Typo.body2">@message.Content</MudText>
            </div>
        }

        @if (_isThinking)
        {
            <div class="chat-message assistant">
                <MudProgressCircular Size="Size.Small" Indeterminate="true" />
                <MudText Typo="Typo.caption">Thinking...</MudText>
            </div>
        }
    </div>

    <MudTextField @bind-Value="_currentMessage"
                  Placeholder="Ask me anything..."
                  Variant="Variant.Outlined"
                  Adornment="Adornment.End"
                  AdornmentIcon="@Icons.Material.Filled.Send"
                  OnAdornmentClick="SendMessageAsync"
                  @onkeydown="HandleKeyDown" />
</MudPaper>

@code {
    private List<ChatMessage> _messages = new();
    private string _currentMessage = "";
    private bool _isThinking;

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentMessage))
            return;

        // Add user message
        _messages.Add(new ChatMessage
        {
            Role = "user",
            Content = _currentMessage
        });

        var userMessage = _currentMessage;
        _currentMessage = "";
        _isThinking = true;

        try
        {
            // Call AI service via REST API (authenticated with bearer token)
            var response = await AIClient.SendChatMessageAsync(userMessage);

            // Add AI response
            _messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response.Message
            });
        }
        catch (Exception ex)
        {
            Snackbar.Add($"AI request failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isThinking = false;
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessageAsync();
        }
    }
}
```

---

## Benefits of This Architecture

### ✅ Clean Separation of Concerns

| Layer | Responsibility | References |
|-------|---------------|------------|
| **Honua.AI.Core** | Reusable AI services, provider abstractions | None (pure business logic) |
| **Honua.Cli.AI** | CLI-specific AI features (execution, migration) | `Honua.AI.Core` |
| **Honua.Server.Host** | Admin API endpoints, authentication | `Honua.AI.Core`, `Honua.Server.Core` |
| **Honua.Admin.UI** | Blazor UI components | None (calls REST API) |

### ✅ Reusability

- `Honua.AI.Core` can be used by:
  - Admin UI (via REST API)
  - CLI (direct reference)
  - Future: Mobile app, desktop app, public API
  - Future: Background jobs, scheduled tasks

### ✅ Testability

- Core AI services have no UI dependencies
- Can unit test in isolation
- Can mock LLM providers for deterministic tests

### ✅ Security

- Admin UI never directly calls LLM providers (no API keys in browser)
- All AI requests authenticated via bearer token
- Rate limiting per user to prevent abuse
- Input/output guards prevent prompt injection and data leakage

### ✅ Deployment Flexibility

- Can deploy Admin UI separately from AI services
- Can scale AI services independently
- Can use different LLM providers per environment (e.g., Ollama in dev, Azure OpenAI in prod)

---

## Configuration

**appsettings.json:**

```json
{
  "AI": {
    "DefaultProvider": "AzureOpenAI",
    "Providers": {
      "AzureOpenAI": {
        "Endpoint": "https://your-resource.openai.azure.com/",
        "DeploymentName": "gpt-4",
        "ApiKey": "vault://azure-openai-key",
        "MaxTokens": 2000,
        "Temperature": 0.7
      },
      "OpenAI": {
        "ApiKey": "vault://openai-key",
        "Model": "gpt-4-turbo-preview",
        "MaxTokens": 2000,
        "Temperature": 0.7
      },
      "Ollama": {
        "Endpoint": "http://localhost:11434",
        "Model": "llama3.1:8b",
        "MaxTokens": 2000
      }
    },
    "VectorSearch": {
      "Provider": "Postgres",
      "ConnectionString": "vault://postgres-connection",
      "EmbeddingDimensions": 1536
    },
    "RateLimits": {
      "RequestsPerMinute": 10,
      "TokensPerDay": 100000
    },
    "Guards": {
      "EnableInputGuard": true,
      "EnableOutputGuard": true,
      "MaxPromptLength": 4000
    }
  }
}
```

---

## Testing Strategy

### Unit Tests (Honua.AI.Core.Tests)

```csharp
public class SemanticSearchServiceTests
{
    [Fact]
    public async Task SearchServicesAsync_WithValidQuery_ReturnsRelevantResults()
    {
        // Arrange
        var mockLlmProvider = new Mock<ILlmProvider>();
        mockLlmProvider
            .Setup(x => x.CompleteAsync(It.IsAny<CompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompletionResponse
            {
                Text = "Find services with Type='WMS' and CachingEnabled=false"
            });

        var service = new SemanticSearchService(mockLlmProvider.Object);

        var snapshot = CreateTestSnapshot();

        // Act
        var result = await service.SearchServicesAsync(
            "Find all WMS services without caching",
            snapshot);

        // Assert
        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, m =>
        {
            Assert.Equal("WMS", m.Service.Type);
            Assert.False(m.Service.CachingEnabled);
        });
    }
}
```

### Integration Tests (Honua.Server.Host.Tests)

```csharp
public class AIEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ChatEndpoint_WithValidMessage_ReturnsResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetBearerTokenAsync(); // Authenticate
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/admin/ai/chat",
            new { message = "Find all unhealthy services" });

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task ChatEndpoint_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        // No auth header

        // Act
        var response = await client.PostAsJsonAsync("/admin/ai/chat",
            new { message = "test" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

---

## Migration Checklist

**Phase 1: Extract Core (Week 1-3)**
- [ ] Create `Honua.AI.Core` project
- [ ] Move `ILlmProvider` and implementations
- [ ] Move `IEmbeddingProvider` and implementations
- [ ] Move guards (`IInputGuard`, `IOutputGuard`)
- [ ] Move vector search providers
- [ ] Create shared services (MetadataGeneration, SemanticSearch, etc.)
- [ ] Add unit tests for core services
- [ ] Update namespaces and references

**Phase 2: Refactor CLI (Week 4)**
- [ ] Add reference to `Honua.AI.Core` in `Honua.Cli.AI`
- [ ] Remove duplicated code (providers, guards)
- [ ] Update using statements
- [ ] Test CLI commands still work
- [ ] Update CLI tests

**Phase 3: Add Admin API (Week 5)**
- [ ] Add reference to `Honua.AI.Core` in `Honua.Server.Host`
- [ ] Create `AIEndpoints.cs` with 5 endpoints
- [ ] Add rate limiting for AI endpoints
- [ ] Add bearer token authentication
- [ ] Add integration tests
- [ ] Update OpenAPI documentation

**Phase 4: Add Admin UI (Week 6)**
- [ ] Create `AIClientService` in Admin UI
- [ ] Update `AIChatPanel.razor` to use client service
- [ ] Add loading states and error handling
- [ ] Add UI tests (Playwright/bUnit)
- [ ] Update UX documentation

**Phase 5: Documentation & Launch (Week 7)**
- [ ] Update architecture docs
- [ ] Create AI configuration guide
- [ ] Document rate limits and quotas
- [ ] Create troubleshooting guide
- [ ] Launch to beta users

---

## Next Steps

1. **Review this proposal** - Get feedback from team
2. **Approve architecture** - Ensure alignment with goals
3. **Create Honua.AI.Core project** - Start extraction
4. **Incremental migration** - One service at a time
5. **Continuous testing** - Ensure nothing breaks

---

**End of Document**

*This refactoring enables the Admin UI to use AI services while maintaining clean architecture and proper separation of concerns.*
