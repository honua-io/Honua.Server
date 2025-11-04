# ADR-0001: Authentication and RBAC Architecture

## Status
Proposed

## Context
Honua needs a cohesive security foundation that covers API authentication, role-based authorization, and operational workflows without introducing heavyweight identity platform dependencies. Phase 0 must support:
- OIDC JWT bearer validation against external identity providers.
- A lightweight local credential store for air-gapped or small deployments.
- A deterministic bootstrap path for the first administrator account.
- Shared RBAC enforcement across admin and publishing surfaces.
- Operational tooling (CLI) for user and role management with auditability.

Earlier design notes describe OIDC bearer setup and an RBAC/local users MVP, but lacked an integrated plan for data schema, bootstrap flow, CLI tooling, and observability.

## Decision
1. **Authentication Modes**: Support mutually exclusive modes configured under `Honua:Authentication:Mode`:
   - `Oidc`: ASP.NET Core JWT bearer middleware validates tokens via configured authority/audience and optional HTTPS metadata. Role claims are read from a configurable claim path (default `roles`).
   - `Local`: Honua issues its own JWTs for CLI/admin sessions after validating usernames/passwords stored in the Honua auth store. Local JWTs are signed with a managed key.
   - `QuickStart`: Development-only API key mode remains available but disabled automatically when `Oidc` or `Local` is enforced.

2. **RBAC**: Fix a three-role catalogue (`Administrator`, `DataPublisher`, `Viewer`). Authorization policies are registered centrally (`AuthorizationRegistry`) and applied to admin APIs, Hangfire dashboard, observability endpoints, and publishing workflows. Role resolution merges external claims with local assignments tied to user subject/username.

3. **Data Model & Storage**: Introduce `auth.Users`, `auth.Roles`, `auth.UserRoles`, `auth.CredentialsAudit`, and `auth.BootstrapState` tables. Password hashes store algorithm metadata (Argon2id defaults) per user to support future upgrades. Role rows are seeded and protected from mutation outside controlled migrations. The canonical Phase 0 storage is a dedicated SQLite database (`data/auth/auth.db`) that ships with Honua; the same schema definition applies to Postgres/SQL Server deployments once metadata moves to relational storage.

4. **Bootstrap**: Provide an idempotent `honua auth bootstrap` CLI command (and optional host startup hook) that ensures migrations are applied, seeds roles, and provisions the initial administrator:
   - Local mode either uses an operator-supplied password or generates a random credential, emits it once to console, and optionally writes an encrypted/permission-locked bootstrap file.
   - OIDC mode requires mapping an IdP subject/claim to the admin account without storing a password.
   - Completion is recorded in `auth.BootstrapState` to prevent duplicate bootstrap.

5. **Credentials & Security Controls**:
   - Passwords hashed with Argon2id (preferred) or PBKDF2 fallback using per-user salts stored alongside algorithm metadata.
   - Login attempts are throttled with lockout/backoff counters; audit events and metrics emit structured telemetry for success/failure.
   - Local JWT signing keys are stored in configuration providers and rotated via CLI tooling.
   - All admin endpoints require HTTPS; responses standardize 401/403 messages to avoid user enumeration.

6. **Tooling & Observability**:
   - CLI commands manage users, passwords, roles, and lockouts while emitting audit records.
   - Metrics such as `auth_login_failures_total` and structured logs provide visibility. Health checks verify OIDC discovery endpoints and bootstrap completion state.

## Consequences
- We avoid bundling or maintaining an embedded identity provider (IdentityServer, Keycloak), reducing operational surface area.
- Database schema and CLI tooling work together to give operators deterministic bootstrap and rotation workflows while keeping storage lightweight (single SQLite file) for file-based deployments.
- Additional work is required to implement migrations, services, and middleware, but changes stay within our existing stack (ASP.NET Core + simple repository abstraction).
- Future enhancements (MFA, custom roles, external IdP integrations) can build on top of this foundation by extending schema and CLI commands.
- Secure defaults (Argon2id hashing, lockouts, logging) raise the security baseline without significant complexity for operators.

## Implementation Notes
- Store configuration mappings and examples in documentation under `docs/authentication` (to be added) with quick-start guides.
- Ensure tests cover bootstrap idempotency, hashing, role resolution, and enforcement boundaries before enabling enforcement by default.
- Bundle the SQLite auth database creation with bootstrap tooling; document how to override the storage path when deployments choose Postgres/SQL Server instead.
- Update container images to run bootstrap command (or instruct operators) during deployment pipelines.
