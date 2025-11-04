# HonuaIO Admin UI Architecture

**Version:** 1.0
**Date:** 2025-11-03
**Status:** Proposed
**Target Framework:** .NET 9.0

---

## Executive Summary

This document outlines the architecture for the HonuaIO metadata administration UI, a Blazor-based interface for managing GIS services, layers, folders, data sources, and configuration. The architecture follows Microsoft's latest Blazor best practices (2025) and leverages existing HonuaIO infrastructure.

### Key Decisions

- **UI Framework**: Blazor Server with MudBlazor components
- **API Layer**: ASP.NET Core Minimal APIs (REST-first control plane)
- **State Management**: Scoped services with dependency injection
- **Data Access**: Existing `IMutableMetadataProvider` interface
- **Security**: Honua unified token strategy (OAuth/OIDC + RBAC) enforced via ASP.NET Core authorization
- **Performance**: Server-side rendering (SSR) for optimal performance

---

## Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     BLAZOR ADMIN UI                             │
│                  (Blazor Server + MudBlazor)                    │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │   Service    │  │    Layer     │  │    Folder    │        │
│  │  Management  │  │  Management  │  │  Management  │        │
│  └──────────────┘  └──────────────┘  └──────────────┘        │
│                                                                 │
│  Components use typed HttpClient + token handler for API calls │
└────────────┬────────────────────────────────────────────────────┘
             │ HTTPS
             │ /admin/metadata/*
             ↓
┌─────────────────────────────────────────────────────────────────┐
│                  ADMIN REST API ENDPOINTS                       │
│                  (ASP.NET Core Minimal APIs)                    │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  POST   /admin/metadata/services                         │  │
│  │  GET    /admin/metadata/services/{id}                    │  │
│  │  PUT    /admin/metadata/services/{id}                    │  │
│  │  DELETE /admin/metadata/services/{id}                    │  │
│  │                                                           │  │
│  │  POST   /admin/metadata/layers                           │  │
│  │  PUT    /admin/metadata/layers/{id}                      │  │
│  │  DELETE /admin/metadata/layers/{id}                      │  │
│  │                                                           │  │
│  │  GET    /admin/metadata/versions                         │  │
│  │  POST   /admin/metadata/versions                         │  │
│  │  POST   /admin/metadata/versions/{id}/restore            │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  [RequireAuthorization("RequireAdministrator")]                │
│  [EnableRateLimiting("admin-operations")]                      │
└────────────┬────────────────────────────────────────────────────┘
             │ Dependency Injection
             ↓
┌─────────────────────────────────────────────────────────────────┐
│              IMutableMetadataProvider (EXISTING)                │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  PostgresMetadataProvider                                │  │
│  │  - ACID transactions                                     │  │
│  │  - NOTIFY/LISTEN for real-time sync                      │  │
│  │  - Versioning and rollback                               │  │
│  │  - Audit trail                                           │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  Alternative: JsonMetadataProvider (file-based)                │
└────────────┬────────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────────┐
│                      POSTGRESQL DATABASE                        │
│                                                                 │
│  Tables: metadata_snapshots, metadata_change_log               │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component Architecture

### Project Structure

```
src/
├── Honua.Admin.Blazor/                  # Blazor Server UI
│   ├── Program.cs                       # App entry point, DI setup
│   ├── Features/
│   │   ├── Services/
│   │   │   ├── Pages/
│   │   │   │   ├── ServiceList.razor
│   │   │   │   ├── ServiceEditor.razor
│   │   │   │   └── ServiceWizard.razor
│   │   │   ├── Components/
│   │   │   │   ├── ServiceForm.razor
│   │   │   │   └── ServiceTree.razor
│   │   │   └── Models/
│   │   │       └── ServiceEditModel.cs
│   │   ├── Layers/
│   │   │   ├── Pages/
│   │   │   ├── Components/
│   │   │   └── Models/
│   │   ├── DataImport/
│   │   │   ├── Pages/
│   │   │   └── Components/
│   │   └── Shared/
│   │       ├── Components/
│   │       │   ├── FolderTreeView.razor
│   │       │   ├── MetadataEditor.razor
│   │       │   └── SecurityPanel.razor
│   │       └── Services/              # UI State Services
│   │           ├── NavigationState.cs
│   │           ├── EditorState.cs
│   │           └── NotificationService.cs
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── Breadcrumbs.razor
│   └── wwwroot/
│
└── Honua.Server.Host/                   # Existing API host
    └── Admin/
        ├── MetadataAdministrationEndpoints.cs  # NEW
        ├── DataIngestionEndpointRouteBuilderExtensions.cs  # Existing
        └── ...
```

---

## Microsoft Best Practices Compliance

### ✅ Render Mode Strategy

**Microsoft Recommendation:** Use Blazor Server-Side Rendering (SSR) as the default for performance-critical applications.

**Our Implementation:**
- Primary render mode: `InteractiveServer`
- Components are render-mode agnostic (set at usage site, not in component)
- Allows future migration to WebAssembly or hybrid if needed

```razor
@* Good: Set render mode at usage *@
<ServiceList @rendermode="InteractiveServer" />

@* Avoid: Setting in component definition *@
@* @rendermode InteractiveServer *@
```

### ✅ Component Design

**Microsoft Recommendation:** Keep components small and focused. Separate presentation from business logic.

**Our Implementation:**
- **Presentation Components**: Display data and trigger events (e.g., `ServiceForm.razor`)
- **Business Logic**: Centralized in backend API endpoints
- **State Services**: Scoped services for cross-component UI state

```razor
@* Presentation Component - No business logic *@
@code {
    [Parameter] public ServiceEditModel Model { get; set; } = new();
    [Parameter] public EventCallback<ServiceEditModel> OnSave { get; set; }

    private async Task HandleSubmit()
    {
        await OnSave.InvokeAsync(Model);  // Parent handles business logic
    }
}
```

### ✅ State Management

**Microsoft Recommendation:** Use dependency injection for state management. Choose appropriate service lifetime.

**Our Implementation:**

| State Type | Lifetime | Service | Purpose |
|------------|----------|---------|---------|
| **UI Navigation** | Scoped | `NavigationState` | Current folder, breadcrumbs |
| **Editor State** | Scoped | `EditorState` | Unsaved changes tracking |
| **Notifications** | Scoped | `NotificationService` | Toast messages |
| **Configuration** | Singleton | `IConfiguration` | App settings |

**Service Registration:**
```csharp
// Program.cs
builder.Services.AddScoped<NavigationState>();
builder.Services.AddScoped<EditorState>();
builder.Services.AddScoped<NotificationService>();
```

**Component Usage:**
```razor
@inject NavigationState NavState

@code {
    protected override void OnInitialized()
    {
        NavState.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        NavState.OnChange -= StateHasChanged;
    }
}
```

### ✅ Security Best Practices

**Microsoft Recommendations:**
- Use ASP.NET Core authentication/authorization
- Never store credentials in client-side code
- Always use HTTPS
- Client-side code can be tampered with—don't trust it for security

**Our Implementation:**

#### Authentication
- Honua authentication modes (OIDC, SAML via token exchange, Local, QuickStart) with unified token strategy
- JWT access tokens scoped to `honua-control-plane`
- HttpOnly secure cookies for Blazor circuits; encrypted server/session storage for detached deployments

#### Authorization
```csharp
// API Endpoints
app.MapGroup("/admin/metadata")
    .RequireAuthorization("RequireAdministrator")  // Policy-based
    .RequireRateLimiting("admin-operations");

// Blazor Pages
@attribute [Authorize(Roles = "administrator")]
```

#### Security Policies
```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministrator", policy =>
        policy.RequireRole("administrator"));

    options.AddPolicy("RequireDataPublisher", policy =>
        policy.RequireRole("administrator", "datapublisher"));
});
```

#### Anti-Forgery Protection

CSRF protection strategy depends on how the API is accessed:

| Scenario | CSRF Protection | Rationale |
|----------|----------------|-----------|
| **API called from CLI/scripts** | ❌ Not needed | Bearer token in `Authorization` header (not cookie-based) |
| **API called from Blazor Server (HttpClient)** | ❌ Not needed | Bearer token in header via delegating handler |
| **API called from Blazor WASM** | ❌ Not needed (if using bearer tokens) | Token in header, not cookie |
| **API called from browser form POST** | ✅ Required | Cookie-based auth vulnerable to CSRF |
| **Public endpoints (if any)** | ✅ Required | Forms submitted by users |

**Admin API Implementation:**

```csharp
// Admin metadata endpoints - No CSRF needed (bearer token auth)
group.MapPost("/admin/metadata/services", CreateService)
    .RequireAuthorization("RequireAdministrator")
    .DisableAntiforgery();  // Explicit: bearer token, not cookie-based

// If you have form-based endpoints (rare in admin API)
group.MapPost("/admin/forms/contact", SubmitContactForm)
    .RequireAntiforgery();  // Cookie-based auth
```

**Why bearer tokens don't need CSRF protection:**
- CSRF attacks rely on cookies being sent automatically by browsers
- Bearer tokens in `Authorization` headers must be explicitly added by JavaScript
- An attacker's site cannot read or send tokens from your domain (Same-Origin Policy)

**Exception:** If you use cookie-based session auth instead of bearer tokens, you MUST enable anti-forgery tokens.

### ✅ Performance Optimization

**Microsoft Recommendations:**
- Minimize re-renders with `ShouldRender()`
- Use `@key` for collection rendering
- Virtualize large lists
- Dispose of event handlers and timers

**Our Implementation:**

#### Virtualization for Large Lists
```razor
<MudDataGrid Items="@_services" Virtualize="true" Height="600px">
    @* Renders only visible rows *@
</MudDataGrid>
```

#### Component Key Usage
```razor
@foreach (var service in services)
{
    <ServiceCard @key="service.Id" Service="@service" />
}
```

#### Lifecycle Optimization
```csharp
@implements IDisposable

protected override bool ShouldRender()
{
    // Only re-render when model changes
    return _modelChanged;
}

public void Dispose()
{
    NavState.OnChange -= StateHasChanged;
    _httpClient?.Dispose();
}
```

### ✅ Dependency Injection Best Practices

**Microsoft Recommendations:**
- Use constructor injection in services
- Use `@inject` in components
- Avoid `IServiceProvider` lookups
- Choose appropriate service lifetimes

**Our Implementation:**

```csharp
// State Service
public class NavigationState
{
    private readonly ILogger<NavigationState> _logger;

    public NavigationState(ILogger<NavigationState> logger)
    {
        _logger = logger;
    }

    // ... state logic
}

// Component
@inject NavigationState NavState
@inject HttpClient AdminApiClient  // Configured with bearer-token delegating handler
@inject ISnackbar Snackbar
```

### ✅ HttpClient Configuration

#### Combined Deployment (Blazor Server + API same process)

```csharp
// Program.cs - Admin UI
builder.Services.AddHttpContextAccessor();

// Register named HttpClient for Admin API
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:5001");  // Base URL
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<BearerTokenDelegatingHandler>();

// Register delegating handler
builder.Services.AddScoped<BearerTokenDelegatingHandler>();
builder.Services.AddScoped<ITokenRefreshService, TokenRefreshService>();

// Optionally provide a pre-configured HttpClient for easier injection
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("AdminApi");
});
```

**Usage in Blazor components:**

```razor
@inject IHttpClientFactory HttpClientFactory

@code {
    private async Task LoadServicesAsync()
    {
        var client = HttpClientFactory.CreateClient("AdminApi");

        // Token automatically attached by delegating handler
        var response = await client.GetAsync("/admin/metadata/services");

        if (response.IsSuccessStatusCode)
        {
            _services = await response.Content
                .ReadFromJsonAsync<List<ServiceDto>>();
        }
    }
}
```

**Or inject pre-configured client directly:**

```razor
@inject HttpClient Http

@code {
    private async Task LoadServicesAsync()
    {
        // Token automatically attached
        _services = await Http.GetFromJsonAsync<List<ServiceDto>>(
            "/admin/metadata/services");
    }
}
```

#### Detached Deployment (Blazor WASM or separate server)

```csharp
// Program.cs - Blazor WASM
builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();

builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
})
.AddHttpMessageHandler<AccessTokenDelegatingHandler>();

builder.Services.AddScoped<AccessTokenDelegatingHandler>();

// Pre-configured client
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("AdminApi");
});
```

**Usage (same as combined):**

```razor
@inject HttpClient Http

@code {
    private async Task LoadServicesAsync()
    {
        // Token automatically attached by AccessTokenDelegatingHandler
        _services = await Http.GetFromJsonAsync<List<ServiceDto>>(
            "/admin/metadata/services");
    }
}
```

---

## API Design

### RESTful Endpoint Structure

```
GET    /admin/metadata/services              # List all services
POST   /admin/metadata/services              # Create service
GET    /admin/metadata/services/{id}         # Get service by ID
PUT    /admin/metadata/services/{id}         # Update service
DELETE /admin/metadata/services/{id}         # Delete service

GET    /admin/metadata/services/{id}/layers  # List layers for service
POST   /admin/metadata/services/{id}/layers  # Add layer to service
PUT    /admin/metadata/layers/{id}           # Update layer
DELETE /admin/metadata/layers/{id}           # Delete layer

GET    /admin/metadata/folders               # List folders
POST   /admin/metadata/folders               # Create folder
PUT    /admin/metadata/folders/{id}          # Update folder
DELETE /admin/metadata/folders/{id}          # Delete folder

GET    /admin/metadata/versions              # List metadata versions
POST   /admin/metadata/versions              # Create snapshot version
POST   /admin/metadata/versions/{id}/restore # Restore version
```

### Request/Response Models

```csharp
// Create Service Request
public record CreateServiceRequest(
    string Id,
    string Name,
    string? FolderId,
    string ServiceType,
    bool CachingEnabled,
    int? CacheTtlSeconds,
    Dictionary<string, string>? Metadata
);

// Create Service Response
public record ServiceResponse(
    string Id,
    string Name,
    string? FolderId,
    string ServiceType,
    bool CachingEnabled,
    int? CacheTtlSeconds,
    List<LayerResponse> Layers,
    DateTime CreatedAt,
    DateTime? ModifiedAt
);

// Error Response (RFC 7807 Problem Details)
public record ProblemDetails(
    string Type,
    string Title,
    int Status,
    string Detail,
    Dictionary<string, object>? Extensions
);
```

### API Implementation Pattern

```csharp
private static async Task<IResult> CreateService(
    CreateServiceRequest request,
    IMutableMetadataProvider metadataProvider,
    ILogger<MetadataAdministrationEndpoints> logger,
    CancellationToken ct)
{
    try
    {
        // 1. Load current snapshot
        var snapshot = await metadataProvider.LoadAsync(ct);

        // 2. Validate request
        if (snapshot.Services.Any(s => s.Id == request.Id))
        {
            return Results.Problem(
                title: "Service already exists",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"Service with ID '{request.Id}' already exists"
            );
        }

        // 3. Map to domain model
        var newService = new ServiceDefinition
        {
            Id = request.Id,
            Name = request.Name,
            FolderId = request.FolderId,
            ServiceType = request.ServiceType,
            // ... other properties
        };

        // 4. Build new snapshot (immutable update)
        var newSnapshot = new MetadataSnapshot(
            snapshot.Catalog,
            snapshot.Folders,
            snapshot.DataSources,
            snapshot.Services.Append(newService).ToList(),
            snapshot.Layers,
            snapshot.RasterDatasets,
            snapshot.Styles,
            snapshot.Server
        );

        // 5. Save atomically (Postgres transaction)
        await metadataProvider.SaveAsync(newSnapshot, ct);

        // 6. Log and return
        logger.LogInformation(
            "Created service {ServiceId} by {UserId}",
            newService.Id,
            "current-user-id");  // Get from ClaimsPrincipal

        return Results.Created(
            $"/admin/metadata/services/{newService.Id}",
            MapToResponse(newService)
        );
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create service");
        return Results.Problem(
            title: "Internal server error",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
}
```

---

## UI State Management

### NavigationState Service

Manages current folder navigation and breadcrumbs.

```csharp
public class NavigationState
{
    private string? _currentFolderId;
    private readonly List<BreadcrumbItem> _breadcrumbs = new();

    public event Action? OnChange;

    public string? CurrentFolderId => _currentFolderId;
    public IReadOnlyList<BreadcrumbItem> Breadcrumbs => _breadcrumbs;

    public void NavigateToFolder(string? folderId, string? folderName = null)
    {
        _currentFolderId = folderId;

        if (folderId is not null && folderName is not null)
        {
            _breadcrumbs.Add(new BreadcrumbItem(folderName, $"/folders/{folderId}"));
        }
        else
        {
            _breadcrumbs.Clear();
        }

        NotifyStateChanged();
    }

    public void NavigateUp()
    {
        if (_breadcrumbs.Count > 0)
        {
            _breadcrumbs.RemoveAt(_breadcrumbs.Count - 1);
            _currentFolderId = _breadcrumbs.LastOrDefault()?.Href?.Split('/').Last();
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

### EditorState Service

Tracks unsaved changes across form components.

```csharp
public class EditorState
{
    private readonly Dictionary<string, bool> _unsavedChanges = new();

    public event Action? OnChange;

    public bool HasUnsavedChanges(string editorId)
        => _unsavedChanges.GetValueOrDefault(editorId, false);

    public void MarkDirty(string editorId)
    {
        _unsavedChanges[editorId] = true;
        OnChange?.Invoke();
    }

    public void MarkClean(string editorId)
    {
        _unsavedChanges.Remove(editorId);
        OnChange?.Invoke();
    }

    public async Task<bool> ConfirmNavigationAsync(IDialogService dialogService)
    {
        if (!_unsavedChanges.Any())
            return true;

        var parameters = new DialogParameters
        {
            { "Message", "You have unsaved changes. Are you sure you want to leave?" }
        };

        var dialog = await dialogService.ShowAsync<ConfirmDialog>("Unsaved Changes", parameters);
        var result = await dialog.Result;

        return !result.Canceled;
    }
}
```

---

## Security Architecture

### Authentication Flow (Unified Token Strategy)

```
┌─────────────────────────────────────────────────────────────┐
│ 1. User navigates to /admin                                 │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. Blazor checks [Authorize] attribute                      │
│    → User not authenticated                                 │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. Redirect to /login                                       │
│    (OIDC login, Local login form, or external SAML IdP)     │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. User authenticates                                       │
│    - OIDC: Authorization Code + PKCE (confidential client)  │
│    - SAML: Assertion exchanged for Honua access token       │
│    - Local: POST /auth/token (username/password)            │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. Honua issues short-lived access token                    │
│    - HttpOnly cookie (co-hosted InteractiveServer)          │
│    - Server-side session or vault (detached deployment)     │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ 6. Blazor circuit established with authenticated ClaimsPrincipal│
│    - User claims populated from JWT                         │
│    - Roles: "administrator", "datapublisher", "viewer"      │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ 7. HttpClient attaches Bearer token via delegating handler  │
│    Authorization: Bearer {token}                            │
└─────────────────────────────────────────────────────────────┘
```

### Token Strategy

- Honua control plane authentication follows ADR [0010-jwt-oidc-authentication](architecture/decisions/0010-jwt-oidc-authentication.md) and platform guidance in [SECURITY.md](../SECURITY.md).
- The Admin UI and CLI always present OAuth 2.1 bearer tokens scoped to `honua-control-plane`, regardless of the upstream protocol.
- **OIDC deployments**: The UI acts as a confidential client using Authorization Code + PKCE. The CLI prefers the Device Code flow, with Client Credentials for service accounts.
- **SAML deployments**: (Phase 2) The UI exchanges the SAML assertion for a JWT via the identity provider's token endpoint or a Honua token broker (`/auth/exchange-saml`). Downstream API calls remain JWT-based.
- **Local deployments**: User/password POST to `/auth/token` mints the same JWT/refresh pair that the REST API validates; refresh tokens are stored in secure cookies (UI) or OS secrets storage (CLI).
- **QuickStart**: (Development only) API keys remain available for development; token exchange via `/auth/exchange-api-key` planned for Phase 3.
- Combined deployments configure `HttpClient` via `IHttpClientFactory` and a delegating handler that reads the current user's token from `HttpContext`. Detached deployments use `IAccessTokenProvider` to attach tokens obtained during login.

### Token Lifecycle

#### Access Token
- **Lifetime:** 15 minutes (configurable via `AccessTokenLifetime`)
- **Storage:**
  - Combined deployment: HttpOnly + Secure cookie
  - Detached deployment: Memory with IndexedDB backup (encrypted)
- **Renewal:** Automatic via refresh token before API calls (transparent to user)
- **Revocation:** On logout, role change, or via `/auth/revoke` endpoint

#### Refresh Token
- **Lifetime:** 7 days (configurable via `RefreshTokenLifetime`)
- **Storage:**
  - Combined deployment: HttpOnly + Secure cookie with SameSite=Strict
  - Detached deployment: Encrypted IndexedDB or secure session storage
- **Rotation:** One-time use (new refresh token issued on each refresh - prevents replay attacks)
- **Revocation:** On logout, password change, or admin action via `/auth/revoke-refresh`

#### Token Validation (API Side)

```csharp
// Program.cs - API token validation
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = "honua-control-plane";  // Validate scope
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            RoleClaimType = "role"  // Map to ASP.NET Core roles
        };
    });

// Authorization policies with scope validation
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdministrator", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("administrator");
        policy.RequireClaim("scope", "honua-control-plane");  // Validate scope
    });

    options.AddPolicy("RequireDataPublisher", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("administrator", "datapublisher");
        policy.RequireClaim("scope", "honua-control-plane");
    });
});
```

### Token Refresh Flow

#### Combined Deployment (Blazor Server + API same process)

When access token expires, the delegating handler automatically refreshes:

```csharp
// BearerTokenDelegatingHandler.cs
public class BearerTokenDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenRefreshService _tokenRefreshService;
    private readonly ILogger<BearerTokenDelegatingHandler> _logger;

    public BearerTokenDelegatingHandler(
        IHttpContextAccessor httpContextAccessor,
        ITokenRefreshService tokenRefreshService,
        ILogger<BearerTokenDelegatingHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenRefreshService = tokenRefreshService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Get access token from HttpContext (server-side)
        var accessToken = await httpContext.GetTokenAsync("access_token");

        // Check if token is expired or will expire soon (within 1 minute)
        if (IsTokenExpiredOrExpiring(accessToken))
        {
            _logger.LogDebug("Access token expired/expiring, refreshing...");

            // Refresh token (uses refresh token from cookie)
            var newAccessToken = await _tokenRefreshService.RefreshAccessTokenAsync(cancellationToken);

            if (newAccessToken != null)
            {
                accessToken = newAccessToken;

                // Update HttpContext with new token
                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    httpContext.User,
                    new AuthenticationProperties
                    {
                        UpdateIssuedUtc = true,
                        StoreTokens = new[]
                        {
                            new AuthenticationToken
                            {
                                Name = "access_token",
                                Value = accessToken
                            }
                        }
                    });
            }
            else
            {
                // Refresh failed - user needs to re-authenticate
                _logger.LogWarning("Token refresh failed, user needs to re-login");
                throw new UnauthorizedAccessException("Session expired");
            }
        }

        // Attach token to outgoing request
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken);
    }

    private bool IsTokenExpiredOrExpiring(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return true;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Refresh if token expires within 1 minute
            return jwtToken.ValidTo <= DateTime.UtcNow.AddMinutes(1);
        }
        catch
        {
            return true;
        }
    }
}

// TokenRefreshService.cs
public interface ITokenRefreshService
{
    Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken = default);
}

public class TokenRefreshService : ITokenRefreshService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public async Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var refreshToken = await httpContext!.GetTokenAsync("refresh_token");

        if (string.IsNullOrEmpty(refreshToken))
            return null;

        // Call token endpoint to refresh
        var response = await _httpClient.PostAsync(
            "/auth/refresh",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return tokenResponse?.AccessToken;
    }
}
```

#### Detached Deployment (Blazor WASM or separate server)

```csharp
// AccessTokenDelegatingHandler.cs
public class AccessTokenDelegatingHandler : DelegatingHandler
{
    private readonly IAccessTokenProvider _tokenProvider;

    public AccessTokenDelegatingHandler(IAccessTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestRequest request,
        CancellationToken cancellationToken)
    {
        // Get token (handles refresh internally)
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

// IAccessTokenProvider implementation
public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public class AccessTokenProvider : IAccessTokenProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    private string? _cachedToken;
    private DateTime _tokenExpiry;

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Return cached token if still valid (with 1-minute buffer)
        if (!string.IsNullOrEmpty(_cachedToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(1))
        {
            return _cachedToken;
        }

        // Try to refresh
        var refreshToken = await _jsRuntime.InvokeAsync<string>(
            "localStorage.getItem",
            "refresh_token");

        if (string.IsNullOrEmpty(refreshToken))
        {
            // No refresh token - user needs to login
            return null;
        }

        var response = await _httpClient.PostAsync("/auth/refresh",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        if (tokenResponse != null)
        {
            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            // Store new refresh token if rotated
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                await _jsRuntime.InvokeVoidAsync(
                    "localStorage.setItem",
                    "refresh_token",
                    tokenResponse.RefreshToken);
            }
        }

        return _cachedToken;
    }
}

public record TokenResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string TokenType
);
```

### Authorization Policies (Existing HonuaIO Policies)

```csharp
// EXISTING POLICIES - Already defined in AuthenticationExtensions.cs
// These are already configured in HonuaIO

// RequireAdministrator: Full access to all metadata operations
// - Role: "administrator"
// - Use for: Create/edit/delete services, layers, folders
group.MapPost("/admin/metadata/services", CreateService)
    .RequireAuthorization("RequireAdministrator");

// RequireDataPublisher: Can create/edit services and layers
// - Roles: "administrator", "datapublisher"
// - Use for: Data import, layer configuration
group.MapPost("/admin/metadata/layers", CreateLayer)
    .RequireAuthorization("RequireDataPublisher");

// RequireViewer: Read-only access
// - Roles: "administrator", "datapublisher", "viewer"
// - Use for: Viewing metadata, browsing services
group.MapGet("/admin/metadata/services", GetServices)
    .RequireAuthorization("RequireViewer");

// Note: HonuaIO supports both enforced and permissive auth modes
// In QuickStart mode (Enforce=false), unauthenticated users are allowed
// In production mode (Enforce=true), authentication is required
```

### CORS Configuration (if Blazor WASM in future)

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminUI", policy =>
    {
        policy.WithOrigins("https://admin.honua.io")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();  // For authentication cookies
    });
});

app.UseCors("AdminUI");
```

---

## Real-Time Updates Architecture

### Architecture Decision: SignalR Hub for All Deployments

**Key Decision:** The Admin UI uses a dedicated `MetadataChangeNotificationHub` (SignalR) for real-time updates, even though Blazor Server already uses SignalR for component rendering.

**Why a separate hub?**

1. **Detached Deployment Support**: Admin UI and Admin API can be deployed separately. The UI cannot directly inject `IMutableMetadataProvider` in this scenario.
2. **Consistent Architecture**: Same code works in both combined and detached deployments (deployment-mode agnostic).
3. **OAuth/SAML Compatibility**: REST API calls use bearer tokens. Direct provider injection bypasses the authentication layer.
4. **Clean Separation**: Server subscribes to provider events (Postgres NOTIFY, Redis Pub/Sub), broadcasts to UI clients via SignalR.

**Flow:**
```
Provider Event (Postgres NOTIFY/Redis Pub/Sub)
        ↓
MetadataChangeNotificationHub (server-side)
        ↓
SignalR broadcast to all connected UI clients
        ↓
UI components call REST API to fetch updated data (with bearer token)
```

**Important:** UI components **never inject `IMutableMetadataProvider` directly**. They always use `HttpClient` (REST API) + `HubConnection` (SignalR notifications).

---

### Leveraging Existing PostgresMetadataProvider NOTIFY/LISTEN

**Good news:** PostgresMetadataProvider already has real-time change notifications built-in using PostgreSQL's NOTIFY/LISTEN!

```
┌─────────────────────────────────────────────────────────────┐
│ Admin User A: Edits service "my-service"                    │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ API: PUT /admin/metadata/services/my-service                │
│ → Calls metadataProvider.SaveAsync(newSnapshot)             │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ PostgresMetadataProvider:                                   │
│ 1. Begins transaction                                       │
│ 2. Inserts new snapshot into metadata_snapshots             │
│ 3. Trigger fires: metadata_change_trigger                   │
│ 4. NOTIFY sent to channel: honua_metadata_changes           │
│ 5. Transaction commits                                      │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ PostgresMetadataProvider (all instances listening):         │
│ - Receives NOTIFY payload with version_id                   │
│ - Raises MetadataChanged event                              │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ MetadataChangeNotificationHub (server-side):                │
│ - Subscribes to provider.MetadataChanged event              │
│ - Broadcasts to all connected UI clients via SignalR        │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ Blazor UI Components (Admin User B's browser):              │
│ - Receives SignalR notification                             │
│ - Calls REST API to fetch updated data (with bearer token)  │
│ - Calls StateHasChanged() to re-render                      │
└─────────────────────────────────────────────────────────────┘
```

### Implementation in Blazor Components

**IMPORTANT:** UI components use HttpClient (REST API) + SignalR hub (real-time notifications). They **never inject `IMutableMetadataProvider` directly** to ensure the architecture works in both combined and detached deployments.

```razor
@* ServiceList.razor *@
@inject HttpClient Http
@inject HubConnection MetadataHub
@implements IAsyncDisposable

<MudDataGrid Items="@_services" />

@code {
    private List<ServiceDto> _services = new();
    private bool _supportsRealTime;

    protected override async Task OnInitializedAsync()
    {
        // Query server capabilities via SignalR
        _supportsRealTime = await MetadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

        if (_supportsRealTime)
        {
            // Subscribe to SignalR notifications from server
            MetadataHub.On<MetadataChangedNotification>("MetadataChanged", HandleMetadataChanged);
        }

        await LoadServicesAsync();
    }

    private void HandleMetadataChanged(MetadataChangedNotification notification)
    {
        // Reload data when metadata changes
        InvokeAsync(async () =>
        {
            await LoadServicesAsync();
            StateHasChanged();  // Trigger UI re-render
        });
    }

    private async Task LoadServicesAsync()
    {
        // Call admin REST API (authenticated via bearer token)
        _services = await Http.GetFromJsonAsync<List<ServiceDto>>("/admin/metadata/services")
            ?? new List<ServiceDto>();
    }

    public async ValueTask DisposeAsync()
    {
        // Unsubscribe from SignalR notifications
        MetadataHub.Remove("MetadataChanged");
        await MetadataHub.DisposeAsync();
    }
}
```

### Real-Time Update Behavior

| Scenario | Behavior |
|----------|----------|
| **Single Admin** | Immediate feedback after save (standard) |
| **Multiple Admins (Same Server)** | All admins see changes within ~100ms via NOTIFY |
| **Multiple Admins (Different Servers)** | All admins see changes within ~1s via Postgres pub/sub |
| **Network Partition** | Changes applied when connection restored |
| **Admin Editing Same Resource** | Last write wins (optimistic concurrency) |

### Configuration

```json
// appsettings.json
{
  "Metadata": {
    "Provider": "postgres",
    "EnableNotifications": true,  // Enable real-time updates
    "NotificationChannel": "honua_metadata_changes"
  }
}
```

**Cost Analysis:**
- ✅ No additional infrastructure (PostgreSQL already required)
- ✅ SignalR hub reuses Blazor circuit (no separate WebSocket infrastructure)
- ✅ Very low latency (~100ms same server, ~1s cross-server)
- ✅ Minimal overhead (single LISTEN connection per server instance)
- ⚠️ Requires PostgresMetadataProvider or RedisMetadataProvider (not JsonMetadataProvider)

### Automatic Service Reload (Already Built-In!)

**Important:** This same NOTIFY/LISTEN mechanism also updates the **main Honua.Server.Host** service when metadata changes!

```
┌─────────────────────────────────────────────────────────────┐
│ Admin saves metadata change in Admin UI                     │
└────────────┬────────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────────┐
│ PostgreSQL NOTIFY fired                                     │
└────────────┬────────────────────────────────────────────────┘
             │
             ├─────────────────────────┬──────────────────────┐
             ↓                         ↓                      ↓
┌────────────────────────┐ ┌──────────────────────┐ ┌────────────────────┐
│ Honua.Server.Host      │ │ Admin API Server     │ │ Other Servers      │
│ (Public API)           │ │ (with SignalR Hub)   │ │                    │
│                        │ │                      │ │                    │
│ - Receives NOTIFY      │ │ - Receives NOTIFY    │ │ - Receive NOTIFY   │
│ - Reloads metadata     │ │ - Hub broadcasts to  │ │ - Reload metadata  │
│ - WMS/WFS updated!     │ │   UI clients         │ │ - Services updated │
└────────────────────────┘ └──────────┬───────────┘ └────────────────────┘
                                      │
                                      ↓ SignalR
                          ┌──────────────────────┐
                          │ Admin UI (Browser)   │
                          │ - Receives SignalR   │
                          │ - Calls REST API     │
                          │ - StateHasChanged    │
                          └──────────────────────┘
```

**This means:**
1. Admin creates a new WMS service in the Admin UI
2. PostgreSQL NOTIFY fires
3. **All running Honua.Server.Host instances immediately reload metadata**
4. New WMS service is **instantly available** to clients (no restart!)
5. Admin UI also updates to show the change

**Zero-downtime metadata updates** are already built into HonuaIO via PostgresMetadataProvider! 🎉

### Fallback for Other Databases

**See:** [ADMIN_UI_REALTIME_FALLBACK.md](./ADMIN_UI_REALTIME_FALLBACK.md) for complete details.

**Summary:**

| Provider | Real-Time Support | Admin UI Behavior |
|----------|------------------|------------------|
| **PostgreSQL** | ✅ NOTIFY/LISTEN | Real-time updates (~100ms) |
| **Redis** | ✅ Pub/Sub | Real-time updates (<100ms) |
| **SQL Server** | ⚠️ Polling | Events every 5-30s (configurable) |
| **JSON/YAML** | ⚠️ FileSystemWatcher | Real-time if `WatchForChanges=true` (single server only) |

**Admin UI Strategy:**

The server-side `MetadataChangeNotificationHub` detects provider capabilities and broadcasts accordingly:

```csharp
// Server: MetadataChangeNotificationHub.cs
public class MetadataChangeNotificationHub : Hub
{
    private readonly IMutableMetadataProvider _provider;

    public MetadataChangeNotificationHub(IMutableMetadataProvider provider)
    {
        _provider = provider;

        if (_provider.SupportsChangeNotifications)
        {
            // Subscribe to provider events (Postgres NOTIFY, Redis Pub/Sub, etc.)
            _provider.MetadataChanged += OnMetadataChanged;
        }
    }

    private async void OnMetadataChanged(object? sender, MetadataChangedEventArgs e)
    {
        // Broadcast to all connected UI clients
        await Clients.All.SendAsync("MetadataChanged", new
        {
            e.ChangeType,
            e.EntityType,
            e.EntityId,
            Timestamp = DateTime.UtcNow
        });
    }

    public Task<bool> GetSupportsRealTimeUpdates()
    {
        return Task.FromResult(_provider.SupportsChangeNotifications);
    }
}
```

**UI components query server capabilities:**
```csharp
// Client: ServiceList.razor
protected override async Task OnInitializedAsync()
{
    // Ask server if real-time updates are available
    _supportsRealTime = await MetadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

    if (_supportsRealTime)
    {
        // Subscribe to SignalR notifications
        MetadataHub.On<MetadataChangedNotification>("MetadataChanged", HandleMetadataChanged);
    }
    else
    {
        // Fallback: Show manual refresh button + optional polling
        ShowRefreshButton = true;
    }
}
```

**Hybrid UI Approach (Recommended):**
- PostgreSQL/Redis: Show "🟢 Live Updates" indicator
- SQL Server: Show "🟡 Auto-Refresh (5s)" indicator
- JSON/YAML without watch: Show "⚪ Manual Mode" + Refresh button
- Always provide manual refresh button as backup

---

## Multi-Tenancy Support

### Deployment Scenarios

#### Scenario 1: Single-Tenant (Primary Use Case - 95%)

**Example:** Customer deploys Honua for their organization.

```json
// metadata.json
{
  "catalog": {
    "id": "acme-corp",
    "title": "ACME Corporation GIS Services"
  },
  "services": [...],
  "layers": [...]
}
```

**Admin UI:**
- No tenant selector needed
- Direct access to single catalog
- Simplified navigation

#### Scenario 2: Multi-Tenant (Honua Demo Site - 5%)

**Example:** demo.honua.io hosts multiple demo catalogs.

```json
// Catalog 1: demo-gis.json
{
  "catalog": { "id": "demo-gis", "title": "Demo GIS Services" },
  "services": [...]
}

// Catalog 2: demo-ogc.json
{
  "catalog": { "id": "demo-ogc", "title": "OGC Compliance Demo" },
  "services": [...]
}
```

**Admin UI:**
- Tenant/catalog selector in top navigation
- Filter services/layers by catalog
- Separate metadata files or database partitioning

### Multi-Tenant Architecture Pattern

```
┌─────────────────────────────────────────────────────────────┐
│ Admin UI Navigation Bar                                     │
│  [Catalog Selector: Demo GIS ▼] [Services] [Layers]        │
└─────────────────────────────────────────────────────────────┘
                    │
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ TenantContext Service (Scoped)                              │
│  - CurrentCatalogId: "demo-gis"                             │
│  - CurrentCatalog: CatalogDefinition                        │
└─────────────────────────────────────────────────────────────┘
                    │
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ ServiceList Component                                       │
│  @inject TenantContext Tenant                               │
│                                                              │
│  var services = snapshot.Services                           │
│      .Where(s => s.CatalogId == Tenant.CurrentCatalogId)    │
└─────────────────────────────────────────────────────────────┘
```

### Multi-Tenant Implementation

```csharp
// TenantContext.cs - Scoped service
public class TenantContext
{
    private readonly HttpClient _http;
    private string? _currentCatalogId;

    public event Action? OnChange;

    public TenantContext(HttpClient http)
    {
        _http = http;
    }

    public string? CurrentCatalogId
    {
        get => _currentCatalogId;
        set
        {
            if (_currentCatalogId != value)
            {
                _currentCatalogId = value;
                OnChange?.Invoke();
            }
        }
    }

    public async Task<CatalogDefinition?> GetCurrentCatalogAsync()
    {
        // Call REST API (authenticated via bearer token)
        if (_currentCatalogId is not null)
        {
            // Get specific catalog
            return await _http.GetFromJsonAsync<CatalogDefinition>(
                $"/admin/metadata/catalogs/{_currentCatalogId}");
        }

        // Get default catalog
        return await _http.GetFromJsonAsync<CatalogDefinition>("/admin/metadata/catalogs/default");
    }
}
```

### Multi-Tenant Considerations

**Option A: Multiple Metadata Files (Simple)**
- Each catalog has its own metadata.json or PostgreSQL database
- Switch metadata provider based on selected tenant
- Simpler isolation

**Option B: Shared Metadata with Partitioning (Complex)**
- Single metadata store with `catalog_id` field on all entities
- Filter by catalog in queries
- Requires changes to MetadataSnapshot model
- Better for high tenant count

**Recommendation for Phase 1:**
- ✅ Support single-tenant primarily
- ✅ Add `TenantContext` service for future multi-tenant support
- ⏸️ Defer full multi-tenant implementation until needed for demo site
- ⏸️ Use Option A (multiple metadata files) when multi-tenant support is added

---

## Data Flow Examples

### Example 1: Creating a New Service

```
User fills ServiceForm.razor
         ↓
User clicks "Save"
         ↓
Component calls API: POST /admin/metadata/services
         ↓
API validates request (auth, input validation)
         ↓
API loads current MetadataSnapshot from PostgresMetadataProvider
         ↓
API creates new ServiceDefinition
         ↓
API builds new MetadataSnapshot (immutable update)
         ↓
API saves to Postgres (atomic transaction)
         ↓
Postgres NOTIFY triggers metadata change event
         ↓
PostgresMetadataProvider raises MetadataChanged event
         ↓
Runtime reloads metadata (hot-reload)
         ↓
API returns 201 Created with service details
         ↓
Blazor component shows success notification
         ↓
NavigationState navigates to service details page
```

### Example 2: Editing a Layer

```
User clicks layer in ServiceEditor
         ↓
Component loads layer: GET /admin/metadata/layers/{id}
         ↓
User modifies layer properties
         ↓
EditorState.MarkDirty("layer-editor")
         ↓
User clicks "Save"
         ↓
Component calls API: PUT /admin/metadata/layers/{id}
         ↓
API uses optimized UpdateLayerAsync() method
         ↓
PostgresMetadataProvider updates single layer (no full snapshot)
         ↓
API returns 200 OK
         ↓
EditorState.MarkClean("layer-editor")
         ↓
Component refreshes layer list
```

### Example 3: Restoring Metadata Version

```
User navigates to Versions page
         ↓
Component loads versions: GET /admin/metadata/versions
         ↓
User selects version and clicks "Restore"
         ↓
ConfirmDialog shows: "Restore to version X?"
         ↓
User confirms
         ↓
Component calls API: POST /admin/metadata/versions/{id}/restore
         ↓
API calls metadataProvider.RestoreVersionAsync()
         ↓
Postgres restores JSONB snapshot from metadata_snapshots table
         ↓
NOTIFY event triggers cluster-wide reload
         ↓
All server instances reload metadata
         ↓
API returns 200 OK
         ↓
Blazor shows success notification
         ↓
ServiceList refreshes to show restored state
```

---

## Error Handling

### API Error Responses

All errors follow RFC 7807 Problem Details standard:

```json
{
  "type": "https://docs.honua.io/errors/validation",
  "title": "Validation Error",
  "status": 400,
  "detail": "Service name must be alphanumeric",
  "errors": {
    "Name": ["Must be alphanumeric", "Maximum length is 100 characters"]
  }
}
```

### Blazor Error Handling

```razor
@code {
    private async Task HandleSaveAsync()
    {
        try
        {
            var response = await AdminApiClient.PostAsJsonAsync("/admin/metadata/services", _model);

            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add("Service created successfully", Severity.Success);
                NavManager.NavigateTo($"/services/{_model.Id}");
            }
            else
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                Snackbar.Add(problem?.Detail ?? "An error occurred", Severity.Error);
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Network error saving service");
            Snackbar.Add("Network error. Please try again.", Severity.Error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error saving service");
            Snackbar.Add("An unexpected error occurred", Severity.Error);
        }
    }
}
```

### Global Error Boundary

```razor
@* App.razor *@
<CascadingAuthenticationState>
    <ErrorBoundary>
        <ChildContent>
            <Router AppAssembly="@typeof(App).Assembly">
                @* ... routes ... *@
            </Router>
        </ChildContent>
        <ErrorContent Context="ex">
            <MudAlert Severity="Severity.Error">
                <MudText>An error occurred: @ex.Message</MudText>
                <MudButton OnClick="@(() => RecreateComponent())">Reload</MudButton>
            </MudAlert>
        </ErrorContent>
    </ErrorBoundary>
</CascadingAuthenticationState>
```

---

## Performance Considerations

### Connection Pooling

```csharp
// PostgresMetadataProvider uses Npgsql's built-in pooling
// Connection string configuration:
"ConnectionStrings:Postgres": "Host=...;Pooling=true;MinPoolSize=5;MaxPoolSize=100"
```

### Caching Strategy

**Metadata Snapshot Caching:**
- PostgresMetadataProvider maintains in-memory snapshot
- Invalidated on NOTIFY events
- No additional caching layer needed (JSONB queries are fast)

**API Response Caching:**
```csharp
// Read-only endpoints can use OutputCache
group.MapGet("/services/{id}", GetService)
    .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));
```

### Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("admin-operations", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});
```

---

## Deployment Architecture

### Deployment Model Decision Matrix

Choose the deployment model based on your requirements:

| Scenario | Deployment Model | Rationale |
|----------|-----------------|-----------|
| **Single organization, internal use** | Combined | Simpler - single process, shared auth |
| **Multi-tenant SaaS** | Detached | Scale admin UI separately from API |
| **High security requirements** | Detached | Admin UI on internal network, API public |
| **Small deployment (<1000 users)** | Combined | Lower operational complexity |
| **Large deployment (>1000 concurrent)** | Detached | Horizontal scaling of API independent of UI |
| **Development/staging** | Combined | Easier debugging, fewer moving parts |
| **Restricted admin access** | Detached | Admin UI behind VPN/firewall |
| **Public API + Private admin** | Detached | Different network security zones |

**Combined Deployment Pros:**
- ✅ Simpler authentication (shared `HttpContext`)
- ✅ No CORS configuration needed
- ✅ Single TLS certificate/domain
- ✅ Lower latency (no network hop between UI and API)
- ✅ Easier to debug (single process)
- ✅ Lower hosting cost (single container)

**Combined Deployment Cons:**
- ❌ Admin UI shares CPU/memory with public API
- ❌ Cannot scale admin and public API independently
- ❌ Admin UI exposed on same endpoint as public API
- ❌ Single point of failure for both admin and public

**Detached Deployment Pros:**
- ✅ Independent scaling (scale public API without affecting admin)
- ✅ Admin UI can be on separate network (VPN, internal only)
- ✅ Different authentication contexts (SSO for UI, API keys for services)
- ✅ Admin UI can be Blazor WASM (offline-capable, faster after initial load)
- ✅ Fault isolation (admin UI crash doesn't affect public API)
- ✅ Easier to enforce network policies (admin on 10.0.0.0/8, public on internet)

**Detached Deployment Cons:**
- ❌ More complex authentication (token management across boundaries)
- ❌ CORS configuration required
- ❌ Separate TLS certificates
- ❌ Higher hosting cost (multiple containers)
- ❌ Network latency between UI and API
- ❌ More complex deployment pipeline

**Recommendation:**
- **Start with Combined** for development and small deployments
- **Migrate to Detached** when you need independent scaling or network isolation
- **Plan for both** by using `IHttpClientFactory` from the start (easy migration path)

### Deployment Options

#### Option A: Combined Deployment (Recommended for simplicity)

```
┌─────────────────────────────────────────────────┐
│  Honua.Server.Host (single container)           │
│                                                 │
│  - Public API endpoints (/ogc, /wms, etc.)      │
│  - Admin API endpoints (/admin/metadata)        │
│  - Blazor Admin UI (/admin)                     │
└─────────────────────────────────────────────────┘
```

**Pros:**
- Simple deployment
- Single TLS certificate
- Shared authentication

**Cons:**
- Admin UI shares resources with public API
- Cannot scale independently

#### Option B: Separate Deployment (Recommended for production)

```
┌─────────────────────────────────────────────────┐
│  Honua.Server.Host (public-facing)              │
│  - Public API endpoints only                    │
│  - Horizontally scaled (multiple instances)     │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  Honua.Admin.Host (internal/VPN)                │
│  - Admin API endpoints                          │
│  - Blazor Admin UI                              │
│  - Single instance (or 2 for HA)                │
└─────────────────────────────────────────────────┘
```

**Pros:**
- Admin isolated from public traffic
- Independent scaling
- Better security (admin not internet-facing)

**Cons:**
- More complex deployment
- Separate TLS certificates

### Container Configuration

```dockerfile
# Dockerfile for Admin UI
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Honua.Admin.Blazor.dll"]
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-admin
spec:
  replicas: 2
  selector:
    matchLabels:
      app: honua-admin
  template:
    metadata:
      labels:
        app: honua-admin
    spec:
      containers:
      - name: admin-ui
        image: honua.azurecr.io/honua-admin:latest
        ports:
        - containerPort: 8080
        env:
        - name: ConnectionStrings__Postgres
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: postgres-connection
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: honua-admin-svc
spec:
  type: ClusterIP  # Internal only
  selector:
    app: honua-admin
  ports:
  - port: 80
    targetPort: 8080
```

---

## Testing Strategy

### Unit Tests

**API Endpoints:**
```csharp
public class MetadataAdministrationEndpointsTests
{
    [Fact]
    public async Task CreateService_ValidRequest_Returns201Created()
    {
        // Arrange
        var mockProvider = new Mock<IMutableMetadataProvider>();
        mockProvider.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestSnapshot());

        var request = new CreateServiceRequest(
            Id: "test-service",
            Name: "Test Service",
            FolderId: null,
            ServiceType: "WMS",
            CachingEnabled: false,
            CacheTtlSeconds: null,
            Metadata: null
        );

        // Act
        var result = await MetadataAdministrationEndpoints.CreateService(
            request,
            mockProvider.Object,
            Mock.Of<ILogger>(),
            CancellationToken.None
        );

        // Assert
        var createdResult = Assert.IsType<Created>(result);
        Assert.Equal("/admin/metadata/services/test-service", createdResult.Location);

        mockProvider.Verify(p => p.SaveAsync(
            It.IsAny<MetadataSnapshot>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
```

### Integration Tests

```csharp
public class MetadataApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MetadataApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateService_EndToEnd_Success()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateServiceRequest(...);

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/services", request);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        Assert.NotNull(service);
        Assert.Equal(request.Name, service.Name);
    }
}
```

### Blazor Component Tests (bUnit)

```csharp
public class ServiceListTests : TestContext
{
    [Fact]
    public void ServiceList_LoadsServices_DisplaysInTable()
    {
        // Arrange
        var mockHttp = Services.AddMockHttpClient();
        mockHttp.When("/admin/metadata/services")
            .RespondJson(new[] { new ServiceResponse(...) });

        // Act
        var cut = RenderComponent<ServiceList>();

        // Assert
        cut.WaitForElement("table");
        Assert.Contains("Test Service", cut.Markup);
    }
}
```

---

## Feature Roadmap by Phase

### Phase 1 - Core Admin UI (MVP)

**Timeline:** Weeks 1-4

**Authentication Support:**
- ✅ OIDC (Authorization Code + PKCE)
- ✅ Local (username/password via `/auth/token`)
- ⏸️ SAML (deferred to Phase 2)
- ⏸️ QuickStart (development only - no production token exchange yet)

**Endpoints Implemented:**
- `/auth/token` - Local authentication
- `/auth/refresh` - Token refresh
- `/admin/metadata/services` - Service CRUD
- `/admin/metadata/layers` - Layer CRUD
- `/admin/metadata/folders` - Folder CRUD

**Features:**
- Service management (create, read, update, delete)
- Layer management (create, read, update, delete)
- Folder organization (tree view with drag-and-drop)
- Basic metadata editing
- Real-time updates (PostgreSQL/Redis only)
- Manual refresh button (all providers)

**Deliverables:**
- Functional admin UI for core metadata operations
- Combined deployment working
- OIDC and Local auth tested
- Documentation for setup and usage

---

### Phase 2 - Enterprise SSO & Advanced Features

**Timeline:** Weeks 5-8

**Authentication Support:**
- ✅ SAML (via `/auth/exchange-saml` endpoint)
- Token exchange for SAML assertions to JWT
- Single sign-on with enterprise identity providers

**Endpoints Implemented:**
- `/auth/exchange-saml` - SAML assertion to JWT exchange
- `/admin/metadata/datasources` - Data source management
- `/admin/metadata/styles` - Style configuration
- `/admin/metadata/import` - Bulk data import

**Features:**
- Data import wizard (files, tables, external services)
- Style configuration (SLD, Mapbox, CartoCSS)
- Caching configuration UI
- Security/permissions management
- Audit log viewer
- Search and filtering
- Bulk operations

**Deliverables:**
- SAML integration working
- Data import wizard functional
- Style editor implemented
- Detached deployment option tested

---

### Phase 3 - Advanced Scenarios & Optimization

**Timeline:** Weeks 9-12

**Authentication Support:**
- ✅ API Key exchange (via `/auth/exchange-api-key`)
- ✅ Device Code flow (CLI support)
- Token exchange for legacy API keys

**Endpoints Implemented:**
- `/auth/exchange-api-key` - API key to JWT exchange
- `/admin/metadata/versions` - Versioning and rollback
- `/admin/metadata/compare` - Compare metadata versions
- `/admin/analytics` - Usage analytics

**Features:**
- Versioning and rollback UI
- Metadata diff viewer
- Performance optimization (lazy loading, virtual scrolling)
- Multi-tenant support (for demo site)
- Export/import metadata configurations
- Advanced search (full-text, filters)
- Workflow automation (approval processes)

**Deliverables:**
- Full feature parity with planned scope
- Multi-tenant demo site support
- CLI integration tested
- Production hardening complete

---

### Phase 4 - Polish & Documentation (Optional)

**Timeline:** Weeks 13-16

**Features:**
- User preferences and customization
- Keyboard shortcuts
- Dark mode
- Localization (i18n)
- Accessibility improvements (WCAG 2.1 AA)
- Mobile-responsive views
- Offline mode (Blazor WASM + service workers)

**Deliverables:**
- Comprehensive user documentation
- Video tutorials
- Admin training materials
- API documentation (OpenAPI/Swagger)
- Troubleshooting guides

---

## Security Implementation Checklist

Use this checklist before deploying to production:

### Token Security
- [ ] Access tokens expire in ≤15 minutes (`AccessTokenLifetime` configured)
- [ ] Refresh tokens expire in ≤7 days (`RefreshTokenLifetime` configured)
- [ ] Refresh tokens rotate on use (one-time use tokens)
- [ ] Tokens validated on every API request
- [ ] Scope claim includes `honua-control-plane` and is validated
- [ ] Tokens revoked on logout (call `/auth/revoke`)
- [ ] Token revocation list checked (if using Redis/database token store)
- [ ] JWT signing key rotated regularly (every 90 days minimum)
- [ ] JWT `jti` (JWT ID) claim used to prevent replay attacks

### Transport Security
- [ ] HTTPS enforced in production (`UseHttpsRedirection()`)
- [ ] HSTS enabled with long max-age (`app.UseHsts()`)
- [ ] Secure cookies (`Secure` flag set) for all auth cookies
- [ ] HttpOnly cookies for sensitive data (tokens, sessions)
- [ ] SameSite=Strict for all cookies (CSRF protection)
- [ ] TLS 1.2 minimum (TLS 1.3 preferred)
- [ ] Strong cipher suites only (no RC4, 3DES, etc.)
- [ ] Certificate pinning considered for mobile/desktop clients

### Authorization
- [ ] All `/admin/metadata/*` endpoints require authentication
- [ ] Role-based access control enforced (administrator, datapublisher, viewer)
- [ ] Least privilege principle (datapublisher cannot delete services)
- [ ] Authorization policies test all claim types (role, scope, custom)
- [ ] API endpoints use `.RequireAuthorization("PolicyName")` explicitly
- [ ] Blazor pages use `@attribute [Authorize(Roles = "...")]`
- [ ] Client-side authorization checks are cosmetic only (server validates)

### OIDC/SAML Configuration
- [ ] Client secrets stored in Azure Key Vault / AWS Secrets Manager / HashiCorp Vault
- [ ] Client secrets rotated regularly (every 90 days)
- [ ] PKCE enabled for all OAuth flows
- [ ] Token replay prevention (`jti` claim validation, token ID checked)
- [ ] Redirect URIs validated against allowlist (no open redirects)
- [ ] State parameter validated (CSRF protection for OAuth flows)
- [ ] Nonce validated (replay protection for ID tokens)
- [ ] Token signing certificates validated (pinned or from JWKS endpoint)
- [ ] Clock skew tolerance set appropriately (≤5 minutes)

### Rate Limiting
- [ ] `admin-operations` rate limiter configured (`FixedWindowLimiter`)
- [ ] Per-user limits configured (prevent single user abuse)
- [ ] Global limits configured (prevent DoS attacks)
- [ ] Rate limit headers returned (`X-RateLimit-*`)
- [ ] Rate limit exceeded returns 429 Too Many Requests
- [ ] Retry-After header included in 429 responses
- [ ] Different limits for read vs. write operations
- [ ] Higher limits for administrators vs. regular users

### Audit Logging
- [ ] All metadata changes logged (create, update, delete)
- [ ] User ID captured in all audit logs
- [ ] IP address captured in audit logs
- [ ] User agent captured (client type/version)
- [ ] Timestamp in UTC for all log entries
- [ ] Log retention policy defined (e.g., 90 days, 1 year)
- [ ] Logs sent to centralized logging (Elasticsearch, Azure Monitor, CloudWatch)
- [ ] Sensitive data (passwords, tokens) never logged
- [ ] PII (personally identifiable information) handled according to GDPR/CCPA
- [ ] Failed authentication attempts logged (security monitoring)

### Input Validation
- [ ] All user inputs validated on server-side (never trust client)
- [ ] File uploads validated (type, size, content)
- [ ] File size limits enforced (`MaxRequestBodySize`)
- [ ] File types allowlisted (not blocklisted)
- [ ] File content scanned (antivirus if handling user uploads)
- [ ] SQL injection prevented (parameterized queries, no string concatenation)
- [ ] XSS prevented (output encoding, Content-Security-Policy header)
- [ ] Command injection prevented (no shell commands with user input)
- [ ] Path traversal prevented (validate file paths, use `Path.GetFullPath`)
- [ ] Deserialization exploits prevented (use type allowlists, validate JSON)

### Error Handling
- [ ] Detailed errors only in development (`IsDevelopment()`)
- [ ] Production errors return RFC 7807 Problem Details (no stack traces)
- [ ] Internal errors logged but not exposed to clients
- [ ] Error correlation IDs included (for support troubleshooting)
- [ ] 500 errors trigger alerts (monitoring system)
- [ ] Error pages don't leak system information (paths, versions, etc.)

### Database Security
- [ ] Database connections use TLS/SSL
- [ ] Database user has minimum required permissions (not `db_owner`)
- [ ] Database credentials stored in secrets manager (not `appsettings.json`)
- [ ] Database backups encrypted at rest
- [ ] Database backups tested regularly (restore drill)
- [ ] Sensitive columns encrypted (e.g., PII, connection strings)
- [ ] SQL queries parameterized (no dynamic SQL with user input)

### Deployment Security
- [ ] Secrets stored in key vault (not environment variables or config files)
- [ ] Container images scanned for vulnerabilities (Trivy, Snyk, etc.)
- [ ] Base images updated regularly (patch management)
- [ ] Application runs as non-root user in container
- [ ] Network policies configured (Kubernetes NetworkPolicy, Azure NSG)
- [ ] Admin UI not exposed to public internet (VPN, internal only)
- [ ] Firewall rules restrict admin UI access (IP allowlist)
- [ ] Intrusion detection system (IDS) configured

### Monitoring & Incident Response
- [ ] Security monitoring enabled (failed logins, privilege escalation)
- [ ] Alerts configured for suspicious activity
- [ ] Incident response plan documented
- [ ] On-call rotation defined
- [ ] Security patch process defined (how quickly can you patch?)
- [ ] Penetration testing scheduled (annual minimum)
- [ ] Vulnerability disclosure policy published

### Compliance (if applicable)
- [ ] GDPR compliance verified (data subject rights, data retention)
- [ ] HIPAA compliance verified (if handling health data)
- [ ] PCI DSS compliance verified (if handling payment data)
- [ ] SOC 2 Type II audit completed (if SaaS offering)
- [ ] Data residency requirements met (where data is stored)
- [ ] Data processing agreements (DPAs) in place with vendors

---

## Migration Path

### Phase 1: Foundation (Week 1-2)
- [ ] Create Honua.Admin.Blazor project
- [ ] Add MudBlazor NuGet packages
- [ ] Create admin API endpoints
- [ ] Implement basic authentication/authorization
- [ ] Set up DI and state services

### Phase 2: Core Features (Week 3-4)
- [ ] Service management (CRUD)
- [ ] Layer management (CRUD)
- [ ] Folder management (CRUD)
- [ ] Folder tree navigation

### Phase 3: Advanced Features (Week 5-6)
- [ ] Data import wizard
- [ ] Style configuration
- [ ] Caching configuration
- [ ] Security/permissions management

### Phase 4: Polish (Week 7-8)
- [ ] Versioning/rollback UI
- [ ] Bulk operations
- [ ] Search and filtering
- [ ] Audit log viewer
- [ ] Documentation

---

## Key Decisions (Confirmed)

1. ✅ **Authentication**: Use existing HonuaIO JWT Bearer authentication (not new ASP.NET Core Identity)
2. ✅ **Multi-tenancy**: Support both single-tenant (primary) and multi-tenant (Honua Demo site) deployments
3. ✅ **Real-time updates**: Yes - leverage existing PostgresMetadataProvider NOTIFY/LISTEN support
4. ✅ **Deployment**: Support both combined and separate deployment configurations
5. ⏸️ **Offline support**: Not required (deferred)
6. ⏸️ **Localization**: Not required initially (deferred)

---

## References

### Related Documentation

- [ADMIN_UI_REALTIME_FALLBACK.md](./ADMIN_UI_REALTIME_FALLBACK.md) - Real-time update strategies for all metadata providers
- [ADMIN_UI_AI_INTEGRATION.md](./ADMIN_UI_AI_INTEGRATION.md) - AI-powered features and integration options (optional)
- [ADMIN_UI_ARCHITECTURE_SUMMARY.md](./ADMIN_UI_ARCHITECTURE_SUMMARY.md) - Executive summary and quick reference
- [ADR 0010: JWT and OIDC Authentication](./architecture/decisions/0010-jwt-oidc-authentication.md)
- [SECURITY.md](../SECURITY.md) - Security policy and vulnerability reporting

### External References

- [ASP.NET Core Blazor Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/?view=aspnetcore-9.0)
- [ASP.NET Core Blazor State Management](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management?view=aspnetcore-9.0)
- [Blazor Security Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-9.0)
- [MudBlazor Documentation](https://mudblazor.com/)
- [RFC 7807 - Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc7807)

---

## Appendix A: Technology Stack

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| **UI Framework** | Blazor Server | .NET 9.0 | Server-side UI rendering |
| **Component Library** | MudBlazor | 7.17.2 | Material Design components |
| **API Framework** | ASP.NET Core Minimal APIs | .NET 9.0 | REST API endpoints |
| **Data Access** | Existing `IMutableMetadataProvider` | - | Metadata persistence |
| **Database** | PostgreSQL | 10+ | Metadata storage |
| **Authentication** | ASP.NET Core Identity | .NET 9.0 | User authentication |
| **Authorization** | Policy-based authorization | .NET 9.0 | Role-based access control |
| **Validation** | FluentValidation | 11.11.0 | Input validation |
| **Testing** | xUnit + bUnit | Latest | Unit/integration tests |

---

## Appendix B: Glossary

- **MetadataSnapshot**: Immutable point-in-time view of all metadata (services, layers, folders, etc.)
- **IMutableMetadataProvider**: Interface for reading and writing metadata with optional versioning
- **Scoped Service**: DI service instance created per Blazor circuit (user session)
- **Singleton Service**: DI service instance shared across entire application
- **Render Mode**: How Blazor renders components (Server, WebAssembly, or Auto)
- **Circuit**: Blazor Server connection between browser and server via SignalR

---

**Document Approval:**

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Architect | [Pending] | [Pending] | [Pending] |
| Lead Developer | [Pending] | [Pending] | [Pending] |
| Security Review | [Pending] | [Pending] | [Pending] |

---

**Change Log:**

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-11-03 | Claude | Initial architecture document |
