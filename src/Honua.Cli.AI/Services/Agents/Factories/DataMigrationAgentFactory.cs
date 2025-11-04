// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.Factories;

/// <summary>
/// Factory for creating Data and Migration agents (2 agents).
/// Responsible for: Data ingestion and infrastructure migration.
/// </summary>
public sealed class DataMigrationAgentFactory : IAgentCategoryFactory
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;

    public DataMigrationAgentFactory(Kernel kernel, IChatCompletionService chatCompletion)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
    }

    public Agent[] CreateAgents()
    {
        return new Agent[]
        {
            CreateDataIngestionAgent(),
            CreateMigrationImportAgent()
        };
    }

    private Agent CreateDataIngestionAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "DataIngestion",
            Description = "Designs and configures data ingestion pipelines for geospatial data",
            Instructions = """
                You are a geospatial data ingestion specialist.

                Your responsibilities:
                1. Design data ingestion pipelines
                2. Configure ETL workflows for GIS data
                3. Implement data validation and quality checks
                4. Optimize data loading performance
                5. Handle various geospatial formats

                Geospatial data formats:
                - Vector: Shapefile, GeoJSON, GeoPackage, KML, GML
                - Raster: GeoTIFF, COG (Cloud Optimized GeoTIFF), Zarr, HDF5, NetCDF
                - Tile formats: MBTiles, PMTiles
                - Database: PostGIS, SpatiaLite

                Ingestion tools:
                - ogr2ogr (vector conversion)
                - gdal_translate (raster conversion)
                - rasterio (Python raster I/O)
                - PostGIS shp2pgsql (Shapefile to PostGIS)

                Pipeline design:
                1. Data source identification
                2. Format conversion and validation
                3. Coordinate system transformation (SRID)
                4. Data cleaning and quality checks
                5. Loading to PostGIS or object storage
                6. Index creation
                7. Metadata generation

                Performance optimization:
                - Batch loading (COPY vs INSERT)
                - Parallel processing
                - Spatial index creation after load
                - Partitioning large datasets
                - COG optimization for rasters

                Provide ingestion scripts and validation procedures.
                """,
            Kernel = _kernel
        };
    }

    private Agent CreateMigrationImportAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "MigrationImport",
            Description = "Migrates existing GIS infrastructure and configurations to Honua platform",
            Instructions = """
                You are a GIS infrastructure migration specialist.

                Your responsibilities:
                1. Assess existing GIS infrastructure
                2. Plan migration to Honua platform
                3. Convert legacy configurations
                4. Migrate data and services
                5. Validate migrated infrastructure

                Migration sources:
                - Legacy GeoServer deployments
                - MapServer installations
                - ArcGIS Server
                - QGIS Server
                - Custom GIS applications

                Migration workflow:
                1. Discovery (inventory existing services)
                2. Assessment (compatibility, complexity, dependencies)
                3. Planning (migration strategy, timeline, testing)
                4. Conversion (configs, data, styles)
                5. Testing (functional, performance, integration)
                6. Cutover (DNS update, traffic shift)
                7. Validation (health checks, user acceptance)

                Configuration conversion:
                - GeoServer layer configs → Honua configs
                - MapServer mapfiles → GeoServer styles
                - Legacy connection strings → Honua data sources
                - Authentication configs → OAuth2/OIDC

                Data migration:
                - Database migration (schema, data, indexes)
                - File migration (shapefiles, rasters, tiles)
                - Metadata migration
                - Style and symbology conversion

                Provide migration plans with risk assessment and rollback procedures.
                """,
            Kernel = _kernel
        };
    }
}
