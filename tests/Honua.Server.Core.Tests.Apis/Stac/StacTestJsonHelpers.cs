using System.Text.Json;
using System.Text.Json.Nodes;

namespace Honua.Server.Core.Tests.Apis.Stac;

internal static class StacTestJsonHelpers
{
    public static JsonObject ToJsonObject(object value)
    {
        return JsonSerializer.SerializeToNode(value) as JsonObject ?? new JsonObject();
    }

    public static string ToGeometry(object geometry)
    {
        return JsonSerializer.Serialize(geometry);
    }
}
