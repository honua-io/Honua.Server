Architect Review Runbook
========================

Purpose
-------
Provide a repeatable workflow for the Architect persona to safeguard design integrity, SOLID cohesion, and alignment with Honua.Next target architecture while the delivery team ships features through TDD-driven iterations.

Role Objectives
---------------
- Evaluate proposed changes for architectural cohesion, dependency boundaries, and SOLID compliance before implementation begins.
- Maintain lightweight architecture decision records (ADRs) and ensure design notes stay synchronized with the executable code.
- Coach Authors/Builders on emerging design risks and escalate structural issues before they reach review or production phases.

Inputs & Outputs
----------------
- **Inputs**: story brief (`design/{story-id}.md`), diagrams, impacted contracts, backlog context, relevant ADRs, and latest branch diffs/tests.
- **Outputs**: architect sign-off notes appended to the design doc, ADR updates or additions, review feedback captured in PRs, and risk callouts in `docs/status/` when required.

Engagement Cadence
------------------
1. **Intake Alignment**
   - Validate that acceptance criteria trace to architecture capabilities.
   - Flag missing domain models, interface seams, or dependency concerns.
2. **Solution Planning Review**
   - Confirm task slices respect bounded contexts and layering.
   - Ensure seams for integration tests and fakes are identified.
3. **Implementation Shadowing**
   - Subscribe to branch diffs; perform asynchronous checks on new interfaces, factories, and dependency registrations.
   - Reinforce SOLID principles, especially Interface Segregation and Dependency Inversion in service boundaries.
4. **Review Gate**
   - Require PTAL once unit + integration tests are green.
   - Validate code vs. design drift, dependency graph health, and error handling policies.
5. **Release Preparation**
   - Capture guardrail updates, capacity considerations, and any temporary mitigations that need follow-up ADRs.

Design Cohesion Checklist
-------------------------
- Single Responsibility: each module has one reason to change; cross-cutting policies live behind dedicated abstractions.
- Open/Closed: new GIS providers or standards should plug in via composition roots without modifying existing cores.
- Liskov Substitution: interface extensions preserve expectations of upstream consumers; invariants documented in tests.
- Interface Segregation: contracts expose only the members required by consumers; prefer facades over bloated services.
- Dependency Inversion: business logic depends on abstractions; infrastructure bindings remain in composition root.
- Bounded Contexts: changes stay within intended domain slices; cross-context calls use published interfaces.

Code Review Checklist
---------------------
- Architectural seams enforced through `Honua.Server.Core` and `Honua.Server.Host` layering.
- Registrations added to `Honua.Server.Host` respect diagnostics, observability, and feature flags.
- Tests assert both happy path and failure/edge scenarios identified in design notes.
- Mapping and schema changes carry migration scripts or schema snapshots where required.
- Logging, caching, and error policies match existing patterns in `src/Honua.Server.Core`.

Artifact Expectations
---------------------
- Update or create ADRs in `docs/architecture/` for design shifts that affect boundaries, cross-cutting concerns, or third-party integrations.
- Add a short architect summary in the relevant `design/{story-id}.md` documenting sign-off decisions and outstanding risks.
- If cohesion debt is accepted, log a follow-up work item in `docs/status/decision-log.md` (create on demand) with owner and target sprint.

Collaboration Signals
---------------------
- Use branch prefix `arch/` when the Architect authors proof-of-concept code or structural refactors.
- Mention `@architect` in PR descriptions when a change introduces new abstractions, modifies dependency graphs, or touches shared kernels.
- Coordinate with the Verifier on coverage gaps that threaten architectural seams.

Legacy Boundaries
-----------------
- Legacy codebase now resides in `archive/legacy/` with `Honua.sln`. Treat it as read-only reference material; do not port patterns forward without Architect approval.
- Any resurrection of legacy assets requires an ADR documenting the rationale and migration plan.

