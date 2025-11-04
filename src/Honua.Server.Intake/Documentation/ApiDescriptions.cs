// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Intake.Documentation;

/// <summary>
/// Rich API descriptions and documentation strings.
/// </summary>
public static class ApiDescriptions
{
    #region Intake API Descriptions

    public const string StartConversationSummary = "Start a new AI conversation for build configuration";

    public const string StartConversationDescription = @"
Initiates a new conversation with the Honua AI agent to configure a custom server build.

The AI will guide you through:
- **Protocol Selection**: ESRI REST API, WFS, WMS, WMTS, OGC API Features/Tiles/Maps, STAC, etc.
- **Database Integration**: PostgreSQL, BigQuery, Snowflake, SQL Server, Oracle, etc.
- **Cloud Platform**: AWS, Azure, GCP, or on-premises deployment
- **Architecture**: ARM64 (cost-optimized) or x64 (maximum compatibility)
- **Load Requirements**: Concurrent users, data volume, performance expectations
- **Advanced Features**: Caching, CDN integration, high availability, etc.

**Response includes:**
- Unique conversation ID for subsequent interactions
- Initial AI greeting and questions
- Timestamp for conversation tracking

**Best Practices:**
- Provide detailed, specific answers to get optimal recommendations
- Mention any existing infrastructure or migration requirements
- Ask about cost optimization if budget is a concern
- The AI can explain technical trade-offs upon request
";

    public const string SendMessageSummary = "Send a message to continue the AI conversation";

    public const string SendMessageDescription = @"
Sends your message to the AI agent in an ongoing conversation.

The AI maintains context throughout the conversation and will:
- Ask clarifying questions to understand your needs
- Explain technical concepts when needed
- Provide recommendations based on best practices
- Estimate costs as requirements become clear
- Extract structured requirements when complete

**When `intakeComplete` is `true`:**
- The `requirements` object contains extracted build configuration
- `estimatedMonthlyCost` provides budget planning information
- `costBreakdown` shows cost by component (compute, storage, database, networking)
- You're ready to call the `/api/intake/build` endpoint

**Tips for effective interaction:**
- Be specific about data volumes, user counts, and performance needs
- Mention any compliance requirements (HIPAA, FedRAMP, etc.)
- Ask about cost vs. performance trade-offs
- Request explanations of unfamiliar protocols or features
- The AI can revise recommendations based on your feedback
";

    public const string GetConversationSummary = "Retrieve conversation history";

    public const string GetConversationDescription = @"
Retrieves the complete history of a conversation including:
- All messages exchanged between you and the AI
- Current conversation status (active, completed, abandoned)
- Extracted requirements (if intake is complete)
- Timestamps for audit and debugging

**Use cases:**
- Resume a conversation after disconnection
- Review what was discussed for audit purposes
- Extract requirements for manual review
- Debug issues with AI responses
- Generate reports on customer interactions
";

    public const string TriggerBuildSummary = "Trigger a container build from completed intake";

    public const string TriggerBuildDescription = @"
Initiates the build process for a custom Honua Server container based on conversation requirements.

**Process flow:**
1. Validates the conversation is complete
2. Generates optimized build manifest
3. Provisions container registry access (ECR, ACR, Artifact Registry, or GHCR)
4. Checks build cache for existing matching image
5. Queues build if not cached, or returns cached image immediately
6. Returns build ID for status tracking

**Registry Provisioning:**
The system automatically:
- Creates customer-specific registry namespace
- Generates secure, time-limited credentials
- Configures appropriate IAM/RBAC permissions
- Sets up image lifecycle policies

**Build Caching:**
If a matching build exists (same protocols, databases, architecture, version):
- Instant delivery from cache
- No build time or cost
- Verified image integrity
- Fresh credentials still generated

**Requirements Override:**
You can override AI-extracted requirements by providing custom `requirementsOverride` object.
Useful for:
- Fine-tuning configuration
- A/B testing different architectures
- Manual specification without conversation
";

    public const string GetBuildStatusSummary = "Get real-time build status and progress";

    public const string GetBuildStatusDescription = @"
Retrieves current status and progress information for a build.

**Status Values:**
- `pending`: Build queued, waiting to start
- `building`: Active build in progress
- `completed`: Build finished successfully
- `failed`: Build encountered an error
- `cached`: Delivered from cache (instant)

**Progress Tracking:**
- `progress`: Percentage complete (0-100)
- `currentStage`: Human-readable current step
- `logsUrl`: Link to detailed build logs

**On Completion:**
- `imageReference`: Full pull path for the container image
- `completedAt`: Timestamp of completion
- Registry credentials are available via the original trigger response

**Typical Build Times:**
- Cached builds: < 1 second
- Simple builds: 3-5 minutes
- Complex builds with multiple databases: 10-15 minutes
- Enterprise builds with all features: 15-20 minutes

**Polling Recommendations:**
- Poll every 10-15 seconds during build
- Use exponential backoff if build is long-running
- Subscribe to webhooks for push notifications (see Webhooks API)
";

    #endregion

    #region Build API Descriptions

    public const string ListBuildsSummary = "List all builds for a customer";

    public const string ListBuildsDescription = @"
Returns paginated list of builds for the authenticated customer.

**Filtering:**
- By status (pending, building, completed, failed)
- By date range
- By tag
- By architecture

**Sorting:**
- Creation date (newest first by default)
- Completion date
- Build name
- Status

**Response includes:**
- Build metadata (ID, name, status, timestamps)
- Manifest summary
- Image reference (if completed)
- Cost information
";

    public const string DownloadBuildArtifactsSummary = "Download build artifacts and deployment files";

    public const string DownloadBuildArtifactsDescription = @"
Downloads a zip archive containing:

**Container Image Information:**
- Full image reference and pull command
- Registry credentials (if applicable)
- Image digest and tags

**Deployment Templates:**
- Docker Compose file
- Kubernetes manifests (Deployment, Service, Ingress, ConfigMap)
- Helm chart
- Terraform modules (AWS ECS, Azure Container Instances, GCP Cloud Run)
- CloudFormation template (AWS)
- ARM template (Azure)

**Configuration:**
- Environment variable reference
- Sample configuration files
- Database connection examples
- Reverse proxy configurations (nginx, Caddy, Traefik)

**Documentation:**
- Quick start guide
- API endpoint reference
- Troubleshooting guide
- Performance tuning recommendations
";

    #endregion

    #region License API Descriptions

    public const string GenerateLicenseSummary = "Generate a new license";

    public const string GenerateLicenseDescription = @"
Creates a new license for a customer with specified tier and features.

**License Tiers:**

**Core** ($0/month - Open Source)
- OGC API Features, Tiles, Maps
- WFS 2.0, WMS 1.3.0, WMTS 1.0.0
- GeoJSON, MVT output
- PostgreSQL/PostGIS only
- Community support

**Pro** ($149/month)
- Everything in Core
- ESRI REST API compatibility
- Multiple database connectors
- Raster tile caching
- Email support
- SLA: 99.5%

**Enterprise** ($599/month)
- Everything in Pro
- STAC Catalog
- Advanced caching (Redis/Memcached)
- High availability configurations
- Priority support
- SLA: 99.9%

**Enterprise ASP** ($1,499/month)
- Everything in Enterprise
- Multi-tenancy
- Custom branding
- Dedicated support engineer
- Custom SLA up to 99.99%
- On-premises deployment option

**License Features:**
- Cryptographically signed
- Machine fingerprint binding (optional)
- Feature flags for granular control
- Automatic expiration handling
- Grace period support
";

    public const string ValidateLicenseSummary = "Validate a license key";

    public const string ValidateLicenseDescription = @"
Validates a license key and returns license details.

**Validation checks:**
- Cryptographic signature verification
- Expiration date
- Revocation status
- Feature entitlements
- Usage limits (if applicable)
- Machine fingerprint (if bound)

**Response includes:**
- Validation result (valid/invalid/expired/revoked)
- Licensed features list
- Expiration information
- Usage statistics
- Renewal information
";

    #endregion

    #region Registry API Descriptions

    public const string ProvisionRegistrySummary = "Provision container registry access";

    public const string ProvisionRegistryDescription = @"
Provisions access to a container registry for a customer.

**Supported Registries:**

**AWS Elastic Container Registry (ECR)**
- Regional private registries
- IAM-based authentication
- Automatic lifecycle policies
- Integrated vulnerability scanning
- Cross-region replication

**Azure Container Registry (ACR)**
- Geo-replication
- Azure AD authentication
- Content trust and signing
- Integrated security scanning
- Private endpoint support

**Google Cloud Artifact Registry**
- Multi-region support
- IAM integration
- Vulnerability scanning
- CMEK encryption
- VPC Service Controls

**GitHub Container Registry (GHCR)**
- Public and private images
- GitHub Actions integration
- Package permissions
- Free tier available
- Global CDN distribution

**Provisioning includes:**
- Namespace creation
- Credential generation
- IAM/RBAC configuration
- Lifecycle policies
- Repository encryption
";

    public const string GetRegistryCredentialsSummary = "Get fresh registry credentials";

    public const string GetRegistryCredentialsDescription = @"
Retrieves current registry credentials for a customer.

**ECR/ACR/Artifact Registry:**
- Credentials are temporary (12-hour tokens)
- Automatically rotated
- New token generated on each request

**GHCR:**
- Personal access token (PAT)
- Long-lived (until revoked)
- Scoped to package:read, package:write

**Response includes:**
- Registry URL
- Username
- Password/token
- Expiration time
- Docker login command

**Security:**
- Credentials are encrypted in transit
- Never logged or cached
- Automatically expire
- Can be revoked immediately
";

    #endregion

    #region Admin API Descriptions

    public const string GetBuildQueueSummary = "View build queue status";

    public const string GetBuildQueueDescription = @"
Returns current build queue status and metrics.

**Information provided:**
- Pending builds count
- Active builds (with progress)
- Recent completions
- Failed builds (last 24h)
- Average build time
- Queue depth by priority

**Use cases:**
- Capacity planning
- SLA monitoring
- Customer support
- Performance analysis
";

    public const string GetSystemHealthSummary = "Check system health status";

    public const string GetSystemHealthDescription = @"
Comprehensive system health check.

**Health checks:**
- API responsiveness
- Database connectivity
- Container registry access
- Build worker availability
- Cache system status
- External service dependencies

**Metrics included:**
- Request rate (req/s)
- Error rate (%)
- Average response time
- P95/P99 latencies
- Database query performance
- Cache hit ratio
";

    #endregion

    #region Common Parameter Descriptions

    public const string ConversationIdParam = "Unique identifier for the conversation";
    public const string BuildIdParam = "Unique identifier for the build";
    public const string CustomerIdParam = "Unique identifier for the customer";
    public const string LicenseIdParam = "Unique identifier for the license";

    #endregion

    #region Response Descriptions

    public const string Response200 = "Request processed successfully";
    public const string Response201 = "Resource created successfully";
    public const string Response204 = "Request processed successfully with no content to return";
    public const string Response400 = "Invalid request parameters or body";
    public const string Response401 = "Missing or invalid authentication credentials";
    public const string Response403 = "Insufficient permissions for this operation";
    public const string Response404 = "Requested resource not found";
    public const string Response409 = "Request conflicts with current state (e.g., duplicate resource)";
    public const string Response422 = "Request validation failed";
    public const string Response429 = "Rate limit exceeded - too many requests";
    public const string Response500 = "Internal server error - please contact support";
    public const string Response503 = "Service temporarily unavailable - please retry";

    #endregion
}
