#!/bin/bash
# Exhaustive Test Matrix - Every Permutation Across All Platforms
# Tests EVERYTHING to find what's broken

set -e

if [ -z "$OPENAI_API_KEY" ]; then
    echo "ERROR: OPENAI_API_KEY required"
    exit 1
fi

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RESULTS_DIR="/tmp/honua-exhaustive-test-$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo "╔══════════════════════════════════════════════════════════════════╗"
echo "║  EXHAUSTIVE TEST MATRIX - All Platforms, All Permutations       ║"
echo "╚══════════════════════════════════════════════════════════════════╝"
echo ""
echo "Results: $RESULTS_DIR"
echo ""

PASS_COUNT=0
FAIL_COUNT=0

test_deployment() {
    local test_id=$1
    local platform=$2
    local database=$3
    local cache=$4
    local extras=$5
    local prompt=$6

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "TEST $test_id: $platform | DB:$database | Cache:$cache | $extras"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "Prompt: $prompt"

    WORKSPACE="$RESULTS_DIR/$test_id"
    mkdir -p "$WORKSPACE"

    cd "$PROJECT_ROOT"
    OUTPUT=$(dotnet run --project src/Honua.Cli -- consultant \
        --prompt "$prompt" \
        --workspace "$WORKSPACE" \
        --mode multi-agent \
        --auto-approve \
        --no-log 2>&1)

    echo "$OUTPUT" > "$WORKSPACE/output.log"

    # Check what was generated
    local result="FAIL"
    local generated_files=$(ls "$WORKSPACE" 2>/dev/null | grep -v "output.log" | grep -v "performance.json" || echo "")

    if [ -n "$generated_files" ]; then
        echo "✓ Generated: $generated_files"

        # Validate file contents
        for file in $WORKSPACE/*; do
            if [ -f "$file" ] && [ "$file" != "$WORKSPACE/output.log" ] && [ "$file" != "$WORKSPACE/performance.json" ]; then
                local filename=$(basename "$file")
                local size=$(wc -c < "$file")

                # Check if it's valid
                if [ "$filename" = "docker-compose.yml" ]; then
                    if docker-compose -f "$file" config --quiet 2>/dev/null; then
                        echo "  ✓ docker-compose.yml is VALID"
                        result="PASS"
                    else
                        echo "  ✗ docker-compose.yml is INVALID"
                        result="FAIL"
                    fi
                elif [[ "$filename" == *.tf ]]; then
                    if grep -q "resource" "$file" && grep -q "provider" "$file"; then
                        echo "  ✓ Terraform file looks valid"
                        result="PASS"
                    else
                        echo "  ✗ Terraform file incomplete"
                        result="FAIL"
                    fi
                elif [[ "$filename" == *.yaml ]] || [[ "$filename" == *.yml ]]; then
                    if grep -q "apiVersion" "$file" 2>/dev/null; then
                        echo "  ✓ K8s manifest detected"
                        result="PASS"
                    else
                        echo "  ~ YAML file but not K8s manifest"
                        result="PARTIAL"
                    fi
                else
                    # Skip summary/metadata files (terraform-aws, kubernetes, etc.)
                    if [[ "$filename" =~ ^(terraform-aws|terraform-azure|terraform-gcp|kubernetes|docker-compose|security\.json)$ ]]; then
                        echo "  ~ Metadata/summary file ($filename) - skipping"
                        continue
                    fi

                    # Check if it's JSON with embedded configs (BROKEN)
                    if grep -q '"terraformConfig"' "$file" 2>/dev/null; then
                        echo "  ✗ JSON with embedded Terraform (BROKEN FORMAT)"
                        result="BROKEN"
                    elif grep -q '"manifests"' "$file" 2>/dev/null; then
                        echo "  ✗ JSON with embedded K8s (BROKEN FORMAT)"
                        result="BROKEN"
                    elif [ $size -gt 100 ]; then
                        echo "  ~ File generated ($size bytes) - unknown format"
                        result="UNKNOWN"
                    fi
                fi
            fi
        done
    else
        echo "✗ NO FILES GENERATED"
        result="FAIL"
    fi

    # Record result
    echo "$test_id|$platform|$database|$cache|$result" >> "$RESULTS_DIR/results.csv"

    if [ "$result" = "PASS" ]; then
        PASS_COUNT=$((PASS_COUNT + 1))
        echo "RESULT: ✓ PASS"
    else
        FAIL_COUNT=$((FAIL_COUNT + 1))
        echo "RESULT: ✗ $result"
    fi

    echo ""
}

# Initialize results CSV
echo "TestID|Platform|Database|Cache|Result" > "$RESULTS_DIR/results.csv"

# ============================================================================
# DOCKER COMPOSE TESTS
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 1: DOCKER COMPOSE (Expected to work)"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "DC-01" "Docker" "PostGIS" "None" "Minimal" \
    "Deploy Honua with PostGIS database using Docker Compose"

test_deployment "DC-02" "Docker" "PostGIS" "Redis" "Standard" \
    "Deploy Honua with PostGIS and Redis using Docker Compose"

test_deployment "DC-03" "Docker" "MySQL" "None" "Minimal" \
    "Deploy Honua with MySQL database using Docker Compose"

test_deployment "DC-04" "Docker" "MySQL" "Redis" "Standard" \
    "Deploy Honua with MySQL and Redis using Docker Compose"

test_deployment "DC-05" "Docker" "SQLServer" "None" "Minimal" \
    "Deploy Honua with SQL Server using Docker Compose"

test_deployment "DC-06" "Docker" "SQLServer" "Redis" "Standard" \
    "Deploy Honua with SQL Server and Redis using Docker Compose"

test_deployment "DC-07" "Docker" "PostGIS" "Redis" "+Nginx" \
    "Deploy Honua with PostGIS, Redis, and Nginx reverse proxy using Docker Compose"

test_deployment "DC-08" "Docker" "MySQL" "Redis" "+Nginx" \
    "Deploy Honua with MySQL, Redis, and Nginx using Docker Compose"

# ============================================================================
# KUBERNETES TESTS
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 2: KUBERNETES (Expected to be broken)"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "K8S-01" "Kubernetes" "PostGIS" "None" "Minimal" \
    "Deploy Honua to Kubernetes with PostGIS"

test_deployment "K8S-02" "Kubernetes" "PostGIS" "Redis" "Standard" \
    "Deploy Honua to Kubernetes with PostGIS and Redis"

test_deployment "K8S-03" "Kubernetes" "PostGIS" "Redis" "+Ingress" \
    "Deploy Honua to Kubernetes with PostGIS, Redis, and Ingress"

test_deployment "K8S-04" "Kubernetes" "PostGIS" "Redis" "+Helm" \
    "Deploy Honua to Kubernetes with PostGIS and Redis using Helm charts"

test_deployment "K8S-05" "Kubernetes" "MySQL" "Redis" "+HPA" \
    "Deploy Honua to Kubernetes with MySQL, Redis, and horizontal pod autoscaling"

# ============================================================================
# AWS TESTS
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 3: AWS (Expected to be broken)"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "AWS-01" "AWS" "RDS-PostgreSQL" "None" "Terraform" \
    "Deploy Honua to AWS with RDS PostgreSQL using Terraform"

test_deployment "AWS-02" "AWS" "RDS-PostgreSQL" "ElastiCache" "Terraform" \
    "Deploy Honua to AWS with RDS PostgreSQL and ElastiCache Redis using Terraform"

test_deployment "AWS-03" "AWS" "RDS-PostgreSQL" "ElastiCache" "+S3" \
    "Deploy Honua to AWS with RDS PostgreSQL, ElastiCache Redis, and S3 tile caching using Terraform"

test_deployment "AWS-04" "AWS" "RDS-PostgreSQL" "ElastiCache" "+ECS" \
    "Deploy Honua to AWS ECS Fargate with RDS PostgreSQL and ElastiCache using Terraform"

test_deployment "AWS-05" "AWS" "RDS-PostgreSQL" "ElastiCache" "+EKS" \
    "Deploy Honua to AWS EKS with RDS PostgreSQL and ElastiCache using Terraform"

# ============================================================================
# AZURE TESTS
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 4: AZURE (Expected to be broken)"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "AZ-01" "Azure" "PostgreSQL" "None" "Terraform" \
    "Deploy Honua to Azure with Azure Database for PostgreSQL using Terraform"

test_deployment "AZ-02" "Azure" "PostgreSQL" "Redis" "Terraform" \
    "Deploy Honua to Azure with Azure Database for PostgreSQL and Azure Cache for Redis using Terraform"

test_deployment "AZ-03" "Azure" "PostgreSQL" "Redis" "+Blob" \
    "Deploy Honua to Azure with PostgreSQL, Redis, and Blob Storage for tiles using Terraform"

test_deployment "AZ-04" "Azure" "PostgreSQL" "Redis" "+AKS" \
    "Deploy Honua to Azure AKS with PostgreSQL and Redis using Terraform"

test_deployment "AZ-05" "Azure" "PostgreSQL" "Redis" "+AppService" \
    "Deploy Honua to Azure App Service with PostgreSQL and Redis using Terraform"

# ============================================================================
# GCP TESTS (Bonus)
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 5: GCP (Expected to fail - not implemented)"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "GCP-01" "GCP" "CloudSQL" "None" "Terraform" \
    "Deploy Honua to GCP with Cloud SQL PostgreSQL using Terraform"

test_deployment "GCP-02" "GCP" "CloudSQL" "Memorystore" "Terraform" \
    "Deploy Honua to GCP with Cloud SQL and Memorystore Redis using Terraform"

test_deployment "GCP-03" "GCP" "CloudSQL" "Memorystore" "+GKE" \
    "Deploy Honua to GCP GKE with Cloud SQL and Memorystore using Terraform"

# ============================================================================
# HYBRID/COMPLEX SCENARIOS
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 6: COMPLEX SCENARIOS"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "CX-01" "Multi" "Various" "Various" "LocalStack" \
    "Deploy Honua locally using LocalStack to simulate AWS S3 and RDS"

test_deployment "CX-02" "Multi" "Various" "Various" "Minikube" \
    "Deploy Honua to local Minikube cluster with PostgreSQL and Redis"

test_deployment "CX-03" "Docker" "PostGIS" "Redis" "Production" \
    "Deploy production-ready Honua with PostGIS, Redis, SSL/TLS, and monitoring using Docker Compose"

# ============================================================================
# FUZZY INPUT TESTS - Natural Language Variations
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 7: FUZZY INPUT - Natural Language Variations"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "FUZZY-01" "Docker" "PostGIS" "None" "Casual" \
    "I need to run honua with postgres locally"

test_deployment "FUZZY-02" "Kubernetes" "PostGIS" "None" "Casual" \
    "Can you help me deploy this to k8s?"

test_deployment "FUZZY-03" "AWS" "RDS-PostgreSQL" "None" "Casual" \
    "I want to put honua in the cloud on aws"

test_deployment "FUZZY-04" "Docker" "MySQL" "Redis" "Verbose" \
    "Please create a docker-compose configuration for Honua that uses MySQL as the database and includes Redis for caching"

test_deployment "FUZZY-05" "Azure" "PostgreSQL" "None" "Minimal" \
    "azure deployment terraform"

test_deployment "FUZZY-06" "Kubernetes" "MySQL" "Redis" "Typos" \
    "deploi honua to kubernetees with mysql and reddis"

test_deployment "FUZZY-07" "GCP" "CloudSQL" "None" "Shorthand" \
    "gcp tf postgres"

test_deployment "FUZZY-08" "Docker" "SQLServer" "None" "Alternative" \
    "docker compose with microsoft sql server"

test_deployment "FUZZY-09" "Kubernetes" "PostGIS" "Redis" "Question" \
    "How do I deploy Honua to Kubernetes with PostgreSQL and Redis cache?"

test_deployment "FUZZY-10" "AWS" "RDS-PostgreSQL" "ElastiCache" "Detailed" \
    "I need a complete AWS deployment using Terraform that includes RDS for the database and ElastiCache for Redis caching, suitable for a production environment"

# ============================================================================
# EDGE CASES - Minimal/Ambiguous Input
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 8: EDGE CASES - Minimal/Ambiguous Input"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "EDGE-01" "Docker" "PostGIS" "None" "Minimal" \
    "docker"

test_deployment "EDGE-02" "Kubernetes" "PostGIS" "None" "Minimal" \
    "kubernetes deployment"

test_deployment "EDGE-03" "AWS" "RDS-PostgreSQL" "None" "Minimal" \
    "aws terraform"

test_deployment "EDGE-04" "Docker" "PostGIS" "Redis" "AllLowercase" \
    "deploy honua with docker compose using postgis and redis"

test_deployment "EDGE-05" "Kubernetes" "PostGIS" "None" "AllUppercase" \
    "DEPLOY HONUA TO KUBERNETES WITH POSTGIS"

# ============================================================================
# MULTI-DATABASE TESTS
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 9: DATABASE VARIATIONS"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "DB-01" "Docker" "PostGIS" "None" "AltNames" \
    "Deploy with PostgreSQL and PostGIS extension"

test_deployment "DB-02" "Docker" "MySQL" "None" "AltNames" \
    "Use MySQL 8.0 database"

test_deployment "DB-03" "Docker" "SQLServer" "None" "AltNames" \
    "Microsoft SQL Server deployment"

test_deployment "DB-04" "Kubernetes" "MySQL" "None" "K8s+MySQL" \
    "Kubernetes with MySQL database"

test_deployment "DB-05" "AWS" "RDS-PostgreSQL" "None" "RDS" \
    "AWS RDS PostgreSQL with PostGIS"

# ============================================================================
# CACHE VARIATIONS
# ============================================================================

echo "═══════════════════════════════════════════════════════════════════"
echo "  SECTION 10: CACHE VARIATIONS"
echo "═══════════════════════════════════════════════════════════════════"
echo ""

test_deployment "CACHE-01" "Docker" "PostGIS" "Redis" "Explicit" \
    "Deploy with Redis caching layer"

test_deployment "CACHE-02" "Kubernetes" "PostGIS" "Redis" "Implicit" \
    "Deploy to k8s with cache"

test_deployment "CACHE-03" "AWS" "RDS-PostgreSQL" "ElastiCache" "AWS" \
    "AWS with ElastiCache Redis"

# ============================================================================
# GENERATE REPORT
# ============================================================================

echo "╔══════════════════════════════════════════════════════════════════╗"
echo "║  TEST MATRIX COMPLETE - Generating Report                       ║"
echo "╚══════════════════════════════════════════════════════════════════╝"
echo ""

# Create HTML report
cat > "$RESULTS_DIR/report.html" <<'EOF'
<!DOCTYPE html>
<html>
<head>
    <title>Honua AI Devsecops - Exhaustive Test Report</title>
    <style>
        body { font-family: monospace; margin: 20px; background: #1a1a1a; color: #00ff00; }
        h1 { color: #00ff00; }
        table { border-collapse: collapse; width: 100%; margin: 20px 0; }
        th, td { border: 1px solid #00ff00; padding: 8px; text-align: left; }
        th { background: #003300; }
        .pass { color: #00ff00; font-weight: bold; }
        .fail { color: #ff0000; font-weight: bold; }
        .broken { color: #ff6600; font-weight: bold; }
        .partial { color: #ffff00; font-weight: bold; }
        .unknown { color: #888888; }
        .section { background: #002200; font-weight: bold; }
    </style>
</head>
<body>
    <h1>Honua AI Devsecops - Exhaustive Test Matrix Results</h1>
    <p><strong>Test Run:</strong> $(date)</p>
    <p><strong>Total Tests:</strong> $((PASS_COUNT + FAIL_COUNT))</p>
    <p><strong>Passed:</strong> <span class="pass">$PASS_COUNT</span></p>
    <p><strong>Failed:</strong> <span class="fail">$FAIL_COUNT</span></p>
    <p><strong>Success Rate:</strong> $(echo "scale=1; $PASS_COUNT * 100 / ($PASS_COUNT + $FAIL_COUNT)" | bc)%</p>

    <h2>Test Results</h2>
    <table>
        <tr>
            <th>Test ID</th>
            <th>Platform</th>
            <th>Database</th>
            <th>Cache</th>
            <th>Result</th>
        </tr>
EOF

# Add results to HTML
tail -n +2 "$RESULTS_DIR/results.csv" | while IFS='|' read test_id platform database cache result; do
    case $result in
        PASS) result_class="pass" ;;
        FAIL) result_class="fail" ;;
        BROKEN) result_class="broken" ;;
        PARTIAL) result_class="partial" ;;
        *) result_class="unknown" ;;
    esac

    echo "        <tr>" >> "$RESULTS_DIR/report.html"
    echo "            <td>$test_id</td>" >> "$RESULTS_DIR/report.html"
    echo "            <td>$platform</td>" >> "$RESULTS_DIR/report.html"
    echo "            <td>$database</td>" >> "$RESULTS_DIR/report.html"
    echo "            <td>$cache</td>" >> "$RESULTS_DIR/report.html"
    echo "            <td class='$result_class'>$result</td>" >> "$RESULTS_DIR/report.html"
    echo "        </tr>" >> "$RESULTS_DIR/report.html"
done

cat >> "$RESULTS_DIR/report.html" <<'EOF'
    </table>

    <h2>Summary by Platform</h2>
EOF

# Platform summary
for platform in "Docker" "Kubernetes" "AWS" "Azure" "GCP"; do
    platform_pass=$(grep "|$platform|" "$RESULTS_DIR/results.csv" | grep -c "PASS" || echo "0")
    platform_total=$(grep -c "|$platform|" "$RESULTS_DIR/results.csv" || echo "0")

    if [ $platform_total -gt 0 ]; then
        echo "    <p><strong>$platform:</strong> $platform_pass / $platform_total passed</p>" >> "$RESULTS_DIR/report.html"
    fi
done

cat >> "$RESULTS_DIR/report.html" <<'EOF'
</body>
</html>
EOF

echo "Results saved to: $RESULTS_DIR"
echo "HTML Report: $RESULTS_DIR/report.html"
echo "CSV Data: $RESULTS_DIR/results.csv"
echo ""
echo "Summary:"
echo "  Total: $((PASS_COUNT + FAIL_COUNT))"
echo "  Passed: $PASS_COUNT"
echo "  Failed: $FAIL_COUNT"
echo "  Success Rate: $(echo "scale=1; $PASS_COUNT * 100 / ($PASS_COUNT + $FAIL_COUNT)" | bc)%"
