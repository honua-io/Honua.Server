// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Migration;

/// <summary>
/// Service for generating complete migration scripts with GDAL commands.
/// </summary>
public sealed class MigrationScriptGenerator
{
    /// <summary>
    /// Generates complete migration script with GDAL commands.
    /// </summary>
    /// <param name="serviceUrl">ArcGIS service URL</param>
    /// <param name="options">Migration options as JSON (target database, layer mapping, transformations)</param>
    /// <returns>JSON script package with main script and utilities</returns>
    public string GenerateScript(string serviceUrl, string options)
    {
        var script = new
        {
            scriptType = "Bash",
            description = "Complete ArcGIS to Honua migration script",
            script = GenerateMainMigrationScript(serviceUrl),
            additionalScripts = new
            {
                incrementalSync = GenerateIncrementalSyncScript(),
                rollback = GenerateRollbackScript(),
                validation = GenerateValidationScript()
            },
            prerequisites = new[]
            {
                "GDAL/OGR 3.x with ArcGIS REST driver",
                "PostgreSQL client (psql)",
                "jq for JSON parsing",
                "Set HONUA_DB_PASSWORD environment variable",
                "Network access to ArcGIS service and Honua database"
            },
            usage = new
            {
                basicMigration = "chmod +x migrate.sh && ./migrate.sh",
                dryRun = "Add -noop flag to ogr2ogr for testing",
                specificLayers = "Modify script to migrate only specific layer indices",
                monitoring = "tail -f migration_*.log in separate terminal"
            }
        };

        return JsonSerializer.Serialize(script, CliJsonOptions.Indented);
    }

    private static string GenerateMainMigrationScript(string serviceUrl)
    {
        return $@"#!/bin/bash
# ArcGIS to Honua Migration Script
# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}
# Source: {serviceUrl}

set -e  # Exit on error
set -u  # Exit on undefined variable

# Configuration
ARCGIS_URL=""{serviceUrl}""
HONUA_DB=""host=localhost dbname=honua user=honua_user password=$HONUA_DB_PASSWORD""
LOG_FILE=""migration_$(date +%Y%m%d_%H%M%S).log""

echo ""Starting migration at $(date)"" | tee -a $LOG_FILE

# Phase 1: Service Analysis
echo ""Phase 1: Analyzing ArcGIS service..."" | tee -a $LOG_FILE
ogrinfo -al -so ""${{ARCGIS_URL}}"" >> $LOG_FILE 2>&1

# Phase 2: Layer Enumeration
echo ""Phase 2: Enumerating layers..."" | tee -a $LOG_FILE
LAYER_COUNT=$(ogrinfo ""${{ARCGIS_URL}}"" | grep -c '1:' || true)
echo ""Found $LAYER_COUNT layers"" | tee -a $LOG_FILE

# Phase 3: Data Migration
echo ""Phase 3: Migrating data..."" | tee -a $LOG_FILE

# Migrate each layer
for i in $(seq 0 $((LAYER_COUNT - 1))); do
    echo ""Migrating layer $i..."" | tee -a $LOG_FILE

    # Get layer name
    LAYER_NAME=$(ogrinfo ""${{ARCGIS_URL}}"" | grep ""$i:"" | awk '{{print $2}}')

    # Extract and load data
    ogr2ogr -f PostgreSQL \
        PG:""${{HONUA_DB}}"" \
        ""${{ARCGIS_URL}}"" \
        -nln ""${{LAYER_NAME}}"" \
        -lco GEOMETRY_NAME=geom \
        -lco FID=id \
        -lco SPATIAL_INDEX=GIST \
        -t_srs EPSG:4326 \
        -progress \
        -skipfailures \
        ""${{LAYER_NAME}}"" 2>&1 | tee -a $LOG_FILE

    # Verify migration
    COUNT=$(psql -h localhost -U honua_user -d honua -tAc ""SELECT COUNT(*) FROM \""${{LAYER_NAME}}\"""")
    echo ""Layer ${{LAYER_NAME}}: $COUNT features migrated"" | tee -a $LOG_FILE
done

# Phase 4: Post-Migration Optimization
echo ""Phase 4: Post-migration optimization..."" | tee -a $LOG_FILE

psql -h localhost -U honua_user -d honua <<EOF
-- Create spatial indexes (if not created by ogr2ogr)
DO \$\$
DECLARE
    r RECORD;
BEGIN
    FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'public'
    LOOP
        EXECUTE format('CREATE INDEX IF NOT EXISTS idx_%I_geom ON %I USING GIST(geom)', r.tablename, r.tablename);
    END LOOP;
END \$\$;

-- Update statistics
VACUUM ANALYZE;

-- Validate geometries
DO \$\$
DECLARE
    r RECORD;
    invalid_count INTEGER;
BEGIN
    FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'public'
    LOOP
        EXECUTE format('SELECT COUNT(*) FROM %I WHERE NOT ST_IsValid(geom)', r.tablename) INTO invalid_count;
        IF invalid_count > 0 THEN
            RAISE NOTICE 'Table % has % invalid geometries', r.tablename, invalid_count;
        END IF;
    END LOOP;
END \$\$;
EOF

# Phase 5: Generate Honua Metadata
echo ""Phase 5: Generating Honua metadata..."" | tee -a $LOG_FILE

cat > metadata.yaml <<'YAML'
collections:
$(psql -h localhost -U honua_user -d honua -tA <<SQL
SELECT format('  - id: %s
    title: %s
    description: Migrated from ArcGIS service
    itemType: feature
    crs:
      - http://www.opengis.net/def/crs/OGC/1.3/CRS84
      - http://www.opengis.net/def/crs/EPSG/0/4326
    extent:
      spatial:
        bbox:
          - [%s]
        crs: http://www.opengis.net/def/crs/OGC/1.3/CRS84
',
    tablename,
    INITCAP(REPLACE(tablename, '_', ' ')),
    ARRAY_TO_STRING(ARRAY[
        ST_XMin(extent)::text, ST_YMin(extent)::text,
        ST_XMax(extent)::text, ST_YMax(extent)::text
    ], ', ')
)
FROM (
    SELECT
        tablename,
        ST_Extent(geom) AS extent
    FROM pg_tables t
    JOIN LATERAL (SELECT geom FROM pg_tables WHERE schemaname = 'public' LIMIT 1) g ON true
    WHERE schemaname = 'public'
    GROUP BY tablename
) sub;
SQL
)
YAML

echo ""Migration completed at $(date)"" | tee -a $LOG_FILE
echo ""Check $LOG_FILE for details""
echo ""Review metadata.yaml and customize as needed""
echo ""Start Honua server with: honua serve --metadata metadata.yaml""
";
    }

    private static string GenerateIncrementalSyncScript()
    {
        return @"#!/bin/bash
# Incremental sync for layers with last_modified field
LAST_SYNC=$(cat last_sync.txt || echo '2000-01-01')
ogr2ogr -append -update \
    PG:""$HONUA_DB"" \
    ""$ARCGIS_URL"" \
    -sql ""SELECT * FROM layer WHERE last_modified > timestamp '$LAST_SYNC'""
date -Iseconds > last_sync.txt";
    }

    private static string GenerateRollbackScript()
    {
        return @"#!/bin/bash
# Rollback script - drop migrated tables
psql -h localhost -U honua_user -d honua <<SQL
DO \$\$
DECLARE
    r RECORD;
BEGIN
    FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'public'
    LOOP
        EXECUTE format('DROP TABLE IF EXISTS %I CASCADE', r.tablename);
    END LOOP;
END \$\$;
SQL";
    }

    private static string GenerateValidationScript()
    {
        return @"#!/bin/bash
# Validation script - compare record counts
echo ""Layer,ArcGIS Count,Honua Count,Match""
for layer in $(psql -h localhost -U honua_user -d honua -tAc ""SELECT tablename FROM pg_tables WHERE schemaname = 'public'""); do
    ARCGIS_COUNT=$(curl -s ""$ARCGIS_URL/$layer/query?where=1=1&returnCountOnly=true&f=json"" | jq .count)
    HONUA_COUNT=$(psql -h localhost -U honua_user -d honua -tAc ""SELECT COUNT(*) FROM \""$layer\"""")
    MATCH=$([ ""$ARCGIS_COUNT"" -eq ""$HONUA_COUNT"" ] && echo ""✓"" || echo ""✗"")
    echo ""$layer,$ARCGIS_COUNT,$HONUA_COUNT,$MATCH""
done";
    }
}
