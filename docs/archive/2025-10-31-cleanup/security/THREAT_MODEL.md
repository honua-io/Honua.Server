# Honua Geospatial Server - Threat Model

**Version**: 1.0
**Date**: 2025-10-06
**Methodology**: STRIDE (Spoofing, Tampering, Repudiation, Information Disclosure, Denial of Service, Elevation of Privilege)
**Scope**: Honua Server Core + Host (Production Deployment)

---

## Executive Summary

This threat model identifies potential security threats to the Honua Geospatial Server using the STRIDE methodology. It covers authentication, data access, API endpoints, and infrastructure components.

**Risk Level Summary**:
- **Critical**: 0
- **High**: 3 (mitigated)
- **Medium**: 8 (6 mitigated, 2 require monitoring)
- **Low**: 12 (all mitigated or accepted)

---

## System Architecture

### Components

1. **API Layer**
   - OGC API (Features, Tiles, Records)
   - OData endpoints
   - WFS/WMS services
   - REST APIs (Collections, Layers, Attachments)
   - OpenRosa/ODK endpoints

2. **Authentication Layer**
   - Local authentication (username/password + Argon2id)
   - JWT bearer tokens
   - QuickStart mode (development only)
   - OIDC support (planned)

3. **Data Layer**
   - PostgreSQL/MySQL/SQL Server/SQLite providers
   - Enterprise providers (Snowflake, Redshift, BigQuery, CosmosDB, MongoDB)
   - File attachments (FileSystem/S3/Azure Blob/Database)
   - Raster tile cache (FileSystem/S3/Azure)

4. **Processing Layer**
   - Data ingestion (Shapefile, GeoJSON, GeoPackage, etc.)
   - Geometry operations
   - Tile generation
   - Export (GeoPackage, Shapefile, CSV)

---

## STRIDE Analysis

### 1. Spoofing Identity

#### T1.1: Credential Theft
**Threat**: Attacker steals user credentials through phishing or brute force
- **STRIDE**: Spoofing
- **Impact**: High (full account access)
- **Likelihood**: Medium
- **Risk**: HIGH

**Mitigations**:
- ✅ Strong password requirements (12+ chars, complexity)
- ✅ Argon2id password hashing (slow, memory-hard)
- ✅ Account lockout after 5 failed attempts
- ✅ Login attempt logging with IP address
- ⚠️ TODO: Add MFA support (Phase 3)

**Status**: Mitigated (current), Enhanced mitigations planned

---

#### T1.2: JWT Token Theft
**Threat**: Attacker intercepts or steals JWT tokens
- **STRIDE**: Spoofing
- **Impact**: High (session hijacking)
- **Likelihood**: Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ HTTPS enforced in production
- ✅ HSTS headers (max-age=31536000)
- ✅ Short token expiration (60 minutes default)
- ✅ HttpOnly cookies (if using cookie-based auth)
- ✅ Secure token storage guidance in docs

**Status**: Mitigated

---

#### T1.3: QuickStart Mode Exploitation
**Threat**: QuickStart mode enabled in production bypasses all authentication
- **STRIDE**: Spoofing, Elevation of Privilege
- **Impact**: Critical (complete system compromise)
- **Likelihood**: Very Low (prevented by design)
- **Risk**: HIGH (if misconfigured)

**Mitigations**:
- ✅ QuickStart mode blocked in production environment
- ✅ Application fails to start if QuickStart enabled in production
- ✅ Security configuration validator checks at startup
- ✅ Loud logging warnings if QuickStart enabled
- ✅ Documentation warnings

**Status**: Mitigated

---

### 2. Tampering

#### T2.1: SQL Injection
**Threat**: Attacker injects malicious SQL through user input
- **STRIDE**: Tampering
- **Impact**: Critical (data breach, corruption)
- **Likelihood**: Very Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ 100% parameterized queries across all providers
- ✅ OData query validation
- ✅ Input validation on all endpoints
- ✅ Principle of least privilege (database users)
- ✅ Security tests for injection attempts

**Status**: Mitigated

---

#### T2.2: Path Traversal in File Operations
**Threat**: Attacker accesses files outside intended directories
- **STRIDE**: Tampering, Information Disclosure
- **Impact**: High (arbitrary file access)
- **Likelihood**: Very Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ Path validation in FileSystemAttachmentStore
- ✅ Sanitized filenames (GUID-based)
- ✅ File extension whitelist
- ✅ Root path boundary checks
- ✅ Security tests for path traversal

**Status**: Mitigated

---

#### T2.3: Malicious File Upload
**Threat**: Attacker uploads executable or malicious files
- **STRIDE**: Tampering, Denial of Service
- **Impact**: Medium (server compromise if executed)
- **Likelihood**: Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ File extension whitelist (.shp, .geojson, .gpkg, .zip, etc.)
- ✅ 1GB file size limit
- ✅ MIME type validation
- ✅ Files not executed on server
- ✅ Separate storage from application code
- ⚠️ TODO: Add virus scanning (optional, Phase 3)

**Status**: Mitigated

---

### 3. Repudiation

#### T3.1: Lack of Audit Trail
**Threat**: Attackers or malicious insiders perform actions without detection
- **STRIDE**: Repudiation
- **Impact**: Medium (incident investigation difficult)
- **Likelihood**: Medium
- **Risk**: MEDIUM

**Mitigations**:
- ✅ Security audit logging for all auth events
- ✅ Login success/failure tracking
- ✅ Admin operation logging
- ✅ IP address and user agent tracking
- ✅ Structured logging (JSON)
- ⚠️ TODO: Centralized log aggregation (deployment-specific)

**Status**: Mitigated (logging in place), Enhancement needed (centralization)

---

### 4. Information Disclosure

#### T4.1: Verbose Error Messages
**Threat**: Error messages reveal sensitive system information
- **STRIDE**: Information Disclosure
- **Impact**: Low (aids reconnaissance)
- **Likelihood**: Medium
- **Risk**: MEDIUM

**Mitigations**:
- ✅ Secure exception handler sanitizes errors in production
- ✅ Generic error messages only
- ✅ Stack traces logged server-side only
- ✅ Server header removed
- ✅ X-Powered-By header removed

**Status**: Mitigated

---

#### T4.2: Insecure Direct Object References
**Threat**: Attackers access resources by manipulating IDs
- **STRIDE**: Information Disclosure, Tampering
- **Impact**: Medium (unauthorized data access)
- **Likelihood**: Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ Authorization checks on all endpoints
- ✅ Feature ownership validation
- ✅ Layer-level permissions
- ⚠️ TODO: Row-level security for multi-tenant (Phase 3)
- ⚠️ TODO: OData filter capability audit

**Status**: Partially Mitigated (requires multi-tenant enhancements)

---

#### T4.3: Sensitive Data in Logs
**Threat**: Passwords, tokens, or PII leaked in logs
- **STRIDE**: Information Disclosure
- **Impact**: High (credential exposure)
- **Likelihood**: Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ SensitiveDataRedactor for connection strings
- ✅ Passwords never logged
- ✅ API keys redacted
- ✅ Authorization headers redacted
- ✅ Configuration validation prevents secrets in config files

**Status**: Mitigated

---

### 5. Denial of Service

#### T5.1: API Rate Limit Bypass
**Threat**: Attacker overwhelms server with requests
- **STRIDE**: Denial of Service
- **Impact**: High (service unavailable)
- **Likelihood**: Medium
- **Risk**: HIGH

**Mitigations**:
- ✅ Rate limiting middleware (100-200 req/min)
- ✅ Sliding window algorithm
- ✅ Per-endpoint limits (OGC: 200/min, Admin: 20/5min)
- ✅ 429 status with Retry-After header
- ⚠️ TODO: IP-based blocking for persistent abuse

**Status**: Mitigated (basic protection), Enhancement recommended

---

#### T5.2: Resource Exhaustion via Large Uploads
**Threat**: Attacker uploads huge files to exhaust disk/memory
- **STRIDE**: Denial of Service
- **Impact**: High (service disruption)
- **Likelihood**: Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ 1GB file size limit
- ✅ Request body size limits (Kestrel)
- ✅ Streaming file processing
- ✅ Temporary file cleanup
- ✅ Disk space monitoring (deployment responsibility)

**Status**: Mitigated

---

#### T5.3: Complex Query DoS
**Threat**: Expensive OData/WFS queries consume excessive resources
- **STRIDE**: Denial of Service
- **Impact**: Medium (performance degradation)
- **Likelihood**: Medium
- **Risk**: MEDIUM

**Mitigations**:
- ✅ OData page size limits (default 100, max 1000)
- ✅ Query timeout settings
- ✅ Rate limiting on query endpoints
- ⚠️ TODO: Query complexity analysis
- ⚠️ TODO: Resource usage monitoring

**Status**: Partially Mitigated (requires monitoring)

---

### 6. Elevation of Privilege

#### T6.1: Role Bypass
**Threat**: Attacker gains admin privileges without authorization
- **STRIDE**: Elevation of Privilege
- **Impact**: Critical (full system access)
- **Likelihood**: Very Low
- **Risk**: HIGH

**Mitigations**:
- ✅ RBAC with 3 roles (Administrator, DataPublisher, Viewer)
- ✅ Authorization policies on all admin endpoints
- ✅ JWT claims validation
- ✅ No role escalation endpoints
- ✅ Admin operations logged

**Status**: Mitigated

---

#### T6.2: GDAL Command Injection
**Threat**: Malicious file exploits GDAL to execute commands
- **STRIDE**: Elevation of Privilege, Tampering
- **Impact**: Critical (remote code execution)
- **Likelihood**: Very Low
- **Risk**: MEDIUM

**Mitigations**:
- ✅ File extension whitelist
- ✅ GDAL runs without shell access
- ✅ Application runs as non-root user
- ✅ Separate process isolation
- ⚠️ TODO: GDAL security audit (manual review needed)

**Status**: Partially Mitigated (requires manual code review)

---

## Attack Scenarios

### Scenario 1: External Attacker - Anonymous
**Goal**: Gain unauthorized access or disrupt service

**Attack Path**:
1. Attempt brute force login → ❌ Blocked by account lockout
2. Try SQL injection → ❌ Blocked by parameterized queries
3. Upload malicious file → ❌ Blocked by extension whitelist
4. DoS via rapid requests → ❌ Blocked by rate limiting
5. Exploit verbose errors → ❌ Blocked by secure exception handler

**Conclusion**: Strong perimeter defenses

---

### Scenario 2: Malicious Insider - Authenticated User
**Goal**: Access data outside authorization

**Attack Path**:
1. Attempt to access admin endpoints → ❌ Blocked by RBAC
2. Try to read other users' data → ⚠️ Possible if multi-tenant not enforced
3. Modify layer definitions → ❌ Blocked by role check (needs DataPublisher)
4. Delete attachments → ✅ Allowed if owns the feature

**Conclusion**: Good role separation, multi-tenant isolation needs enhancement

---

### Scenario 3: Compromised Admin Account
**Goal**: Maximum damage / data exfiltration

**Attack Path**:
1. Access all layers and features → ✅ Possible (authorized)
2. Export entire database → ✅ Possible (authorized)
3. Delete all data → ✅ Possible (authorized)
4. Actions logged → ✅ Yes, audit trail captured

**Mitigation**:
- ✅ Audit logging enables detection
- ⚠️ TODO: Add critical operation confirmations
- ⚠️ TODO: Add backup/recovery procedures

---

## Data Flow Diagrams

### Authentication Flow
```
[Client] → [HTTPS] → [Rate Limiter] → [Auth Controller]
                                            ↓
                                    [Password Hasher]
                                            ↓
                                    [Auth Repository]
                                            ↓
                                    [SQLite/PostgreSQL]
                                            ↓
                                    [JWT Token Service]
                                            ↓
                                    [Security Audit Logger]
                                            ↓
                                    [Client (with token)]
```

**Threats**:
- T1.1: Credential theft → Mitigated (Argon2id, lockout)
- T1.2: Token theft → Mitigated (HTTPS, short expiration)
- T3.1: Repudiation → Mitigated (audit logging)

---

### Data Access Flow
```
[Client] → [HTTPS] → [Rate Limiter] → [Authorization Middleware]
                                            ↓
                                    [API Controller]
                                            ↓
                                    [Feature Repository]
                                            ↓
                                    [Data Store Provider]
                                            ↓
                                    [Database] ← (Parameterized Query)
```

**Threats**:
- T2.1: SQL injection → Mitigated (parameterized queries)
- T4.2: IDOR → Partially mitigated (needs row-level security)
- T6.1: Privilege escalation → Mitigated (RBAC)

---

### File Upload Flow
```
[Client] → [HTTPS] → [Rate Limiter] → [Authorization Check]
                                            ↓
                                    [Size Validation (1GB)]
                                            ↓
                                    [Extension Whitelist]
                                            ↓
                                    [Filename Sanitization]
                                            ↓
                                    [Storage Provider] (S3/FS/Azure)
                                            ↓
                                    [Attachment Repository]
```

**Threats**:
- T2.2: Path traversal → Mitigated (path validation)
- T2.3: Malicious upload → Mitigated (whitelist, size limit)
- T5.2: Resource exhaustion → Mitigated (size limits)

---

## Trust Boundaries

### External → DMZ
- **Boundary**: Internet → Reverse Proxy/Load Balancer
- **Controls**: TLS termination, DDoS protection, rate limiting
- **Threats**: Network attacks, brute force
- **Status**: Deployment-dependent (not in application scope)

### DMZ → Application
- **Boundary**: Reverse Proxy → Honua Server
- **Controls**: HTTPS enforcement, HSTS, rate limiting
- **Threats**: Man-in-the-middle, session hijacking
- **Status**: ✅ Mitigated

### Application → Database
- **Boundary**: Honua Server → PostgreSQL/MySQL/etc.
- **Controls**: Connection encryption, parameterized queries, least privilege
- **Threats**: SQL injection, credential theft
- **Status**: ✅ Mitigated

### Application → External Services
- **Boundary**: Honua Server → S3/Azure/External APIs
- **Controls**: IAM roles, API keys (environment variables), TLS
- **Threats**: Credential exposure, SSRF
- **Status**: ✅ Mitigated (no user-controlled URLs)

---

## Assumptions and Dependencies

### Assumptions
1. Application deployed behind reverse proxy (nginx, Caddy, or cloud LB)
2. TLS certificates properly configured and renewed
3. Database credentials stored in environment variables
4. Application runs as non-root user
5. Operating system and dependencies patched
6. Network firewall restricts database access

### External Dependencies
1. **.NET Runtime**: Security updates from Microsoft
2. **NuGet Packages**: Dependabot monitoring for vulnerabilities
3. **Database Servers**: Vendor security patches
4. **Cloud Providers**: AWS/Azure security controls
5. **GDAL Library**: Community security updates

**Mitigation**:
- ✅ Automated dependency scanning (Dependabot, CodeQL)
- ✅ SBOM generation for supply chain transparency
- ⚠️ TODO: Automated update notifications

---

## Residual Risks

### Accepted Risks (Low Priority)
1. **GDAL command injection**: Requires manual code audit (Phase 3)
2. **Multi-tenant row-level security**: Not required for single-tenant deployments
3. **Virus scanning**: Optional feature, deployment-specific
4. **MFA**: Planned for Phase 3

### Monitoring Required
1. **OData filter complexity**: Monitor query performance in production
2. **Rate limit tuning**: Adjust based on traffic patterns
3. **Disk space**: Monitor upload storage consumption

---

## Security Testing Recommendations

### Continuous Testing
- ✅ Automated dependency scanning (Dependabot)
- ✅ Static analysis (CodeQL)
- ✅ Unit tests for security controls
- ⏳ Abuse case tests (in progress)

### Manual Testing (Quarterly)
- OWASP ZAP active scan
- Manual penetration testing
- Configuration review
- Access control audit

### Before Production
- Load testing with rate limiting
- Fail-over testing
- Backup restoration testing
- Security header verification

---

## Threat Model Maintenance

**Review Schedule**: Quarterly or when:
- New features added
- Architecture changes
- New external dependencies
- Security incident occurs

**Next Review**: 2026-01-06

**Version History**:
- v1.0 (2025-10-06): Initial threat model

---

## References

- [OWASP Top 10 2021](https://owasp.org/Top10/)
- [STRIDE Threat Modeling](https://learn.microsoft.com/en-us/azure/security/develop/threat-modeling-tool-threats)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)
- Honua OWASP Assessment: `docs/security/OWASP_TOP_10_ASSESSMENT.md`
- Honua Security Policy: `SECURITY.md`

---

*Threat Model v1.0 - Honua Geospatial Server*
