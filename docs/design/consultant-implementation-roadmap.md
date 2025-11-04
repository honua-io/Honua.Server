# Consultant Implementation Roadmap

**Status**: In Progress (Phase 1 of 4 complete)
**Last Updated**: 2025-10-02

## Executive Summary

The Honua Consultant is a Terraform-inspired CLI tool that helps users optimize and manage their geospatial infrastructure. We've completed the core planning/execution architecture and are now ready to build operational components.

**Current Status**: 11/15 foundation tasks complete (73%)

**Important**: See [FOSS Boundary Document](./consultant-foss-boundary.md) for clear separation between open source CLI and AI-powered features. The Consultant is built as an **intelligent wrapper** around FOSS CLI commands, ensuring users can always do manually what the AI does automatically.

---

## Phase 1: Foundation Architecture ✅ COMPLETE

**Goal**: Build the core abstractions and safety model

### Completed Components

- ✅ **Project Structure** - Honua.Cli.AI, Honua.Cli.AI.Secrets, Honua.Cli.AI.Tests
- ✅ **LLM Abstraction** - ILlmProvider with OpenAI and Mock implementations
- ✅ **Safety Model Design** - 900+ line comprehensive design document
- ✅ **Execution Plan Models** - Terraform-style plan/apply workflow
- ✅ **Secrets Management Interface** - Scoped token architecture (interface only)
- ✅ **Semantic Kernel Integration** - Multi-agent orchestration framework
- ✅ **Planning Engine** - SemanticAssistantPlanner with SK plugins
- ✅ **Execution Engine** - PlanExecutor with validation and rollback
- ✅ **Validation System** - 8 comprehensive safety checks
- ✅ **Snapshot Manager** - FileSystemSnapshotManager for config backups

### Foundation Metrics
- **Lines of Code**: ~4,000
- **Projects**: 3
- **Design Docs**: 2 (safety model + this roadmap)
- **Build Status**: ✅ Successful (1 minor warning)
- **Test Coverage**: 0% (MockLlmProvider tests exist but need expansion)

---

## Phase 2: Operational Integration 🔄 NEXT

**Goal**: Wire up the infrastructure to actually work

### 2.0 Interactive Setup Wizard 🆕 HIGH PRIORITY

**Priority**: HIGH (This was in original design!)
**Estimated Effort**: 4-5 days

This is the **killer feature** - natural language setup wizard for complete novices.

**Tasks**:
1. Create conversational setup flow
   - `honua assistant setup` - Interactive wizard
   - Natural language prompts (no technical jargon)
   - Smart defaults based on detected environment
   - Progress indication

2. Environment detection
   - Check for existing Docker
   - Detect available data files (Shapefile, GeoPackage, GeoJSON)
   - Analyze local system (ports, resources)
   - Suggest optimal configuration

3. Automatic PostGIS provisioning
   - Docker container setup (if Docker available)
   - Database creation
   - Extension installation (PostGIS, pg_trgm)
   - Connection string generation

4. Data import wizard
   - File format detection and validation
   - Schema analysis (geometry type, attributes, record count)
   - Import strategy generation
   - Progress tracking with ETA

5. Service configuration
   - Generate metadata.json/yaml
   - Configure OGC API endpoints
   - Set up authentication (optional)
   - Generate working URLs

**Example Flow**:
```bash
$ honua assistant setup

👋 Welcome to Honua! I'll help you set up your first geospatial service.

1. What data source?
   [1] PostGIS (recommended for production)
   [2] SQLite (good for development)
   [3] I already have a database
   Choice: 1

2. Do you have PostGIS ready? [y/N]: n

📦 I'll set up PostGIS in Docker...
   ✓ Docker detected
   ✓ Container started (honua-postgis)
   ✓ Database 'honua' created
   ✓ PostGIS extension installed

3. What data do you have?
   [1] Shapefile [2] GeoPackage [3] GeoJSON [4] CSV with coordinates
   Choice: 2

   Path: ./data/parcels.gpkg

🔍 Analyzing parcels.gpkg...
   ✓ 12,450 Polygon features
   ✓ 15 attributes (id, owner, zoning, ...)
   ✓ CRS: EPSG:4326
   ✓ Est. import time: 8 seconds

📋 Import plan:
   1. Create PostGIS table 'parcels'
   2. Import 12,450 features
   3. Create spatial index (GIST)
   4. Create primary key index
   5. Configure OGC API endpoint

   Proceed? [Y/n]: y

⏳ Importing data...
   [████████████████████] 100% (12,450/12,450) 7.2s

✅ Import complete!

⏳ Creating indexes...
   ✓ Spatial index (idx_parcels_geom)
   ✓ Primary key (parcels_pkey)

⏳ Configuring service...
   ✓ Created metadata.json
   ✓ Configured OGC API

✨ Your service is ready!

🌐 OGC API: http://localhost:5000/ogc/collections/parcels
   • Items: http://localhost:5000/ogc/collections/parcels/items
   • Schema: http://localhost:5000/ogc/collections/parcels/schema
   • Map: http://localhost:5000/ogc/collections/parcels/map

💡 Next steps:
   [1] Add more layers
   [2] Add authentication
   [3] Optimize performance
   [4] Deploy to production

   What would you like to do? [1/2/3/4/exit]: 3

🔍 Analyzing performance...
   ✓ Spatial indexes present
   ✓ Geometry complexity: Medium (avg 145 vertices)
   ✓ Estimated query performance: Good (<200ms)

📊 Performance is already optimal for 12k features!

   For larger datasets (>100k features), I recommend:
   • Multi-resolution geometry storage
   • Response caching
   • Tile pre-generation

   Configure these now? [y/N]: n

✨ You're all set! Start the server:
   honua serve --workspace ./metadata.json
```

**Files to Create**:
- `src/Honua.Cli/Commands/Assistant/SetupCommand.cs`
- `src/Honua.Cli.AI/Services/Setup/SetupOrchestrator.cs`
- `src/Honua.Cli.AI/Services/Setup/EnvironmentDetector.cs`
- `src/Honua.Cli.AI/Services/Setup/DataImportWizard.cs`
- `src/Honua.Cli.AI/Services/Setup/PostGISProvisioner.cs`
- `src/Honua.Cli.AI/Services/Setup/ServiceConfigurator.cs`

**Integration Points**:
- Uses existing IMetadataProvider for configuration
- Uses Semantic Kernel for intelligent decision-making
- Can switch between wizard mode and natural language mode
- Generates Terraform-style plans for review before execution

**Natural Language Alternative**:
```bash
$ honua assistant "I'm new to Honua, help me get started with my GeoPackage file"

👋 I'd be happy to help! Let me guide you through setup...

[Wizard launches automatically with AI-detected preferences]
```

**Cloud Deployment Support**:

The wizard should also handle cloud deployments, not just local Docker:

```bash
$ honua assistant setup --cloud

🌐 Cloud Deployment Setup

1. Where would you like to deploy?
   [1] AWS (Elastic Beanstalk, ECS, or EC2)
   [2] Azure (App Service, Container Instances, or AKS)
   [3] Google Cloud (Cloud Run, GKE, or Compute Engine)
   [4] Local Docker (development)
   Choice: 1

2. AWS Deployment Strategy
   [1] Quick Start (ECS Fargate - easiest, ~$50/mo)
   [2] Production (ECS + RDS + ALB - ~$300/mo)
   [3] Enterprise (EKS + Aurora + CDN - ~$800/mo)
   [4] Custom
   Choice: 2

3. Database Setup
   ⚠️ AWS RDS Aurora (PostgreSQL) recommended for production

   Configuration:
   • Instance: db.t4g.medium (2 vCPU, 4GB RAM)
   • Storage: 50GB SSD (auto-scaling enabled)
   • Backup: 7-day retention
   • Multi-AZ: Yes (high availability)
   • Cost: ~$180/mo

   Proceed? [Y/n]: y

🔍 Checking AWS credentials...
   ✓ AWS CLI configured (Profile: default)
   ✓ Region: us-east-1
   ✓ Permissions: Sufficient for deployment

📋 Deployment Plan (30-45 min):

Phase 1: Network Infrastructure (5 min)
  ✓ Create VPC (honua-prod-vpc)
  ✓ Create subnets (public + private)
  ✓ Configure security groups
  ✓ Set up NAT gateway

Phase 2: Database (15 min)
  ✓ Launch RDS Aurora cluster
  ✓ Enable PostGIS extension
  ✓ Configure parameter group
  ✓ Set up automated backups

Phase 3: Application (15 min)
  ✓ Build Docker image
  ✓ Push to ECR (Elastic Container Registry)
  ✓ Create ECS cluster
  ✓ Deploy Fargate service (2 tasks)

Phase 4: Load Balancing (5 min)
  ✓ Create Application Load Balancer
  ✓ Configure health checks
  ✓ Request TLS certificate (ACM)
  ✓ Set up DNS (Route 53)

Phase 5: Import Data (variable)
  ✓ Upload data to S3
  ✓ Import via secure tunnel
  ✓ Create spatial indexes

Phase 6: Monitoring (5 min)
  ✓ CloudWatch dashboards
  ✓ Log aggregation
  ✓ Alarm configuration

Estimated cost: $280-320/month
  • RDS Aurora: $180/mo
  • ECS Fargate: $70/mo
  • ALB: $25/mo
  • NAT Gateway: $35/mo
  • Data transfer: ~$10/mo

Proceed? [y/N]: y

⏳ Phase 1: Creating VPC...
   ✓ VPC created (vpc-0abc123)
   ✓ Subnets configured (2 public, 2 private)
   ✓ Internet gateway attached
   ✓ Security groups configured

⏳ Phase 2: Launching RDS Aurora...
   [This will take ~15 minutes]
   ⏳ Creating cluster... (8/15 min)

💡 While waiting, let me analyze your data...

🔍 Analyzing parcels.gpkg...
   • 12,450 polygons
   • Average complexity: 145 vertices
   • Recommended optimizations:
     - Geometry simplification for zoom levels 0-10
     - Multi-resolution storage
     - Expected query time: <100ms

   Configure optimizations? [Y/n]: y

⏳ Phase 2: Complete!
   ✓ Aurora cluster ready
   ✓ Writer endpoint: honua-prod.cluster-xyz.us-east-1.rds.amazonaws.com
   ✓ Reader endpoint: honua-prod.cluster-ro-xyz.us-east-1.rds.amazonaws.com

⏳ Phase 3: Building and deploying application...
   ✓ Docker image built
   ✓ Pushed to ECR: 123456789.dkr.ecr.us-east-1.amazonaws.com/honua:latest
   ✓ ECS cluster created
   ✓ Task definition registered
   ✓ Service deployed (2 tasks running)

⏳ Phase 4: Setting up load balancer...
   ✓ ALB created: honua-prod-alb-xyz.us-east-1.elb.amazonaws.com
   ✓ Target group configured
   ✓ TLS certificate requested (ACM)
   ⏳ Waiting for certificate validation... (check email)

   DNS Setup Options:
   [1] Use Route 53 (I'll configure automatically)
   [2] Use my own DNS (show me CNAME)
   [3] Skip for now (use ALB URL)
   Choice: 1

   What domain? gis.yourdomain.com

   ✓ Route 53 record created
   ✓ Certificate validated
   ✓ HTTPS enabled

⏳ Phase 5: Importing data...
   ✓ Data uploaded to S3
   ✓ Creating EC2 bastion (temporary)
   ✓ Importing via secure tunnel...
     [████████████████████] 100% (12,450/12,450) 45s
   ✓ Spatial indexes created
   ✓ Bastion terminated

⏳ Phase 6: Configuring monitoring...
   ✓ CloudWatch dashboard: https://console.aws.amazon.com/cloudwatch/dashboards/honua-prod
   ✓ Log groups configured
   ✓ Alarms set:
     - High CPU (>80%)
     - High memory (>85%)
     - Error rate (>5%)
     - Database connections (>80% max)

✨ Deployment Complete!

🌐 Your service is live:
   https://gis.yourdomain.com/ogc

📊 Monitoring:
   • CloudWatch: https://console.aws.amazon.com/cloudwatch/dashboards/honua-prod
   • Service health: https://gis.yourdomain.com/healthz
   • Metrics: https://gis.yourdomain.com/metrics

💰 Monthly Cost Estimate: $285
   (Actual costs may vary based on usage)

🔐 Credentials stored securely:
   • Database password: AWS Secrets Manager
   • API keys: Parameter Store

📋 Generated Files:
   • infrastructure/terraform/main.tf (for IaC)
   • docker-compose.prod.yml (for local testing)
   • .env.production (encrypted)
   • README-deployment.md

🎯 Next Steps:
   [1] Configure custom domain
   [2] Set up CI/CD pipeline
   [3] Add monitoring alerts
   [4] Scale up (add more tasks)
   [5] Configure backup retention

What would you like to do? [1/2/3/4/5/done]:
```

**Multi-Cloud Support Matrix**:

| Feature | AWS | Azure | Google Cloud | Local Docker |
|---------|-----|-------|--------------|--------------|
| **Compute** | ECS Fargate, EKS | Container Instances, AKS | Cloud Run, GKE | Docker Compose |
| **Database** | RDS Aurora, RDS PostgreSQL | Azure Database for PostgreSQL | Cloud SQL | PostgreSQL Container |
| **Load Balancer** | ALB, NLB | Application Gateway | Cloud Load Balancing | Traefik, Nginx |
| **DNS** | Route 53 | Azure DNS | Cloud DNS | /etc/hosts |
| **Secrets** | Secrets Manager | Key Vault | Secret Manager | Encrypted files |
| **Monitoring** | CloudWatch | Azure Monitor | Cloud Monitoring | Prometheus |
| **Storage** | S3 | Blob Storage | Cloud Storage | Local volumes |
| **CDN** | CloudFront | Azure CDN | Cloud CDN | None |

**Infrastructure as Code Generation**:

The wizard should generate deployment artifacts:
- **Terraform** - Multi-cloud IaC
- **CloudFormation** - AWS native
- **ARM Templates** - Azure native
- **Deployment Manager** - GCP native
- **Pulumi** - Modern IaC (TypeScript/Python/Go)
- **Docker Compose** - Local/simple deployments
- **Kubernetes YAML** - For AKS/EKS/GKE

---

### 2.0.1 TLS/SSL Certificate Management with Let's Encrypt 🔒

**Priority**: HIGH (Security requirement for production)
**Estimated Effort**: 2-3 days

Automatic HTTPS setup with Let's Encrypt integration across all deployment targets.

**Features**:

1. **Automatic Certificate Provisioning**
   - Let's Encrypt ACME v2 protocol
   - DNS-01 challenge (for wildcard certs)
   - HTTP-01 challenge (for standard certs)
   - TLS-ALPN-01 challenge (for restricted networks)
   - Automatic renewal (30 days before expiry)

2. **Multi-Environment Support**
   - **Local Docker**: Traefik with Let's Encrypt
   - **AWS**: ACM (AWS Certificate Manager) + Let's Encrypt fallback
   - **Azure**: App Service Certificates + Let's Encrypt
   - **GCP**: Managed SSL + Let's Encrypt
   - **Self-Hosted**: Certbot + automatic renewal

3. **Certificate Management**
   - Automatic DNS validation setup
   - Certificate storage in secrets manager
   - Renewal monitoring and alerts
   - Certificate revocation support
   - Multiple domain support (SAN certificates)

**Example Flows**:

**A. Local Development with Let's Encrypt**:
```bash
$ honua assistant setup --local --tls

🔒 HTTPS Setup

1. Domain configuration
   Do you have a domain pointing to this server? [y/N]: y

   Domain name: gis.example.com

   🔍 Checking DNS...
   ✓ gis.example.com → 203.0.113.42
   ✓ DNS propagation complete

2. Certificate provider
   [1] Let's Encrypt (free, automatic)
   [2] Self-signed (development only)
   [3] Custom certificate
   Choice: 1

3. Email for Let's Encrypt: admin@example.com
   ⚠️  Used for renewal notifications only

📋 TLS Setup Plan:

  1. Install Traefik reverse proxy
  2. Configure Let's Encrypt ACME
  3. Request certificate for gis.example.com
  4. Configure HTTP → HTTPS redirect
  5. Set up automatic renewal

Proceed? [Y/n]: y

⏳ Installing Traefik...
   ✓ Traefik container started
   ✓ Let's Encrypt configuration applied

⏳ Requesting certificate...
   ✓ HTTP-01 challenge successful
   ✓ Certificate issued (valid 90 days)
   ✓ Certificate stored securely

⏳ Configuring Honua...
   ✓ TLS enabled
   ✓ HTTP → HTTPS redirect configured
   ✓ Security headers applied:
     - Strict-Transport-Security
     - X-Content-Type-Options
     - X-Frame-Options
     - Content-Security-Policy

✨ HTTPS enabled!

🌐 Your service is now secure:
   https://gis.example.com/ogc

🔐 Certificate details:
   • Issuer: Let's Encrypt
   • Valid: 90 days (expires 2025-03-31)
   • Auto-renewal: Enabled (60 days before expiry)
   • Monitoring: Certificate expiry alerts configured

📋 Renewal schedule:
   • Next check: 2025-03-01 (30 days before expiry)
   • Renewal window: 30 days
   • Backup renewal: 7 days before expiry

💡 Certificate will auto-renew. Monitor at:
   honua certs status
```

**B. AWS with ACM Integration**:
```bash
$ honua assistant setup --cloud aws --tier production

[... deployment steps ...]

⏳ Phase 4: TLS Configuration...

   Certificate options:
   [1] AWS Certificate Manager (recommended, free)
   [2] Let's Encrypt (portable to other clouds)
   [3] Custom certificate
   Choice: 1

   Domain: gis.example.com

   🔍 Checking Route 53...
   ✓ Hosted zone found: example.com

   📋 ACM Certificate Plan:
   1. Request certificate from ACM
   2. Add DNS validation record (Route 53)
   3. Wait for validation (~5 minutes)
   4. Attach to Application Load Balancer
   5. Configure CloudFront (optional)

   Include CloudFront CDN? [Y/n]: y

   Additional domains:
   • www.gis.example.com
   • api.example.com

   Include these? [Y/n]: y

⏳ Requesting ACM certificate...
   ✓ Certificate requested (ARN: arn:aws:acm:...)
   ✓ DNS validation record added to Route 53
   ✓ Waiting for validation...
   ✓ Certificate validated (5m 23s)
   ✓ Certificate issued

⏳ Configuring ALB...
   ✓ HTTPS listener added (port 443)
   ✓ Certificate attached
   ✓ HTTP → HTTPS redirect configured
   ✓ Security policy: ELBSecurityPolicy-TLS13-1-2-2021-06

⏳ Configuring CloudFront...
   ✓ Distribution created
   ✓ Origin: ALB (gis.example.com)
   ✓ Certificate attached
   ✓ Custom domain: gis.example.com
   ✓ WAF enabled (basic rules)

✨ HTTPS configured!

🌐 Your service:
   https://gis.example.com/ogc (via CloudFront)

🔐 TLS Configuration:
   • Certificate: AWS Certificate Manager
   • Valid: Lifetime (auto-renewed by AWS)
   • Protocol: TLS 1.3, TLS 1.2
   • Ciphers: Modern secure suite
   • HSTS: Enabled (max-age=31536000)
   • CloudFront: Enabled (edge caching)

📊 SSL Labs grade: A+ (expected)
   Test at: https://www.ssllabs.com/ssltest/analyze.html?d=gis.example.com
```

**C. Self-Hosted with Certbot**:
```bash
$ honua assistant setup --self-hosted --domain gis.example.com

🔒 TLS Setup for Self-Hosted Deployment

   Platform detected: Ubuntu 22.04
   Web server: Nginx (recommended)

📋 Certbot Installation Plan:

   1. Install Certbot + Nginx plugin
   2. Configure Nginx virtual host
   3. Request Let's Encrypt certificate
   4. Set up automatic renewal (systemd timer)
   5. Configure security headers

Proceed? [Y/n]: y

⏳ Installing Certbot...
   ✓ apt-get update
   ✓ certbot installed (v2.7.4)
   ✓ python3-certbot-nginx installed

⏳ Configuring Nginx...
   ✓ Virtual host created: /etc/nginx/sites-available/honua
   ✓ Site enabled
   ✓ Configuration tested
   ✓ Nginx reloaded

⏳ Requesting certificate...
   $ sudo certbot --nginx -d gis.example.com

   ✓ Certificate obtained
   ✓ Nginx configuration updated
   ✓ HTTP → HTTPS redirect enabled

⏳ Setting up auto-renewal...
   ✓ Systemd timer created
   ✓ Timer enabled: certbot-renew.timer
   ✓ Test renewal: SUCCESS

✨ HTTPS configured!

🌐 Service: https://gis.example.com/ogc

🔐 Certificate:
   • Issuer: Let's Encrypt
   • Valid: 90 days (expires 2025-03-31)
   • Auto-renewal: systemd timer (runs daily)
   • Logs: /var/log/letsencrypt/

📋 Manual renewal (if needed):
   sudo certbot renew

💡 Monitor certificate:
   honua certs status
   # or
   sudo certbot certificates
```

**D. Kubernetes with cert-manager**:
```bash
$ honua assistant setup --kubernetes --tls

🔒 TLS Setup for Kubernetes

   Detected: AKS cluster (honua-prod-aks)

   Certificate manager:
   [1] cert-manager (recommended for Kubernetes)
   [2] External Load Balancer (Azure App Gateway)
   Choice: 1

📋 cert-manager Installation Plan:

   1. Install cert-manager (v1.13)
   2. Create ClusterIssuer (Let's Encrypt)
   3. Create Certificate resource
   4. Configure Ingress with TLS
   5. Set up automatic renewal

⏳ Installing cert-manager...
   $ kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

   ✓ namespace/cert-manager created
   ✓ customresourcedefinition.apiextensions.k8s.io/certificates.cert-manager.io created
   ✓ deployment.apps/cert-manager created
   ✓ Waiting for cert-manager pods... READY

⏳ Creating ClusterIssuer...
   ✓ ClusterIssuer/letsencrypt-prod created
   ✓ ACME email: admin@example.com
   ✓ Challenge type: HTTP-01

⏳ Configuring Ingress...
   ✓ Ingress/honua-ingress updated
   ✓ TLS configuration added
   ✓ cert-manager annotations added

⏳ Requesting certificate...
   ✓ Certificate/honua-tls created
   ✓ Waiting for certificate... READY (2m 15s)
   ✓ Secret/honua-tls-secret created

✨ HTTPS configured!

🌐 Service: https://gis.example.com/ogc

🔐 Certificate:
   • Issuer: Let's Encrypt
   • Valid: 90 days
   • Auto-renewal: cert-manager (30 days before expiry)
   • Stored in: Secret/honua-tls-secret

📋 Check certificate status:
   kubectl describe certificate honua-tls
   kubectl get certificate
```

**Certificate Management Commands**:

```bash
# Check all certificates
$ honua certs list

Certificates:
┌─────────────────────┬──────────────┬─────────────┬─────────────┬─────────┐
│ Domain              │ Issuer       │ Expires     │ Days Left   │ Status  │
├─────────────────────┼──────────────┼─────────────┼─────────────┼─────────┤
│ gis.example.com     │ Let's Encrypt│ 2025-03-31  │ 60          │ Valid   │
│ api.example.com     │ Let's Encrypt│ 2025-04-02  │ 62          │ Valid   │
│ *.dev.example.com   │ Let's Encrypt│ 2025-02-15  │ 15          │ ⚠ Soon  │
└─────────────────────┴──────────────┴─────────────┴─────────────┴─────────┘

# Renew certificate manually
$ honua certs renew gis.example.com
⏳ Renewing certificate...
✓ Certificate renewed successfully
✓ New expiry: 2025-06-29

# Test renewal (dry-run)
$ honua certs test-renewal
⏳ Testing renewal process...
✓ All certificates can be renewed

# Revoke certificate
$ honua certs revoke gis.example.com --reason superseded
⚠️  This will revoke the certificate immediately
Proceed? [y/N]: y
✓ Certificate revoked

# Check certificate details
$ honua certs info gis.example.com

Certificate: gis.example.com
  Subject: CN=gis.example.com
  Issuer: Let's Encrypt
  Serial: 04:5f:9a:2b:...

  Validity:
    Not Before: 2024-12-31 00:00:00 UTC
    Not After:  2025-03-31 23:59:59 UTC
    Days Left: 60

  SANs:
    - gis.example.com
    - www.gis.example.com

  Key Type: RSA 2048-bit
  Signature: SHA256-RSA

  Auto-renewal: Enabled
  Next renewal check: 2025-03-01 00:00:00 UTC

  Storage: /etc/letsencrypt/live/gis.example.com/
```

**Security Features**:

1. **TLS Best Practices**
   - TLS 1.2+ only (1.0/1.1 disabled)
   - Modern cipher suites only
   - Perfect Forward Secrecy (PFS)
   - HSTS enabled (max-age=31536000)
   - OCSP stapling

2. **Security Headers** (automatically configured)
   ```
   Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
   X-Content-Type-Options: nosniff
   X-Frame-Options: DENY
   X-XSS-Protection: 1; mode=block
   Content-Security-Policy: default-src 'self'
   Referrer-Policy: strict-origin-when-cross-origin
   ```

3. **Certificate Monitoring**
   - Daily expiry checks
   - Slack/email alerts (30, 14, 7 days)
   - Prometheus metrics export
   - CloudWatch/Azure Monitor integration

4. **Backup & Recovery**
   - Certificate backup to secrets manager
   - Private key rotation support
   - Emergency manual renewal procedures
   - Fallback to self-signed (with warnings)

**Files to Create**:
- `src/Honua.Cli.AI/Services/Certificates/ICertificateManager.cs`
- `src/Honua.Cli.AI/Services/Certificates/LetsEncryptProvider.cs`
- `src/Honua.Cli.AI/Services/Certificates/AcmProvider.cs` (AWS)
- `src/Honua.Cli.AI/Services/Certificates/CertbotProvider.cs` (Self-hosted)
- `src/Honua.Cli.AI/Services/Certificates/CertManagerProvider.cs` (Kubernetes)
- `src/Honua.Cli.AI/Services/Certificates/CertificateMonitor.cs`
- `src/Honua.Cli/Commands/CertsCommand.cs`

**NuGet Packages**:
- `Certes` - ACME v2 client for .NET
- `BouncyCastle` - Certificate generation/manipulation
- `AWSSDK.CertificateManager` - AWS ACM integration
- `Azure.Security.KeyVault.Certificates` - Azure Key Vault

---

### 2.1 CLI Integration

**Priority**: HIGH
**Estimated Effort**: 3-5 days

**Tasks**:
1. Add `AssistantCommand` to Honua.Cli
   - `honua assistant plan <intent>` - Generate execution plan
   - `honua assistant apply` - Execute approved plan
   - `honua assistant rollback` - Undo last change
   - `honua assistant status` - Show active tokens/plans

2. Implement plan rendering for terminal output
   - Colored diff-style output (like Terraform)
   - Risk level indicators
   - Credential requirements display
   - Step-by-step breakdown

3. Add interactive approval workflow
   - Show plan summary
   - Prompt for confirmation
   - Support --auto-approve flag (with warnings)
   - Support --dry-run flag

**Files to Create**:
- `src/Honua.Cli/Commands/AssistantCommand.cs`
- `src/Honua.Cli.AI/Rendering/PlanRenderer.cs`
- `src/Honua.Cli.AI/Rendering/ColorScheme.cs`

**Example Usage**:
```bash
$ honua assistant plan "optimize database performance"
Analyzing workspace...
✓ Found 5 layers, 2.3M features
✓ Detected 3 optimization opportunities

Generated plan: plan-20251002-143022
  + CREATE INDEX idx_parcels_geom_gist ON parcels USING GIST (geometry)
  + CREATE INDEX idx_roads_name_btree ON roads (name)
  ~ UPDATE shared_buffers = '2GB'

Risk: Medium | Reversible: Yes | Credentials: postgres-production (DDL)

Apply this plan? [y/N]
```

---

### 2.2 Secrets Backend Implementation

**Priority**: HIGH
**Estimated Effort**: 2-3 days

**Tasks**:
1. Implement OS Keychain backend (macOS/Windows/Linux)
   - Use `System.Security.Cryptography` for encryption
   - Platform-specific storage (Keychain, Credential Manager, Secret Service)

2. Implement encrypted file backend (development/testing)
   - AES-256 encryption
   - Store in `~/.honua/secrets/`
   - Warn user about security implications

3. Add CLI commands for secrets management
   - `honua secrets set <name>` - Store secret
   - `honua secrets list` - List secret names (not values!)
   - `honua secrets delete <name>` - Remove secret
   - `honua secrets test <name>` - Test connection

4. Implement token management
   - Store active tokens in memory
   - Auto-cleanup on process exit
   - CLI command to list/revoke tokens

**Files to Create**:
- `src/Honua.Cli.AI.Secrets/Backends/OSKeychainSecretsManager.cs`
- `src/Honua.Cli.AI.Secrets/Backends/EncryptedFileSecretsManager.cs`
- `src/Honua.Cli/Commands/SecretsCommand.cs`

**Security Requirements**:
- Never log credential values
- Clear memory after use (SecureString)
- File permissions: 0600 (user read/write only)
- Warn when storing production credentials

---

### 2.3 Enhanced Workspace Plugin

**Priority**: MEDIUM
**Estimated Effort**: 2-3 days

**Tasks**:
1. Integrate with existing IMetadataProvider
   - Replace placeholder in SimpleWorkspacePlugin
   - Use `LoadAsync()` to get workspace metadata
   - Extract layer information (geometry types, record counts)

2. Add database introspection
   - Query information_schema for indexes
   - Check spatial index presence (pg_indexes)
   - Get table statistics (pg_stat_user_tables)
   - Detect missing primary keys

3. Add configuration analysis
   - Parse metadata.json/yaml
   - Check for common misconfigurations
   - Validate connection strings (without exposing them)

**Files to Update**:
- `src/Honua.Cli.AI/Services/Plugins/SimpleWorkspacePlugin.cs` → `WorkspacePlugin.cs`

**Plugin Functions**:
```csharp
[KernelFunction]
public async Task<string> AnalyzeWorkspaceAsync(string workspacePath)
{
    var metadata = await _metadataProvider.LoadAsync(workspacePath);
    // Return JSON with layer info, index status, config issues
}

[KernelFunction]
public async Task<string> GetLayerDetailsAsync(string layerName)
{
    // Return geometry type, record count, extent, indexes
}

[KernelFunction]
public async Task<string> DetectConfigurationIssuesAsync(string workspacePath)
{
    // Return list of configuration problems
}
```

---

### 2.4 Database Execution Integration

**Priority**: MEDIUM
**Estimated Effort**: 3-4 days

**Tasks**:
1. Implement database step executors
   - `CreateIndexExecutor` - Execute CREATE INDEX commands
   - `CreateStatisticsExecutor` - Execute CREATE STATISTICS
   - `UpdateConfigExecutor` - Update PostgreSQL settings
   - `VacuumAnalyzeExecutor` - Run VACUUM ANALYZE

2. Add connection pooling
   - Use existing Honua connection infrastructure
   - Request scoped tokens from ISecretsManager
   - Execute with minimal privileges (DDL only, no DML)

3. Add execution logging
   - Log all SQL commands to audit trail
   - Measure execution time
   - Capture database errors
   - Store output for review

**Files to Create**:
- `src/Honua.Cli.AI/Services/Execution/Executors/CreateIndexExecutor.cs`
- `src/Honua.Cli.AI/Services/Execution/Executors/CreateStatisticsExecutor.cs`
- `src/Honua.Cli.AI/Services/Execution/Executors/UpdateConfigExecutor.cs`
- `src/Honua.Cli.AI/Services/Execution/ExecutorFactory.cs`

**Integration with PlanExecutor**:
```csharp
private async Task<string> ExecuteStepOperationAsync(
    PlanStep step,
    ExecutionPlan plan,
    IExecutionContext context,
    CancellationToken cancellationToken)
{
    var executor = _executorFactory.CreateExecutor(step.Type);
    var result = await executor.ExecuteAsync(step, context, cancellationToken);
    return result.Output;
}
```

---

## Phase 3: Intelligence & Safety 🔮 FUTURE

**Goal**: Make the AI smarter and safer

### 3.1 Enhanced AI Capabilities

**Priority**: MEDIUM
**Estimated Effort**: 4-6 days

**Tasks**:
1. Add more LLM providers
   - Anthropic Claude integration
   - Azure OpenAI support
   - Ollama for local models

2. Enhance PerformancePlugin with real analysis
   - Query slow_query_log
   - Analyze pg_stat_statements
   - Recommend composite indexes
   - Detect missing constraints

3. Create SecurityPlugin
   - Analyze authentication configuration
   - Check for exposed endpoints
   - Recommend rate limiting
   - Detect security misconfigurations

4. Improve prompt engineering
   - Add few-shot examples to system prompt
   - Fine-tune temperature for determinism
   - Add reasoning traces for complex decisions

**Files to Create**:
- `src/Honua.Cli.AI/Services/AI/Providers/AnthropicLlmProvider.cs`
- `src/Honua.Cli.AI/Services/AI/Providers/AzureOpenAILlmProvider.cs`
- `src/Honua.Cli.AI/Services/AI/Providers/OllamaLlmProvider.cs`
- `src/Honua.Cli.AI/Services/Plugins/SecurityPlugin.cs`

---

### 3.2 Advanced Validation

**Priority**: MEDIUM
**Estimated Effort**: 2-3 days

**Tasks**:
1. Add more validation checks
   - Check for lock conflicts (pg_locks)
   - Estimate disk space requirements
   - Validate index names (no duplicates)
   - Check user permissions before execution

2. Add constraint validation
   - Detect steps that would cause downtime
   - Flag operations that block writes
   - Estimate impact on active connections

3. Add cost estimation
   - Estimate index build time based on table size
   - Calculate disk space for indexes
   - Predict query performance improvements

**Files to Update**:
- `src/Honua.Cli.AI/Services/Validation/PlanValidator.cs`

---

### 3.3 Telemetry Service

**Priority**: LOW
**Estimated Effort**: 2-3 days

**Tasks**:
1. Implement opt-in telemetry
   - Track plan generation (no data, just metrics)
   - Track execution success/failure rates
   - Track which plugins are most used
   - Track LLM token usage

2. Add privacy controls
   - Strict opt-in (default: disabled)
   - Hash all identifiable information
   - Never send credentials or data
   - Allow users to inspect telemetry before sending

3. Add telemetry CLI commands
   - `honua telemetry status` - Show opt-in status
   - `honua telemetry enable` - Opt in
   - `honua telemetry disable` - Opt out
   - `honua telemetry view` - Show what would be sent

**Files to Create**:
- `src/Honua.Cli.AI/Services/Telemetry/ITelemetryService.cs`
- `src/Honua.Cli.AI/Services/Telemetry/TelemetryService.cs`

---

## Phase 4: Testing & Documentation 🧪 FUTURE

**Goal**: Production-ready quality

### 4.1 Comprehensive Testing

**Priority**: MEDIUM
**Estimated Effort**: 5-7 days

**Tasks**:
1. Expand unit tests
   - Test all validators (8 validation checks)
   - Test plan parsing edge cases
   - Test credential scoping
   - Test rollback scenarios
   - **Target**: 80% code coverage

2. Add integration tests
   - End-to-end plan generation
   - Real database operations (with Testcontainers)
   - Multi-step execution
   - Failure recovery

3. Add safety tests
   - Ensure dangerous operations are blocked
   - Test token expiration
   - Test rollback on failure
   - Test audit logging

4. Add LLM tests with MockLlmProvider
   - Test plan generation with known responses
   - Test error handling
   - Test token limit handling

**Files to Create**:
- `tests/Honua.Cli.AI.Tests/Services/Validation/PlanValidatorTests.cs`
- `tests/Honua.Cli.AI.Tests/Services/Execution/PlanExecutorTests.cs`
- `tests/Honua.Cli.AI.Tests/Services/Execution/IntegrationTests.cs`
- `tests/Honua.Cli.AI.Tests/Services/Planners/SemanticAssistantPlannerTests.cs`

---

### 4.2 Documentation

**Priority**: MEDIUM
**Estimated Effort**: 3-4 days

**Tasks**:
1. User documentation
   - Getting started guide
   - CLI reference
   - Configuration guide
   - Safety best practices
   - Troubleshooting guide

2. Developer documentation
   - Architecture overview
   - Plugin development guide
   - Adding new executors
   - Contributing guidelines

3. Demo scripts
   - Automated demo: optimize database
   - Automated demo: security hardening
   - Automated demo: rollback scenario
   - Video tutorials

**Files to Create**:
- `docs/user/consultant-getting-started.md`
- `docs/user/consultant-cli-reference.md`
- `docs/user/consultant-configuration.md`
- `docs/dev/consultant-architecture.md`
- `docs/dev/consultant-plugin-development.md`
- `demos/consultant/01-optimize-database.sh`
- `demos/consultant/02-security-hardening.sh`
- `demos/consultant/03-rollback-scenario.sh`

---

## Current Implementation Gaps

### Critical (Blocking)
1. ❌ **No CLI integration** - Can't run `honua assistant` commands
2. ❌ **No secrets backend** - Can't store/retrieve credentials
3. ❌ **No database executors** - Can't actually create indexes

### Important (Limits functionality)
4. ❌ **SimpleWorkspacePlugin is placeholder** - AI can't analyze real workspaces
5. ❌ **No database introspection** - AI can't see current index state
6. ❌ **No actual LLM calls** - Need OpenAI API key configuration
7. ❌ **No tests** - Only MockLlmProviderTests exist (8 tests)

### Nice to Have (Future enhancements)
8. ❌ **Only OpenAI provider** - Missing Anthropic, Azure, Ollama
9. ❌ **No SecurityPlugin** - Can't analyze auth/CORS/rate limiting
10. ❌ **No telemetry** - Can't track usage metrics
11. ❌ **No documentation** - No user guides yet
12. ❌ **No demos** - No automated showcases

---

## Recommended Next Steps (Immediate)

### Week 1: Make it Work (CLI + Secrets)
1. **Day 1-2**: CLI Integration
   - Add `AssistantCommand` to Honua.Cli
   - Implement plan rendering
   - Add approval workflow

2. **Day 3-4**: Secrets Backend
   - Implement EncryptedFileSecretsManager (development)
   - Add `honua secrets` commands
   - Test with real OpenAI API key

3. **Day 5**: Integration Testing
   - Wire everything together
   - Generate first real plan with OpenAI
   - Verify plan can be approved (but not yet executed)

**Milestone**: User can run `honua assistant plan "optimize database"` and see a real AI-generated plan

---

### Week 2: Make it Execute (Database Integration)
1. **Day 1-2**: Workspace Plugin
   - Integrate with IMetadataProvider
   - Add database introspection
   - Test layer analysis

2. **Day 3-4**: Database Executors
   - Implement CreateIndexExecutor
   - Add connection management
   - Add execution logging

3. **Day 5**: End-to-End Test
   - Generate plan for test database
   - Execute plan with approval
   - Verify index was created
   - Test rollback

**Milestone**: User can run `honua assistant apply` and have AI actually create database indexes

---

### Week 3: Make it Safe (Testing + Validation)
1. **Day 1-2**: Unit Tests
   - Test all validators
   - Test execution flow
   - Test error handling

2. **Day 3-4**: Integration Tests
   - Testcontainers setup
   - Multi-step execution tests
   - Rollback tests

3. **Day 5**: Safety Audit
   - Review credential handling
   - Review dangerous operation blocking
   - Review audit logging

**Milestone**: 80% test coverage, all safety checks validated

---

### Week 4: Make it Ready (Documentation + Polish)
1. **Day 1-2**: User Documentation
   - Getting started guide
   - CLI reference
   - Configuration guide

2. **Day 3-4**: Demo Scripts
   - Automated demos
   - Video recordings
   - Example scenarios

3. **Day 5**: Release Preparation
   - Final testing
   - Release notes
   - Update roadmap

**Milestone**: Ready for Phase 1 release / community feedback

---

## Success Metrics

### Phase 2 Complete When:
- ✅ `honua assistant plan` generates real AI plans
- ✅ `honua assistant apply` executes approved plans
- ✅ Database indexes can be created via AI
- ✅ Secrets are stored securely (encrypted file or OS keychain)
- ✅ Rollback works for failed operations
- ✅ Audit logs track all actions

### Phase 3 Complete When:
- ✅ Multiple LLM providers supported
- ✅ SecurityPlugin analyzes auth/CORS
- ✅ Advanced validation catches edge cases
- ✅ Telemetry is opt-in and working

### Phase 4 Complete When:
- ✅ 80%+ test coverage
- ✅ Comprehensive user documentation
- ✅ 3+ automated demo scenarios
- ✅ Production-ready for v1.0 release

---

## Open Questions

1. **LLM Provider Priority**: Which to implement first after OpenAI?
   - Option A: Anthropic Claude (better reasoning)
   - Option B: Ollama (local, no API costs)
   - Option C: Azure OpenAI (enterprise)

2. **Secrets Backend**: Which to prioritize?
   - Option A: OS Keychain (production-ready)
   - Option B: Encrypted File (easier development)
   - Option C: Both in parallel

3. **Execution Safety**: How aggressive should validation be?
   - Option A: Block all dangerous operations (very safe, less useful)
   - Option B: Warn but allow with explicit confirmation (balanced)
   - Option C: Trust AI judgment (risky but flexible)

4. **Testing Strategy**: Testcontainers or mock database?
   - Option A: Testcontainers (real PostgreSQL, slower tests)
   - Option B: In-memory SQLite (fast, less realistic)
   - Option C: Hybrid approach

---

## Dependencies

**External**:
- OpenAI API access (requires key)
- Semantic Kernel 1.65.0 (already integrated)
- Testcontainers 3.10.0+ (for testing)

**Internal**:
- Honua.Server.Core.Metadata (IMetadataProvider)
- Honua.Server.Core.Database (connection infrastructure)
- Honua.Cli (command infrastructure)

**New**:
- System.Security.Cryptography (for secrets encryption)
- Spectre.Console (for rich CLI output - optional)

---

## Timeline Estimates

| Phase | Duration | Dependencies | Status |
|-------|----------|--------------|--------|
| Phase 1: Foundation | 2 weeks | None | ✅ Complete |
| Phase 2: Operational | 3-4 weeks | Phase 1 | 🔄 Next |
| Phase 3: Intelligence | 2-3 weeks | Phase 2 | ⏳ Future |
| Phase 4: Testing & Docs | 2-3 weeks | Phase 3 | ⏳ Future |
| **Total** | **9-12 weeks** | | **~27% Complete** |

**Note**: Timeline assumes one developer working full-time. Parallelization possible for some tasks.

---

## Risk Assessment

### High Risk
- **LLM API costs** - Uncontrolled usage could be expensive
  - Mitigation: Add token limits, warn before expensive operations

- **Security vulnerabilities** - Bugs could expose credentials
  - Mitigation: Comprehensive security audit, penetration testing

### Medium Risk
- **AI hallucinations** - LLM might generate invalid plans
  - Mitigation: Validation layer catches errors, dry-run required

- **Database corruption** - Failed operations could damage data
  - Mitigation: Snapshots, rollback, DDL-only access

### Low Risk
- **User adoption** - Might not trust AI with infrastructure
  - Mitigation: Transparent plan/apply workflow, extensive documentation

---

## Conclusion

We've built a solid foundation (73% of Phase 1 complete). The core architecture is sound and extensible.

**Immediate priority**: CLI integration + secrets management to make the assistant usable.

**Next priority**: Database execution to make it actually do something.

**Final priority**: Testing, documentation, and polish for production readiness.

**Estimated time to MVP**: 3-4 weeks for basic working version, 9-12 weeks for production-ready v1.0.
