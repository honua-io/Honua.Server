namespace Honua.MapSDK.Components.Chart;

/// <summary>
/// Event args for chart segment clicked event
/// </summary>
public class ChartSegmentClickedEventArgs
{
    public required string Label { get; init; }
    public required double Value { get; init; }
    public required int Index { get; init; }
    public required string Field { get; init; }
}
