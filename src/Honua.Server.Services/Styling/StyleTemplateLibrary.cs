// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Services.Styling;

/// <summary>
/// Library of predefined style templates for common use cases
/// </summary>
public static class StyleTemplateLibrary
{
    /// <summary>
    /// Get all available template names
    /// </summary>
    public static IReadOnlyList<string> GetTemplateNames()
    {
        return Templates.Keys.ToList();
    }

    /// <summary>
    /// Get a template by name
    /// </summary>
    public static StyleTemplate? GetTemplate(string name)
    {
        return Templates.TryGetValue(name, out var template) ? template : null;
    }

    /// <summary>
    /// Get templates filtered by geometry type
    /// </summary>
    public static IReadOnlyList<StyleTemplate> GetTemplatesByGeometry(string geometryType)
    {
        return Templates.Values
            .Where(t => t.SupportedGeometries.Contains(geometryType.ToLowerInvariant()))
            .ToList();
    }

    /// <summary>
    /// Get templates filtered by use case
    /// </summary>
    public static IReadOnlyList<StyleTemplate> GetTemplatesByUseCase(string useCase)
    {
        return Templates.Values
            .Where(t => t.UseCase.Equals(useCase, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Apply a template to create a style
    /// </summary>
    public static StyleDefinition ApplyTemplate(string templateName, StyleTemplateOptions options)
    {
        var template = GetTemplate(templateName);
        if (template == null)
        {
            throw new ArgumentException($"Template '{templateName}' not found");
        }

        return template.Generator(options);
    }

    private static readonly Dictionary<string, StyleTemplate> Templates = new()
    {
        // Point templates
        ["simple-points"] = new StyleTemplate
        {
            Name = "simple-points",
            DisplayName = "Simple Points",
            Description = "Basic point markers with customizable color and size",
            UseCase = "general",
            SupportedGeometries = new[] { "point" },
            ThumbnailUrl = "/assets/styles/simple-points.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "simple-points",
                Title = options.Title ?? "Simple Points",
                Renderer = "simple",
                GeometryType = "point",
                Format = "legacy",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "shape",
                    FillColor = options.BaseColor ?? "#3B82F6",
                    StrokeColor = "#1E40AF",
                    StrokeWidth = 1.5,
                    Size = options.Size ?? 8,
                    Opacity = options.Opacity ?? 0.8
                }
            }
        },

        ["heatmap"] = new StyleTemplate
        {
            Name = "heatmap",
            DisplayName = "Heatmap",
            Description = "Density-based visualization for point clusters",
            UseCase = "density",
            SupportedGeometries = new[] { "point" },
            ThumbnailUrl = "/assets/styles/heatmap.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "heatmap",
                Title = options.Title ?? "Heatmap",
                Renderer = "heatmap",
                GeometryType = "point",
                Format = "legacy",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "heatmap"
                }
            }
        },

        ["proportional-symbols"] = new StyleTemplate
        {
            Name = "proportional-symbols",
            DisplayName = "Proportional Symbols",
            Description = "Point size varies by data value",
            UseCase = "quantitative",
            SupportedGeometries = new[] { "point" },
            ThumbnailUrl = "/assets/styles/proportional-symbols.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "proportional-symbols",
                Title = options.Title ?? "Proportional Symbols",
                Renderer = "proportional",
                GeometryType = "point",
                Format = "legacy",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "shape",
                    FillColor = options.BaseColor ?? "#8B5CF6",
                    StrokeColor = "#6D28D9",
                    StrokeWidth = 1.5,
                    Size = options.Size ?? 10,
                    Opacity = options.Opacity ?? 0.75
                }
            }
        },

        // Line templates
        ["simple-lines"] = new StyleTemplate
        {
            Name = "simple-lines",
            DisplayName = "Simple Lines",
            Description = "Basic line styling with customizable color and width",
            UseCase = "general",
            SupportedGeometries = new[] { "line" },
            ThumbnailUrl = "/assets/styles/simple-lines.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "simple-lines",
                Title = options.Title ?? "Simple Lines",
                Renderer = "simple",
                GeometryType = "line",
                Format = "legacy",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "shape",
                    StrokeColor = options.BaseColor ?? "#10B981",
                    StrokeWidth = options.LineWidth ?? 2.5,
                    StrokeStyle = "solid",
                    Opacity = options.Opacity ?? 0.8
                }
            }
        },

        ["road-network"] = new StyleTemplate
        {
            Name = "road-network",
            DisplayName = "Road Network",
            Description = "Hierarchical road styling (highways, streets, etc.)",
            UseCase = "transportation",
            SupportedGeometries = new[] { "line" },
            ThumbnailUrl = "/assets/styles/road-network.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "road-network",
                Title = options.Title ?? "Road Network",
                Renderer = "uniqueValue",
                GeometryType = "line",
                Format = "legacy",
                UniqueValue = new UniqueValueStyleDefinition
                {
                    Field = options.ClassificationField ?? "road_type",
                    Classes = new[]
                    {
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "highway",
                            Symbol = new SimpleStyleDefinition { StrokeColor = "#EF4444", StrokeWidth = 4, Opacity = 0.9 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "arterial",
                            Symbol = new SimpleStyleDefinition { StrokeColor = "#F59E0B", StrokeWidth = 3, Opacity = 0.85 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "collector",
                            Symbol = new SimpleStyleDefinition { StrokeColor = "#FBBF24", StrokeWidth = 2.5, Opacity = 0.8 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "local",
                            Symbol = new SimpleStyleDefinition { StrokeColor = "#94A3B8", StrokeWidth = 1.5, Opacity = 0.7 }
                        }
                    },
                    DefaultSymbol = new SimpleStyleDefinition { StrokeColor = "#CBD5E1", StrokeWidth = 1, Opacity = 0.6 }
                }
            }
        },

        ["rivers-streams"] = new StyleTemplate
        {
            Name = "rivers-streams",
            DisplayName = "Rivers & Streams",
            Description = "Water features with varying widths",
            UseCase = "hydrology",
            SupportedGeometries = new[] { "line" },
            ThumbnailUrl = "/assets/styles/rivers-streams.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "rivers-streams",
                Title = options.Title ?? "Rivers & Streams",
                Renderer = "simple",
                GeometryType = "line",
                Format = "legacy",
                Simple = new SimpleStyleDefinition
                {
                    StrokeColor = "#3B82F6",
                    StrokeWidth = options.LineWidth ?? 2,
                    StrokeStyle = "solid",
                    Opacity = 0.7
                }
            }
        },

        // Polygon templates
        ["simple-polygons"] = new StyleTemplate
        {
            Name = "simple-polygons",
            DisplayName = "Simple Polygons",
            Description = "Basic polygon styling with fill and border",
            UseCase = "general",
            SupportedGeometries = new[] { "polygon" },
            ThumbnailUrl = "/assets/styles/simple-polygons.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "simple-polygons",
                Title = options.Title ?? "Simple Polygons",
                Renderer = "simple",
                GeometryType = "polygon",
                Format = "legacy",
                Simple = new SimpleStyleDefinition
                {
                    FillColor = options.BaseColor ?? "#F59E0B",
                    StrokeColor = "#D97706",
                    StrokeWidth = 1.5,
                    Opacity = options.Opacity ?? 0.6
                }
            }
        },

        ["choropleth"] = new StyleTemplate
        {
            Name = "choropleth",
            DisplayName = "Choropleth",
            Description = "Color-coded areas based on data values",
            UseCase = "thematic",
            SupportedGeometries = new[] { "polygon" },
            ThumbnailUrl = "/assets/styles/choropleth.png",
            Generator = options =>
            {
                var classCount = options.ClassCount ?? 7;
                var palette = options.ColorPalette ?? "Blues";
                var colors = CartographicPalettes.GetPalette(palette, classCount);

                var rules = new List<StyleRuleDefinition>();
                for (int i = 0; i < classCount; i++)
                {
                    rules.Add(new StyleRuleDefinition
                    {
                        Id = $"class-{i}",
                        Label = $"Class {i + 1}",
                        Symbolizer = new SimpleStyleDefinition
                        {
                            FillColor = colors[i],
                            StrokeColor = "#64748B",
                            StrokeWidth = 0.5,
                            Opacity = 0.75
                        }
                    });
                }

                return new StyleDefinition
                {
                    Id = options.StyleId ?? "choropleth",
                    Title = options.Title ?? "Choropleth Map",
                    Renderer = "classBreaks",
                    GeometryType = "polygon",
                    Format = "legacy",
                    Rules = rules
                };
            }
        },

        ["land-use"] = new StyleTemplate
        {
            Name = "land-use",
            DisplayName = "Land Use",
            Description = "Categorical styling for land use/land cover",
            UseCase = "land-use",
            SupportedGeometries = new[] { "polygon" },
            ThumbnailUrl = "/assets/styles/land-use.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "land-use",
                Title = options.Title ?? "Land Use",
                Renderer = "uniqueValue",
                GeometryType = "polygon",
                Format = "legacy",
                UniqueValue = new UniqueValueStyleDefinition
                {
                    Field = options.ClassificationField ?? "land_use",
                    Classes = new[]
                    {
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "residential",
                            Symbol = new SimpleStyleDefinition { FillColor = "#FEF3C7", StrokeColor = "#F59E0B", StrokeWidth = 1, Opacity = 0.7 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "commercial",
                            Symbol = new SimpleStyleDefinition { FillColor = "#FEE2E2", StrokeColor = "#EF4444", StrokeWidth = 1, Opacity = 0.7 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "industrial",
                            Symbol = new SimpleStyleDefinition { FillColor = "#E0E7FF", StrokeColor = "#6366F1", StrokeWidth = 1, Opacity = 0.7 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "agricultural",
                            Symbol = new SimpleStyleDefinition { FillColor = "#ECFCCB", StrokeColor = "#84CC16", StrokeWidth = 1, Opacity = 0.7 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "forest",
                            Symbol = new SimpleStyleDefinition { FillColor = "#D1FAE5", StrokeColor = "#10B981", StrokeWidth = 1, Opacity = 0.7 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "water",
                            Symbol = new SimpleStyleDefinition { FillColor = "#DBEAFE", StrokeColor = "#3B82F6", StrokeWidth = 1, Opacity = 0.7 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "wetland",
                            Symbol = new SimpleStyleDefinition { FillColor = "#E0F2FE", StrokeColor = "#0EA5E9", StrokeWidth = 1, Opacity = 0.7 }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "barren",
                            Symbol = new SimpleStyleDefinition { FillColor = "#FEF3C7", StrokeColor = "#D97706", StrokeWidth = 1, Opacity = 0.7 }
                        }
                    },
                    DefaultSymbol = new SimpleStyleDefinition { FillColor = "#F3F4F6", StrokeColor = "#9CA3AF", StrokeWidth = 1, Opacity = 0.5 }
                }
            }
        },

        ["administrative-boundaries"] = new StyleTemplate
        {
            Name = "administrative-boundaries",
            DisplayName = "Administrative Boundaries",
            Description = "Hierarchical boundary styling for countries, states, counties",
            UseCase = "boundaries",
            SupportedGeometries = new[] { "polygon", "line" },
            ThumbnailUrl = "/assets/styles/admin-boundaries.png",
            Generator = options => new StyleDefinition
            {
                Id = options.StyleId ?? "admin-boundaries",
                Title = options.Title ?? "Administrative Boundaries",
                Renderer = "uniqueValue",
                GeometryType = options.GeometryType ?? "polygon",
                Format = "legacy",
                UniqueValue = new UniqueValueStyleDefinition
                {
                    Field = options.ClassificationField ?? "admin_level",
                    Classes = new[]
                    {
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "country",
                            Symbol = new SimpleStyleDefinition
                            {
                                FillColor = "#FEF3C7",
                                StrokeColor = "#78350F",
                                StrokeWidth = 3,
                                Opacity = 0.3
                            }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "state",
                            Symbol = new SimpleStyleDefinition
                            {
                                FillColor = "#FEF3C7",
                                StrokeColor = "#92400E",
                                StrokeWidth = 2,
                                Opacity = 0.2
                            }
                        },
                        new UniqueValueStyleClassDefinition
                        {
                            Value = "county",
                            Symbol = new SimpleStyleDefinition
                            {
                                FillColor = "#FEF3C7",
                                StrokeColor = "#B45309",
                                StrokeWidth = 1.5,
                                Opacity = 0.1
                            }
                        }
                    },
                    DefaultSymbol = new SimpleStyleDefinition
                    {
                        FillColor = "#FAFAF9",
                        StrokeColor = "#D6D3D1",
                        StrokeWidth = 1,
                        Opacity = 0.3
                    }
                }
            }
        },

        ["environmental-zones"] = new StyleTemplate
        {
            Name = "environmental-zones",
            DisplayName = "Environmental Zones",
            Description = "Color-coded environmental or ecological zones",
            UseCase = "environmental",
            SupportedGeometries = new[] { "polygon" },
            ThumbnailUrl = "/assets/styles/environmental-zones.png",
            Generator = options =>
            {
                var colors = CartographicPalettes.GetPalette("Environmental", 7);
                return new StyleDefinition
                {
                    Id = options.StyleId ?? "environmental-zones",
                    Title = options.Title ?? "Environmental Zones",
                    Renderer = "uniqueValue",
                    GeometryType = "polygon",
                    Format = "legacy",
                    UniqueValue = new UniqueValueStyleDefinition
                    {
                        Field = options.ClassificationField ?? "zone_type",
                        Classes = colors.Select((color, i) => new UniqueValueStyleClassDefinition
                        {
                            Value = $"zone-{i + 1}",
                            Symbol = new SimpleStyleDefinition
                            {
                                FillColor = color,
                                StrokeColor = "#374151",
                                StrokeWidth = 1,
                                Opacity = 0.65
                            }
                        }).ToArray(),
                        DefaultSymbol = new SimpleStyleDefinition
                        {
                            FillColor = "#F3F4F6",
                            StrokeColor = "#9CA3AF",
                            StrokeWidth = 1,
                            Opacity = 0.5
                        }
                    }
                };
            }
        }
    };
}

/// <summary>
/// Style template definition
/// </summary>
public class StyleTemplate
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public required string UseCase { get; set; }
    public required string[] SupportedGeometries { get; set; }
    public string? ThumbnailUrl { get; set; }
    public required Func<StyleTemplateOptions, StyleDefinition> Generator { get; set; }
}

/// <summary>
/// Options for applying a style template
/// </summary>
public class StyleTemplateOptions
{
    public string? StyleId { get; set; }
    public string? Title { get; set; }
    public string? GeometryType { get; set; }
    public string? BaseColor { get; set; }
    public double? Opacity { get; set; }
    public double? Size { get; set; }
    public double? LineWidth { get; set; }
    public string? ColorPalette { get; set; }
    public int? ClassCount { get; set; }
    public string? ClassificationField { get; set; }
}
