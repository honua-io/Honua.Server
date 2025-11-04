# Honua Admin UI - Phased Implementation Plan

**Date:** 2025-11-04
**Status:** Planning
**Based on:** [ADMIN_UI_ARCHITECTURE.md](./ADMIN_UI_ARCHITECTURE.md) and [ADMIN_UI_UX_DESIGN.md](./ADMIN_UI_UX_DESIGN.md)

---

## Executive Summary

This document outlines a phased approach to implement the Honua Admin UI, a Blazor Server-based interface for GIS administrators to manage services, layers, folders, and data sources.

**Technology Stack:**
- **UI Framework**: Blazor Server (.NET 9)
- **Component Library**: MudBlazor 7.x
- **API**: ASP.NET Core Minimal APIs
- **Data Access**: Existing `IMutableMetadataProvider`
- **Auth**: Honua unified token strategy (OIDC, Local, SAML)
- **Real-time**: SignalR (leveraging PostgreSQL NOTIFY/LISTEN)

**Timeline:** 12-16 weeks (3-4 phases)

---

## Phase 1: Foundation & Core CRUD (Weeks 1-4)

### Goal
Deliver a functional Admin UI with basic CRUD operations for services, layers, and folders.

### Deliverables

#### 1.1 Project Setup (Week 1)
- [ ] Create `Honua.Admin.Blazor` project (Blazor Server)
- [ ] Add MudBlazor NuGet packages
  - `MudBlazor` (7.17.2+)
  - `MudBlazor.ThemeManager` (optional)
- [ ] Configure project structure:
  ```
  src/Honua.Admin.Blazor/
  ├── Program.cs
  ├── Features/
  │   ├── Services/
  │   │   ├── Pages/
  │   │   ├── Components/
  │   │   └── Models/
  │   ├── Layers/
  │   └── Folders/
  ├── Shared/
  │   ├── Components/
  │   └── Services/
  ├── Layout/
  └── wwwroot/
  ```
- [ ] Set up dependency injection
- [ ] Configure routing

#### 1.2 Admin REST API Endpoints (Week 1-2)
Create new endpoints in `Honua.Server.Host/Admin/MetadataAdministrationEndpoints.cs`:

**Services:**
- [ ] `GET /admin/metadata/services` - List all services
- [ ] `POST /admin/metadata/services` - Create service
- [ ] `GET /admin/metadata/services/{id}` - Get service by ID
- [ ] `PUT /admin/metadata/services/{id}` - Update service
- [ ] `DELETE /admin/metadata/services/{id}` - Delete service

**Layers:**
- [ ] `GET /admin/metadata/layers` - List all layers
- [ ] `POST /admin/metadata/layers` - Create layer
- [ ] `GET /admin/metadata/layers/{id}` - Get layer by ID
- [ ] `PUT /admin/metadata/layers/{id}` - Update layer
- [ ] `DELETE /admin/metadata/layers/{id}` - Delete layer

**Folders:**
- [ ] `GET /admin/metadata/folders` - List all folders
- [ ] `POST /admin/metadata/folders` - Create folder
- [ ] `PUT /admin/metadata/folders/{id}` - Update folder
- [ ] `DELETE /admin/metadata/folders/{id}` - Delete folder

**Request/Response Models:**
- [ ] Create DTOs (CreateServiceRequest, ServiceResponse, etc.)
- [ ] Add FluentValidation validators
- [ ] Implement RFC 7807 Problem Details for errors

#### 1.3 Authentication & Authorization (Week 2)
- [ ] Configure authentication modes:
  - [x] OIDC (Authorization Code + PKCE) - already exists
  - [x] Local (username/password) - already exists
  - [ ] Integrate with Admin UI
- [ ] Set up authorization policies:
  - [x] `RequireAdministrator` - already exists
  - [x] `RequireDataPublisher` - already exists
  - [ ] Apply to admin endpoints
- [ ] Configure `HttpClient` with bearer token handler:
  - [ ] `BearerTokenDelegatingHandler` for combined deployment
  - [ ] `IHttpClientFactory` registration
  - [ ] Token refresh logic

#### 1.4 UI State Services (Week 2)
- [ ] `NavigationState` - Current folder, breadcrumbs
- [ ] `EditorState` - Unsaved changes tracking
- [ ] `NotificationService` - Toast messages (MudSnackbar wrapper)
- [ ] Register as scoped services in DI

#### 1.5 Service Management UI (Week 3)
**Pages:**
- [ ] `ServiceList.razor` - List all services with search/filter
  - MudDataGrid with virtualization
  - Search by name, ID, type
  - Filter by folder, service type
  - Actions: View, Edit, Delete
- [ ] `ServiceEditor.razor` - Create/edit service
  - Service metadata form (ID, Name, Type)
  - Folder selection
  - Caching configuration
  - Layer management (add/remove/reorder)
  - Validation with error display
  - Save/Cancel actions

**Components:**
- [ ] `ServiceCard.razor` - Service summary card
- [ ] `ServiceForm.razor` - Reusable service form
- [ ] `ServiceTypeSelector.razor` - WMS/WFS/WMTS picker

#### 1.6 Layer Management UI (Week 3-4)
**Pages:**
- [ ] `LayerList.razor` - List layers across all services
  - Search by name, ID
  - Filter by service, geometry type
  - Bulk selection
- [ ] `LayerEditor.razor` - Create/edit layer
  - Layer metadata (ID, Name, Title, Abstract)
  - Data source selection
  - CRS configuration
  - Bounding box editor
  - Style assignment
  - Queryability settings

**Components:**
- [ ] `LayerForm.razor` - Reusable layer form
- [ ] `BoundingBoxEditor.razor` - Interactive bbox editor
- [ ] `CrsSelector.razor` - CRS picker with search

#### 1.7 Folder Management UI (Week 4)
**Pages:**
- [ ] `FolderBrowser.razor` - Tree view of folders
  - Expandable folder tree (MudTreeView)
  - Drag-and-drop to reorganize
  - Context menu (New, Rename, Delete)
  - Service count badges

**Components:**
- [ ] `FolderTreeView.razor` - Recursive folder tree
- [ ] `FolderDialog.razor` - Create/rename folder modal

#### 1.8 Layout & Navigation (Week 4)
- [ ] `MainLayout.razor` - App layout with sidebar
- [ ] `NavMenu.razor` - Left navigation menu
  - Dashboard
  - Services
  - Layers
  - Folders
  - Data Sources (future)
  - Settings
- [ ] `Breadcrumbs.razor` - Navigation breadcrumbs
- [ ] Theme configuration (MudBlazor theme colors)

### Testing (Week 4)
- [ ] Unit tests for API endpoints
- [ ] Integration tests for service/layer CRUD
- [ ] Blazor component tests (bUnit) for key components
- [ ] Manual testing of full workflows

### Documentation (Week 4)
- [ ] Admin UI setup guide
- [ ] Developer documentation (how to extend)
- [ ] User guide (screenshots + workflows)

### Phase 1 Success Criteria
- ✅ Admin can create, read, update, delete services
- ✅ Admin can create, read, update, delete layers
- ✅ Admin can organize services in folders
- ✅ Authentication working (OIDC + Local)
- ✅ Authorization policies enforced
- ✅ Basic error handling and validation
- ✅ Combined deployment working (Admin UI + API same process)

---

## Phase 2: Real-Time Updates & Data Import (Weeks 5-8)

### Goal
Add real-time collaboration features and a data import wizard for easy onboarding.

### Deliverables

#### 2.1 Real-Time Updates (Week 5)
**Backend:**
- [ ] `MetadataChangeNotificationHub` - SignalR hub for metadata changes
- [ ] Subscribe to `IMutableMetadataProvider.MetadataChanged` event
- [ ] Broadcast changes to all connected clients
- [ ] `GetSupportsRealTimeUpdates()` method for capability detection

**Frontend:**
- [ ] Configure SignalR `HubConnection` in UI components
- [ ] Subscribe to `MetadataChanged` events
- [ ] Auto-refresh data when changes detected
- [ ] Show "Live Updates" indicator when supported
- [ ] Fallback to manual refresh button when not supported

**Providers:**
- [x] PostgreSQL - NOTIFY/LISTEN (already built-in)
- [ ] Redis - Pub/Sub support (if needed)
- [ ] SQL Server - Polling fallback
- [ ] JSON/YAML - FileSystemWatcher (single server only)

#### 2.2 Data Import Wizard (Week 5-6)
**Pages:**
- [ ] `DataImportWizard.razor` - Multi-step wizard
  - Step 1: Choose source type (File, Database, URL, External service)
  - Step 2: Upload/configure connection
  - Step 3: Preview data (geometry, attributes, CRS)
  - Step 4: Configure layer settings (name, style, metadata)
  - Step 5: Review & publish

**Components:**
- [ ] `FileUploadStep.razor` - Drag-drop file upload (Shapefile, GeoJSON, CSV)
- [ ] `DatabaseConnectionStep.razor` - Connection string builder
- [ ] `DataPreviewGrid.razor` - Preview table with geometry viewer
- [ ] `StyleWizard.razor` - Simple style configuration
- [ ] `PublishReviewStep.razor` - Summary before publish

**Backend:**
- [ ] `POST /admin/metadata/import/upload` - Upload file
- [ ] `POST /admin/metadata/import/preview` - Preview data
- [ ] `POST /admin/metadata/import/publish` - Create service + layer

#### 2.3 Enhanced Search & Filtering (Week 6)
- [ ] Global search across services, layers, folders
- [ ] Advanced filters:
  - Service type (WMS, WFS, WMTS)
  - Geometry type (Point, Line, Polygon)
  - CRS (EPSG:4326, EPSG:3857, etc.)
  - Folder location
  - Metadata keywords
- [ ] Saved filter presets
- [ ] Recent searches history

#### 2.4 Versioning UI (Week 7)
**Pages:**
- [ ] `VersionHistory.razor` - List of metadata snapshots
  - Snapshot date/time
  - User who created
  - Change summary
  - Actions: View Diff, Restore

- [ ] `VersionDiff.razor` - Side-by-side comparison
  - Highlight added/removed/modified services
  - JSON diff viewer
  - Restore button

**Backend:**
- [ ] `GET /admin/metadata/versions` - List versions
- [ ] `GET /admin/metadata/versions/{id}` - Get specific version
- [ ] `POST /admin/metadata/versions/{id}/restore` - Restore version
- [ ] `GET /admin/metadata/versions/{id}/diff` - Compare versions

#### 2.5 Audit Log Viewer (Week 7)
**Pages:**
- [ ] `AuditLog.razor` - Searchable audit trail
  - Filter by user, entity type, action, date range
  - Export to CSV
  - Detailed event view

**Backend:**
- [ ] `GET /admin/metadata/audit` - Query audit log
- [ ] Capture user ID, IP, timestamp for all mutations

#### 2.6 Caching Configuration UI (Week 8)
**Components:**
- [ ] `CacheConfigPanel.razor` - Cache settings per service/layer
  - Enable/disable caching
  - TTL configuration
  - Cache invalidation
  - Cache statistics (hit rate, size)

#### 2.7 Security & Permissions UI (Week 8)
**Pages:**
- [ ] `UserManagement.razor` - List users
- [ ] `RoleAssignment.razor` - Assign roles (Administrator, DataPublisher, Viewer)

**Backend:**
- [ ] User CRUD endpoints (if not using external IdP)
- [ ] Role assignment endpoints

### Testing (Week 8)
- [ ] Real-time update testing (multi-user scenarios)
- [ ] Data import testing (various formats)
- [ ] Versioning/rollback testing
- [ ] Performance testing (large datasets)

### Phase 2 Success Criteria
- ✅ Real-time updates working for PostgreSQL
- ✅ Data import wizard can publish Shapefile, GeoJSON, CSV
- ✅ Version history and restore working
- ✅ Audit log capturing all changes
- ✅ Multi-user collaboration tested
- ✅ Detached deployment option verified (UI separate from API)

---

## Phase 3: Advanced Features & Polish (Weeks 9-12)

### Goal
Advanced admin features, performance optimization, and production hardening.

### Deliverables

#### 3.1 Bulk Operations (Week 9)
**Features:**
- [ ] Bulk delete services/layers
- [ ] Bulk move to folder
- [ ] Bulk update metadata (tags, keywords)
- [ ] Bulk style application
- [ ] Progress indicator for long-running operations
- [ ] Undo/rollback support

**UI:**
- [ ] Checkbox selection in grids
- [ ] Bulk action toolbar
- [ ] Confirmation dialogs with preview

#### 3.2 Style Editor (Week 9-10)
**Pages:**
- [ ] `StyleEditor.razor` - Visual SLD/MapBox editor
  - Split-pane layout (controls + map preview)
  - Style types: Simple, Categorized, Graduated
  - Color pickers, line width, opacity controls
  - Live map preview (updates as you edit)
  - Import/export SLD, MapBox Style Spec

**Features:**
- [ ] Automatic categorized style generation (unique values)
- [ ] ColorBrewer palettes
- [ ] Jenks natural breaks for graduated styles
- [ ] Style templates library
- [ ] Save custom styles

#### 3.3 Monitoring Dashboard (Week 10)
**Pages:**
- [ ] `Dashboard.razor` - Overview with metrics
  - Active services count
  - Total layers
  - API request rate (last 24h)
  - Cache hit rate
  - Storage usage
  - Recent activity feed
  - Health status indicators

**Charts:**
- [ ] Line charts for request rate over time
- [ ] Donut charts for service type distribution
- [ ] Bar charts for top services by usage

#### 3.4 Performance Optimization (Week 11)
- [ ] Implement `ShouldRender()` for heavy components
- [ ] Add virtualization to large lists (MudDataGrid `Virtualize="true"`)
- [ ] Lazy loading for detail pages
- [ ] Component disposal (`IDisposable` for event handlers)
- [ ] Use `@key` for collection rendering
- [ ] Client-side caching of metadata (reduce API calls)
- [ ] Debounce search inputs

#### 3.5 AI Assistant Integration (Optional - Week 11)
**See:** [ADMIN_UI_AI_INTEGRATION.md](./ADMIN_UI_AI_INTEGRATION.md) for full details.

**Features:**
- [ ] AI-powered search (natural language)
- [ ] Smart suggestions (CRS, style recommendations)
- [ ] Troubleshooting assistant (diagnose issues)
- [ ] Metadata auto-generation (descriptions, keywords)

#### 3.6 Export/Import Configurations (Week 12)
**Features:**
- [ ] Export metadata to JSON/YAML
- [ ] Import metadata from file (merge or replace)
- [ ] Export single service or entire catalog
- [ ] Validate before import
- [ ] Dry-run mode (show what would change)

**Backend:**
- [ ] `POST /admin/metadata/export` - Export catalog
- [ ] `POST /admin/metadata/import` - Import catalog

#### 3.7 Multi-Tenant Support (Week 12)
**Note:** Only for your demo site, not customer-facing.

**Features:**
- [ ] `TenantContext` service (scoped)
- [ ] Catalog/tenant selector in nav bar
- [ ] Filter services/layers by catalog
- [ ] Separate metadata per tenant (Option A: multiple files)

**Backend:**
- [x] Tenant resolution (already exists in Enterprise)
- [x] Quota enforcement (already exists)
- [ ] Admin API endpoints respect tenant context

### Testing (Week 12)
- [ ] Load testing (1000+ layers)
- [ ] Multi-tenant testing
- [ ] Performance benchmarks (page load, API calls)
- [ ] Browser compatibility (Chrome, Edge, Firefox, Safari)
- [ ] Mobile responsiveness

### Phase 3 Success Criteria
- ✅ Bulk operations working
- ✅ Style editor can create/edit SLD styles
- ✅ Monitoring dashboard shows real metrics
- ✅ Performance acceptable for 1000+ layers
- ✅ Export/import working
- ✅ Multi-tenant demo site functional

---

## Phase 4: Production Hardening & Documentation (Weeks 13-16)

### Goal
Security hardening, comprehensive documentation, and production deployment.

### Deliverables

#### 4.1 Security Review & Hardening (Week 13)
**Use checklist from ADMIN_UI_ARCHITECTURE.md Section "Security Implementation Checklist":**

**Token Security:**
- [ ] Access tokens expire in ≤15 minutes
- [ ] Refresh tokens rotate on use
- [ ] Tokens validated on every request
- [ ] Scope claim validated (`honua-control-plane`)
- [ ] Token revocation working

**Transport Security:**
- [ ] HTTPS enforced
- [ ] HSTS enabled
- [ ] Secure cookies (HttpOnly, Secure, SameSite=Strict)
- [ ] TLS 1.2 minimum

**Authorization:**
- [ ] All admin endpoints require authentication
- [ ] RBAC enforced (administrator, datapublisher, viewer)
- [ ] Least privilege principle
- [ ] Client-side auth checks are cosmetic only

**Input Validation:**
- [ ] Server-side validation for all inputs
- [ ] File upload validation (type, size, content)
- [ ] SQL injection prevention (parameterized queries)
- [ ] XSS prevention (output encoding, CSP header)

**Rate Limiting:**
- [ ] `admin-operations` rate limiter configured
- [ ] Per-user limits
- [ ] 429 Too Many Requests returned correctly

**Audit Logging:**
- [ ] All mutations logged (user, IP, timestamp)
- [ ] Logs sent to centralized logging
- [ ] Sensitive data never logged
- [ ] Failed auth attempts logged

#### 4.2 SAML Integration (Week 13-14)
**Note:** Enterprise SSO support

- [ ] `/auth/exchange-saml` endpoint
- [ ] SAML assertion to JWT token exchange
- [ ] IdP metadata configuration UI
- [ ] SP metadata generation
- [ ] Test with Azure AD, Okta, Auth0

#### 4.3 Accessibility (WCAG 2.1 AA) (Week 14)
- [ ] Keyboard navigation (Tab, Enter, Esc)
- [ ] ARIA labels for interactive elements
- [ ] Focus indicators
- [ ] Color contrast ratios (4.5:1 minimum)
- [ ] Screen reader testing (NVDA, JAWS)
- [ ] Skip navigation links
- [ ] Form error announcements

#### 4.4 Error Handling & Resilience (Week 14)
- [ ] Global error boundary (`ErrorBoundary` component)
- [ ] Retry logic for transient failures
- [ ] Offline detection
- [ ] Graceful degradation (fallback UI)
- [ ] User-friendly error messages
- [ ] Error correlation IDs (for support)

#### 4.5 Comprehensive Documentation (Week 15)
**Administrator Guide:**
- [ ] Installation & setup
- [ ] Configuration reference
- [ ] Authentication setup (OIDC, SAML, Local)
- [ ] User management
- [ ] Backup & restore procedures
- [ ] Troubleshooting guide

**User Guide:**
- [ ] Getting started
- [ ] Publishing your first service
- [ ] Managing layers
- [ ] Using the style editor
- [ ] Data import wizard
- [ ] Version history & rollback
- [ ] Screenshots & videos

**Developer Guide:**
- [ ] Architecture overview
- [ ] Extending the UI (custom components)
- [ ] API reference (Swagger/OpenAPI)
- [ ] Contribution guidelines

#### 4.6 Deployment Options (Week 15)
**Combined Deployment (Default):**
- [ ] `Honua.Server.Host` includes Admin UI
- [ ] Single container/process
- [ ] Shared authentication
- [ ] Docker Compose example
- [ ] Kubernetes manifest

**Detached Deployment (Advanced):**
- [ ] `Honua.Admin.Blazor` separate process
- [ ] CORS configuration
- [ ] Separate TLS certificates
- [ ] Network isolation (Admin UI on VPN)
- [ ] Docker Compose example
- [ ] Kubernetes manifest

**Cloud Deployments:**
- [ ] Azure Container Apps
- [ ] AWS ECS/Fargate
- [ ] Google Cloud Run

#### 4.7 Observability (Week 16)
- [ ] OpenTelemetry instrumentation
- [ ] Distributed tracing (Jaeger, Zipkin)
- [ ] Metrics export (Prometheus)
- [ ] Health check endpoints (`/health`, `/ready`)
- [ ] Structured logging (JSON)

#### 4.8 Final Testing & QA (Week 16)
- [ ] End-to-end testing (Playwright or Selenium)
- [ ] Security penetration testing
- [ ] Performance testing (load, stress)
- [ ] Usability testing (with real GIS admins)
- [ ] Browser compatibility testing
- [ ] Mobile responsiveness testing

### Phase 4 Success Criteria
- ✅ Security review passed (no critical issues)
- ✅ SAML integration working
- ✅ WCAG 2.1 AA compliant
- ✅ Comprehensive documentation complete
- ✅ Both deployment options tested
- ✅ Production deployment successful
- ✅ User acceptance testing passed

---

## Post-Launch Enhancements (Future)

### Phase 5: Polish & Advanced UX (Optional)
- [ ] Dark mode
- [ ] User preferences & customization
- [ ] Keyboard shortcuts
- [ ] Localization (i18n) - multiple languages
- [ ] Guided tours (Shepherd.js)
- [ ] Advanced AI features (see ADMIN_UI_AI_INTEGRATION.md)
- [ ] Mobile app (MAUI)
- [ ] Offline mode (Blazor WASM + service workers)

---

## Dependencies & Prerequisites

### Required
- [x] .NET 9 SDK
- [x] PostgreSQL 10+ (or compatible metadata provider)
- [x] Existing `IMutableMetadataProvider` implementation
- [x] Honua authentication system (OIDC, Local)
- [x] Existing OGC services (WMS, WFS, WMTS)

### Optional
- [ ] Redis (for distributed caching)
- [ ] Elasticsearch (for advanced search)
- [ ] Azure AD / Okta (for SAML SSO)
- [ ] OpenTelemetry Collector (for observability)

---

## Resource Allocation

### Team
- **1 Backend Developer** (API endpoints, authentication, real-time)
- **1 Full-Stack Developer** (Blazor UI, components, integration)
- **0.5 DevOps Engineer** (deployment, infrastructure, observability)
- **0.5 QA Engineer** (testing, security review)
- **0.25 Technical Writer** (documentation)

### Effort Estimate
- **Phase 1:** 160 hours (1 month, 2 devs)
- **Phase 2:** 160 hours (1 month, 2 devs)
- **Phase 3:** 160 hours (1 month, 2 devs)
- **Phase 4:** 160 hours (1 month, 2 devs)
- **Total:** 640 hours (~4 months with 2 developers)

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **MudBlazor breaking changes** | Low | Medium | Pin to specific version, test upgrades thoroughly |
| **Real-time performance issues** | Medium | Medium | Implement connection limits, throttling, fallback to polling |
| **Complex metadata models** | Medium | High | Simplify UI, progressive disclosure, AI assistance |
| **Browser compatibility** | Low | Medium | Test early, use polyfills, fallback for unsupported features |
| **Security vulnerabilities** | Medium | High | Regular security audits, penetration testing, bug bounty |
| **Performance with 10,000+ layers** | High | High | Pagination, virtualization, indexing, caching |
| **User adoption resistance** | Medium | Medium | User testing, training materials, gradual rollout |

---

## Success Metrics

### Technical Metrics
- **API Response Time**: <500ms (p95)
- **Page Load Time**: <2s (p95)
- **Uptime**: 99.9% (excluding maintenance)
- **Error Rate**: <0.1% of requests
- **Cache Hit Rate**: >80%

### User Metrics
- **Time to Publish Service**: <5 minutes (vs. 30 minutes manual)
- **User Satisfaction**: >4.0/5.0
- **Feature Adoption**: >70% of admins use data import wizard
- **Support Tickets**: -50% reduction in "How do I...?" tickets

### Business Metrics
- **Onboarding Time**: <1 day (vs. 3-5 days without UI)
- **Admin Productivity**: 3x increase (measured by services published per week)
- **Error Rate**: 60% fewer errors on first publish

---

## Related Documentation

- [ADMIN_UI_ARCHITECTURE.md](./ADMIN_UI_ARCHITECTURE.md) - Technical architecture details
- [ADMIN_UI_UX_DESIGN.md](./ADMIN_UI_UX_DESIGN.md) - User experience design
- [ADMIN_UI_REALTIME_FALLBACK.md](./ADMIN_UI_REALTIME_FALLBACK.md) - Real-time update strategies
- [ADMIN_UI_AI_INTEGRATION.md](./ADMIN_UI_AI_INTEGRATION.md) - AI-powered features (optional)
- [SECURITY.md](../SECURITY.md) - Security policy

---

## Next Steps

1. **Review this plan** with stakeholders
2. **Prioritize features** (must-have vs. nice-to-have)
3. **Set up project tracking** (GitHub Issues/Projects)
4. **Create initial project structure** (Phase 1.1)
5. **Begin Sprint 1** (Admin API endpoints + authentication)

---

**Approval:**

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Product Owner | [Pending] | [Pending] | [Pending] |
| Lead Developer | [Pending] | [Pending] | [Pending] |
| Security Lead | [Pending] | [Pending] | [Pending] |

---

**Document Version:** 1.0
**Last Updated:** 2025-11-04
**Author:** Claude (AI Assistant)
