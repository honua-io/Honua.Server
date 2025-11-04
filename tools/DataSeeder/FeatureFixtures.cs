using System.Collections.Generic;

namespace Honua.Tools.DataSeeder;

internal static class FeatureFixtures
{
    public static IReadOnlyList<FeatureDefinition> Default { get; } = new List<FeatureDefinition>
    {
        new(2001, "Global Meridian", new [] { new[]{-1d, 51d}, new[]{1d, 52d} }, "2022-01-01T00:00:00Z"),
        new(2002, "Equatorial Connector", new [] { new[]{-75d, -2d}, new[]{-72d, 3d} }, "2022-01-02T00:00:00Z"),
        new(2003, "Dateline Bridge", new [] { new[]{179d, 67d}, new[]{-179d, 69d} }, "2022-01-03T00:00:00Z"),
        new(2004, "Arctic Loop", new [] { new[]{-5d, 86d}, new[]{5d, 89d} }, "2022-01-04T00:00:00Z"),
        new(2005, "Antarctic Traverse", new [] { new[]{-5d, -88d}, new[]{5d, -86d} }, "2022-01-05T00:00:00Z"),
    };
}
