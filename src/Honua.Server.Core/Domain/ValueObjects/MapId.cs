// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a Map entity.
/// Prevents mixing up map IDs with other entity IDs and provides type safety.
/// </summary>
public sealed record MapId
{
    /// <summary>
    /// Gets the underlying GUID value of this map ID.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MapId"/> record.
    /// </summary>
    /// <param name="value">The GUID value for this map ID.</param>
    /// <exception cref="DomainException">Thrown when the value is an empty GUID.</exception>
    public MapId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException(
                "Map ID cannot be empty.",
                "MAPID_EMPTY");
        }

        Value = value;
    }

    /// <summary>
    /// Creates a new map ID with a generated GUID.
    /// </summary>
    /// <returns>A new <see cref="MapId"/> instance with a unique value.</returns>
    public static MapId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a map ID from a string representation of a GUID.
    /// </summary>
    /// <param name="value">The string representation of a GUID.</param>
    /// <returns>A new <see cref="MapId"/> instance.</returns>
    /// <exception cref="DomainException">Thrown when the string is not a valid GUID.</exception>
    public static MapId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new DomainException(
                $"Invalid map ID format: {value}",
                "MAPID_INVALID_FORMAT");
        }

        return new MapId(guid);
    }

    /// <summary>
    /// Attempts to create a map ID from a string representation of a GUID.
    /// </summary>
    /// <param name="value">The string representation of a GUID.</param>
    /// <param name="mapId">When this method returns, contains the map ID if parsing succeeded.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string value, out MapId? mapId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            mapId = new MapId(guid);
            return true;
        }

        mapId = null;
        return false;
    }

    /// <summary>
    /// Returns the string representation of this map ID.
    /// </summary>
    /// <returns>The string representation of the underlying GUID.</returns>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Implicitly converts a <see cref="MapId"/> to a <see cref="Guid"/>.
    /// </summary>
    /// <param name="mapId">The map ID to convert.</param>
    public static implicit operator Guid(MapId mapId) => mapId.Value;
}
