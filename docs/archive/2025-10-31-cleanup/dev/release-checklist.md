# Release Checklist (Phase 0)

Use this checklist before cutting a Phase-0 release candidate.

## 1. Build & Tests
- [ ] dotnet build succeeds for all solutions.
- [ ] Unit / integration tests pass (dotnet test).
- [ ] Format smoke tests executed (scripts/honua-support or CI job) covering ?f=geojson|kml|kmz|mvt|geopackage.
- [ ] OGC API Features ETS run completed (docs/dev/runbooks/ogc-features.md).
- [ ] KML ETS run completed (docs/dev/runbooks/kml-conformance.md).
- [ ] WMS 1.3 ETS run completed (docs/dev/runbooks/wms-conformance.md).
- [ ] `/records` endpoints smoke-tested (landing, collections, items).

## 2. Compliance & Security
- [ ] OWASP hardening checklist reviewed (authentication, headers, rate limiting).
- [ ] SBOM + license report regenerated; exceptions documented (docs/compliance/).
- [ ] DCO/CLA verification for all commits in release branch.
- [ ] Security patch audit (dependency updates / CVEs) completed.

## 3. Documentation
- [ ] README quickstart verified against latest instructions.
- [ ] docs/user/endpoints.md and docs/user/format-matrix.md updated with new endpoints/formats.
- [ ] Metadata authoring guide (`docs/user/metadata-authoring.md`) reflects current schemas.
- [ ] Runbooks / support docs refreshed with latest diagnostics commands.

## 4. Support & Telemetry
- [ ] Support CLI (scripts/honua-support) run and bundle attached to release notes.
- [ ] Telemetry/Crash opt-in defaults confirmed (off) and privacy notice included.
- [ ] GitHub issue templates + roadmap discussions reviewed for accuracy.

## 5. Sign-off
- Engineering Lead: ____________________
- Security/Compliance: _________________
- Product/Support: _____________________
- Date: ________________________________

