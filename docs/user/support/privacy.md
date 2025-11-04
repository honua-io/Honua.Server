# Telemetry & Privacy Notice (Phase 0)

Telemetry and crash reporting are opt-in. When disabled, no diagnostic data leaves your environment.

## Data Collected (when enabled)
| Category | Fields | Retention |
|----------|--------|-----------|
| Service metadata | Service name, version, build SHA | 30 days |
| Usage counters | ?f= format counts, endpoint hit totals (aggregated) | 30 days |
| Error summaries | Exception type, status code, stack hash (no parameters) | 30 days |
| Environment | OS type/version, provider list (PostGIS/SQLite/SQL Server) | 30 days |

Crash reports add timestamp, correlation ID, top stack frames (scrubbed), and optional support bundle ID.

## Opt-In / Opt-Out
- Configuration: set Support.Telemetry.Enabled and Support.CrashReports.Enabled to 	rue or alse.
- Environment variables: HONUA_TELEMETRY_ENABLED, HONUA_CRASHREPORT_ENABLED override file settings.
- The support CLI prompts for consent before uploading data.

## Storage & Access
- Default endpoints: https://telemetry.honua.local/collect and https://telemetry.honua.local/crash (configure for production).
- Data stored in the internal observability cluster (OpenTelemetry collector + secure storage).
- Access limited to Honua engineering/support teams for debugging and roadmap planning.

## User Rights
- Disable telemetry at any time via configuration or environment variable.
- Request deletion of stored data by emailing privacy@honua.next with relevant timestamps/support bundle IDs.
- Request a copy of telemetry records associated with your instance.

For privacy questions, contact privacy@honua.next.
