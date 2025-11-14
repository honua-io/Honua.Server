# DNS and SSL/TLS Setup Guide for Cloud Marketplace Deployments

This guide explains how to configure custom domains and HTTPS for your Honua IO cloud marketplace deployment.

## Overview

All cloud marketplace deployments of Honua IO support:

✅ **Automatic HTTPS** - SSL/TLS certificates are automatically provisioned and renewed
✅ **Custom Domains** - Use your own domain name (e.g., `honua.example.com`)
✅ **Free Certificates** - Let's Encrypt (AWS/GCP) or Azure Managed Certificates (Azure)
✅ **Zero Configuration** - Default deployments work out of the box with cloud-provided URLs

---

## Deployment Options

### Option 1: Default Configuration (No Custom Domain)

**Best for**: Quick setup, testing, proof-of-concept

Your Honua IO deployment will be accessible via a cloud-provided URL:

| Platform | Default URL Format |
|----------|-------------------|
| **AWS EKS** | `https://<load-balancer-id>.<region>.elb.amazonaws.com` |
| **AWS ECS Fargate** | `https://<load-balancer-id>.<region>.elb.amazonaws.com` |
| **Azure Container Apps** | `https://<app-name>.azurecontainerapps.io` |
| **GCP Cloud Run** | `https://<service-name>-<hash>-<region>.run.app` |

**Setup**: None required! HTTPS is enabled by default.

---

### Option 2: Custom Domain + Cloud-Managed DNS

**Best for**: Production deployments, new domains, simplest option

The cloud provider manages both DNS and SSL certificates automatically.

#### AWS (EKS or ECS Fargate)

**During Deployment:**
```yaml
Parameters:
  DomainName: honua.example.com
  CreateRoute53HostedZone: "yes"
  # Leave CertificateArn empty for automatic certificate creation
```

**After Deployment:**

1. Get Route 53 nameservers from CloudFormation outputs:
   ```bash
   aws cloudformation describe-stacks \
     --stack-name honua-server \
     --query 'Stacks[0].Outputs[?OutputKey==`Route53Nameservers`].OutputValue' \
     --output text
   ```

2. Update your domain registrar:
   - Go to your domain registrar (GoDaddy, Namecheap, etc.)
   - Update nameservers to the Route 53 values from step 1
   - Wait for DNS propagation (5 minutes to 48 hours)

3. SSL certificate is automatically validated and issued via DNS

4. Access Honua IO at `https://honua.example.com`

#### Azure (Container Apps)

**During Deployment:**
```json
{
  "customDomainName": "honua.example.com",
  "createDnsZone": "yes"
}
```

**After Deployment:**

1. Get DNS zone nameservers from Azure Portal or CLI:
   ```bash
   az network dns zone show \
     --name honua.example.com \
     --resource-group honua-rg \
     --query nameServers
   ```

2. Update your domain registrar with Azure DNS nameservers

3. Follow the `customDomainInstructions` output to bind custom domain:
   ```bash
   # Get validation token
   az containerapp hostname list \
     --name honua-server \
     --resource-group honua-rg

   # Add DNS records and bind domain
   az containerapp hostname bind \
     --hostname honua.example.com \
     --name honua-server \
     --resource-group honua-rg \
     --validation-method CNAME
   ```

4. Azure automatically provisions a **FREE managed TLS certificate** (no Let's Encrypt needed!)

#### GCP (Cloud Run)

**During Deployment:**
```yaml
domainName: honua.example.com
createCloudDnsZone: yes
```

**After Deployment:**

1. Get Cloud DNS nameservers:
   ```bash
   gcloud dns managed-zones describe honua-zone \
     --format="value(nameServers)"
   ```

2. Update your domain registrar with Cloud DNS nameservers

3. Cloud Run automatically provisions SSL certificate

---

### Option 3: Custom Domain + Bring Your Own DNS

**Best for**: Existing DNS infrastructure, complex DNS setups, multiple subdomains

You manage DNS yourself; the cloud provider handles SSL certificates.

#### AWS (EKS or ECS Fargate)

**Prerequisites:**
- Existing Route 53 hosted zone OR external DNS provider (Cloudflare, etc.)

**During Deployment:**
```yaml
Parameters:
  DomainName: honua.example.com
  CreateRoute53HostedZone: "no"
  Route53HostedZoneId: Z1234567890ABC  # If using Route 53
  # OR leave empty if using external DNS
```

**After Deployment:**

If using Route 53:
- ACM certificate is automatically validated via DNS
- Route 53 A record is automatically created

If using external DNS (Cloudflare, etc.):
1. Get Load Balancer DNS from CloudFormation outputs
2. Create CNAME record:
   ```
   Name: honua.example.com
   Type: CNAME
   Value: <LoadBalancerDNS from output>
   ```
3. Create TXT record for ACM validation (check ACM console for validation records)

#### Azure (Container Apps)

**During Deployment:**
```json
{
  "customDomainName": "honua.example.com",
  "createDnsZone": "no",
  "existingDnsZoneResourceGroup": "my-dns-rg"
}
```

**After Deployment:**

1. Get Container App FQDN from outputs

2. Add CNAME record in your DNS provider:
   ```
   Name: honua.example.com
   Type: CNAME
   Value: <containerAppFQDN from output>
   ```

3. Get validation token and add TXT record:
   ```bash
   az containerapp hostname list --name honua-server --resource-group honua-rg
   ```

   ```
   Name: asuid.honua.example.com
   Type: TXT
   Value: <validation-token>
   ```

4. Bind custom domain:
   ```bash
   az containerapp hostname bind \
     --hostname honua.example.com \
     --name honua-server \
     --resource-group honua-rg \
     --validation-method CNAME
   ```

---

### Option 4: Bring Your Own SSL Certificate

**Best for**: Wildcard certificates, extended validation (EV) certificates, compliance requirements

#### AWS (EKS or ECS Fargate)

**Prerequisites:**
- SSL certificate imported into AWS Certificate Manager (ACM)

**Import Certificate:**
```bash
aws acm import-certificate \
  --certificate fileb://certificate.crt \
  --private-key fileb://private.key \
  --certificate-chain fileb://ca-bundle.crt \
  --region us-east-1
```

**During Deployment:**
```yaml
Parameters:
  DomainName: honua.example.com
  CertificateArn: arn:aws:acm:us-east-1:123456789012:certificate/abc123...
```

#### Azure (Container Apps)

Azure Container Apps currently only supports managed certificates. For bring-your-own-certificate scenarios, consider Azure Application Gateway or Azure Front Door in front of Container Apps.

---

## Automated Setup Scripts

### AWS EKS - Complete DNS/SSL Setup

Run the automated setup script after CloudFormation deployment:

```bash
cd deploy/marketplace/aws/scripts
./setup-dns-ssl.sh
```

This script will:
1. ✅ Install cert-manager (Let's Encrypt automation)
2. ✅ Install external-dns (automatic DNS record management)
3. ✅ Create ClusterIssuers for Let's Encrypt
4. ✅ Configure HTTPS ingress
5. ✅ Provision and validate SSL certificates
6. ✅ Create DNS A records pointing to load balancer

**Prerequisites:**
- kubectl configured with EKS cluster access
- helm installed
- jq installed

---

## SSL/TLS Certificate Details

### Let's Encrypt (AWS EKS, GCP)

- **Issuer**: Let's Encrypt
- **Validation**: DNS-01 challenge (automatic)
- **Renewal**: Automatic (60 days before expiry)
- **Certificate Type**: Domain Validated (DV)
- **Cost**: FREE
- **Wildcard Support**: Yes (*.example.com)
- **Rate Limits**: 50 certificates per week per domain

### AWS Certificate Manager (AWS ECS Fargate)

- **Issuer**: Amazon Trust Services
- **Validation**: DNS (automatic via Route 53)
- **Renewal**: Automatic (AWS handles everything)
- **Certificate Type**: Domain Validated (DV)
- **Cost**: FREE
- **Wildcard Support**: Yes (*.example.com)
- **Rate Limits**: None

### Azure Managed Certificates (Azure Container Apps)

- **Issuer**: DigiCert (via Azure)
- **Validation**: TXT record validation
- **Renewal**: Automatic (Azure handles everything)
- **Certificate Type**: Domain Validated (DV)
- **Cost**: FREE
- **Wildcard Support**: No (single domain or SAN)
- **Rate Limits**: None

---

## Troubleshooting

### Issue: ACM Certificate Stuck in "Pending Validation"

**Cause**: DNS validation records not created or DNS not propagated

**Solution**:
```bash
# 1. Check validation records required
aws acm describe-certificate \
  --certificate-arn <cert-arn> \
  --query 'Certificate.DomainValidationOptions[*].ResourceRecord'

# 2. Verify DNS propagation
dig TXT _<validation-hash>.honua.example.com

# 3. If using Route 53, records should be auto-created
# If using external DNS, manually add the CNAME record
```

### Issue: Let's Encrypt Challenge Fails in Kubernetes

**Cause**: cert-manager cannot update Route 53 (IAM permissions issue)

**Solution**:
```bash
# 1. Verify cert-manager has IRSA role configured
kubectl describe sa cert-manager -n cert-manager | grep Annotations

# 2. Check IAM role has Route 53 permissions
aws iam get-role-policy \
  --role-name <cert-manager-role> \
  --policy-name CertManagerRoute53Access

# 3. Check cert-manager logs
kubectl logs -n cert-manager -l app=cert-manager

# 4. Check certificate status
kubectl describe certificate honua-tls -n honua-system
```

### Issue: Azure Custom Domain Binding Fails

**Cause**: TXT validation record not found or CNAME record missing

**Solution**:
```bash
# 1. Verify CNAME record exists
nslookup honua.example.com

# 2. Verify TXT record exists
nslookup -type=TXT asuid.honua.example.com

# 3. Check Container App hostname status
az containerapp hostname list \
  --name honua-server \
  --resource-group honua-rg

# 4. View detailed errors
az containerapp show \
  --name honua-server \
  --resource-group honua-rg \
  --query properties.customDomainVerificationFailureInfo
```

### Issue: DNS Propagation Takes Too Long

**Cause**: DNS caching, TTL not expired, incorrect nameservers

**Solution**:
```bash
# 1. Check current nameservers for your domain
dig NS example.com

# 2. Check if new nameservers are propagated globally
# Use https://www.whatsmydns.net

# 3. Reduce TTL before migration (optional, for next time)
# Set TTL to 300 seconds (5 minutes) 24 hours before changing nameservers

# 4. Flush local DNS cache
# macOS:
sudo dnsflush; sudo killall -HUP mDNSResponder

# Linux:
sudo systemd-resolve --flush-caches

# Windows:
ipconfig /flushdns
```

---

## Best Practices

### 1. Use Cloud-Managed DNS for Production

- **Why**: Automatic integration with SSL certificates
- **Why**: Better performance (GeoDNS, health checks)
- **Why**: No manual DNS record management

### 2. Enable Automatic Renewal

All cloud providers handle certificate renewal automatically:
- **Let's Encrypt**: cert-manager renews 30 days before expiry
- **ACM**: AWS renews automatically
- **Azure Managed**: Azure renews automatically

No action required! Monitor via:
```bash
# AWS - ACM
aws acm list-certificates

# Kubernetes - cert-manager
kubectl get certificate -A

# Azure - Container Apps
az containerapp hostname list --name honua-server --resource-group honua-rg
```

### 3. Use Wildcard Certificates for Subdomains

If you plan to have multiple subdomains (e.g., `api.example.com`, `admin.example.com`):

**AWS/Kubernetes (Let's Encrypt)**:
```yaml
Certificate:
  dnsNames:
  - example.com
  - "*.example.com"  # Wildcard covers all subdomains
```

**AWS (ACM)**:
```yaml
SubjectAlternativeNames:
  - example.com
  - "*.example.com"
```

**Azure Container Apps**:
Azure managed certificates don't support wildcards. Options:
1. Add each subdomain separately
2. Use Azure Application Gateway with wildcard certificate

### 4. Monitor Certificate Expiration

Set up alerts before certificates expire (backup plan if auto-renewal fails):

**AWS (CloudWatch)**:
```bash
# ACM automatically publishes DaysToExpiry metric
# Create CloudWatch alarm for DaysToExpiry < 30
```

**Kubernetes (Prometheus + cert-manager)**:
```yaml
# cert-manager exports certmanager_certificate_expiration_timestamp_seconds metric
# Alert when < 30 days
```

**Azure (Azure Monitor)**:
```bash
# Azure Container Apps managed certificates send alerts automatically
# Check Action Groups for certificate renewal failures
```

### 5. Test with Staging Certificates First

**Let's Encrypt Staging** (avoid rate limits during testing):
```yaml
ClusterIssuer:
  name: letsencrypt-staging
  server: https://acme-staging-v02.api.letsencrypt.org/directory
```

Staging certificates are not trusted by browsers but verify your setup works.

Switch to production once confirmed:
```yaml
ClusterIssuer:
  name: letsencrypt-prod
  server: https://acme-v02.api.letsencrypt.org/directory
```

---

## Security Considerations

### 1. Always Use HTTPS

All Honua IO cloud marketplace deployments **enforce HTTPS**:
- AWS ECS Fargate: HTTP automatically redirects to HTTPS
- AWS EKS: Ingress configured for HTTPS only
- Azure Container Apps: `allowInsecure: false` (HTTPS only)
- GCP Cloud Run: HTTPS by default

### 2. TLS Version Policy

Minimum TLS version enforced:

| Platform | Minimum TLS Version | Cipher Suites |
|----------|---------------------|---------------|
| **AWS EKS** | TLS 1.2 | Modern (ECDHE, AES-GCM) |
| **AWS ECS Fargate** | TLS 1.2 | ELBSecurityPolicy-TLS13-1-2-2021-06 |
| **Azure Container Apps** | TLS 1.2 | Azure default (PFS enabled) |
| **GCP Cloud Run** | TLS 1.2 | Google default (BoringSSL) |

### 3. Certificate Validation

All deployments validate:
✅ Certificate not expired
✅ Certificate matches domain
✅ Certificate chain is trusted
✅ Certificate not revoked (OCSP)

---

## Support

For DNS/SSL issues:

1. **Check deployment outputs** for configuration instructions
2. **Review logs**:
   - AWS EKS: `kubectl logs -n cert-manager -l app=cert-manager`
   - AWS ECS: CloudWatch Logs
   - Azure: `az containerapp logs show --name honua-server --resource-group honua-rg`
3. **Verify DNS records**: Use `dig`, `nslookup`, or online DNS checkers
4. **Contact support**: support@honua.io with deployment details and error messages

---

## Additional Resources

- [Let's Encrypt Documentation](https://letsencrypt.org/docs/)
- [AWS Certificate Manager](https://docs.aws.amazon.com/acm/)
- [cert-manager Documentation](https://cert-manager.io/docs/)
- [Azure Container Apps Custom Domains](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates)
- [external-dns Documentation](https://github.com/kubernetes-sigs/external-dns)
