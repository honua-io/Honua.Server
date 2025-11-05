# Honua Admin UI

Blazor Server-based admin interface for managing Honua GIS services, layers, folders, and data sources.

## Overview

The Honua Admin UI provides a modern, Material Design-based interface for GIS administrators to:
- Manage OGC services (WMS, WFS, WMTS)
- Configure layers and styling
- Organize content in folders
- Import data from various sources
- Monitor system health
- Track changes with version history

## Technology Stack

- **Framework**: ASP.NET Core 9.0 Blazor Server
- **UI Components**: MudBlazor 7.17.2 (Material Design)
- **Validation**: FluentValidation 11.11.0
- **Real-time**: SignalR (built into Blazor Server)
- **Authentication**: Honua unified token strategy (OIDC, Local, SAML)

## Project Structure

```
Honua.Admin.Blazor/
├── Components/
│   ├── Layout/              # MainLayout, NavMenu, Breadcrumbs
│   ├── Pages/               # Routable pages
│   │   ├── Services/        # Service management pages
│   │   ├── Layers/          # Layer management pages
│   │   └── Folders/         # Folder management pages
│   ├── App.razor            # Root component
│   ├── Routes.razor         # Router configuration
│   └── _Imports.razor       # Global using statements
├── Shared/
│   ├── Components/          # Reusable components
│   └── Services/            # UI state services
│       ├── NavigationState.cs
│       ├── EditorState.cs
│       └── NotificationService.cs
├── wwwroot/
│   ├── css/                 # Custom styles
│   └── js/                  # Custom JavaScript (if needed)
├── Program.cs               # Application entry point
└── appsettings.json         # Configuration

```

## Getting Started

### Prerequisites

1. .NET 9 SDK
2. Honua.Server.Host running (provides Admin API endpoints)
3. PostgreSQL database (for metadata)

### Configuration

Edit `appsettings.json`:

```json
{
  "AdminApi": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

### Running Locally

```bash
cd src/Honua.Admin.Blazor
dotnet run
```

Navigate to: https://localhost:7002

## Development Status

**Current Phase**: Phase 1.1 - Project Setup ✅

**Completed:**
- [x] Project structure created
- [x] MudBlazor integration
- [x] UI state services (NavigationState, EditorState, NotificationService)
- [x] Basic layout and navigation
- [x] Home dashboard page
- [x] Service list page (placeholder)

**Next Steps (Phase 1.2):**
- [ ] Create Admin REST API endpoints in Honua.Server.Host
- [ ] Implement service CRUD operations
- [ ] Implement layer CRUD operations
- [ ] Implement folder CRUD operations
- [ ] Add authentication integration

## Implementation Plan

See [ADMIN_UI_IMPLEMENTATION_PLAN.md](../../docs/ADMIN_UI_IMPLEMENTATION_PLAN.md) for full details.

**Phase 1: Foundation & Core CRUD (Weeks 1-4)**
- Project setup ✅
- Admin REST API endpoints
- Service/Layer/Folder management
- Authentication & authorization

**Phase 2: Real-Time Updates & Data Import (Weeks 5-8)**
**Phase 3: Advanced Features & Polish (Weeks 9-12)**
**Phase 4: Production Hardening (Weeks 13-16)**

## Architecture Decisions

### Why Blazor Server?
- Fast initial load (server-side rendering)
- Full .NET ecosystem access
- Real-time updates via SignalR (already built-in)
- Secure (sensitive logic on server)
- Works well with existing Honua authentication

### Why MudBlazor?
- Material Design (modern, familiar UX)
- Comprehensive component library (data grids, forms, dialogs)
- Active development and community
- Good documentation
- TypeScript-free (pure C#)

### Deployment Options

**Combined Deployment (Default):**
- Admin UI hosted in same process as Honua.Server.Host
- Single container/domain
- Shared authentication
- Simpler setup

**Detached Deployment (Advanced):**
- Admin UI separate process
- Independent scaling
- Network isolation (VPN for admin)
- CORS configuration required

## Related Documentation

- [ADMIN_UI_ARCHITECTURE.md](../../docs/ADMIN_UI_ARCHITECTURE.md) - Technical architecture
- [ADMIN_UI_UX_DESIGN.md](../../docs/ADMIN_UI_UX_DESIGN.md) - User experience design
- [ADMIN_UI_IMPLEMENTATION_PLAN.md](../../docs/ADMIN_UI_IMPLEMENTATION_PLAN.md) - Implementation roadmap

## License

Copyright (c) 2025 HonuaIO. All rights reserved.
Licensed under the Elastic License 2.0. See LICENSE file in the project root.
