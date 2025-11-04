# HonuaIO Project Dependency Graph

**Last Updated:** 2025-10-17
**Status:** No Circular Dependencies

## Overview

This document provides a visual representation of the project dependencies within the HonuaIO solution. All dependencies form a **directed acyclic graph (DAG)** with no circular references.

## Project Dependency Diagram

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        ENTRY POINTS                              │
│                       (Applications)                             │
└──────────────────┬──────────────────────────────┬───────────────┘
                   │                              │
                   ▼                              ▼
        ┌──────────────────┐          ┌──────────────────┐
        │   Honua.Cli      │          │ Honua.Server.    │
        │                  │          │      Host        │
        │  (Console App)   │          │   (Web API)      │
        └────────┬─────────┘          └────────┬─────────┘
                 │                              │
                 │                              │
        ┌────────┴────────┐                     │
        │                 │                     │
        ▼                 ▼                     ▼
┌──────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ Honua.Cli.AI │  │ Honua.Cli.AI.    │  │ Honua.Server.    │
│              │  │    Secrets       │  │      Core        │
│ (AI Layer)   │  │  (Secrets Mgmt)  │  │   (Core Logic)   │
└──────┬───────┘  └──────────────────┘  └────────▲─────────┘
       │                                          │
       │                                          │
       └──────────────────┬───────────────────────┘
                          │
                          ▼
                   ┌──────────────────┐
                   │ Honua.Server.    │
                   │      Core        │
                   │   (Core Logic)   │
                   └──────────────────┘

Standalone Applications:
┌──────────────────────────────┐
│ Honua.Server.AlertReceiver   │
│    (Microservice)            │
│    No Honua Dependencies     │
└──────────────────────────────┘

Optional Enterprise Features:
┌──────────────────┐
│ Honua.Server.    │
│   Enterprise     │
│ (Big Data DBs)   │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ Honua.Server.    │
│      Core        │
└──────────────────┘
```

## Detailed Project Dependency Matrix

### Direct Dependencies

| Project | Depends On | Dependency Type | Notes |
|---------|-----------|-----------------|-------|
| **Honua.Cli** | Honua.Cli.AI<br>Honua.Cli.AI.Secrets<br>Honua.Server.Core | Project References | Entry point for CLI |
| **Honua.Cli.AI** | Honua.Cli.AI.Secrets<br>Honua.Server.Core | Project References | AI/LLM integration layer |
| **Honua.Cli.AI.Secrets** | (none) | - | Secrets management |
| **Honua.Server.Host** | Honua.Server.Core | Project Reference | Web API entry point |
| **Honua.Server.Core** | (none) | - | Core business logic |
| **Honua.Server.Enterprise** | Honua.Server.Core | Project Reference | Enterprise DB connectors |
| **Honua.Server.AlertReceiver** | (none) | - | Standalone microservice |

### Transitive Dependencies

```
Honua.Cli
  ├─→ Honua.Cli.AI
  │   ├─→ Honua.Cli.AI.Secrets (direct)
  │   └─→ Honua.Server.Core (direct)
  ├─→ Honua.Cli.AI.Secrets (direct)
  └─→ Honua.Server.Core (direct)

Honua.Server.Host
  └─→ Honua.Server.Core (direct)

Honua.Server.Enterprise
  └─→ Honua.Server.Core (direct)
```

## Dependency Layers

### Layer 0: Foundation (Leaf Nodes)
Projects with no Honua project dependencies:

- `Honua.Server.Core` - Core business logic and domain models
- `Honua.Cli.AI.Secrets` - Secrets management abstraction
- `Honua.Server.AlertReceiver` - Standalone alert receiver

**Characteristics:**
- Can be compiled independently
- No internal project dependencies
- May depend on external NuGet packages only

### Layer 1: Domain Extensions
Projects depending only on Layer 0:

- `Honua.Cli.AI` - AI/LLM integration (depends on Core, Secrets)
- `Honua.Server.Host` - Web API host (depends on Core)
- `Honua.Server.Enterprise` - Enterprise DB support (depends on Core)

**Characteristics:**
- Add functionality to core
- Single layer of dependencies
- Domain-specific implementations

### Layer 2: Applications (Root Nodes)
Top-level entry points:

- `Honua.Cli` - Command-line interface application

**Characteristics:**
- Application entry points
- Orchestrate lower layers
- Not referenced by other projects

## Dependency Metrics

### Project Statistics

```
Total Projects:              7
Entry Point Projects:        2 (Honua.Cli, Honua.Server.Host)
Standalone Projects:         1 (Honua.Server.AlertReceiver)
Core/Library Projects:       4
Maximum Dependency Depth:    3 levels
Average Dependencies/Project: 1.14
```

### Dependency Health Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Circular Dependencies | 0 | ✅ Excellent |
| Projects with 0 deps | 3 | ✅ Good modularity |
| Projects with >3 deps | 0 | ✅ Low coupling |
| Max dependency chain | 3 | ✅ Reasonable |
| InternalsVisibleTo (non-test) | 0 | ✅ Clean boundaries |

## Namespace Cross-References

### Honua.Server.* Namespace Analysis

```
Honua.Server.Host → Honua.Server.Core
  Direction: One-way ✅
  Files using Core: 92
  Files in Core using Host: 0
  Status: CLEAN
```

### Honua.Cli.* Namespace Analysis

```
Honua.Cli → Honua.Cli.AI
  Direction: One-way ✅
  Files in Cli using Cli.AI: 22
  Files in Cli.AI using Cli: 0 (project level)
  Status: CLEAN (some namespace organization review recommended)
```

## Dependency Flow Rules

### Established Patterns

1. **Core Independence**
   ```
   Honua.Server.Core must not reference any Honua.* projects
   ```

2. **Upward Dependencies Only**
   ```
   Applications → Services → Core
   (Higher layers may depend on lower layers, never reverse)
   ```

3. **Horizontal Isolation**
   ```
   Honua.Cli.AI may not reference Honua.Cli
   Honua.Server.Host may not reference Honua.Cli.*
   ```

4. **Entry Point Isolation**
   ```
   Entry points (Honua.Cli, Honua.Server.Host) are never referenced
   ```

### Anti-Patterns (Prevented)

❌ **Forbidden:** `Honua.Server.Core` → `Honua.Server.Host`
❌ **Forbidden:** `Honua.Cli.AI` → `Honua.Cli`
❌ **Forbidden:** `Honua.Server.Host` → `Honua.Cli`
❌ **Forbidden:** Circular references of any kind

## Build Order

Given the dependency graph, the optimal build order is:

### Serial Build Order
```
1. Honua.Server.Core
2. Honua.Cli.AI.Secrets
3. Honua.Server.AlertReceiver
   (These can build in parallel - no interdependencies)

4. Honua.Cli.AI
   Honua.Server.Host
   Honua.Server.Enterprise
   (These can build in parallel - all depend only on layer 0)

5. Honua.Cli
   (Top-level application)
```

### Parallel Build Opportunities

```
Phase 1 (Parallel):
  ├── Honua.Server.Core
  ├── Honua.Cli.AI.Secrets
  └── Honua.Server.AlertReceiver

Phase 2 (Parallel):
  ├── Honua.Cli.AI (waits for Core + Secrets)
  ├── Honua.Server.Host (waits for Core)
  └── Honua.Server.Enterprise (waits for Core)

Phase 3:
  └── Honua.Cli (waits for Cli.AI + Secrets + Core)
```

## External Dependencies

### Shared NuGet Packages

These packages are used across multiple projects and may indicate architectural coupling:

- `Microsoft.Extensions.*` (DI, Configuration, Logging)
- `Microsoft.SemanticKernel.*` (AI/LLM functionality)
- `Npgsql` (Database access)
- `OpenTelemetry.*` (Observability)
- `Polly` (Resilience)
- `StackExchange.Redis` (Caching)

### Potential Coupling Concerns

**High:** Projects sharing heavy dependencies may benefit from abstraction:
- Both Cli.AI and Server.Core use Semantic Kernel
- Both Cli and Server projects use StackExchange.Redis

**Recommendation:** Monitor for duplication; extract to shared contracts if needed.

## Future Considerations

### When to Create Abstraction Projects

Consider creating `Honua.Contracts` or `Honua.Abstractions` when:

1. **Multiple projects need shared interfaces** beyond what Core provides
2. **NuGet packaging** - Need clean API boundaries for distribution
3. **Plugin architecture** - Third-party extensions need stable contracts
4. **Version isolation** - Need to version APIs independently

### Potential Future Structure

```
Honua.Contracts (Future)
  ├── Shared interfaces
  ├── DTOs
  └── Events
       ▲
       │
       ├─── Honua.Server.Core
       ├─── Honua.Cli.AI
       └─── (Third-party plugins)
```

## Verification Commands

### Check for Circular Dependencies

```bash
# MSBuild check
dotnet msbuild -t:ResolveProjectReferences 2>&1 | grep -i "circular"

# Expected output: (empty - no results)
```

### Generate Dependency Tree

```bash
# For Honua.Cli project
dotnet list src/Honua.Cli/Honua.Cli.csproj reference --graph

# For Honua.Server.Host project
dotnet list src/Honua.Server.Host/Honua.Server.Host.csproj reference --graph
```

### Count Namespace Cross-References

```bash
# Server.Core using Server.Host (should be 0)
find src -name "*.cs" -path "*/Honua.Server.Core/*" -exec grep -l "Honua\.Server\.Host\." {} \; | wc -l

# Server.Host using Server.Core (should be many)
find src -name "*.cs" -path "*/Honua.Server.Host/*" -exec grep -l "Honua\.Server\.Core\." {} \; | wc -l
```

## Related Documentation

- [Circular Dependency Analysis Report](./CIRCULAR_DEPENDENCY_ANALYSIS.md)
- [ADR-0003: Dependency Management](./ADR-0003-dependency-management.md)
- [Architecture Overview](../../README.md)

---

**Maintained By:** Development Team
**Review Frequency:** Quarterly or when significant changes occur
**Last Verified:** 2025-10-17
