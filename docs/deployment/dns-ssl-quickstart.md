# DNS and SSL Quick Deployment Guide

This guide provides strategies for quick deployment with instant DNS and SSL/TLS for Honua, optimized for fast feedback cycles and production readiness.

## Quick DNS Solutions (No Configuration Required)

### 1. nip.io - Magic DNS for IP Addresses

**Use Case**: Instant DNS for any IP without registration

**How it works**:
- `honua.192.168.1.100.nip.io` → `192.168.1.100`
- `api.10.0.0.50.nip.io` → `10.0.0.50`
- Works with any IP (public or private)

**Example Docker Compose**:
```yaml
services:
  caddy:
    image: caddy:2-alpine
    command: caddy reverse-proxy --from honua.${PUBLIC_IP}.nip.io --to honua:5000
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - caddy_data:/data
```

**Pros**: Zero configuration, works immediately
**Cons**: Depends on external service availability

### 2. sslip.io - Alternative Magic DNS

**Use Case**: Backup to nip.io with different syntax

**How it works**:
- `honua-192-168-1-100.sslip.io` → `192.168.1.100`
- `api.10-0-0-50.sslip.io` → `10.0.0.50`

**Pros**: More formats supported (dashes, dots, hex)
**Cons**: Same external dependency as nip.io

### 3. localho.st - Local Development

**Use Case**: Local development with SSL

**How it works**:
- `*.localho.st` → `127.0.0.1`
- Pre-configured SSL certificate for localhost

**Example**:
```bash
curl https://honua.localho.st:5000
```

**Pros**: Works offline, includes SSL
**Cons**: Only for 127.0.0.1

## Automatic SSL/TLS Solutions

### 1. Caddy (Recommended for Quick Start)

**Use Case**: Zero-config HTTPS with automatic Let's Encrypt

**Docker Compose Example**:
```yaml
version: '3.8'

services:
  honua:
    image: honua-server:latest
    environment:
      - ASPNETCORE_URLS=http://+:5000
    expose:
      - "5000"

  caddy:
    image: caddy:2-alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data
      - caddy_config:/config
    depends_on:
      - honua

volumes:
  caddy_data:
  caddy_config:
```

**Caddyfile**:
```
{
    email admin@yourdomain.com
}

honua.yourdomain.com {
    reverse_proxy honua:5000

    # Enable compression
    encode gzip

    # Security headers
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "DENY"
        X-XSS-Protection "1; mode=block"
    }

    # Logging
    log {
        output file /var/log/caddy/access.log
        format json
    }
}
```

**With nip.io for instant SSL**:
```
{
    email admin@example.com
}

honua.{$PUBLIC_IP}.nip.io {
    reverse_proxy honua:5000
}
```

**Startup**:
```bash
PUBLIC_IP=$(curl -s ifconfig.me)
export PUBLIC_IP
docker-compose up -d
```

**Pros**: Automatic certificate renewal, zero config
**Cons**: Requires public domain/IP

### 2. Traefik with Let's Encrypt

**Use Case**: Kubernetes-style labels, automatic discovery

**Docker Compose Example**:
```yaml
version: '3.8'

services:
  traefik:
    image: traefik:v2.10
    command:
      - "--api.insecure=true"
      - "--providers.docker=true"
      - "--providers.docker.exposedbydefault=false"
      - "--entrypoints.web.address=:80"
      - "--entrypoints.websecure.address=:443"
      - "--certificatesresolvers.letsencrypt.acme.tlschallenge=true"
      - "--certificatesresolvers.letsencrypt.acme.email=admin@example.com"
      - "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json"
    ports:
      - "80:80"
      - "443:443"
      - "8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - traefik-certificates:/letsencrypt

  honua:
    image: honua-server:latest
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.honua.rule=Host(`honua.${PUBLIC_IP}.nip.io`)"
      - "traefik.http.routers.honua.entrypoints=websecure"
      - "traefik.http.routers.honua.tls.certresolver=letsencrypt"
      - "traefik.http.services.honua.loadbalancer.server.port=5000"

volumes:
  traefik-certificates:
```

**Pros**: Label-based config, dashboard
**Cons**: More complex than Caddy

### 3. cert-manager (Kubernetes)

**Use Case**: Production Kubernetes deployments

**Installation**:
```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Create Let's Encrypt issuer
kubectl apply -f - <<EOF
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

**Ingress with SSL**:
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: honua-ingress
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  tls:
  - hosts:
    - honua.yourdomain.com
    secretName: honua-tls
  rules:
  - host: honua.yourdomain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: honua
            port:
              number: 80
```

**Pros**: Production-grade, auto-renewal
**Cons**: Kubernetes-only

## Cloud Provider DNS Solutions

### AWS Route 53 + External DNS (Kubernetes)

**Use Case**: Automatic DNS record creation from Kubernetes

**External DNS Deployment**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: external-dns
  namespace: kube-system
spec:
  selector:
    matchLabels:
      app: external-dns
  template:
    metadata:
      labels:
        app: external-dns
    spec:
      serviceAccountName: external-dns
      containers:
      - name: external-dns
        image: registry.k8s.io/external-dns/external-dns:v0.13.5
        args:
        - --source=service
        - --source=ingress
        - --domain-filter=yourdomain.com
        - --provider=aws
        - --policy=upsert-only
        - --aws-zone-type=public
        - --registry=txt
        - --txt-owner-id=honua-cluster
```

**Service with DNS annotation**:
```yaml
apiVersion: v1
kind: Service
metadata:
  name: honua
  annotations:
    external-dns.alpha.kubernetes.io/hostname: honua.yourdomain.com
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
  selector:
    app: honua
```

**Pros**: Fully automated, production-ready
**Cons**: Cloud-specific, requires IAM permissions

### Azure DNS

**Terraform Example**:
```hcl
resource "azurerm_dns_zone" "honua" {
  name                = "yourdomain.com"
  resource_group_name = azurerm_resource_group.honua.name
}

resource "azurerm_dns_a_record" "honua" {
  name                = "honua"
  zone_name           = azurerm_dns_zone.honua.name
  resource_group_name = azurerm_resource_group.honua.name
  ttl                 = 300
  records             = [azurerm_public_ip.honua.ip_address]
}
```

### Cloudflare (Recommended for Production)

**Use Case**: Free SSL, instant propagation, API-driven

**Setup Script**:
```bash
#!/bin/bash
# Add DNS record via Cloudflare API

CLOUDFLARE_API_TOKEN="your-api-token"
ZONE_ID="your-zone-id"
PUBLIC_IP=$(curl -s ifconfig.me)

curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/dns_records" \
  -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{
    "type": "A",
    "name": "honua",
    "content": "'$PUBLIC_IP'",
    "ttl": 120,
    "proxied": true
  }'
```

**Pros**: Free SSL, DDoS protection, instant updates
**Cons**: Requires Cloudflare account

## Quick Start Recommendation Matrix

| Scenario | DNS Solution | SSL Solution | Time to Deploy |
|----------|-------------|--------------|----------------|
| Local Dev | localho.st | Built-in SSL | < 1 minute |
| Quick Demo | nip.io | Caddy | < 2 minutes |
| Staging | sslip.io + Cloud IP | Caddy | < 5 minutes |
| Production (Docker) | Cloudflare | Caddy | < 10 minutes |
| Production (K8s) | External DNS + Route 53 | cert-manager | < 15 minutes |

## Complete Quick Start Example

**1-Command Production Deployment** (using nip.io + Caddy):

```bash
#!/bin/bash
# deploy-honua-instant.sh - One-command Honua deployment with SSL

# Get public IP
PUBLIC_IP=$(curl -s ifconfig.me)
echo "Deploying Honua to honua.$PUBLIC_IP.nip.io"

# Create Caddyfile
cat > Caddyfile <<EOF
{
    email admin@example.com
}

honua.$PUBLIC_IP.nip.io {
    reverse_proxy honua:5000
    encode gzip
}
EOF

# Create docker-compose.yml
cat > docker-compose.yml <<EOF
version: '3.8'
services:
  honua:
    image: honua-server:latest
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - HONUA__AUTHENTICATION__MODE=QuickStart
      - HONUA__AUTHENTICATION__ENFORCE=false
    expose:
      - "5000"

  caddy:
    image: caddy:2-alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data
    depends_on:
      - honua

volumes:
  caddy_data:
EOF

# Deploy
docker-compose up -d

echo "✅ Honua deployed with SSL at https://honua.$PUBLIC_IP.nip.io"
echo "🔒 Certificate will be issued automatically (may take 30-60 seconds)"
```

**Run it**:
```bash
chmod +x deploy-honua-instant.sh
./deploy-honua-instant.sh
```

**Result**: Fully SSL-enabled Honua instance accessible via HTTPS in under 2 minutes.

## Troubleshooting

### Let's Encrypt Rate Limits
- **Problem**: Too many certificate requests
- **Solution**: Use staging environment during testing:
  ```
  --certificatesresolvers.letsencrypt.acme.caserver=https://acme-staging-v02.api.letsencrypt.org/directory
  ```

### DNS Propagation Delays
- **Problem**: DNS changes not visible immediately
- **Solution**: Use low TTL (60-300 seconds) or nip.io for instant resolution

### Certificate Validation Failures
- **Problem**: Let's Encrypt can't reach your server
- **Solution**: Ensure ports 80/443 are open and accessible from internet

## Security Best Practices

1. **Always use HTTPS in production**
2. **Enable HSTS headers** (Strict-Transport-Security)
3. **Use strong TLS versions** (TLS 1.2+)
4. **Implement rate limiting** at reverse proxy level
5. **Monitor certificate expiration** (though Caddy/cert-manager auto-renew)
6. **Use DNS CAA records** to restrict certificate issuers

## Next Steps

- See `docker-examples/` for complete deployment configurations
- See `kubernetes/` for production K8s manifests with SSL
- See `terraform/` for infrastructure-as-code DNS setup
