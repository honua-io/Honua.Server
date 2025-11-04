# Honua Security Architecture

**Version**: 1.0
**Date**: 2025-10-06
**Status**: Active

---

## Overview

This document describes the security architecture of the Honua Geospatial Server, including security controls, defense-in-depth strategies, and design principles.

---

## Security Principles

### 1. Defense in Depth
Multiple layers of security controls protect against single point of failure:
- Network (HTTPS, rate limiting)
- Authentication (strong passwords, account lockout)
- Authorization (RBAC, endpoint policies)
- Input validation (whitelists, sanitization)
- Output encoding (secure error handling)
- Data protection (encryption, hashing)
- Logging & monitoring (audit trails)

### 2. Least Privilege
- Users have minimum permissions needed
- Database users restricted to required operations
- API endpoints require specific roles
- File system access limited to designated directories

### 3. Fail Secure
- Invalid configuration prevents startup in production
- Authentication failures result in access denial
- Missing permissions block operations
- Errors reveal minimal information

### 4. Secure by Default
- QuickStart mode disabled in production
- HTTPS redirection enabled
- Security headers applied automatically
- Rate limiting active by default

### 5. Separation of Concerns
- Authentication logic isolated
- Configuration separate from code
- Secrets in environment variables
- Security validation runs at startup

---

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                         CLIENT LAYER                         │
│           (Web Browser, Mobile App, GIS Desktop)             │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTPS/TLS 1.2+
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                    SECURITY BOUNDARY 1                       │
│                  (Reverse Proxy / WAF)                       │
│  • TLS Termination                                           │
│  • DDoS Protection                                           │
│  • IP Filtering                                              │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                   PRESENTATION LAYER                         │
│              (ASP.NET Core Middleware)                       │
│  ┌────────────────────────────────────────────────┐          │
│  │  1. Security Headers Middleware                │          │
│  │  2. HTTPS Redirection                          │          │
│  │  3. Exception Handler (Secure)                 │          │
│  │  4. Rate Limiting                              │          │
│  │  5. CORS                                       │          │
│  │  6. Authentication                             │          │
│  │  7. Authorization                              │          │
│  └────────────────────────────────────────────────┘          │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                    SECURITY BOUNDARY 2                       │
│                   (Authorization Layer)                      │
│  • JWT Token Validation                                      │
│  • Role-Based Access Control                                │
│  • Endpoint Policy Enforcement                              │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                    APPLICATION LAYER                         │
│              (Business Logic / Services)                     │
│  ┌────────────────────────────────────────────────┐          │
│  │  • OGC API (Features, Tiles, Records)         │          │
│  │  • OData Services                             │          │
│  │  • WFS/WMS Services                           │          │
│  │  • REST APIs                                  │          │
│  │  • OpenRosa/ODK                               │          │
│  └────────────────────────────────────────────────┘          │
│                                                              │
│  ┌────────────────────────────────────────────────┐          │
│  │  Security Controls:                            │          │
│  │  • Input Validation                            │          │
│  │  • Path Traversal Protection                   │          │
│  │  • File Upload Validation                      │          │
│  │  • Security Audit Logging                      │          │
│  └────────────────────────────────────────────────┘          │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                    SECURITY BOUNDARY 3                       │
│                      (Data Access)                           │
│  • Parameterized Queries Only                               │
│  • Connection String Encryption                             │
│  • Least Privilege DB Users                                 │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                      DATA LAYER                              │
│         (PostgreSQL / MySQL / SQL Server / SQLite)           │
│  • Encrypted at Rest (Deployment Responsibility)            │
│  • Network Isolation                                        │
│  • Backup Encryption                                        │
└─────────────────────────────────────────────────────────────┘
```

---

## Authentication Architecture

### Flow Diagram

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ 1. POST /api/auth/local/login
       │    {username, password}
       ▼
┌────────────────────────────────────┐
│    LocalAuthController             │
│  • Extracts IP + User-Agent        │
└──────┬─────────────────────────────┘
       │ 2. AuthenticateAsync()
       ▼
┌────────────────────────────────────┐
│  LocalAuthenticationService        │
│  • Checks mode = Local             │
│  • Gets credentials from DB        │
└──────┬─────────────────────────────┘
       │ 3. VerifyPassword()
       ▼
┌────────────────────────────────────┐
│     PasswordHasher (Argon2id)      │
│  • Time: 4 iterations              │
│  • Memory: 64MB                    │
│  • Salt: 16 bytes                  │
└──────┬─────────────────────────────┘
       │ 4. Valid? Check lockout
       ▼
┌────────────────────────────────────┐
│    Account Lockout Logic           │
│  • Max 5 failed attempts           │
│  • 30 min lockout window           │
└──────┬─────────────────────────────┘
       │ 5. Success → Generate JWT
       ▼
┌────────────────────────────────────┐
│      LocalTokenService             │
│  • Creates JWT with claims         │
│  • Expires in 60 minutes           │
│  • Signs with secret key           │
└──────┬─────────────────────────────┘
       │ 6. Log security event
       ▼
┌────────────────────────────────────┐
│    SecurityAuditLogger             │
│  • LogLoginSuccess()               │
│  • Records: user, IP, timestamp    │
└──────┬─────────────────────────────┘
       │ 7. Return token
       ▼
┌─────────────┐
│   Client    │
│ (stores JWT)│
└─────────────┘
```

### Security Controls

| Control | Implementation | Purpose |
|---------|----------------|---------|
| **Password Hashing** | Argon2id | Slow brute force attacks |
| **Account Lockout** | 5 attempts / 30 min | Prevent brute force |
| **Token Expiration** | 60 minutes | Limit compromise window |
| **Secure Transport** | HTTPS + HSTS | Prevent MitM attacks |
| **Audit Logging** | All auth events | Detection & investigation |

---

## Authorization Architecture

### Role-Based Access Control (RBAC)

```
┌───────────────────────────────────────────────────────────────┐
│                        ROLES                                   │
├───────────────────┬──────────────────┬────────────────────────┤
│  Administrator    │  DataPublisher   │       Viewer           │
│  (Full Access)    │  (Read + Write)  │    (Read Only)         │
└─────────┬─────────┴────────┬─────────┴──────────┬─────────────┘
          │                  │                    │
          ▼                  ▼                    ▼
┌─────────────────────────────────────────────────────────────┐
│                      PERMISSIONS                             │
├─────────────────────┬─────────────────┬──────────────────────┤
│ • Manage Users      │ • Ingest Data   │ • View Layers        │
│ • Configure System  │ • Edit Features │ • Query Features     │
│ • Manage Layers     │ • Upload Files  │ • Download Exports   │
│ • View Audit Logs   │ • Manage Layers │ • View Maps          │
└─────────────────────┴─────────────────┴──────────────────────┘
```

### Enforcement Points

1. **Endpoint Level** (via `[Authorize]` attribute)
   ```csharp
   .RequireAuthorization("RequireAdministrator")
   .RequireAuthorization("RequireDataPublisher")
   ```

2. **Method Level** (via policy checks)
   ```csharp
   if (!User.IsInRole("Administrator"))
       return Forbid();
   ```

3. **Resource Level** (via ownership checks)
   ```csharp
   if (feature.CreatedBy != currentUserId)
       return Forbid();
   ```

---

## Input Validation Architecture

### Defense Layers

```
┌─────────────────────────────────────────────────────────────┐
│                    INPUT VALIDATION                          │
│                                                              │
│  Layer 1: Protocol Validation                               │
│  ├─ Request size limits (1GB max)                           │
│  ├─ Content-Type validation                                 │
│  └─ HTTP method whitelist                                   │
│                                                              │
│  Layer 2: Format Validation                                 │
│  ├─ JSON schema validation                                  │
│  ├─ Model binding validation                                │
│  └─ Data type checking                                      │
│                                                              │
│  Layer 3: Business Logic Validation                         │
│  ├─ Range checks                                            │
│  ├─ Enum validation                                         │
│  └─ Cross-field validation                                  │
│                                                              │
│  Layer 4: Security Validation                               │
│  ├─ SQL injection prevention (parameterized queries)        │
│  ├─ Path traversal prevention (path normalization)          │
│  ├─ XSS prevention (output encoding)                        │
│  ├─ File extension whitelist                                │
│  └─ MIME type validation                                    │
└─────────────────────────────────────────────────────────────┘
```

### File Upload Security

```
Client Upload
     │
     ▼
┌─────────────────────┐
│ Size Check (1GB)    │
└──────┬──────────────┘
       ▼
┌─────────────────────┐
│ Extension Whitelist │
│ (.shp, .geojson,    │
│  .gpkg, .zip, etc)  │
└──────┬──────────────┘
       ▼
┌─────────────────────┐
│ MIME Type Check     │
└──────┬──────────────┘
       ▼
┌─────────────────────┐
│ Filename Sanitize   │
│ (GUID generation)   │
└──────┬──────────────┘
       ▼
┌─────────────────────┐
│ Path Validation     │
│ (prevent traversal) │
└──────┬──────────────┘
       ▼
┌─────────────────────┐
│ Stream to Storage   │
│ (S3/FS/Azure)       │
└─────────────────────┘
```

---

## Rate Limiting Architecture

### Configuration

| Endpoint Type | Limit | Window | Queue | Purpose |
|---------------|-------|--------|-------|---------|
| **Default** | 100 req | 1 min | 10 | General protection |
| **OGC API** | 200 req | 1 min | 20 | High-traffic endpoints |
| **OpenRosa** | 50 req | 1 min | 5 | Mobile data collection |
| **Admin** | 20 req | 5 min | 2 | Sensitive operations |

### Algorithm

```
Sliding Window Rate Limiter
┌─────────────────────────────────────────────────────────┐
│  Time Window: 60 seconds                                │
│  Segments: 4 (15 seconds each)                          │
│  Limit: 100 requests                                    │
│                                                          │
│  [Segment 1] [Segment 2] [Segment 3] [Segment 4]        │
│     25 req      25 req      25 req      25 req          │
│                                                          │
│  As time progresses, oldest segment drops off           │
│  New segment added with current request count           │
└─────────────────────────────────────────────────────────┘
```

---

## Logging & Monitoring Architecture

### Security Events Logged

```
┌──────────────────────────────────────────────────────────────┐
│                   SECURITY AUDIT LOG                         │
│                                                              │
│  Authentication Events:                                      │
│  ├─ Login success (user, IP, user-agent, timestamp)         │
│  ├─ Login failure (user, IP, reason, timestamp)             │
│  ├─ Account lockout (user, IP, locked-until)                │
│  └─ Logout (user, timestamp)                                │
│                                                              │
│  Authorization Events:                                       │
│  ├─ Unauthorized access attempt (user, resource, IP)        │
│  ├─ Privilege escalation attempt                            │
│  └─ Role changes (admin, target-user, old-roles, new-roles) │
│                                                              │
│  Admin Operations:                                           │
│  ├─ User management (create, update, delete)                │
│  ├─ Layer operations (create, update, delete)               │
│  ├─ Configuration changes (key, old-value, new-value)       │
│  └─ System operations (backup, restore, migration)          │
│                                                              │
│  Data Access:                                                │
│  ├─ Bulk exports (user, layer, record-count)                │
│  ├─ Sensitive data access (user, resource-type, resource-id)│
│  └─ Data modifications (user, operation, resource)          │
│                                                              │
│  Security Events:                                            │
│  ├─ Rate limit violations (IP, endpoint, timestamp)         │
│  ├─ Suspicious activity (type, details, IP)                 │
│  ├─ Configuration validation failures                        │
│  └─ Exception patterns                                       │
└──────────────────────────────────────────────────────────────┘
```

### Log Format (Structured JSON)

```json
{
  "timestamp": "2025-10-06T12:00:00.000Z",
  "level": "Warning",
  "category": "Security.Audit",
  "event": "LoginFailure",
  "username": "admin",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0...",
  "reason": "invalid_credentials",
  "attemptNumber": 3,
  "maxAttempts": 5
}
```

---

## Data Protection Architecture

### Data at Rest

| Data Type | Encryption | Storage | Responsibility |
|-----------|-----------|---------|----------------|
| **Passwords** | Argon2id hash | Database | Application |
| **JWT Secrets** | Environment var | OS keystore | Deployment |
| **Database** | TDE/at-rest | Database | DBA |
| **File Uploads** | Optional | S3/Azure | Deployment |
| **Backups** | Encrypted | Storage | DBA |

### Data in Transit

- **Client ↔ Server**: TLS 1.2+ (HTTPS enforced)
- **Server ↔ Database**: TLS/SSL (configurable)
- **Server ↔ S3/Azure**: HTTPS (mandatory)

### Sensitive Data Handling

```
┌──────────────────────────────────────────────────────────┐
│          SENSITIVE DATA REDACTION (Logging)              │
│                                                          │
│  Connection Strings:                                     │
│    Before: "Server=db;User=admin;Password=secret123"    │
│    After:  "Server=db;User=admin;Password=***REDACTED***│
│                                                          │
│  API Keys:                                               │
│    Before: "ApiKey=sk_live_abc123def456"                │
│    After:  "ApiKey=***REDACTED***"                      │
│                                                          │
│  Authorization Headers:                                  │
│    Before: "Authorization: Bearer eyJhbG..."            │
│    After:  "Authorization: ***REDACTED***"              │
│                                                          │
│  AWS Credentials:                                        │
│    Before: "AKIAIOSFODNN7EXAMPLE"                       │
│    After:  "***REDACTED_AWS_KEY***"                     │
└──────────────────────────────────────────────────────────┘
```

---

## Configuration Security

### Security Configuration Validator

```
Startup Validation
       │
       ▼
┌────────────────────────┐
│ Production Check?      │
└────┬────────────────┬──┘
     │ No             │ Yes
     ▼                ▼
 [WARN]          [ERROR + FAIL]
     │                │
     ▼                ▼
┌────────────────────────────────────┐
│  Validation Checks:                │
│  ├─ Metadata path configured?      │
│  ├─ Metadata provider valid?       │
│  ├─ Services properly configured?  │
│  └─ No QuickStart in production?   │
└────────────────────────────────────┘
       │
       ▼
┌────────────────────────┐
│  Validation Result     │
│  ├─ Errors: Block Start│
│  ├─ Warnings: Log Only │
│  └─ Success: Continue  │
└────────────────────────┘
```

### Environment-Specific Security

| Setting | Development | Production |
|---------|-------------|------------|
| **HTTPS** | Optional | ✅ Enforced |
| **HSTS** | Disabled | ✅ Enabled |
| **QuickStart** | Allowed | ❌ Blocked |
| **Error Details** | Verbose | Generic only |
| **Stack Traces** | In response | Server-side only |
| **Validation** | Warnings | Errors block startup |

---

## Deployment Security Checklist

### Pre-Deployment

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] JWT secret key is 256+ bits, stored in environment variable
- [ ] Database credentials in environment variables
- [ ] QuickStart mode disabled
- [ ] TLS certificates valid and configured
- [ ] Security headers enabled
- [ ] Rate limiting configured
- [ ] CORS origins explicitly whitelisted
- [ ] Run `./scripts/security-test.sh`

### Post-Deployment

- [ ] HTTPS redirect working (`curl -I http://domain.com`)
- [ ] HSTS header present (`curl -I https://domain.com | grep Strict`)
- [ ] Authentication requires valid credentials
- [ ] Rate limiting triggers after threshold
- [ ] Audit logs being generated
- [ ] Error messages are generic
- [ ] security.txt accessible

---

## Security Metrics & KPIs

### Monitoring Targets

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| **Failed Login Rate** | < 5% | > 10% |
| **Account Lockouts** | < 10/day | > 50/day |
| **Rate Limit Hits** | < 1% | > 5% |
| **401/403 Responses** | < 2% | > 10% |
| **Average Response Time** | < 200ms | > 1000ms |
| **Vulnerability Count** | 0 | > 0 |

### Security Health Dashboard

```
┌─────────────────────────────────────────────────────────┐
│               SECURITY HEALTH SCORE                      │
│                                                          │
│  Authentication Security:          ████████░░ 85%       │
│  ├─ Strong passwords enforced      ✅                   │
│  ├─ Account lockout active         ✅                   │
│  ├─ MFA enabled                    ❌ (Phase 3)         │
│  └─ Token security                 ✅                   │
│                                                          │
│  Authorization Security:           ██████████ 100%      │
│  ├─ RBAC implemented               ✅                   │
│  ├─ Endpoint policies              ✅                   │
│  └─ Resource ownership             ✅                   │
│                                                          │
│  Network Security:                 ████████░░ 90%       │
│  ├─ HTTPS enforced                 ✅                   │
│  ├─ HSTS enabled                   ✅                   │
│  ├─ Rate limiting active           ✅                   │
│  └─ DDoS protection                ⚠️  (Deploy-level)   │
│                                                          │
│  Data Security:                    ████████░░ 85%       │
│  ├─ Encryption in transit          ✅                   │
│  ├─ Secure password storage        ✅                   │
│  ├─ Input validation               ✅                   │
│  └─ Database encryption            ⚠️  (DB-level)       │
│                                                          │
│  Logging & Monitoring:             ████████░░ 80%       │
│  ├─ Audit logging enabled          ✅                   │
│  ├─ Security events tracked        ✅                   │
│  ├─ Centralized logging            ❌ (Phase 3)         │
│  └─ Real-time alerting             ❌ (Phase 3)         │
│                                                          │
│  Overall Security Score:  ████████░░ 88%                │
└─────────────────────────────────────────────────────────┘
```

---

## Incident Response Plan

### Phases

1. **Detection**: Audit logs, monitoring alerts, user reports
2. **Containment**: Disable accounts, block IPs, isolate systems
3. **Investigation**: Analyze logs, identify scope, determine root cause
4. **Eradication**: Patch vulnerabilities, remove malicious code, reset credentials
5. **Recovery**: Restore from backups, verify integrity, resume operations
6. **Post-Mortem**: Document lessons, update procedures, notify stakeholders

### Emergency Contacts

- **Security Lead**: security@honua.io
- **On-Call Engineer**: (configured per deployment)
- **Escalation**: (configured per deployment)

---

## References

- Threat Model: `docs/security/THREAT_MODEL.md`
- OWASP Assessment: `docs/security/OWASP_TOP_10_ASSESSMENT.md`
- Deployment Checklist: `docs/security/PRODUCTION_DEPLOYMENT_CHECKLIST.md`
- Security Policy: `SECURITY.md`

---

*Security Architecture v1.0 - Honua Geospatial Server*
*Last Updated: 2025-10-06*
