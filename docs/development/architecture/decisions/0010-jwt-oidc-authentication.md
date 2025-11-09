# 10. JWT + OIDC Authentication Strategy

Date: 2025-10-17

Status: Accepted

## Context

Honua requires flexible authentication to support diverse deployment scenarios:
- **Development**: Quick start without identity infrastructure
- **Small Deployments**: Simple local user management
- **Enterprise**: Integration with corporate identity providers (Azure AD, Okta, Auth0)
- **Multi-tenant**: Delegation to external identity systems

**Requirements:**
- Support both internal and external authentication
- Role-based access control (RBAC)
- API key authentication for CLI/automation
- Backward compatibility with QuickStart mode
- Zero-trust security model for production

**Existing Evidence:**
- Three authentication modes: QuickStart, Local, OIDC
- JWT bearer validation: `/src/Honua.Server.Core/Authentication/JwtBearerOptionsConfigurator.cs`
- Local user store: `/src/Honua.Server.Core/Data/Auth/SqliteAuthRepository.cs`
- Configuration: `/src/Honua.Server.Core/Configuration/HonuaAuthenticationOptions.cs`
- Archived ADR: `/docs/archive/2025-10-15/architecture/ADR-0001-authentication-rbac.md`

## Decision

Implement **three-tier authentication strategy**:

1. **QuickStart Mode** (Development Only)
   - API key authentication
   - No user management
   - WARNING: Never use in production

2. **Local Mode** (Small Deployments)
   - Honua issues JWTs
   - Local user/password store (SQLite/PostgreSQL)
   - Argon2id password hashing
   - Built-in user management CLI

3. **OIDC Mode** (Enterprise)
   - Delegate to external identity provider
   - JWT bearer validation
   - Support Azure AD, Okta, Auth0, Keycloak, etc.
   - No local credential storage

**RBAC Roles:**
- `Administrator`: Full system access
- `DataPublisher`: Publish/edit data
- `Viewer`: Read-only access

## Consequences

### Positive

- **Flexibility**: Support all deployment scenarios
- **Security**: Industry-standard JWT/OIDC
- **Scalability**: OIDC for enterprise, local for small deployments
- **Simplicity**: QuickStart for development
- **Standards**: OAuth 2.0 / OpenID Connect compliance
- **Ecosystem**: Works with all OIDC providers

### Negative

- **Complexity**: Three modes to maintain and document
- **Security Risk**: QuickStart mode is insecure if misused
- **User Confusion**: Must choose correct mode
- **Testing**: All three modes need test coverage

### Neutral

- Local mode requires credential management
- OIDC mode requires external infrastructure

## Alternatives Considered

### 1. OIDC Only
**Verdict:** Rejected - too heavyweight for small deployments

### 2. API Keys Only
**Verdict:** Rejected - insufficient for RBAC and auditability

### 3. Built-in Identity Provider
**Verdict:** Rejected - too complex to maintain

## Implementation

**Configuration:**
```json
{
  "Honua": {
    "Authentication": {
      "Mode": "Oidc",  // "QuickStart", "Local", or "Oidc"
      "Enforce": true,
      "Oidc": {
        "Authority": "https://login.microsoftonline.com/tenant-id",
        "Audience": "api://honua",
        "RequireHttpsMetadata": true
      }
    }
  }
}
```

**Code Reference:**
- Auth setup: `/src/Honua.Server.Host/Extensions/AuthenticationExtensions.cs`
- JWT config: `/src/Honua.Server.Core/Authentication/JwtBearerOptionsConfigurator.cs`
- User store: `/src/Honua.Server.Core/Data/Auth/SqliteAuthRepository.cs`

## References

- [OpenID Connect Specification](https://openid.net/connect/)
- [RFC 7519: JSON Web Tokens](https://tools.ietf.org/html/rfc7519)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)

## Notes

See archived ADR-0001 for detailed authentication architecture design. This decision codifies the implemented authentication strategy.
