using System.Text.Json.Serialization;

namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a request to create observations using the Data Array extension.
/// This provides a more efficient way to upload bulk observations by reducing JSON overhead.
/// </summary>
public sealed record DataArrayRequest
{
    /// <summary>
    /// Reference to the Datastream these observations belong to.
    /// </summary>
    [JsonPropertyName("Datastream")]
    public EntityReference? Datastream { get; init; }

    /// <summary>
    /// Array of component names that define the structure of each data array row.
    /// Common components: "phenomenonTime", "result", "resultTime", "FeatureOfInterest/id", "parameters"
    /// </summary>
    [JsonPropertyName("components")]
    public IReadOnlyList<string>? Components { get; init; }

    /// <summary>
    /// Array of observation data. Each row corresponds to the components array.
    /// Example: [["2025-11-05T10:00:00Z", 22.5], ["2025-11-05T10:05:00Z", 22.7]]
    /// </summary>
    [JsonPropertyName("dataArray")]
    public IReadOnlyList<IReadOnlyList<object>>? DataArray { get; init; }

    /// <summary>
    /// Converts the data array format to a list of Observation entities.
    /// </summary>
    public IReadOnlyList<Observation> ToObservations()
    {
        if (Components == null || DataArray == null || Datastream?.Id == null)
            return Array.Empty<Observation>();

        var observations = new List<Observation>();

        foreach (var row in DataArray)
        {
            if (row.Count != Components.Count)
                throw new ArgumentException($"Data array row has {row.Count} items but expected {Components.Count} based on components.");

            var obs = new Observation
            {
                DatastreamId = Datastream.Id
            };

            for (int i = 0; i < Components.Count; i++)
            {
                var component = Components[i];
                var value = row[i];

                obs = component switch
                {
                    "phenomenonTime" => obs with { PhenomenonTime = ParseDateTime(value) },
                    "result" => obs with { Result = value },
                    "resultTime" => obs with { ResultTime = ParseDateTime(value) },
                    "resultQuality" => obs with { ResultQuality = value?.ToString() },
                    "validTime" when value is IList<object> validTimePair && validTimePair.Count == 2 =>
                        obs with
                        {
                            ValidTimeStart = ParseDateTime(validTimePair[0]),
                            ValidTimeEnd = ParseDateTime(validTimePair[1])
                        },
                    "FeatureOfInterest/id" => obs with { FeatureOfInterestId = value?.ToString() },
                    "parameters" when value is Dictionary<string, object> parameters =>
                        obs with { Parameters = parameters },
                    _ => obs
                };
            }

            observations.Add(obs);
        }

        return observations;
    }

    private static DateTime ParseDateTime(object? value)
    {
        if (value == null)
            return DateTime.UtcNow;

        if (value is DateTime dt)
            return dt;

        if (value is string str)
            return DateTime.Parse(str);

        throw new ArgumentException($"Cannot parse {value} as DateTime");
    }
}
