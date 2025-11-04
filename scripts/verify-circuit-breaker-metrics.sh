#!/bin/bash
#
# Circuit Breaker Metrics Verification Script
# Verifies that circuit breaker metrics are being exported correctly
#

set -e

PROMETHEUS_URL="${PROMETHEUS_URL:-http://localhost:9090}"
GRAFANA_URL="${GRAFANA_URL:-http://localhost:3000}"

echo "=== Circuit Breaker Metrics Verification ==="
echo ""

# Function to query Prometheus
query_prometheus() {
    local query="$1"
    local name="$2"

    echo "Checking: $name"

    if command -v curl &> /dev/null; then
        result=$(curl -s "${PROMETHEUS_URL}/api/v1/query?query=${query}" | grep -o '"status":"[^"]*"' || echo "error")

        if [[ "$result" == *"success"* ]]; then
            echo "  ✓ Query successful"
            return 0
        else
            echo "  ✗ Query failed or no data"
            return 1
        fi
    else
        echo "  ⚠ curl not available, skipping"
        return 2
    fi
}

# Check Prometheus is available
echo "1. Checking Prometheus connectivity..."
if curl -s "${PROMETHEUS_URL}/-/healthy" &> /dev/null; then
    echo "  ✓ Prometheus is reachable at ${PROMETHEUS_URL}"
else
    echo "  ✗ Prometheus is not reachable at ${PROMETHEUS_URL}"
    echo "  Set PROMETHEUS_URL environment variable if using custom URL"
    exit 1
fi
echo ""

# Check metrics existence
echo "2. Verifying circuit breaker metrics..."

query_prometheus "honua_circuit_breaker_state" "Circuit state metric"
query_prometheus "honua_circuit_breaker_breaks_total" "Circuit breaks counter"
query_prometheus "honua_circuit_breaker_state_transitions_total" "State transitions counter"
query_prometheus "honua_circuit_breaker_closures_total" "Circuit closures counter"
query_prometheus "honua_circuit_breaker_half_opens_total" "Half-open counter"

echo ""

# Check for active metrics
echo "3. Checking for active circuit breakers..."

services=("s3" "azure_blob" "gcs" "http")
for service in "${services[@]}"; do
    echo "  Checking ${service}..."
    query_prometheus "honua_circuit_breaker_state{service=\"${service}\"}" "  ${service} state" || true
done

echo ""

# Check alert rules
echo "4. Verifying alert rules..."
if curl -s "${PROMETHEUS_URL}/api/v1/rules" | grep -q "circuit_breaker"; then
    echo "  ✓ Circuit breaker alert rules are loaded"
else
    echo "  ⚠ Circuit breaker alert rules not found"
    echo "    Ensure docker/prometheus/alerts/circuit-breakers.yml is loaded"
fi

echo ""

# Check Grafana dashboard
echo "5. Verifying Grafana dashboard..."
if curl -s "${GRAFANA_URL}/api/health" &> /dev/null; then
    echo "  ✓ Grafana is reachable at ${GRAFANA_URL}"
    echo "    Dashboard: ${GRAFANA_URL}/d/honua-circuit-breakers"
else
    echo "  ⚠ Grafana is not reachable at ${GRAFANA_URL}"
    echo "    Set GRAFANA_URL environment variable if using custom URL"
fi

echo ""

# Check current circuit states
echo "6. Current circuit breaker states:"
echo ""

if command -v curl &> /dev/null && command -v jq &> /dev/null; then
    response=$(curl -s "${PROMETHEUS_URL}/api/v1/query?query=honua_circuit_breaker_state")

    if echo "$response" | jq -e '.data.result | length > 0' &> /dev/null; then
        echo "$response" | jq -r '.data.result[] | "  \(.metric.service): \(.value[1]) (\(if .value[1] == "0" then "Closed" elif .value[1] == "1" then "OPEN" elif .value[1] == "2" then "Half-Open" else "Unknown" end))"'
    else
        echo "  ℹ No circuit breaker state metrics found yet"
        echo "    Metrics will appear after first circuit breaker activity"
    fi
else
    echo "  ⚠ jq not available, skipping detailed state display"
fi

echo ""
echo "=== Verification Complete ==="
echo ""
echo "Next steps:"
echo "  1. View dashboard: ${GRAFANA_URL}/d/honua-circuit-breakers"
echo "  2. Query metrics: ${PROMETHEUS_URL}/graph"
echo "  3. Check alerts: ${PROMETHEUS_URL}/alerts"
echo ""
