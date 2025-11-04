# Support & Troubleshooting (Phase 0)

## 1. Run the Support Assistant
`ash
./scripts/honua-support.sh      # macOS/Linux
./scripts/honua-support.ps1     # Windows PowerShell
`
The assistant validates prerequisites, metadata, service health, database connectivity, and generates an optional support bundle (qa-report/support-bundle-<timestamp>.zip). Include the bundle ID when opening a GitHub issue.

## 2. Common Fixes
| Symptom | Suggested Action |
|---------|------------------|
| Metadata validation failed | Run the schema mapping CLI, correct JSON syntax, reapply metadata. |
| Database connection timeout | Verify connectionSecret environment variable and network reachability. |
| ?f=kml returns 406 | Ensure the KML serializer is enabled and metadata exposes the format; rerun the support CLI to confirm. |
| Tiles missing (?f=mvt) | Check tile matrix configuration and ensure spatial indexes exist on provider tables. |

## 3. Telemetry & Crash Reporting
- Disabled by default; enable via Support.Telemetry.Enabled = true in ppsettings.json.
- Crash reports (stack traces + request identifiers) require Support.CrashReports.Enabled = true.
- See the [privacy notice](privacy.md) for data fields and retention policy.

## 4. Feedback & Feature Requests
- Vote on roadmap items: [GitHub Discussions › Roadmap](https://github.com/honua/honua.next/discussions/categories/roadmap).
- Submit feature requests via .github/ISSUE_TEMPLATE/feature_request.yml.
- File bugs using .github/ISSUE_TEMPLATE/bug_report.yml and include the support bundle ID.

## 5. Need Live Help?
For Phase 0, contact the Honua engineering team via Slack #honua-support or email support@honua.next.

> Always attach the support bundle, telemetry consent status, and relevant logs when requesting assistance.
