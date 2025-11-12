namespace Honua.Server.Core.Models.Drone;

/// <summary>
/// Result of LOD generation operation
/// </summary>
public class LodGenerationResult
{
    /// <summary>
    /// Whether the LOD generation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Array of LOD levels that were generated
    /// </summary>
    public int[] LevelsGenerated { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Message about the generation result
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
