using System.Text.Json;
using System.Text.Json.Nodes;

namespace Honua.Tools.DataSeeder;

internal readonly record struct FeatureDefinition(int Id, string Name, double[][] Coordinates, string ObservedAt)
{
    public string ToGeoJson() => JsonSerializer.Serialize(new JsonObject
    {
        ["type"] = "LineString",
        ["coordinates"] = JsonSerializer.SerializeToNode(Coordinates)
    });
}
