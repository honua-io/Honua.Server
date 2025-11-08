// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Honua.Server.Core.LocationServices.Models;
using Honua.MapSDK.Services.Routing;

namespace Honua.MapSDK.Components.Routing;

public partial class RouteTurnByTurn
{
    [Parameter]
    public Route? Route { get; set; }

    [Parameter]
    public string? DestinationAddress { get; set; }

    [Parameter]
    public bool ShowNavigationControls { get; set; } = true;

    [Parameter]
    public bool ShowFilterOptions { get; set; } = true;

    [Parameter]
    public bool ShowCumulativeMetrics { get; set; } = false;

    [Parameter]
    public bool ShowPrintButton { get; set; } = true;

    [Parameter]
    public bool ShowShareButton { get; set; } = true;

    [Parameter]
    public bool ShowExportButton { get; set; } = true;

    [Parameter]
    public int InitialStepIndex { get; set; } = 0;

    [Parameter]
    public EventCallback<int> OnStepSelected { get; set; }

    [Parameter]
    public EventCallback<RouteInstruction> OnInstructionHighlighted { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    private List<RouteInstruction> Instructions { get; set; } = new();
    private int CurrentStepIndex { get; set; }
    private bool ShowAllSteps { get; set; } = true;
    private bool ShowDistances { get; set; } = true;
    private bool ShowExportMenu { get; set; }

    protected override void OnParametersSet()
    {
        if (Route?.Instructions != null)
        {
            Instructions = Route.Instructions.ToList();
        }
        else
        {
            Instructions.Clear();
        }

        CurrentStepIndex = InitialStepIndex;
    }

    private void SelectInstruction(int index)
    {
        CurrentStepIndex = index;
        OnStepSelected.InvokeAsync(index);

        if (index >= 0 && index < Instructions.Count)
        {
            OnInstructionHighlighted.InvokeAsync(Instructions[index]);
        }
    }

    private void HighlightInstruction(int index, bool highlight)
    {
        if (highlight && index >= 0 && index < Instructions.Count)
        {
            OnInstructionHighlighted.InvokeAsync(Instructions[index]);
        }
    }

    private void PreviousStep()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            SelectInstruction(CurrentStepIndex);
        }
    }

    private void NextStep()
    {
        if (CurrentStepIndex < Instructions.Count - 1)
        {
            CurrentStepIndex++;
            SelectInstruction(CurrentStepIndex);
        }
    }

    private async Task PrintDirections()
    {
        if (Route == null) return;

        var html = ExportService.GeneratePrintableHtml(Route, "Turn-by-turn Directions");
        await JSRuntime.InvokeVoidAsync("eval",
            $"var w = window.open(); w.document.write({System.Text.Json.JsonSerializer.Serialize(html)}); w.document.close();");
    }

    private async Task ShareDirections()
    {
        // Generate share text
        var shareText = GenerateShareText();
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", shareText);
        await JSRuntime.InvokeVoidAsync("alert", "Directions copied to clipboard!");
    }

    private void ToggleExportMenu()
    {
        ShowExportMenu = !ShowExportMenu;
    }

    private async Task ExportGpx()
    {
        if (Route == null) return;
        var coordinates = ParseRouteCoordinates();
        var gpx = ExportService.ExportToGpx(Route, coordinates, "Route Directions");
        await DownloadFile("directions.gpx", gpx, "application/gpx+xml");
        ShowExportMenu = false;
    }

    private async Task ExportKml()
    {
        if (Route == null) return;
        var coordinates = ParseRouteCoordinates();
        var kml = ExportService.ExportToKml(Route, coordinates, "Route Directions");
        await DownloadFile("directions.kml", kml, "application/vnd.google-earth.kml+xml");
        ShowExportMenu = false;
    }

    private async Task ExportGeoJson()
    {
        if (Route == null) return;
        var coordinates = ParseRouteCoordinates();
        var geoJson = ExportService.ExportToGeoJson(Route, coordinates, "Route Directions");
        await DownloadFile("directions.geojson", geoJson, "application/geo+json");
        ShowExportMenu = false;
    }

    private async Task ExportCsv()
    {
        if (Route == null || Instructions.Count == 0) return;

        var csv = new StringBuilder();
        csv.AppendLine("Step,Maneuver,Instruction,Road,Distance (m),Duration (s)");

        for (int i = 0; i < Instructions.Count; i++)
        {
            var instr = Instructions[i];
            csv.AppendLine($"{i + 1}," +
                          $"\"{instr.ManeuverType ?? ""}\"," +
                          $"\"{instr.Text.Replace("\"", "\"\"")}\"," +
                          $"\"{instr.RoadName ?? ""}\"," +
                          $"{instr.DistanceMeters}," +
                          $"{instr.DurationSeconds}");
        }

        await DownloadFile("directions.csv", csv.ToString(), "text/csv");
        ShowExportMenu = false;
    }

    private List<double[]> ParseRouteCoordinates()
    {
        // Extract coordinates from instructions if available
        if (Instructions.Count > 0)
        {
            return Instructions
                .Where(i => i.Location != null)
                .Select(i => i.Location!)
                .ToList();
        }

        // Fallback to empty list
        return new List<double[]>();
    }

    private string GenerateShareText()
    {
        if (Route == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine("Route Directions");
        sb.AppendLine($"Distance: {FormatDistance(Route.DistanceMeters)}");
        sb.AppendLine($"Duration: {FormatDuration(Route.DurationSeconds)}");
        sb.AppendLine();

        for (int i = 0; i < Instructions.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {Instructions[i].Text}");
        }

        return sb.ToString();
    }

    private async Task DownloadFile(string filename, string content, string mimeType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var base64 = Convert.ToBase64String(bytes);

        await JSRuntime.InvokeVoidAsync("eval", $@"
            var link = document.createElement('a');
            link.download = '{filename}';
            link.href = 'data:{mimeType};base64,{base64}';
            link.click();
        ");
    }

    private string GetManeuverText(string? maneuverType) => maneuverType?.ToLowerInvariant() switch
    {
        "turn-left" => "Turn left",
        "turn-right" => "Turn right",
        "sharp-left" => "Sharp left",
        "sharp-right" => "Sharp right",
        "slight-left" => "Slight left",
        "slight-right" => "Slight right",
        "straight" => "Continue straight",
        "u-turn" => "Make a U-turn",
        "roundabout-left" => "Take roundabout left",
        "roundabout-right" => "Take roundabout right",
        "merge" => "Merge",
        "fork-left" => "Keep left at fork",
        "fork-right" => "Keep right at fork",
        "ramp-left" => "Take ramp left",
        "ramp-right" => "Take ramp right",
        "arrive" => "Arrive",
        "depart" => "Depart",
        "ferry" => "Take ferry",
        _ => "Continue"
    };

    private string FormatDistance(double meters)
    {
        if (meters < 1000)
            return $"{meters:F0} m";
        return $"{meters / 1000:F1} km";
    }

    private string FormatDuration(double seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.TotalMinutes} min";
        return $"{(int)seconds} sec";
    }
}
