# CLI & Automation Tooling Review – 2025-02-18

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Cli/**`, `src/Honua.Cli.AI/**`, process framework steps, control-plane orchestration, CLI AI agents, plugin system |
| Methods | Static analysis only (no command execution) |

---

## High-Level Observations

- The AI-powered process framework drives Terraform/app deployment, but numerous steps assume single-tenant runs (global state `_state`, shared workspaces). No concurrency isolation or resumable workflow beyond ad-hoc JSON state.
- Extensive shell command execution (Terraform, psql, cloud CLIs) is done via `ProcessHelper` without input sanitisation or structured logging of failures.
- Secrets are stored in process state dictionaries and serialized; persistence/rollback guidelines are unclear. Several steps log sensitive outputs at `Information` level.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | Process steps share mutable `_state` without locks; parallel executions can corrupt deployments | `DeployInfrastructureStep`, `ConfigureServicesStep`, `DeployHonuaApplicationStep` store shared `_state` field on class instances registered as singletons in DI. Kernel may reuse steps across concurrent workflows. | Treat steps as transient (scoped per execution) or make `_state` local to `ActivateAsync`. Ensure DI registration uses factory per workflow instance.
| 2 | **High** | Shell command execution lacks escaping and auditing | `ProcessHelper.RunCommandAsync` (and similar) accepts command string and uses `/bin/bash -c`, embedding user-provided values (workspace paths, service IDs) without escaping. | Switch to structured `ProcessStartInfo` with argument arrays; validate inputs. Capture stdout/stderr securely and redact secrets.
| 3 | **High** | Secrets emitted to logs | `ConfigureServicesStep` logs database connection info; `GenerateInfrastructureCodeStep` writes full terraform output to log. Logging at `Information` may leak credentials. | Audit logging statements; downgrade to Debug and redact sensitive data. Add static analyzer to prevent `InfrastructureOutputs` from being logged.
| 4 | **Medium** | Terraform workspace reuse without locking | Deploy step reuses `_state.TerraformWorkspacePath` but doesn’t lock directory; concurrent processes could race or clobber plan file. | Introduce file locks or per-execution temp directories; ensure plan/apply run sequentially with workspace copies.
| 5 | **Medium** | Terraform error handling relies on interpreting stdout text | Failures bubble via `ProcessHelper`, but no structured parsing; rollback step may run with inconsistent state. | Parse exit codes/structured JSON outputs; record failure reason in state for frontend.
| 6 | **Medium** | Cloud CLI integration hardcodes providers, assumes CLI installed/configured | `ConfigureServicesStep` uses `DefaultAwsCli.Shared`; no validation of CLI version or region. Missing retries/backoff for API calls. | Add environment pre-flight checks; abstract provider clients via SDK (e.g., AWS SDK) rather than shelling out.
| 7 | **Medium** | AI-driven workflow lacks guardrails; prompts may produce different command sequences each run | Steps rely on SemanticKernel functions; no deterministic plan recorded. | Introduce execution plans or dry-run stage; log step inputs/outputs for auditing.
| 8 | **Low** | No unit tests for process steps | CLI automation is complex but lacks tests under `tests/Honua.Cli*`. | Add integration/unit tests covering Terraform command wrapper, state transitions, rollback behaviour.

---

## Suggested Follow-Ups

1. Refactor process steps to be stateless or instantiate per execution; avoid shared mutable `_state`.
2. Replace shell invocation with typed SDK interactions where possible; sanitise arguments and capture structured logs.
3. Harden secret handling: mask sensitive values in state/logs, enforce redaction helpers.
4. Add concurrency guards (mutex/lockfile) around Terraform/DNS modifications.
5. Expand test coverage for automation steps; include failure-path scenarios and rollback validation.

---

## Tests / Verification

- None (static analysis only).
