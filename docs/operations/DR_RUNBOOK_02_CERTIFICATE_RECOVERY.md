# Disaster Recovery Runbook: Certificate Recovery and Reissuance

**Runbook ID**: DR-02
**Last Updated**: 2025-10-18
**Version**: 1.0
**Severity**: P1 (Critical)
**Estimated Time**: 15-60 minutes

## Table of Contents

- [Overview](#overview)
- [Recovery Objectives](#recovery-objectives)
- [Prerequisites](#prerequisites)
- [Recovery Scenarios](#recovery-scenarios)
- [Step-by-Step Procedures](#step-by-step-procedures)
- [Validation](#validation)
- [Emergency Procedures](#emergency-procedures)

---

## Overview

This runbook provides procedures for recovering or reissuing TLS/SSL certificates in disaster scenarios, including expired certificates, lost private keys, certificate revocation, and CA failures.

### When to Use This Runbook

- **Certificate expiration**: Production certificates expired unexpectedly
- **Private key compromise**: Certificate private key leaked or compromised
- **Certificate revocation**: Certificates revoked by CA
- **Lost certificates**: Certificates lost in infrastructure failure
- **CA service failure**: Let's Encrypt or commercial CA unavailable
- **Wildcard certificate issues**: *.domain.com certificate problems

### Impact of Certificate Failure

- **HTTPS services unavailable** (browsers show security warnings)
- **API integrations broken** (clients reject invalid certificates)
- **Mobile apps non-functional** (certificate pinning failures)
- **Revenue loss** (users cannot access services)
- **SEO penalties** (search engines downrank insecure sites)

---

## Recovery Objectives

### Production Environment

| Metric | Target | Maximum |
|--------|--------|---------|
| **RTO** (Recovery Time Objective) | 15 minutes | 1 hour |
| **RPO** (Recovery Point Objective) | N/A (stateless) | N/A |
| **Service Interruption** | < 5 minutes | < 30 minutes |

### Certificate Types and Priorities

| Certificate Type | Priority | RTO | Notes |
|------------------|----------|-----|-------|
| **Production Wildcard** | P0 | 15 min | Affects all services |
| **Production Single Domain** | P1 | 30 min | Affects specific service |
| **API Gateway** | P1 | 30 min | Breaks integrations |
| **Staging** | P2 | 2 hours | Non-critical |
| **Development** | P3 | 24 hours | Can use self-signed |

---

## Prerequisites

### Required Access

- [ ] DNS provider access (Route53/Azure DNS/Cloudflare)
- [ ] Kubernetes cluster access (cert-manager)
- [ ] Key Vault access (certificate storage)
- [ ] Domain registrar access (verification)
- [ ] Load balancer/ingress controller access
- [ ] Certificate Authority account (Let's Encrypt/DigiCert)

### Required Tools

```bash
# Install certbot (Let's Encrypt)
apt-get install certbot

# Install cert-manager CLI (Kubernetes)
kubectl krew install cert-manager

# Install OpenSSL
apt-get install openssl

# Install Azure CLI (if using Azure)
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Install AWS CLI (if using AWS)
pip install awscli
```

### Certificate Inventory

Before disaster, maintain certificate inventory:

```bash
# List all certificates
kubectl get certificates -A

# Check expiration dates
kubectl cert-manager status certificate -n honua --all-namespaces

# Export certificate inventory
kubectl get certificates -A -o json > /backups/cert-inventory-$(date +%Y%m%d).json
```

---

## Recovery Scenarios

### Scenario A: Expired Production Certificate

**Trigger**: Certificate expired, services showing SSL errors

**Impact**: All HTTPS traffic failing

**RTO**: 15 minutes

---

### Scenario B: Compromised Private Key

**Trigger**: Private key exposure detected, must revoke and reissue

**Impact**: Security breach, immediate revocation required

**RTO**: 30 minutes

---

### Scenario C: Let's Encrypt Rate Limit

**Trigger**: Hit Let's Encrypt rate limits (5 certs/domain/week)

**Impact**: Cannot issue new certificates

**RTO**: Use backup CA or cached certificates

---

### Scenario D: Complete Certificate Infrastructure Loss

**Trigger**: Kubernetes cluster deleted, all cert-manager resources lost

**Impact**: All certificate automation destroyed

**RTO**: 60 minutes

---

## Step-by-Step Procedures

### Procedure 1: Emergency Certificate Reissuance (Let's Encrypt)

**When to use**: Production certificate expired or expiring within hours

**Estimated Time**: 15-30 minutes

#### Step 1: Assess Current State

```bash
#!/bin/bash
# DR-02-certificate-recovery.sh

set -euo pipefail

LOG_FILE="/var/log/honua/cert-recovery-$(date +%Y%m%d_%H%M%S).log"
mkdir -p "$(dirname "$LOG_FILE")"

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

log "=== Certificate Recovery Started ==="

# Configuration
DOMAIN="gis.honua.io"
WILDCARD_DOMAIN="*.honua.io"
EMAIL="ops@honua.io"
DNS_PROVIDER="cloudflare"  # or route53, azure-dns
NAMESPACE="honua"

# Check current certificate status
log "Checking certificate status..."

kubectl get certificate -n "$NAMESPACE" | tee -a "$LOG_FILE"

# Check certificate expiration
CERT_NAME=$(kubectl get certificate -n "$NAMESPACE" -o jsonpath='{.items[0].metadata.name}')

EXPIRES=$(kubectl get certificate -n "$NAMESPACE" "$CERT_NAME" -o jsonpath='{.status.notAfter}')
log "Current certificate expires: $EXPIRES"

# Check if expired
EXPIRES_EPOCH=$(date -d "$EXPIRES" +%s 2>/dev/null || echo "0")
NOW_EPOCH=$(date +%s)

if [ "$EXPIRES_EPOCH" -lt "$NOW_EPOCH" ]; then
    log "⚠️ CERTIFICATE EXPIRED! Immediate action required."
    EXPIRED=true
else
    REMAINING_HOURS=$(( ($EXPIRES_EPOCH - $NOW_EPOCH) / 3600 ))
    log "Certificate expires in $REMAINING_HOURS hours"
    EXPIRED=false
fi
```

#### Step 2: Prepare DNS Challenge Provider

```bash
# Set up DNS provider credentials
case "$DNS_PROVIDER" in
    cloudflare)
        log "Configuring Cloudflare DNS provider..."

        # Get API token from Key Vault
        CF_API_TOKEN=$(az keyvault secret show \
            --vault-name "kv-honua-prod" \
            --name "Cloudflare-API-Token" \
            --query "value" \
            --output tsv)

        # Create Kubernetes secret
        kubectl create secret generic cloudflare-api-token \
            --namespace=honua \
            --from-literal=api-token="$CF_API_TOKEN" \
            --dry-run=client -o yaml | kubectl apply -f -

        log "✓ Cloudflare DNS provider configured"
        ;;

    route53)
        log "Configuring Route53 DNS provider..."

        # Create IAM policy for DNS validation
        AWS_ACCESS_KEY=$(az keyvault secret show --vault-name "kv-honua-prod" --name "AWS-AccessKey" -o tsv)
        AWS_SECRET_KEY=$(az keyvault secret show --vault-name "kv-honua-prod" --name "AWS-SecretKey" -o tsv)

        kubectl create secret generic route53-credentials \
            --namespace=honua \
            --from-literal=access-key="$AWS_ACCESS_KEY" \
            --from-literal=secret-key="$AWS_SECRET_KEY" \
            --dry-run=client -o yaml | kubectl apply -f -

        log "✓ Route53 DNS provider configured"
        ;;

    azure-dns)
        log "Configuring Azure DNS provider..."

        # Use managed identity or service principal
        SP_CLIENT_ID=$(az keyvault secret show --vault-name "kv-honua-prod" --name "Azure-SP-ClientID" -o tsv)
        SP_CLIENT_SECRET=$(az keyvault secret show --vault-name "kv-honua-prod" --name "Azure-SP-ClientSecret" -o tsv)
        TENANT_ID=$(az account show --query tenantId -o tsv)

        kubectl create secret generic azure-dns-credentials \
            --namespace=honua \
            --from-literal=client-id="$SP_CLIENT_ID" \
            --from-literal=client-secret="$SP_CLIENT_SECRET" \
            --from-literal=tenant-id="$TENANT_ID" \
            --dry-run=client -o yaml | kubectl apply -f -

        log "✓ Azure DNS provider configured"
        ;;
esac
```

#### Step 3: Deploy Emergency ClusterIssuer

```bash
# Create Let's Encrypt ClusterIssuer with DNS challenge
log "Creating emergency ClusterIssuer..."

cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-emergency
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: $EMAIL
    privateKeySecretRef:
      name: letsencrypt-emergency-key
    solvers:
    - dns01:
        cloudflare:
          email: $EMAIL
          apiTokenSecretRef:
            name: cloudflare-api-token
            key: api-token
      selector:
        dnsZones:
        - honua.io
EOF

log "✓ ClusterIssuer created"
```

#### Step 4: Issue New Certificate

```bash
# Delete existing failed certificate
log "Removing failed certificate..."
kubectl delete certificate -n "$NAMESPACE" "$CERT_NAME" --ignore-not-found

# Create new Certificate resource
log "Requesting new certificate..."

cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: honua-tls-emergency
  namespace: $NAMESPACE
spec:
  secretName: honua-tls
  issuerRef:
    name: letsencrypt-emergency
    kind: ClusterIssuer
  commonName: $DOMAIN
  dnsNames:
  - $DOMAIN
  - $WILDCARD_DOMAIN
  duration: 2160h  # 90 days
  renewBefore: 360h  # 15 days
EOF

log "✓ Certificate requested"
```

#### Step 5: Monitor Certificate Issuance

```bash
# Watch certificate status
log "Monitoring certificate issuance (this may take 2-5 minutes)..."

TIMEOUT=300  # 5 minutes
ELAPSED=0

while [ $ELAPSED -lt $TIMEOUT ]; do
    STATUS=$(kubectl get certificate -n "$NAMESPACE" honua-tls-emergency -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || echo "Unknown")

    if [ "$STATUS" == "True" ]; then
        log "✅ Certificate issued successfully!"
        break
    elif [ "$STATUS" == "False" ]; then
        REASON=$(kubectl get certificate -n "$NAMESPACE" honua-tls-emergency -o jsonpath='{.status.conditions[?(@.type=="Ready")].message}')
        log "Certificate status: Not Ready - $REASON"
    else
        log "Certificate status: Pending..."
    fi

    sleep 10
    ELAPSED=$((ELAPSED + 10))
done

if [ $ELAPSED -ge $TIMEOUT ]; then
    log "❌ ERROR: Certificate issuance timed out after ${TIMEOUT}s"
    log "Checking cert-manager logs for errors..."
    kubectl logs -n cert-manager deployment/cert-manager --tail=50 | tee -a "$LOG_FILE"
    exit 1
fi
```

#### Step 6: Verify Certificate

```bash
# Extract and verify certificate
log "Verifying certificate..."

# Get certificate from secret
kubectl get secret -n "$NAMESPACE" honua-tls -o jsonpath='{.data.tls\.crt}' | base64 -d > /tmp/cert.crt
kubectl get secret -n "$NAMESPACE" honua-tls -o jsonpath='{.data.tls\.key}' | base64 -d > /tmp/cert.key

# Verify certificate details
openssl x509 -in /tmp/cert.crt -noout -text | tee -a "$LOG_FILE"

# Check expiration
EXPIRES=$(openssl x509 -in /tmp/cert.crt -noout -enddate | cut -d= -f2)
log "New certificate expires: $EXPIRES"

# Verify private key matches certificate
CERT_MODULUS=$(openssl x509 -noout -modulus -in /tmp/cert.crt | openssl md5)
KEY_MODULUS=$(openssl rsa -noout -modulus -in /tmp/cert.key | openssl md5)

if [ "$CERT_MODULUS" == "$KEY_MODULUS" ]; then
    log "✓ Certificate and private key match"
else
    log "❌ ERROR: Certificate and private key do NOT match!"
    exit 1
fi

# Cleanup temp files
rm -f /tmp/cert.crt /tmp/cert.key
```

#### Step 7: Update Ingress Controllers

```bash
# Reload ingress to pick up new certificate
log "Updating ingress controllers..."

# For nginx-ingress
kubectl rollout restart deployment -n ingress-nginx ingress-nginx-controller

# For traefik
kubectl rollout restart deployment -n traefik traefik

# Wait for rollout
kubectl wait --for=condition=available deployment -n ingress-nginx ingress-nginx-controller --timeout=120s

log "✓ Ingress controllers updated"
```

#### Step 8: Test HTTPS Endpoints

```bash
# Test certificate with curl
log "Testing HTTPS endpoints..."

# Test main domain
curl -vvI "https://$DOMAIN" 2>&1 | grep -E "subject:|issuer:|expire date:" | tee -a "$LOG_FILE"

# Verify certificate is trusted
if curl -f "https://$DOMAIN/health" > /dev/null 2>&1; then
    log "✅ HTTPS endpoint accessible with valid certificate"
else
    log "❌ WARNING: HTTPS endpoint test failed"
fi

# Check certificate via OpenSSL
echo | openssl s_client -servername "$DOMAIN" -connect "$DOMAIN:443" 2>/dev/null | \
    openssl x509 -noout -dates | tee -a "$LOG_FILE"

log "=== Certificate Recovery Completed ==="
```

---

### Procedure 2: Certificate Recovery from Backup

**When to use**: Kubernetes cluster lost, need to restore certificates

**Steps**:

```bash
# Restore from Key Vault backup
BACKUP_DATE="2025-10-18"

# Download certificate backup
az keyvault secret show \
    --vault-name "kv-honua-prod" \
    --name "TLS-Certificate-Backup-${BACKUP_DATE}" \
    --query "value" \
    --output tsv | base64 -d > /tmp/cert-backup.pfx

# Extract certificate and key
openssl pkcs12 -in /tmp/cert-backup.pfx -nocerts -nodes -out /tmp/cert.key
openssl pkcs12 -in /tmp/cert-backup.pfx -clcerts -nokeys -out /tmp/cert.crt
openssl pkcs12 -in /tmp/cert-backup.pfx -cacerts -nokeys -out /tmp/ca.crt

# Create Kubernetes secret
kubectl create secret tls honua-tls \
    --namespace=honua \
    --cert=/tmp/cert.crt \
    --key=/tmp/cert.key \
    --dry-run=client -o yaml | kubectl apply -f -

# Cleanup
rm -f /tmp/cert-backup.pfx /tmp/cert.key /tmp/cert.crt /tmp/ca.crt

# Verify
kubectl get secret -n honua honua-tls
```

---

### Procedure 3: Emergency Self-Signed Certificate

**When to use**: CA unavailable, need temporary certificate immediately

**Estimated Time**: 5 minutes

```bash
#!/bin/bash
# Generate temporary self-signed certificate

DOMAIN="gis.honua.io"

# Generate private key
openssl genrsa -out /tmp/temp.key 2048

# Generate self-signed certificate (valid 30 days)
openssl req -new -x509 \
    -key /tmp/temp.key \
    -out /tmp/temp.crt \
    -days 30 \
    -subj "/C=US/ST=State/L=City/O=Honua/CN=$DOMAIN" \
    -addext "subjectAltName=DNS:$DOMAIN,DNS:*.$DOMAIN"

# Create Kubernetes secret
kubectl create secret tls honua-tls-temp \
    --namespace=honua \
    --cert=/tmp/temp.crt \
    --key=/tmp/temp.key \
    --dry-run=client -o yaml | kubectl apply -f -

# Update ingress to use temporary certificate
kubectl patch ingress honua -n honua \
    --type merge \
    --patch '{"spec":{"tls":[{"hosts":["'$DOMAIN'"],"secretName":"honua-tls-temp"}]}}'

# Cleanup
rm -f /tmp/temp.key /tmp/temp.crt

echo "⚠️ TEMPORARY SELF-SIGNED CERTIFICATE INSTALLED"
echo "Valid for 30 days. Replace with CA-signed certificate ASAP."
echo "Users will see browser security warnings."
```

---

### Procedure 4: Certificate Revocation and Reissuance

**When to use**: Private key compromised, must revoke immediately

**Steps**:

```bash
# Step 1: Revoke compromised certificate
log "Revoking compromised certificate..."

# Get certificate to revoke
kubectl get secret -n honua honua-tls -o jsonpath='{.data.tls\.crt}' | base64 -d > /tmp/revoke.crt

# Revoke via Let's Encrypt
certbot revoke \
    --cert-path /tmp/revoke.crt \
    --reason keyCompromise \
    --non-interactive

log "✓ Certificate revoked"

# Step 2: Delete old secret immediately
kubectl delete secret -n honua honua-tls

# Step 3: Issue new certificate (use Procedure 1 above)
# This will generate a new private key automatically

# Step 4: Update security monitoring
cat >> /tmp/security-incident.log <<EOF
CERTIFICATE REVOCATION
Date: $(date)
Reason: Private key compromise
Domain: $DOMAIN
Action: Certificate revoked and reissued
New certificate fingerprint: $(openssl x509 -in /tmp/new-cert.crt -noout -fingerprint)
EOF
```

---

## Validation

### Validation Checklist

- [ ] Certificate issued successfully
- [ ] Certificate not expired (valid dates)
- [ ] Certificate matches domain names
- [ ] Private key matches certificate
- [ ] Certificate trusted by browsers
- [ ] HTTPS endpoints accessible
- [ ] No browser security warnings
- [ ] Certificate auto-renewal working

### Validation Tests

```bash
#!/bin/bash
# validate-certificate.sh

DOMAIN="gis.honua.io"

echo "=== Certificate Validation ==="

# Test 1: Certificate exists
if kubectl get secret -n honua honua-tls &>/dev/null; then
    echo "✓ Certificate secret exists"
else
    echo "✗ Certificate secret missing"
    exit 1
fi

# Test 2: Certificate valid
kubectl get secret -n honua honua-tls -o jsonpath='{.data.tls\.crt}' | base64 -d > /tmp/test-cert.crt

EXPIRES=$(openssl x509 -in /tmp/test-cert.crt -noout -enddate | cut -d= -f2)
EXPIRES_EPOCH=$(date -d "$EXPIRES" +%s)
NOW_EPOCH=$(date +%s)
DAYS_UNTIL_EXPIRY=$(( ($EXPIRES_EPOCH - $NOW_EPOCH) / 86400 ))

if [ $DAYS_UNTIL_EXPIRY -gt 7 ]; then
    echo "✓ Certificate valid for $DAYS_UNTIL_EXPIRY days"
else
    echo "⚠️ Certificate expires in $DAYS_UNTIL_EXPIRY days"
fi

# Test 3: Domain name matches
COMMON_NAME=$(openssl x509 -in /tmp/test-cert.crt -noout -subject | sed 's/.*CN=//')

if [ "$COMMON_NAME" == "$DOMAIN" ]; then
    echo "✓ Common name matches: $COMMON_NAME"
else
    echo "✗ Common name mismatch: $COMMON_NAME != $DOMAIN"
fi

# Test 4: HTTPS accessible
if curl -f -m 10 "https://$DOMAIN/health" &>/dev/null; then
    echo "✓ HTTPS endpoint accessible"
else
    echo "✗ HTTPS endpoint failed"
    exit 1
fi

# Test 5: Certificate chain valid
if openssl s_client -connect "$DOMAIN:443" -servername "$DOMAIN" </dev/null 2>/dev/null | \
   openssl x509 -noout -checkend 0; then
    echo "✓ Certificate chain valid"
else
    echo "✗ Certificate chain invalid"
fi

rm -f /tmp/test-cert.crt

echo "=== Validation Complete ==="
```

---

## Emergency Procedures

### Immediate Mitigation (< 5 minutes)

If certificate recovery is taking longer than expected:

1. **Enable HTTP fallback** (temporary)
   ```bash
   kubectl patch ingress honua -n honua \
       --type merge \
       --patch '{"spec":{"rules":[{"http":{"paths":[{"path":"/","pathType":"Prefix","backend":{"service":{"name":"honua-server","port":{"number":80}}}}]}}]}}'
   ```

2. **Use CloudFlare flexible SSL** (if available)
   - Enables HTTPS to users
   - HTTP between CloudFlare and origin
   - Immediate fix while certificate renews

3. **Communicate status**
   ```bash
   # Update status page
   curl -X POST https://status.honua.io/api/incidents \
       -d '{"status":"investigating","message":"SSL certificate renewal in progress"}'
   ```

### Rate Limit Mitigation

If hitting Let's Encrypt rate limits:

1. **Switch to staging environment** (for testing)
   ```yaml
   server: https://acme-staging-v02.api.letsencrypt.org/directory
   ```

2. **Use backup CA**
   - DigiCert (paid)
   - ZeroSSL (free alternative to Let's Encrypt)
   - BuyPass (European alternative)

3. **Use cached certificate** (if available)
   ```bash
   # Restore from recent backup
   kubectl apply -f /backups/certificates/honua-tls-$(date -d yesterday +%Y%m%d).yaml
   ```

---

## Related Documentation

- [DR Database Recovery](./DR_RUNBOOK_01_DATABASE_RECOVERY.md)
- [Azure Key Vault Recovery](../deployment/AZURE_KEY_VAULT_RECOVERY.md)
- [Deployment Guide](../deployment/README.md)
- [Security Architecture](../security/SECURITY_ARCHITECTURE.md)

---

## Emergency Contacts

| Role | Contact | Availability |
|------|---------|--------------|
| **Security Team** | security@honua.io | 24x7 |
| **Platform Lead** | platform@honua.io | 24x7 |
| **On-Call Engineer** | oncall@honua.io | 24x7 |
| **DNS Provider Support** | Cloudflare/Route53 | 24x7 |

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Security & Platform Teams
**Tested**: 2025-10-10 (Staging), 2025-09-15 (Production DR Drill)
