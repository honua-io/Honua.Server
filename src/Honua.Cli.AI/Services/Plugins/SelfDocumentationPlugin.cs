// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin that allows the AI assistant to document and explain its own capabilities.
/// Provides users with guidance on what the assistant can help them accomplish.
/// </summary>
public sealed class SelfDocumentationPlugin
{
    [KernelFunction, Description("Lists all available AI assistant capabilities organized by category")]
    public string ListCapabilities()
    {
        var capabilities = new[]
        {
            new
            {
                category = "Setup & Configuration",
                plugin = "SetupWizardPlugin",
                description = "Guides users through initial Honua server setup with deployment-specific recommendations",
                capabilities = new[]
                {
                    "Generate complete 7-phase setup plans (prerequisites, database, data, metadata, security, performance, deployment)",
                    "Validate workspace readiness with comprehensive checks",
                    "Provide database connection string templates for PostGIS and SpatiaLite",
                    "Troubleshoot common setup issues with diagnostic guidance"
                },
                exampleQueries = new[]
                {
                    "Help me set up Honua for production with PostGIS",
                    "I'm getting a connection error during setup",
                    "What's the best database for my development environment?"
                }
            },
            new
            {
                category = "Data Ingestion & Migration",
                plugin = "DataIngestionPlugin & MigrationPlugin",
                description = "Assists with loading geospatial data and migrating from Esri/ArcGIS services",
                capabilities = new[]
                {
                    "Analyze geospatial file formats (GeoPackage, Shapefile, GeoJSON, GeoTIFF, KML)",
                    "Recommend optimal ingestion strategies based on file size and target database",
                    "Validate data quality (null geometries, invalid features, CRS consistency)",
                    "Generate ogr2ogr commands with best-practice options",
                    "Plan and execute ArcGIS service migrations to Honua",
                    "Troubleshoot migration errors with specific guidance"
                },
                exampleQueries = new[]
                {
                    "How do I load a 5GB Shapefile into PostGIS?",
                    "Analyze this GeoJSON file for issues",
                    "Help me migrate an ArcGIS Feature Service to Honua",
                    "What's the best way to ingest 10,000 GeoTIFF files?"
                }
            },
            new
            {
                category = "Metadata & Standards",
                plugin = "MetadataPlugin & CompliancePlugin",
                description = "Auto-generates OGC metadata and validates standards compliance",
                capabilities = new[]
                {
                    "Generate OGC API Collections metadata from database schemas",
                    "Validate compliance with OGC API Features, Tiles, and STAC standards",
                    "Create STAC catalogs for raster datasets",
                    "Suggest metadata enhancements for discoverability",
                    "Validate GeoJSON against RFC 7946 specification",
                    "Audit security compliance and best practices"
                },
                exampleQueries = new[]
                {
                    "Generate metadata for my PostGIS tables",
                    "Is my OGC API Features endpoint compliant?",
                    "Create a STAC catalog for my elevation data",
                    "How can I improve my collection metadata?"
                }
            },
            new
            {
                category = "Performance & Optimization",
                plugin = "PerformancePlugin & OptimizationEnhancementsPlugin",
                description = "Analyzes and optimizes database, query, and caching performance",
                capabilities = new[]
                {
                    "Analyze database performance (PostgreSQL/SQLite configuration)",
                    "Suggest spatial query optimizations and indexes",
                    "Recommend caching strategies (HTTP, query result, tile, CDN)",
                    "Analyze SQL query performance with EXPLAIN output",
                    "Optimize vector tile generation (MVT configuration)",
                    "Recommend horizontal/vertical scaling strategies",
                    "Estimate server resource requirements"
                },
                exampleQueries = new[]
                {
                    "My spatial queries are slow, how can I optimize them?",
                    "What caching strategy should I use for this endpoint?",
                    "Analyze this EXPLAIN output",
                    "How many servers do I need for 1000 requests per second?"
                }
            },
            new
            {
                category = "Spatial Analysis & Operations",
                plugin = "SpatialAnalysisPlugin",
                description = "Provides guidance on spatial operations, CRS, and geometry validation",
                capabilities = new[]
                {
                    "Validate geometries and identify topology errors",
                    "Suggest appropriate PostGIS spatial functions (ST_*)",
                    "Recommend best CRS/projection for data extent and use case",
                    "Analyze spatial distribution (clustering, density)",
                    "Generate map styles based on geometry types and attributes"
                },
                exampleQueries = new[]
                {
                    "What CRS should I use for data covering California?",
                    "How do I find all polygons within 5km of a point?",
                    "My geometries are invalid, how do I fix them?",
                    "Generate a style for my road network layer"
                }
            },
            new
            {
                category = "Diagnostics & Troubleshooting",
                plugin = "DiagnosticsPlugin",
                description = "Diagnoses server issues, analyzes logs, and provides debugging guidance",
                capabilities = new[]
                {
                    "Diagnose server issues from symptoms and logs",
                    "Parse and analyze log files for patterns and errors",
                    "Suggest health check and monitoring strategies",
                    "Troubleshoot OGC endpoint errors",
                    "Generate comprehensive diagnostic reports"
                },
                exampleQueries = new[]
                {
                    "My server is returning 500 errors, help me diagnose",
                    "Analyze these logs and find the problem",
                    "What health checks should I implement?",
                    "My /collections endpoint is failing"
                }
            },
            new
            {
                category = "Security & Authentication",
                plugin = "SecurityPlugin",
                description = "Provides security recommendations and credential management guidance",
                capabilities = new[]
                {
                    "Recommend secure credential storage strategies",
                    "Validate credential requirements for deployments",
                    "Suggest authentication and authorization configurations",
                    "Provide production security checklists",
                    "Recommend security best practices by environment"
                },
                exampleQueries = new[]
                {
                    "How should I store database credentials in production?",
                    "What authentication method should I use for a public API?",
                    "Give me a security checklist for production deployment"
                }
            },
            new
            {
                category = "Testing & Quality Assurance",
                plugin = "TestingPlugin",
                description = "Helps create tests, generate test data, and validate conformance",
                capabilities = new[]
                {
                    "Generate synthetic spatial test data",
                    "Suggest integration test scenarios",
                    "Validate OGC API conformance",
                    "Generate load test scripts (NBomber, k6)",
                    "Analyze test results and failures"
                },
                exampleQueries = new[]
                {
                    "Generate test data for my points layer",
                    "What should I test for OGC API Features compliance?",
                    "Create a load test script for my tiles endpoint",
                    "Why are my OGC conformance tests failing?"
                }
            },
            new
            {
                category = "Documentation & Integration",
                plugin = "DocumentationPlugin & IntegrationPlugin",
                description = "Auto-generates documentation and helps integrate with third-party tools",
                capabilities = new[]
                {
                    "Generate OpenAPI/Swagger documentation",
                    "Create user guides and deployment documentation",
                    "Generate example HTTP/curl requests",
                    "Document data models and schemas",
                    "Generate QGIS and ArcGIS Pro connection configs",
                    "Suggest web mapping libraries (Leaflet, OpenLayers, Mapbox)",
                    "Generate JavaScript map client code"
                },
                exampleQueries = new[]
                {
                    "Generate API documentation for my collections",
                    "Create a deployment guide",
                    "How do I connect QGIS to my Honua server?",
                    "Generate a Leaflet map for my WFS layer"
                }
            },
            new
            {
                category = "Cloud Deployment & Infrastructure",
                plugin = "CloudDeploymentPlugin",
                description = "Assists with cloud deployment, containerization, and infrastructure as code",
                capabilities = new[]
                {
                    "Generate production Dockerfiles",
                    "Create Kubernetes deployment manifests",
                    "Compare cloud providers (AWS, Azure, GCP)",
                    "Generate Terraform/IaC configurations",
                    "Optimize for serverless deployments"
                },
                exampleQueries = new[]
                {
                    "Generate a Dockerfile for production",
                    "Create Kubernetes manifests for Honua",
                    "Which cloud provider is best for my needs?",
                    "Generate Terraform config for AWS deployment"
                }
            },
            new
            {
                category = "Monitoring & Observability",
                plugin = "MonitoringPlugin",
                description = "Recommends monitoring, metrics collection, and alerting strategies",
                capabilities = new[]
                {
                    "Suggest metrics to collect for service types",
                    "Generate Prometheus scrape configurations",
                    "Recommend alert thresholds based on SLAs",
                    "Analyze performance trends from metrics",
                    "Suggest structured logging strategies"
                },
                exampleQueries = new[]
                {
                    "What metrics should I monitor?",
                    "Generate Prometheus config for my services",
                    "What alert thresholds should I set?",
                    "How should I structure my logs?"
                }
            },
            new
            {
                category = "Workspace & Configuration",
                plugin = "WorkspacePlugin",
                description = "Analyzes workspaces and provides configuration recommendations",
                capabilities = new[]
                {
                    "Analyze workspace directory structure",
                    "Recommend configurations based on workspace type",
                    "Check for required tools and dependencies",
                    "Provide workspace-specific best practices"
                },
                exampleQueries = new[]
                {
                    "Analyze my Honua workspace",
                    "What's the recommended config for production?",
                    "Check if I have all required dependencies"
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            totalCategories = capabilities.Length,
            totalCapabilities = capabilities.Sum(c => c.capabilities.Length),
            categories = capabilities,
            usage = new
            {
                howToAsk = "Ask natural language questions about any of these topics",
                examples = new[]
                {
                    "Start with: 'Help me...' or 'How do I...'",
                    "Be specific: 'My spatial queries are slow on large datasets'",
                    "Provide context: 'I have a 10GB Shapefile and need to load it into PostGIS'"
                },
                multiStep = "I can guide you through complex multi-step workflows"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Explains what the assistant can help with for a specific topic or problem")]
    public string ExplainCapability(
        [Description("Topic or problem area (e.g., 'data ingestion', 'performance', 'setup')")] string topic)
    {
        var normalizedTopic = topic.ToLowerInvariant();

        var explanation = normalizedTopic switch
        {
            var t when t.Contains("setup") || t.Contains("install") || t.Contains("configure") => new
            {
                topic = "Setup & Configuration",
                whatICanDo = new[]
                {
                    "Walk you through complete Honua setup from scratch",
                    "Recommend database choices (PostGIS vs SpatiaLite) based on your needs",
                    "Generate step-by-step deployment plans (development, staging, production)",
                    "Validate your workspace has all prerequisites",
                    "Troubleshoot connection and configuration errors",
                    "Provide environment-specific best practices"
                },
                howToAsk = new[]
                {
                    "Help me set up Honua for the first time",
                    "I need a production-ready PostGIS setup",
                    "My database connection isn't working"
                },
                relatedCommands = new[]
                {
                    "honua setup - Interactive setup wizard",
                    "honua config init - Initialize CLI configuration",
                    "honua metadata validate - Validate setup"
                }
            },
            var t when t.Contains("data") || t.Contains("ingest") || t.Contains("load") || t.Contains("import") => new
            {
                topic = "Data Ingestion",
                whatICanDo = new[]
                {
                    "Analyze your data files (format, size, CRS, schema)",
                    "Recommend optimal loading strategies",
                    "Generate ogr2ogr commands with best practices",
                    "Validate data quality (geometry validity, CRS consistency)",
                    "Help with schema mapping and transformations",
                    "Troubleshoot ingestion errors"
                },
                howToAsk = new[]
                {
                    "How do I load this Shapefile?",
                    "Analyze this GeoPackage file",
                    "My data ingestion is too slow",
                    "What's wrong with these geometries?"
                },
                relatedCommands = new[]
                {
                    "honua data ingest - Managed data ingestion",
                    "ogrinfo - Inspect file contents",
                    "ogr2ogr - Data conversion and loading"
                }
            },
            var t when t.Contains("perform") || t.Contains("slow") || t.Contains("optim") || t.Contains("fast") => new
            {
                topic = "Performance & Optimization",
                whatICanDo = new[]
                {
                    "Analyze why queries are slow",
                    "Recommend spatial indexes",
                    "Suggest caching strategies (HTTP, tiles, query results)",
                    "Tune database configuration (PostgreSQL, SQLite)",
                    "Analyze query execution plans",
                    "Recommend scaling strategies",
                    "Estimate server resource needs"
                },
                howToAsk = new[]
                {
                    "My spatial queries are too slow",
                    "What caching should I use?",
                    "How can I make this faster?",
                    "Do I need more servers?"
                },
                relatedCommands = new[]
                {
                    "EXPLAIN ANALYZE - Query performance analysis",
                    "CREATE INDEX - Create spatial indexes",
                    "honua status - Check service health"
                }
            },
            var t when t.Contains("migrat") || t.Contains("arcgis") || t.Contains("esri") => new
            {
                topic = "Esri/ArcGIS Migration",
                whatICanDo = new[]
                {
                    "Analyze ArcGIS REST service structure",
                    "Plan step-by-step migration to Honua",
                    "Check migration compatibility",
                    "Generate migration scripts",
                    "Troubleshoot migration errors",
                    "Validate migrated data"
                },
                howToAsk = new[]
                {
                    "Help me migrate from ArcGIS Server",
                    "Analyze this ArcGIS Feature Service",
                    "My migration failed with this error..."
                },
                relatedCommands = new[]
                {
                    "honua migrate arcgis - Migrate Esri services",
                    "honua migrate status - Check migration progress",
                    "honua migrate jobs - List migration jobs"
                }
            },
            var t when t.Contains("error") || t.Contains("debug") || t.Contains("troubl") || t.Contains("fix") => new
            {
                topic = "Diagnostics & Troubleshooting",
                whatICanDo = new[]
                {
                    "Diagnose server errors from symptoms",
                    "Analyze log files for root causes",
                    "Suggest health checks",
                    "Debug OGC endpoint issues",
                    "Generate diagnostic reports",
                    "Provide step-by-step fixes"
                },
                howToAsk = new[]
                {
                    "My server is returning 500 errors",
                    "Help me diagnose this problem",
                    "What's wrong with this endpoint?",
                    "Analyze these error logs"
                },
                relatedCommands = new[]
                {
                    "honua status - Server health check",
                    "honua metadata validate - Validate configuration",
                    "docker logs - View container logs"
                }
            },
            var t when t.Contains("test") || t.Contains("qa") || t.Contains("quality") => new
            {
                topic = "Testing & Quality Assurance",
                whatICanDo = new[]
                {
                    "Generate synthetic test data",
                    "Suggest test scenarios",
                    "Validate OGC conformance",
                    "Create load test scripts",
                    "Analyze test failures",
                    "Recommend test coverage"
                },
                howToAsk = new[]
                {
                    "Generate test data for my layer",
                    "What should I test?",
                    "Create a load test",
                    "Is my API OGC compliant?"
                },
                relatedCommands = new[]
                {
                    "honua test ogc-conformance - Run OGC tests",
                    "pytest - Run integration tests",
                    "nbomber - Load testing"
                }
            },
            var t when t.Contains("deploy") || t.Contains("docker") || t.Contains("k8s") || t.Contains("cloud") => new
            {
                topic = "Deployment & Infrastructure",
                whatICanDo = new[]
                {
                    "Generate production Dockerfiles",
                    "Create Kubernetes manifests",
                    "Compare cloud providers",
                    "Generate Infrastructure as Code (Terraform)",
                    "Optimize for serverless",
                    "Provide deployment checklists"
                },
                howToAsk = new[]
                {
                    "How do I deploy to production?",
                    "Generate a Dockerfile",
                    "Create Kubernetes config",
                    "Which cloud provider should I use?"
                },
                relatedCommands = new[]
                {
                    "docker build - Build container image",
                    "kubectl apply - Deploy to Kubernetes",
                    "terraform apply - Provision infrastructure"
                }
            },
            _ => new
            {
                topic = "General Assistance",
                whatICanDo = new[]
                {
                    "I can help with any Honua-related question",
                    "Ask me about setup, data, performance, security, testing, deployment",
                    "I provide step-by-step guidance and generate commands",
                    "I can troubleshoot errors and analyze issues"
                },
                howToAsk = new[]
                {
                    "Be specific about what you're trying to accomplish",
                    "Provide context (environment, error messages, etc.)",
                    "Ask follow-up questions for more details"
                },
                relatedCommands = new[]
                {
                    "honua --help - See all available commands",
                    "honua devsecops - Interactive AI Devsecops",
                    "honua setup - Guided setup wizard"
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            requestedTopic = topic,
            explanation,
            nextSteps = new[]
            {
                "Ask a specific question about this topic",
                "Provide details about your situation",
                "I'll give you actionable guidance and commands"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Provides examples of questions the assistant can answer")]
    public string ShowExampleQueries(
        [Description("Optional category to filter examples (setup, data, performance, etc.)")] string? category = null)
    {
        var allExamples = new[]
        {
            new { category = "Setup", query = "Help me set up Honua for production with PostGIS", expectedGuidance = "7-phase setup plan with security best practices" },
            new { category = "Setup", query = "I'm getting a database connection error", expectedGuidance = "Connection troubleshooting steps and validation" },
            new { category = "Data", query = "How do I load a 5GB Shapefile into PostGIS?", expectedGuidance = "Batch loading strategy with ogr2ogr commands" },
            new { category = "Data", query = "Analyze this GeoJSON file for issues", expectedGuidance = "File format analysis and quality validation" },
            new { category = "Data", query = "My geometries are invalid, how do I fix them?", expectedGuidance = "ST_MakeValid usage and validation queries" },
            new { category = "Performance", query = "My spatial queries are too slow", expectedGuidance = "Index recommendations and query optimization" },
            new { category = "Performance", query = "What caching strategy should I use?", expectedGuidance = "Caching recommendations based on traffic patterns" },
            new { category = "Performance", query = "Analyze this EXPLAIN output", expectedGuidance = "Query plan analysis and optimization suggestions" },
            new { category = "Migration", query = "Help me migrate from ArcGIS Feature Service", expectedGuidance = "Service analysis and migration plan" },
            new { category = "Metadata", query = "Generate OGC metadata for my PostGIS tables", expectedGuidance = "Auto-generated collection metadata" },
            new { category = "Metadata", query = "Is my API OGC Features compliant?", expectedGuidance = "Conformance validation and issues" },
            new { category = "Spatial", query = "What CRS should I use for California data?", expectedGuidance = "CRS recommendations with pros/cons" },
            new { category = "Spatial", query = "How do I buffer polygons by 100 meters?", expectedGuidance = "ST_Buffer usage with examples" },
            new { category = "Diagnostics", query = "My /collections endpoint returns 500 errors", expectedGuidance = "Endpoint-specific troubleshooting" },
            new { category = "Diagnostics", query = "Analyze these server logs", expectedGuidance = "Log parsing and root cause identification" },
            new { category = "Security", query = "How should I store database credentials?", expectedGuidance = "Credential management best practices" },
            new { category = "Testing", query = "Generate test data for my points layer", expectedGuidance = "Synthetic spatial test data generation" },
            new { category = "Testing", query = "Create a load test for my tiles endpoint", expectedGuidance = "NBomber/k6 load test script" },
            new { category = "Deployment", query = "Generate a production Dockerfile", expectedGuidance = "Optimized multi-stage Dockerfile" },
            new { category = "Deployment", query = "Which cloud provider should I use?", expectedGuidance = "AWS/Azure/GCP comparison" },
            new { category = "Integration", query = "How do I connect QGIS to my server?", expectedGuidance = "QGIS connection XML configuration" },
            new { category = "Documentation", query = "Generate API docs for my collections", expectedGuidance = "OpenAPI/Swagger documentation" }
        };

        var filtered = category.IsNullOrEmpty()
            ? allExamples
            : allExamples.Where(e => e.category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToArray();

        return JsonSerializer.Serialize(new
        {
            totalExamples = filtered.Length,
            categoryFilter = category ?? "All categories",
            examples = filtered,
            howToUse = new
            {
                approach = "Ask similar questions in natural language",
                tips = new[]
                {
                    "Be specific about your goal",
                    "Include relevant details (file sizes, error messages, environment)",
                    "Ask follow-up questions for clarification",
                    "I can generate actual commands you can run"
                }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Recommends which assistant capability to use for a given problem")]
    public string RecommendCapability(
        [Description("Description of what user wants to accomplish or problem they're facing")] string userGoal)
    {
        var goal = userGoal.ToLowerInvariant();

        var recommendation = goal switch
        {
            var g when g.Contains("first time") || g.Contains("getting started") || g.Contains("begin") =>
                new { usePlugin = "SetupWizardPlugin", reason = "You're starting fresh - setup wizard will guide you through everything", command = "honua setup" },

            var g when g.Contains("load") || g.Contains("import") || g.Contains("ingest") =>
                new { usePlugin = "DataIngestionPlugin", reason = "You need to load data - I'll analyze your files and recommend strategies", command = "Describe your data file" },

            var g when g.Contains("slow") || g.Contains("performance") || g.Contains("faster") =>
                new { usePlugin = "PerformancePlugin", reason = "You need performance optimization - I'll analyze and recommend improvements", command = "Describe the slow operation" },

            var g when g.Contains("arcgis") || g.Contains("esri") || g.Contains("feature server") =>
                new { usePlugin = "MigrationPlugin", reason = "You're migrating from Esri - I'll help you plan and execute the migration", command = "Provide the ArcGIS service URL" },

            var g when g.Contains("error") || g.Contains("broken") || g.Contains("not working") =>
                new { usePlugin = "DiagnosticsPlugin", reason = "Something's not working - I'll help diagnose and fix the issue", command = "Share the error message or symptoms" },

            var g when g.Contains("metadata") || g.Contains("collection") || g.Contains("ogc") =>
                new { usePlugin = "MetadataPlugin", reason = "You need metadata - I can auto-generate it from your data sources", command = "Describe your data sources" },

            var g when g.Contains("test") || g.Contains("validate") || g.Contains("verify") =>
                new { usePlugin = "TestingPlugin", reason = "You need to test - I'll help create tests and validate compliance", command = "Describe what you want to test" },

            var g when g.Contains("deploy") || g.Contains("production") || g.Contains("kubernetes") || g.Contains("docker") =>
                new { usePlugin = "CloudDeploymentPlugin", reason = "You're deploying - I'll generate deployment configs and guides", command = "Describe your deployment target" },

            var g when g.Contains("crs") || g.Contains("projection") || g.Contains("spatial") =>
                new { usePlugin = "SpatialAnalysisPlugin", reason = "You have spatial questions - I'll help with CRS, operations, and analysis", command = "Describe your spatial problem" },

            var g when g.Contains("monitor") || g.Contains("metrics") || g.Contains("alerts") =>
                new { usePlugin = "MonitoringPlugin", reason = "You need observability - I'll recommend monitoring and alerting", command = "Describe your monitoring needs" },

            _ => new { usePlugin = "Multiple plugins", reason = "I'll use the right combination of my capabilities to help you", command = "Explain what you're trying to accomplish" }
        };

        return JsonSerializer.Serialize(new
        {
            yourGoal = userGoal,
            recommendation,
            nextStep = "Provide more details and I'll give you specific, actionable guidance"
        }, CliJsonOptions.Indented);
    }
}
