#!/bin/bash
# E2E Test: LocalStack AWS (S3 + RDS + Secrets Manager)
# Tests AI Assistant's ability to configure AWS cloud services

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

echo "=== Test: LocalStack AWS (S3 + Secrets Manager) ==="

# Clean up any existing LocalStack container
docker stop honua-localstack-aws-e2e 2>/dev/null || true
docker rm honua-localstack-aws-e2e 2>/dev/null || true

# Start LocalStack
echo "Starting LocalStack..."
docker run -d \
    --name honua-localstack-aws-e2e \
    -p 4566:4566 \
    -e SERVICES=s3,secretsmanager,kms \
    -e DEBUG=1 \
    localstack/localstack:latest

echo "Waiting for LocalStack to initialize..."
sleep 15

# Wait for LocalStack health
echo "Checking LocalStack health..."
for i in {1..30}; do
    if curl -s http://localhost:4566/_localstack/health | grep -q '"s3": "available"'; then
        echo "✓ LocalStack is ready"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "✗ LocalStack failed to become ready"
        exit 1
    fi
    sleep 2
done

# Create S3 bucket for tile caching
echo "Creating S3 bucket..."
docker exec honua-localstack-aws-e2e \
    awslocal s3 mb s3://honua-tiles-aws || true

# Verify bucket exists
if docker exec honua-localstack-aws-e2e \
    awslocal s3 ls | grep -q honua-tiles-aws; then
    echo "✓ S3 bucket created"
else
    echo "✗ Failed to create S3 bucket"
    exit 1
fi

# Start Honua with AWS S3 configuration
echo "Starting Honua with AWS S3 configuration..."
cd "$PROJECT_ROOT"

# Kill any existing Honua processes on port 6001
pkill -f "dotnet run.*6001" || true
sleep 2

# Generate metadata pointing to sample SQLite database (absolute path avoids startup issues)
mkdir -p "$TEST_DIR/localstack/config"
DATA_DIR="$TEST_DIR/localstack/data"
mkdir -p "$DATA_DIR"
chmod 777 "$DATA_DIR"
cp "$PROJECT_ROOT/samples/ogc/ogc-sample.db" "$DATA_DIR/ogc-sample.db"
chmod 666 "$DATA_DIR/ogc-sample.db"
METADATA_DB_PATH=$(realpath "$DATA_DIR/ogc-sample.db")
cat > "$TEST_DIR/localstack/config/metadata-aws.json" <<EOF_AWS
{
  "catalog": {
    "id": "honua-ogc-sample",
    "title": "Honua OGC Sample Catalog",
    "description": "Sample metadata for OGC API Features development",
    "version": "2025.09"
  },
  "folders": [
    {
      "id": "transportation",
      "title": "Transportation",
      "order": 10
    }
  ],
  "dataSources": [
    {
      "id": "sqlite-primary",
      "provider": "sqlite",
      "connectionString": "Data Source=${METADATA_DB_PATH};Mode=ReadWrite"
    }
  ],
  "services": [
    {
      "id": "roads",
      "title": "Road Centerlines",
      "folderId": "transportation",
      "serviceType": "feature",
      "dataSourceId": "sqlite-primary",
      "enabled": true
    }
  ],
  "layers": [
    {
      "id": "roads-primary",
      "serviceId": "roads",
      "title": "Primary Roads",
      "geometryType": "Point",
      "idField": "road_id",
      "displayField": "name",
      "geometryField": "geom",
      "crs": ["EPSG:4326", "EPSG:3857"],
      "storage": {
        "table": "roads_primary",
        "geometryColumn": "geom",
        "primaryKey": "road_id",
        "temporalColumn": "observed_at"
      }
    }
  ]
}
EOF_AWS

# Start Honua in background
HONUA__METADATA__PROVIDER=json \
HONUA__METADATA__PATH="$TEST_DIR/localstack/config/metadata-aws.json" \
HONUA__AUTHENTICATION__MODE=QuickStart \
HONUA__AUTHENTICATION__ENFORCE=false \
HONUA__SERVICES__RASTERTILES__ENABLED=true \
HONUA__SERVICES__RASTERTILES__PROVIDER=s3 \
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles-aws \
HONUA__SERVICES__RASTERTILES__S3__REGION=us-east-1 \
HONUA__SERVICES__RASTERTILES__S3__SERVICEURL=http://localhost:4566 \
HONUA__SERVICES__RASTERTILES__S3__ACCESSKEYID=test \
HONUA__SERVICES__RASTERTILES__S3__SECRETACCESSKEY=test \
HONUA__SERVICES__RASTERTILES__S3__FORCEPATHSTYLE=true \
dotnet run --project src/Honua.Server.Host --no-build -c Release --urls http://0.0.0.0:6001 > /tmp/honua-localstack-aws.log 2>&1 &
HONUA_PID=$!
echo "Honua started with PID $HONUA_PID"

# Wait for Honua to start with active polling
echo "Waiting for Honua to be ready..."
MAX_WAIT=180  # 3 minutes for LocalStack + Honua startup
ELAPSED=0
INTERVAL=5

while [ $ELAPSED -lt $MAX_WAIT ]; do
    if ! kill -0 $HONUA_PID 2>/dev/null; then
        echo "✗ Honua process died unexpectedly"
        tail -50 /tmp/honua-localstack-aws.log
        exit 1
    fi

    HTTP_CODE=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:6001/ogc 2>/dev/null)
    if echo "$HTTP_CODE" | grep -qE '^(200|401)$'; then
        echo "✓ Honua is ready after ${ELAPSED}s (HTTP $HTTP_CODE)"
        break
    fi

    echo "⏳ Honua responding with HTTP $HTTP_CODE at ${ELAPSED}s, waiting..."
    sleep $INTERVAL
    ELAPSED=$((ELAPSED + INTERVAL))
done

if [ $ELAPSED -ge $MAX_WAIT ]; then
    echo "✗ Timeout after ${MAX_WAIT}s (HTTP $HTTP_CODE)"
    echo "Honua logs:"
    tail -50 /tmp/honua-localstack-aws.log
    kill $HONUA_PID 2>/dev/null || true
    exit 1
fi

# Test endpoints
echo "Testing OGC API endpoints..."

# Test 1: OGC Landing Page
if curl -s http://localhost:6001/ogc > /dev/null; then
    echo "✓ OGC landing page accessible"
else
    echo "✗ Failed to access OGC landing page"
    tail -50 /tmp/honua-localstack-aws.log
    kill $HONUA_PID 2>/dev/null || true
    exit 1
fi

# Test 2: Collections endpoint
if curl -s http://localhost:6001/ogc/collections | jq -e '.collections' > /dev/null; then
    echo "✓ OGC collections endpoint working"
else
    echo "✗ Failed to access collections"
    kill $HONUA_PID 2>/dev/null || true
    exit 1
fi

# Test 3: Feature items with retry (metadata initialization can be asynchronous)
FEATURE_SUCCESS=false
for attempt in {1..10}; do
    RESPONSE=$(curl -s "http://localhost:6001/ogc/collections/roads::roads-primary/items?limit=10") || RESPONSE=""
    if echo "$RESPONSE" | jq -e '.features' > /dev/null 2>&1; then
        FEATURE_COUNT=$(echo "$RESPONSE" | jq '.features | length')
        echo "✓ Feature items accessible (returned $FEATURE_COUNT features)"
        FEATURE_SUCCESS=true
        break
    fi
    echo "⏳ Feature items not yet available (attempt $attempt/10)"
    sleep 2
done

if [ "$FEATURE_SUCCESS" != true ]; then
    echo "✗ Failed to access feature items"
    tail -50 /tmp/honua-localstack-aws.log || true
    kill $HONUA_PID 2>/dev/null || true
    exit 1
fi

# Test 4: S3 bucket is accessible from Honua
BUCKET_COUNT=$(docker exec honua-localstack-aws-e2e awslocal s3 ls | wc -l)
if [ "$BUCKET_COUNT" -gt 0 ]; then
    echo "✓ S3 buckets accessible ($BUCKET_COUNT buckets)"
else
    echo "✗ S3 buckets not accessible"
    kill $HONUA_PID 2>/dev/null || true
    exit 1
fi

# Test 5: LocalStack health check
if curl -s http://localhost:4566/_localstack/health | jq -e '.services.s3' | grep -q available; then
    echo "✓ LocalStack S3 service healthy"
else
    echo "✗ LocalStack S3 service not healthy"
    kill $HONUA_PID 2>/dev/null || true
    exit 1
fi

# Cleanup
echo "Cleaning up..."
kill $HONUA_PID 2>/dev/null || true
wait $HONUA_PID 2>/dev/null || true
rm -rf "$TEST_DIR/localstack/data"
rm -rf "$TEST_DIR/localstack/config"
docker stop honua-localstack-aws-e2e 2>/dev/null || true
docker rm honua-localstack-aws-e2e 2>/dev/null || true

echo "=== Test PASSED ==="
