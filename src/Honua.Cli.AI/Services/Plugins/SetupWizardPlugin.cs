// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for guided Honua setup workflows.
/// Provides AI with structured setup recommendations and validation.
/// </summary>
public sealed class SetupWizardPlugin
{
    [KernelFunction, Description("Recommends a complete setup plan based on user's deployment scenario")]
    public string RecommendSetupPlan(
        [Description("Deployment target: development, staging, or production")] string deploymentTarget = "development",
        [Description("Database preference: postgis, spatialite, or undecided")] string databaseType = "undecided",
        [Description("Data source type: files, existing-database, or cloud-storage")] string dataSource = "files")
    {
        var isProd = deploymentTarget.Equals("production", StringComparison.OrdinalIgnoreCase);
        var isDev = deploymentTarget.Equals("development", StringComparison.OrdinalIgnoreCase);

        // Auto-recommend database based on deployment target if undecided
        if (databaseType.Equals("undecided", StringComparison.OrdinalIgnoreCase))
        {
            databaseType = isProd || deploymentTarget.Equals("staging", StringComparison.OrdinalIgnoreCase)
                ? "postgis"
                : "spatialite";
        }

        var isPostGIS = databaseType.Equals("postgis", StringComparison.OrdinalIgnoreCase);
        var setupPhases = new[]
        {
            new
            {
                phase = 1,
                name = "Environment Prerequisites",
                description = "Ensure required tools and dependencies are installed",
                steps = new[]
                {
                    new
                    {
                        action = "Check Docker installation",
                        required = isPostGIS && isDev,
                        command = "docker --version",
                        rationale = "Docker is needed for local PostGIS development environment"
                    },
                    new
                    {
                        action = "Check GDAL/ogr2ogr installation",
                        required = dataSource == "files",
                        command = "ogr2ogr --version",
                        rationale = "GDAL tools are needed for geospatial data format conversion"
                    },
                    new
                    {
                        action = "Install Honua CLI globally",
                        required = true,
                        command = "dotnet tool install -g honua-cli",
                        rationale = "CLI provides management and automation capabilities"
                    }
                }
            },
            new
            {
                phase = 2,
                name = "Database Setup",
                description = $"Configure and initialize {databaseType.ToUpper()} database",
                steps = isPostGIS ? new[]
                {
                    new
                    {
                        action = "Provision PostGIS database",
                        required = true,
                        command = isDev
                            ? "docker run -d --name honua-postgis -e POSTGRES_PASSWORD=honua -p 5432:5432 postgis/postgis:16-3.4"
                            : "Configure managed PostgreSQL with PostGIS extension (RDS, Azure Database, Cloud SQL)",
                        rationale = "PostGIS provides enterprise-grade spatial database capabilities"
                    },
                    new
                    {
                        action = "Enable PostGIS extension",
                        required = true,
                        command = "psql -c \"CREATE EXTENSION IF NOT EXISTS postgis;\"",
                        rationale = "PostGIS extension adds spatial data types and functions"
                    },
                    new
                    {
                        action = "Create application database user",
                        required = isProd,
                        command = "CREATE USER honua_app WITH PASSWORD 'xxx'; GRANT CONNECT ON DATABASE honua TO honua_app;",
                        rationale = "Separate application user follows security best practices"
                    }
                } : new[]
                {
                    new
                    {
                        action = "Create SpatiaLite database",
                        required = true,
                        command = "honua database create --type spatialite --path ./data/honua.db",
                        rationale = "SpatiaLite provides lightweight file-based spatial database"
                    },
                    new
                    {
                        action = "Initialize spatial metadata",
                        required = true,
                        command = "SELECT InitSpatialMetadata(1);",
                        rationale = "Initialize SpatiaLite spatial reference systems and metadata"
                    }
                }
            },
            new
            {
                phase = 3,
                name = "Data Ingestion",
                description = "Load geospatial data into database",
                steps = dataSource switch
                {
                    "files" => new[]
                    {
                        new
                        {
                            action = "Scan workspace for geospatial files",
                            required = true,
                            command = "honua data scan --workspace .",
                            rationale = "Identify .gpkg, .shp, .geojson, .tif files for ingestion"
                        },
                        new
                        {
                            action = "Ingest vector data",
                            required = true,
                            command = isPostGIS
                                ? "ogr2ogr -f PostgreSQL PG:\"dbname=honua\" data.gpkg -nln my_layer"
                                : "ogr2ogr -f SQLite spatialite:honua.db data.gpkg -nln my_layer",
                            rationale = "Import vector layers with geometry preservation"
                        },
                        new
                        {
                            action = "Configure raster tile caching",
                            required = false,
                            command = "honua raster-cache preseed --layer elevation --zoom 0-10",
                            rationale = "Pre-generate raster tiles for improved performance"
                        }
                    },
                    "existing-database" => new[]
                    {
                        new
                        {
                            action = "Test database connection",
                            required = true,
                            command = "honua database test-connection --connection-string \"...\"",
                            rationale = "Verify connectivity and permissions"
                        },
                        new
                        {
                            action = "Discover existing layers",
                            required = true,
                            command = "honua database discover-layers",
                            rationale = "Enumerate spatial tables and geometry columns"
                        },
                        new
                        {
                            action = "Analyze spatial indexes",
                            required = true,
                            command = "honua database analyze-indexes",
                            rationale = "Identify missing spatial indexes for performance"
                        }
                    },
                    _ => new[]
                    {
                        new
                        {
                            action = "Configure cloud storage connection",
                            required = true,
                            command = "honua secrets set CLOUD_STORAGE_KEY",
                            rationale = "Secure credential storage for S3/Azure Blob access"
                        },
                        new
                        {
                            action = "Sync cloud data catalog",
                            required = true,
                            command = "honua cloud sync --source s3://bucket/data",
                            rationale = "Download and process cloud-hosted geospatial data"
                        }
                    }
                }
            },
            new
            {
                phase = 4,
                name = "Service Configuration",
                description = "Configure Honua server metadata and endpoints",
                steps = new[]
                {
                    new
                    {
                        action = "Initialize workspace metadata",
                        required = true,
                        command = "honua metadata init --format yaml",
                        rationale = "Create metadata configuration for OGC API services"
                    },
                    new
                    {
                        action = "Configure collection metadata",
                        required = true,
                        command = "Edit metadata.yaml to define collections, spatial extent, and CRS",
                        rationale = "OGC API Collections requires metadata for service discovery"
                    },
                    new
                    {
                        action = "Validate metadata schema",
                        required = true,
                        command = "honua metadata validate",
                        rationale = "Ensure configuration matches JSON schema specification"
                    },
                    new
                    {
                        action = "Create metadata snapshot",
                        required = isProd,
                        command = $"honua metadata snapshot --label {deploymentTarget}-initial",
                        rationale = "Backup configuration before deployment"
                    }
                }
            },
            new
            {
                phase = 5,
                name = "Security & Authentication",
                description = "Configure access control and credential management",
                steps = isProd ? new[]
                {
                    new
                    {
                        action = "Bootstrap authentication system",
                        required = true,
                        command = "honua auth bootstrap --mode OAuth",
                        rationale = "Initialize authentication with identity provider integration"
                    },
                    new
                    {
                        action = "Create admin user",
                        required = true,
                        command = "honua auth create-user --username admin --role admin",
                        rationale = "Setup initial administrator account"
                    },
                    new
                    {
                        action = "Configure TLS certificates",
                        required = true,
                        command = "Setup Let's Encrypt with certbot or use cloud-managed certificates",
                        rationale = "HTTPS is mandatory for production deployments"
                    },
                    new
                    {
                        action = "Store database credentials",
                        required = true,
                        command = "honua secrets set DATABASE_PASSWORD",
                        rationale = "Secure encrypted storage of sensitive credentials"
                    }
                } : new[]
                {
                    new
                    {
                        action = "Configure QuickStart mode",
                        required = true,
                        command = "Set HONUA__AUTHENTICATION__MODE=QuickStart in appsettings.json",
                        rationale = "QuickStart mode allows rapid development without authentication"
                    },
                    new
                    {
                        action = "Store local credentials",
                        required = dataSource == "existing-database",
                        command = "honua secrets set DATABASE_PASSWORD",
                        rationale = "Secure storage even for development credentials"
                    }
                }
            },
            new
            {
                phase = 6,
                name = "Performance Optimization",
                description = "Configure caching, indexing, and performance tuning",
                steps = new[]
                {
                    new
                    {
                        action = "Create spatial indexes",
                        required = true,
                        command = isPostGIS
                            ? "CREATE INDEX CONCURRENTLY idx_layer_geom ON layer USING GIST(geom);"
                            : "SELECT CreateSpatialIndex('layer', 'geom');",
                        rationale = "Spatial indexes are critical for query performance"
                    },
                    new
                    {
                        action = "Configure Redis caching",
                        required = isProd,
                        command = "Add Redis:ConnectionString to appsettings.json",
                        rationale = "Distributed caching improves multi-instance scalability"
                    },
                    new
                    {
                        action = "Run VACUUM/ANALYZE",
                        required = true,
                        command = isPostGIS ? "VACUUM ANALYZE;" : "VACUUM;",
                        rationale = "Update database statistics for query optimization"
                    },
                    new
                    {
                        action = "Configure connection pooling",
                        required = isProd,
                        command = "Set MinPoolSize=10, MaxPoolSize=100 in connection string",
                        rationale = "Connection pooling reduces database overhead"
                    }
                }
            },
            new
            {
                phase = 7,
                name = "Deployment & Testing",
                description = "Launch server and validate endpoints",
                steps = new[]
                {
                    new
                    {
                        action = "Start Honua server",
                        required = true,
                        command = isDev
                            ? "dotnet run --project src/Honua.Server.Host"
                            : "Deploy to cloud platform (Azure App Service, AWS ECS, Google Cloud Run)",
                        rationale = "Launch the OGC API services"
                    },
                    new
                    {
                        action = "Test landing page",
                        required = true,
                        command = "curl http://localhost:5000/",
                        rationale = "Verify server is responding"
                    },
                    new
                    {
                        action = "Validate OGC conformance",
                        required = isProd,
                        command = "honua test ogc-conformance --url http://localhost:5000",
                        rationale = "Ensure OGC API compliance for interoperability"
                    },
                    new
                    {
                        action = "Test data access",
                        required = true,
                        command = "curl http://localhost:5000/collections/my_layer/items?limit=10",
                        rationale = "Verify data is accessible via OGC API Features"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            deploymentTarget,
            databaseType,
            dataSource,
            recommendation = isPostGIS ? "PostGIS" : "SpatiaLite",
            totalPhases = setupPhases.Length,
            estimatedTime = isProd ? "4-8 hours" : "1-2 hours",
            setupPhases
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Validates current workspace state against setup prerequisites")]
    public string ValidateWorkspaceReadiness(
        [Description("Path to workspace directory")] string workspacePath = "/tmp/workspace",
        [Description("Target deployment: development, staging, or production")] string deploymentTarget = "development")
    {
        var checks = new System.Collections.Generic.List<object>();

        try
        {
            // Check if workspace exists
            if (!Directory.Exists(workspacePath))
            {
                checks.Add(new
                {
                    check = "Workspace directory",
                    status = "FAIL",
                    message = $"Directory does not exist: {workspacePath}",
                    blocker = true
                });

                return JsonSerializer.Serialize(new
                {
                    ready = false,
                    workspacePath,
                    deploymentTarget,
                    checks
                });
            }

            checks.Add(new
            {
                check = "Workspace directory",
                status = "PASS",
                message = "Workspace directory exists and is accessible"
            });

            // Check for metadata configuration
            var metadataJson = Path.Combine(workspacePath, "metadata.json");
            var metadataYaml = Path.Combine(workspacePath, "metadata.yaml");
            var hasMetadata = File.Exists(metadataJson) || File.Exists(metadataYaml);

            checks.Add(new
            {
                check = "Metadata configuration",
                status = hasMetadata ? "PASS" : "WARN",
                message = hasMetadata
                    ? "Metadata configuration found"
                    : "No metadata.json or metadata.yaml found - will need to create"
            });

            // Check for data files
            var dataFiles = Directory.GetFiles(workspacePath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".shp", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".tif", StringComparison.OrdinalIgnoreCase))
                .ToList();

            checks.Add(new
            {
                check = "Geospatial data files",
                status = dataFiles.Any() ? "PASS" : "WARN",
                message = dataFiles.Any()
                    ? $"Found {dataFiles.Count} geospatial data files"
                    : "No geospatial data files found - may be using external database"
            });

            // Check for appsettings
            var appsettings = Path.Combine(workspacePath, "appsettings.json");
            var hasAppsettings = File.Exists(appsettings);

            checks.Add(new
            {
                check = "Application settings",
                status = hasAppsettings ? "PASS" : "WARN",
                message = hasAppsettings
                    ? "appsettings.json found"
                    : "No appsettings.json - using defaults"
            });

            // Production-specific checks
            if (deploymentTarget.Equals("production", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(new
                {
                    check = "TLS/HTTPS configuration",
                    status = "MANUAL",
                    message = "Verify TLS certificates are configured for production"
                });

                checks.Add(new
                {
                    check = "Backup strategy",
                    status = "MANUAL",
                    message = "Ensure database backup and disaster recovery plan is in place"
                });
            }

            var ready = !checks.Any(c => (string)((dynamic)c).status == "FAIL");

            return JsonSerializer.Serialize(new
            {
                ready,
                workspacePath,
                deploymentTarget,
                totalChecks = checks.Count,
                passed = checks.Count(c => ((dynamic)c).status.ToString() == "PASS"),
                warnings = checks.Count(c => ((dynamic)c).status.ToString() == "WARN"),
                failed = checks.Count(c => ((dynamic)c).status.ToString() == "FAIL"),
                checks
            });
        }
        catch (Exception ex)
        {
            checks.Add(new
            {
                check = "Workspace validation",
                status = "ERROR",
                message = ex.Message,
                blocker = true
            });

            return JsonSerializer.Serialize(new
            {
                ready = false,
                workspacePath,
                deploymentTarget,
                error = ex.Message,
                checks
            });
        }
    }

    [KernelFunction, Description("Generates database-specific connection string templates and examples")]
    public string GetConnectionStringTemplate(
        [Description("Database type: postgis or spatialite")] string databaseType = "postgis",
        [Description("Environment: development, staging, or production")] string environment = "development")
    {
        var isProd = environment.Equals("production", StringComparison.OrdinalIgnoreCase);
        var isPostGIS = databaseType.Equals("postgis", StringComparison.OrdinalIgnoreCase);

        if (isPostGIS)
        {
            var templates = new[]
            {
                new
                {
                    name = "Development (Local Docker)",
                    connectionString = "Host=localhost;Port=5432;Database=honua;Username=postgres;Password=[use secrets]",
                    notes = "Default PostGIS Docker container configuration"
                },
                new
                {
                    name = "Production (Managed PostgreSQL)",
                    connectionString = "Host=db.example.com;Port=5432;Database=honua;Username=honua_app;Password=[use secrets];SSL Mode=Require;Trust Server Certificate=false",
                    notes = "SSL/TLS required for production. Store password in secrets manager."
                },
                new
                {
                    name = "Production (AWS RDS)",
                    connectionString = "Host=honua.xxx.us-east-1.rds.amazonaws.com;Port=5432;Database=honua;Username=honua_app;Password=[use secrets];SSL Mode=Require",
                    notes = "RDS provides managed PostgreSQL with automated backups"
                },
                new
                {
                    name = "Production (Azure Database)",
                    connectionString = "Host=honua.postgres.database.azure.com;Port=5432;Database=honua;Username=honua_app@honua;Password=[use secrets];SSL Mode=Require",
                    notes = "Azure requires @servername suffix on username"
                }
            };

            return JsonSerializer.Serialize(new
            {
                databaseType = "PostGIS",
                environment,
                secretsCommand = "honua secrets set DATABASE_PASSWORD",
                recommendedPooling = isProd
                    ? "MinPoolSize=10;MaxPoolSize=100;Connection Lifetime=300"
                    : "Default pooling settings",
                templates
            });
        }
        else
        {
            var templates = new[]
            {
                new
                {
                    name = "Development (Local File)",
                    connectionString = "Data Source=./data/honua.db",
                    notes = "Relative path for local development"
                },
                new
                {
                    name = "Production (Absolute Path)",
                    connectionString = "Data Source=/var/lib/honua/data.db;Mode=ReadOnly",
                    notes = "Read-only mode for production serving. Use separate connection for writes."
                }
            };

            return JsonSerializer.Serialize(new
            {
                databaseType = "SpatiaLite",
                environment,
                warning = "SpatiaLite is not recommended for production workloads with high concurrency",
                recommendation = "Consider PostGIS for production deployments",
                templates
            });
        }
    }

    [KernelFunction, Description("Provides troubleshooting guidance for common setup issues")]
    public string TroubleshootSetupIssue(
        [Description("Issue category: connection, permissions, metadata, or performance")] string issueCategory = "general",
        [Description("Error message or symptom description")] string errorDescription = "No specific error")
    {
        var solutions = issueCategory.ToLowerInvariant() switch
        {
            "connection" => new[]
            {
                new
                {
                    symptom = "Connection refused / timeout",
                    causes = new[] { "Database not running", "Firewall blocking connection", "Wrong host/port" },
                    diagnostics = new[] {
                        "Check database is running: docker ps or systemctl status postgresql",
                        "Verify connection string: honua database test-connection",
                        "Check firewall rules: telnet dbhost 5432"
                    },
                    solutions = new[] {
                        "Start database service",
                        "Update connection string with correct host/port",
                        "Configure firewall to allow PostgreSQL port 5432"
                    }
                },
                new
                {
                    symptom = "SSL/TLS connection error",
                    causes = new[] { "SSL mode mismatch", "Certificate validation failure", "Wrong certificate path" },
                    diagnostics = new[] {
                        "Check server SSL requirements: psql -h host -U user -c 'SHOW ssl'",
                        "Verify certificate location and permissions"
                    },
                    solutions = new[] {
                        "Add SSL Mode=Require to connection string",
                        "Download server CA certificate",
                        "Use Trust Server Certificate=true for development only"
                    }
                }
            },
            "permissions" => new[]
            {
                new
                {
                    symptom = "Permission denied on table/schema",
                    causes = new[] { "Insufficient database user privileges", "Wrong schema search path" },
                    diagnostics = new[] {
                        "Check user grants: \\dp tablename in psql",
                        "Verify schema: SELECT current_schema(), current_user"
                    },
                    solutions = new[] {
                        "GRANT SELECT ON ALL TABLES IN SCHEMA public TO honua_app;",
                        "GRANT USAGE ON SCHEMA public TO honua_app;",
                        "Add Search Path=public to connection string"
                    }
                },
                new
                {
                    symptom = "Cannot create spatial index",
                    causes = new[] { "Missing PostGIS extension", "Wrong geometry type", "Insufficient permissions" },
                    diagnostics = new[] {
                        "Check PostGIS: SELECT PostGIS_version();",
                        "Verify geometry type: SELECT GeometryType(geom) FROM layer LIMIT 1;"
                    },
                    solutions = new[] {
                        "CREATE EXTENSION IF NOT EXISTS postgis;",
                        "GRANT honua_app TO postgres; -- temporary for index creation",
                        "Ensure geometry column has valid SRID"
                    }
                }
            },
            "metadata" => new[]
            {
                new
                {
                    symptom = "Metadata validation errors",
                    causes = new[] { "Invalid JSON/YAML syntax", "Missing required fields", "Schema version mismatch" },
                    diagnostics = new[] {
                        "Validate syntax: honua metadata validate",
                        "Check schema version in metadata",
                        "Review error messages for specific field issues"
                    },
                    solutions = new[] {
                        "Fix JSON/YAML syntax errors",
                        "Add required fields: id, title, extent, crs",
                        "Update schema version to match server"
                    }
                },
                new
                {
                    symptom = "Collections not appearing in API",
                    causes = new[] { "Metadata not loaded", "Wrong metadata provider", "Collection disabled" },
                    diagnostics = new[] {
                        "Check logs for metadata loading errors",
                        "Verify HONUA__METADATA__PROVIDER environment variable",
                        "Ensure enabled: true in collection config"
                    },
                    solutions = new[] {
                        "Restart server after metadata changes",
                        "Set HONUA__METADATA__PROVIDER=yaml and HONUA__METADATA__PATH",
                        "Remove enabled: false from collection definitions"
                    }
                }
            },
            "performance" => new[]
            {
                new
                {
                    symptom = "Slow query performance",
                    causes = new[] { "Missing spatial indexes", "Large result sets", "Complex geometries" },
                    diagnostics = new[] {
                        "Check for indexes: \\d+ tablename in psql",
                        "EXPLAIN ANALYZE query to see execution plan",
                        "Check geometry complexity: SELECT ST_NPoints(geom) FROM layer;"
                    },
                    solutions = new[] {
                        "CREATE INDEX ON layer USING GIST(geom);",
                        "Add LIMIT to queries",
                        "Simplify geometries: ST_SimplifyPreserveTopology(geom, tolerance)",
                        "Use bounding box filters in queries"
                    }
                },
                new
                {
                    symptom = "High memory usage",
                    causes = new[] { "Large raster tiles", "No connection pooling", "Memory leaks" },
                    diagnostics = new[] {
                        "Monitor memory: docker stats or top",
                        "Check connection count: SELECT count(*) FROM pg_stat_activity;",
                        "Review raster tile cache size"
                    },
                    solutions = new[] {
                        "Configure Redis caching for tile cache",
                        "Set MaxPoolSize in connection string",
                        "Reduce tile cache size or implement LRU eviction",
                        "Enable server garbage collection in appsettings"
                    }
                }
            },
            _ => new[]
            {
                new
                {
                    symptom = "General troubleshooting",
                    causes = new[] { "Check logs", "Verify configuration", "Review documentation" },
                    diagnostics = new[] {
                        "Check server logs for error details",
                        "Validate all configuration files",
                        "Review Honua documentation"
                    },
                    solutions = new[] {
                        "Enable detailed logging: set Logging:LogLevel:Default to Debug",
                        "Use honua diagnostic commands",
                        "Check GitHub issues for similar problems"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            issueCategory,
            errorDescription,
            troubleshootingSteps = solutions
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Checks if required dependencies are installed")]
    public string CheckDependencies()
    {
        var dependencies = new[]
        {
            new { name = "Docker", command = "docker", description = "Required for local PostGIS provisioning" },
            new { name = "ogr2ogr", command = "ogr2ogr", description = "GDAL tool for data format conversion" },
            new { name = "psql", command = "psql", description = "PostgreSQL command-line client" },
            new { name = "git", command = "git", description = "Version control for metadata tracking" }
        };

        // In a real implementation, we'd actually check if these commands exist
        // For now, return a structured response
        return JsonSerializer.Serialize(new
        {
            checkCount = dependencies.Length,
            dependencies = dependencies.Select(d => new
            {
                d.name,
                d.command,
                d.description,
                installed = "unknown", // Would check with `which` or `where` command
                required = d.name == "Docker" || d.name == "git"
            })
        });
    }

    [KernelFunction, Description("Provides configuration recommendations for workspace")]
    public string GetConfigurationRecommendations(
        [Description("Path to workspace directory")] string workspacePath = "/tmp/workspace")
    {
        return JsonSerializer.Serialize(new
        {
            workspacePath,
            note = "For detailed configuration analysis, use Workspace.GetConfigurationRecommendations",
            recommendations = new[]
            {
                new
                {
                    category = "Metadata",
                    priority = "high",
                    suggestion = "Initialize metadata configuration with 'honua metadata init'"
                },
                new
                {
                    category = "Database",
                    priority = "high",
                    suggestion = "Configure database connection string"
                },
                new
                {
                    category = "Security",
                    priority = "medium",
                    suggestion = "Set up credential management with 'honua secrets'"
                }
            }
        }, CliJsonOptions.Indented);
    }
}
