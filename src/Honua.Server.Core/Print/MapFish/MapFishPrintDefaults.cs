// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Server.Core.Print.MapFish;

internal static class MapFishPrintDefaults
{
    public static IReadOnlyList<MapFishPrintApplicationDefinition> Create()
    {
        var applications = new List<MapFishPrintApplicationDefinition>();

        var mapAttribute = new MapFishPrintAttributeDefinition
        {
            Type = "MapAttributeValue",
            Required = true,
            Description = "Map frame definition including bbox, projection, dpi, and layers",
            ClientInfo = new MapFishMapAttributeClientInfo()
        };

        var titleAttribute = new MapFishPrintAttributeDefinition
        {
            Type = "String",
            Description = "Map title displayed above the map frame",
            Required = false
        };

        var subtitleAttribute = new MapFishPrintAttributeDefinition
        {
            Type = "String",
            Description = "Optional subtitle displayed below the title"
        };

        var notesAttribute = new MapFishPrintAttributeDefinition
        {
            Type = "String",
            Description = "Optional notes rendered below the map"
        };

        var portraitLayout = new MapFishPrintLayoutDefinition
        {
            Name = "A4 Portrait",
            Default = true,
            SupportsRotation = true,
            Page = MapFishPrintLayoutPageDefinition.A4Portrait(),
            Map = new MapFishPrintLayoutMapDefinition
            {
                WidthPixels = 512,
                HeightPixels = 512,
                OffsetX = 40f,
                OffsetY = 150f
            },
            Legend = MapFishPrintLayoutLegendDefinition.Disabled(),
            Title = new MapFishPrintLayoutTitleDefinition
            {
                OffsetX = 40f,
                OffsetY = 60f,
                TitleFontSize = 20f,
                SubtitleFontSize = 12f,
                Spacing = 6f
            },
            Scale = new MapFishPrintLayoutScaleDefinition
            {
                OffsetX = 40f,
                OffsetY = 120f,
                FontSize = 10f
            }
        };

        var landscapeLayout = new MapFishPrintLayoutDefinition
        {
            Name = "A4 Landscape",
            Default = false,
            SupportsRotation = true,
            Page = MapFishPrintLayoutPageDefinition.A4Landscape(),
            Map = new MapFishPrintLayoutMapDefinition
            {
                WidthPixels = 720,
                HeightPixels = 420,
                OffsetX = 40f,
                OffsetY = 140f
            },
            Legend = MapFishPrintLayoutLegendDefinition.Disabled(),
            Title = new MapFishPrintLayoutTitleDefinition
            {
                OffsetX = 40f,
                OffsetY = 60f,
                TitleFontSize = 20f,
                SubtitleFontSize = 12f,
                Spacing = 6f
            },
            Scale = new MapFishPrintLayoutScaleDefinition
            {
                OffsetX = 40f,
                OffsetY = 120f,
                FontSize = 10f
            }
        };

        var application = new MapFishPrintApplicationDefinition
        {
            Id = "default",
            Title = "Honua Default Print",
            Description = "Preconfigured MapFish-compatible print layouts for Honua deployments.",
            DefaultLayout = portraitLayout.Name,
            DefaultOutputFormat = "pdf",
            DefaultDpi = 150,
            Dpis = new List<int> { 96, 150, 300 },
            OutputFormats = new List<string> { "pdf" },
            Layouts = new List<MapFishPrintLayoutDefinition> { portraitLayout, landscapeLayout },
            Attributes =
            {
                ["map"] = mapAttribute,
                ["title"] = titleAttribute,
                ["subtitle"] = subtitleAttribute,
                ["notes"] = notesAttribute
            }
        };

        applications.Add(application);
        return applications;
    }
}
