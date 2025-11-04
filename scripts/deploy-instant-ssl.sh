#!/bin/bash
# Honua Instant SSL Deployment Script
# Deploys Honua with automatic HTTPS using nip.io + Caddy
# No DNS configuration required - works immediately!

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${GREEN}╔═══════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   Honua Instant SSL Deployment               ║${NC}"
echo -e "${GREEN}║   Auto HTTPS with nip.io + Caddy              ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════╝${NC}"
echo

# Configuration
DEPLOY_DIR="${1:-./honua-deploy}"
DATABASE_PROVIDER="${DATABASE_PROVIDER:-sqlite}"
ENABLE_REDIS="${ENABLE_REDIS:-true}"

# Detect public IP
echo -e "${YELLOW}🔍 Detecting public IP address...${NC}"
PUBLIC_IP=$(curl -s --max-time 5 ifconfig.me || curl -s --max-time 5 icanhazip.com || echo "")

if [ -z "$PUBLIC_IP" ]; then
    echo -e "${RED}❌ Could not detect public IP. Using localhost instead.${NC}"
    PUBLIC_IP="127.0.0.1"
    DOMAIN="honua.localho.st"
else
    echo -e "${GREEN}✓ Public IP detected: $PUBLIC_IP${NC}"
    DOMAIN="honua.$PUBLIC_IP.nip.io"
fi

echo -e "${GREEN}🌐 Your Honua instance will be available at: https://$DOMAIN${NC}"
echo

# Create deployment directory
mkdir -p "$DEPLOY_DIR"
cd "$DEPLOY_DIR"

# Generate Caddyfile
echo -e "${YELLOW}📝 Generating Caddyfile...${NC}"
cat > Caddyfile <<EOF
{
    email admin@example.com
    # Use staging for testing (no rate limits)
    # acme_ca https://acme-staging-v02.api.letsencrypt.org/directory
}

$DOMAIN {
    reverse_proxy honua:5000

    # Enable compression
    encode gzip zstd

    # Security headers
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "SAMEORIGIN"
        X-XSS-Protection "1; mode=block"
        Referrer-Policy "strict-origin-when-cross-origin"
        Permissions-Policy "geolocation=(), microphone=(), camera=()"
    }

    # CORS for OGC services (optional - enable if needed)
    # @ogc {
    #     path /ogc/*
    # }
    # header @ogc {
    #     Access-Control-Allow-Origin *
    #     Access-Control-Allow-Methods "GET, POST, OPTIONS"
    #     Access-Control-Allow-Headers "Content-Type, Authorization"
    # }

    # Logging
    log {
        output file /var/log/caddy/access.log {
            roll_size 10mb
            roll_keep 5
        }
        format json
    }

    # Health check endpoint (no auth required)
    handle /health {
        reverse_proxy honua:5000
    }

    # Handle ACME challenges (required for Let's Encrypt)
    handle /.well-known/acme-challenge/* {
        root * /var/www/html
        file_server
    }
}
EOF

# Generate docker-compose.yml
echo -e "${YELLOW}📝 Generating docker-compose.yml...${NC}"

# Determine database configuration
case $DATABASE_PROVIDER in
    postgis)
        DB_CONFIG=$(cat <<'DBEOF'
  postgis:
    image: postgis/postgis:15-3.3
    environment:
      POSTGRES_DB: honua
      POSTGRES_USER: honua_user
      POSTGRES_PASSWORD: honua_password
    volumes:
      - postgis-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua_user -d honua"]
      interval: 10s
      timeout: 5s
      retries: 5
DBEOF
)
        CONNECTION_STRING="Host=postgis;Database=honua;Username=honua_user;Password=honua_password"
        DEPENDS_ON="postgis"
        ;;
    sqlserver)
        DB_CONFIG=$(cat <<'DBEOF'
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "HonuaSecure123!"
      MSSQL_PID: Express
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "HonuaSecure123!" -Q "SELECT 1" -b -C
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
DBEOF
)
        CONNECTION_STRING="Server=sqlserver;Database=honua;User Id=sa;Password=HonuaSecure123!;TrustServerCertificate=True"
        DEPENDS_ON="sqlserver"
        ;;
    mysql)
        DB_CONFIG=$(cat <<'DBEOF'
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
DBEOF
)
        CONNECTION_STRING="Server=mysql;Database=honua;User=honua_user;Password=honua_password"
        DEPENDS_ON="mysql"
        ;;
    sqlite)
        DB_CONFIG=""
        CONNECTION_STRING="Data Source=/data/honua.db"
        DEPENDS_ON=""
        ;;
    *)
        echo -e "${RED}❌ Unknown database provider: $DATABASE_PROVIDER${NC}"
        exit 1
        ;;
esac

# Redis configuration
if [ "$ENABLE_REDIS" = "true" ]; then
    REDIS_CONFIG=$(cat <<'REDISEOF'
  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5
REDISEOF
)
    REDIS_ENV=$(cat <<'REDISENVEOF'
      - HONUA__SERVICES__REDIS__ENABLED=true
      - HONUA__SERVICES__REDIS__CONNECTIONSTRING=redis:6379
REDISENVEOF
)
    REDIS_DEPENDS="redis"
else
    REDIS_CONFIG=""
    REDIS_ENV=""
    REDIS_DEPENDS=""
fi

# Combine dependencies
if [ -n "$DEPENDS_ON" ] && [ -n "$REDIS_DEPENDS" ]; then
    COMBINED_DEPENDS="$DEPENDS_ON,${REDIS_DEPENDS}"
elif [ -n "$DEPENDS_ON" ]; then
    COMBINED_DEPENDS="$DEPENDS_ON"
elif [ -n "$REDIS_DEPENDS" ]; then
    COMBINED_DEPENDS="$REDIS_DEPENDS"
else
    COMBINED_DEPENDS=""
fi

# Generate docker-compose.yml
cat > docker-compose.yml <<EOF
version: '3.8'

services:
$DB_CONFIG

$REDIS_CONFIG

  honua:
    image: honua-server:latest
    build:
      context: ../..
      dockerfile: src/Honua.Server.Host/Dockerfile
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - HONUA__DATABASE__PROVIDER=$DATABASE_PROVIDER
      - HONUA__DATABASE__CONNECTIONSTRING=$CONNECTION_STRING
      - HONUA__METADATA__PROVIDER=json
      - HONUA__METADATA__PATH=/app/samples/ogc/metadata.json
      - HONUA__AUTHENTICATION__MODE=QuickStart
      - HONUA__AUTHENTICATION__ENFORCE=false
$REDIS_ENV
    volumes:
      - honua-data:/data
EOF

if [ "$DATABASE_PROVIDER" = "sqlite" ]; then
    echo "      - ../../samples:/app/samples" >> docker-compose.yml
fi

if [ -n "$COMBINED_DEPENDS" ]; then
    echo "    depends_on:" >> docker-compose.yml
    IFS=',' read -ra DEPS <<< "$COMBINED_DEPENDS"
    for dep in "${DEPS[@]}"; do
        echo "      ${dep}:" >> docker-compose.yml
        echo "        condition: service_healthy" >> docker-compose.yml
    done
fi

cat >> docker-compose.yml <<EOF
    expose:
      - "5000"
    healthcheck:
      test: ["CMD", "sh", "-c", "curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ogc | grep -E '^(200|401)$'"]
      interval: 10s
      timeout: 5s
      retries: 15

  caddy:
    image: caddy:2-alpine
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"  # HTTP/3
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data
      - caddy_config:/config
      - caddy_logs:/var/log/caddy
    depends_on:
      honua:
        condition: service_healthy
    restart: unless-stopped

volumes:
  honua-data:
  caddy_data:
  caddy_config:
  caddy_logs:
EOF

# Add database-specific volumes
case $DATABASE_PROVIDER in
    postgis)
        echo "  postgis-data:" >> docker-compose.yml
        ;;
    sqlserver)
        echo "  sqlserver-data:" >> docker-compose.yml
        ;;
    mysql)
        echo "  mysql-data:" >> docker-compose.yml
        ;;
esac

if [ "$ENABLE_REDIS" = "true" ]; then
    echo "  redis-data:" >> docker-compose.yml
fi

echo -e "${GREEN}✓ Configuration files generated${NC}"
echo

# Deploy
echo -e "${YELLOW}🚀 Starting deployment...${NC}"
docker-compose up -d --build

echo
echo -e "${GREEN}✓ Deployment started!${NC}"
echo
echo -e "${GREEN}╔═══════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              DEPLOYMENT COMPLETE              ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════╝${NC}"
echo
echo -e "${YELLOW}📍 URL:${NC}          https://$DOMAIN"
echo -e "${YELLOW}🔒 SSL:${NC}          Automatic (Let's Encrypt)"
echo -e "${YELLOW}💾 Database:${NC}     $DATABASE_PROVIDER"
echo -e "${YELLOW}⚡ Redis:${NC}        $ENABLE_REDIS"
echo
echo -e "${YELLOW}⏳ Certificate Generation:${NC}"
echo -e "   The SSL certificate will be issued automatically."
echo -e "   This may take 30-60 seconds on first deployment."
echo
echo -e "${YELLOW}📊 Check Status:${NC}"
echo -e "   docker-compose logs -f"
echo
echo -e "${YELLOW}🧪 Test Endpoints:${NC}"
echo -e "   curl -k https://$DOMAIN/ogc"
echo -e "   curl -k https://$DOMAIN/ogc/collections"
echo
echo -e "${YELLOW}🛑 Stop:${NC}"
echo -e "   docker-compose down"
echo
echo -e "${GREEN}✨ Honua is deploying with automatic HTTPS!${NC}"
