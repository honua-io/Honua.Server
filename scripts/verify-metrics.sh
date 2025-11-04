#!/bin/bash
# Script to verify Honua metrics are properly exposed and working
# Usage: ./scripts/verify-metrics.sh [http://localhost:5000]

set -e

HONUA_URL="${1:-http://localhost:5000}"
METRICS_ENDPOINT="${HONUA_URL}/metrics"

echo "================================================"
echo "Honua Metrics Verification Script"
echo "================================================"
echo "Target: ${METRICS_ENDPOINT}"
echo ""

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check if a metric exists
check_metric() {
    local metric_name=$1
    local category=$2

    if curl -s "${METRICS_ENDPOINT}" | grep -q "^${metric_name}"; then
        echo -e "${GREEN}✓${NC} ${category}: ${metric_name}"
        return 0
    else
        echo -e "${RED}✗${NC} ${category}: ${metric_name} (MISSING)"
        return 1
    fi
}

# Test endpoint accessibility
echo "1. Testing Metrics Endpoint Accessibility..."
if curl -s -o /dev/null -w "%{http_code}" "${METRICS_ENDPOINT}" | grep -q "200"; then
    echo -e "${GREEN}✓${NC} Metrics endpoint is accessible"
else
    echo -e "${RED}✗${NC} Metrics endpoint is not accessible"
    echo "   Make sure Honua Server is running and observability.metrics.enabled=true"
    exit 1
fi
echo ""

# Check API Metrics
echo "2. Checking API Metrics..."
check_metric "honua_api_requests_total" "API"
check_metric "honua_api_request_duration" "API"
check_metric "honua_api_errors_total" "API"
check_metric "honua_api_features_returned_total" "API"
echo ""

# Check Database Metrics
echo "3. Checking Database Metrics..."
check_metric "honua_database_queries_total" "Database"
check_metric "honua_database_query_duration" "Database"
check_metric "honua_database_slow_queries_total" "Database"
check_metric "honua_database_connection_wait_time" "Database"
check_metric "honua_database_connection_errors_total" "Database"
check_metric "honua_database_transaction_commits_total" "Database"
check_metric "honua_database_transaction_rollbacks_total" "Database"
echo ""

# Check Cache Metrics
echo "4. Checking Cache Metrics..."
check_metric "honua_cache_hits_total" "Cache"
check_metric "honua_cache_misses_total" "Cache"
check_metric "honua_cache_writes_total" "Cache"
check_metric "honua_cache_evictions_total" "Cache"
check_metric "honua_cache_operation_duration" "Cache"
echo ""

# Check Raster Tile Metrics
echo "5. Checking Raster Tile Metrics..."
check_metric "honua_raster_cache_hits_total" "Raster"
check_metric "honua_raster_cache_misses_total" "Raster"
check_metric "honua_raster_render_latency_ms" "Raster"
check_metric "honua_raster_preseed_jobs_completed_total" "Raster"
echo ""

# Check Vector Tile Metrics
echo "6. Checking Vector Tile Metrics..."
check_metric "honua_vectortile_tiles_generated_total" "Vector Tiles"
check_metric "honua_vectortile_tiles_served_total" "Vector Tiles"
check_metric "honua_vectortile_generation_duration" "Vector Tiles"
check_metric "honua_vectortile_features_per_tile" "Vector Tiles"
check_metric "honua_vectortile_preseed_jobs_started_total" "Vector Tiles"
echo ""

# Check Security Metrics
echo "7. Checking Security Metrics..."
check_metric "honua_security_login_attempts_total" "Security"
check_metric "honua_security_login_failures_total" "Security"
check_metric "honua_security_token_validations_total" "Security"
check_metric "honua_security_authorization_checks_total" "Security"
check_metric "honua_security_sessions_created_total" "Security"
check_metric "honua_security_api_key_usage_total" "Security"
echo ""

# Check Business Metrics
echo "8. Checking Business Metrics..."
check_metric "honua_business_features_served_total" "Business"
check_metric "honua_business_raster_tiles_served_total" "Business"
check_metric "honua_business_vector_tiles_served_total" "Business"
check_metric "honua_business_stac_searches_total" "Business"
check_metric "honua_business_exports_total" "Business"
check_metric "honua_business_active_sessions" "Business"
echo ""

# Check Infrastructure Metrics
echo "9. Checking Infrastructure Metrics..."
check_metric "honua_infrastructure_memory_working_set" "Infrastructure"
check_metric "honua_infrastructure_memory_gc_heap" "Infrastructure"
check_metric "honua_infrastructure_gc_collections_total" "Infrastructure"
check_metric "honua_infrastructure_gc_duration" "Infrastructure"
check_metric "honua_infrastructure_threadpool_worker_threads" "Infrastructure"
check_metric "honua_infrastructure_threadpool_queue_length" "Infrastructure"
check_metric "honua_infrastructure_cpu_usage_percent" "Infrastructure"
echo ""

# Count total metrics
echo "10. Metrics Summary..."
TOTAL_METRICS=$(curl -s "${METRICS_ENDPOINT}" | grep -c "^honua_" || echo "0")
echo "Total Honua metrics found: ${TOTAL_METRICS}"

if [ "${TOTAL_METRICS}" -ge 40 ]; then
    echo -e "${GREEN}✓${NC} Metrics count looks good (expected 40+)"
else
    echo -e "${YELLOW}⚠${NC} Expected 40+ metrics, found ${TOTAL_METRICS}"
fi
echo ""

# Check for high cardinality issues
echo "11. Cardinality Check..."
HIGH_CARD_METRICS=$(curl -s "${METRICS_ENDPOINT}" | grep "^honua_" | wc -l)
echo "Total metric time series: ${HIGH_CARD_METRICS}"

if [ "${HIGH_CARD_METRICS}" -lt 1000 ]; then
    echo -e "${GREEN}✓${NC} Cardinality is within acceptable range (<1000)"
else
    echo -e "${YELLOW}⚠${NC} High cardinality detected (>1000 time series)"
fi
echo ""

# Test PromQL compatibility
echo "12. Testing Prometheus Format..."
if curl -s "${METRICS_ENDPOINT}" | grep -q "# HELP"; then
    echo -e "${GREEN}✓${NC} Metrics use proper Prometheus format"
else
    echo -e "${YELLOW}⚠${NC} Metrics may not be in proper Prometheus format"
fi
echo ""

# Sample some metric values
echo "13. Sample Metric Values..."
echo "Active Sessions:"
curl -s "${METRICS_ENDPOINT}" | grep "honua_business_active_sessions" | head -1
echo ""
echo "Memory Usage:"
curl -s "${METRICS_ENDPOINT}" | grep "honua_infrastructure_memory_working_set" | head -1
echo ""
echo "Request Count:"
curl -s "${METRICS_ENDPOINT}" | grep "honua_api_requests_total" | head -3
echo ""

echo "================================================"
echo "Metrics Verification Complete"
echo "================================================"
echo ""
echo "Next Steps:"
echo "1. Check Grafana dashboard: http://localhost:3000"
echo "2. Import dashboard: docker/grafana/dashboards/honua-metrics.json"
echo "3. Configure Prometheus scraping"
echo "4. Review metrics documentation: docs/observability/METRICS.md"
echo ""
