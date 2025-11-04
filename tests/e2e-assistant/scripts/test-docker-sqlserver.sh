#!/bin/bash
# E2E Test: Docker Compose with SQL Server + Caddy + Redis
# Tests AI Assistant's ability to deploy Microsoft stack

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
export PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

echo "=== Test: Docker Compose with SQL Server + Caddy + Redis ==="

# Generate docker-compose.yml for SQL Server stack
cat > "$TEST_DIR/docker-compose/test-sqlserver.yml" <<'EOF'
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "HonuaTest123!"
      MSSQL_PID: Express
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "HonuaTest123!" -Q "SELECT 1" -b -C
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

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
      - HONUA__DATABASE__PROVIDER=sqlserver
      - HONUA__DATABASE__CONNECTIONSTRING=Server=sqlserver;Database=honua;User Id=sa;Password=HonuaTest123!;TrustServerCertificate=True;
      - HONUA__METADATA__PROVIDER=json
      - HONUA__METADATA__PATH=/app/tests/e2e-assistant/docker-compose/metadata-sqlserver.json
      - HONUA__AUTHENTICATION__MODE=QuickStart
      - HONUA__AUTHENTICATION__ENFORCE=false
      - HONUA__SERVICES__REDIS__ENABLED=true
      - HONUA__SERVICES__REDIS__CONNECTIONSTRING=redis:6379
    volumes:
      - ${PROJECT_ROOT}:/app
    depends_on:
      sqlserver:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "sh", "-c", "curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ogc | grep -E '^(200|401)$$'"]
      interval: 10s
      timeout: 5s
      retries: 15

  caddy:
    image: caddy:2-alpine
    ports:
      - "19080:80"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
    depends_on:
      honua:
        condition: service_healthy

volumes:
  sqlserver-data:
EOF

# Generate SQL Server-specific metadata configuration
METADATA_TEMPLATE="$TEST_DIR/docker-compose/metadata-template.json"
METADATA_FILE="$TEST_DIR/docker-compose/metadata-sqlserver.json"
cp "$METADATA_TEMPLATE" "$METADATA_FILE"
jq '(.dataSources[0].id) = "sqlserver-primary"
  | (.dataSources[0].provider) = "sqlserver"
  | (.dataSources[0].connectionString) = "Server=sqlserver;Database=honua;User Id=sa;Password=HonuaTest123!;TrustServerCertificate=True"
  | (.services[0].dataSourceId) = "sqlserver-primary"' \
  "$METADATA_FILE" > "$METADATA_FILE.tmp" && mv "$METADATA_FILE.tmp" "$METADATA_FILE"

# Seed SQL Server schema and data script
cat > "$TEST_DIR/docker-compose/init-sqlserver.sql" <<'EOF'
IF DB_ID('honua') IS NULL
BEGIN
    CREATE DATABASE honua;
END
GO

USE honua;
IF OBJECT_ID('dbo.roads_primary', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.roads_primary (
        road_id INT IDENTITY(1,1) PRIMARY KEY,
        name NVARCHAR(255) NOT NULL,
        road_class NVARCHAR(50) NOT NULL,
        observed_at DATETIME2 DEFAULT SYSUTCDATETIME(),
        geom geometry NOT NULL
    );
END
ELSE
BEGIN
    DELETE FROM dbo.roads_primary;
END
GO

INSERT INTO dbo.roads_primary (name, road_class, geom)
VALUES
    ('Main Street', 'highway', geometry::STGeomFromText('POINT(-122.45 45.55)', 4326)),
    ('Oak Avenue', 'local', geometry::STGeomFromText('POINT(-122.46 45.56)', 4326)),
    ('River Road', 'highway', geometry::STGeomFromText('POINT(-122.47 45.57)', 4326));
GO
EOF

# Create Caddyfile
cat > "$TEST_DIR/docker-compose/Caddyfile" <<'EOF'
:80 {
    reverse_proxy honua:5000 {
        header_up Host {host}
        header_up X-Real-IP {remote}
        header_up X-Forwarded-For {remote}
        header_up X-Forwarded-Proto {scheme}
    }

    # Health check endpoint
    handle /health {
        reverse_proxy honua:5000
    }

    # Enable compression
    encode gzip

    # Logging
    log {
        output stdout
        format console
    }
}
EOF

echo "✓ Configuration files generated"

# Start the stack
echo "Starting Docker Compose stack..."
cd "$TEST_DIR/docker-compose"
PROJECT_ROOT="$PROJECT_ROOT" docker-compose -f test-sqlserver.yml -p honua-test-sqlserver up -d

# Prepare database schema and seed data
echo "Preparing SQL Server schema..."
SQLSERVER_CONTAINER="honua-test-sqlserver-sqlserver-1"
for i in {1..60}; do
    if docker exec "$SQLSERVER_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "HonuaTest123!" -C -Q "SELECT 1" >/dev/null 2>&1; then
        docker cp "$TEST_DIR/docker-compose/init-sqlserver.sql" "$SQLSERVER_CONTAINER":/tmp/init-sqlserver.sql
        docker exec "$SQLSERVER_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "HonuaTest123!" -C -i /tmp/init-sqlserver.sql >/dev/null
        break
    fi
    if [ $i -eq 60 ]; then
        echo "✗ SQL Server did not become ready in time"
        exit 1
    fi
    sleep 2
done

# Wait for services to be healthy with active polling
echo "Waiting for services to be ready..."
MAX_WAIT=180  # 3 minutes for SQL Server (slow startup)
ELAPSED=0
INTERVAL=5

while [ $ELAPSED -lt $MAX_WAIT ]; do
    # Check container health status
    if docker-compose -f test-sqlserver.yml -p honua-test-sqlserver ps | grep -q "unhealthy"; then
        echo "⏳ Services unhealthy at ${ELAPSED}s, waiting..."
    elif docker-compose -f test-sqlserver.yml -p honua-test-sqlserver ps honua | grep -q "Up"; then
        HTTP_CODE=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:19080/ogc 2>/dev/null)
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
    docker-compose -f test-sqlserver.yml -p honua-test-sqlserver ps
    echo "Honua logs:"
    docker-compose -f test-sqlserver.yml -p honua-test-sqlserver logs honua
    exit 1
fi

# Test endpoints
echo "Testing OGC API endpoints..."

# Test 1: OGC Landing Page
if curl -s http://localhost:19080/ogc > /dev/null; then
    echo "✓ OGC landing page accessible"
else
    echo "✗ Failed to access OGC landing page"
    docker-compose -f test-sqlserver.yml -p honua-test-sqlserver logs honua
    exit 1
fi

# Test 2: Collections endpoint
if curl -s http://localhost:19080/ogc/collections | jq -e '.collections' > /dev/null; then
    echo "✓ OGC collections endpoint working"
else
    echo "✗ Failed to access collections"
    exit 1
fi

# Test 3: Feature items endpoint structure
RESPONSE=$(curl -s "http://localhost:19080/ogc/collections/roads::roads-primary/items?limit=10")
if echo "$RESPONSE" | jq -e '.type == "FeatureCollection" and .features != null' > /dev/null; then
    FEATURE_COUNT=$(echo "$RESPONSE" | jq '.features | length')
    echo "✓ Feature items endpoint accessible (returned $FEATURE_COUNT features)"
else
    echo "✗ Failed to access feature items endpoint"
    echo "Response: $RESPONSE"
    exit 1
fi

# Test 4: Redis connection
if docker exec honua-test-sqlserver-redis-1 redis-cli ping | grep -q PONG; then
    echo "✓ Redis responding"
else
    echo "✗ Redis not responding"
    exit 1
fi

# Test 5: SQL Server connectivity
if docker exec honua-test-sqlserver-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "HonuaTest123!" -Q "SELECT 1" -b -C > /dev/null 2>&1; then
    echo "✓ SQL Server responding"
else
    echo "✗ SQL Server not responding"
    exit 1
fi

# Test 6: Caddy proxy headers
HEADERS=$(curl -s -I http://localhost:19080/ogc)
if echo "$HEADERS" | grep -q "HTTP"; then
    echo "✓ Caddy proxy responding"
else
    echo "✗ Caddy proxy not responding correctly"
    exit 1
fi

# Cleanup
echo "Cleaning up..."
docker-compose -f test-sqlserver.yml -p honua-test-sqlserver down -v

echo "=== Test PASSED ==="
