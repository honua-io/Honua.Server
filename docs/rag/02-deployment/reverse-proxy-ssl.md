# Reverse Proxy and SSL Configuration

**Keywords**: proxy, reverse-proxy, ssl, https, letsencrypt, nginx, caddy, traefik, certificates, tls, certbot, cert-manager, ingress, security-headers, hsts, load-balancing, websockets
**Related**: docker-deployment, kubernetes-deployment, aws-ecs-deployment, environment-variables, security-best-practices

## Overview

Production deployments of Honua require SSL/TLS termination and reverse proxy capabilities for security, performance, and scalability. This guide covers production-ready configurations for the most popular reverse proxy solutions.

**Key Benefits**:
- SSL/TLS encryption for secure communications
- Automatic certificate management with Let's Encrypt
- Load balancing across multiple Honua instances
- Rate limiting and DDoS protection
- Static asset caching
- WebSocket support for real-time features
- Security headers (HSTS, CSP, X-Frame-Options)
- Centralized access logging and monitoring

**Covered Solutions**:
1. **Nginx** - Industry-standard, high-performance reverse proxy
2. **Caddy** - Modern proxy with automatic HTTPS
3. **Traefik** - Cloud-native proxy with dynamic configuration
4. **HAProxy** - High-performance load balancer

## Quick Start

### Nginx with Let's Encrypt (Recommended)

```bash
# Create directory structure
mkdir -p honua-prod/{nginx,certbot/conf,certbot/www,config}

# Download Nginx config
cd honua-prod

# Create docker-compose.yml
cat > docker-compose.yml << 'EOF'
version: '3.8'
services:
  honua:
    image: honua:latest
    environment:
      HONUA__METADATA__PROVIDER: json
      HONUA__METADATA__PATH: /app/config/metadata.json
    volumes:
      - ./config:/app/config:ro
    networks:
      - honua-network
    restart: unless-stopped

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certbot/conf:/etc/letsencrypt:ro
      - ./certbot/www:/var/www/certbot:ro
    depends_on:
      - honua
    networks:
      - honua-network
    restart: unless-stopped

  certbot:
    image: certbot/certbot
    volumes:
      - ./certbot/conf:/etc/letsencrypt
      - ./certbot/www:/var/www/certbot
    entrypoint: "/bin/sh -c 'trap exit TERM; while :; do certbot renew; sleep 12h & wait $${!}; done;'"

networks:
  honua-network:
    driver: bridge
EOF

# Start services
docker-compose up -d
```

### Caddy (Zero-Config HTTPS)

```bash
# Create Caddyfile
cat > Caddyfile << 'EOF'
honua.example.com {
    reverse_proxy honua:8080
}
EOF

# Start with Docker
docker run -d \
  --name caddy \
  -p 80:80 \
  -p 443:443 \
  -v $(pwd)/Caddyfile:/etc/caddy/Caddyfile:ro \
  -v caddy_data:/data \
  -v caddy_config:/config \
  --network honua-network \
  caddy:latest
```

## Nginx Reverse Proxy

### Complete Production Configuration

**nginx.conf** with SSL, security headers, caching, and rate limiting:

```nginx
user nginx;
worker_processes auto;
worker_rlimit_nofile 65535;
error_log /var/log/nginx/error.log warn;
pid /var/run/nginx.pid;

events {
    worker_connections 4096;
    use epoll;
    multi_accept on;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    # Logging format with detailed information
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for" '
                    'rt=$request_time uct="$upstream_connect_time" '
                    'uht="$upstream_header_time" urt="$upstream_response_time"';

    access_log /var/log/nginx/access.log main;

    # Performance optimizations
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    keepalive_requests 100;
    reset_timedout_connection on;
    client_body_timeout 10s;
    send_timeout 10s;

    # Buffer sizes
    client_body_buffer_size 128k;
    client_max_body_size 100m;
    client_header_buffer_size 1k;
    large_client_header_buffers 4 16k;
    output_buffers 1 32k;
    postpone_output 1460;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_min_length 1000;
    gzip_disable "msie6";
    gzip_types
        application/atom+xml
        application/geo+json
        application/javascript
        application/json
        application/ld+json
        application/manifest+json
        application/rdf+xml
        application/rss+xml
        application/x-web-app-manifest+json
        application/xhtml+xml
        application/xml
        font/eot
        font/otf
        font/ttf
        image/svg+xml
        text/css
        text/javascript
        text/plain
        text/xml;

    # Rate limiting zones
    limit_req_zone $binary_remote_addr zone=general_limit:10m rate=10r/s;
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=100r/s;
    limit_req_zone $binary_remote_addr zone=login_limit:10m rate=5r/m;
    limit_conn_zone $binary_remote_addr zone=addr:10m;
    limit_req_status 429;
    limit_conn_status 429;

    # Upstream configuration with load balancing
    upstream honua_backend {
        least_conn;  # Load balancing method
        server honua:8080 max_fails=3 fail_timeout=30s;
        # Add more servers for horizontal scaling:
        # server honua2:8080 max_fails=3 fail_timeout=30s;
        # server honua3:8080 max_fails=3 fail_timeout=30s;

        keepalive 32;
        keepalive_timeout 60s;
        keepalive_requests 100;
    }

    # Cache configuration for static assets
    proxy_cache_path /var/cache/nginx/honua levels=1:2 keys_zone=honua_cache:10m
                     max_size=1g inactive=60m use_temp_path=off;

    # HTTP server - redirect to HTTPS
    server {
        listen 80;
        listen [::]:80;
        server_name honua.example.com;

        # ACME challenge for Let's Encrypt
        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
        }

        # Redirect all other traffic to HTTPS
        location / {
            return 301 https://$server_name$request_uri;
        }
    }

    # HTTPS server
    server {
        listen 443 ssl http2;
        listen [::]:443 ssl http2;
        server_name honua.example.com;

        # SSL configuration
        ssl_certificate /etc/letsencrypt/live/honua.example.com/fullchain.pem;
        ssl_certificate_key /etc/letsencrypt/live/honua.example.com/privkey.pem;
        ssl_trusted_certificate /etc/letsencrypt/live/honua.example.com/chain.pem;

        # SSL protocols and ciphers (Mozilla Modern configuration)
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers 'ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384';
        ssl_prefer_server_ciphers off;

        # SSL session management
        ssl_session_timeout 1d;
        ssl_session_cache shared:SSL:50m;
        ssl_session_tickets off;

        # OCSP stapling
        ssl_stapling on;
        ssl_stapling_verify on;
        resolver 8.8.8.8 8.8.4.4 valid=300s;
        resolver_timeout 5s;

        # Security headers
        add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header X-XSS-Protection "1; mode=block" always;
        add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        add_header Permissions-Policy "geolocation=(), microphone=(), camera=()" always;

        # Content Security Policy (adjust based on your needs)
        add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'self';" always;

        # Remove server header
        server_tokens off;
        more_clear_headers Server;

        # Health check endpoint (no rate limiting, no logging)
        location /health {
            access_log off;
            proxy_pass http://honua_backend;
            proxy_http_version 1.1;
            proxy_set_header Connection "";
        }

        # Authentication endpoints (stricter rate limiting)
        location ~ ^/(auth|login|oauth) {
            limit_req zone=login_limit burst=10 nodelay;
            limit_conn addr 5;

            proxy_pass http://honua_backend;
            proxy_http_version 1.1;

            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Forwarded-Host $server_name;
            proxy_set_header Connection "";

            proxy_connect_timeout 60s;
            proxy_send_timeout 60s;
            proxy_read_timeout 60s;
        }

        # WebSocket support for real-time features
        location /ws {
            limit_req zone=api_limit burst=50 nodelay;

            proxy_pass http://honua_backend;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";

            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;

            # WebSocket timeout
            proxy_read_timeout 86400s;
            proxy_send_timeout 86400s;
        }

        # API endpoints with moderate rate limiting
        location ~ ^/(ogc|odata|geoservices) {
            limit_req zone=api_limit burst=200 nodelay;
            limit_conn addr 20;

            # CORS headers (adjust allowed origins)
            add_header Access-Control-Allow-Origin "*" always;
            add_header Access-Control-Allow-Methods "GET, POST, PUT, DELETE, OPTIONS" always;
            add_header Access-Control-Allow-Headers "Authorization, Content-Type, Accept, Origin" always;
            add_header Access-Control-Max-Age 3600 always;

            if ($request_method = 'OPTIONS') {
                return 204;
            }

            proxy_pass http://honua_backend;
            proxy_http_version 1.1;
            proxy_set_header Connection "";

            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Forwarded-Host $server_name;

            # Timeout configuration
            proxy_connect_timeout 60s;
            proxy_send_timeout 120s;
            proxy_read_timeout 120s;

            # Buffering
            proxy_buffering on;
            proxy_buffer_size 4k;
            proxy_buffers 8 4k;
            proxy_busy_buffers_size 8k;
            proxy_temp_file_write_size 8k;

            # Response headers
            proxy_hide_header X-Powered-By;
        }

        # Static tiles with aggressive caching
        location ~ ^/tiles/ {
            limit_req zone=api_limit burst=500 nodelay;

            proxy_pass http://honua_backend;
            proxy_http_version 1.1;
            proxy_set_header Connection "";

            # Cache configuration
            proxy_cache honua_cache;
            proxy_cache_key "$scheme$request_method$host$request_uri";
            proxy_cache_valid 200 304 7d;
            proxy_cache_valid 404 1m;
            proxy_cache_use_stale error timeout updating http_500 http_502 http_503 http_504;
            proxy_cache_background_update on;
            proxy_cache_lock on;

            add_header X-Cache-Status $upstream_cache_status;
            add_header Cache-Control "public, max-age=604800, immutable";

            # Tile-specific timeouts
            proxy_connect_timeout 5s;
            proxy_read_timeout 30s;
        }

        # All other requests
        location / {
            limit_req zone=general_limit burst=20 nodelay;
            limit_conn addr 10;

            proxy_pass http://honua_backend;
            proxy_http_version 1.1;
            proxy_set_header Connection "";

            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_set_header X-Forwarded-Host $server_name;

            proxy_connect_timeout 60s;
            proxy_send_timeout 60s;
            proxy_read_timeout 60s;
        }
    }
}
```

### Docker Compose with Nginx and Let's Encrypt

Complete production setup with automatic SSL certificate management:

```yaml
version: '3.8'

services:
  postgis:
    image: postgis/postgis:16-3.4
    container_name: honua-postgis
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: honuadb
    volumes:
      - postgis-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua -d honuadb"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - honua-internal

  honua:
    image: honua:latest
    container_name: honua-server
    depends_on:
      postgis:
        condition: service_healthy
    environment:
      HONUA__METADATA__PROVIDER: database
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;Port=5432;Database=honuadb;Username=honua;Password=${POSTGRES_PASSWORD}"
      HONUA__AUTHENTICATION__MODE: OAuth
      HONUA__AUTHENTICATION__ENFORCE: "true"
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
    volumes:
      - ./config:/app/config:ro
      - honua-tiles:/app/tiles
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped
    networks:
      - honua-internal
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G

  nginx:
    image: nginx:alpine
    container_name: honua-nginx
    depends_on:
      - honua
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certbot/conf:/etc/letsencrypt:ro
      - ./certbot/www:/var/www/certbot:ro
      - nginx-cache:/var/cache/nginx
      - nginx-logs:/var/log/nginx
    ports:
      - "80:80"
      - "443:443"
    healthcheck:
      test: ["CMD", "wget", "--quiet", "--tries=1", "--spider", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped
    networks:
      - honua-internal
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 512M

  certbot:
    image: certbot/certbot
    container_name: honua-certbot
    volumes:
      - ./certbot/conf:/etc/letsencrypt
      - ./certbot/www:/var/www/certbot
    entrypoint: "/bin/sh -c 'trap exit TERM; while :; do certbot renew; sleep 12h & wait $${!}; done;'"
    restart: unless-stopped

volumes:
  postgis-data:
    driver: local
  honua-tiles:
    driver: local
  nginx-cache:
    driver: local
  nginx-logs:
    driver: local

networks:
  honua-internal:
    driver: bridge
```

### Initial SSL Certificate Setup

```bash
#!/bin/bash
# init-letsencrypt.sh - Initial Let's Encrypt certificate setup

domains=(honua.example.com)
rsa_key_size=4096
data_path="./certbot"
email="admin@example.com"
staging=0  # Set to 1 for testing

# Create required directories
mkdir -p "$data_path/conf/live/$domains"
mkdir -p "$data_path/www"

# Download recommended TLS parameters
if [ ! -e "$data_path/conf/options-ssl-nginx.conf" ] || [ ! -e "$data_path/conf/ssl-dhparams.pem" ]; then
  echo "### Downloading recommended TLS parameters..."
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot-nginx/certbot_nginx/_internal/tls_configs/options-ssl-nginx.conf > "$data_path/conf/options-ssl-nginx.conf"
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot/certbot/ssl-dhparams.pem > "$data_path/conf/ssl-dhparams.pem"
fi

# Create dummy certificate for nginx to start
echo "### Creating dummy certificate for $domains..."
path="/etc/letsencrypt/live/$domains"
mkdir -p "$data_path/conf/live/$domains"
docker-compose run --rm --entrypoint "\
  openssl req -x509 -nodes -newkey rsa:$rsa_key_size -days 1\
    -keyout '$path/privkey.pem' \
    -out '$path/fullchain.pem' \
    -subj '/CN=localhost'" certbot
echo

# Start nginx
echo "### Starting nginx..."
docker-compose up --force-recreate -d nginx
echo

# Delete dummy certificate
echo "### Deleting dummy certificate for $domains..."
docker-compose run --rm --entrypoint "\
  rm -Rf /etc/letsencrypt/live/$domains && \
  rm -Rf /etc/letsencrypt/archive/$domains && \
  rm -Rf /etc/letsencrypt/renewal/$domains.conf" certbot
echo

# Request real certificate
echo "### Requesting Let's Encrypt certificate for $domains..."
domain_args=""
for domain in "${domains[@]}"; do
  domain_args="$domain_args -d $domain"
done

case "$staging" in
  1) staging_arg="--staging" ;;
  *) staging_arg="" ;;
esac

docker-compose run --rm --entrypoint "\
  certbot certonly --webroot -w /var/www/certbot \
    $staging_arg \
    $domain_args \
    --email $email \
    --rsa-key-size $rsa_key_size \
    --agree-tos \
    --force-renewal" certbot
echo

# Reload nginx
echo "### Reloading nginx..."
docker-compose exec nginx nginx -s reload
```

### Certificate Renewal

Certificates are automatically renewed by the certbot container. Manual renewal:

```bash
# Test renewal (dry-run)
docker-compose run --rm certbot renew --dry-run

# Force renewal
docker-compose run --rm certbot renew --force-renewal

# Reload nginx after renewal
docker-compose exec nginx nginx -s reload
```

## Caddy Reverse Proxy

### Advantages of Caddy

- **Zero-config HTTPS**: Automatic SSL certificate acquisition and renewal
- **Simple configuration**: Human-readable Caddyfile format
- **Built-in ACME support**: No separate certbot container needed
- **Automatic HTTP/2 and HTTP/3**: Modern protocol support out of the box
- **Security by default**: Best-practice headers automatically applied

### Production Caddyfile

```caddyfile
# Global options
{
    # Email for Let's Encrypt notifications
    email admin@example.com

    # ACME CA (use staging for testing)
    # acme_ca https://acme-staging-v02.api.letsencrypt.org/directory

    # Admin API endpoint
    admin off

    # Default SNI
    default_sni honua.example.com
}

# Main site configuration
honua.example.com {
    # Reverse proxy to Honua backend
    reverse_proxy honua:8080 {
        # Load balancing
        lb_policy least_conn
        lb_try_duration 2s
        lb_try_interval 250ms

        # Health checks
        health_uri /health
        health_interval 30s
        health_timeout 5s
        health_status 200

        # Multiple backends for horizontal scaling
        # Add more servers as needed:
        # to honua2:8080
        # to honua3:8080

        # Headers
        header_up Host {upstream_hostport}
        header_up X-Real-IP {remote_host}
        header_up X-Forwarded-For {remote_host}
        header_up X-Forwarded-Proto {scheme}
        header_up X-Forwarded-Host {host}
    }

    # Custom response headers
    header {
        # Security headers
        Strict-Transport-Security "max-age=63072000; includeSubDomains; preload"
        X-Frame-Options "SAMEORIGIN"
        X-Content-Type-Options "nosniff"
        X-XSS-Protection "1; mode=block"
        Referrer-Policy "strict-origin-when-cross-origin"
        Permissions-Policy "geolocation=(), microphone=(), camera=()"

        # Remove server identification
        -Server

        # Content Security Policy
        Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'self';"
    }

    # Rate limiting
    rate_limit {
        zone dynamic {
            key {remote_host}
            events 100
            window 1m
        }
    }

    # Compression
    encode gzip zstd

    # Access logging
    log {
        output file /var/log/caddy/access.log {
            roll_size 100mb
            roll_keep 10
            roll_keep_for 720h
        }
        format json
    }

    # Handle CORS for API endpoints
    @api {
        path /ogc/* /odata/* /geoservices/*
    }

    handle @api {
        header {
            Access-Control-Allow-Origin "*"
            Access-Control-Allow-Methods "GET, POST, PUT, DELETE, OPTIONS"
            Access-Control-Allow-Headers "Authorization, Content-Type, Accept, Origin"
            Access-Control-Max-Age "3600"
        }
        reverse_proxy honua:8080
    }

    # Tile caching
    @tiles {
        path /tiles/*
    }

    handle @tiles {
        header Cache-Control "public, max-age=604800, immutable"
        reverse_proxy honua:8080
    }

    # WebSocket support
    @websocket {
        path /ws/*
    }

    handle @websocket {
        reverse_proxy honua:8080 {
            header_up Upgrade {http.request.header.Upgrade}
            header_up Connection {http.request.header.Connection}
        }
    }
}

# Additional domains (if needed)
www.honua.example.com {
    redir https://honua.example.com{uri} permanent
}
```

### Docker Compose with Caddy

```yaml
version: '3.8'

services:
  postgis:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: honuadb
    volumes:
      - postgis-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua -d honuadb"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - honua-internal

  honua:
    image: honua:latest
    depends_on:
      postgis:
        condition: service_healthy
    environment:
      HONUA__METADATA__PROVIDER: database
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;Port=5432;Database=honuadb;Username=honua;Password=${POSTGRES_PASSWORD}"
      ASPNETCORE_URLS: http://+:8080
    volumes:
      - ./config:/app/config:ro
      - honua-tiles:/app/tiles
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped
    networks:
      - honua-internal

  caddy:
    image: caddy:2-alpine
    container_name: honua-caddy
    depends_on:
      - honua
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"  # HTTP/3
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
      - caddy-logs:/var/log/caddy
    restart: unless-stopped
    networks:
      - honua-internal

volumes:
  postgis-data:
  honua-tiles:
  caddy-data:
  caddy-config:
  caddy-logs:

networks:
  honua-internal:
    driver: bridge
```

### Caddy Deployment

```bash
# Create directory structure
mkdir -p honua-caddy/{config,logs}

# Create Caddyfile
cat > honua-caddy/Caddyfile << 'EOF'
honua.example.com {
    reverse_proxy honua:8080
}
EOF

# Create .env file
cat > honua-caddy/.env << 'EOF'
POSTGRES_PASSWORD=your_secure_password_here
EOF

# Start services
cd honua-caddy
docker-compose up -d

# View Caddy logs
docker-compose logs -f caddy

# Reload Caddyfile after changes
docker-compose exec caddy caddy reload --config /etc/caddy/Caddyfile
```

## Traefik Reverse Proxy

### Advantages of Traefik

- **Dynamic configuration**: Automatically discovers services via Docker labels
- **Native Docker/Kubernetes integration**: No config file updates needed
- **Built-in Let's Encrypt**: ACME protocol support (HTTP-01, DNS-01, TLS-ALPN-01)
- **Dashboard**: Web UI for monitoring and configuration
- **Middlewares**: Composable request/response transformations
- **Multiple providers**: Docker, Kubernetes, Consul, Etcd, and more

### Traefik with Docker Labels

**docker-compose.yml** with Traefik v3:

```yaml
version: '3.8'

services:
  traefik:
    image: traefik:v3.0
    container_name: honua-traefik
    command:
      # API and Dashboard
      - "--api.dashboard=true"
      - "--api.insecure=false"

      # Providers
      - "--providers.docker=true"
      - "--providers.docker.exposedbydefault=false"
      - "--providers.docker.network=honua-web"

      # Entrypoints
      - "--entrypoints.web.address=:80"
      - "--entrypoints.websecure.address=:443"
      - "--entrypoints.web.http.redirections.entrypoint.to=websecure"
      - "--entrypoints.web.http.redirections.entrypoint.scheme=https"

      # Let's Encrypt
      - "--certificatesresolvers.letsencrypt.acme.email=admin@example.com"
      - "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json"
      - "--certificatesresolvers.letsencrypt.acme.httpchallenge=true"
      - "--certificatesresolvers.letsencrypt.acme.httpchallenge.entrypoint=web"

      # Logging
      - "--log.level=INFO"
      - "--accesslog=true"
      - "--accesslog.filepath=/var/log/traefik/access.log"

      # Metrics
      - "--metrics.prometheus=true"
      - "--metrics.prometheus.addEntryPointsLabels=true"
      - "--metrics.prometheus.addServicesLabels=true"
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - traefik-letsencrypt:/letsencrypt
      - traefik-logs:/var/log/traefik
    labels:
      # Dashboard configuration
      - "traefik.enable=true"
      - "traefik.http.routers.dashboard.rule=Host(`traefik.example.com`)"
      - "traefik.http.routers.dashboard.entrypoints=websecure"
      - "traefik.http.routers.dashboard.tls.certresolver=letsencrypt"
      - "traefik.http.routers.dashboard.service=api@internal"
      - "traefik.http.routers.dashboard.middlewares=auth"

      # Basic auth for dashboard (username: admin, password: admin)
      # Generate with: echo $(htpasswd -nb admin admin) | sed 's/\$/\$\$/g'
      - "traefik.http.middlewares.auth.basicauth.users=admin:$$apr1$$8EVjn/nj$$GiLUZqcbueTFeD23SuB6x0"
    restart: unless-stopped
    networks:
      - honua-web
      - honua-internal

  postgis:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: honuadb
    volumes:
      - postgis-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua -d honuadb"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - honua-internal

  honua:
    image: honua:latest
    depends_on:
      postgis:
        condition: service_healthy
    environment:
      HONUA__METADATA__PROVIDER: database
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;Port=5432;Database=honuadb;Username=honua;Password=${POSTGRES_PASSWORD}"
      HONUA__AUTHENTICATION__MODE: OAuth
      ASPNETCORE_URLS: http://+:8080
    volumes:
      - ./config:/app/config:ro
      - honua-tiles:/app/tiles
    labels:
      # Enable Traefik
      - "traefik.enable=true"

      # HTTP router
      - "traefik.http.routers.honua.rule=Host(`honua.example.com`)"
      - "traefik.http.routers.honua.entrypoints=websecure"
      - "traefik.http.routers.honua.tls.certresolver=letsencrypt"

      # Load balancer
      - "traefik.http.services.honua.loadbalancer.server.port=8080"
      - "traefik.http.services.honua.loadbalancer.healthcheck.path=/health"
      - "traefik.http.services.honua.loadbalancer.healthcheck.interval=30s"

      # Middlewares
      - "traefik.http.routers.honua.middlewares=security-headers,rate-limit,compression"

      # Security headers middleware
      - "traefik.http.middlewares.security-headers.headers.stsSeconds=63072000"
      - "traefik.http.middlewares.security-headers.headers.stsIncludeSubdomains=true"
      - "traefik.http.middlewares.security-headers.headers.stsPreload=true"
      - "traefik.http.middlewares.security-headers.headers.forceSTSHeader=true"
      - "traefik.http.middlewares.security-headers.headers.frameDeny=true"
      - "traefik.http.middlewares.security-headers.headers.contentTypeNosniff=true"
      - "traefik.http.middlewares.security-headers.headers.browserXssFilter=true"
      - "traefik.http.middlewares.security-headers.headers.referrerPolicy=strict-origin-when-cross-origin"
      - "traefik.http.middlewares.security-headers.headers.permissionsPolicy=geolocation=(), microphone=(), camera=()"
      - "traefik.http.middlewares.security-headers.headers.customResponseHeaders.X-Robots-Tag=none,noarchive,nosnippet,notranslate,noimageindex"

      # Rate limiting middleware
      - "traefik.http.middlewares.rate-limit.ratelimit.average=100"
      - "traefik.http.middlewares.rate-limit.ratelimit.burst=200"

      # Compression middleware
      - "traefik.http.middlewares.compression.compress=true"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped
    networks:
      - honua-web
      - honua-internal
    deploy:
      replicas: 2  # Horizontal scaling
      resources:
        limits:
          cpus: '2.0'
          memory: 2G

volumes:
  postgis-data:
  honua-tiles:
  traefik-letsencrypt:
  traefik-logs:

networks:
  honua-web:
    external: true
  honua-internal:
    driver: bridge
```

### Traefik Setup

```bash
# Create external network
docker network create honua-web

# Create .env file
cat > .env << 'EOF'
POSTGRES_PASSWORD=your_secure_password_here
EOF

# Start services
docker-compose up -d

# View Traefik logs
docker-compose logs -f traefik

# Access dashboard
# https://traefik.example.com (username: admin, password: admin)
```

### Traefik with File Provider

For static configuration without Docker labels:

**traefik.yml**:

```yaml
api:
  dashboard: true
  insecure: false

entryPoints:
  web:
    address: ":80"
    http:
      redirections:
        entryPoint:
          to: websecure
          scheme: https
  websecure:
    address: ":443"

providers:
  file:
    filename: /etc/traefik/dynamic.yml
    watch: true

certificatesResolvers:
  letsencrypt:
    acme:
      email: admin@example.com
      storage: /letsencrypt/acme.json
      httpChallenge:
        entryPoint: web

log:
  level: INFO

accessLog:
  filePath: /var/log/traefik/access.log
```

**dynamic.yml**:

```yaml
http:
  routers:
    honua:
      rule: "Host(`honua.example.com`)"
      entryPoints:
        - websecure
      service: honua-service
      tls:
        certResolver: letsencrypt
      middlewares:
        - security-headers
        - rate-limit
        - compression

  services:
    honua-service:
      loadBalancer:
        servers:
          - url: "http://honua:8080"
        healthCheck:
          path: /health
          interval: 30s

  middlewares:
    security-headers:
      headers:
        stsSeconds: 63072000
        stsIncludeSubdomains: true
        stsPreload: true
        forceSTSHeader: true
        frameDeny: true
        contentTypeNosniff: true
        browserXssFilter: true
        referrerPolicy: strict-origin-when-cross-origin

    rate-limit:
      rateLimit:
        average: 100
        burst: 200

    compression:
      compress: true
```

### Traefik DNS-01 Challenge

For wildcard certificates using Cloudflare DNS:

```yaml
certificatesResolvers:
  letsencrypt:
    acme:
      email: admin@example.com
      storage: /letsencrypt/acme.json
      dnsChallenge:
        provider: cloudflare
        resolvers:
          - "1.1.1.1:53"
          - "8.8.8.8:53"

# Environment variables for DNS provider
environment:
  CF_API_EMAIL: your-email@example.com
  CF_API_KEY: your-cloudflare-api-key
```

## Kubernetes Ingress

### Nginx Ingress Controller with cert-manager

**cert-manager installation**:

```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Create ClusterIssuer for Let's Encrypt
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@example.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
```

**Ingress resource**:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: honua-ingress
  namespace: honua
  annotations:
    # Ingress class
    kubernetes.io/ingress.class: nginx

    # cert-manager
    cert-manager.io/cluster-issuer: letsencrypt-prod

    # SSL redirect
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"

    # Security headers
    nginx.ingress.kubernetes.io/configuration-snippet: |
      more_set_headers "Strict-Transport-Security: max-age=63072000; includeSubDomains; preload";
      more_set_headers "X-Frame-Options: SAMEORIGIN";
      more_set_headers "X-Content-Type-Options: nosniff";
      more_set_headers "X-XSS-Protection: 1; mode=block";
      more_set_headers "Referrer-Policy: strict-origin-when-cross-origin";

    # Rate limiting
    nginx.ingress.kubernetes.io/limit-rps: "100"
    nginx.ingress.kubernetes.io/limit-burst-multiplier: "2"

    # CORS
    nginx.ingress.kubernetes.io/enable-cors: "true"
    nginx.ingress.kubernetes.io/cors-allow-origin: "*"
    nginx.ingress.kubernetes.io/cors-allow-methods: "GET, POST, PUT, DELETE, OPTIONS"

    # Timeouts
    nginx.ingress.kubernetes.io/proxy-connect-timeout: "60"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "120"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "120"

    # Body size
    nginx.ingress.kubernetes.io/proxy-body-size: "100m"

    # WebSocket support
    nginx.ingress.kubernetes.io/websocket-services: honua

spec:
  tls:
  - hosts:
    - honua.example.com
    secretName: honua-tls
  rules:
  - host: honua.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: honua
            port:
              number: 8080
```

### Traefik Ingress with IngressRoute

```yaml
apiVersion: traefik.containo.us/v1alpha1
kind: IngressRoute
metadata:
  name: honua-ingress
  namespace: honua
spec:
  entryPoints:
    - websecure
  routes:
  - match: Host(`honua.example.com`)
    kind: Rule
    services:
    - name: honua
      port: 8080
    middlewares:
    - name: security-headers
    - name: rate-limit
  tls:
    certResolver: letsencrypt

---
apiVersion: traefik.containo.us/v1alpha1
kind: Middleware
metadata:
  name: security-headers
  namespace: honua
spec:
  headers:
    stsSeconds: 63072000
    stsIncludeSubdomains: true
    stsPreload: true
    forceSTSHeader: true
    frameDeny: true
    contentTypeNosniff: true
    browserXssFilter: true
    referrerPolicy: strict-origin-when-cross-origin

---
apiVersion: traefik.containo.us/v1alpha1
kind: Middleware
metadata:
  name: rate-limit
  namespace: honua
spec:
  rateLimit:
    average: 100
    burst: 200
```

## HAProxy Configuration

High-performance TCP/HTTP load balancer with SSL termination:

**haproxy.cfg**:

```haproxy
global
    maxconn 4096
    log stdout format raw local0
    ssl-default-bind-ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384
    ssl-default-bind-options ssl-min-ver TLSv1.2 no-tls-tickets
    tune.ssl.default-dh-param 2048

defaults
    log global
    mode http
    option httplog
    option dontlognull
    option http-server-close
    option forwardfor except 127.0.0.0/8
    option redispatch
    retries 3
    timeout connect 5s
    timeout client 50s
    timeout server 50s
    timeout http-request 10s
    timeout queue 5s

frontend http_front
    bind *:80
    redirect scheme https code 301 if !{ ssl_fc }

frontend https_front
    bind *:443 ssl crt /etc/haproxy/certs/honua.pem

    # Security headers
    http-response set-header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload"
    http-response set-header X-Frame-Options "SAMEORIGIN"
    http-response set-header X-Content-Type-Options "nosniff"
    http-response set-header X-XSS-Protection "1; mode=block"

    # Rate limiting
    stick-table type ip size 100k expire 30s store http_req_rate(10s)
    http-request track-sc0 src
    http-request deny deny_status 429 if { sc_http_req_rate(0) gt 100 }

    default_backend honua_backend

backend honua_backend
    balance leastconn
    option httpchk GET /health
    http-check expect status 200

    server honua1 honua:8080 check inter 30s rise 2 fall 3 maxconn 100
    # Add more servers for horizontal scaling:
    # server honua2 honua2:8080 check inter 30s rise 2 fall 3 maxconn 100
```

## Production Best Practices

### SSL/TLS Configuration

#### Modern Configuration (Recommended)

Best security with TLSv1.2+ only:

```nginx
ssl_protocols TLSv1.2 TLSv1.3;
ssl_ciphers 'ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384';
ssl_prefer_server_ciphers off;
```

#### Intermediate Configuration

Balance security and compatibility:

```nginx
ssl_protocols TLSv1.2 TLSv1.3;
ssl_ciphers 'ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384:DHE-RSA-CHACHA20-POLY1305:ECDHE-ECDSA-AES128-SHA256:ECDHE-RSA-AES128-SHA256:ECDHE-ECDSA-AES256-SHA384:ECDHE-RSA-AES256-SHA384';
ssl_prefer_server_ciphers off;
```

### HSTS Configuration

HTTP Strict Transport Security (HSTS) forces HTTPS:

```nginx
# Start with low max-age for testing
add_header Strict-Transport-Security "max-age=300; includeSubDomains" always;

# After verification, increase to 1 year
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

# Submit to HSTS preload list (optional, irreversible!)
add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;
```

### Certificate Management

#### Multiple Domains

```nginx
# SNI support for multiple certificates
ssl_certificate /etc/letsencrypt/live/honua.example.com/fullchain.pem;
ssl_certificate_key /etc/letsencrypt/live/honua.example.com/privkey.pem;
ssl_certificate /etc/letsencrypt/live/api.example.com/fullchain.pem;
ssl_certificate_key /etc/letsencrypt/live/api.example.com/privkey.pem;
```

#### Wildcard Certificates

Requires DNS-01 challenge:

```bash
# Using certbot with Cloudflare DNS
certbot certonly \
  --dns-cloudflare \
  --dns-cloudflare-credentials ~/.secrets/cloudflare.ini \
  -d example.com \
  -d '*.example.com'
```

#### Certificate Monitoring

Monitor expiration dates:

```bash
#!/bin/bash
# check-certs.sh

CERT_PATH="/etc/letsencrypt/live/honua.example.com/cert.pem"
DAYS_BEFORE_EXPIRY=$(openssl x509 -enddate -noout -in "$CERT_PATH" | \
  sed 's/notAfter=//' | \
  xargs -I {} date -d {} +%s | \
  awk -v now="$(date +%s)" '{print int(($1 - now) / 86400)}')

if [ "$DAYS_BEFORE_EXPIRY" -lt 30 ]; then
  echo "WARNING: Certificate expires in $DAYS_BEFORE_EXPIRY days"
  # Send alert (email, Slack, PagerDuty, etc.)
fi
```

### Load Balancing Strategies

#### Least Connections (Recommended for Honua)

Routes to server with fewest active connections:

```nginx
upstream honua_backend {
    least_conn;
    server honua1:8080 max_fails=3 fail_timeout=30s;
    server honua2:8080 max_fails=3 fail_timeout=30s;
    server honua3:8080 max_fails=3 fail_timeout=30s;
}
```

#### IP Hash (Session Affinity)

Same client always routed to same server:

```nginx
upstream honua_backend {
    ip_hash;
    server honua1:8080;
    server honua2:8080;
    server honua3:8080;
}
```

#### Weighted Round Robin

Distribute based on server capacity:

```nginx
upstream honua_backend {
    server honua1:8080 weight=3;  # More powerful server
    server honua2:8080 weight=2;
    server honua3:8080 weight=1;
}
```

### Caching Static Assets

```nginx
# Cache static tiles
location ~ ^/tiles/ {
    proxy_pass http://honua_backend;
    proxy_cache honua_cache;
    proxy_cache_key "$scheme$request_method$host$request_uri";
    proxy_cache_valid 200 7d;
    proxy_cache_valid 404 1m;
    proxy_cache_use_stale error timeout updating;
    proxy_cache_background_update on;
    proxy_cache_lock on;

    add_header X-Cache-Status $upstream_cache_status;
    add_header Cache-Control "public, max-age=604800, immutable";
}

# Bypass cache for API requests
location ~ ^/(ogc|odata)/ {
    proxy_pass http://honua_backend;
    proxy_cache_bypass $http_pragma $http_authorization;
    proxy_no_cache $http_pragma $http_authorization;
}
```

### Security Headers

Complete security headers configuration:

```nginx
# HSTS
add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;

# Prevent clickjacking
add_header X-Frame-Options "SAMEORIGIN" always;

# Prevent MIME type sniffing
add_header X-Content-Type-Options "nosniff" always;

# XSS protection (legacy browsers)
add_header X-XSS-Protection "1; mode=block" always;

# Referrer policy
add_header Referrer-Policy "strict-origin-when-cross-origin" always;

# Permissions policy
add_header Permissions-Policy "geolocation=(), microphone=(), camera=()" always;

# Content Security Policy
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'self';" always;

# Remove server identification
server_tokens off;
more_clear_headers Server;
```

### CORS Configuration

Allow cross-origin requests for API endpoints:

```nginx
location ~ ^/(ogc|odata|geoservices)/ {
    # Handle preflight requests
    if ($request_method = 'OPTIONS') {
        add_header Access-Control-Allow-Origin "*" always;
        add_header Access-Control-Allow-Methods "GET, POST, PUT, DELETE, OPTIONS" always;
        add_header Access-Control-Allow-Headers "Authorization, Content-Type, Accept, Origin, X-Requested-With" always;
        add_header Access-Control-Max-Age 3600 always;
        add_header Content-Length 0;
        add_header Content-Type text/plain;
        return 204;
    }

    # Add CORS headers to all responses
    add_header Access-Control-Allow-Origin "*" always;
    add_header Access-Control-Allow-Methods "GET, POST, PUT, DELETE, OPTIONS" always;
    add_header Access-Control-Allow-Headers "Authorization, Content-Type, Accept, Origin, X-Requested-With" always;

    proxy_pass http://honua_backend;
}
```

### Rate Limiting Best Practices

```nginx
# Define zones with different limits
limit_req_zone $binary_remote_addr zone=general:10m rate=10r/s;
limit_req_zone $binary_remote_addr zone=api:10m rate=100r/s;
limit_req_zone $binary_remote_addr zone=login:10m rate=5r/m;
limit_req_zone $binary_remote_addr zone=tiles:10m rate=500r/s;

# Connection limiting
limit_conn_zone $binary_remote_addr zone=addr:10m;

# Apply limits
location / {
    limit_req zone=general burst=20 nodelay;
    limit_conn addr 10;
}

location ~ ^/(ogc|odata)/ {
    limit_req zone=api burst=200 nodelay;
    limit_conn addr 20;
}

location /login {
    limit_req zone=login burst=10 nodelay;
    limit_conn addr 5;
}

location ~ ^/tiles/ {
    limit_req zone=tiles burst=1000 nodelay;
    limit_conn addr 50;
}
```

## Troubleshooting

### Certificate Issues

#### Certificate Not Found

```bash
# Verify certificate files exist
ls -la /etc/letsencrypt/live/honua.example.com/

# Check certificate details
openssl x509 -in /etc/letsencrypt/live/honua.example.com/cert.pem -text -noout

# Test certificate chain
openssl s_client -connect honua.example.com:443 -servername honua.example.com
```

#### Certificate Renewal Failures

```bash
# Check certbot logs
docker-compose logs certbot

# Test renewal (dry-run)
docker-compose run --rm certbot renew --dry-run

# Force renewal
docker-compose run --rm certbot renew --force-renewal

# Check DNS resolution
dig honua.example.com

# Verify HTTP challenge is accessible
curl http://honua.example.com/.well-known/acme-challenge/test
```

#### Mixed Content Warnings

Ensure all resources use HTTPS:

```nginx
# Force HTTPS for all resources
add_header Content-Security-Policy "upgrade-insecure-requests" always;
```

### SSL Handshake Errors

```bash
# Test SSL configuration
openssl s_client -connect honua.example.com:443 -tls1_2
openssl s_client -connect honua.example.com:443 -tls1_3

# Check cipher compatibility
nmap --script ssl-enum-ciphers -p 443 honua.example.com

# Verify certificate chain
curl -v https://honua.example.com

# Test with SSL Labs
# Visit: https://www.ssllabs.com/ssltest/
```

### Port Conflicts

```bash
# Check what's using port 80/443
sudo netstat -tlnp | grep :80
sudo netstat -tlnp | grep :443

# Or using ss
ss -tlnp | grep :80
ss -tlnp | grep :443

# Stop conflicting services
sudo systemctl stop apache2  # If Apache is running
sudo systemctl stop nginx    # If nginx is already running

# Verify ports are free
docker-compose down
sudo lsof -i :80
sudo lsof -i :443
```

### Proxy Timeout Issues

#### 504 Gateway Timeout

Increase timeouts in Nginx:

```nginx
location / {
    proxy_connect_timeout 300s;
    proxy_send_timeout 300s;
    proxy_read_timeout 300s;
    send_timeout 300s;
}
```

Increase timeouts in Caddy:

```caddyfile
reverse_proxy honua:8080 {
    transport http {
        dial_timeout 300s
        response_header_timeout 300s
    }
}
```

#### 502 Bad Gateway

```bash
# Check backend is running
docker-compose ps honua

# Check backend logs
docker-compose logs honua

# Test backend directly
curl http://localhost:8080/health

# Verify network connectivity
docker-compose exec nginx ping honua
```

### WebSocket Connection Issues

```nginx
# Ensure WebSocket headers are forwarded
location /ws {
    proxy_pass http://honua_backend;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;

    # Increase timeouts for long-lived connections
    proxy_read_timeout 86400s;
    proxy_send_timeout 86400s;
}
```

Test WebSocket connection:

```bash
# Install wscat
npm install -g wscat

# Test WebSocket endpoint
wscat -c wss://honua.example.com/ws
```

### High CPU/Memory Usage

```bash
# Check resource usage
docker stats

# View Nginx worker processes
docker-compose exec nginx ps aux

# Adjust worker processes
# In nginx.conf:
worker_processes auto;  # Use number of CPU cores

# Limit worker connections
events {
    worker_connections 1024;  # Adjust based on resources
}
```

### Rate Limiting Issues

```bash
# Check rate limit logs
docker-compose logs nginx | grep "limiting requests"

# Temporarily disable rate limiting (for testing)
# Comment out limit_req lines in nginx.conf

# Adjust rate limits
limit_req_zone $binary_remote_addr zone=api:10m rate=1000r/s;  # Increase rate
```

### Debugging Proxy Configuration

```bash
# Test Nginx configuration
docker-compose exec nginx nginx -t

# Reload Nginx without downtime
docker-compose exec nginx nginx -s reload

# Enable debug logging
# In nginx.conf:
error_log /var/log/nginx/error.log debug;

# View access logs
docker-compose exec nginx tail -f /var/log/nginx/access.log

# View error logs
docker-compose exec nginx tail -f /var/log/nginx/error.log
```

### Testing SSL Configuration

```bash
# Use testssl.sh for comprehensive SSL testing
docker run --rm -ti drwetter/testssl.sh honua.example.com

# Check certificate expiry
echo | openssl s_client -servername honua.example.com -connect honua.example.com:443 2>/dev/null | openssl x509 -noout -dates

# Verify HSTS header
curl -I https://honua.example.com | grep -i strict

# Check security headers
curl -I https://honua.example.com

# Test OCSP stapling
echo QUIT | openssl s_client -connect honua.example.com:443 -status 2> /dev/null | grep -A 17 'OCSP response:'
```

## Monitoring and Logging

### Nginx Metrics

```nginx
# Enable stub_status module
location /nginx_status {
    stub_status;
    access_log off;
    allow 127.0.0.1;
    deny all;
}
```

Export to Prometheus:

```bash
# Use nginx-prometheus-exporter
docker run -d \
  --name nginx-exporter \
  --network honua-network \
  -p 9113:9113 \
  nginx/nginx-prometheus-exporter:latest \
  -nginx.scrape-uri=http://nginx:80/nginx_status
```

### Centralized Logging

Forward logs to external system:

```yaml
# docker-compose.yml
services:
  nginx:
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
        labels: "service,environment"
        env: "NGINX_VERSION"
```

### Access Log Analysis

```bash
# Top IPs by request count
docker-compose exec nginx awk '{print $1}' /var/log/nginx/access.log | sort | uniq -c | sort -rn | head -10

# Response time statistics
docker-compose exec nginx awk '{print $NF}' /var/log/nginx/access.log | sort -n | awk 'BEGIN{c=0;s=0;}{a[c++]=$1;s+=$1}END{print "min: "a[0]" max: "a[c-1]" avg: "s/c" median: "a[int(c/2)]}'

# Status code distribution
docker-compose exec nginx awk '{print $9}' /var/log/nginx/access.log | sort | uniq -c | sort -rn

# Top requested URLs
docker-compose exec nginx awk '{print $7}' /var/log/nginx/access.log | sort | uniq -c | sort -rn | head -10
```

## Performance Optimization

### Connection Pooling

```nginx
upstream honua_backend {
    server honua:8080;
    keepalive 32;
    keepalive_timeout 60s;
    keepalive_requests 100;
}

server {
    location / {
        proxy_pass http://honua_backend;
        proxy_http_version 1.1;
        proxy_set_header Connection "";  # Enable keepalive
    }
}
```

### Compression

```nginx
gzip on;
gzip_vary on;
gzip_proxied any;
gzip_comp_level 6;
gzip_min_length 1000;
gzip_types application/json application/geo+json text/plain text/css application/javascript;
```

### File Descriptor Limits

```nginx
# In nginx.conf
worker_rlimit_nofile 65535;

events {
    worker_connections 4096;
}
```

### Kernel Tuning

```bash
# /etc/sysctl.conf
net.core.somaxconn = 65535
net.ipv4.tcp_max_syn_backlog = 65535
net.ipv4.ip_local_port_range = 1024 65535
net.ipv4.tcp_tw_reuse = 1
net.ipv4.tcp_fin_timeout = 30
```

Apply settings:

```bash
sudo sysctl -p
```

## See Also

- [Docker Deployment](docker-deployment.md) - Container deployment guide
- [Kubernetes Deployment](kubernetes-deployment.md) - Orchestrated deployment
- [AWS ECS Deployment](aws-ecs-deployment.md) - AWS container service
- [Environment Variables](../01-configuration/environment-variables.md) - Configuration reference
- [Performance Tuning](../04-operations/performance-tuning.md) - Performance optimization
- [Troubleshooting](../04-operations/troubleshooting.md) - Diagnostic workflows
- [Security Best Practices](../04-operations/security-best-practices.md) - Security hardening

## External Resources

- [Mozilla SSL Configuration Generator](https://ssl-config.mozilla.org/)
- [Let's Encrypt Documentation](https://letsencrypt.org/docs/)
- [Nginx Documentation](https://nginx.org/en/docs/)
- [Caddy Documentation](https://caddyserver.com/docs/)
- [Traefik Documentation](https://doc.traefik.io/traefik/)
- [SSL Labs SSL Test](https://www.ssllabs.com/ssltest/)
- [Security Headers Scanner](https://securityheaders.com/)
