# Security & Identity Review – 2025-02-18

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Host/Authentication/**`, `.../Security/**`, CSRF middleware, trusted proxy handling, security validation hosted services, admin endpoints auth, `Honua.Server.Core.Authentication` primitives |
| Methods | Static analysis of authentication/authorization flows, CSRF & proxy trust logic, QuickStart toggles, and security configuration validators |

---

## High-Level Observations

- Auth stack supports JWT/OIDC, local basic, and QuickStart bypass. Policies flip between enforced and permissive modes; however QuickStart remains enabled in repo config and several guards rely on environment variables that aren’t documented for ops.
- SecurityPolicyMiddleware provides defense-in-depth but only logs; it doesn’t block GET/POST on custom routes that lack attributes if the path isn’t recognised. Relying on string heuristics is brittle.
- API key handler restricts to header usage and constant-time compare, but enabling it outside QuickStart is just an option flag with no rate limiting or rotation guidance.
- TrustedProxyValidator exists yet many helpers (`RequestLinkHelper`, `LocalBasicAuthenticationHandler`) fall back to trusting forwarded headers when validator isn’t registered/configured—common misconfig.
- CSRF middleware is robust for cookie auth but automatically bypasses API key requests. Admin endpoints use API keys as a recommended integration path—needs caution.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | QuickStart mode still defaulted (see `appsettings.QuickStart.json`) and enforced via env flag; SecurityPolicy doesn’t stop anonymous POSTs outside heuristics | `WebApplicationExtensions.ValidateQuickStartMode` only throws in production if env var missing; developers can forget to flip config before deploy. | Remove QuickStart config from repo default builds; require explicit compile-time symbol or environment gating with tests that fail if QuickStart enabled in production builds. **Update 2025‑02‑18:** Defaults now ship with Local auth, QuickStart requires Development or `QuickStart` environment plus explicit env flag. |
| 2 | **High** | SecurityPolicyMiddleware allows anonymous admin endpoints if path slightly differs (`/adminx/...`) or query-based actions; reliance on string matching allows bypass | `IsProtectedRoute` checks hardcoded prefixes/verbs (`SecurityPolicyMiddleware.cs:120-174`). Renaming route to `/management` or using POST `/api/datasets/publish` bypasses block. | Replace heuristics with default-deny: require `[Authorize]` on all endpoints except those explicitly marked `[AllowAnonymous]`; log + reject missing attributes regardless of path. Add analyzer to enforce policy at build time. **Update 2025‑02‑18:** Middleware now default-denies non-read operations unless allow-listed and warns on anonymous GETs; regression tests added. |
| 3 | **High** | Trusted proxy validation silently becomes permissive if validator not registered or config empty; `RequestLinkHelper.ShouldTrustForwardedHeaders` returns true when validator null | `RequestLinkHelper.cs:368-380` obtains validator with `GetService`; if null, it just trusts forwarded headers. | Fail closed: if validator or configuration missing, never trust forwarded headers. Surface warning during startup when proxies configured but validator absent.
| 4 | **Medium** | API key authentication allowed with `AllowInProductionMode` flag but no rate limiting, IP restrictions, or audit scope controls | `ApiKeyAuthenticationHandler` supports unlimited roles and logs success/failure only. | Require per-key rate limiting or integrate with existing throttling middleware; document rotation procedure; consider HMAC-based keys with expiry.
| 5 | **Medium** | CSRF protection bypassed for API keys; admin SPA relying on API key can be CSRF’d if browser stores key in local storage and sends via headers automatically | `CsrfValidationMiddleware.IsApiKeyAuthenticated` returns true if header present—not verifying client type. | Make bypass optional; require `CSRF-Bypass: true` claim on API key or disallow for browser contexts. Provide docs cautioning against storing API keys client-side.
| 6 | **Medium** | `MetadataAdministrationEndpointRouteBuilderExtensions` checks QuickStart mode via config but returns 403 rather than 401, leaking feature availability | `MetadataAdministrationEndpointRouteBuilderExtensions.cs:80-238` respond with forbidden messages for QuickStart; potential information disclosure. | Use 401 without detailing QuickStart status, or return generic error to avoid enumerating admin capabilities to unauthenticated clients. **Update 2025‑02‑18:** Admin metadata endpoints now short-circuit with `401 Unauthorized` whenever QuickStart auth is active. |
| 7 | **Low** | Production security validator logs warnings but doesn’t mark health status; operators might miss failure until logs reviewed | `ProductionSecurityValidationHostedService.cs:57-146`. | Integrate with health checks (fail liveness/readiness when validation fails) and expose metrics.
| 8 | **Low** | SecurityPolicyMiddleware logs warnings at runtime but lacks unit/integration tests ensuring coverage of new routes | No tests under `tests/Honua.Server.Host.Tests/Security` verifying policy outcomes for dynamic endpoints. | Add tests to ensure new admin/api routes require `[Authorize]` and policy denies missing ones.

---

## Suggested Follow-Ups

1. Move toward explicit authorization annotations everywhere; add Roslyn analyzer or ASP.NET endpoint convention to fail builds when attributes missing.
2. Harden proxy trust path: fail closed, require explicit config, and add startup warnings/metrics showing trust status.
3. Revisit QuickStart support—ship separate profile or developer-only packages so production artifacts cannot accidentally run insecure mode.
4. Expand API key story with rate limiting, per-key analytics, and rotation endpoints; document safe usage for integrations.
5. Ensure CSRF protection covers browser-based API key usage or clearly document limitations and alternatives (OAuth, PKCE).

---

## Tests / Verification

- `dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj --filter DataIngestionServiceTests`
- `dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj --filter FullyQualifiedName~Honua.Server.Host.Tests.Security.SecurityPolicyMiddlewareTests`
