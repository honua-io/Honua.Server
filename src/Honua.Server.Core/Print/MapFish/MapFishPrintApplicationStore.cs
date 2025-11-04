// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Print.MapFish;

public interface IMapFishPrintApplicationStore
{
    ValueTask<IReadOnlyDictionary<string, MapFishPrintApplicationDefinition>> GetApplicationsAsync(CancellationToken cancellationToken = default);
    ValueTask<MapFishPrintApplicationDefinition?> FindAsync(string appId, CancellationToken cancellationToken = default);
}

public sealed class MapFishPrintApplicationStore : IMapFishPrintApplicationStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IHonuaConfigurationService _configurationService;
    private readonly ILogger<MapFishPrintApplicationStore> _logger;
    private readonly object _syncRoot = new();
    private readonly IDisposable _changeSubscription;

    private IReadOnlyDictionary<string, MapFishPrintApplicationDefinition>? _cache;
    private bool _disposed;

    public MapFishPrintApplicationStore(IHonuaConfigurationService configurationService, ILogger<MapFishPrintApplicationStore> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _changeSubscription = ChangeToken.OnChange(configurationService.GetChangeToken, Invalidate);
    }

    public ValueTask<IReadOnlyDictionary<string, MapFishPrintApplicationDefinition>> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cache = Volatile.Read(ref _cache);
        if (cache is not null)
        {
            return ValueTask.FromResult(cache);
        }

        lock (_syncRoot)
        {
            cache = _cache;
            if (cache is not null)
            {
                return ValueTask.FromResult(cache);
            }

            cache = LoadApplications();
            Volatile.Write(ref _cache, cache);
            return ValueTask.FromResult(cache);
        }
    }

    public async ValueTask<MapFishPrintApplicationDefinition?> FindAsync(string appId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var applications = await GetApplicationsAsync(cancellationToken).ConfigureAwait(false);
        return applications.TryGetValue(appId, out var application) ? application : null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _changeSubscription.Dispose();
    }

    private IReadOnlyDictionary<string, MapFishPrintApplicationDefinition> LoadApplications()
    {
        var config = _configurationService.Current.Services.Print;
        if (!config.Enabled)
        {
            _logger.LogInformation("MapFish print service is disabled in configuration.");
            return new ReadOnlyDictionary<string, MapFishPrintApplicationDefinition>(new Dictionary<string, MapFishPrintApplicationDefinition>(StringComparer.OrdinalIgnoreCase));
        }

        IReadOnlyList<MapFishPrintApplicationDefinition> applications;
        if (string.Equals(config.Provider, "json", StringComparison.OrdinalIgnoreCase))
        {
            applications = LoadFromJson(config.ConfigurationPath);
        }
        else
        {
            applications = MapFishPrintDefaults.Create();
        }

        if (applications.Count == 0)
        {
            _logger.LogWarning("No MapFish print applications found; falling back to built-in defaults.");
            applications = MapFishPrintDefaults.Create();
        }

        var map = new Dictionary<string, MapFishPrintApplicationDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var application in applications)
        {
            if (string.IsNullOrWhiteSpace(application.Id))
            {
                _logger.LogWarning("Skipping MapFish application with empty id.");
                continue;
            }

            EnsureLayoutDefaults(application);
            map[application.Id] = application;
        }

        return new ReadOnlyDictionary<string, MapFishPrintApplicationDefinition>(map);
    }

    private IReadOnlyList<MapFishPrintApplicationDefinition> LoadFromJson(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("MapFish print provider is set to 'json' but configurationPath is not specified.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"MapFish print configuration not found at '{path}'.", path);
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.Deserialize<List<MapFishPrintApplicationDefinition>>(SerializerOptions)
                   ?? new List<MapFishPrintApplicationDefinition>();
        }

        if (root.TryGetProperty("applications", out var applicationsElement))
        {
            return applicationsElement.Deserialize<List<MapFishPrintApplicationDefinition>>(SerializerOptions)
                   ?? new List<MapFishPrintApplicationDefinition>();
        }

        var single = root.Deserialize<MapFishPrintApplicationDefinition>(SerializerOptions);
        if (single is not null)
        {
            return new List<MapFishPrintApplicationDefinition> { single };
        }

        return new List<MapFishPrintApplicationDefinition>();
    }

    private static void EnsureLayoutDefaults(MapFishPrintApplicationDefinition application)
    {
        application.Layouts ??= new List<MapFishPrintLayoutDefinition>();
        application.OutputFormats ??= new List<string>();
        application.Dpis ??= new List<int>();
        application.Attributes ??= new Dictionary<string, MapFishPrintAttributeDefinition>(StringComparer.OrdinalIgnoreCase);

        if (application.Layouts.Count == 0)
        {
            var defaults = MapFishPrintDefaults.Create()[0].Layouts;
            foreach (var layout in defaults)
            {
                application.Layouts.Add(CloneLayout(layout));
            }
        }

        foreach (var layout in application.Layouts)
        {
            layout.Page ??= MapFishPrintLayoutPageDefinition.A4Portrait();
            layout.Map ??= MapFishPrintLayoutMapDefinition.Default();
            layout.Legend ??= MapFishPrintLayoutLegendDefinition.Disabled();
            layout.Title ??= MapFishPrintLayoutTitleDefinition.Default();
            layout.Scale ??= MapFishPrintLayoutScaleDefinition.Default();
        }

        if (string.IsNullOrWhiteSpace(application.DefaultLayout))
        {
            application.DefaultLayout = application.Layouts[0].Name;
        }

        if (application.OutputFormats.Count == 0)
        {
            application.OutputFormats.Add("pdf");
        }

        if (string.IsNullOrWhiteSpace(application.DefaultOutputFormat) ||
            application.OutputFormats.FindIndex(format => string.Equals(format, application.DefaultOutputFormat, StringComparison.OrdinalIgnoreCase)) < 0)
        {
            application.DefaultOutputFormat = application.OutputFormats[0];
        }

        if (application.Dpis.Count == 0)
        {
            application.Dpis.AddRange(new[] { 96, 150, 300 });
        }

        if (application.DefaultDpi <= 0)
        {
            application.DefaultDpi = application.Dpis[0];
        }

        if (!application.Attributes.ContainsKey("map"))
        {
            application.Attributes["map"] = new MapFishPrintAttributeDefinition
            {
                Type = "MapAttributeValue",
                Required = true,
                Description = "Map frame definition including bbox, projection, dpi, and layers",
                ClientInfo = new MapFishMapAttributeClientInfo()
            };
        }
    }

    private static MapFishPrintLayoutDefinition CloneLayout(MapFishPrintLayoutDefinition template)
    {
        return new MapFishPrintLayoutDefinition
        {
            Name = template.Name,
            Default = template.Default,
            SupportsRotation = template.SupportsRotation,
            Page = new MapFishPrintLayoutPageDefinition
            {
                WidthPoints = template.Page.WidthPoints,
                HeightPoints = template.Page.HeightPoints,
                MarginPoints = template.Page.MarginPoints,
                Size = template.Page.Size,
                Orientation = template.Page.Orientation
            },
            Map = new MapFishPrintLayoutMapDefinition
            {
                WidthPixels = template.Map.WidthPixels,
                HeightPixels = template.Map.HeightPixels,
                OffsetX = template.Map.OffsetX,
                OffsetY = template.Map.OffsetY
            },
            Legend = new MapFishPrintLayoutLegendDefinition
            {
                Enabled = template.Legend.Enabled,
                OffsetX = template.Legend.OffsetX,
                OffsetY = template.Legend.OffsetY,
                Width = template.Legend.Width,
                ItemHeight = template.Legend.ItemHeight,
                SymbolSize = template.Legend.SymbolSize
            },
            Title = new MapFishPrintLayoutTitleDefinition
            {
                OffsetX = template.Title.OffsetX,
                OffsetY = template.Title.OffsetY,
                TitleFontSize = template.Title.TitleFontSize,
                SubtitleFontSize = template.Title.SubtitleFontSize,
                Spacing = template.Title.Spacing
            },
            Scale = new MapFishPrintLayoutScaleDefinition
            {
                OffsetX = template.Scale.OffsetX,
                OffsetY = template.Scale.OffsetY,
                FontSize = template.Scale.FontSize
            }
        };
    }

    private void Invalidate()
    {
        lock (_syncRoot)
        {
            Volatile.Write(ref _cache, null);
        }
    }
}
