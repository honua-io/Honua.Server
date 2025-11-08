using System.Text;
using System.Text.Json;
using Honua.MapSDK.Models;
using YamlDotNet.Serialization;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for saving, loading, and exporting map configurations
/// </summary>
public interface IMapConfigurationService
{
    /// <summary>
    /// Export map configuration as JSON
    /// </summary>
    string ExportAsJson(MapConfiguration config, bool formatted = true);

    /// <summary>
    /// Export map configuration as YAML
    /// </summary>
    string ExportAsYaml(MapConfiguration config);

    /// <summary>
    /// Export map configuration as HTML embed code
    /// </summary>
    string ExportAsHtmlEmbed(MapConfiguration config, string sdkUrl);

    /// <summary>
    /// Export map configuration as Blazor component code
    /// </summary>
    string ExportAsBlazorComponent(MapConfiguration config);

    /// <summary>
    /// Import map configuration from JSON
    /// </summary>
    MapConfiguration ImportFromJson(string json);

    /// <summary>
    /// Import map configuration from YAML
    /// </summary>
    MapConfiguration ImportFromYaml(string yaml);

    /// <summary>
    /// Validate map configuration
    /// </summary>
    ValidationResult Validate(MapConfiguration config);
}

public class MapConfigurationService : IMapConfigurationService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string ExportAsJson(MapConfiguration config, bool formatted = true)
    {
        var options = formatted ? _jsonOptions : new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(config, options);
    }

    public string ExportAsYaml(MapConfiguration config)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();

        return serializer.Serialize(config);
    }

    public string ExportAsHtmlEmbed(MapConfiguration config, string sdkUrl)
    {
        var json = ExportAsJson(config, false);
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine($"  <title>{config.Name}</title>");
        html.AppendLine($"  <script src=\"{sdkUrl}/honua-mapsdk.js\"></script>");
        html.AppendLine($"  <link rel=\"stylesheet\" href=\"{sdkUrl}/honua-mapsdk.css\">");
        html.AppendLine("  <style>");
        html.AppendLine("    body { margin: 0; padding: 0; }");
        html.AppendLine("    #map { width: 100vw; height: 100vh; }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <div id=\"map\"></div>");
        html.AppendLine("  <script>");
        html.AppendLine($"    const config = {json};");
        html.AppendLine("    HonuaMap.create('#map', config);");
        html.AppendLine("  </script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    public string ExportAsBlazorComponent(MapConfiguration config)
    {
        var razor = new StringBuilder();

        razor.AppendLine($"@* {config.Name} *@");
        razor.AppendLine($"@* Generated from Honua MapSDK *@");
        razor.AppendLine();
        razor.AppendLine("<HonuaMap");
        razor.AppendLine($"    Id=\"{config.Id}\"");
        razor.AppendLine($"    Style=\"{config.Settings.Style}\"");
        razor.AppendLine($"    Center=\"@(new[] {{ {config.Settings.Center[0]}, {config.Settings.Center[1]} }})\"");
        razor.AppendLine($"    Zoom=\"{config.Settings.Zoom}\"");

        if (config.Settings.Bearing != 0)
            razor.AppendLine($"    Bearing=\"{config.Settings.Bearing}\"");
        if (config.Settings.Pitch != 0)
            razor.AppendLine($"    Pitch=\"{config.Settings.Pitch}\"");
        if (config.Settings.Projection != "mercator")
            razor.AppendLine($"    Projection=\"{config.Settings.Projection}\"");

        razor.AppendLine(">");
        razor.AppendLine();

        // Layers
        foreach (var layer in config.Layers)
        {
            razor.AppendLine($"    <HonuaLayer");
            razor.AppendLine($"        Id=\"{layer.Id}\"");
            razor.AppendLine($"        Name=\"{layer.Name}\"");
            razor.AppendLine($"        Type=\"LayerType.{layer.Type}\"");
            razor.AppendLine($"        Source=\"{layer.Source}\"");
            razor.AppendLine($"        Visible=\"{layer.Visible.ToString().ToLower()}\"");
            razor.AppendLine($"        Opacity=\"{layer.Opacity}\" />");
            razor.AppendLine();
        }

        // Controls
        foreach (var control in config.Controls.Where(c => c.Visible))
        {
            razor.AppendLine($"    <Honua{control.Type}Control Position=\"{control.Position}\" />");
        }

        razor.AppendLine("</HonuaMap>");

        return razor.ToString();
    }

    public MapConfiguration ImportFromJson(string json)
    {
        var config = JsonSerializer.Deserialize<MapConfiguration>(json, _jsonOptions);
        if (config == null)
            throw new InvalidOperationException("Failed to deserialize map configuration");

        return config;
    }

    public MapConfiguration ImportFromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<MapConfiguration>(yaml);
        return config;
    }

    public ValidationResult Validate(MapConfiguration config)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(config.Name))
            result.Errors.Add("Map name is required");

        if (string.IsNullOrWhiteSpace(config.Settings.Style))
            result.Errors.Add("Map style is required");

        if (config.Settings.Center == null || config.Settings.Center.Length != 2)
            result.Errors.Add("Map center must be [longitude, latitude]");

        if (config.Settings.Zoom < 0 || config.Settings.Zoom > 22)
            result.Errors.Add("Map zoom must be between 0 and 22");

        if (config.Settings.Pitch < 0 || config.Settings.Pitch > 60)
            result.Errors.Add("Map pitch must be between 0 and 60");

        // Validate layers
        foreach (var layer in config.Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Name))
                result.Errors.Add($"Layer {layer.Id}: name is required");

            if (string.IsNullOrWhiteSpace(layer.Source))
                result.Errors.Add($"Layer {layer.Id}: source is required");

            if (layer.Opacity < 0 || layer.Opacity > 1)
                result.Errors.Add($"Layer {layer.Id}: opacity must be between 0 and 1");
        }

        // Check for duplicate layer IDs
        var duplicateLayerIds = config.Layers
            .GroupBy(l => l.Id)
            .Where(g => g.Skip(1).Any())
            .Select(g => g.Key);

        foreach (var id in duplicateLayerIds)
        {
            result.Errors.Add($"Duplicate layer ID: {id}");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
}
