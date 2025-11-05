namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Represents a unit of measurement for a Datastream.
/// </summary>
public sealed record UnitOfMeasurement
{
    /// <summary>
    /// Full name of the unit of measurement.
    /// Example: "Degree Celsius"
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// Textual form of the unit symbol.
    /// Example: "°C"
    /// </summary>
    public string Symbol { get; init; } = default!;

    /// <summary>
    /// URI defining the unit of measurement.
    /// Should reference a standard like UCUM (http://unitsofmeasure.org) or QUDT.
    /// Example: "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
    /// </summary>
    public string Definition { get; init; } = default!;
}
