namespace Honua.MapSDK.Models;

/// <summary>
/// Sample model classes for use with HonuaDataGrid examples
/// These demonstrate best practices for typed data models
/// </summary>

/// <summary>
/// Sample model for property parcel data
/// </summary>
public class ParcelFeature
{
    /// <summary>
    /// Unique parcel identifier
    /// </summary>
    public string ParcelId { get; set; } = "";

    /// <summary>
    /// Property address
    /// </summary>
    public string Address { get; set; } = "";

    /// <summary>
    /// Property owner name
    /// </summary>
    public string Owner { get; set; } = "";

    /// <summary>
    /// Assessed property value
    /// </summary>
    public decimal AssessedValue { get; set; }

    /// <summary>
    /// Property acreage
    /// </summary>
    public double Acreage { get; set; }

    /// <summary>
    /// Zoning classification
    /// </summary>
    public string? Zoning { get; set; }

    /// <summary>
    /// Year built
    /// </summary>
    public int? YearBuilt { get; set; }

    /// <summary>
    /// GeoJSON geometry (stored as JSON string)
    /// </summary>
    public string? Geometry { get; set; }
}

/// <summary>
/// Sample model for point-of-interest data
/// </summary>
public class PointOfInterest
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// POI name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Category or type
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Latitude
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Rating (0-5)
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Website URL
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// GeoJSON geometry
    /// </summary>
    public string? Geometry { get; set; }
}

/// <summary>
/// Sample model for sensor/telemetry data
/// </summary>
public class SensorReading
{
    /// <summary>
    /// Unique reading identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Sensor name or identifier
    /// </summary>
    public string SensorName { get; set; } = "";

    /// <summary>
    /// Temperature reading (Fahrenheit)
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Humidity percentage
    /// </summary>
    public double Humidity { get; set; }

    /// <summary>
    /// Air quality index
    /// </summary>
    public int? AirQualityIndex { get; set; }

    /// <summary>
    /// Timestamp of the reading
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Sensor location (latitude)
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Sensor location (longitude)
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// GeoJSON geometry
    /// </summary>
    public string? Geometry { get; set; }
}

/// <summary>
/// Sample model for infrastructure assets
/// </summary>
public class InfrastructureAsset
{
    /// <summary>
    /// Asset identifier
    /// </summary>
    public string AssetId { get; set; } = "";

    /// <summary>
    /// Asset type (e.g., "Water Main", "Street Light")
    /// </summary>
    public string AssetType { get; set; } = "";

    /// <summary>
    /// Asset status
    /// </summary>
    public AssetStatus Status { get; set; }

    /// <summary>
    /// Installation date
    /// </summary>
    public DateTime? InstallDate { get; set; }

    /// <summary>
    /// Last inspection date
    /// </summary>
    public DateTime? LastInspection { get; set; }

    /// <summary>
    /// Condition rating (1-5, 5 being best)
    /// </summary>
    public int ConditionRating { get; set; }

    /// <summary>
    /// Maintenance cost (annual)
    /// </summary>
    public decimal MaintenanceCost { get; set; }

    /// <summary>
    /// Notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// GeoJSON geometry
    /// </summary>
    public string? Geometry { get; set; }
}

/// <summary>
/// Asset status enumeration
/// </summary>
public enum AssetStatus
{
    Active,
    Inactive,
    UnderMaintenance,
    NeedsRepair,
    Decommissioned
}

/// <summary>
/// Generic feature model for dynamic GeoJSON data
/// Use this when you don't have a strongly-typed model
/// </summary>
public class GenericFeature
{
    /// <summary>
    /// Feature identifier
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Feature type
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Feature properties as dictionary
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// GeoJSON geometry
    /// </summary>
    public string? Geometry { get; set; }

    /// <summary>
    /// Get property value by key
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        if (Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Set property value
    /// </summary>
    public void SetProperty(string key, object value)
    {
        Properties[key] = value;
    }
}
