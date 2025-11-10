// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Storage configuration for SensorThings entities.
/// Allows customization of table names and storage strategies.
/// </summary>
public sealed record SensorThingsStorageDefinition
{
    // Table names (allow customization per deployment)

    public string ThingsTable { get; init; } = "sta_things";
    public string LocationsTable { get; init; } = "sta_locations";
    public string HistoricalLocationsTable { get; init; } = "sta_historical_locations";
    public string DatastreamsTable { get; init; } = "sta_datastreams";
    public string SensorsTable { get; init; } = "sta_sensors";
    public string ObservedPropertiesTable { get; init; } = "sta_observed_properties";
    public string ObservationsTable { get; init; } = "sta_observations";
    public string FeaturesOfInterestTable { get; init; } = "sta_features_of_interest";
    public string ThingLocationLinkTable { get; init; } = "sta_thing_location";
    public string HistoricalLocationLinkTable { get; init; } = "sta_historical_location_location";

    // Partitioning strategy for observations (critical for mobile scale)

    /// <summary>
    /// Whether to partition the observations table by time.
    /// Recommended: true for high-volume observation collection.
    /// </summary>
    public bool PartitionObservations { get; init; } = true;

    /// <summary>
    /// The partitioning strategy to use for observations.
    /// Options: "daily", "weekly", "monthly", "yearly"
    /// </summary>
    public string PartitionStrategy { get; init; } = "monthly";

    // Retention policies

    /// <summary>
    /// Number of days to retain historical location records.
    /// null = keep forever
    /// </summary>
    public int? HistoricalLocationRetentionDays { get; init; } = 365;

    /// <summary>
    /// Number of days to retain observation records.
    /// null = keep forever
    /// </summary>
    public int? ObservationRetentionDays { get; init; } = null;
}
