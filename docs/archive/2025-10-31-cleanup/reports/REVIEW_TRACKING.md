# Incremental Code Review Tracker

Last updated: <!-- YYYY-MM-DD --> 2025-02-19  
Owner: Code Review Agent

This document records progress as we walk the repository with the structured checklist. Detailed findings for each area should live in their own report files (e.g., `docs/review/YYYY-MM/<area>.md`) and be linked back here.

---

## 0. Foundation Pass

| Item | Status | Last Review | Notes / Findings Link |
| --- | --- | --- | --- |
| Repo / solution topology & DI bootstrapping | Not Started | – | – |
| Shared utilities (`Honua.Server.Core` primitives) | Not Started | – | – |
| Infrastructure configs (Docker, CI/CD, appsettings) | Not Started | – | – |
| Test harness layout & smoke run | Not Started | – | – |

---

## 1. Functional Area Sweeps

| Area ID | Scope | Status | Last Review | Findings |
| --- | --- | --- | --- | --- |
| 1.1 | OGC API stack (handlers, streaming writers, tiles) | Verified | 2025-02-16 | [Report](../review/2025-02/ogc-api.md) |
| 1.2 | Geoservices REST (controllers, services, exporters) | Verified | 2025-02-15 | [Report](../review/2025-02/geoservices-rest.md) |
| 1.3 | STAC & Records APIs | Verified | 2025-02-16 | [Report](../review/2025-02/stac-api.md) |
| 1.4 | OData & legacy compatibility endpoints | Verified | 2025-02-17 | [Report](../review/2025-02/odata-legacy.md) |
| 1.5 | Data persistence & repositories | Verified | 2025-02-17 | [Report](../review/2025-02/data-persistence.md) |
| 1.6 | WFS & WMS services | Verified | 2025-02-18 | [Report](../review/2025-02/wfs-wms.md) |
| 1.7 | Background jobs & pipelines | Findings Logged | 2025-02-18 | [Report](../review/2025-02/background-jobs.md) |
| 1.8 | Security & identity services | Findings Logged | 2025-02-18 | [Report](../review/2025-02/security-identity.md) |
| 1.9 | Alerting & ops integrations | Findings Logged | 2025-02-19 | [Report](../review/2025-02/alerting-ops.md) |
| 1.10 | CLI & automation tooling | Findings Logged | 2025-02-18 | [Report](../review/2025-02/cli-automation.md) |
| 1.11 | Docs & templates | Findings Logged | 2025-02-18 | [Report](../review/2025-02/docs-templates.md) |

---

## 2. Cross-Cutting Passes

These runs should happen after each functional sweep (scoped) and once across the repo once all areas are reviewed.

| Pass | Current Status | Notes / Findings Link |
| --- | --- | --- |
| Performance & Streaming | Findings Logged | 2025-02-18 | [Report](../review/2025-02/crosscut-performance.md) |
| Security Hardening | Findings Logged | 2025-02-18 | [Report](../review/2025-02/crosscut-security.md) |
| Resilience & Reliability | Findings Logged | 2025-02-18 | [Report](../review/2025-02/crosscut-resilience.md) |
| Observability | Not Started | – |
| Testing & Verification | Not Started | – |
| Compliance & Config Hygiene | Not Started | – |

---

## 3. Issue Register (Quick Reference)

Maintain detailed findings in per-area documents; log summaries here for quick scanning.

| Area | Issue Title | Severity | Status | Link |
| --- | --- | --- | --- | --- |
| Geoservices REST | Phase 2 controller placeholders still present | Low | Open | [Report §Findings #1](../review/2025-02/geoservices-rest.md) |
| Geoservices REST | KML streaming lacks style support | Low | Backlog | [Report §Findings #5](../review/2025-02/geoservices-rest.md) |

---

### How to Use

1. Pick the next unchecked item in Section 0 or 1.  
2. Perform the review, capturing evidence in `docs/review/YYYY-MM/<area>.md`.  
3. Update the table row with status (`In Progress`, `Findings Logged`, `Verified`) and link to the findings document.  
4. After each functional area, run the relevant cross-cutting passes and record status above.  
5. Keep the Issue Register in sync with any open findings/tickets.
