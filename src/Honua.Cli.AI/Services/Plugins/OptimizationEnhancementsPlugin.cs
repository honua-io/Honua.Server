// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for advanced performance optimization.
/// Enhances the existing PerformancePlugin with additional functions.
/// </summary>
public sealed class OptimizationEnhancementsPlugin
{
    [KernelFunction, Description("Analyzes SQL query performance using EXPLAIN output")]
    public string AnalyzeQueryPerformance(
        [Description("SQL query text")] string queryText = "SELECT * FROM example WHERE ST_Intersects(geom, ST_MakeEnvelope(0,0,1,1,4326))",
        [Description("EXPLAIN ANALYZE output")] string explainOutput = "")
    {
        return JsonSerializer.Serialize(new
        {
            queryText,
            explainOutput,
            analysis = new
            {
                keyIndicators = new object[]
                {
                    new { metric = "Execution Time", check = "Total runtime in milliseconds", threshold = "< 1000ms for most queries", action = (string?)null, positive = (string?)null, consider = (string?)null },
                    new { metric = "Seq Scan", check = "Sequential scans indicate missing indexes", action = "Add appropriate index", threshold = (string?)null, positive = (string?)null, consider = (string?)null },
                    new { metric = "Index Scan", check = "Using index efficiently", positive = "Good performance", action = (string?)null, threshold = (string?)null, consider = (string?)null },
                    new { metric = "Nested Loop", check = "Can be expensive for large datasets", consider = "Hash join or merge join", action = (string?)null, threshold = (string?)null, positive = (string?)null },
                    new { metric = "Rows Estimated vs Actual", check = "Large discrepancy indicates stale statistics", action = "Run ANALYZE", threshold = (string?)null, positive = (string?)null, consider = (string?)null }
                },
                commonIssues = new[]
                {
                    new
                    {
                        issue = "Sequential Scan on Large Table",
                        symptom = "Seq Scan on table with >10k rows",
                        fix = new[]
                        {
                            "CREATE INDEX idx_column ON table(column);",
                            "For spatial: CREATE INDEX idx_geom ON table USING GIST(geom);",
                            "Run ANALYZE table; after index creation"
                        }
                    },
                    new
                    {
                        issue = "Nested Loop with High Row Count",
                        symptom = "Nested Loop with actual rows > 10k",
                        fix = new[]
                        {
                            "Increase work_mem: SET work_mem = '256MB';",
                            "Force hash join: SET enable_nestloop = off; (testing only)",
                            "Add join column indexes"
                        }
                    },
                    new
                    {
                        issue = "Sort Operation Exceeding Memory",
                        symptom = "Sort using disk (external merge)",
                        fix = new[]
                        {
                            "Increase work_mem for session",
                            "Add index on ORDER BY column",
                            "Reduce result set size with better WHERE clause"
                        }
                    },
                    new
                    {
                        issue = "Bitmap Heap Scan with Low Correlation",
                        symptom = "Bitmap scan with many heap fetches",
                        fix = new[]
                        {
                            "CLUSTER table USING index_name; -- Physical reordering",
                            "VACUUM table; -- Cleanup"
                        }
                    }
                }
            },
            optimizationWorkflow = @"
1. Run EXPLAIN ANALYZE query
   psql -c ""EXPLAIN (ANALYZE, BUFFERS) SELECT ...""

2. Identify bottlenecks:
   - Look for Seq Scan on large tables -> add index
   - Check for high actual rows vs estimated -> run ANALYZE
   - Find slow operations (high actual time) -> optimize

3. Test fix:
   - Create index: CREATE INDEX CONCURRENTLY ...
   - Re-run EXPLAIN ANALYZE
   - Compare execution times

4. Monitor:
   - Use pg_stat_statements for query statistics
   - Track slow queries over time
   - Adjust indexes based on usage patterns",

            tooling = new[]
            {
                new { tool = "pgAdmin", feature = "Visual EXPLAIN plan viewer" },
                new { tool = "pg_stat_statements", feature = "Query statistics extension" },
                new { tool = "EXPLAIN (ANALYZE, BUFFERS)", feature = "Detailed execution stats" },
                new { tool = "auto_explain", feature = "Automatically log slow queries" }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Suggests optimal index strategy based on query patterns")]
    public string SuggestIndexStrategy(
        [Description("Table info as JSON")] string tableInfo = "{\"tableName\":\"example\",\"columns\":[{\"name\":\"geom\",\"type\":\"geometry\"}]}",
        [Description("Query patterns as JSON array")] string queryPatterns = "[]")
    {
        return JsonSerializer.Serialize(new
        {
            indexStrategies = new[]
            {
                new
                {
                    indexType = "B-Tree Index",
                    useCase = "Equality and range queries on scalar columns",
                    syntax = "CREATE INDEX idx_name ON table(column);",
                    examples = new[]
                    {
                        "CREATE INDEX idx_city ON buildings(city); -- For WHERE city = 'San Francisco'",
                        "CREATE INDEX idx_height ON buildings(height); -- For WHERE height > 100",
                        "CREATE INDEX idx_city_height ON buildings(city, height); -- Composite index"
                    },
                    when = "Use for most attribute queries (=, <, >, <=, >=, BETWEEN)",
                    benefits = (string[]?)null
                },
                new
                {
                    indexType = "GiST Index (Spatial)",
                    useCase = "Spatial queries (intersects, contains, within)",
                    syntax = "CREATE INDEX idx_geom ON table USING GIST(geom);",
                    examples = new[]
                    {
                        "CREATE INDEX idx_geom ON buildings USING GIST(geom);",
                        "CREATE INDEX CONCURRENTLY idx_geom ON buildings USING GIST(geom); -- No downtime"
                    },
                    when = "Required for ST_Intersects, ST_Contains, ST_Within, bbox queries",
                    benefits = (string[]?)null
                },
                new
                {
                    indexType = "GIN Index (Full-Text)",
                    useCase = "Full-text search, array containment",
                    syntax = "CREATE INDEX idx_fts ON table USING GIN(to_tsvector('english', column));",
                    examples = new[]
                    {
                        "CREATE INDEX idx_name_fts ON buildings USING GIN(to_tsvector('english', name));",
                        "CREATE INDEX idx_tags ON buildings USING GIN(tags); -- For array column"
                    },
                    when = "Use for full-text search or array @> queries",
                    benefits = (string[]?)null
                },
                new
                {
                    indexType = "Partial Index",
                    useCase = "Index subset of rows that match frequent queries",
                    syntax = "CREATE INDEX idx_name ON table(column) WHERE condition;",
                    examples = new[]
                    {
                        "CREATE INDEX idx_active_buildings ON buildings(id) WHERE active = true;",
                        "CREATE INDEX idx_tall_buildings ON buildings(height) WHERE height > 100;"
                    },
                    when = "When queries consistently filter on specific condition",
                    benefits = (string[]?)new[] { "Smaller index size", "Faster updates", "Better for specific queries" }
                },
                new
                {
                    indexType = "Expression Index",
                    useCase = "Index computed values used in queries",
                    syntax = "CREATE INDEX idx_name ON table((expression));",
                    examples = new[]
                    {
                        "CREATE INDEX idx_lower_name ON buildings((LOWER(name)));",
                        "CREATE INDEX idx_year ON buildings((EXTRACT(YEAR FROM built_date)));"
                    },
                    when = "When queries use functions or expressions consistently",
                    benefits = (string[]?)null
                },
                new
                {
                    indexType = "Covering Index (INCLUDE)",
                    useCase = "Include non-key columns for index-only scans",
                    syntax = "CREATE INDEX idx_name ON table(key_col) INCLUDE (other_cols);",
                    examples = new[]
                    {
                        "CREATE INDEX idx_city_inc ON buildings(city) INCLUDE (name, height);",
                        "-- Query can be satisfied from index alone, no table access needed"
                    },
                    when = "Frequently queried columns that aren't in WHERE clause (PostgreSQL 11+)",
                    benefits = (string[]?)null
                }
            },
            indexingBestPractices = new[]
            {
                new { practice = "Primary Key Index", recommendation = "Always have PRIMARY KEY - creates automatic B-tree index" },
                new { practice = "Foreign Key Index", recommendation = "Index foreign key columns for join performance" },
                new { practice = "Composite Indexes", recommendation = "Put most selective column first: (selective_col, other_col)" },
                new { practice = "Spatial Index", recommendation = "ALWAYS create GIST index on geometry columns" },
                new { practice = "Concurrent Creation", recommendation = "Use CREATE INDEX CONCURRENTLY for zero-downtime on production" },
                new { practice = "Index Maintenance", recommendation = "REINDEX or VACUUM regularly to maintain performance" },
                new { practice = "Monitor Usage", recommendation = "Use pg_stat_user_indexes to find unused indexes" }
            },
            indexMaintenanceQueries = new
            {
                findUnusedIndexes = @"
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE idx_scan = 0
    AND schemaname = 'public'
ORDER BY pg_relation_size(indexrelid) DESC;",

                findDuplicateIndexes = @"
SELECT
    pg_size_pretty(SUM(pg_relation_size(idx))::BIGINT) AS total_size,
    (array_agg(idx))[1] AS idx1,
    (array_agg(idx))[2] AS idx2,
    (array_agg(idx))[3] AS idx3,
    (array_agg(idx))[4] AS idx4
FROM (
    SELECT
        indexrelid::regclass AS idx,
        (indrelid::text || E'\n' || indclass::text || E'\n' || indkey::text || E'\n' ||
         COALESCE(indexprs::text, '') || E'\n' || COALESCE(indpred::text, '')) AS key
    FROM pg_index
) sub
GROUP BY key
HAVING COUNT(*) > 1
ORDER BY SUM(pg_relation_size(idx)) DESC;",

                indexBloat = @"
SELECT
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
    pg_size_pretty(bloat_size) AS bloat_size,
    ROUND(bloat_ratio * 100, 2) AS bloat_ratio_pct
FROM (
    SELECT
        schemaname,
        tablename,
        indexname,
        indexrelid,
        GREATEST(0, pg_relation_size(indexrelid) - (current_setting('block_size')::INTEGER * relpages)) AS bloat_size,
        CASE
            WHEN pg_relation_size(indexrelid) = 0 THEN 0
            ELSE GREATEST(0, pg_relation_size(indexrelid) - (current_setting('block_size')::INTEGER * relpages))::NUMERIC / pg_relation_size(indexrelid)
        END AS bloat_ratio
    FROM pg_stat_user_indexes
    JOIN pg_class ON pg_class.oid = indexrelid
) bloat
WHERE bloat_ratio > 0.3
ORDER BY bloat_size DESC;"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Optimizes vector tile configuration")]
    public string OptimizeVectorTiles(
        [Description("Tile configuration as JSON")] string tileConfig = "{\"minZoom\":0,\"maxZoom\":14,\"format\":\"mvt\"}")
    {
        return JsonSerializer.Serialize(new
        {
            mvtOptimizations = new object[]
            {
                new
                {
                    aspect = "Simplification",
                    description = "Reduce geometry complexity based on zoom level",
                    implementation = @"
-- Dynamic simplification by zoom level
ST_AsMVTGeom(
    ST_Transform(
        CASE
            WHEN $1 <= 10 THEN ST_SimplifyPreserveTopology(geom, 0.001)
            WHEN $1 <= 14 THEN ST_SimplifyPreserveTopology(geom, 0.0001)
            ELSE geom
        END,
        3857
    ),
    ST_TileEnvelope($1, $2, $3),
    4096,
    256,
    true
) AS geom",
                    benefit = "Smaller tile sizes, faster rendering, reduced bandwidth",
                    importance = (string?)null
                },
                new
                {
                    aspect = "Attribute Filtering",
                    description = "Include only necessary properties per zoom level",
                    implementation = @"
SELECT
    id,
    CASE
        WHEN $1 <= 10 THEN jsonb_build_object('name', name)
        WHEN $1 <= 14 THEN jsonb_build_object('name', name, 'type', type)
        ELSE jsonb_build_object('name', name, 'type', type, 'height', height, 'year', year)
    END AS properties,
    ST_AsMVTGeom(...) AS geom",
                    benefit = "Reduces tile size by 30-50%",
                    importance = (string?)null
                },
                new
                {
                    aspect = "Clipping",
                    description = "Clip geometries to tile boundaries",
                    implementation = "ST_AsMVTGeom with clip_geom = true",
                    importance = "Prevents rendering artifacts at tile boundaries",
                    benefit = (string?)null
                },
                new
                {
                    aspect = "Feature Filtering",
                    description = "Exclude small features at low zoom levels",
                    implementation = @"
WHERE
    CASE
        WHEN $1 <= 10 THEN ST_Area(geom::geography) > 1000000 -- > 1 km²
        WHEN $1 <= 14 THEN ST_Area(geom::geography) > 10000   -- > 1 hectare
        ELSE true
    END",
                    benefit = "Fewer features = smaller tiles, faster rendering",
                    importance = (string?)null
                },
                new
                {
                    aspect = "Overzooming",
                    description = "Generate tiles up to certain zoom, then overzoom client-side",
                    implementation = "Max zoom 14 in database, zoom 15-22 client-side",
                    benefit = "Reduced tile generation and storage, acceptable for most use cases",
                    importance = (string?)null
                }
            },
            cachingStrategy = new
            {
                approaches = new object[]
                {
                    new
                    {
                        strategy = "Pre-generation",
                        description = "Generate tiles for common zoom levels ahead of time",
                        zoomLevels = "0-10 (global to city level)",
                        command = "honua tiles pregenerate --collection buildings --zoom 0-10",
                        storage = "Store in filesystem or S3",
                        cacheBackend = (string?)null,
                        ttl = (string?)null,
                        implementation = (string?)null,
                        bestPractice = (string?)null
                    },
                    new
                    {
                        strategy = "On-Demand with Cache",
                        description = "Generate on first request, cache for future requests",
                        cacheBackend = "Redis, Filesystem, or CDN",
                        ttl = "7-30 days for static data, 1 hour for dynamic",
                        zoomLevels = (string?)null,
                        command = (string?)null,
                        storage = (string?)null,
                        implementation = (string?)null,
                        bestPractice = (string?)null
                    },
                    new
                    {
                        strategy = "Hybrid",
                        description = "Pre-generate popular tiles, on-demand for rest",
                        implementation = "Pre-gen zoom 0-10, on-demand 11-18",
                        bestPractice = "Monitor hit rates to adjust pre-generation levels",
                        zoomLevels = (string?)null,
                        command = (string?)null,
                        storage = (string?)null,
                        cacheBackend = (string?)null,
                        ttl = (string?)null
                    }
                },
                cacheHeaders = @"
Cache-Control: public, max-age=604800
ETag: ""abc123def456""
Vary: Accept-Encoding"
            },
            compressionOptimization = new
            {
                gzip = new
                {
                    savings = "60-80% size reduction",
                    configuration = "Enable gzip in web server (nginx, Apache)",
                    clientSupport = "Universal browser support"
                },
                brotli = new
                {
                    savings = "70-85% size reduction (better than gzip)",
                    configuration = "Enable Brotli in modern web servers",
                    clientSupport = "Modern browsers only"
                },
                recommendation = "Support both, serve Brotli to modern clients, gzip as fallback"
            },
            performanceMetrics = new
            {
                targetMetrics = new[]
                {
                    new { metric = "Tile generation time", target = "< 100ms per tile" },
                    new { metric = "Tile size (gzipped)", target = "< 500KB per tile" },
                    new { metric = "Feature count", target = "< 10,000 features per tile at zoom 14" },
                    new { metric = "Cache hit rate", target = "> 90%" }
                },
                monitoring = @"
-- Monitor tile generation performance
SELECT
    collection_id,
    zoom_level,
    AVG(generation_time_ms) AS avg_time,
    AVG(tile_size_bytes) AS avg_size,
    AVG(feature_count) AS avg_features
FROM tile_metrics
WHERE created_at > NOW() - INTERVAL '24 hours'
GROUP BY collection_id, zoom_level
ORDER BY avg_time DESC;"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Recommends scaling strategy based on traffic profile")]
    public string RecommendScalingStrategy(
        [Description("Traffic profile as JSON")] string trafficProfile = "{\"peakRequestsPerSecond\":100,\"averageRequestsPerSecond\":50}",
        [Description("Current resources as JSON")] string currentResources = "{\"cpuCores\":4,\"memoryGb\":16}")
    {
        return JsonSerializer.Serialize(new
        {
            scalingDimensions = new object[]
            {
                new
                {
                    dimension = "Horizontal Scaling (More Instances)",
                    when = new[] { "CPU usage consistently > 70%", "Request queue building up", "Increasing traffic" },
                    approaches = new[]
                    {
                        new { method = "Kubernetes HPA", trigger = "CPU/Memory utilization", scaling = "Pods: 3-10", tools = (string[]?)null, configuration = (string?)null, approach = (string?)null },
                        new { method = "AWS Auto Scaling", trigger = "CloudWatch metrics", scaling = "EC2 instances or ECS tasks", tools = (string[]?)null, configuration = (string?)null, approach = (string?)null },
                        new { method = "Azure VMSS", trigger = "Platform metrics", scaling = "Virtual machines", tools = (string[]?)null, configuration = (string?)null, approach = (string?)null }
                    },
                    implementation = @"
# Kubernetes HPA
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70"
                },
                new
                {
                    dimension = "Vertical Scaling (Bigger Instances)",
                    when = new[] { "Single-threaded bottlenecks", "Memory-intensive operations", "Large query results" },
                    approaches = new[]
                    {
                        "Increase instance size: t3.medium -> t3.large -> t3.xlarge",
                        "Add memory: 2GB -> 4GB -> 8GB",
                        "Increase CPU cores: 2 cores -> 4 cores -> 8 cores"
                    },
                    considerations = new[]
                    {
                        "Requires restart/downtime (unless using blue-green deployment)",
                        "Cost increases linearly or exponentially",
                        "May hit platform limits (max instance size)"
                    }
                },
                new
                {
                    dimension = "Database Scaling",
                    strategies = new object[]
                    {
                        new
                        {
                            strategy = "Read Replicas",
                            useCase = "Read-heavy workloads (OGC API is mostly read)",
                            implementation = new[]
                            {
                                "Create read replicas for GET requests",
                                "Route writes to primary, reads to replicas",
                                "Use connection pooler (PgBouncer) for replica distribution"
                            },
                            tools = (string[]?)null,
                            configuration = (string?)null,
                            approach = (string?)null
                        },
                        new
                        {
                            strategy = "Connection Pooling",
                            useCase = "High connection overhead",
                            tools = new[] { "PgBouncer", "RDS Proxy", "Azure DB Proxy" },
                            configuration = "MaxPoolSize: 100-200, MinPoolSize: 10-20",
                            implementation = (string[]?)null,
                            approach = (string?)null
                        },
                        new
                        {
                            strategy = "Vertical Scaling",
                            useCase = "CPU or memory bound queries",
                            approach = "Increase RDS instance size: db.t3.medium -> db.r5.large",
                            implementation = (string[]?)null,
                            tools = (string[]?)null,
                            configuration = (string?)null
                        },
                        new
                        {
                            strategy = "Partitioning",
                            useCase = "Very large tables (> 100M rows)",
                            implementation = new[]
                            {
                                "Partition by spatial region (grid-based)",
                                "Partition by time (for temporal data)",
                                "Use declarative partitioning (PostgreSQL 10+)"
                            }
                        }
                    }
                },
                new
                {
                    dimension = "Caching Layer",
                    strategies = new object[]
                    {
                        new
                        {
                            cache = "Redis Cache",
                            cacheables = new[] { "Query results", "Rendered tiles", "Metadata" },
                            ttl = "Static data: 24h, Dynamic: 5min",
                            eviction = "LRU (Least Recently Used)",
                            benefit = (string?)null,
                            mechanism = (string?)null,
                            implementation = (string?)null
                        },
                        new
                        {
                            cache = "CDN (CloudFront/CloudFlare)",
                            cacheables = new[] { "Vector tiles", "Raster tiles", "Static assets" },
                            ttl = "Tiles: 7 days, API responses: 1 hour",
                            benefit = "90% reduction in origin requests",
                            eviction = (string?)null,
                            mechanism = (string?)null,
                            implementation = (string?)null
                        },
                        new
                        {
                            cache = "HTTP Response Cache",
                            mechanism = "Cache-Control headers",
                            implementation = "Add Cache-Control: public, max-age=3600 to responses",
                            cacheables = (string[]?)null,
                            ttl = (string?)null,
                            eviction = (string?)null,
                            benefit = (string?)null
                        }
                    }
                }
            },
            decisionMatrix = new
            {
                scenarios = new[]
                {
                    new
                    {
                        scenario = "Steady growth (20% monthly)",
                        recommendation = "Gradual horizontal scaling + caching",
                        actions = new[] { "Add instances proactively", "Implement Redis caching", "Enable CDN" }
                    },
                    new
                    {
                        scenario = "Spiky traffic (2-10x during events)",
                        recommendation = "Auto-scaling + aggressive caching",
                        actions = new[] { "Configure HPA with aggressive scaling", "Pre-warm cache before events", "Use CDN edge caching" }
                    },
                    new
                    {
                        scenario = "Slow queries despite scaling",
                        recommendation = "Database optimization",
                        actions = new[] { "Add missing indexes", "Optimize query patterns", "Consider read replicas", "Increase database resources" }
                    },
                    new
                    {
                        scenario = "Global users, high latency",
                        recommendation = "Geographic distribution",
                        actions = new[] { "Deploy to multiple regions", "Use global CDN", "Implement geo-routing", "Consider edge computing" }
                    }
                }
            },
            costOptimization = new[]
            {
                "Use spot instances / preemptible VMs for non-critical workloads (60-80% cost savings)",
                "Right-size instances based on actual usage (turn off auto-scaling temporarily to measure)",
                "Use reserved instances / savings plans for steady-state capacity",
                "Implement aggressive caching to reduce compute needs",
                "Set scale-down policies to reduce resources during off-peak hours"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Estimates resource needs based on data volume and load")]
    public string EstimateResourceNeeds(
        [Description("Data volume as JSON (feature count, geometry complexity)")] string dataVolume,
        [Description("Expected load as JSON (requests/sec, concurrent users)")] string expectedLoad)
    {
        return JsonSerializer.Serialize(new
        {
            calculationMethod = new
            {
                formula = "Resources = f(data_size, request_rate, query_complexity, cache_hit_rate)",
                factors = new object[]
                {
                    "Data size (storage and memory)",
                    "Request rate (CPU and network)",
                    "Query complexity (CPU and I/O)",
                    "Cache efficiency (reduces all resources)"
                }
            },
            estimationWorksheet = new
            {
                databaseResources = new
                {
                    storage = new
                    {
                        calculation = "features × avg_feature_size × 1.5 (indexes and overhead)",
                        example = "1M features × 2KB × 1.5 = 3GB storage",
                        recommendation = "Provision 2x calculated for growth"
                    },
                    memory = new
                    {
                        calculation = "shared_buffers (25% RAM) + work_mem (per connection) + OS cache",
                        example = "4GB shared_buffers + (256MB × 50 connections) + 4GB OS = 20GB RAM",
                        recommendation = "Minimum 8GB for production, 16-32GB for high performance"
                    },
                    cpu = new
                    {
                        calculation = "Based on query complexity and concurrency",
                        guideline = new[]
                        {
                            "Light queries (indexed lookups): 2-4 cores",
                            "Medium queries (spatial operations): 4-8 cores",
                            "Heavy queries (complex spatial analysis): 8-16 cores"
                        },
                        example = (string?)null,
                        recommendation = (string?)null
                    },
                    iops = new
                    {
                        calculation = "request_rate × avg_disk_seeks_per_query",
                        example = "100 req/s × 10 seeks = 1000 IOPS",
                        recommendation = "Provision SSD with 3000-5000 IOPS minimum"
                    }
                },
                applicationResources = new
                {
                    instances = new
                    {
                        calculation = "request_rate ÷ requests_per_instance",
                        example = "1000 req/s ÷ 200 req/s/instance = 5 instances",
                        recommendation = "Add 50% buffer: 5 × 1.5 = 8 instances"
                    },
                    memory_per_instance = new
                    {
                        baseline = "512MB minimum for .NET runtime",
                        per_request = "1-5MB depending on response size",
                        recommendation = "1-2GB per instance for typical workloads"
                    },
                    cpu_per_instance = new
                    {
                        guideline = "1 CPU core per 100-200 req/s",
                        recommendation = "2-4 cores per instance for production"
                    }
                },
                networkBandwidth = new
                {
                    calculation = "request_rate × avg_response_size",
                    example = "100 req/s × 50KB = 5 MB/s = 40 Mbps",
                    recommendation = "Provision 2-3x calculated for bursts"
                }
            },
            sampleConfigurations = new object[]
            {
                new
                {
                    scale = "Small (< 100 req/s, < 1M features)",
                    database = "PostgreSQL: 2 vCPU, 8GB RAM, 100GB SSD",
                    application = "3 instances: 1 vCPU, 1GB RAM each",
                    caching = "Optional: Redis 1GB",
                    monthlyCost = "$200-400 (AWS/Azure/GCP)",
                    loadBalancer = (string?)null,
                    cdn = (string?)null,
                    architecture = (string?)null
                },
                new
                {
                    scale = "Medium (100-500 req/s, 1-10M features)",
                    database = "PostgreSQL: 4 vCPU, 16GB RAM, 500GB SSD",
                    application = "5-10 instances: 2 vCPU, 2GB RAM each",
                    caching = "Redis 5GB cluster",
                    monthlyCost = "$800-1500",
                    loadBalancer = (string?)null,
                    cdn = (string?)null,
                    architecture = (string?)null
                },
                new
                {
                    scale = "Large (500-2000 req/s, 10-100M features)",
                    database = "PostgreSQL: 8 vCPU, 32GB RAM, 1TB SSD + read replicas",
                    application = "10-20 instances: 4 vCPU, 4GB RAM each",
                    caching = "Redis 20GB cluster",
                    loadBalancer = "Application Load Balancer",
                    monthlyCost = "$3000-6000",
                    cdn = (string?)null,
                    architecture = (string?)null
                },
                new
                {
                    scale = "X-Large (> 2000 req/s, > 100M features)",
                    database = "PostgreSQL: 16+ vCPU, 64GB+ RAM, multi-TB SSD + multiple read replicas",
                    application = "20-50 instances with auto-scaling",
                    caching = "Redis cluster 50GB+ with sharding",
                    cdn = "CloudFront/CloudFlare with edge caching",
                    architecture = "Multi-region deployment",
                    monthlyCost = "$10000+",
                    loadBalancer = (string?)null
                }
            },
            validationSteps = new[]
            {
                "1. Load test with synthetic data matching production volume",
                "2. Measure actual resource utilization under load",
                "3. Identify bottlenecks (CPU, memory, I/O, network)",
                "4. Adjust resources based on metrics",
                "5. Plan for 2x growth over next 12 months"
            },
            tools = new[]
            {
                new { tool = "k6", usage = (string?)"Load testing to measure resource needs", urls = (string[]?)null },
                new { tool = "pgbench", usage = (string?)"PostgreSQL performance testing", urls = (string[]?)null },
                new { tool = "CloudWatch/Prometheus", usage = (string?)"Real-time resource monitoring", urls = (string[]?)null },
                new { tool = "Cost calculators", usage = (string?)null, urls = (string[]?)new[] { "AWS Pricing Calculator", "Azure Pricing Calculator", "GCP Pricing Calculator" } }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Suggests spatial query optimizations")]
    public string SuggestSpatialOptimizations(
        [Description("Geometry type (Point, LineString, Polygon)")] string geometryType = "Polygon",
        [Description("Average feature count")] int featureCount = 10000,
        [Description("Whether spatial index exists")] bool hasSpatialIndex = false)
    {
        var suggestions = new System.Collections.Generic.List<object>();

        if (!hasSpatialIndex && featureCount > 1000)
        {
            suggestions.Add(new
            {
                priority = "critical",
                optimization = "Create spatial index",
                reason = $"With {featureCount:N0} features, queries will be extremely slow without a spatial index",
                estimatedImprovement = "85-95% faster spatial queries",
                implementation = "CREATE INDEX idx_geom_gist ON table_name USING GIST (geometry_column)"
            });
        }

        if (geometryType.Equals("Polygon", StringComparison.OrdinalIgnoreCase) && featureCount > 10000)
        {
            suggestions.Add(new
            {
                priority = "high",
                optimization = "Geometry simplification",
                reason = "Large polygon datasets benefit from multi-resolution storage",
                estimatedImprovement = "50-70% faster rendering at small scales",
                implementation = "Store simplified geometries in separate column for overview queries"
            });
        }

        if (featureCount > 100000)
        {
            suggestions.Add(new
            {
                priority = "medium",
                optimization = "Spatial clustering",
                reason = "Large datasets benefit from spatial clustering for better disk locality",
                estimatedImprovement = "30-50% faster range queries",
                implementation = "CLUSTER table_name USING idx_geom_gist"
            });
        }

        return JsonSerializer.Serialize(new
        {
            geometryType,
            featureCount,
            hasSpatialIndex,
            suggestionCount = suggestions.Count,
            suggestions
        }, CliJsonOptions.Indented);
    }
}
