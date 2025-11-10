// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Honua.Server.Host.Admin;

public static class MapConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapMapConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/map-configurations")
            .WithTags("Map Configurations")
            .WithOpenApi();

        // List all map configurations
        group.MapGet("/", async ([FromServices] DbContext db, [FromQuery] bool includeTemplates = false) =>
        {
            var query = db.Set<MapConfigurationEntity>().AsQueryable();

            if (includeTemplates)
            {
                query = query.Where(m => m.IsPublic || m.IsTemplate);
            }

            var configs = await query
                .OrderByDescending(m => m.UpdatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Description,
                    m.CreatedAt,
                    m.UpdatedAt,
                    m.CreatedBy,
                    m.IsPublic,
                    m.IsTemplate,
                    m.Tags,
                    m.ThumbnailUrl,
                    m.ViewCount
                })
                .ToListAsync();

            return Results.Ok(configs);
        })
        .WithName("ListMapConfigurations")
        .WithSummary("List all map configurations");

        // Get single map configuration
        group.MapGet("/{id}", async ([FromServices] DbContext db, string id) =>
        {
            var config = await db.Set<MapConfigurationEntity>()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (config == null)
                return Results.NotFound(new { error = "Map configuration not found" });

            // Increment view count
            config.ViewCount++;
            await db.SaveChangesAsync();

            return Results.Ok(config);
        })
        .WithName("GetMapConfiguration")
        .WithSummary("Get a map configuration by ID");

        // Create new map configuration
        group.MapPost("/", async ([FromServices] DbContext db, [FromBody] CreateMapConfigurationRequest request) =>
        {
            var config = new MapConfigurationEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Configuration = request.Configuration,
                CreatedBy = request.CreatedBy ?? "system",
                IsPublic = request.IsPublic,
                IsTemplate = request.IsTemplate,
                Tags = request.Tags,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Set<MapConfigurationEntity>().Add(config);
            await db.SaveChangesAsync();

            return Results.Created($"/admin/api/map-configurations/{config.Id}", config);
        })
        .WithName("CreateMapConfiguration")
        .WithSummary("Create a new map configuration");

        // Update map configuration
        group.MapPut("/{id}", async ([FromServices] DbContext db, string id, [FromBody] UpdateMapConfigurationRequest request) =>
        {
            var config = await db.Set<MapConfigurationEntity>()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (config == null)
                return Results.NotFound(new { error = "Map configuration not found" });

            if (request.Name != null)
                config.Name = request.Name;

            if (request.Description != null)
                config.Description = request.Description;

            if (request.Configuration != null)
                config.Configuration = request.Configuration;

            if (request.IsPublic.HasValue)
                config.IsPublic = request.IsPublic.Value;

            if (request.IsTemplate.HasValue)
                config.IsTemplate = request.IsTemplate.Value;

            if (request.Tags != null)
                config.Tags = request.Tags;

            if (request.ThumbnailUrl != null)
                config.ThumbnailUrl = request.ThumbnailUrl;

            config.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(config);
        })
        .WithName("UpdateMapConfiguration")
        .WithSummary("Update a map configuration");

        // Delete map configuration
        group.MapDelete("/{id}", async ([FromServices] DbContext db, string id) =>
        {
            var config = await db.Set<MapConfigurationEntity>()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (config == null)
                return Results.NotFound(new { error = "Map configuration not found" });

            db.Set<MapConfigurationEntity>().Remove(config);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Map configuration deleted successfully" });
        })
        .WithName("DeleteMapConfiguration")
        .WithSummary("Delete a map configuration");

        // Export as JSON
        group.MapGet("/{id}/export/json", async ([FromServices] DbContext db, string id) =>
        {
            var config = await db.Set<MapConfigurationEntity>()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (config == null)
                return Results.NotFound(new { error = "Map configuration not found" });

            return Results.Content(config.Configuration, "application/json");
        })
        .WithName("ExportMapConfigurationAsJson")
        .WithSummary("Export map configuration as JSON");

        // Export as YAML
        group.MapGet("/{id}/export/yaml", async ([FromServices] DbContext db, string id) =>
        {
            var config = await db.Set<MapConfigurationEntity>()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (config == null)
                return Results.NotFound(new { error = "Map configuration not found" });

            // Convert JSON to YAML using YamlDotNet
            var jsonObj = JsonSerializer.Deserialize<object>(config.Configuration);
            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = serializer.Serialize(jsonObj);

            return Results.Content(yaml, "application/x-yaml");
        })
        .WithName("ExportMapConfigurationAsYaml")
        .WithSummary("Export map configuration as YAML");

        // Export as HTML embed
        group.MapGet("/{id}/export/html", async ([FromServices] DbContext db, string id, [FromQuery] string sdkUrl = "https://cdn.honua.io/sdk") =>
        {
            var config = await db.Set<MapConfigurationEntity>()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (config == null)
                return Results.NotFound(new { error = "Map configuration not found" });

            var html = $$"""
<!DOCTYPE html>
<html>
<head>
  <title>{{config.Name}}</title>
  <script src="{{sdkUrl}}/honua-mapsdk.js"></script>
  <link rel="stylesheet" href="{{sdkUrl}}/honua-mapsdk.css">
  <style>
    body { margin: 0; padding: 0; }
    #map { width: 100vw; height: 100vh; }
  </style>
</head>
<body>
  <div id="map"></div>
  <script>
    const config = {{config.Configuration}};
    HonuaMap.create('#map', config);
  </script>
</body>
</html>
""";

            return Results.Content(html, "text/html");
        })
        .WithName("ExportMapConfigurationAsHtml")
        .WithSummary("Export map configuration as embeddable HTML");

        // Clone map configuration
        group.MapPost("/{id}/clone", async ([FromServices] DbContext db, string id, [FromQuery] string? newName = null) =>
        {
            var original = await db.Set<MapConfigurationEntity>()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (original == null)
                return Results.NotFound(new { error = "Map configuration not found" });

            var cloned = new MapConfigurationEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = newName ?? $"{original.Name} (Copy)",
                Description = original.Description,
                Configuration = original.Configuration,
                CreatedBy = original.CreatedBy,
                IsPublic = false, // Clones are private by default
                IsTemplate = false,
                Tags = original.Tags,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Set<MapConfigurationEntity>().Add(cloned);
            await db.SaveChangesAsync();

            return Results.Created($"/admin/api/map-configurations/{cloned.Id}", cloned);
        })
        .WithName("CloneMapConfiguration")
        .WithSummary("Clone a map configuration");

        // Get templates
        group.MapGet("/templates/list", async ([FromServices] DbContext db) =>
        {
            var templates = await db.Set<MapConfigurationEntity>()
                .Where(m => m.IsTemplate)
                .OrderBy(m => m.Name)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Description,
                    m.ThumbnailUrl,
                    m.Tags
                })
                .ToListAsync();

            return Results.Ok(templates);
        })
        .WithName("ListMapTemplates")
        .WithSummary("List all map templates");

        return endpoints;
    }
}

// Request models
public record CreateMapConfigurationRequest(
    string Name,
    string? Description,
    string Configuration,
    string? CreatedBy,
    bool IsPublic = false,
    bool IsTemplate = false,
    string? Tags = null
);

public record UpdateMapConfigurationRequest(
    string? Name = null,
    string? Description = null,
    string? Configuration = null,
    bool? IsPublic = null,
    bool? IsTemplate = null,
    string? Tags = null,
    string? ThumbnailUrl = null
);
