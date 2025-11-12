# 11. ASP.NET Core Middleware-based Request Pipeline

Date: 2025-10-17

Status: Accepted

## Context

Honua is built as a web service that handles OGC API requests, WFS/WMS requests, and administrative operations. We need a request processing architecture that supports:
- Multiple API standards (OGC API, WFS, WMS, Geoservices REST a.k.a. Esri REST)
- Authentication and authorization
- Content negotiation (GeoJSON, KML, CSV, etc.)
- Request logging and tracing
- Error handling and standardized responses
- CORS for web clients

## Decision

Use **ASP.NET Core middleware pipeline** with endpoint routing for all HTTP request processing.

**Architecture:**
```
Request → Middleware Pipeline → Endpoint Routing → Controllers/Handlers
  ├─ CORS
  ├─ Authentication
  ├─ Authorization
  ├─ Request Logging
  ├─ Exception Handling
  └─ Response Compression
```

**Benefits:**
- Industry-standard .NET web framework
- Minimal API support for lightweight endpoints
- Built-in dependency injection
- OpenTelemetry instrumentation
- Rich ecosystem (Swagger, versioning, etc.)

## Consequences

### Positive

- **Mature Framework**: Well-tested, production-ready
- **Performance**: High-throughput, low-latency
- **Ecosystem**: Large library of middleware and tools
- **Cloud-Native**: Works with all cloud platforms
- **Developer Experience**: Excellent tooling and IDE support

### Negative

- **Framework Coupling**: Tied to ASP.NET Core lifecycle
- **Complexity**: Many features that may not be needed
- **Breaking Changes**: Major version upgrades can be disruptive

## Alternatives Considered

**Self-hosted HTTP listener**: Rejected - reinventing the wheel
**FastEndpoints**: Rejected - adds abstraction over ASP.NET Core
**gRPC**: Rejected - not suitable for REST APIs

## Code Reference

- Program.cs: `/src/Honua.Server.Host/Program.cs`
- Extensions: `/src/Honua.Server.Host/Extensions/`

## Notes

ASP.NET Core is the obvious choice for .NET web services. This ADR documents the decision for completeness.
