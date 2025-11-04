// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Pre-defined workspace templates for common deployment scenarios.
/// These are high-confidence patterns for instant plan generation.
/// </summary>
public static class PatternTemplates
{
    public static IReadOnlyList<DeploymentPattern> GetBuiltInTemplates()
    {
        return new List<DeploymentPattern>
        {
            CreateProductionPostGISTemplate(),
            CreateSTACCatalogTemplate(),
            CreateVectorTileServiceTemplate(),
            CreateRasterProcessingPipelineTemplate(),
            CreateDevelopmentEnvironmentTemplate()
        };
    }

    private static DeploymentPattern CreateProductionPostGISTemplate()
    {
        return new DeploymentPattern
        {
            Id = "template-prod-postgis-aws",
            Name = "Production PostGIS on AWS (RDS + ECS)",
            CloudProvider = "aws",
            DataVolumeMin = 100,
            DataVolumeMax = 10000,
            ConcurrentUsersMin = 50,
            ConcurrentUsersMax = 10000,
            SuccessRate = 0.95,
            DeploymentCount = 100,
            HumanApproved = true,
            ApprovedBy = "system",
            ApprovedDate = DateTime.UtcNow.AddMonths(-6),
            Version = 1,
            IsTemplate = true,
            Tags = new List<string> { "production", "postgis", "aws", "high-availability" },
            Configuration = new
            {
                database = new
                {
                    engine = "postgres",
                    version = "16",
                    instanceClass = "db.r6g.xlarge",
                    storage = new { type = "gp3", size = 500, iops = 12000 },
                    multiAZ = true,
                    backupRetention = 7,
                    encryption = true
                },
                compute = new
                {
                    platform = "ecs-fargate",
                    cpu = 2048,
                    memory = 4096,
                    autoscaling = new { min = 2, max = 10, targetCPU = 70 }
                },
                networking = new
                {
                    vpc = "dedicated",
                    privateSubnets = true,
                    natGateway = true,
                    loadBalancer = "application"
                },
                security = new
                {
                    ssl = true,
                    waf = true,
                    secretsManager = true,
                    iamRoles = true
                },
                monitoring = new
                {
                    cloudWatch = true,
                    enhancedMonitoring = true,
                    performanceInsights = true,
                    alarms = new[] { "cpu", "memory", "connections", "storage" }
                }
            }
        };
    }

    private static DeploymentPattern CreateSTACCatalogTemplate()
    {
        return new DeploymentPattern
        {
            Id = "template-stac-catalog-serverless",
            Name = "STAC Catalog (Serverless on AWS)",
            CloudProvider = "aws",
            DataVolumeMin = 1,
            DataVolumeMax = 10000,
            ConcurrentUsersMin = 10,
            ConcurrentUsersMax = 1000,
            SuccessRate = 0.92,
            DeploymentCount = 75,
            HumanApproved = true,
            ApprovedBy = "system",
            ApprovedDate = DateTime.UtcNow.AddMonths(-3),
            Version = 1,
            IsTemplate = true,
            Tags = new List<string> { "stac", "serverless", "aws", "geospatial" },
            Configuration = new
            {
                api = new
                {
                    type = "lambda",
                    runtime = "python3.12",
                    memory = 1024,
                    timeout = 30
                },
                database = new
                {
                    type = "dynamodb",
                    billingMode = "PAY_PER_REQUEST",
                    pointInTimeRecovery = true
                },
                storage = new
                {
                    bucket = "stac-assets",
                    versioning = true,
                    lifecycle = new { transitionToDays = 90, storageClass = "INTELLIGENT_TIERING" }
                },
                cdn = new
                {
                    cloudFront = true,
                    caching = new { defaultTTL = 3600, maxTTL = 86400 }
                },
                search = new
                {
                    type = "elasticsearch-serverless",
                    encryption = true
                }
            }
        };
    }

    private static DeploymentPattern CreateVectorTileServiceTemplate()
    {
        return new DeploymentPattern
        {
            Id = "template-vector-tiles-cdn",
            Name = "Vector Tile Service (PostGIS + CDN)",
            CloudProvider = "aws",
            DataVolumeMin = 10,
            DataVolumeMax = 5000,
            ConcurrentUsersMin = 100,
            ConcurrentUsersMax = 100000,
            SuccessRate = 0.94,
            DeploymentCount = 60,
            HumanApproved = true,
            ApprovedBy = "system",
            ApprovedDate = DateTime.UtcNow.AddMonths(-4),
            Version = 1,
            IsTemplate = true,
            Tags = new List<string> { "vector-tiles", "mvt", "cdn", "high-performance" },
            Configuration = new
            {
                tileServer = new
                {
                    type = "martin",
                    runtime = "ecs-fargate",
                    cpu = 1024,
                    memory = 2048,
                    autoscaling = new { min = 2, max = 20, targetRPS = 1000 }
                },
                database = new
                {
                    engine = "postgres",
                    version = "16",
                    instanceClass = "db.r6g.large",
                    readReplicas = 2,
                    connectionPooling = true
                },
                cdn = new
                {
                    provider = "cloudfront",
                    caching = new
                    {
                        defaultTTL = 86400,
                        maxTTL = 604800,
                        compress = true,
                        queryStringCaching = true
                    },
                    edgeLocations = "all"
                },
                optimization = new
                {
                    preGenerateZoomLevels = new[] { 0, 1, 2, 3, 4, 5 },
                    spatialIndexes = true,
                    mvtOptimization = true
                }
            }
        };
    }

    private static DeploymentPattern CreateRasterProcessingPipelineTemplate()
    {
        return new DeploymentPattern
        {
            Id = "template-raster-pipeline-batch",
            Name = "Raster Processing Pipeline (AWS Batch)",
            CloudProvider = "aws",
            DataVolumeMin = 100,
            DataVolumeMax = 100000,
            ConcurrentUsersMin = 1,
            ConcurrentUsersMax = 50,
            SuccessRate = 0.90,
            DeploymentCount = 45,
            HumanApproved = true,
            ApprovedBy = "system",
            ApprovedDate = DateTime.UtcNow.AddMonths(-2),
            Version = 1,
            IsTemplate = true,
            Tags = new List<string> { "raster", "batch", "processing", "cog" },
            Configuration = new
            {
                compute = new
                {
                    type = "aws-batch",
                    computeEnvironment = "spot",
                    instanceTypes = new[] { "c6i.4xlarge", "c6i.8xlarge" },
                    maxvCpus = 256
                },
                storage = new
                {
                    input = new { bucket = "raster-input", storageClass = "STANDARD" },
                    output = new { bucket = "raster-output", storageClass = "INTELLIGENT_TIERING" },
                    scratch = new { type = "efs", throughputMode = "bursting" }
                },
                processing = new
                {
                    containerImage = "gdal:latest",
                    operations = new[] { "cog-conversion", "pyramids", "compression" },
                    parallelism = "per-tile"
                },
                orchestration = new
                {
                    type = "step-functions",
                    errorHandling = new { maxAttempts = 3, backoff = "exponential" },
                    notifications = new { sns = true, email = true }
                }
            }
        };
    }

    private static DeploymentPattern CreateDevelopmentEnvironmentTemplate()
    {
        return new DeploymentPattern
        {
            Id = "template-dev-environment-local",
            Name = "Development Environment (Docker Compose)",
            CloudProvider = "local",
            DataVolumeMin = 1,
            DataVolumeMax = 100,
            ConcurrentUsersMin = 1,
            ConcurrentUsersMax = 10,
            SuccessRate = 0.98,
            DeploymentCount = 200,
            HumanApproved = true,
            ApprovedBy = "system",
            ApprovedDate = DateTime.UtcNow.AddMonths(-1),
            Version = 1,
            IsTemplate = true,
            Tags = new List<string> { "development", "docker", "local", "quick-start" },
            Configuration = new
            {
                services = new
                {
                    database = new
                    {
                        image = "postgis/postgis:16-3.4",
                        ports = new[] { "5432:5432" },
                        volumes = new[] { "pgdata:/var/lib/postgresql/data" },
                        environment = new { POSTGRES_DB = "honua", POSTGRES_USER = "honua" }
                    },
                    api = new
                    {
                        image = "honua-server:latest",
                        ports = new[] { "8080:8080" },
                        depends = new[] { "database" }
                    },
                    frontend = new
                    {
                        image = "honua-ui:latest",
                        ports = new[] { "3000:3000" },
                        depends = new[] { "api" }
                    }
                },
                development = new
                {
                    hotReload = true,
                    debugPorts = new { api = 5005, frontend = 9229 },
                    volumeMounts = new[] { "./src:/app/src" }
                }
            }
        };
    }
}
