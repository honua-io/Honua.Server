#!/bin/bash
# E2E Test: Docker Compose with MySQL + Traefik + Redis
# Tests AI Assistant's ability to deploy alternative database stack

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
export PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

echo "=== Test: Docker Compose with MySQL + Traefik + Redis ==="

# Generate docker-compose.yml for MySQL stack
cat > "$TEST_DIR/docker-compose/test-mysql.yml" <<'EOF'
version: '3.8'

services:
  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: honua_root_pass
      MYSQL_DATABASE: honua
      MYSQL_USER: honua_user
      MYSQL_PASSWORD: honua_password
    command: --default-authentication-plugin=mysql_native_password
    volumes:
      - mysql-data:/var/lib/mysql
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-phonua_root_pass"]
      interval: 10s
      timeout: 5s
      retries: 10

  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

  honua:
    image: mcr.microsoft.com/dotnet/sdk:9.0
    working_dir: /app
    command: dotnet run --project src/Honua.Server.Host --no-build -c Release --urls http://0.0.0.0:5000
    environment:
      - HONUA__DATABASE__PROVIDER=mysql
      - HONUA__DATABASE__CONNECTIONSTRING=Server=mysql;Database=honua;User=honua_user;Password=honua_password;
      - HONUA__METADATA__PROVIDER=json
      - HONUA__METADATA__PATH=/app/tests/e2e-assistant/docker-compose/metadata-mysql.json
      - HONUA__AUTHENTICATION__MODE=QuickStart
      - HONUA__AUTHENTICATION__ENFORCE=false
      - HONUA__SERVICES__REDIS__ENABLED=true
      - HONUA__SERVICES__REDIS__CONNECTIONSTRING=redis:6379
    volumes:
      - ${PROJECT_ROOT}:/app
    depends_on:
      mysql:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "sh", "-c", "curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ogc | grep -E '^(200|401)$$'"]
      interval: 10s
      timeout: 5s
      retries: 15
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.honua.rule=PathPrefix(`/`)"
      - "traefik.http.services.honua.loadbalancer.server.port=5000"

  traefik:
    image: traefik:v2.10
    command:
      - "--api.insecure=true"
      - "--providers.docker=true"
      - "--providers.docker.exposedbydefault=false"
      - "--entrypoints.web.address=:80"
      - "--accesslog=true"
    ports:
      - "20080:80"
      - "20081:8080"  # Traefik dashboard
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    depends_on:
      honua:
        condition: service_healthy

volumes:
  mysql-data:
EOF

METADATA_TEMPLATE="$TEST_DIR/docker-compose/metadata-template.json"
METADATA_FILE="$TEST_DIR/docker-compose/metadata-mysql.json"
cp "$METADATA_TEMPLATE" "$METADATA_FILE"
jq '(.dataSources[0].id) = "mysql-primary"
  | (.dataSources[0].provider) = "mysql"
  | (.dataSources[0].connectionString) = "Server=mysql;Database=honua;User=honua_user;Password=honua_password"
  | (.services[0].dataSourceId) = "mysql-primary"' \
  "$METADATA_FILE" > "$METADATA_FILE.tmp" && mv "$METADATA_FILE.tmp" "$METADATA_FILE"

# Seed MySQL schema and data script
cat > "$TEST_DIR/docker-compose/init-mysql.sql" <<'EOF'
CREATE DATABASE IF NOT EXISTS honua;
USE honua;

CREATE TABLE IF NOT EXISTS roads_primary (
    road_id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    road_class VARCHAR(50) NOT NULL,
    observed_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    geom POINT NOT NULL SRID 4326
);

DELETE FROM roads_primary;

INSERT INTO roads_primary (name, road_class, geom)
VALUES
    ('Main Street', 'highway', ST_SRID(POINT(-122.45, 45.55), 4326)),
    ('Oak Avenue', 'local', ST_SRID(POINT(-122.46, 45.56), 4326)),
    ('River Road', 'highway', ST_SRID(POINT(-122.47, 45.57), 4326));
EOF

echo "✓ Configuration files generated"

# Start the stack
echo "Starting Docker Compose stack..."
cd "$TEST_DIR/docker-compose"
PROJECT_ROOT="$PROJECT_ROOT" docker-compose -f test-mysql.yml -p honua-test-mysql up -d

# Prepare MySQL schema and seed data
echo "Preparing MySQL schema..."
MYSQL_CONTAINER="honua-test-mysql-mysql-1"
for i in {1..60}; do
    if docker exec "$MYSQL_CONTAINER" mysqladmin ping -uroot -phonua_root_pass >/dev/null 2>&1; then
        docker cp "$TEST_DIR/docker-compose/init-mysql.sql" "$MYSQL_CONTAINER":/tmp/init-mysql.sql
        docker exec "$MYSQL_CONTAINER" sh -c "mysql -uroot -phonua_root_pass < /tmp/init-mysql.sql" >/dev/null
        break
    fi
    if [ $i -eq 60 ]; then
        echo "✗ MySQL did not become ready in time"
        exit 1
    fi
    sleep 2
done

# Wait for services to be healthy with active polling
echo "Waiting for services to be ready..."
MAX_WAIT=180  # 3 minutes for MySQL (slower startup than PostGIS)
ELAPSED=0
INTERVAL=5

while [ $ELAPSED -lt $MAX_WAIT ]; do
    # Check container health status
    if docker-compose -f test-mysql.yml -p honua-test-mysql ps | grep -q "unhealthy"; then
        echo "⏳ Services unhealthy at ${ELAPSED}s, waiting..."
    elif docker-compose -f test-mysql.yml -p honua-test-mysql ps honua | grep -q "Up"; then
        HTTP_CODE=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:20080/ogc 2>/dev/null)
        if echo "$HTTP_CODE" | grep -qE "^(200|401)$"; then
            echo "✓ Honua is ready after ${ELAPSED}s (HTTP $HTTP_CODE)"
            break
        fi
        echo "⏳ Service responded with HTTP $HTTP_CODE at ${ELAPSED}s, waiting..."
    else
        echo "⏳ Containers starting at ${ELAPSED}s..."
    fi

    sleep $INTERVAL
    ELAPSED=$((ELAPSED + INTERVAL))
done

if [ $ELAPSED -ge $MAX_WAIT ]; then
    echo "✗ Timeout after ${MAX_WAIT}s"
    echo "Container status:"
    docker-compose -f test-mysql.yml -p honua-test-mysql ps
    echo "Honua logs:"
    docker-compose -f test-mysql.yml -p honua-test-mysql logs honua
    exit 1
fi

# Test endpoints
echo "Testing OGC API endpoints..."

# Test 1: OGC Landing Page
if curl -s http://localhost:20080/ogc > /dev/null; then
    echo "✓ OGC landing page accessible"
else
    echo "✗ Failed to access OGC landing page"
    docker-compose -f test-mysql.yml -p honua-test-mysql logs honua
    exit 1
fi

# Test 2: Collections endpoint
if curl -s http://localhost:20080/ogc/collections | jq -e '.collections' > /dev/null; then
    echo "✓ OGC collections endpoint working"
else
    echo "✗ Failed to access collections"
    exit 1
fi

# Test 3: Feature items endpoint structure
RESPONSE=$(curl -s "http://localhost:20080/ogc/collections/roads::roads-primary/items?limit=10")
if echo "$RESPONSE" | jq -e '.type == "FeatureCollection" and .features != null' > /dev/null; then
    FEATURE_COUNT=$(echo "$RESPONSE" | jq '.features | length')
    echo "✓ Feature items endpoint accessible (returned $FEATURE_COUNT features)"
else
    echo "✗ Failed to access feature items endpoint"
    echo "Response: $RESPONSE"
    exit 1
fi

# Test 4: Redis connection
if docker exec honua-test-mysql-redis-1 redis-cli ping | grep -q PONG; then
    echo "✓ Redis responding"
else
    echo "✗ Redis not responding"
    exit 1
fi

# Test 5: MySQL connectivity
if docker exec honua-test-mysql-mysql-1 mysqladmin ping -u root -phonua_root_pass 2>/dev/null | grep -q "mysqld is alive"; then
    echo "✓ MySQL responding"
else
    echo "✗ MySQL not responding"
    exit 1
fi

# Test 6: Traefik dashboard
if curl -s http://localhost:20081/api/overview | jq -e '.http' > /dev/null; then
    echo "✓ Traefik dashboard accessible"
else
    echo "✗ Traefik dashboard not accessible"
    exit 1
fi

# Test 7: Traefik routing
HEADERS=$(curl -s -I http://localhost:20080/ogc)
if echo "$HEADERS" | grep -q "HTTP"; then
    echo "✓ Traefik routing working"
else
    echo "✗ Traefik routing failed"
    exit 1
fi

# Cleanup
echo "Cleaning up..."
docker-compose -f test-mysql.yml -p honua-test-mysql down -v

echo "=== Test PASSED ==="
