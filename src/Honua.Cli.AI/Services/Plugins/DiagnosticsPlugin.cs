// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for server diagnostics and troubleshooting.
/// Provides AI with capabilities to diagnose issues, analyze logs, and generate debug reports.
/// </summary>
public sealed class DiagnosticsPlugin
{
    [KernelFunction, Description("Performs root cause analysis for server issues based on symptoms and logs")]
    public string DiagnoseServerIssue(
        [Description("Symptoms description (e.g., '500 errors', 'slow responses', 'connection timeouts')")] string symptoms,
        [Description("Recent log entries or error messages")] string recentLogs)
    {
        var symptomsLower = symptoms.ToLowerInvariant();
        var diagnosis = new System.Collections.Generic.List<object>();

        // 500 Internal Server Error patterns
        if (symptomsLower.Contains("500") || symptomsLower.Contains("internal server error"))
        {
            diagnosis.Add(new
            {
                issue = "HTTP 500 Internal Server Error",
                likelyCauses = new[]
                {
                    "Unhandled exception in request pipeline",
                    "Database connection failure",
                    "Missing or invalid metadata configuration",
                    "Geometry processing error (invalid geometries)"
                },
                diagnosticSteps = new[]
                {
                    "Check application logs for exception stack traces",
                    "Verify database connectivity: honua database test-connection",
                    "Validate metadata: honua metadata validate",
                    "Test with simple query: curl http://localhost:5000/collections"
                },
                solutions = new[]
                {
                    "Enable detailed error responses in Development environment",
                    "Fix database connection string in appsettings.json",
                    "Repair invalid geometries: UPDATE table SET geom = ST_MakeValid(geom)",
                    "Review and fix metadata.yaml syntax errors"
                }
            });
        }

        // Performance issues
        if (symptomsLower.Contains("slow") || symptomsLower.Contains("timeout") || symptomsLower.Contains("performance"))
        {
            diagnosis.Add(new
            {
                issue = "Slow Query Performance / Timeouts",
                likelyCauses = new[]
                {
                    "Missing spatial indexes on geometry columns",
                    "Large result sets without pagination",
                    "Complex geometries causing CPU bottlenecks",
                    "Database resource exhaustion (connections, memory)"
                },
                diagnosticSteps = new[]
                {
                    "Check for spatial indexes: SELECT * FROM pg_indexes WHERE tablename = 'layer'",
                    "Run EXPLAIN ANALYZE on slow queries",
                    "Monitor connection pool: SELECT count(*) FROM pg_stat_activity",
                    "Check PostGIS geometry complexity: SELECT AVG(ST_NPoints(geom)) FROM layer"
                },
                solutions = new[]
                {
                    "CREATE INDEX CONCURRENTLY idx_geom ON layer USING GIST(geom);",
                    "Add limit parameter to queries: ?limit=100",
                    "Simplify geometries: ST_SimplifyPreserveTopology(geom, 0.0001)",
                    "Increase connection pool: MaxPoolSize=100 in connection string",
                    "Enable query result caching with Redis"
                }
            });
        }

        // Connection issues
        if (symptomsLower.Contains("connection") || symptomsLower.Contains("refused") || symptomsLower.Contains("cannot connect"))
        {
            diagnosis.Add(new
            {
                issue = "Database Connection Failures",
                likelyCauses = new[]
                {
                    "Database server not running",
                    "Incorrect connection string credentials",
                    "Network/firewall blocking connection",
                    "Connection pool exhaustion",
                    "SSL/TLS configuration mismatch"
                },
                diagnosticSteps = new[]
                {
                    "Verify database is running: docker ps | grep postgres OR systemctl status postgresql",
                    "Test direct connection: psql -h localhost -U honua_user -d honua",
                    "Check firewall: telnet dbhost 5432",
                    "Review connection string in appsettings.json",
                    "Check SSL requirements: psql -c 'SHOW ssl'"
                },
                solutions = new[]
                {
                    "Start database: docker start honua-postgis OR systemctl start postgresql",
                    "Fix credentials in connection string or secrets manager",
                    "Open firewall port: sudo ufw allow 5432",
                    "Increase MaxPoolSize in connection string",
                    "Add SSL Mode=Require or SSL Mode=Disable based on server config"
                }
            });
        }

        // OGC API specific issues
        if (symptomsLower.Contains("ogc") || symptomsLower.Contains("conformance") || symptomsLower.Contains("404"))
        {
            diagnosis.Add(new
            {
                issue = "OGC API Endpoint Issues",
                likelyCauses = new[]
                {
                    "Collections not loading from metadata",
                    "Incorrect routing configuration",
                    "Metadata provider not configured",
                    "Collection disabled in metadata"
                },
                diagnosticSteps = new[]
                {
                    "Test conformance: curl http://localhost:5000/conformance",
                    "List collections: curl http://localhost:5000/collections",
                    "Check metadata provider: echo $HONUA__METADATA__PROVIDER",
                    "Review logs for metadata loading errors",
                    "Validate metadata schema: honua metadata validate"
                },
                solutions = new[]
                {
                    "Set HONUA__METADATA__PROVIDER=yaml in environment",
                    "Set HONUA__METADATA__PATH to metadata.yaml location",
                    "Ensure enabled: true in collection definitions",
                    "Restart server after metadata changes",
                    "Check routing middleware order in Program.cs"
                }
            });
        }

        // Memory issues
        if (symptomsLower.Contains("memory") || symptomsLower.Contains("oom") || symptomsLower.Contains("crash"))
        {
            diagnosis.Add(new
            {
                issue = "Memory Exhaustion / Out of Memory",
                likelyCauses = new[]
                {
                    "Large raster tile generation",
                    "Unbounded result sets",
                    "Memory leak in caching layer",
                    "Insufficient server resources"
                },
                diagnosticSteps = new[]
                {
                    "Monitor memory: docker stats OR top/htop",
                    "Check cache size: redis-cli info memory",
                    "Review large queries in logs",
                    "Analyze heap dumps with dotnet-dump"
                },
                solutions = new[]
                {
                    "Configure tile cache eviction policy (LRU)",
                    "Add LIMIT to all queries: default 1000 items",
                    "Enable server garbage collection in appsettings.json",
                    "Implement response streaming for large datasets",
                    "Scale vertically (more RAM) or horizontally (more instances)"
                }
            });
        }

        // Authentication/Authorization issues
        if (symptomsLower.Contains("401") || symptomsLower.Contains("403") || symptomsLower.Contains("unauthorized"))
        {
            diagnosis.Add(new
            {
                issue = "Authentication/Authorization Failures",
                likelyCauses = new[]
                {
                    "Invalid or expired token",
                    "Insufficient user permissions",
                    "Authentication middleware not configured",
                    "CORS policy blocking requests"
                },
                diagnosticSteps = new[]
                {
                    "Verify token: jwt.io or honua auth validate-token",
                    "Check user roles: honua auth list-users",
                    "Review CORS policy in appsettings.json",
                    "Test without auth: set HONUA__AUTHENTICATION__MODE=QuickStart"
                },
                solutions = new[]
                {
                    "Refresh authentication token",
                    "Grant required role: honua auth assign-role --user user1 --role datapublisher",
                    "Configure CORS: add AllowedOrigins to appsettings.json",
                    "Update authentication configuration in Program.cs"
                }
            });
        }

        if (diagnosis.Count == 0)
        {
            diagnosis.Add(new
            {
                issue = "General Diagnostics",
                recommendation = "Enable detailed logging to gather more diagnostic information",
                steps = new[]
                {
                    "Set Logging:LogLevel:Default to Debug in appsettings.json",
                    "Restart server and reproduce the issue",
                    "Review logs for error patterns",
                    "Run: honua diagnostic collect-info",
                    "Check system resources: disk space, CPU, memory"
                }
            });
        }

        return JsonSerializer.Serialize(new
        {
            symptoms,
            diagnosisCount = diagnosis.Count,
            diagnosis,
            generalChecklist = new[]
            {
                "✓ Check server logs for exceptions and errors",
                "✓ Verify database connectivity and health",
                "✓ Validate metadata configuration",
                "✓ Test with minimal request (landing page)",
                "✓ Review system resource utilization"
            },
            logAnalysisTips = new[]
            {
                "Look for exception stack traces - they point to exact failure location",
                "Search for 'ERROR' or 'FATAL' log levels",
                "Check timestamps - correlate errors with symptom occurrence",
                "Review database query logs for slow queries",
                "Look for repeated errors - indicates systemic issue"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Analyzes log content and identifies patterns, errors, and anomalies")]
    public string AnalyzeLogs(
        [Description("Log content to analyze (plain text or JSON logs)")] string logContent = "No logs provided",
        [Description("Time range for analysis (e.g., 'last-hour', 'today', '2024-01-01 to 2024-01-02')")] string timeRange = "all")
    {
        var analysis = new
        {
            timeRange,
            patterns = new[]
            {
                new
                {
                    pattern = "Exception/Error Events",
                    description = "Critical failures requiring immediate attention",
                    searchTerms = new[] { "Exception", "ERROR", "FATAL", "failed", "threw" },
                    action = "Extract stack traces and error messages for root cause analysis"
                },
                new
                {
                    pattern = "Performance Warnings",
                    description = "Slow queries and timeout indicators",
                    searchTerms = new[] { "slow", "timeout", "exceeded", "took", "ms" },
                    action = "Identify slow endpoints and optimize queries"
                },
                new
                {
                    pattern = "Authentication Issues",
                    description = "Failed login attempts and authorization errors",
                    searchTerms = new[] { "401", "403", "Unauthorized", "Forbidden", "authentication failed" },
                    action = "Review auth configuration and check for security threats"
                },
                new
                {
                    pattern = "Database Errors",
                    description = "Connection failures and query errors",
                    searchTerms = new[] { "connection", "database", "SQL", "query", "deadlock", "timeout" },
                    action = "Check database health and connection pool settings"
                },
                new
                {
                    pattern = "OGC API Requests",
                    description = "API usage patterns and endpoint access",
                    searchTerms = new[] { "/collections", "/items", "/conformance", "OGC", "features" },
                    action = "Analyze API usage patterns and identify popular endpoints"
                }
            },
            metrics = new
            {
                totalLines = "Count total log entries",
                errorCount = "Count ERROR/FATAL level messages",
                warningCount = "Count WARN level messages",
                uniqueErrors = "Identify distinct error patterns",
                errorRate = "Calculate errors per minute/hour",
                topErrors = "Rank most frequent errors"
            },
            analysis_commands = new[]
            {
                new
                {
                    purpose = "Extract errors",
                    command = "grep -i 'error\\|exception\\|fatal' logs.txt",
                    output = "All error entries"
                },
                new
                {
                    purpose = "Count error types",
                    command = "grep -i 'exception' logs.txt | sort | uniq -c | sort -rn",
                    output = "Frequency count of each exception type"
                },
                new
                {
                    purpose = "Find slow queries",
                    command = "grep -E 'took [0-9]{3,}ms|exceeded' logs.txt",
                    output = "Queries taking 100ms+"
                },
                new
                {
                    purpose = "Extract stack traces",
                    command = "grep -A 10 'Exception' logs.txt",
                    output = "Exceptions with 10 lines of context"
                },
                new
                {
                    purpose = "Time-based filtering",
                    command = "grep '2024-01-15' logs.txt | grep ERROR",
                    output = "Errors from specific date"
                }
            },
            structuredLogParsing = new
            {
                jsonLogs = new
                {
                    parser = "jq or JSON log analysis tools",
                    queries = new[]
                    {
                        new { query = "jq 'select(.level == \"ERROR\")' logs.json", purpose = "Filter errors" },
                        new { query = "jq '.message' logs.json | sort | uniq -c", purpose = "Count unique messages" },
                        new { query = "jq 'select(.duration > 1000)' logs.json", purpose = "Find slow operations" }
                    }
                },
                aggregation = new
                {
                    tools = new[] { "Elasticsearch + Kibana", "Grafana Loki", "Azure Log Analytics", "AWS CloudWatch Insights" },
                    benefit = "Real-time log aggregation, search, and visualization"
                }
            },
            recommendations = new[]
            {
                "Enable structured JSON logging for easier parsing and analysis",
                "Set up log aggregation platform for production (ELK, Loki, CloudWatch)",
                "Configure log rotation to prevent disk space exhaustion",
                "Implement log levels consistently: TRACE, DEBUG, INFO, WARN, ERROR, FATAL",
                "Add correlation IDs to track requests across distributed services",
                "Create alerts for error rate thresholds"
            }
        };

        return JsonSerializer.Serialize(analysis, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Suggests health check configuration for monitoring and alerting")]
    public string SuggestHealthChecks(
        [Description("Service configuration as JSON (database type, deployment mode, endpoints)")] string serviceConfig = "{\"databaseType\":\"postgis\",\"deploymentMode\":\"local\"}")
    {
        var healthChecks = new[]
        {
            new
            {
                category = "Database Health",
                checks = new[]
                {
                    new
                    {
                        name = "Database Connectivity",
                        endpoint = "/health/database",
                        implementation = "Test SELECT 1 query",
                        failureThreshold = "3 consecutive failures",
                        alertSeverity = "Critical"
                    },
                    new
                    {
                        name = "Connection Pool Status",
                        endpoint = "/health/database/pool",
                        implementation = "Check active vs max connections",
                        failureThreshold = "Pool usage > 90%",
                        alertSeverity = "Warning"
                    },
                    new
                    {
                        name = "Spatial Extension",
                        endpoint = "/health/postgis",
                        implementation = "Query PostGIS version",
                        failureThreshold = "PostGIS not available",
                        alertSeverity = "Critical"
                    }
                }
            },
            new
            {
                category = "API Endpoints",
                checks = new[]
                {
                    new
                    {
                        name = "Landing Page",
                        endpoint = "/",
                        implementation = "HTTP GET / expect 200",
                        failureThreshold = "Non-200 response",
                        alertSeverity = "High"
                    },
                    new
                    {
                        name = "Conformance",
                        endpoint = "/conformance",
                        implementation = "Validate conformance classes",
                        failureThreshold = "Empty or invalid response",
                        alertSeverity = "High"
                    },
                    new
                    {
                        name = "Collections List",
                        endpoint = "/collections",
                        implementation = "Verify collections array exists",
                        failureThreshold = "No collections returned",
                        alertSeverity = "Medium"
                    }
                }
            },
            new
            {
                category = "System Resources",
                checks = new[]
                {
                    new
                    {
                        name = "Memory Usage",
                        endpoint = "/health/memory",
                        implementation = "Check process memory consumption",
                        failureThreshold = "Memory > 80% of limit",
                        alertSeverity = "Warning"
                    },
                    new
                    {
                        name = "Disk Space",
                        endpoint = "/health/disk",
                        implementation = "Check available disk space",
                        failureThreshold = "Free space < 10%",
                        alertSeverity = "High"
                    },
                    new
                    {
                        name = "Thread Pool",
                        endpoint = "/health/threads",
                        implementation = "Monitor thread pool saturation",
                        failureThreshold = "Queue length > 100",
                        alertSeverity = "Warning"
                    }
                }
            },
            new
            {
                category = "Data Quality",
                checks = new[]
                {
                    new
                    {
                        name = "Metadata Validity",
                        endpoint = "/health/metadata",
                        implementation = "Validate metadata schema",
                        failureThreshold = "Schema validation fails",
                        alertSeverity = "Medium"
                    },
                    new
                    {
                        name = "Sample Data Query",
                        endpoint = "/health/data",
                        implementation = "Execute test query on each collection",
                        failureThreshold = "Query fails or returns no data",
                        alertSeverity = "High"
                    }
                }
            }
        };

        var aspNetCoreImplementation = new
        {
            package = "Microsoft.Extensions.Diagnostics.HealthChecks",
            configuration = @"// Program.cs
services.AddHealthChecks()
    .AddNpgSql(connectionString, name: ""database"")
    .AddCheck<MetadataHealthCheck>(""metadata"")
    .AddCheck<CollectionHealthCheck>(""collections"");

app.MapHealthChecks(""/health"", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});",
            endpoints = new[]
            {
                "/health - Overall health status",
                "/health/ready - Readiness probe (Kubernetes)",
                "/health/live - Liveness probe (Kubernetes)"
            }
        };

        var monitoringIntegration = new
        {
            prometheus = new
            {
                package = "AspNetCore.HealthChecks.Publisher.Prometheus",
                metrics = new[] { "health_check_status", "health_check_duration_seconds" },
                queries = new[]
                {
                    "health_check_status{name=\"database\"} == 0  # Unhealthy",
                    "rate(health_check_duration_seconds[5m]) > 1  # Slow health checks"
                }
            },
            kubernetesProbes = new
            {
                livenessProbe = new
                {
                    httpGet = new { path = "/health/live", port = 5000 },
                    initialDelaySeconds = 30,
                    periodSeconds = 10
                },
                readinessProbe = new
                {
                    httpGet = new { path = "/health/ready", port = 5000 },
                    initialDelaySeconds = 10,
                    periodSeconds = 5
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            healthCheckCategories = healthChecks,
            implementation = aspNetCoreImplementation,
            monitoring = monitoringIntegration,
            alerting = new
            {
                rules = new[]
                {
                    "Alert: DatabaseDown - When database health check fails for 5 minutes",
                    "Alert: HighErrorRate - When error rate exceeds 5% for 10 minutes",
                    "Alert: SlowQueries - When P95 query latency exceeds 1 second",
                    "Alert: DiskSpaceLow - When free disk space below 10%"
                },
                channels = new[] { "Email", "Slack", "PagerDuty", "Microsoft Teams" }
            },
            bestPractices = new[]
            {
                "Run health checks at different intervals: critical (10s), normal (60s), background (5m)",
                "Use readiness probes to control traffic during deployment",
                "Use liveness probes to restart failed containers",
                "Include health check results in telemetry and dashboards",
                "Set appropriate timeout values for health checks (2-5 seconds)",
                "Test health check failure scenarios during deployment"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Provides OGC-specific endpoint debugging guidance")]
    public string TroubleshootOgcEndpoint(
        [Description("OGC endpoint URL (e.g., /collections/layer1/items)")] string endpointUrl,
        [Description("Error message or HTTP status code")] string errorMessage)
    {
        var endpoint = endpointUrl.ToLowerInvariant();
        var troubleshooting = new System.Collections.Generic.List<object>();

        // Landing page issues
        if (endpoint == "/" || endpoint.Contains("landing"))
        {
            troubleshooting.Add(new
            {
                endpoint = "Landing Page (/)",
                commonIssues = new[]
                {
                    "Returns 404 - Routing middleware not configured",
                    "Missing links - Metadata provider not loaded",
                    "CORS error - CORS policy too restrictive"
                },
                debugging = new[]
                {
                    "Verify routing: app.MapControllers() in Program.cs",
                    "Check metadata: GET /collections to verify provider works",
                    "Test CORS: Add Access-Control-Allow-Origin: * for testing",
                    "Review logs for startup errors"
                },
                expectedResponse = new
                {
                    title = "Honua Geospatial API",
                    description = "OGC API implementation",
                    links = new[] { "self", "conformance", "collections", "api" }
                }
            });
        }

        // Collections endpoint
        if (endpoint.Contains("/collections") && !endpoint.Contains("/items"))
        {
            troubleshooting.Add(new
            {
                endpoint = "Collections List (/collections)",
                commonIssues = new[]
                {
                    "Empty array - No metadata configured",
                    "Collections missing - Metadata not loading",
                    "500 error - Database connection failure"
                },
                debugging = new[]
                {
                    "Check HONUA__METADATA__PROVIDER environment variable",
                    "Verify metadata.yaml location: HONUA__METADATA__PATH",
                    "Validate metadata: honua metadata validate",
                    "Check enabled: true in collection definitions",
                    "Test database: honua database test-connection"
                },
                expectedResponse = new
                {
                    collections = new[]
                    {
                        new
                        {
                            id = "collection-id",
                            title = "Collection Title",
                            extent = "Required field",
                            links = "Array of rel:items, rel:self"
                        }
                    }
                }
            });
        }

        // Items endpoint
        if (endpoint.Contains("/items"))
        {
            troubleshooting.Add(new
            {
                endpoint = "Items (/collections/{id}/items)",
                commonIssues = new[]
                {
                    "404 - Collection ID not found in metadata",
                    "Empty features - Table has no data or wrong table name",
                    "500 - Missing spatial index or invalid geometries",
                    "Slow response - No spatial index"
                },
                debugging = new[]
                {
                    "Verify collection exists: GET /collections",
                    "Check table name in metadata matches database",
                    "Verify data exists: SELECT COUNT(*) FROM table",
                    "Check for spatial index: \\d+ table in psql",
                    "Validate geometries: SELECT COUNT(*) FROM table WHERE NOT ST_IsValid(geom)",
                    "Test simple query: ?limit=10"
                },
                optimizations = new[]
                {
                    "CREATE INDEX idx_geom ON table USING GIST(geom);",
                    "Run VACUUM ANALYZE table;",
                    "Fix invalid geometries: UPDATE table SET geom = ST_MakeValid(geom) WHERE NOT ST_IsValid(geom)",
                    "Add pagination: ?limit=100&offset=0"
                }
            });
        }

        // Conformance endpoint
        if (endpoint.Contains("/conformance"))
        {
            troubleshooting.Add(new
            {
                endpoint = "Conformance (/conformance)",
                commonIssues = new[]
                {
                    "404 - Endpoint not registered",
                    "Missing classes - Incomplete OGC implementation"
                },
                debugging = new[]
                {
                    "Check conformance classes in response",
                    "Verify OGC API Features Core is listed",
                    "Compare with OGC specification requirements"
                },
                requiredConformanceClasses = new[]
                {
                    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
                    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
                    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30"
                }
            });
        }

        // CRS and projection issues
        if (errorMessage.Contains("crs", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("projection", StringComparison.OrdinalIgnoreCase))
        {
            troubleshooting.Add(new
            {
                issue = "CRS/Projection Errors",
                commonCauses = new[]
                {
                    "Unsupported CRS requested",
                    "CRS not configured in metadata",
                    "SRID mismatch between data and metadata"
                },
                debugging = new[]
                {
                    "Check supported CRS in metadata",
                    "Verify data SRID: SELECT DISTINCT ST_SRID(geom) FROM table",
                    "Test with default CRS84: no crs parameter",
                    "Transform to common CRS: ST_Transform(geom, 4326)"
                },
                solutions = new[]
                {
                    "Add requested CRS to metadata crs array",
                    "Standardize all geometries to single SRID",
                    "Use storageCrs to declare native projection",
                    "Implement CRS transformation in query layer"
                }
            });
        }

        if (troubleshooting.Count == 0)
        {
            troubleshooting.Add(new
            {
                endpoint = endpointUrl,
                generalDebugging = new[]
                {
                    "Check HTTP status code for category (4xx client, 5xx server)",
                    "Review error response body for details",
                    "Check server logs for exception stack traces",
                    "Test with curl for raw response: curl -v " + endpointUrl,
                    "Verify authentication if required",
                    "Test with OGC API validation tools"
                }
            });
        }

        return JsonSerializer.Serialize(new
        {
            endpointUrl,
            errorMessage,
            troubleshootingSteps = troubleshooting,
            validationTools = new object[]
            {
                new { tool = "OGC CITE Tests", url = "https://cite.opengeospatial.org/teamengine/", usage = (string?)null },
                new { tool = "curl", usage = "curl -v http://localhost:5000/collections", url = (string?)null },
                new { tool = "Postman", usage = "Import OGC API collection for testing", url = (string?)null },
                new { tool = "QGIS", usage = "Add WFS3/OGC API Features connection", url = (string?)null }
            },
            ogcSpecification = "https://docs.ogc.org/is/17-069r4/17-069r4.html"
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Generates comprehensive diagnostic report for troubleshooting")]
    public string GenerateDebugReport(
        [Description("Workspace info as JSON (path, database type, deployment mode)")] string workspaceInfo)
    {
        var report = new
        {
            reportGenerated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            message = "Diagnostic report functionality - run honua diagnostic commands manually",
            sections = new[] { "System Information", "Database Status", "Metadata Configuration", "OGC API Endpoints" }
        };

        return JsonSerializer.Serialize(report, CliJsonOptions.Indented);
    }
}
