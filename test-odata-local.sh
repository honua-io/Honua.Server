#!/bin/bash
# Local OData endpoint testing script - bypasses Docker for easier debugging

set -e

echo "========== LOCAL ODATA TESTING =========="

# Create temp directory for test data
TEST_DIR=$(mktemp -d -t honua-local-test-XXXXXX)
echo "Test directory: $TEST_DIR"

# Create test metadata JSON
cat > "$TEST_DIR/metadata.json" <<'EOF'
{
  "catalog": {
    "id": "honua-odata-test",
    "title": "Honua OData Test Catalog",
    "description": "Test catalog for OData endpoint tests",
    "version": "1.0.0"
  },
  "folders": [
    {
      "id": "transportation",
      "title": "Transportation"
    }
  ],
  "dataSources": [
    {
      "id": "sqlite-primary",
      "provider": "sqlite",
      "connectionString": "Data Source=$TEST_DIR/test.db"
    }
  ],
  "services": [
    {
      "id": "roads",
      "title": "Road Centerlines",
      "folderId": "transportation",
      "serviceType": "feature",
      "dataSourceId": "sqlite-primary",
      "enabled": true,
      "description": "Road centerline reference layer",
      "ogc": {
        "collectionsEnabled": true,
        "itemLimit": 1000
      }
    }
  ],
  "layers": [
    {
      "id": "roads-primary",
      "serviceId": "roads",
      "title": "Primary Roads",
      "geometryType": "LineString",
      "idField": "road_id",
      "displayField": "name",
      "geometryField": "geom",
      "itemType": "feature",
      "storage": {
        "table": "roads_primary",
        "geometryColumn": "geom",
        "primaryKey": "road_id",
        "srid": 4326
      },
      "fields": [
        {
          "name": "road_id",
          "dataType": "int",
          "nullable": false
        },
        {
          "name": "name",
          "dataType": "string",
          "nullable": true
        },
        {
          "name": "status",
          "dataType": "string",
          "nullable": true
        },
        {
          "name": "length_km",
          "dataType": "double",
          "nullable": true
        }
      ]
    }
  ],
  "styles": [],
  "layerGroups": [],
  "server": {
    "allowedHosts": ["*"]
  }
}
EOF

# Create test SQLite database
sqlite3 "$TEST_DIR/test.db" <<'SQLEOF'
CREATE TABLE roads_primary (
    road_id INTEGER PRIMARY KEY,
    name TEXT,
    status TEXT,
    length_km REAL,
    geom TEXT
);

INSERT INTO roads_primary (road_id, name, status, length_km, geom) VALUES
(1, 'Main Street', 'active', 5.2, 'LINESTRING(-122.4 45.5, -122.3 45.6)'),
(2, 'Oak Avenue', 'active', 3.1, 'LINESTRING(-122.5 45.4, -122.4 45.5)'),
(3, 'Pine Road', 'inactive', 7.8, 'LINESTRING(-122.3 45.6, -122.2 45.7)');
SQLEOF

echo "Test database created with 3 records"

# Build the application
echo "Building Honua.Server.Host..."
dotnet build src/Honua.Server.Host/Honua.Server.Host.csproj -c Debug

# Start the server in the background
echo "Starting Honua Server..."
cd src/Honua.Server.Host
ASPNETCORE_ENVIRONMENT=Development \
HONUA__METADATA__PROVIDER=json \
HONUA__METADATA__PATH="$TEST_DIR/metadata.json" \
ConnectionStrings__HonuaDb="Data Source=$TEST_DIR/test.db" \
HONUA__AUTHENTICATION__ENFORCE=false \
HONUA__SERVICES__ODATA__ENABLED=true \
ASPNETCORE_URLS="http://localhost:5555" \
dotnet run --no-build --configuration Debug > "$TEST_DIR/server.log" 2>&1 &

SERVER_PID=$!
echo "Server started with PID: $SERVER_PID"
echo "Server log: $TEST_DIR/server.log"

# Wait for server to start
echo "Waiting for server to start..."
for i in {1..30}; do
    if curl -s http://localhost:5555/healthz/live > /dev/null 2>&1; then
        echo "Server is UP!"
        break
    fi
    echo -n "."
    sleep 1
done

if ! curl -s http://localhost:5555/healthz/live > /dev/null 2>&1; then
    echo "ERROR: Server failed to start within 30 seconds"
    echo "Server log output:"
    cat "$TEST_DIR/server.log"
    kill $SERVER_PID 2>/dev/null || true
    exit 1
fi

# Show diagnostic logging from server startup
echo ""
echo "========== SERVER STARTUP LOGS =========="
grep -i "odata\|MapOData\|======" "$TEST_DIR/server.log" || echo "No OData-related logs found"

# Test endpoints
echo ""
echo "========== TESTING ODATA ENDPOINTS =========="

echo ""
echo "1. Testing GET /odata (service document)"
HTTP_CODE=$(curl -s -o "$TEST_DIR/response1.json" -w "%{http_code}" http://localhost:5555/odata)
echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "200" ]; then
    echo "SUCCESS! Response:"
    cat "$TEST_DIR/response1.json" | jq '.' || cat "$TEST_DIR/response1.json"
else
    echo "FAILED! Response:"
    cat "$TEST_DIR/response1.json"
fi

echo ""
echo "2. Testing GET /odata/\$metadata"
HTTP_CODE=$(curl -s -o "$TEST_DIR/response2.xml" -w "%{http_code}" http://localhost:5555/odata/\$metadata)
echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "200" ]; then
    echo "SUCCESS! (First 20 lines):"
    head -20 "$TEST_DIR/response2.xml"
else
    echo "FAILED! Response:"
    cat "$TEST_DIR/response2.xml"
fi

echo ""
echo "3. Testing GET /odata/roads-primary"
HTTP_CODE=$(curl -s -o "$TEST_DIR/response3.json" -w "%{http_code}" http://localhost:5555/odata/roads-primary)
echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "200" ]; then
    echo "SUCCESS! Response:"
    cat "$TEST_DIR/response3.json" | jq '.' || cat "$TEST_DIR/response3.json"
else
    echo "FAILED! Response:"
    cat "$TEST_DIR/response3.json"
fi

# Shutdown
echo ""
echo "========== CLEANUP =========="
echo "Stopping server (PID: $SERVER_PID)"
kill $SERVER_PID 2>/dev/null || true
sleep 2

echo ""
echo "Full server log saved at: $TEST_DIR/server.log"
echo "Test artifacts in: $TEST_DIR"
echo ""
echo "To view full server log:"
echo "cat $TEST_DIR/server.log"
echo ""
echo "To manually test while server is running:"
echo "curl http://localhost:5555/odata"
