using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;

namespace Honua.Server.Core.Tests.Shared.Builders;

/// <summary>
/// Builder for creating STAC test items with fluent API.
/// Reduces ~200 lines of repetitive STAC item creation across tests.
/// </summary>
/// <remarks>
/// <para>
/// Usage example:
/// </para>
/// <code>
/// var item = new StacItemBuilder()
///     .WithId("test-item-001")
///     .WithCollection("test-collection")
///     .WithGeometry(-122.5, 37.8)
///     .WithDatetime(DateTime.UtcNow)
///     .WithAsset("visual", "image/tiff", "https://example.com/visual.tif")
///     .Build();
/// </code>
/// </remarks>
public class StacItemBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private string _collectionId = "default-collection";
    private Dictionary<string, object> _geometry = CreateDefaultGeometry();
    private double[] _bbox = new[] { -122.5, 37.8, -122.4, 37.9 };
    private Dictionary<string, object> _properties = new();
    private Dictionary<string, StacAsset> _assets = new();
    private List<StacLink> _links = new();

    /// <summary>
    /// Sets the STAC item ID.
    /// </summary>
    public StacItemBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the collection ID that this item belongs to.
    /// </summary>
    public StacItemBuilder WithCollection(string collectionId)
    {
        _collectionId = collectionId;
        return this;
    }

    /// <summary>
    /// Sets a point geometry at the specified coordinates.
    /// </summary>
    public StacItemBuilder WithGeometry(double lon, double lat)
    {
        _geometry = new Dictionary<string, object>
        {
            ["type"] = "Point",
            ["coordinates"] = new[] { lon, lat }
        };

        _bbox = new[] { lon, lat, lon, lat };
        return this;
    }

    /// <summary>
    /// Sets a polygon geometry from the specified bounding box.
    /// </summary>
    public StacItemBuilder WithBboxGeometry(double west, double south, double east, double north)
    {
        _geometry = new Dictionary<string, object>
        {
            ["type"] = "Polygon",
            ["coordinates"] = new object[]
            {
                new[]
                {
                    new[] { west, south },
                    new[] { east, south },
                    new[] { east, north },
                    new[] { west, north },
                    new[] { west, south }
                }
            }
        };

        _bbox = new[] { west, south, east, north };
        return this;
    }

    /// <summary>
    /// Creates a geometry that crosses the antimeridian (dateline).
    /// Useful for testing edge cases with Pacific Ocean features.
    /// </summary>
    public StacItemBuilder CrossingDateline()
    {
        _geometry = new Dictionary<string, object>
        {
            ["type"] = "Polygon",
            ["coordinates"] = new object[]
            {
                new[]
                {
                    new[] { 170.0, -20.0 },
                    new[] { -170.0, -20.0 },
                    new[] { -170.0, -10.0 },
                    new[] { 170.0, -10.0 },
                    new[] { 170.0, -20.0 }
                }
            }
        };

        _bbox = new[] { 170.0, -20.0, -170.0, -10.0 };
        return this;
    }

    /// <summary>
    /// Sets the datetime property.
    /// </summary>
    public StacItemBuilder WithDatetime(DateTime datetime)
    {
        _properties["datetime"] = datetime.ToString("O");
        return this;
    }

    /// <summary>
    /// Sets a date range with start and end times.
    /// </summary>
    public StacItemBuilder WithDateRange(DateTime start, DateTime end)
    {
        _properties["datetime"] = null;
        _properties["start_datetime"] = start.ToString("O");
        _properties["end_datetime"] = end.ToString("O");
        return this;
    }

    /// <summary>
    /// Adds a custom property to the STAC item.
    /// </summary>
    public StacItemBuilder WithProperty(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Adds an asset to the STAC item.
    /// </summary>
    public StacItemBuilder WithAsset(string key, string mediaType, string href, string title = null)
    {
        _assets[key] = new StacAsset
        {
            Href = href,
            Type = mediaType,
            Title = title
        };
        return this;
    }

    /// <summary>
    /// Adds a visual asset (common for satellite imagery).
    /// </summary>
    public StacItemBuilder WithVisualAsset(string href = "https://example.com/visual.tif")
    {
        return WithAsset("visual", "image/tiff", href, "Visual RGB");
    }

    /// <summary>
    /// Adds a thumbnail asset.
    /// </summary>
    public StacItemBuilder WithThumbnail(string href = "https://example.com/thumbnail.png")
    {
        return WithAsset("thumbnail", "image/png", href, "Thumbnail");
    }

    /// <summary>
    /// Adds a link to the STAC item.
    /// </summary>
    public StacItemBuilder WithLink(string rel, string href, string type = null, string title = null)
    {
        _links.Add(new StacLink
        {
            Rel = rel,
            Href = href,
            Type = type,
            Title = title
        });
        return this;
    }

    /// <summary>
    /// Adds a self link.
    /// </summary>
    public StacItemBuilder WithSelfLink(string href)
    {
        return WithLink("self", href, "application/geo+json");
    }

    /// <summary>
    /// Adds a collection link.
    /// </summary>
    public StacItemBuilder WithCollectionLink(string href)
    {
        return WithLink("collection", href, "application/json");
    }

    /// <summary>
    /// Builds the STAC item record.
    /// </summary>
    public StacItemRecord Build()
    {
        // Ensure datetime is set if not already
        if (!_properties.ContainsKey("datetime") && !_properties.ContainsKey("start_datetime"))
        {
            _properties["datetime"] = DateTime.UtcNow.ToString("O");
        }

        var propertiesNode = JsonSerializer.SerializeToNode(_properties) as JsonObject;

        // Extract datetime fields from properties to set on the record
        // The StacApiMapper.MergeProperties method uses these record fields
        // to build the datetime properties in the API response
        DateTimeOffset? datetime = null;
        DateTimeOffset? startDatetime = null;
        DateTimeOffset? endDatetime = null;

        if (_properties.TryGetValue("datetime", out var datetimeValue))
        {
            if (datetimeValue is string datetimeStr && !string.IsNullOrEmpty(datetimeStr))
            {
                if (DateTimeOffset.TryParse(datetimeStr, out var parsedDatetime))
                {
                    datetime = parsedDatetime;
                }
            }
        }

        if (_properties.TryGetValue("start_datetime", out var startValue))
        {
            if (startValue is string startStr && DateTimeOffset.TryParse(startStr, out var parsedStart))
            {
                startDatetime = parsedStart;
            }
        }

        if (_properties.TryGetValue("end_datetime", out var endValue))
        {
            if (endValue is string endStr && DateTimeOffset.TryParse(endStr, out var parsedEnd))
            {
                endDatetime = parsedEnd;
            }
        }

        return new StacItemRecord
        {
            Id = _id,
            CollectionId = _collectionId,
            Geometry = JsonSerializer.Serialize(_geometry),
            Bbox = _bbox,
            Properties = propertiesNode,
            Assets = _assets,
            Links = _links,
            Datetime = datetime,
            StartDatetime = startDatetime,
            EndDatetime = endDatetime
        };
    }

    private static Dictionary<string, object> CreateDefaultGeometry()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "Point",
            ["coordinates"] = new[] { -122.5, 37.8 }
        };
    }
}
