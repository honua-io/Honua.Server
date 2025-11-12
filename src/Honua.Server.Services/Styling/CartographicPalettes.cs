// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Services.Styling;

/// <summary>
/// Professional color palettes for cartographic visualization based on ColorBrewer schemes.
/// All palettes are designed to be colorblind-safe and print-friendly where possible.
/// </summary>
public static class CartographicPalettes
{
    /// <summary>
    /// Get a palette by name and number of classes
    /// </summary>
    public static string[] GetPalette(string name, int classes)
    {
        if (classes < 3) classes = 3;
        if (classes > 12) classes = 12;

        return name.ToLowerInvariant() switch
        {
            // Sequential - Single Hue
            "blues" => Blues[classes],
            "greens" => Greens[classes],
            "oranges" => Oranges[classes],
            "reds" => Reds[classes],
            "purples" => Purples[classes],

            // Sequential - Multi Hue
            "ylgn" or "yellowgreen" => YellowGreen[classes],
            "ylgnbu" or "yellowgreenblue" => YellowGreenBlue[classes],
            "gnbu" or "greenblue" => GreenBlue[classes],
            "bupu" or "bluepurple" => BluePurple[classes],
            "orrd" or "orangered" => OrangeRed[classes],
            "ylorrd" or "yelloworangered" => YellowOrangeRed[classes],

            // Diverging
            "rdylgn" or "redyellowgreen" => RedYellowGreen[classes],
            "spectral" => Spectral[classes],
            "rdbu" or "redblue" => RedBlue[classes],
            "brbg" or "brownteal" => BrownTeal[classes],
            "prgn" or "purplegreen" => PurpleGreen[classes],

            // Qualitative
            "set1" => Set1[Math.Min(classes, 9)],
            "set2" => Set2[Math.Min(classes, 8)],
            "set3" => Set3[classes],
            "paired" => Paired[classes],
            "accent" => Accent[Math.Min(classes, 8)],

            // Context-specific
            "landuse" => LandUse[classes],
            "demographics" => Demographics[classes],
            "environmental" => Environmental[classes],
            "traffic" => Traffic[Math.Min(classes, 7)],

            _ => Blues[classes]
        };
    }

    /// <summary>
    /// Get recommended palette for data type
    /// </summary>
    public static string GetRecommendedPalette(DataClassification classification, int classes = 7)
    {
        return classification switch
        {
            DataClassification.Sequential => classes <= 7 ? "Blues" : "YellowGreenBlue",
            DataClassification.Diverging => "Spectral",
            DataClassification.Categorical => classes <= 9 ? "Set1" : "Paired",
            DataClassification.Temporal => "YlGnBu",
            DataClassification.LandUse => "LandUse",
            DataClassification.Demographics => "Demographics",
            DataClassification.Environmental => "Environmental",
            _ => "Blues"
        };
    }

    #region Sequential - Single Hue

    public static readonly Dictionary<int, string[]> Blues = new()
    {
        [3] = new[] { "#DEEBF7", "#9ECAE1", "#3182BD" },
        [4] = new[] { "#EFF3FF", "#BDD7E7", "#6BAED6", "#2171B5" },
        [5] = new[] { "#EFF3FF", "#BDD7E7", "#6BAED6", "#3182BD", "#08519C" },
        [6] = new[] { "#EFF3FF", "#C6DBEF", "#9ECAE1", "#6BAED6", "#3182BD", "#08519C" },
        [7] = new[] { "#EFF3FF", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#084594" },
        [8] = new[] { "#F7FBFF", "#DEEBF7", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#084594" },
        [9] = new[] { "#F7FBFF", "#DEEBF7", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#08519C", "#08306B" },
        [10] = new[] { "#F7FBFF", "#E3EEF7", "#DEEBF7", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#08519C", "#08306B" },
        [11] = new[] { "#F7FBFF", "#E3EEF7", "#DEEBF7", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#084594", "#08519C", "#08306B" },
        [12] = new[] { "#F7FBFF", "#E3EEF7", "#DEEBF7", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#084594", "#08519C", "#063066", "#08306B" }
    };

    public static readonly Dictionary<int, string[]> Greens = new()
    {
        [3] = new[] { "#E5F5E0", "#A1D99B", "#31A354" },
        [4] = new[] { "#EDF8E9", "#BAE4B3", "#74C476", "#238B45" },
        [5] = new[] { "#EDF8E9", "#BAE4B3", "#74C476", "#31A354", "#006D2C" },
        [6] = new[] { "#EDF8E9", "#C7E9C0", "#A1D99B", "#74C476", "#31A354", "#006D2C" },
        [7] = new[] { "#EDF8E9", "#C7E9C0", "#A1D99B", "#74C476", "#41AB5D", "#238B45", "#005A32" },
        [8] = new[] { "#F7FCF5", "#E5F5E0", "#C7E9C0", "#A1D99B", "#74C476", "#41AB5D", "#238B45", "#005A32" },
        [9] = new[] { "#F7FCF5", "#E5F5E0", "#C7E9C0", "#A1D99B", "#74C476", "#41AB5D", "#238B45", "#006D2C", "#00441B" },
        [10] = new[] { "#F7FCF5", "#ECFAEC", "#E5F5E0", "#C7E9C0", "#A1D99B", "#74C476", "#41AB5D", "#238B45", "#006D2C", "#00441B" },
        [11] = new[] { "#F7FCF5", "#ECFAEC", "#E5F5E0", "#C7E9C0", "#A1D99B", "#74C476", "#41AB5D", "#238B45", "#005A32", "#006D2C", "#00441B" },
        [12] = new[] { "#F7FCF5", "#ECFAEC", "#E5F5E0", "#C7E9C0", "#A1D99B", "#74C476", "#41AB5D", "#238B45", "#005A32", "#006D2C", "#003F1B", "#00441B" }
    };

    public static readonly Dictionary<int, string[]> Oranges = new()
    {
        [3] = new[] { "#FEE6CE", "#FDAE6B", "#E6550D" },
        [4] = new[] { "#FEEDDE", "#FDBE85", "#FD8D3C", "#D94701" },
        [5] = new[] { "#FEEDDE", "#FDBE85", "#FD8D3C", "#E6550D", "#A63603" },
        [6] = new[] { "#FEEDDE", "#FDD0A2", "#FDAE6B", "#FD8D3C", "#E6550D", "#A63603" },
        [7] = new[] { "#FEEDDE", "#FDD0A2", "#FDAE6B", "#FD8D3C", "#F16913", "#D94801", "#8C2D04" },
        [8] = new[] { "#FFF5EB", "#FEE6CE", "#FDD0A2", "#FDAE6B", "#FD8D3C", "#F16913", "#D94801", "#8C2D04" },
        [9] = new[] { "#FFF5EB", "#FEE6CE", "#FDD0A2", "#FDAE6B", "#FD8D3C", "#F16913", "#D94801", "#A63603", "#7F2704" },
        [10] = new[] { "#FFF5EB", "#FFF0E1", "#FEE6CE", "#FDD0A2", "#FDAE6B", "#FD8D3C", "#F16913", "#D94801", "#A63603", "#7F2704" },
        [11] = new[] { "#FFF5EB", "#FFF0E1", "#FEE6CE", "#FDD0A2", "#FDAE6B", "#FD8D3C", "#F16913", "#D94801", "#8C2D04", "#A63603", "#7F2704" },
        [12] = new[] { "#FFF5EB", "#FFF0E1", "#FEE6CE", "#FDD0A2", "#FDAE6B", "#FD8D3C", "#F16913", "#D94801", "#8C2D04", "#A63603", "#7A2704", "#7F2704" }
    };

    public static readonly Dictionary<int, string[]> Reds = new()
    {
        [3] = new[] { "#FEE0D2", "#FC9272", "#DE2D26" },
        [4] = new[] { "#FEE5D9", "#FCAE91", "#FB6A4A", "#CB181D" },
        [5] = new[] { "#FEE5D9", "#FCAE91", "#FB6A4A", "#DE2D26", "#A50F15" },
        [6] = new[] { "#FEE5D9", "#FCBBA1", "#FC9272", "#FB6A4A", "#DE2D26", "#A50F15" },
        [7] = new[] { "#FEE5D9", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D" },
        [8] = new[] { "#FFF5F0", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D" },
        [9] = new[] { "#FFF5F0", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#A50F15", "#67000D" },
        [10] = new[] { "#FFF5F0", "#FFF0EA", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#A50F15", "#67000D" },
        [11] = new[] { "#FFF5F0", "#FFF0EA", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D", "#A50F15", "#67000D" },
        [12] = new[] { "#FFF5F0", "#FFF0EA", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D", "#A50F15", "#600D0D", "#67000D" }
    };

    public static readonly Dictionary<int, string[]> Purples = new()
    {
        [3] = new[] { "#EFEDF5", "#BCBDDC", "#756BB1" },
        [4] = new[] { "#F2F0F7", "#CBC9E2", "#9E9AC8", "#6A51A3" },
        [5] = new[] { "#F2F0F7", "#CBC9E2", "#9E9AC8", "#756BB1", "#54278F" },
        [6] = new[] { "#F2F0F7", "#DADAEB", "#BCBDDC", "#9E9AC8", "#756BB1", "#54278F" },
        [7] = new[] { "#F2F0F7", "#DADAEB", "#BCBDDC", "#9E9AC8", "#807DBA", "#6A51A3", "#4A1486" },
        [8] = new[] { "#FCFBFD", "#EFEDF5", "#DADAEB", "#BCBDDC", "#9E9AC8", "#807DBA", "#6A51A3", "#4A1486" },
        [9] = new[] { "#FCFBFD", "#EFEDF5", "#DADAEB", "#BCBDDC", "#9E9AC8", "#807DBA", "#6A51A3", "#54278F", "#3F007D" },
        [10] = new[] { "#FCFBFD", "#F8F7FB", "#EFEDF5", "#DADAEB", "#BCBDDC", "#9E9AC8", "#807DBA", "#6A51A3", "#54278F", "#3F007D" },
        [11] = new[] { "#FCFBFD", "#F8F7FB", "#EFEDF5", "#DADAEB", "#BCBDDC", "#9E9AC8", "#807DBA", "#6A51A3", "#4A1486", "#54278F", "#3F007D" },
        [12] = new[] { "#FCFBFD", "#F8F7FB", "#EFEDF5", "#DADAEB", "#BCBDDC", "#9E9AC8", "#807DBA", "#6A51A3", "#4A1486", "#54278F", "#37007D", "#3F007D" }
    };

    #endregion

    #region Sequential - Multi Hue

    public static readonly Dictionary<int, string[]> YellowGreen = new()
    {
        [3] = new[] { "#F7FCB9", "#ADDD8E", "#31A354" },
        [4] = new[] { "#FFFFCC", "#C2E699", "#78C679", "#238B45" },
        [5] = new[] { "#FFFFCC", "#C2E699", "#78C679", "#31A354", "#006837" },
        [6] = new[] { "#FFFFCC", "#D9F0A3", "#ADDD8E", "#78C679", "#31A354", "#006837" },
        [7] = new[] { "#FFFFCC", "#D9F0A3", "#ADDD8E", "#78C679", "#41AB5D", "#238B45", "#005A32" },
        [8] = new[] { "#FFFFE5", "#F7FCB9", "#D9F0A3", "#ADDD8E", "#78C679", "#41AB5D", "#238B45", "#005A32" },
        [9] = new[] { "#FFFFE5", "#F7FCB9", "#D9F0A3", "#ADDD8E", "#78C679", "#41AB5D", "#238B45", "#006837", "#004529" },
        [10] = new[] { "#FFFFE5", "#FFFFD9", "#F7FCB9", "#D9F0A3", "#ADDD8E", "#78C679", "#41AB5D", "#238B45", "#006837", "#004529" },
        [11] = new[] { "#FFFFE5", "#FFFFD9", "#F7FCB9", "#D9F0A3", "#ADDD8E", "#78C679", "#41AB5D", "#238B45", "#005A32", "#006837", "#004529" },
        [12] = new[] { "#FFFFE5", "#FFFFD9", "#F7FCB9", "#D9F0A3", "#ADDD8E", "#78C679", "#41AB5D", "#238B45", "#005A32", "#006837", "#004029", "#004529" }
    };

    public static readonly Dictionary<int, string[]> YellowGreenBlue = new()
    {
        [3] = new[] { "#EDF8B1", "#7FCDBB", "#2C7FB8" },
        [4] = new[] { "#FFFFCC", "#A1DAB4", "#41B6C4", "#225EA8" },
        [5] = new[] { "#FFFFCC", "#A1DAB4", "#41B6C4", "#2C7FB8", "#253494" },
        [6] = new[] { "#FFFFCC", "#C7E9B4", "#7FCDBB", "#41B6C4", "#2C7FB8", "#253494" },
        [7] = new[] { "#FFFFCC", "#C7E9B4", "#7FCDBB", "#41B6C4", "#1D91C0", "#225EA8", "#0C2C84" },
        [8] = new[] { "#FFFFD9", "#EDF8B1", "#C7E9B4", "#7FCDBB", "#41B6C4", "#1D91C0", "#225EA8", "#0C2C84" },
        [9] = new[] { "#FFFFD9", "#EDF8B1", "#C7E9B4", "#7FCDBB", "#41B6C4", "#1D91C0", "#225EA8", "#253494", "#081D58" },
        [10] = new[] { "#FFFFD9", "#FFFFF0", "#EDF8B1", "#C7E9B4", "#7FCDBB", "#41B6C4", "#1D91C0", "#225EA8", "#253494", "#081D58" },
        [11] = new[] { "#FFFFD9", "#FFFFF0", "#EDF8B1", "#C7E9B4", "#7FCDBB", "#41B6C4", "#1D91C0", "#225EA8", "#0C2C84", "#253494", "#081D58" },
        [12] = new[] { "#FFFFD9", "#FFFFF0", "#EDF8B1", "#C7E9B4", "#7FCDBB", "#41B6C4", "#1D91C0", "#225EA8", "#0C2C84", "#253494", "#071858", "#081D58" }
    };

    public static readonly Dictionary<int, string[]> GreenBlue = new()
    {
        [3] = new[] { "#E0F3DB", "#A8DDB5", "#43A2CA" },
        [4] = new[] { "#F0F9E8", "#BAE4BC", "#7BCCC4", "#2B8CBE" },
        [5] = new[] { "#F0F9E8", "#BAE4BC", "#7BCCC4", "#43A2CA", "#0868AC" },
        [6] = new[] { "#F0F9E8", "#CCEBC5", "#A8DDB5", "#7BCCC4", "#43A2CA", "#0868AC" },
        [7] = new[] { "#F0F9E8", "#CCEBC5", "#A8DDB5", "#7BCCC4", "#4EB3D3", "#2B8CBE", "#08589E" },
        [8] = new[] { "#F7FCF0", "#E0F3DB", "#CCEBC5", "#A8DDB5", "#7BCCC4", "#4EB3D3", "#2B8CBE", "#08589E" },
        [9] = new[] { "#F7FCF0", "#E0F3DB", "#CCEBC5", "#A8DDB5", "#7BCCC4", "#4EB3D3", "#2B8CBE", "#0868AC", "#084081" },
        [10] = new[] { "#F7FCF0", "#EEF9E8", "#E0F3DB", "#CCEBC5", "#A8DDB5", "#7BCCC4", "#4EB3D3", "#2B8CBE", "#0868AC", "#084081" },
        [11] = new[] { "#F7FCF0", "#EEF9E8", "#E0F3DB", "#CCEBC5", "#A8DDB5", "#7BCCC4", "#4EB3D3", "#2B8CBE", "#08589E", "#0868AC", "#084081" },
        [12] = new[] { "#F7FCF0", "#EEF9E8", "#E0F3DB", "#CCEBC5", "#A8DDB5", "#7BCCC4", "#4EB3D3", "#2B8CBE", "#08589E", "#0868AC", "#073B81", "#084081" }
    };

    public static readonly Dictionary<int, string[]> BluePurple = new()
    {
        [3] = new[] { "#E0ECF4", "#9EBCDA", "#8856A7" },
        [4] = new[] { "#EDF8FB", "#B3CDE3", "#8C96C6", "#88419D" },
        [5] = new[] { "#EDF8FB", "#B3CDE3", "#8C96C6", "#8856A7", "#810F7C" },
        [6] = new[] { "#EDF8FB", "#BFD3E6", "#9EBCDA", "#8C96C6", "#8856A7", "#810F7C" },
        [7] = new[] { "#EDF8FB", "#BFD3E6", "#9EBCDA", "#8C96C6", "#8C6BB1", "#88419D", "#6E016B" },
        [8] = new[] { "#F7FCFD", "#E0ECF4", "#BFD3E6", "#9EBCDA", "#8C96C6", "#8C6BB1", "#88419D", "#6E016B" },
        [9] = new[] { "#F7FCFD", "#E0ECF4", "#BFD3E6", "#9EBCDA", "#8C96C6", "#8C6BB1", "#88419D", "#810F7C", "#4D004B" },
        [10] = new[] { "#F7FCFD", "#EEF3F9", "#E0ECF4", "#BFD3E6", "#9EBCDA", "#8C96C6", "#8C6BB1", "#88419D", "#810F7C", "#4D004B" },
        [11] = new[] { "#F7FCFD", "#EEF3F9", "#E0ECF4", "#BFD3E6", "#9EBCDA", "#8C96C6", "#8C6BB1", "#88419D", "#6E016B", "#810F7C", "#4D004B" },
        [12] = new[] { "#F7FCFD", "#EEF3F9", "#E0ECF4", "#BFD3E6", "#9EBCDA", "#8C96C6", "#8C6BB1", "#88419D", "#6E016B", "#810F7C", "#44004B", "#4D004B" }
    };

    public static readonly Dictionary<int, string[]> OrangeRed = new()
    {
        [3] = new[] { "#FEE8C8", "#FDBB84", "#E34A33" },
        [4] = new[] { "#FEF0D9", "#FDCC8A", "#FC8D59", "#D7301F" },
        [5] = new[] { "#FEF0D9", "#FDCC8A", "#FC8D59", "#E34A33", "#B30000" },
        [6] = new[] { "#FEF0D9", "#FDD49E", "#FDBB84", "#FC8D59", "#E34A33", "#B30000" },
        [7] = new[] { "#FEF0D9", "#FDD49E", "#FDBB84", "#FC8D59", "#EF6548", "#D7301F", "#990000" },
        [8] = new[] { "#FFF7EC", "#FEE8C8", "#FDD49E", "#FDBB84", "#FC8D59", "#EF6548", "#D7301F", "#990000" },
        [9] = new[] { "#FFF7EC", "#FEE8C8", "#FDD49E", "#FDBB84", "#FC8D59", "#EF6548", "#D7301F", "#B30000", "#7F0000" },
        [10] = new[] { "#FFF7EC", "#FFF3E0", "#FEE8C8", "#FDD49E", "#FDBB84", "#FC8D59", "#EF6548", "#D7301F", "#B30000", "#7F0000" },
        [11] = new[] { "#FFF7EC", "#FFF3E0", "#FEE8C8", "#FDD49E", "#FDBB84", "#FC8D59", "#EF6548", "#D7301F", "#990000", "#B30000", "#7F0000" },
        [12] = new[] { "#FFF7EC", "#FFF3E0", "#FEE8C8", "#FDD49E", "#FDBB84", "#FC8D59", "#EF6548", "#D7301F", "#990000", "#B30000", "#750000", "#7F0000" }
    };

    public static readonly Dictionary<int, string[]> YellowOrangeRed = new()
    {
        [3] = new[] { "#FFEDA0", "#FD8D3C", "#E31A1C" },
        [4] = new[] { "#FFFFB2", "#FECC5C", "#FD8D3C", "#E31A1C" },
        [5] = new[] { "#FFFFB2", "#FECC5C", "#FD8D3C", "#F03B20", "#BD0026" },
        [6] = new[] { "#FFFFB2", "#FED976", "#FEB24C", "#FD8D3C", "#F03B20", "#BD0026" },
        [7] = new[] { "#FFFFB2", "#FED976", "#FEB24C", "#FD8D3C", "#FC4E2A", "#E31A1C", "#B10026" },
        [8] = new[] { "#FFFFCC", "#FFEDA0", "#FED976", "#FEB24C", "#FD8D3C", "#FC4E2A", "#E31A1C", "#B10026" },
        [9] = new[] { "#FFFFCC", "#FFEDA0", "#FED976", "#FEB24C", "#FD8D3C", "#FC4E2A", "#E31A1C", "#BD0026", "#800026" },
        [10] = new[] { "#FFFFCC", "#FFFFC2", "#FFEDA0", "#FED976", "#FEB24C", "#FD8D3C", "#FC4E2A", "#E31A1C", "#BD0026", "#800026" },
        [11] = new[] { "#FFFFCC", "#FFFFC2", "#FFEDA0", "#FED976", "#FEB24C", "#FD8D3C", "#FC4E2A", "#E31A1C", "#B10026", "#BD0026", "#800026" },
        [12] = new[] { "#FFFFCC", "#FFFFC2", "#FFEDA0", "#FED976", "#FEB24C", "#FD8D3C", "#FC4E2A", "#E31A1C", "#B10026", "#BD0026", "#750026", "#800026" }
    };

    #endregion

    #region Diverging

    public static readonly Dictionary<int, string[]> RedYellowGreen = new()
    {
        [3] = new[] { "#FC8D59", "#FFFFBF", "#91CF60" },
        [4] = new[] { "#D7191C", "#FDAE61", "#A6D96A", "#1A9641" },
        [5] = new[] { "#D7191C", "#FDAE61", "#FFFFBF", "#A6D96A", "#1A9641" },
        [6] = new[] { "#D73027", "#FC8D59", "#FEE08B", "#D9EF8B", "#91CF60", "#1A9850" },
        [7] = new[] { "#D73027", "#FC8D59", "#FEE08B", "#FFFFBF", "#D9EF8B", "#91CF60", "#1A9850" },
        [8] = new[] { "#D73027", "#F46D43", "#FDAE61", "#FEE08B", "#D9EF8B", "#A6D96A", "#66BD63", "#1A9850" },
        [9] = new[] { "#D73027", "#F46D43", "#FDAE61", "#FEE08B", "#FFFFBF", "#D9EF8B", "#A6D96A", "#66BD63", "#1A9850" },
        [10] = new[] { "#A50026", "#D73027", "#F46D43", "#FDAE61", "#FEE08B", "#D9EF8B", "#A6D96A", "#66BD63", "#1A9850", "#006837" },
        [11] = new[] { "#A50026", "#D73027", "#F46D43", "#FDAE61", "#FEE08B", "#FFFFBF", "#D9EF8B", "#A6D96A", "#66BD63", "#1A9850", "#006837" },
        [12] = new[] { "#A50026", "#D73027", "#F46D43", "#FDAE61", "#FEE08B", "#FFFFBF", "#D9EF8B", "#A6D96A", "#66BD63", "#1A9850", "#006837", "#003F1A" }
    };

    public static readonly Dictionary<int, string[]> Spectral = new()
    {
        [3] = new[] { "#FC8D59", "#FFFFBF", "#99D594" },
        [4] = new[] { "#D7191C", "#FDAE61", "#ABDDA4", "#2B83BA" },
        [5] = new[] { "#D7191C", "#FDAE61", "#FFFFBF", "#ABDDA4", "#2B83BA" },
        [6] = new[] { "#D53E4F", "#FC8D59", "#FEE08B", "#E6F598", "#99D594", "#3288BD" },
        [7] = new[] { "#D53E4F", "#FC8D59", "#FEE08B", "#FFFFBF", "#E6F598", "#99D594", "#3288BD" },
        [8] = new[] { "#D53E4F", "#F46D43", "#FDAE61", "#FEE08B", "#E6F598", "#ABDDA4", "#66C2A5", "#3288BD" },
        [9] = new[] { "#D53E4F", "#F46D43", "#FDAE61", "#FEE08B", "#FFFFBF", "#E6F598", "#ABDDA4", "#66C2A5", "#3288BD" },
        [10] = new[] { "#9E0142", "#D53E4F", "#F46D43", "#FDAE61", "#FEE08B", "#E6F598", "#ABDDA4", "#66C2A5", "#3288BD", "#5E4FA2" },
        [11] = new[] { "#9E0142", "#D53E4F", "#F46D43", "#FDAE61", "#FEE08B", "#FFFFBF", "#E6F598", "#ABDDA4", "#66C2A5", "#3288BD", "#5E4FA2" },
        [12] = new[] { "#9E0142", "#D53E4F", "#F46D43", "#FDAE61", "#FEE08B", "#FFFFBF", "#E6F598", "#ABDDA4", "#66C2A5", "#3288BD", "#5E4FA2", "#462D91" }
    };

    public static readonly Dictionary<int, string[]> RedBlue = new()
    {
        [3] = new[] { "#EF8A62", "#F7F7F7", "#67A9CF" },
        [4] = new[] { "#CA0020", "#F4A582", "#92C5DE", "#0571B0" },
        [5] = new[] { "#CA0020", "#F4A582", "#F7F7F7", "#92C5DE", "#0571B0" },
        [6] = new[] { "#B2182B", "#EF8A62", "#FDDBC7", "#D1E5F0", "#67A9CF", "#2166AC" },
        [7] = new[] { "#B2182B", "#EF8A62", "#FDDBC7", "#F7F7F7", "#D1E5F0", "#67A9CF", "#2166AC" },
        [8] = new[] { "#B2182B", "#D6604D", "#F4A582", "#FDDBC7", "#D1E5F0", "#92C5DE", "#4393C3", "#2166AC" },
        [9] = new[] { "#B2182B", "#D6604D", "#F4A582", "#FDDBC7", "#F7F7F7", "#D1E5F0", "#92C5DE", "#4393C3", "#2166AC" },
        [10] = new[] { "#67001F", "#B2182B", "#D6604D", "#F4A582", "#FDDBC7", "#D1E5F0", "#92C5DE", "#4393C3", "#2166AC", "#053061" },
        [11] = new[] { "#67001F", "#B2182B", "#D6604D", "#F4A582", "#FDDBC7", "#F7F7F7", "#D1E5F0", "#92C5DE", "#4393C3", "#2166AC", "#053061" },
        [12] = new[] { "#67001F", "#B2182B", "#D6604D", "#F4A582", "#FDDBC7", "#F7F7F7", "#D1E5F0", "#92C5DE", "#4393C3", "#2166AC", "#053061", "#042451" }
    };

    public static readonly Dictionary<int, string[]> BrownTeal = new()
    {
        [3] = new[] { "#D8B365", "#F5F5F5", "#5AB4AC" },
        [4] = new[] { "#A6611A", "#DFC27D", "#80CDC1", "#018571" },
        [5] = new[] { "#A6611A", "#DFC27D", "#F5F5F5", "#80CDC1", "#018571" },
        [6] = new[] { "#8C510A", "#D8B365", "#F6E8C3", "#C7EAE5", "#5AB4AC", "#01665E" },
        [7] = new[] { "#8C510A", "#D8B365", "#F6E8C3", "#F5F5F5", "#C7EAE5", "#5AB4AC", "#01665E" },
        [8] = new[] { "#8C510A", "#BF812D", "#DFC27D", "#F6E8C3", "#C7EAE5", "#80CDC1", "#35978F", "#01665E" },
        [9] = new[] { "#8C510A", "#BF812D", "#DFC27D", "#F6E8C3", "#F5F5F5", "#C7EAE5", "#80CDC1", "#35978F", "#01665E" },
        [10] = new[] { "#543005", "#8C510A", "#BF812D", "#DFC27D", "#F6E8C3", "#C7EAE5", "#80CDC1", "#35978F", "#01665E", "#003C30" },
        [11] = new[] { "#543005", "#8C510A", "#BF812D", "#DFC27D", "#F6E8C3", "#F5F5F5", "#C7EAE5", "#80CDC1", "#35978F", "#01665E", "#003C30" },
        [12] = new[] { "#543005", "#8C510A", "#BF812D", "#DFC27D", "#F6E8C3", "#F5F5F5", "#C7EAE5", "#80CDC1", "#35978F", "#01665E", "#003C30", "#002D25" }
    };

    public static readonly Dictionary<int, string[]> PurpleGreen = new()
    {
        [3] = new[] { "#AF8DC3", "#F7F7F7", "#7FBF7B" },
        [4] = new[] { "#7B3294", "#C2A5CF", "#A6DBA0", "#008837" },
        [5] = new[] { "#7B3294", "#C2A5CF", "#F7F7F7", "#A6DBA0", "#008837" },
        [6] = new[] { "#762A83", "#AF8DC3", "#E7D4E8", "#D9F0D3", "#7FBF7B", "#1B7837" },
        [7] = new[] { "#762A83", "#AF8DC3", "#E7D4E8", "#F7F7F7", "#D9F0D3", "#7FBF7B", "#1B7837" },
        [8] = new[] { "#762A83", "#9970AB", "#C2A5CF", "#E7D4E8", "#D9F0D3", "#A6DBA0", "#5AAE61", "#1B7837" },
        [9] = new[] { "#762A83", "#9970AB", "#C2A5CF", "#E7D4E8", "#F7F7F7", "#D9F0D3", "#A6DBA0", "#5AAE61", "#1B7837" },
        [10] = new[] { "#40004B", "#762A83", "#9970AB", "#C2A5CF", "#E7D4E8", "#D9F0D3", "#A6DBA0", "#5AAE61", "#1B7837", "#00441B" },
        [11] = new[] { "#40004B", "#762A83", "#9970AB", "#C2A5CF", "#E7D4E8", "#F7F7F7", "#D9F0D3", "#A6DBA0", "#5AAE61", "#1B7837", "#00441B" },
        [12] = new[] { "#40004B", "#762A83", "#9970AB", "#C2A5CF", "#E7D4E8", "#F7F7F7", "#D9F0D3", "#A6DBA0", "#5AAE61", "#1B7837", "#00441B", "#00361B" }
    };

    #endregion

    #region Qualitative

    public static readonly Dictionary<int, string[]> Set1 = new()
    {
        [3] = new[] { "#E41A1C", "#377EB8", "#4DAF4A" },
        [4] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3" },
        [5] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00" },
        [6] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33" },
        [7] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33", "#A65628" },
        [8] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33", "#A65628", "#F781BF" },
        [9] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33", "#A65628", "#F781BF", "#999999" },
        [10] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33", "#A65628", "#F781BF", "#999999", "#66C2A5" },
        [11] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33", "#A65628", "#F781BF", "#999999", "#66C2A5", "#FC8D62" },
        [12] = new[] { "#E41A1C", "#377EB8", "#4DAF4A", "#984EA3", "#FF7F00", "#FFFF33", "#A65628", "#F781BF", "#999999", "#66C2A5", "#FC8D62", "#8DA0CB" }
    };

    public static readonly Dictionary<int, string[]> Set2 = new()
    {
        [3] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB" },
        [4] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3" },
        [5] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854" },
        [6] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F" },
        [7] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F", "#E5C494" },
        [8] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F", "#E5C494", "#B3B3B3" },
        [9] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F", "#E5C494", "#B3B3B3", "#8DD3C7" },
        [10] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F", "#E5C494", "#B3B3B3", "#8DD3C7", "#FFFFB3" },
        [11] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F", "#E5C494", "#B3B3B3", "#8DD3C7", "#FFFFB3", "#BEBADA" },
        [12] = new[] { "#66C2A5", "#FC8D62", "#8DA0CB", "#E78AC3", "#A6D854", "#FFD92F", "#E5C494", "#B3B3B3", "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072" }
    };

    public static readonly Dictionary<int, string[]> Set3 = new()
    {
        [3] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA" },
        [4] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072" },
        [5] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3" },
        [6] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3", "#FDB462" },
        [7] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3", "#FDB462", "#B3DE69" },
        [8] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3", "#FDB462", "#B3DE69", "#FCCDE5" },
        [9] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3", "#FDB462", "#B3DE69", "#FCCDE5", "#D9D9D9" },
        [10] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3", "#FDB462", "#B3DE69", "#FCCDE5", "#D9D9D9", "#BC80BD" },
        [11] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3", "#FDB462", "#B3DE69", "#FCCDE5", "#D9D9D9", "#BC80BD", "#CCEBC5" },
        [12] = new[] { "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072", "#80B1D3", "#FDB462", "#B3DE69", "#FCCDE5", "#D9D9D9", "#BC80BD", "#CCEBC5", "#FFED6F" }
    };

    public static readonly Dictionary<int, string[]> Paired = new()
    {
        [3] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A" },
        [4] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C" },
        [5] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99" },
        [6] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C" },
        [7] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C", "#FDBF6F" },
        [8] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C", "#FDBF6F", "#FF7F00" },
        [9] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C", "#FDBF6F", "#FF7F00", "#CAB2D6" },
        [10] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C", "#FDBF6F", "#FF7F00", "#CAB2D6", "#6A3D9A" },
        [11] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C", "#FDBF6F", "#FF7F00", "#CAB2D6", "#6A3D9A", "#FFFF99" },
        [12] = new[] { "#A6CEE3", "#1F78B4", "#B2DF8A", "#33A02C", "#FB9A99", "#E31A1C", "#FDBF6F", "#FF7F00", "#CAB2D6", "#6A3D9A", "#FFFF99", "#B15928" }
    };

    public static readonly Dictionary<int, string[]> Accent = new()
    {
        [3] = new[] { "#7FC97F", "#BEAED4", "#FDC086" },
        [4] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99" },
        [5] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0" },
        [6] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0", "#F0027F" },
        [7] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0", "#F0027F", "#BF5B17" },
        [8] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0", "#F0027F", "#BF5B17", "#666666" },
        [9] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0", "#F0027F", "#BF5B17", "#666666", "#8DD3C7" },
        [10] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0", "#F0027F", "#BF5B17", "#666666", "#8DD3C7", "#FFFFB3" },
        [11] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0", "#F0027F", "#BF5B17", "#666666", "#8DD3C7", "#FFFFB3", "#BEBADA" },
        [12] = new[] { "#7FC97F", "#BEAED4", "#FDC086", "#FFFF99", "#386CB0", "#F0027F", "#BF5B17", "#666666", "#8DD3C7", "#FFFFB3", "#BEBADA", "#FB8072" }
    };

    #endregion

    #region Context-Specific Palettes

    /// <summary>
    /// Land use and land cover palette
    /// </summary>
    public static readonly Dictionary<int, string[]> LandUse = new()
    {
        [3] = new[] { "#E31A1C", "#33A02C", "#1F78B4" },  // Urban, Forest, Water
        [4] = new[] { "#E31A1C", "#33A02C", "#FFFF99", "#1F78B4" },  // Urban, Forest, Agriculture, Water
        [5] = new[] { "#E31A1C", "#33A02C", "#FFFF99", "#A6CEE3", "#1F78B4" },  // Urban, Forest, Agriculture, Wetland, Water
        [6] = new[] { "#E31A1C", "#33A02C", "#FFFF99", "#A6CEE3", "#1F78B4", "#B15928" },  // +Barren
        [7] = new[] { "#E31A1C", "#FF7F00", "#33A02C", "#FFFF99", "#A6CEE3", "#1F78B4", "#B15928" },  // +Industrial
        [8] = new[] { "#E31A1C", "#FF7F00", "#33A02C", "#B2DF8A", "#FFFF99", "#A6CEE3", "#1F78B4", "#B15928" },
        [9] = new[] { "#E31A1C", "#FF7F00", "#33A02C", "#B2DF8A", "#FFFF99", "#A6CEE3", "#1F78B4", "#B15928", "#CAB2D6" },
        [10] = new[] { "#E31A1C", "#FF7F00", "#33A02C", "#B2DF8A", "#FFFF99", "#FDBF6F", "#A6CEE3", "#1F78B4", "#B15928", "#CAB2D6" },
        [11] = new[] { "#E31A1C", "#FF7F00", "#FB9A99", "#33A02C", "#B2DF8A", "#FFFF99", "#FDBF6F", "#A6CEE3", "#1F78B4", "#B15928", "#CAB2D6" },
        [12] = new[] { "#E31A1C", "#FF7F00", "#FB9A99", "#33A02C", "#B2DF8A", "#FFFF99", "#FDBF6F", "#A6CEE3", "#1F78B4", "#B15928", "#CAB2D6", "#6A3D9A" }
    };

    /// <summary>
    /// Demographics palette
    /// </summary>
    public static readonly Dictionary<int, string[]> Demographics = new()
    {
        [3] = new[] { "#FEE0D2", "#FC9272", "#DE2D26" },
        [4] = new[] { "#FEE5D9", "#FCAE91", "#FB6A4A", "#CB181D" },
        [5] = new[] { "#FEE5D9", "#FCAE91", "#FB6A4A", "#DE2D26", "#A50F15" },
        [6] = new[] { "#FEE5D9", "#FCBBA1", "#FC9272", "#FB6A4A", "#DE2D26", "#A50F15" },
        [7] = new[] { "#FEE5D9", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D" },
        [8] = new[] { "#FFF5F0", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D" },
        [9] = new[] { "#FFF5F0", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#A50F15", "#67000D" },
        [10] = new[] { "#FFF5F0", "#FFF0EA", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#A50F15", "#67000D" },
        [11] = new[] { "#FFF5F0", "#FFF0EA", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D", "#A50F15", "#67000D" },
        [12] = new[] { "#FFF5F0", "#FFF0EA", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A", "#EF3B2C", "#CB181D", "#99000D", "#A50F15", "#600D0D", "#67000D" }
    };

    /// <summary>
    /// Environmental indicators palette
    /// </summary>
    public static readonly Dictionary<int, string[]> Environmental = new()
    {
        [3] = new[] { "#1A9850", "#FFFFBF", "#D73027" },
        [4] = new[] { "#1A9641", "#A6D96A", "#FDAE61", "#D7191C" },
        [5] = new[] { "#1A9641", "#A6D96A", "#FFFFBF", "#FDAE61", "#D7191C" },
        [6] = new[] { "#1A9850", "#91CF60", "#D9EF8B", "#FEE08B", "#FC8D59", "#D73027" },
        [7] = new[] { "#1A9850", "#91CF60", "#D9EF8B", "#FFFFBF", "#FEE08B", "#FC8D59", "#D73027" },
        [8] = new[] { "#1A9850", "#66BD63", "#A6D96A", "#D9EF8B", "#FEE08B", "#FDAE61", "#F46D43", "#D73027" },
        [9] = new[] { "#1A9850", "#66BD63", "#A6D96A", "#D9EF8B", "#FFFFBF", "#FEE08B", "#FDAE61", "#F46D43", "#D73027" },
        [10] = new[] { "#006837", "#1A9850", "#66BD63", "#A6D96A", "#D9EF8B", "#FEE08B", "#FDAE61", "#F46D43", "#D73027", "#A50026" },
        [11] = new[] { "#006837", "#1A9850", "#66BD63", "#A6D96A", "#D9EF8B", "#FFFFBF", "#FEE08B", "#FDAE61", "#F46D43", "#D73027", "#A50026" },
        [12] = new[] { "#006837", "#1A9850", "#66BD63", "#A6D96A", "#D9EF8B", "#FFFFBF", "#FEE08B", "#FDAE61", "#F46D43", "#D73027", "#A50026", "#8F0023" }
    };

    /// <summary>
    /// Traffic flow palette (green = good, red = congested)
    /// </summary>
    public static readonly Dictionary<int, string[]> Traffic = new()
    {
        [3] = new[] { "#00B050", "#FFFF00", "#FF0000" },
        [4] = new[] { "#00B050", "#92D050", "#FFC000", "#FF0000" },
        [5] = new[] { "#00B050", "#92D050", "#FFFF00", "#FFC000", "#FF0000" },
        [6] = new[] { "#00B050", "#92D050", "#C5E0B4", "#FFEB9C", "#FFC000", "#FF0000" },
        [7] = new[] { "#00B050", "#92D050", "#C5E0B4", "#FFFF00", "#FFEB9C", "#FFC000", "#FF0000" },
        [8] = new[] { "#00B050", "#92D050", "#C5E0B4", "#FFFF00", "#FFEB9C", "#FFC000", "#FF0000", "#C00000" },
        [9] = new[] { "#00B050", "#92D050", "#C5E0B4", "#E2EFDA", "#FFFF00", "#FFEB9C", "#FFC000", "#FF0000", "#C00000" },
        [10] = new[] { "#00B050", "#92D050", "#C5E0B4", "#E2EFDA", "#FFFF00", "#FFF2CC", "#FFEB9C", "#FFC000", "#FF0000", "#C00000" },
        [11] = new[] { "#00B050", "#70AD47", "#92D050", "#C5E0B4", "#E2EFDA", "#FFFF00", "#FFF2CC", "#FFEB9C", "#FFC000", "#FF0000", "#C00000" },
        [12] = new[] { "#00B050", "#70AD47", "#92D050", "#C5E0B4", "#E2EFDA", "#FFFF00", "#FFF2CC", "#FFEB9C", "#FFC000", "#ED7D31", "#FF0000", "#C00000" }
    };

    #endregion

    /// <summary>
    /// Get all available palette names
    /// </summary>
    public static IReadOnlyList<string> GetPaletteNames()
    {
        return new[]
        {
            "Blues", "Greens", "Oranges", "Reds", "Purples",
            "YellowGreen", "YellowGreenBlue", "GreenBlue", "BluePurple", "OrangeRed", "YellowOrangeRed",
            "RedYellowGreen", "Spectral", "RedBlue", "BrownTeal", "PurpleGreen",
            "Set1", "Set2", "Set3", "Paired", "Accent",
            "LandUse", "Demographics", "Environmental", "Traffic"
        };
    }
}

/// <summary>
/// Data classification types for palette selection
/// </summary>
public enum DataClassification
{
    Sequential,
    Diverging,
    Categorical,
    Temporal,
    LandUse,
    Demographics,
    Environmental
}
