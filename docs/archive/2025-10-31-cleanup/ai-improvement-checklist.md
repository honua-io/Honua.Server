# Honua AI Improvement Checklist

> Use this list to plan and track incremental enhancements to the Honua AI workflow, emulator harness, and production readiness.  
> Check items off as they are delivered; append dates/owners where helpful.

## 1. Emulator & Infrastructure Foundation
- [x] Document emulator prerequisites and helper scripts (Docker, LocalStack, Azurite, fake GCS, Postgres) in `README` / Quick Start.
- [x] Create consolidated emulator bootstrapper (single CLI entry point) with health reporting.
- [ ] Add readiness telemetry: emit per-emulator availability metrics to test output and CI artifacts.
- [ ] Cache emulator containers across test runs (where feasible) to reduce start-up time.
- [ ] Fail fast when emulators unavailable (skippable facts already wired—add CI gating so missing Docker marks job as infra failure, not success).

## 2. Terraform & Infrastructure Verification
- [ ] Snapshot Terraform `main.tf` / `terraform.tfvars` per provider and assert key resources (ALB, Postgres, DNS, storage) exist.
- [ ] Validate generated secrets (length, entropy) and ensure they persist through `DeployInfrastructureStep`.
- [ ] Add regression tests verifying Terraform output merge logic retains prior secrets when new outputs arrive.
- [ ] Exercise `DeployInfrastructureStep` rollback path end-to-end against emulators (simulate `terraform apply` failure).
- [ ] Generate least-privilege discovery IAM policies and Terraform modules for AWS, Azure, and GCP (service account creation + instructions).

## 3. Deployment Workflow Coverage
- [ ] Orchestrate full `DeploymentProcess` (plan → generate → deploy infra → configure services → deploy app → validate) using emulators.
- [ ] Add concurrency test that runs two parallel deployments (different ids) to ensure isolation.
- [ ] Create negative-path suites: missing `load_balancer_endpoint`, DNS propagation timeout, guardrail evaluation failure, CLI command failure mid-run.
- [ ] Cover authentication scenarios by injecting API key / bearer token into validation step requests.
- [ ] Verify database provisioning across providers (PostGIS extension installed, Azure/GCP SQL reachable once emulator clients available).
- [ ] Implement provider discovery routines (VPC/subnet, DNS zone, database inventory) with AI decision prompts to choose new vs. existing infrastructure.
- [ ] Extend guardrail envelopes and prompts to enforce “reuse existing” vs. “greenfield” constraints automatically.
- [ ] Modularize Terraform generation so it can target both create-new (module A) and attach-to-existing (module B) paths based on discovery results.
- [ ] Add validation loops comparing post-deploy state (network reachability, DB connectivity) against discovery data; fail safely with remediation hints.
- [ ] Ensure rollback logic differentiates user-owned resources from AI-created assets in brownfield scenarios.

## 4. AI & Parameter Extraction Hardening
- [ ] Expand LLM prompt fixtures to cover multi-feature deployments (STAC only, GeoServer only, benchmarks, mixed tiers).
- [ ] Validate error handling when LLM response missing required fields (ensure fallback defaults trigger guardrails/tests).
- [ ] Add snapshot tests for AI parameter extraction outputs to catch prompt/regression changes.
- [ ] Introduce red-team prompts to ensure guardrails reject unsafe or unsupported configurations.

## 5. Observability & Diagnostics
- [ ] Emit structured logs per test (step timings, emulator endpoints, Terraform workspace path) through `ITestOutputHelper`.
- [ ] Capture emulator logs (LocalStack/Azurite/GCS/Postgres) on failure and attach to test artifacts.
- [ ] Add metrics assertions post-deployment (`PostDeployGuardrailMonitor` thresholds, custom telemetry).
- [ ] Integrate OpenTelemetry exporters in test harness to trace end-to-end execution.

## 6. CI / Automation Integration
- [ ] Create dedicated GitHub Action or pipeline stage for emulator tests with Docker cache warm-up.
- [ ] Publish matrix of supported providers/features and expected runtime; surface flaky test detector.
- [ ] Gate merges on key emulator scenarios (at least AWS happy-path + failure-path) while allowing optional providers behind feature flags.
- [ ] Upload Terraform artifacts and test logs as CI build artifacts for debugging.

## 7. Security & Compliance Validations
- [ ] Scan generated Terraform for hardcoded passwords/unsafe defaults (automated lint pass).
- [ ] Ensure TLS/SSL flags validated across providers (fail pipeline if workspaces request insecure settings without override).
- [ ] Add tests for secret rotation workflows (regenerate credentials and re-run config/deploy).
- [ ] Validate IAM / role bindings output to ensure principle of least privilege (mocked or emulator-based checks).

## 8. Documentation & Developer Experience
- [ ] Publish “Emulator Testing Playbook” with step-by-step runbook and troubleshooting tips.
- [ ] Provide sample scripts to seed emulator data (Route53 hosted zones, sample buckets, PostGIS datasets).
- [ ] Add CONTRIBUTING section describing how to add new emulator-backed tests and guardrail cases.
- [ ] Track checklist status in weekly AI QA review; promote items to engineering roadmap as needed.
