#!/bin/bash
set -e

echo "======================================"
echo " Honua Observability Setup Verification"
echo "======================================"
echo ""

# Check if project builds
echo "[1/6] Building project..."
cd "$(dirname "$0")"
dotnet build Honua.Server.Observability.csproj > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "✓ Project builds successfully"
else
    echo "✗ Project build failed"
    exit 1
fi
echo ""

# Check if Docker is running
echo "[2/6] Checking Docker..."
if docker ps > /dev/null 2>&1; then
    echo "✓ Docker is running"
else
    echo "✗ Docker is not running. Please start Docker to use the monitoring stack."
fi
echo ""

# Check if required files exist
echo "[3/6] Checking configuration files..."
files=(
    "prometheus/prometheus.yml"
    "prometheus/alerts.yml"
    "grafana/dashboards/honua-overview.json"
    "grafana/datasources/prometheus.yml"
    "alertmanager/alertmanager.yml"
    "docker-compose.monitoring.yml"
)

all_files_exist=true
for file in "${files[@]}"; do
    if [ -f "$file" ]; then
        echo "✓ $file"
    else
        echo "✗ $file (missing)"
        all_files_exist=false
    fi
done

if [ "$all_files_exist" = false ]; then
    echo "Some configuration files are missing!"
    exit 1
fi
echo ""

# Check metric classes
echo "[4/6] Checking metric classes..."
metric_classes=(
    "Metrics/BuildQueueMetrics.cs"
    "Metrics/CacheMetrics.cs"
    "Metrics/LicenseMetrics.cs"
    "Metrics/RegistryMetrics.cs"
    "Metrics/IntakeMetrics.cs"
)

all_metrics_exist=true
for class in "${metric_classes[@]}"; do
    if [ -f "$class" ]; then
        echo "✓ $class"
    else
        echo "✗ $class (missing)"
        all_metrics_exist=false
    fi
done

if [ "$all_metrics_exist" = false ]; then
    echo "Some metric classes are missing!"
    exit 1
fi
echo ""

# Check health checks
echo "[5/6] Checking health checks..."
health_checks=(
    "HealthChecks/DatabaseHealthCheck.cs"
    "HealthChecks/LicenseHealthCheck.cs"
    "HealthChecks/QueueHealthCheck.cs"
    "HealthChecks/RegistryHealthCheck.cs"
)

all_health_checks_exist=true
for check in "${health_checks[@]}"; do
    if [ -f "$check" ]; then
        echo "✓ $check"
    else
        echo "✗ $check (missing)"
        all_health_checks_exist=false
    fi
done

if [ "$all_health_checks_exist" = false ]; then
    echo "Some health checks are missing!"
    exit 1
fi
echo ""

# Summary
echo "[6/6] Setup verification complete!"
echo ""
echo "======================================"
echo " Next Steps"
echo "======================================"
echo ""
echo "1. Start the monitoring stack:"
echo "   docker-compose -f docker-compose.monitoring.yml up -d"
echo ""
echo "2. Integrate into your application:"
echo "   - Add project reference"
echo "   - Update Program.cs (see QUICKSTART.md)"
echo ""
echo "3. Access dashboards:"
echo "   - Grafana: http://localhost:3000 (admin/admin)"
echo "   - Prometheus: http://localhost:9090"
echo "   - Alertmanager: http://localhost:9093"
echo ""
echo "For detailed instructions, see:"
echo "   - QUICKSTART.md (5-minute setup)"
echo "   - README.md (comprehensive guide)"
echo "   - IMPLEMENTATION_SUMMARY.md (technical details)"
echo ""
