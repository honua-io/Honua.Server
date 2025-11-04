# Cross-Cutting Pass: Security Hardening – 2025-02-18

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | Trusted proxy validation, CSRF middleware, QuickStart safeguards, API key usage, alert receiver auth, CLI secret handling |
| Methods | Static analysis; no runtime checks |

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | Trusted proxy validator optional; many helpers fall back to trusting forwarded headers when validator missing | `RequestLinkHelper.ShouldTrustForwardedHeaders` trusts forwarded headers if validator missing (null). | Fail closed: if validator absent, never trust forwarded headers. Add startup check to ensure config present when behind proxy.
| 2 | **High** | Alert receiver JWT secret stored in plain config with no rotation | `Honua.Server.AlertReceiver` requires static secret; no rotation story. | Integrate with identity provider or allow key rotation via signing keys; avoid long-lived shared secret.
| 3 | **High** | CLI automation logs terraform outputs (may contain secrets) | `DeployInfrastructureStep` logs plan/apply outputs at `Information`. | Sanitize terraform output; only log summary. Redact secrets before writing to log.
| 4 | **Medium** | CSRF middleware bypass for API key requests could allow browser misuse | `CsrfValidationMiddleware.IsApiKeyAuthenticated` trusts presence of header. | Provide opt-out flag; document risk; consider additional header (e.g., `X-Requested-With`) or per-key policy.
| 5 | **Medium** | SecurityPolicyMiddleware heuristics bypassable via custom paths | Path-based detection misses new admin endpoints. | Move to default-deny (require explicit `[Authorize]`). Add unit tests to guard future routes.
| 6 | **Medium** | Threat model TODOs not tracked | Multiple `⚠️ TODO` items in `docs/security/THREAT_MODEL.md`. | Convert to backlog with owners/dates; update doc to reflect status.
| 7 | **Medium** | CLI stores secrets in plain JSON state | Process state `InfrastructureOutputs` includes passwords; persisted/resumed between steps. | Encrypt state at rest (use DPAPI/Azure Key Vault). Ensure secrets redacted on serialization.
| 8 | **Low** | Alert receiver rate limiter uses IP, no API key override | Could lead to shared limits. | Optionally allow per-token limits or require forwarded header evaluation for trusted proxies.

---

## Suggested Follow-Ups

1. Treat trusted proxy config as required in production; add startup health check.
2. Provide key rotation and centralised identity for alert receiver and CLI automation (no static secrets).
3. Harden logging (terraforms outputs, CLI state) to prevent accidental disclosure.
4. Document CSRF bypass risk; consider double submit token or same-site cookie strategies for API-key clients.

---

## Tests / Verification

- None (static analysis).
