namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Options for expanding related entities in a query response.
/// </summary>
public sealed record ExpandOptions
{
    /// <summary>
    /// Names of navigation properties to expand.
    /// Examples: "Locations", "Datastreams", "Observations"
    /// </summary>
    public IReadOnlyList<string> Properties { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Nested expand options for expanded properties.
    /// Allows expanding multiple levels (e.g., Things/Datastreams/Observations).
    /// </summary>
    public Dictionary<string, ExpandOptions>? Nested { get; init; }

    /// <summary>
    /// Maximum depth to allow for nested expansions.
    /// Prevents excessive nesting that could impact performance.
    /// Default: 2
    /// </summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>
    /// For each expanded collection, apply these query options.
    /// Allows filtering/ordering expanded collections.
    /// </summary>
    public Dictionary<string, QueryOptions>? CollectionOptions { get; init; }

    /// <summary>
    /// Parses an OData $expand query parameter string into ExpandOptions.
    /// Supports comma-separated navigation properties (e.g., "Locations,Datastreams").
    /// </summary>
    public static ExpandOptions Parse(string expand)
    {
        if (string.IsNullOrWhiteSpace(expand))
            return new ExpandOptions { Properties = Array.Empty<string>() };

        var properties = expand.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList();

        return new ExpandOptions { Properties = properties };
    }
}
