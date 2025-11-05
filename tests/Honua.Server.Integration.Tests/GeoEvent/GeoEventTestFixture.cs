// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoEvent;

/// <summary>
/// Test fixture for GeoEvent API integration tests.
/// Provides a PostgreSQL container with PostGIS and configures the test server.
/// </summary>
public class GeoEventTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        // Start PostgreSQL with PostGIS
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase("geoevent_test")
            .WithUsername("postgres")
            .WithPassword("testpass")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        var host = _postgresContainer.Hostname;
        var port = _postgresContainer.GetMappedPublicPort(5432);
        _connectionString = $"Host={host};Port={port};Database=geoevent_test;Username=postgres;Password=testpass";

        // Create schema
        await CreateSchemaAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for tests
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _connectionString,
                ["honua:authentication:mode"] = "Local",
                ["honua:authentication:enforce"] = "false", // Disable auth for tests
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = "../../samples/metadata/sample-metadata.json"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Additional test-specific service configuration if needed
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.UseEnvironment("Test");
    }

    private async Task CreateSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Enable PostGIS
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS postgis;");

        // Create geofences table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS geofences (
                id UUID PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                geometry geometry(Polygon, 4326) NOT NULL,
                properties JSONB,
                enabled_event_types INT NOT NULL DEFAULT 3,
                is_active BOOLEAN NOT NULL DEFAULT true,
                tenant_id VARCHAR(100),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                created_by VARCHAR(255),
                updated_by VARCHAR(255)
            );

            CREATE INDEX IF NOT EXISTS idx_geofences_geometry ON geofences USING GIST(geometry);
            CREATE INDEX IF NOT EXISTS idx_geofences_tenant ON geofences(tenant_id);
            CREATE INDEX IF NOT EXISTS idx_geofences_active ON geofences(is_active);
        ");

        // Create entity_geofence_state table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS entity_geofence_state (
                entity_id VARCHAR(255) NOT NULL,
                geofence_id UUID NOT NULL,
                is_inside BOOLEAN NOT NULL,
                entered_at TIMESTAMPTZ,
                last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                tenant_id VARCHAR(100),
                PRIMARY KEY (entity_id, geofence_id)
            );

            CREATE INDEX IF NOT EXISTS idx_entity_state_tenant ON entity_geofence_state(tenant_id);
            CREATE INDEX IF NOT EXISTS idx_entity_state_updated ON entity_geofence_state(last_updated);
        ");

        // Create geofence_events table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS geofence_events (
                id UUID PRIMARY KEY,
                event_type VARCHAR(20) NOT NULL,
                event_time TIMESTAMPTZ NOT NULL,
                geofence_id UUID NOT NULL,
                geofence_name VARCHAR(255) NOT NULL,
                entity_id VARCHAR(255) NOT NULL,
                entity_type VARCHAR(100),
                location geometry(Point, 4326) NOT NULL,
                properties JSONB,
                dwell_time_seconds INT,
                sensorthings_observation_id UUID,
                tenant_id VARCHAR(100),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_geofence_events_entity ON geofence_events(entity_id);
            CREATE INDEX IF NOT EXISTS idx_geofence_events_geofence ON geofence_events(geofence_id);
            CREATE INDEX IF NOT EXISTS idx_geofence_events_time_brin ON geofence_events USING BRIN(event_time);
            CREATE INDEX IF NOT EXISTS idx_geofence_events_tenant ON geofence_events(tenant_id);
        ");
    }
}
