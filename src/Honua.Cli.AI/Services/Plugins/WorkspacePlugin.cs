// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Security;

namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for workspace analysis.
/// Provides AI with safe, read-only access to workspace metadata and configuration.
/// </summary>
public sealed class WorkspacePlugin
{
    [KernelFunction, Description("Analyzes a workspace directory and reports on available metadata files and structure")]
    public string AnalyzeWorkspace(
        [Description("Path to the workspace directory")] string workspacePath)
    {
        try
        {
            // Validate path format to prevent traversal attacks
            try
            {
                PathTraversalValidator.ValidatePathFormat(workspacePath);
            }
            catch (SecurityException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Invalid path format: {ex.Message}",
                    workspacePath = "[REDACTED]"
                });
            }

            if (!Directory.Exists(workspacePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Workspace directory does not exist",
                    workspacePath
                });
            }

            // Check for common Honua metadata files
            var metadataJson = Path.Combine(workspacePath, "metadata.json");
            var metadataYaml = Path.Combine(workspacePath, "metadata.yaml");
            var metadataYml = Path.Combine(workspacePath, "metadata.yml");

            var hasMetadataJson = File.Exists(metadataJson);
            var hasMetadataYaml = File.Exists(metadataYaml) || File.Exists(metadataYml);

            // Look for data files
            var dataFiles = Directory.GetFiles(workspacePath, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".gpkg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".shp", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                workspacePath,
                configuration = new
                {
                    hasMetadataJson,
                    hasMetadataYaml,
                    isConfigured = hasMetadataJson || hasMetadataYaml,
                    metadataFormat = hasMetadataJson ? "JSON" : hasMetadataYaml ? "YAML" : "None"
                },
                dataFiles = new
                {
                    count = dataFiles.Count,
                    files = dataFiles.Select(f => new
                    {
                        name = Path.GetFileName(f),
                        extension = Path.GetExtension(f),
                        sizeKB = new FileInfo(f).Length / 1024.0
                    })
                },
                recommendations = GetWorkspaceRecommendations(hasMetadataJson, hasMetadataYaml, dataFiles.Count)
            });
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SecretSanitizer.SanitizeException(ex);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = sanitizedMessage,
                workspacePath
            });
        }
    }

    [KernelFunction, Description("Gets configuration recommendations based on workspace type and deployment target")]
    public string GetConfigurationRecommendations(
        [Description("Workspace type: development, staging, or production")] string workspaceType = "development",
        [Description("Database type: postgis or spatialite")] string databaseType = "postgis")
    {
        var isProd = workspaceType.Equals("production", StringComparison.OrdinalIgnoreCase);
        var isPostGIS = databaseType.Equals("postgis", StringComparison.OrdinalIgnoreCase);

        var recommendations = new[]
        {
            new {
                category = "Spatial Indexes",
                priority = "High",
                recommendation = isPostGIS
                    ? "Create GIST indexes on all geometry columns using CREATE INDEX CONCURRENTLY for zero-downtime"
                    : "Create spatial indexes on all geometry columns using CreateSpatialIndex()",
                command = isPostGIS
                    ? "CREATE INDEX CONCURRENTLY idx_table_geom ON table USING GIST(geom);"
                    : "SELECT CreateSpatialIndex('table', 'geom');"
            },
            new {
                category = "Primary Keys",
                priority = "High",
                recommendation = "Ensure all layers have primary key constraints for optimal OGC API performance",
                command = isPostGIS
                    ? "ALTER TABLE table ADD PRIMARY KEY (id);"
                    : "N/A - Define primary key in CREATE TABLE statement"
            },
            new {
                category = "Connection Pooling",
                priority = isProd ? "High" : "Medium",
                recommendation = isProd
                    ? "Configure connection pooling with min 10, max 100 connections for production workloads"
                    : "Use default connection pooling settings for development",
                command = "Set in appsettings.json: \"ConnectionStrings\": { \"MinPoolSize\": 10, \"MaxPoolSize\": 100 }"
            },
            new {
                category = "Query Optimization",
                priority = "Medium",
                recommendation = isPostGIS
                    ? "Run ANALYZE on all tables after bulk data loads to update statistics"
                    : "Run VACUUM after bulk data loads to optimize database file",
                command = isPostGIS
                    ? "ANALYZE table;"
                    : "VACUUM;"
            },
            new {
                category = "Backup Strategy",
                priority = isProd ? "Critical" : "Low",
                recommendation = isProd
                    ? "Implement automated backups with point-in-time recovery (PITR)"
                    : "Use metadata snapshots for configuration versioning",
                command = isProd
                    ? "Configure pg_basebackup or use cloud provider managed backups"
                    : "honua metadata snapshot --label pre-deployment"
            }
        };

        return JsonSerializer.Serialize(new
        {
            workspaceType,
            databaseType,
            isProduction = isProd,
            recommendationCount = recommendations.Length,
            recommendations
        });
    }

    [KernelFunction, Description("Checks if required tools and dependencies are installed (Docker, ogr2ogr, psql, etc.)")]
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

    private static object[] GetWorkspaceRecommendations(bool hasMetadataJson, bool hasMetadataYaml, int dataFileCount)
    {
        var recommendations = new System.Collections.Generic.List<object>();

        if (!hasMetadataJson && !hasMetadataYaml)
        {
            recommendations.Add(new
            {
                priority = "High",
                action = "Initialize metadata configuration",
                command = "honua config init --host http://localhost:5000"
            });
        }

        if (dataFileCount > 0 && (!hasMetadataJson && !hasMetadataYaml))
        {
            recommendations.Add(new
            {
                priority = "High",
                action = $"Found {dataFileCount} data files but no service configuration",
                command = "Use 'honua assistant setup' to configure services for your data"
            });
        }

        if (dataFileCount == 0)
        {
            recommendations.Add(new
            {
                priority = "Medium",
                action = "No geospatial data files found in workspace",
                command = "Place .gpkg, .shp, .geojson, or .tif files in the workspace directory"
            });
        }

        return recommendations.ToArray();
    }
}
