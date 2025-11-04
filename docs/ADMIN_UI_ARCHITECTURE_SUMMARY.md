# Admin UI Architecture - Executive Summary

**Status:** ✅ Ready for Implementation
**Date:** 2025-11-03

---

## Key Decisions ✅

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **UI Framework** | Blazor Server + MudBlazor | Microsoft's recommended approach for admin UIs (SSR) |
| **API Layer** | ASP.NET Core Minimal APIs | REST endpoints at `/admin/metadata/*` |
| **Authentication** | Unified OAuth 2.1 Bearer Token Strategy | OIDC (Code+PKCE), SAML exchange, Local mode, API-key exchange (see below) |
| **Authorization** | Existing policies | `RequireAdministrator`, `RequireDataPublisher`, `RequireViewer` |
| **Data Access** | `IMutableMetadataProvider` | Leverage existing interface with versioning |
| **Real-Time Updates** | SignalR + Provider notifications | Server subscribes to provider, broadcasts to UI clients |
| **State Management** | Scoped DI services | Per Microsoft best practices (not MVVM) |
| **Multi-Tenancy** | Phase 1: Single tenant | Add `TenantContext` for future multi-tenant support |
| **Deployment** | Both combined & detached | Flexible deployment options |

---

## Unified Authentication Strategy

**All authentication methods normalize to OAuth 2.1 bearer tokens scoped to `honua-control-plane`:**

| Authentication Mode | Flow | Phase | Notes |
|-------------------|------|-------|-------|
| **OIDC** | Authorization Code + PKCE | Phase 1 | Primary enterprise auth (Okta, Entra ID, Auth0) |
| **Local** | Username/password → token exchange | Phase 1 | Fallback for air-gapped/dev environments |
| **SAML** | SAML assertion → `/auth/exchange-saml` → bearer token | Phase 2 | Enterprise SSO legacy support |
| **API Key** | API key → `/auth/exchange-api-key` → bearer token | Phase 3 | For CLI/automation (dev environments only) |

**Token Lifecycle:**
- **Access Token**: 15 minutes, stored in HttpOnly cookie (combined) or memory (detached)
- **Refresh Token**: 7 days, one-time use with rotation
- **Scope**: `honua-control-plane` (distinguishes admin from public API access)

**All API calls use the same bearer token** regardless of how the user originally authenticated.

**See ADR [0010-jwt-oidc-authentication](architecture/decisions/0010-jwt-oidc-authentication.md) for details.**

---

## Architecture at a Glance

```
┌─────────────────────────────────────────────────────────┐
│ BLAZOR SERVER UI (MudBlazor Components)                 │
│  • Service Management    • Layer Management             │
│  • Folder Organization   • Data Import Wizard           │
│  • Real-time updates via SignalR notifications          │
└────────────┬────────────────────────────────────────────┘
             │ HTTPS REST API + SignalR
             │ Authorization: Bearer {oauth-token}
             │ Scope: honua-control-plane
             ↓
┌─────────────────────────────────────────────────────────┐
│ ADMIN API ENDPOINTS                                     │
│  POST   /admin/metadata/services                        │
│  GET    /admin/metadata/services/{id}                   │
│  PUT    /admin/metadata/services/{id}                   │
│  DELETE /admin/metadata/services/{id}                   │
│  ... (layers, folders, versions)                        │
│                                                          │
│  [RequireAuthorization("RequireAdministrator")]         │
│  [EnableRateLimiting("admin-operations")]               │
└────────────┬────────────────────────────────────────────┘
             │ Dependency Injection
             ↓
┌─────────────────────────────────────────────────────────┐
│ IMutableMetadataProvider (EXISTING!)                    │
│  • PostgresMetadataProvider (with versioning)           │
│  • ACID transactions                                    │
│  • NOTIFY/LISTEN for real-time sync                     │
└────────────┬────────────────────────────────────────────┘
             │
             ↓
┌─────────────────────────────────────────────────────────┐
│ PostgreSQL Database                                     │
│  • metadata_snapshots (JSONB)                           │
│  • metadata_change_log (audit trail)                    │
│  • Automatic NOTIFY on changes                          │
└─────────────────────────────────────────────────────────┘
```

---

## Real-Time Updates (Built-In! 🎉)

**Zero-downtime metadata updates:**

```
Admin UI saves change via REST API
       ↓
Server writes to IMutableMetadataProvider
       ↓
PostgreSQL NOTIFY (provider fires event)
       ↓
    ┌──┴───────────┬──────────────┬──────────────┐
    ↓              ↓              ↓              ↓
Server 1       Server 2       Server 3       Admin UI Clients
Reloads        Reloads        Reloads        (via SignalR)
Metadata       Metadata       Metadata       Refresh UI

⏱️ Latency: ~100ms (same server), ~1s (cross-server)
💰 Cost: $0 (PostgreSQL + SignalR already required)
```

**Flow:**
1. Admin UI calls `/admin/metadata/services/{id}` (REST API with bearer token)
2. Server writes to metadata provider
3. Provider fires `MetadataChanged` event (if supported)
4. Server's `MetadataChangeNotificationHub` broadcasts to all connected UI clients via SignalR
5. **All servers consuming the metadata provider reload automatically:**
   - **Public API servers** (WMS, WFS, OGC, WMTS, etc.) - **hot reload without restart**
   - Other Admin API servers (if deployed in HA configuration)
   - Any Honua.Server.Host instances connected to the same metadata provider
6. UI clients refresh their data automatically

**This means:**
- ✅ Admin creates a WMS service → **Immediately available to public WMS clients** (~100ms)
- ✅ Admin updates layer style → **New style served instantly** (no restart)
- ✅ Admin disables a service → **Public API returns 404 immediately**
- ✅ Zero-downtime configuration changes across entire deployment

### Fallback for Other Databases

All metadata providers supported:

| Provider | Real-Time | Fallback Strategy |
|----------|-----------|------------------|
| PostgreSQL | ✅ NOTIFY/LISTEN (~100ms) | None needed |
| Redis | ✅ Pub/Sub (<100ms) | None needed |
| SQL Server | ⚠️ Polling (5-30s) | Provider fires events on poll |
| JSON/YAML | ⚠️ FileSystemWatcher | Manual refresh button |

**Admin UI adapts automatically** - checks `SupportsChangeNotifications` at runtime!

**See:** [ADMIN_UI_REALTIME_FALLBACK.md](./ADMIN_UI_REALTIME_FALLBACK.md) for details.

---

**This means:**
- Create a service in Admin UI → WMS instantly available to clients
- Edit a layer → Changes immediately reflected in public API
- No server restart required!

---

## Microsoft Best Practices Compliance ✅

| Practice | Implementation |
|----------|----------------|
| **Render Mode** | ✅ Blazor Server SSR (Microsoft's 2025 recommendation) |
| **Component Design** | ✅ Small, focused components. Business logic in services. |
| **State Management** | ✅ Scoped DI services (not singletons or static) |
| **Security** | ✅ JWT Bearer auth, policy-based authorization, HTTPS |
| **Performance** | ✅ Virtualization, @key usage, proper disposal |
| **DI Best Practices** | ✅ Constructor injection in services, @inject in components |

---

## Project Structure

```
src/
├── Honua.Admin.Blazor/              # NEW - Blazor Server UI
│   ├── Features/
│   │   ├── Services/
│   │   │   ├── Pages/
│   │   │   │   ├── ServiceList.razor
│   │   │   │   └── ServiceEditor.razor
│   │   │   └── Components/
│   │   ├── Layers/
│   │   ├── DataImport/
│   │   └── Shared/
│   │       └── Services/            # UI State Services
│   │           ├── NavigationState.cs
│   │           ├── EditorState.cs
│   │           └── TenantContext.cs (for future multi-tenant)
│   └── wwwroot/
│
└── Honua.Server.Host/               # EXISTING - Add admin endpoints
    └── Admin/
        └── MetadataAdministrationEndpoints.cs  # NEW
```

---

## API Endpoints

### Services
- `GET    /admin/metadata/services` - List all services
- `POST   /admin/metadata/services` - Create service
- `GET    /admin/metadata/services/{id}` - Get service
- `PUT    /admin/metadata/services/{id}` - Update service
- `DELETE /admin/metadata/services/{id}` - Delete service

### Layers
- `GET    /admin/metadata/services/{id}/layers` - List layers for service
- `POST   /admin/metadata/services/{id}/layers` - Add layer
- `PUT    /admin/metadata/layers/{id}` - Update layer
- `DELETE /admin/metadata/layers/{id}` - Delete layer

### Folders
- `GET    /admin/metadata/folders` - List folders
- `POST   /admin/metadata/folders` - Create folder
- `PUT    /admin/metadata/folders/{id}` - Update folder
- `DELETE /admin/metadata/folders/{id}` - Delete folder

### Versioning
- `GET    /admin/metadata/versions` - List snapshots
- `POST   /admin/metadata/versions` - Create snapshot
- `POST   /admin/metadata/versions/{id}/restore` - Restore version

**All endpoints:**
- Require authentication (JWT Bearer)
- Use existing authorization policies
- Rate-limited (`admin-operations`)
- Return RFC 7807 Problem Details for errors

---

## Deployment Options

### Option A: Combined (Development/Small Deployments)
```
Single container: Honua.Server.Host
  - Public API (/ogc, /wms, etc.)
  - Admin API (/admin/metadata)
  - Admin UI (/admin)
```

**Pros:** Simple deployment, single certificate
**Cons:** Shared resources

### Option B: Separate (Production/Large Deployments)
```
Container 1: Honua.Server.Host (public-facing)
  - Public API only
  - Horizontally scaled

Container 2: Honua.Admin.Host (internal/VPN)
  - Admin API + UI
  - Single instance or 2 for HA
```

**Pros:** Isolated resources, better security
**Cons:** More complex

---

## Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| UI Framework | Blazor Server | .NET 9.0 |
| Component Library | MudBlazor | 7.17.2 |
| API Framework | ASP.NET Core Minimal APIs | .NET 9.0 |
| Data Access | IMutableMetadataProvider | Existing |
| Database | PostgreSQL | 10+ |
| Authentication | JWT Bearer | Existing |
| Authorization | Policy-based | Existing |

---

## Implementation Phases

### Phase 1: Foundation (Week 1-2)
- ✅ Architecture approved
- Create Honua.Admin.Blazor project
- Add MudBlazor packages
- Create admin API endpoints
- Set up authentication/authorization
- Basic layout and navigation

### Phase 2: Core Features (Week 3-4)
- Service management (CRUD)
- Layer management (CRUD)
- Folder management (CRUD)
- Folder tree navigation
- Real-time updates integration

### Phase 3: Advanced Features (Week 5-6)
- Data import wizard
- Style configuration
- Caching configuration
- Security/permissions management
- AI Assistant (optional - see AI Integration section)

### Phase 4: Polish (Week 7-8)
- Versioning/rollback UI
- Bulk operations
- Search and filtering
- Audit log viewer

---

## AI Integration (Optional Enhancement)

**Leverages existing Honua.Cli.AI infrastructure** (Semantic Kernel, Azure OpenAI, Anthropic Claude)

### Key AI Features

**Phase 1 AI (MVP):**
- **Natural Language Search**: "Show me all WMS services created last month"
- **Metadata Generation**: Upload shapefile → AI suggests metadata
- **Configuration Assistant**: Chat-based help for complex setups

**Phase 2 AI (Advanced):**
- **Intelligent Error Detection**: AI analyzes configs, finds issues
- **Style Generation**: AI creates styles based on data characteristics
- **Query Optimization**: AI suggests database indexes and optimizations
- **Bulk Operations Assistant**: AI helps with bulk metadata changes

**Phase 3 AI (Predictive):**
- **Usage Prediction**: Forecast resource needs based on patterns
- **Automated Documentation**: Generate user guides from configurations

### Architecture Options

| Approach | Description | When to Use |
|----------|-------------|-------------|
| **Direct Integration** | Admin UI → AI endpoints → Honua.Cli.AI services | Simpler, combined deployment |
| **Separate AI Service** | Admin UI → AI Service API → Honua.Cli.AI | Scalable, dedicated AI resources |

### Cost Estimates

| Model | Usage Pattern | Estimated Cost |
|-------|--------------|----------------|
| **GPT-4** | 1000 AI requests/day | ~$525/month |
| **GPT-3.5-turbo** | 1000 AI requests/day | ~$50/month |
| **Claude 3 Sonnet** | 1000 AI requests/day | ~$150/month |

**Recommendation**: Start with GPT-3.5-turbo for MVP, upgrade selectively to GPT-4 for complex features.

### Integration Example

```razor
@* AI-Powered Search Component *@
<MudTextField @bind-Value="_searchQuery"
              Label="Ask me anything about your services..."
              Adornment="Adornment.End"
              AdornmentIcon="@Icons.Material.Filled.Psychology"
              OnAdornmentClick="HandleAISearchAsync" />

@code {
    private async Task HandleAISearchAsync()
    {
        var result = await Http.PostAsJsonAsync("/admin/ai/search", new
        {
            Query = _searchQuery,
            Context = "services",
            Filters = new { TenantId = _tenantId }
        });

        _services = await result.Content.ReadFromJsonAsync<List<ServiceDto>>();
    }
}
```

**See:** [ADMIN_UI_AI_INTEGRATION.md](./ADMIN_UI_AI_INTEGRATION.md) for complete details.

---

## What Makes This Architecture Good?

✅ **Reuses Existing Infrastructure**
- IMutableMetadataProvider (no new data layer)
- PostgresMetadataProvider with versioning (already built!)
- JWT authentication (already configured)
- NOTIFY/LISTEN (already working!)

✅ **Follows Microsoft Best Practices**
- Blazor Server SSR (2025 recommendation)
- Scoped DI for state management
- Policy-based authorization
- Component-based architecture

✅ **Production-Ready**
- Zero-downtime metadata updates
- ACID transactions (PostgreSQL)
- Versioning and rollback
- Rate limiting and security
- Audit logging

✅ **Flexible**
- Combined or separate deployment
- Single-tenant primary, multi-tenant ready
- Can migrate to Blazor WASM later if needed

✅ **Low Risk**
- No new authentication to implement
- No new data layer to build
- API pattern already used elsewhere in codebase
- Real-time already working

---

## Next Steps

### Ready to Start Implementation?

**Option 1: Full Implementation**
- Create project structure
- Build admin API endpoints
- Implement first Blazor feature (Service management)
- Add authentication integration

**Option 2: Proof of Concept**
- Build minimal Service CRUD only
- Verify architecture works end-to-end
- Then expand to other features

**Option 3: Review & Refine**
- Discuss specific concerns
- Adjust architecture as needed
- Then proceed with implementation

---

## Phase 1 Scope (MVP - Weeks 1-4)

**Authentication:**
- ✅ OIDC (Authorization Code + PKCE)
- ✅ Local (username/password)
- ⏸️ SAML (Phase 2)
- ⏸️ QuickStart API keys (dev only)

**Features:**
- Service management (CRUD)
- Layer management (CRUD)
- Folder organization (tree view)
- Basic metadata editing
- Real-time updates (PostgreSQL/Redis)
- Manual refresh button (all providers)

**Deployment:**
- Combined deployment (development)
- Detached deployment (optional)

---

## Documentation Updates (2025-11-03)

✅ **Added comprehensive token refresh flow** (combined & detached)
✅ **Added HttpClient configuration examples** (IHttpClientFactory setup)
✅ **Clarified CSRF strategy** (table showing when needed/not needed)
✅ **Added deployment decision matrix** (when to use combined vs. detached)
✅ **Added feature roadmap by phase** (4 phases with timelines)
✅ **Added security implementation checklist** (production readiness)
✅ **Standardized terminology** (using "Combined" consistently)
✅ **Added token lifecycle details** (expiry, storage, rotation)
✅ **Added AI integration proposal** (9 AI-powered features across 3 phases)

**All questions resolved!** 🎉

---

## Questions Confirmed

1. ✅ Use existing HonuaIO auth? → **Yes (unified token strategy)**
2. ✅ Support multi-tenancy? → **Phase 1: Single tenant, Phase 3: Multi-tenant**
3. ✅ Real-time updates? → **Yes, via existing NOTIFY/LISTEN (PostgreSQL/Redis)**
4. ✅ Deployment options? → **Both combined (dev) and detached (production)**
5. ✅ SAML support needed Phase 1? → **No (Phase 2)**
6. ✅ Token refresh strategy? → **Documented for both combined & detached**

---

## Documentation Package

**Complete architecture documentation ready for implementation:**

| Document | Purpose | Pages |
|----------|---------|-------|
| **ADMIN_UI_ARCHITECTURE_SUMMARY.md** | Executive summary and quick reference | This file |
| **ADMIN_UI_ARCHITECTURE.md** | Complete technical architecture and implementation guide | ~27 pages |
| **ADMIN_UI_REALTIME_FALLBACK.md** | Database provider fallback strategies | ~15 pages |
| **ADMIN_UI_AI_INTEGRATION.md** | AI-powered features proposal (optional) | ~12 pages |
| **ADMIN_UI_PUBLISHING_WORKFLOW.md** | Publishing workflow with validation & health checks | ~25 pages |
| **ADMIN_UI_DEPLOYMENT_INTEGRATION.md** | How Admin UI, GitOps, and Blue/Green work together | ~20 pages |

**Key Topics Covered:**
- ✅ Architecture decisions and rationale
- ✅ Authentication & authorization (OIDC, SAML, Local)
- ✅ Token lifecycle and refresh strategies
- ✅ Combined vs Detached deployment models
- ✅ Real-time updates across all database providers
- ✅ API endpoint design (REST with IMutableMetadataProvider)
- ✅ Security implementation checklist (95 items)
- ✅ 4-phase implementation roadmap
- ✅ AI integration options (optional enhancement)
- ✅ Code examples and component patterns
- ✅ Microsoft Blazor best practices (2025)
- ✅ Integration with existing GitOps and Blue/Green deployment

**Total: ~74 pages of comprehensive documentation**

---

**Full documentation:** See `ADMIN_UI_ARCHITECTURE.md` for complete details.

**Ready to build!** Let's start with Phase 1!
