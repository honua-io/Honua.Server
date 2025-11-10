// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace Honua.Server.Core.Cloud.EventGrid.Models;

/// <summary>
/// CloudEvents v1.0 compliant event for Honua geospatial data.
/// Extends CloudEvents with geospatial metadata.
/// </summary>
/// <remarks>
/// Spec: https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md
/// </remarks>
public class HonuaCloudEvent
{
    /// <summary>
    /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Identifies the context in which an event happened (e.g., honua.io/tenant/abc/features/parcels).
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// The version of the CloudEvents specification (1.0).
    /// </summary>
    [JsonPropertyName("specversion")]
    public string SpecVersion { get; init; } = "1.0";

    /// <summary>
    /// Describes the type of event (e.g., honua.features.created, honua.sensor.observation.created).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Content type of the data value (application/json).
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    public string DataContentType { get; init; } = "application/json";

    /// <summary>
    /// Timestamp of when the occurrence happened (ISO 8601).
    /// </summary>
    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// A link to the schema that the data adheres to.
    /// </summary>
    [JsonPropertyName("dataschema")]
    public string? DataSchema { get; init; }

    /// <summary>
    /// The event payload (domain-specific).
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; init; }

    /// <summary>
    /// Subject of the event in the context of the event producer (e.g., feature ID, sensor ID).
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; init; }

    // ========================================
    // Honua-specific Extensions
    // ========================================

    /// <summary>
    /// Tenant ID for multi-tenancy support.
    /// </summary>
    [JsonPropertyName("honuatenantid")]
    public string? TenantId { get; init; }

    /// <summary>
    /// Bounding box of the geospatial data [minLon, minLat, maxLon, maxLat].
    /// </summary>
    [JsonPropertyName("honuabbox")]
    public double[]? BoundingBox { get; init; }

    /// <summary>
    /// Coordinate Reference System (e.g., EPSG:4326).
    /// </summary>
    [JsonPropertyName("honuacrs")]
    public string? Crs { get; init; }

    /// <summary>
    /// Collection or datastream ID this event relates to.
    /// </summary>
    [JsonPropertyName("honuacollection")]
    public string? Collection { get; init; }

    /// <summary>
    /// Event severity for alerts/notifications (info, warning, error, critical).
    /// </summary>
    [JsonPropertyName("honuaseverity")]
    public string? Severity { get; init; }
}

/// <summary>
/// Event types for Honua platform.
/// </summary>
public static class HonuaEventTypes
{
    // Features API (OGC API - Features / WFS-T)
    public const string FeatureCreated = "honua.features.created";
    public const string FeatureUpdated = "honua.features.updated";
    public const string FeatureDeleted = "honua.features.deleted";
    public const string FeatureBatchCreated = "honua.features.batch.created";
    public const string FeatureBatchUpdated = "honua.features.batch.updated";
    public const string FeatureBatchDeleted = "honua.features.batch.deleted";

    // SensorThings API (OGC SensorThings API)
    public const string ObservationCreated = "honua.sensor.observation.created";
    public const string ObservationBatchCreated = "honua.sensor.observation.batch.created";
    public const string DatastreamUpdated = "honua.sensor.datastream.updated";
    public const string ThingCreated = "honua.sensor.thing.created";
    public const string ThingUpdated = "honua.sensor.thing.updated";
    public const string LocationUpdated = "honua.sensor.location.updated";

    // GeoEvent API (Geofencing & Real-time Events)
    public const string GeofenceEntered = "honua.geoevent.geofence.entered";
    public const string GeofenceExited = "honua.geoevent.geofence.exited";
    public const string GeofenceAlert = "honua.geoevent.geofence.alert";
    public const string LocationEvaluated = "honua.geoevent.location.evaluated";

    // General Alerts
    public const string AlertTriggered = "honua.alert.triggered";
    public const string AlertResolved = "honua.alert.resolved";
}

/// <summary>
/// Builder for creating CloudEvents with proper defaults.
/// </summary>
public class HonuaCloudEventBuilder
{
    private string? _id;
    private string? _source;
    private string? _type;
    private string? _subject;
    private object? _data;
    private string? _tenantId;
    private double[]? _bbox;
    private string? _crs = "EPSG:4326";
    private string? _collection;
    private string? _severity;
    private DateTimeOffset? _time;
    private string? _dataSchema;

    public HonuaCloudEventBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public HonuaCloudEventBuilder WithSource(string source)
    {
        _source = source;
        return this;
    }

    public HonuaCloudEventBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    public HonuaCloudEventBuilder WithSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    public HonuaCloudEventBuilder WithData(object data)
    {
        _data = data;
        return this;
    }

    public HonuaCloudEventBuilder WithTenantId(string? tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public HonuaCloudEventBuilder WithBoundingBox(double[]? bbox)
    {
        _bbox = bbox;
        return this;
    }

    public HonuaCloudEventBuilder WithBoundingBox(Envelope? envelope)
    {
        if (envelope != null && !envelope.IsNull)
        {
            _bbox = new[] { envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY };
        }
        return this;
    }

    public HonuaCloudEventBuilder WithCrs(string crs)
    {
        _crs = crs;
        return this;
    }

    public HonuaCloudEventBuilder WithCollection(string collection)
    {
        _collection = collection;
        return this;
    }

    public HonuaCloudEventBuilder WithSeverity(string severity)
    {
        _severity = severity;
        return this;
    }

    public HonuaCloudEventBuilder WithTime(DateTimeOffset time)
    {
        _time = time;
        return this;
    }

    public HonuaCloudEventBuilder WithDataSchema(string? schema)
    {
        _dataSchema = schema;
        return this;
    }

    public HonuaCloudEvent Build()
    {
        if (string.IsNullOrEmpty(_id))
            throw new InvalidOperationException("Event ID is required");
        if (string.IsNullOrEmpty(_source))
            throw new InvalidOperationException("Event source is required");
        if (string.IsNullOrEmpty(_type))
            throw new InvalidOperationException("Event type is required");

        return new HonuaCloudEvent
        {
            Id = _id,
            Source = _source,
            Type = _type,
            Subject = _subject,
            Data = _data,
            TenantId = _tenantId,
            BoundingBox = _bbox,
            Crs = _crs,
            Collection = _collection,
            Severity = _severity,
            Time = _time ?? DateTimeOffset.UtcNow,
            DataSchema = _dataSchema
        };
    }
}
