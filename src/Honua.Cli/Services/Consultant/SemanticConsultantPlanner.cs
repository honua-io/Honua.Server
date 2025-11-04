// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.Services.Metadata;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Globalization;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Semantic Kernel-powered consultant planner that generates execution plans using LLMs.
/// </summary>
public sealed class SemanticConsultantPlanner : IConsultantPlanner
{
    private readonly ILlmProvider _llmProvider;
    private readonly ISystemClock _clock;
    private readonly Kernel _kernel;
    private readonly IDeploymentPatternKnowledgeStore _patternStore;
    private readonly PatternExplainer? _patternExplainer;
    private readonly IPatternUsageTelemetry? _patternTelemetry;
    private readonly ILogger<SemanticConsultantPlanner> _logger;
    private const string PlannerResponseSchema = """
{
  "executiveSummary": string,
  "confidence": "high" | "medium" | "low",
  "reinforcedObservations": [
    { "id": string, "severity": string, "recommendation": string }
  ],
  "plan": [
    {
      "title": string,
      "skill": string,
      "action": string,
      "category": string,
      "rationale": string,
      "successCriteria": string,
      "risk": string,
      "dependencies": [ string ],
      "inputs": { string: string }
    }
  ]
}
""";

    public SemanticConsultantPlanner(
        ILlmProvider llmProvider,
        ISystemClock clock,
        Kernel kernel,
        IDeploymentPatternKnowledgeStore patternStore,
        ILogger<SemanticConsultantPlanner> logger,
        PatternExplainer? patternExplainer = null,
        IPatternUsageTelemetry? patternTelemetry = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _patternStore = patternStore ?? throw new ArgumentNullException(nameof(patternStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patternExplainer = patternExplainer;
        _patternTelemetry = patternTelemetry;
    }

    public async Task<ConsultantPlan> CreatePlanAsync(
        ConsultantPlanningContext planningContext,
        CancellationToken cancellationToken)
    {
        if (planningContext is null)
        {
            throw new ArgumentNullException(nameof(planningContext));
        }

        // Check if this is a refinement request
        var isRefinement = planningContext.Request.PreviousPlan != null;

        // Build context-aware prompt for the LLM
        var systemPrompt = BuildSystemPrompt();
        var deploymentPatterns = await TryGetDeploymentPatternsAsync(planningContext, cancellationToken).ConfigureAwait(false);

        string userPrompt;
        if (isRefinement)
        {
            // Build refinement prompt incorporating previous plan
            userPrompt = await BuildRefinementPromptAsync(
                planningContext,
                planningContext.Request.PreviousPlan!,
                deploymentPatterns,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Build standard prompt
            userPrompt = await BuildUserPromptAsync(planningContext, deploymentPatterns, cancellationToken).ConfigureAwait(false);
        }

        // Call LLM to generate plan
        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.3,  // Lower temperature for more predictable planning
            MaxTokens = 2000
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException($"LLM call failed: {response.ErrorMessage}");
        }

        // Parse LLM response into structured plan
        var plan = ParseLlmResponse(response.Content, planningContext);

        // Track which patterns were recommended in this plan
        var patternIds = deploymentPatterns.Select(p => p.Id).ToList();
        return plan with { RecommendedPatternIds = patternIds };
    }

    private string BuildSystemPrompt()
    {
        return @"You are a world-class geospatial infrastructure consultant, DevSecOps expert, geodesy wizard, and cloud guru - the leading authority on Honua.
You have 20+ years of experience spanning spatial databases, OGC standards, geodetic science, secure cloud operations, and production-scale geospatial systems.

## YOUR EXPERTISE

**Geospatial & Geodesy Mastery:**
- **OGC Standards**: Deep knowledge of OGC API Features, OGC API Tiles, STAC, WFS, WMS, WCS, CSW, GeoJSON, GeoPackage, CityGML
- **Spatial Databases**: Expert in PostGIS optimization, spatial indexing (GiST, SP-GiST, BRIN), query planning, partitioning, replication
- **Geodesy & Projections**: Master of coordinate reference systems, datum transformations (EPSG, PROJ), ellipsoid calculations, geoid models
- **High-Precision Positioning**: Experience with GNSS/GPS processing, RTK corrections, coordinate accuracy analysis, datum shifts
- **Coordinate Transformations**: Expert in WGS84, NAD83, ITRF, local grids, UTM zones, State Plane, custom projections
- **Geospatial Analysis**: Proficient in spatial operations, topology validation, geometry simplification, buffer analysis, overlay operations

**Cloud & Infrastructure Guru:**
- **Multi-Cloud Mastery**: AWS (S3, ECS, Lambda, RDS), Azure (Blob, AKS, Functions), GCP (Cloud Storage, GKE, Cloud Run)
- **Container Orchestration**: Docker, Kubernetes, Helm charts, service meshes (Istio), autoscaling, rolling deployments
- **Infrastructure as Code**: Terraform, CloudFormation, Pulumi, GitOps workflows, immutable infrastructure
- **Serverless Geospatial**: Lambda/Functions for on-demand tile generation, spatial processing pipelines, event-driven architectures
- **CDN & Edge**: CloudFront, Fastly, Akamai for global tile delivery, edge computing for spatial queries
- **Cost Optimization**: Right-sizing, reserved instances, spot instances, data lifecycle policies, tiered storage

**DevSecOps Excellence:**
- **CI/CD Pipelines**: GitHub Actions, GitLab CI, Jenkins, Azure DevOps, automated testing, canary deployments
- **Security Best Practices**: OWASP Top 10, container scanning (Trivy, Snyk), secrets management (Vault, AWS Secrets Manager)
- **Identity & Access**: OAuth2, OIDC, SAML, RBAC, attribute-based access control (ABAC), API gateway security
- **Vulnerability Management**: CVE scanning, dependency audits, security patching, penetration testing
- **Compliance**: GDPR, HIPAA, FedRAMP, SOC2, data sovereignty, audit logging, encryption at rest and in transit
- **Shift-Left Security**: Security as code, policy as code (OPA), automated security testing, threat modeling

**Performance & Scale:**
- **Massive Scale**: Handling billions of features, petabytes of raster data, global tile caches, distributed processing
- **Caching Strategies**: Multi-tier caching (CDN, Redis, database), cache invalidation, tile seeding, precomputation
- **Vector Tiles**: Tippecanoe, Martin, T-Rex, dynamic simplification, MVT optimization, style optimization
- **Database Tuning**: Connection pooling, query optimization, EXPLAIN analysis, vacuum strategies, index maintenance

## YOUR ROLE
You are the user's trusted advisor who:
1. **Anticipates Problems**: Identify potential issues before they occur (performance bottlenecks, data quality, security gaps)
2. **Recommends Best Practices**: Always suggest industry-standard solutions with clear rationale
3. **Optimizes from Day One**: Never deploy inefficient solutions - build for scale and performance from the start
4. **Validates Thoroughly**: Include verification steps to ensure every deployment works correctly
5. **Explains Tradeoffs**: Help users understand pros/cons of different approaches
6. **Accelerates Learning**: Share geospatial expertise so users become more knowledgeable

## PLANNING PRINCIPLES
1. **Safety First**: Always validate inputs, create backups, use dry-run mode for destructive operations, implement rollback strategies
2. **Production-Ready**: Design for production from day one - monitoring, logging, error handling, disaster recovery
3. **Security by Design**: Never use default credentials, implement least privilege, encrypt everything, scan for vulnerabilities
4. **Performance-Aware**: Use spatial indexes, optimize queries, implement caching, consider data volumes and query patterns
5. **Standards-Compliant**: Follow OGC specifications, GeoJSON best practices, proper CRS/datum handling, ISO 19115 metadata
6. **Cloud-Native**: Design for horizontal scaling, stateless services, managed services, multi-region resilience
7. **Observable**: Include health checks, metrics (Prometheus), distributed tracing (Jaeger), structured logging (ELK)
8. **Automated**: CI/CD pipelines, automated testing, infrastructure as code, automated security scanning
9. **Cost-Optimized**: Right-size resources, use spot instances, implement data lifecycle policies, monitor cloud spend
10. **Geodetically Accurate**: Validate CRS transformations, handle datum shifts, account for projection distortion
11. **Testable**: Generate test data, validate deployments, verify endpoints, load testing, chaos engineering
12. **Documented**: Explain technical decisions, provide runbooks, document architecture decisions (ADRs)

## WHEN TO USE SPECIFIC APPROACHES

**Geospatial & Geodesy:**
- **PostGIS vs SpatiaLite**: PostGIS for production/multi-user/replication, SpatiaLite for embedded/mobile/single-file
- **Vector Tiles vs Features**: Vector tiles for visualization at scale, Features API for data access/editing/transactions
- **Spatial Index Types**: GiST for general geometries, SP-GiST for partitioned data, BRIN for append-only time-series
- **CRS Selection**: EPSG:4326 for global web maps, EPSG:3857 for web mercator tiles, local UTM/State Plane for accuracy
- **Datum Transformations**: Use PROJ.db for accuracy, validate transformation parameters, document vertical datums
- **Coordinate Precision**: Store full precision, round only for display, use geography type for Earth surface calculations

**Cloud & Infrastructure:**
- **Cloud Provider**: AWS for mature geospatial services, GCP for BigQuery GIS, Azure for enterprise integration
- **Compute**: Kubernetes for stateful spatial services, Lambda/Functions for event-driven tile generation, ECS/Cloud Run for containers
- **Storage**: S3/Blob for tiles and imagery, managed PostgreSQL/PostGIS for vectors, object storage with CDN for global delivery
- **Caching**: CDN (CloudFront/Fastly) for tiles, Redis/ElastiCache for API responses, database materialized views for precomputation
- **Container Orchestration**: Docker Compose for local dev, Kubernetes + Helm for production, managed services (EKS/GKE/AKS) for operations

**DevSecOps:**
- **CI/CD**: GitHub Actions for OSS, GitLab CI for self-hosted, Azure DevOps for enterprise, multi-stage pipelines with gates
- **Security Scanning**: Trivy for containers, Snyk for dependencies, SAST (SonarQube), DAST for APIs, policy as code (OPA)
- **Secrets Management**: HashiCorp Vault for multi-cloud, AWS Secrets Manager for AWS, never commit secrets to Git
- **Access Control**: OAuth2 + OIDC for users, API keys for services, mTLS for service-to-service, RBAC with principle of least privilege
- **Compliance**: Encrypt at rest (AES-256), encrypt in transit (TLS 1.3), audit logging to immutable storage, GDPR data residency

Available skills and actions organized by category:

SELF-DOCUMENTATION (Use when user asks 'what can you do?' or needs capability guidance):
- SelfDocumentation: ListCapabilities, ExplainCapability, ShowExampleQueries, RecommendCapability

SETUP & CONFIGURATION (Use for initial setup, deployment planning, workspace validation):
- SetupWizard: RecommendSetupPlan, ValidateWorkspaceReadiness, GetConnectionStringTemplate, TroubleshootSetupIssue
- Workspace: AnalyzeWorkspace, GetConfigurationRecommendations, CheckDependencies

DATA INGESTION & MIGRATION (Use for loading data, format conversion, ArcGIS migration):
- DataIngestion: AnalyzeDataFile, SuggestIngestionStrategy, ValidateDataQuality, RecommendSchemaMapping, GenerateIngestionCommand
- Migration: AnalyzeArcGISService, PlanMigration, ValidateMigrationReadiness, GenerateMigrationScript, TroubleshootMigrationIssue

METADATA & STANDARDS (Use for OGC metadata, compliance validation, STAC catalogs):
- Metadata: GenerateCollectionMetadata, ValidateOgcCompliance, SuggestMetadataEnhancements, GenerateSTACCatalog, OptimizeMetadataForDiscovery
- Compliance: ValidateOgcApiFeatures, ValidateOgcApiTiles, CheckStacCompliance, ValidateGeoJSON, AuditSecurityCompliance

PERFORMANCE & OPTIMIZATION (Use for slow queries, caching, scaling, resource planning):
- Performance: AnalyzeDatabasePerformanceAsync, SuggestSpatialOptimizations, RecommendCachingStrategy
- OptimizationEnhancements: AnalyzeQueryPerformance, SuggestIndexStrategy, OptimizeVectorTiles, RecommendScalingStrategy, EstimateResourceNeeds

SPATIAL ANALYSIS (Use for CRS selection, geometry operations, spatial functions):
- SpatialAnalysis: ValidateGeometries, SuggestSpatialOperations, RecommendCRS, AnalyzeSpatialDistribution, GenerateStyleForData

DIAGNOSTICS & TROUBLESHOOTING (Use for errors, debugging, log analysis):
- Diagnostics: DiagnoseServerIssue, AnalyzeLogs, SuggestHealthChecks, TroubleshootOgcEndpoint, GenerateDebugReport

SECURITY & AUTHENTICATION (Use for credential management, access control, security best practices):
- Security: RecommendCredentialStrategy, ValidateCredentialRequirements, SuggestAuthConfiguration, GetProductionSecurityChecklist

TESTING & QA (Use for test data generation, conformance validation, load testing):
- Testing: GenerateTestData, SuggestTestScenarios, ValidateOgcConformance, GenerateLoadTestScript, AnalyzeTestResults

DOCUMENTATION & INTEGRATION (Use for API docs, user guides, third-party tool integration):
- Documentation: GenerateApiDocs, CreateUserGuide, GenerateExampleRequests, DocumentDataModel, CreateDeploymentGuide
- Integration: GenerateQgisConnection, ConfigureArcGisProConnection, SuggestWebMapLibrary, GenerateMapClientCode, IntegrateWithGeoserver

CLOUD DEPLOYMENT (Use for Docker, Kubernetes, cloud platforms, infrastructure as code):
- CloudDeployment: GenerateDockerfile, GenerateKubernetesManifests, SuggestCloudProvider, GenerateTerraformConfig, OptimizeForServerless

MONITORING & OBSERVABILITY (Use for metrics, alerts, logging, performance monitoring):
- Monitoring: SuggestMetrics, GeneratePrometheusConfig, RecommendAlerts, AnalyzePerformanceTrends, SuggestLoggingStrategy

EXECUTION & DEPLOYMENT (Use for actual execution of Docker, files, databases, infrastructure):
- FileSystem: WriteFile(path, content, description), CreateDirectory(path, description), DeleteFile(path, reason)
- Docker: RunDockerContainer(image, containerName, ports, environment), StopDockerContainer(containerName), DockerComposeUp(composePath)
- Database: ExecuteSQL(connection, sql), CreatePostGISDatabase(containerName, databaseName)
- Terraform: GenerateTerraformConfig(provider, resources), TerraformInit(workingDirectory), TerraformPlan(workingDirectory), TerraformApply(workingDirectory)
- Validation: CheckDockerContainer(container), CheckHttpEndpoint(url, timeoutSeconds), CheckFileExists(path), CheckDatabaseConnection(connection, dbType), ValidateJsonStructure(jsonContent, expectedFields)

IMPORTANT: Use exact plugin names (FileSystem, Docker, Database, Terraform, Validation) NOT ""FileSystem Plugin"" or similar variations.

## ADVANCED PLANNING STRATEGIES

**Multi-Step Workflows** - Always think holistically:
1. Setup → Validation → Execution → Verification → Documentation
2. Example: PostGIS deployment = Docker setup + DB creation + spatial extension + test data + index creation + performance validation + monitoring setup

**Proactive Problem Prevention**:
- ALWAYS add spatial indexes (GiST/SP-GiST/BRIN) immediately after data loading (don't wait for performance issues)
- ALWAYS validate CRS consistency and datum transformations before data operations (avoid silently wrong coordinates)
- ALWAYS set resource limits on containers (memory, CPU, disk I/O) to prevent noisy neighbor issues
- ALWAYS use connection pooling (PgBouncer, RDS Proxy) for database access
- ALWAYS implement health checks (liveness, readiness, startup probes) on deployed services
- ALWAYS scan container images for CVE vulnerabilities before deployment (Trivy, Snyk)
- ALWAYS use secrets management (Vault, Secrets Manager) - never hardcode credentials
- ALWAYS validate coordinate precision and handle floating-point rounding appropriately
- ALWAYS generate test data with known ground truth to verify deployments actually work

**Production-Ready Defaults**:
- Use strong passwords (min 16 chars, never 'postgres'/'password'/'admin')
- Enable SSL/TLS for all database connections (verify-full mode)
- Set up centralized log aggregation from day one (ELK, CloudWatch, Splunk)
- Configure automated backups with point-in-time recovery and test restores
- Implement rate limiting and throttling on public APIs (Kong, API Gateway)
- Use read replicas for high-traffic read operations, connection pooling for write scaling
- Enable encryption at rest (AWS KMS, Azure Key Vault, GCP KMS)
- Implement audit logging to immutable storage (S3 Object Lock, Azure Immutable Blob)
- Set up monitoring dashboards with SLOs/SLIs for spatial query latency
- Configure auto-scaling policies based on actual load patterns

**Geodesy & Coordinate System Best Practices**:
- ALWAYS document the CRS/datum used for each dataset (don't assume WGS84)
- Validate transformation accuracy when converting between CRS (use PROJ transformation grids)
- Account for datum shifts when combining datasets from different epochs (NAD27 vs NAD83 vs ITRF)
- Use appropriate CRS for the geographic extent (UTM zones, State Plane for local accuracy)
- Store coordinates with full precision, round only for display (avoid cumulative errors)
- Handle vertical datums explicitly (NAVD88, EGM96, ellipsoid heights)
- Validate coordinate bounds to prevent out-of-range errors (longitude ±180, latitude ±90)

**Performance Optimization Patterns**:
- For >1M features: Partition tables by geography (GeoHash, S2 cells) or time
- For tile serving: Pre-generate tiles (Tippecanoe) or use on-demand with CDN caching (CloudFront)
- For complex geometries: Simplify based on zoom level (ST_Simplify with tolerance), use generalized tables
- For large imports: Use COPY instead of INSERT, disable indexes during load, vacuum after
- For spatial queries: Use bounding box filters (&&) before exact geometry tests (ST_Intersects)
- For global datasets: Use geography type for spherical calculations, avoid planar projections
- For vector tiles: Use MVT (ST_AsMVT) with pre-filtered/simplified geometries

**Cloud Architecture Patterns**:
- Use managed services (RDS/Cloud SQL for PostGIS, S3/Blob for tiles) to reduce operational burden
- Implement multi-region deployments with geo-routing for low latency globally
- Use object storage lifecycle policies to transition cold data to cheaper tiers (S3 Glacier)
- Leverage spot/preemptible instances for batch processing (tile generation, data imports)
- Implement circuit breakers and retries with exponential backoff for resilience
- Use service meshes (Istio, Linkerd) for observability and traffic management

**Common Workflow Patterns**:
1. **Initial Setup**: SetupWizard → Docker → Database → Validation → Testing
2. **Data Migration**: Migration.AnalyzeArcGISService → DataIngestion → SpatialAnalysis.RecommendCRS → Performance.SuggestIndexStrategy → Testing
3. **Performance Issues**: Diagnostics → Performance.AnalyzeDatabasePerformance → OptimizationEnhancements → Monitoring
4. **Production Deployment**: Security → CloudDeployment → Monitoring → Testing → Documentation
5. **Troubleshooting**: Diagnostics.DiagnoseServerIssue → AnalyzeLogs → SuggestHealthChecks → Generate fix → Validate

CRITICAL USAGE NOTES:
1. ALWAYS start by using SelfDocumentation when user asks what you can do
2. For first-time setup, use SetupWizard.RecommendSetupPlan
3. For data loading, ALWAYS analyze the data first, recommend CRS, suggest indexes
4. For performance issues, diagnose BEFORE optimizing (don't guess)
5. For production deployments, ALWAYS include security, monitoring, and backup strategies
6. Combine multiple skills when needed - think like an architect, not just an implementer

        Always produce responses as minified JSON that matches the schema provided in the user instructions.
        The payload must be valid JSON (no comments) and cover safety, validation, performance, security, monitoring, optimization, and documentation considerations where relevant.";
    }

    private async Task<string> BuildUserPromptAsync(
        ConsultantPlanningContext context,
        IReadOnlyList<PatternSearchResult>? patternMatches,
        CancellationToken cancellationToken)
    {
        var request = context.Request;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("### Engagement Brief");
        sb.AppendLine(request.Prompt);
        sb.AppendLine();
        sb.AppendLine($"Mode: {(request.DryRun ? "dry-run (analysis + plan)" : "apply (plan + execute)")}");
        sb.AppendLine($"Workspace: {context.Workspace.RootPath}");
        sb.AppendLine($"Context tags: {string.Join(", ", context.Workspace.Tags)}");
        sb.AppendLine();

        sb.AppendLine("### Workspace Snapshot");
        sb.AppendLine(SummarizeWorkspace(context.Workspace));
        sb.AppendLine();

        if (patternMatches is { Count: > 0 })
        {
            sb.AppendLine("### Relevant Deployment Patterns (knowledge base)");
            var requirements = BuildDeploymentRequirements(context);
            var index = 1;
            foreach (var pattern in patternMatches.Take(3))
            {
                var confidence = pattern.GetConfidence();
                sb.AppendLine($"{index}. {pattern.PatternName} — {confidence.Level} confidence ({confidence.Overall:P0})");
                sb.AppendLine($"   Success: {pattern.SuccessRate:P0} over {pattern.DeploymentCount} deployments, Cloud: {pattern.CloudProvider}");
                sb.AppendLine($"   Similarity: {confidence.VectorSimilarity:P0}, Notes: {TrimContent(pattern.Content)}");

                // Add LLM-powered explanation if explainer is available
                if (_patternExplainer != null)
                {
                    try
                    {
                        var explanation = await _patternExplainer.ExplainPatternAsync(
                            pattern, requirements, confidence, cancellationToken).ConfigureAwait(false);
                        sb.AppendLine($"   Why this matches: {explanation}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate explanation for pattern {PatternId}", pattern.Id);
                    }
                }

                index++;
            }
            sb.AppendLine();
        }

        if (context.Observations.Count > 0)
        {
            sb.AppendLine("### Active Observations (address directly)");
            foreach (var obs in context.Observations.Take(8))
            {
                sb.AppendLine($"- [{obs.Severity}] {obs.Summary}: {obs.Recommendation}");
            }
            if (context.Observations.Count > 8)
            {
                sb.AppendLine($"- ... {context.Observations.Count - 8} additional observations available");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### Planning Directives");
        sb.AppendLine("1. Establish a clear intent statement before drafting steps.");
        sb.AppendLine("2. Resolve or mitigate the observations above as part of the plan.");
        sb.AppendLine("3. Cover setup, security, performance, validation, monitoring, and documentation.");
        sb.AppendLine("4. Include safety guardrails (snapshots, preflight checks) before irreversible operations.");
        sb.AppendLine("5. Use only approved skills/actions; provide precise inputs with workspace-aware paths.");
        sb.AppendLine("6. Never emit secrets in plain text—use placeholders like ${ENV:HONUA_DB_PASSWORD}.");
        sb.AppendLine("7. Limit to at most 10 steps unless additional actions are critical for safety.");
        sb.AppendLine();

        sb.AppendLine("### Response Format");
        sb.AppendLine("Return ONLY valid minified JSON matching this schema:");
        sb.AppendLine(PlannerResponseSchema);
        sb.AppendLine();
        sb.AppendLine("Categories must be one of: safety, discovery, database, deployment, validation, monitoring, documentation, security, optimization.");
        sb.AppendLine("Dependencies list prerequisite step titles when ordering matters.");

        return sb.ToString();
    }

    private async Task<IReadOnlyList<PatternSearchResult>> TryGetDeploymentPatternsAsync(
        ConsultantPlanningContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var requirements = BuildDeploymentRequirements(context);
            var allResults = await _patternStore.SearchPatternsAsync(requirements, cancellationToken).ConfigureAwait(false);

            // Filter patterns by confidence score and version status
            // Exclude: deprecated patterns, low-confidence patterns
            // Prioritize: templates, high-confidence patterns
            var filteredResults = allResults
                .Select(r => new { Pattern = r, Confidence = r.GetConfidence() })
                .Where(x => x.Confidence.Level != "Low")  // Exclude low-confidence patterns
                // Note: We'd filter deprecated patterns here if we had access to full DeploymentPattern
                // For now, PatternSearchResult doesn't include deprecation status
                .OrderByDescending(x => x.Confidence.Overall)
                .Select(x => x.Pattern)
                .ToList();

            // Log confidence stats
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var confidenceStats = allResults
                    .Select(r => r.GetConfidence())
                    .GroupBy(c => c.Level)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToList();

                _logger.LogDebug(
                    "Pattern search confidence distribution: {Stats}. Showing {Shown} of {Total} patterns.",
                    string.Join(", ", confidenceStats),
                    filteredResults.Count,
                    allResults.Count);
            }

            // Track recommendations for telemetry (fire-and-forget)
            // Properly handle and log exceptions to avoid silent failures
            if (_patternTelemetry != null && filteredResults.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var rank = 1;
                        foreach (var pattern in filteredResults.Take(5))
                        {
                            try
                            {
                                var confidence = pattern.GetConfidence();
                                await _patternTelemetry.TrackRecommendationAsync(
                                    pattern.Id,
                                    requirements,
                                    confidence,
                                    rank++,
                                    wasAccepted: false,  // Will be updated later if pattern is used
                                    CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to track pattern recommendation for pattern {PatternId}", pattern.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Top-level catch for critical exceptions - log for visibility
                        _logger.LogError(ex, "Critical error in pattern recommendation tracking");
                    }
                }, CancellationToken.None);
            }

            return filteredResults;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogDebug(ex, "Deployment pattern knowledge store not configured.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve deployment patterns for workspace {Workspace}.", context.Workspace.RootPath);
        }

        return Array.Empty<PatternSearchResult>();
    }

    private DeploymentRequirements BuildDeploymentRequirements(ConsultantPlanningContext context)
    {
        return new DeploymentRequirements
        {
            CloudProvider = DetermineCloudProvider(context),
            DataVolumeGb = EstimateDataVolume(context),
            ConcurrentUsers = EstimateConcurrentUsers(context),
            Region = DetermineRegion(context)
        };
    }

    private static string DetermineCloudProvider(ConsultantPlanningContext context)
    {
        var infraProvider = context.Workspace.Infrastructure.PotentialCloudProviders.FirstOrDefault();
        var normalized = NormalizeProvider(infraProvider);
        if (!normalized.IsNullOrEmpty())
        {
            return normalized;
        }

        foreach (var tag in context.Workspace.Tags)
        {
            normalized = NormalizeProvider(tag);
            if (!normalized.IsNullOrEmpty())
            {
                return normalized;
            }
        }

        return "aws";
    }

    private static int EstimateDataVolume(ConsultantPlanningContext context)
    {
        var dataSources = context.Workspace.Metadata?.DataSources.Count ?? 0;
        if (dataSources > 0)
        {
            var estimate = 50 + (dataSources * 25);
            return Math.Clamp(estimate, 50, 1000);
        }

        return 100;
    }

    private static int EstimateConcurrentUsers(ConsultantPlanningContext context)
    {
        var services = context.Workspace.Metadata?.Services.Count ?? 0;
        if (services > 0)
        {
            var estimate = 40 + (services * 20);
            return Math.Clamp(estimate, 40, 400);
        }

        return 60;
    }

    private static string DetermineRegion(ConsultantPlanningContext context)
    {
        foreach (var tag in context.Workspace.Tags)
        {
            var lower = tag.Trim().ToLowerInvariant();
            if (lower.StartsWith("us-") || lower.StartsWith("eu-") || lower.StartsWith("ap-") || lower.StartsWith("ca-") || lower.StartsWith("sa-"))
            {
                return lower;
            }
        }

        return "us-east-1";
    }

    private static string? NormalizeProvider(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return null;
        }

        var lower = value.Trim().ToLowerInvariant();
        return lower switch
        {
            "aws" or "amazon" or "amazon web services" => "aws",
            "azure" or "microsoft" or "microsoft azure" => "azure",
            "gcp" or "google" or "google cloud" or "google cloud platform" => "gcp",
            _ => null
        };
    }

    private static string TrimContent(string? content)
    {
        if (content.IsNullOrWhiteSpace())
        {
            return "No summary available.";
        }

        var flattened = content.ReplaceLineEndings(" ").Trim();
        return flattened.Length > 180 ? flattened[..180] + "..." : flattened;
    }

    private ConsultantPlan ParseLlmResponse(string llmResponse, ConsultantPlanningContext planningContext)
    {
        var jsonPayload = ExtractJsonPayload(llmResponse);
        if (jsonPayload.IsNullOrWhiteSpace())
        {
            return new ConsultantPlan(ParseLegacyPlanSteps(llmResponse));
        }

        try
        {
            using var document = JsonDocument.Parse(jsonPayload);
            var root = document.RootElement;
            JsonElement planElement;
            string? executiveSummary = null;
            string? confidence = null;
            IReadOnlyList<ConsultantObservation> reinforced = Array.Empty<ConsultantObservation>();

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("plan", out var planProperty))
            {
                planElement = planProperty;
                executiveSummary = root.TryGetProperty("executiveSummary", out var summaryElement) ? summaryElement.GetString() : null;
                confidence = root.TryGetProperty("confidence", out var confidenceElement) ? confidenceElement.GetString() : null;

                if (root.TryGetProperty("reinforcedObservations", out var obsElement) && obsElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<ConsultantObservation>();
                    foreach (var item in obsElement.EnumerateArray())
                    {
                        list.Add(MapObservation(item, planningContext));
                    }
                    reinforced = list;
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                planElement = root;
            }
            else
            {
                return new ConsultantPlan(ParseLegacyPlanSteps(llmResponse));
            }

            var steps = new List<ConsultantPlanStep>();
            foreach (var element in planElement.EnumerateArray())
            {
                if (!element.TryGetProperty("skill", out var skillElement) || skillElement.GetString().IsNullOrWhiteSpace() == true)
                {
                    continue;
                }

                if (!element.TryGetProperty("action", out var actionElement) || actionElement.GetString().IsNullOrWhiteSpace() == true)
                {
                    continue;
                }

                var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (element.TryGetProperty("inputs", out var inputsElement) && inputsElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in inputsElement.EnumerateObject())
                    {
                        inputs[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.Object or JsonValueKind.Array => property.Value.GetRawText(),
                            JsonValueKind.Null => string.Empty,
                            _ => property.Value.ToString()
                        };
                    }
                }

                var dependencies = new List<string>();
                if (element.TryGetProperty("dependencies", out var depElement) && depElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dep in depElement.EnumerateArray())
                    {
                        if (dep.ValueKind == JsonValueKind.String && dep.GetString().HasValue())
                        {
                            dependencies.Add(dep.GetString()!);
                        }
                    }
                }

                steps.Add(new ConsultantPlanStep(
                    skillElement.GetString()!,
                    actionElement.GetString()!,
                    inputs,
                    element.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null,
                    element.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() : null,
                    element.TryGetProperty("rationale", out var rationaleElement) ? rationaleElement.GetString() : null,
                    element.TryGetProperty("successCriteria", out var successElement) ? successElement.GetString() : null,
                    element.TryGetProperty("risk", out var riskElement) ? riskElement.GetString() : null,
                    dependencies));
            }

            if (steps.Count == 0)
            {
                return new ConsultantPlan(ParseLegacyPlanSteps(llmResponse));
            }

            return new ConsultantPlan(steps, executiveSummary, confidence, reinforced);
        }
        catch (JsonException)
        {
            return new ConsultantPlan(ParseLegacyPlanSteps(llmResponse));
        }
    }

    private static string ExtractJsonPayload(string content)
    {
        if (content.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        if (content.Contains("```json", StringComparison.OrdinalIgnoreCase))
        {
            var start = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase) + 7;
            var end = content.IndexOf("```", start, StringComparison.OrdinalIgnoreCase);
            if (end > start)
            {
                return content.Substring(start, end - start).Trim();
            }
        }
        else if (content.Contains("```", StringComparison.Ordinal))
        {
            var start = content.IndexOf("```", StringComparison.Ordinal) + 3;
            var end = content.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
            {
                return content.Substring(start, end - start).Trim();
            }
        }

        return content.Trim();
    }

    private static IReadOnlyList<ConsultantPlanStep> ParseLegacyPlanSteps(string llmResponse)
    {
        try
        {
            var legacySteps = JsonSerializer.Deserialize<List<LlmPlanStep>>(ExtractJsonPayload(llmResponse), JsonSerializerOptionsRegistry.DevTooling);
            if (legacySteps is null)
            {
                return Array.Empty<ConsultantPlanStep>();
            }

            return legacySteps.Select(step => new ConsultantPlanStep(
                step.Skill ?? "UnknownSkill",
                step.Action ?? "UnknownAction",
                step.Inputs?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ValueKind == JsonValueKind.Object || kvp.Value.ValueKind == JsonValueKind.Array ? kvp.Value.GetRawText() : kvp.Value.ToString())
                    ?? new Dictionary<string, string>(),
                step.Rationale ?? "No rationale provided"))
                .ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<ConsultantPlanStep>();
        }
    }

    private static ConsultantObservation MapObservation(JsonElement element, ConsultantPlanningContext context)
    {
        // Handle case where LLM returns a string instead of an object
        if (element.ValueKind == JsonValueKind.String)
        {
            var textValue = element.GetString() ?? string.Empty;
            return new ConsultantObservation(
                $"adhoc-{Guid.NewGuid():N}",
                "medium",
                textValue,
                string.Empty,
                "Follow up manually");
        }

        var id = element.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var match = id.HasValue()
            ? context.Observations.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase))
            : null;

        var severity = element.TryGetProperty("severity", out var severityElement)
            ? severityElement.GetString()
            : match?.Severity ?? "medium";

        var summary = element.TryGetProperty("summary", out var summaryElement)
            ? summaryElement.GetString()
            : match?.Summary ?? "Observation";

        var detail = element.TryGetProperty("detail", out var detailElement)
            ? detailElement.GetString()
            : match?.Detail ?? string.Empty;

        var recommendation = element.TryGetProperty("recommendation", out var recommendationElement)
            ? recommendationElement.GetString()
            : match?.Recommendation ?? "Document next steps";

        return new ConsultantObservation(
            id ?? match?.Id ?? $"adhoc-{Guid.NewGuid():N}",
            severity ?? match?.Severity ?? "medium",
            summary ?? match?.Summary ?? "Observation",
            detail ?? match?.Detail ?? string.Empty,
            recommendation ?? match?.Recommendation ?? "Follow up manually");
    }

    private string SummarizeWorkspace(WorkspaceProfile workspace)
    {
        var summary = new System.Text.StringBuilder();

        if (workspace.Metadata is { } metadata)
        {
            summary.AppendLine($"Services: {metadata.Services.Count} (" + string.Join(", ", metadata.Services.Select(s => s.Id)) + ")");
            summary.AppendLine($"Data sources: {metadata.DataSources.Count} (missing credentials: {metadata.DataSources.Count(ds => !ds.HasConnectionString)})");
            if (metadata.RasterDatasets.Count > 0)
            {
                summary.AppendLine($"Raster datasets: {metadata.RasterDatasets.Count}");
            }
        }
        else
        {
            summary.AppendLine("No metadata file detected");
        }

        var infra = workspace.Infrastructure;
        var artifacts = new List<string>();
        if (infra.HasDockerCompose) artifacts.Add("docker-compose");
        if (infra.HasKubernetesManifests) artifacts.Add("kubernetes");
        if (infra.HasHelmCharts) artifacts.Add("helm");
        if (infra.HasTerraform) artifacts.Add("terraform");

        summary.AppendLine("Deployment artifacts: " + (artifacts.Count == 0 ? "none" : string.Join(", ", artifacts)));
        summary.AppendLine("Monitoring configured: " + (infra.HasMonitoringConfig ? "yes" : "no"));

        if (infra.PotentialCloudProviders.Count > 0)
        {
            summary.AppendLine("Cloud indicators: " + string.Join(", ", infra.PotentialCloudProviders));
        }

        return summary.ToString().Trim();
    }

    /// <summary>
    /// Builds a refinement prompt that incorporates the previous plan and user's adjustment request.
    /// </summary>
    private Task<string> BuildRefinementPromptAsync(
        ConsultantPlanningContext context,
        ConsultantPlan previousPlan,
        IReadOnlyList<PatternSearchResult> deploymentPatterns,
        CancellationToken cancellationToken)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# Plan Refinement Request");
        sb.AppendLine();
        sb.AppendLine("## Previous Plan");
        sb.AppendLine();
        sb.AppendLine("The user previously received this plan:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(new
        {
            executiveSummary = previousPlan.ExecutiveSummary,
            confidence = previousPlan.Confidence,
            steps = previousPlan.Steps.Select(s => new
            {
                description = s.Description,
                action = s.Action,
                category = s.Category,
                rationale = s.Rationale
            }).ToList()
        }, JsonSerializerOptionsRegistry.WebIndented));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Refinement Request");
        sb.AppendLine();
        sb.AppendLine($"The user now wants to refine the plan with this adjustment:");
        sb.AppendLine($"\"{context.Request.Prompt}\"");
        sb.AppendLine();

        // Include conversation history if available
        if (context.Request.ConversationHistory?.Count > 0)
        {
            sb.AppendLine("## Conversation History");
            sb.AppendLine();
            foreach (var message in context.Request.ConversationHistory)
            {
                sb.AppendLine($"- {message}");
            }
            sb.AppendLine();
        }

        // Include workspace snapshot (abbreviated)
        sb.AppendLine("## Current Workspace State");
        sb.AppendLine();
        sb.AppendLine($"- **Workspace**: `{context.Workspace.RootPath}`");
        sb.AppendLine($"- **Mode**: {(context.Request.DryRun ? "Planning (dry-run)" : "Execution")}");

        if (context.Observations?.Count > 0)
        {
            sb.AppendLine($"- **Issues Detected**: {context.Observations.Count} observations");
            foreach (var obs in context.Observations.Take(3))
            {
                sb.AppendLine($"  - {obs.Severity}: {obs.Summary}");
            }
        }
        sb.AppendLine();

        // Include deployment patterns if available
        if (deploymentPatterns.Count > 0)
        {
            sb.AppendLine("## Recommended Deployment Patterns");
            sb.AppendLine();
            for (int i = 0; i < Math.Min(3, deploymentPatterns.Count); i++)
            {
                var pattern = deploymentPatterns[i];
                var confidence = pattern.GetConfidence();

                sb.AppendLine($"{i + 1}. **{pattern.PatternName}** — {confidence.Level} confidence ({confidence.Overall:P0})");
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("Generate an **improved plan** that:");
        sb.AppendLine($"1. **Addresses the refinement request**: {context.Request.Prompt}");
        sb.AppendLine("2. **Preserves successful elements** from the previous plan where applicable");
        sb.AppendLine("3. **Adds, modifies, or removes steps** to fulfill the user's adjustment");
        sb.AppendLine("4. **Maintains overall plan coherence** and dependency order");
        sb.AppendLine("5. **Updates the executive summary** to reflect the refinement");
        sb.AppendLine();
        sb.AppendLine("Return your response in this JSON schema:");
        sb.AppendLine("```json");
        sb.AppendLine(PlannerResponseSchema);
        sb.AppendLine("```");

        return Task.FromResult(sb.ToString());
    }

    // Internal DTO for parsing legacy LLM responses
    private sealed class LlmPlanStep
    {
        public string? Skill { get; set; }
        public string? Action { get; set; }
        public Dictionary<string, JsonElement>? Inputs { get; set; }
        public string? Rationale { get; set; }
    }
}
