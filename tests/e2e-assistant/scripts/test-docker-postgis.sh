#!/bin/bash
# E2E Test: Docker Compose with PostGIS + Nginx + Redis
# Tests AI Assistant's ability to deploy complete GIS stack

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
export PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

echo "=== Test: Docker Compose with PostGIS + Nginx + Redis ==="

# Run consultant to produce plan and workspace outputs
WORKSPACE="$TEST_DIR/workspaces/postgis"
rm -rf "$WORKSPACE"
mkdir -p "$WORKSPACE"

CONSULTANT_CMD=(honua devsecops --workspace "$WORKSPACE" --auto-approve --mode multi --prompt "Deploy Honua on Docker with PostGIS database, Redis cache, and Nginx reverse proxy. Include metadata for a sample roads layer for testing.")

if ! "${CONSULTANT_CMD[@]}"; then
    echo "✗ Consultant command failed"
    exit 1
fi

# Copy consultant outputs into test directory
cp "$WORKSPACE/docker-compose.yml" "$TEST_DIR/docker-compose/test-postgis.yml"
cp "$WORKSPACE/init.sql" "$TEST_DIR/docker-compose/init-postgis.sql"
cp "$WORKSPACE/metadata.json" "$TEST_DIR/docker-compose/metadata-postgis.json"
cp "$WORKSPACE/nginx.conf" "$TEST_DIR/docker-compose/nginx.conf"

echo "✓ Consultant generated deployment artifacts"

cat > "$TEST_DIR/docker-compose/test-postgis.yml" <<'EOF'
version: '3.8'

services:
  postgis:
    image: postgis/postgis:15-3.3
    environment:
      POSTGRES_DB: honua
      POSTGRES_USER: honua_user
      POSTGRES_PASSWORD: honua_password
    volumes:
      - postgis-data:/var/lib/postgresql/data
      - ./init-postgis.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua_user -d honua"]
      interval: 10s
      timeout: 5s
      retries: 5

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
      - HONUA__DATABASE__PROVIDER=postgis
      - HONUA__DATABASE__CONNECTIONSTRING=Host=postgis;Database=honua;Username=honua_user;Password=honua_password;Pooling=true;MaxPoolSize=100
      - HONUA__METADATA__PROVIDER=json
      - HONUA__METADATA__PATH=/app/tests/e2e-assistant/docker-compose/metadata-postgis.json
      - HONUA__AUTHENTICATION__MODE=QuickStart
      - HONUA__AUTHENTICATION__ENFORCE=false
      - HONUA__SERVICES__REDIS__ENABLED=true
      - HONUA__SERVICES__REDIS__CONNECTIONSTRING=redis:6379
    volumes:
      - ${PROJECT_ROOT}:/app
    depends_on:
      postgis:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "sh", "-c", "curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ogc | grep -E '^(200|401)$$'"]
      interval: 10s
      timeout: 5s
      retries: 10

  nginx:
    image: nginx:alpine
    ports:
      - "18080:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      honua:
        condition: service_healthy

volumes:
  postgis-data:
EOF

# Generate PostGIS-specific metadata configuration
METADATA_TEMPLATE="$TEST_DIR/docker-compose/metadata-template.json"
METADATA_FILE="$TEST_DIR/docker-compose/metadata-postgis.json"
cp "$METADATA_TEMPLATE" "$METADATA_FILE"
jq '(.dataSources[0].id) = "postgis-primary"
  | (.dataSources[0].provider) = "postgis"
  | (.dataSources[0].connectionString) = "Host=postgis;Database=honua;Username=honua_user;Password=honua_password"
  | (.services[0].dataSourceId) = "postgis-primary"' \
  "$METADATA_FILE" > "$METADATA_FILE.tmp" && mv "$METADATA_FILE.tmp" "$METADATA_FILE"

# Create PostGIS initialization SQL
cat > "$TEST_DIR/docker-compose/init-postgis.sql" <<'EOF'
-- Enable PostGIS extension
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- Create sample table
CREATE TABLE IF NOT EXISTS roads_primary (
    road_id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    road_class VARCHAR(50),
    observed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    geom GEOMETRY(Point, 4326)
);

-- Create spatial index
CREATE INDEX IF NOT EXISTS idx_roads_primary_geom ON roads_primary USING GIST(geom);

-- Insert sample data
INSERT INTO roads_primary (name, road_class, geom) VALUES
    ('Main Street', 'highway', ST_SetSRID(ST_MakePoint(-122.45, 45.55), 4326)),
    ('Oak Avenue', 'local', ST_SetSRID(ST_MakePoint(-122.46, 45.56), 4326)),
    ('River Road', 'highway', ST_SetSRID(ST_MakePoint(-122.47, 45.57), 4326));
EOF

# Create Nginx configuration
cat > "$TEST_DIR/docker-compose/nginx.conf" <<'EOF'
events {
    worker_connections 1024;
}

http {
    upstream honua_backend {
        server honua:5000;
    }

    server {
        listen 80;
        server_name localhost;

        location / {
            proxy_pass http://honua_backend;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;

            # Timeout settings
            proxy_connect_timeout 60s;
            proxy_send_timeout 60s;
            proxy_read_timeout 60s;
        }

        # Health check endpoint
        location /health {
            proxy_pass http://honua_backend/health;
            access_log off;
        }
    }
}
EOF

echo "✓ Configuration files generated"

# Start the stack
echo "Starting Docker Compose stack..."
cd "$TEST_DIR/docker-compose"
PROJECT_ROOT="$PROJECT_ROOT" docker-compose -f test-postgis.yml -p honua-test-postgis up -d

# Wait for services to be healthy
echo "Waiting for services to be ready..."
for i in {1..60}; do
    HTTP_CODE=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:18080/ogc 2>/dev/null)
    if echo "$HTTP_CODE" | grep -qE '^(200|401)$'; then
        echo "✓ Honua is ready (HTTP $HTTP_CODE)"
        break
    fi
    if [ $i -eq 60 ]; then
        echo "✗ Honua failed to become ready (HTTP $HTTP_CODE)"
        docker-compose -f test-postgis.yml -p honua-test-postgis logs honua
        exit 1
    fi
    sleep 2
done

# Test endpoints
echo "Testing OGC API endpoints..."

# Test 1: OGC Landing Page
if curl -s http://localhost:18080/ogc > /dev/null; then
    echo "✓ OGC landing page accessible"
else
    echo "✗ Failed to access OGC landing page"
    docker-compose -f test-postgis.yml -p honua-test-postgis logs honua
    exit 1
fi

# Test 2: Collections endpoint
if curl -s http://localhost:18080/ogc/collections | jq -e '.collections' > /dev/null; then
    echo "✓ OGC collections endpoint working"
else
    echo "✗ Failed to access collections"
    exit 1
fi

# Test 3: Feature items
if curl -s "http://localhost:18080/ogc/collections/roads::roads-primary/items?limit=10" | jq -e '.features' > /dev/null; then
    echo "✓ Feature items accessible"
else
    echo "✗ Failed to access feature items"
    exit 1
fi

# Test 4: Redis connection
if docker exec honua-test-postgis-redis-1 redis-cli ping | grep -q PONG; then
    echo "✓ Redis responding"
else
    echo "✗ Redis not responding"
    exit 1
fi

# Test 5: PostGIS query
FEATURE_COUNT=$(curl -s "http://localhost:18080/ogc/collections/roads::roads-primary/items" | jq '.features | length')
if [ "$FEATURE_COUNT" -gt 0 ]; then
    echo "✓ PostGIS data accessible ($FEATURE_COUNT features)"
else
    echo "✗ No features returned from PostGIS"
    exit 1
fi

# Cleanup
echo "Cleaning up..."
docker-compose -f test-postgis.yml -p honua-test-postgis down -v

echo "=== Test PASSED ==="
