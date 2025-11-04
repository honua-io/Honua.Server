# Observability Runbook

## Enable Structured Logging
1. Update configuration to include the observability block:
   ```json
   {
     "observability": {
       "logging": {
         "jsonConsole": true,
         "includeScopes": true
       }
     }
   }
   ```
2. JSON console logging is enabled by default in `appsettings.json`. Set `jsonConsole` to `false` to fall back to text console output.
3. Use `includeScopes` when you want correlation scope identifiers (request ids, service name) inline with every log entry.

## Enable Prometheus Metrics
1. Toggle the Prometheus exporter:
   ```json
   {
     "observability": {
       "metrics": {
         "enabled": true,
         "endpoint": "/metrics",
         "usePrometheus": true
       }
     }
   }
   ```
   - `endpoint` defaults to `/metrics`; supply a custom value (e.g., `/internal-metrics`) for hardened environments.
2. Restart Honua and verify the scrape endpoint:
   ```bash
   curl -s http://localhost:5000/metrics | head
   ```
   Expect Prometheus-formatted output containing counters such as `http_server_request_duration_seconds`.
3. The exporter automatically includes ASP.NET Core and .NET runtime instrumentation. Add application-specific meters later by registering additional `Meter` sources.

## Troubleshooting
- **404 on /metrics**: Ensure `observability.metrics.enabled=true` and `usePrometheus=true` in the active configuration profile.
- **Scrape path mismatch**: Confirm the Prometheus server points at the configured `endpoint` value. Honua automatically prefixes a leading slash when omitted.
- **Empty metrics**: Issue a warm-up request (e.g., `curl /healthz/live`) before scraping; this ensures the ASP.NET instrumentation records at least one request.

## Next Steps
- Integrate the companion Docker Compose bundle (`docker/prometheus/docker-compose.prometheus.yml`) to run Prometheus alongside Honua.
- Add remote exporters (OTLP) in a future iteration when tracing support lands.
