// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.Sky;

/// <summary>
/// Predefined sky configuration presets for common scenarios
/// </summary>
public static class SkyPresets
{
    /// <summary>
    /// Clear day sky with bright blue atmosphere
    /// </summary>
    public static SkyConfiguration ClearDay => new()
    {
        SkyType = SkyType.Atmosphere,
        SkyColor = "#87CEEB",
        HorizonColor = "#B0E0E6",
        HorizonBlend = 0.1,
        EnableAtmosphere = true,
        AtmosphereIntensity = 1.0,
        AtmosphereColor = "#87CEEB",
        EnableStars = false,
        SunPosition = new Vector2(180, 45),
        EnableFog = false
    };

    /// <summary>
    /// Sunrise/dawn with warm orange and pink hues
    /// </summary>
    public static SkyConfiguration Sunrise => new()
    {
        SkyType = SkyType.Gradient,
        SkyColor = "#FFB347",
        HorizonColor = "#FF6B6B",
        HorizonBlend = 0.3,
        EnableAtmosphere = true,
        AtmosphereIntensity = 0.8,
        AtmosphereColor = "#FFA07A",
        EnableStars = false,
        SunPosition = new Vector2(90, 5),
        EnableFog = true,
        FogDensity = 0.3,
        FogColor = "#FFE4E1"
    };

    /// <summary>
    /// Sunset/dusk with warm golden and red hues
    /// </summary>
    public static SkyConfiguration Sunset => new()
    {
        SkyType = SkyType.Gradient,
        SkyColor = "#FF8C00",
        HorizonColor = "#FF4500",
        HorizonBlend = 0.4,
        EnableAtmosphere = true,
        AtmosphereIntensity = 0.9,
        AtmosphereColor = "#FF7F50",
        EnableStars = false,
        SunPosition = new Vector2(270, 3),
        EnableFog = true,
        FogDensity = 0.2,
        FogColor = "#FFD7A8"
    };

    /// <summary>
    /// Night sky with stars and deep blue/black atmosphere
    /// </summary>
    public static SkyConfiguration Night => new()
    {
        SkyType = SkyType.Atmosphere,
        SkyColor = "#0B1026",
        HorizonColor = "#1B2A49",
        HorizonBlend = 0.15,
        EnableAtmosphere = true,
        AtmosphereIntensity = 0.3,
        AtmosphereColor = "#1B2A49",
        EnableStars = true,
        SunPosition = new Vector2(0, -30),
        EnableFog = false
    };

    /// <summary>
    /// Twilight (civil) with purple and blue hues
    /// </summary>
    public static SkyConfiguration Twilight => new()
    {
        SkyType = SkyType.Gradient,
        SkyColor = "#4B0082",
        HorizonColor = "#8B4789",
        HorizonBlend = 0.25,
        EnableAtmosphere = true,
        AtmosphereIntensity = 0.6,
        AtmosphereColor = "#6A5ACD",
        EnableStars = true,
        SunPosition = new Vector2(270, -5),
        EnableFog = false
    };

    /// <summary>
    /// Overcast/cloudy day with gray tones
    /// </summary>
    public static SkyConfiguration Overcast => new()
    {
        SkyType = SkyType.Solid,
        SkyColor = "#A9A9A9",
        HorizonColor = "#D3D3D3",
        HorizonBlend = 0.2,
        EnableAtmosphere = false,
        AtmosphereIntensity = 0.3,
        AtmosphereColor = "#BEBEBE",
        EnableStars = false,
        SunPosition = new Vector2(180, 50),
        EnableFog = true,
        FogDensity = 0.6,
        FogColor = "#E0E0E0"
    };

    /// <summary>
    /// Blue hour (early morning or late evening)
    /// </summary>
    public static SkyConfiguration BlueHour => new()
    {
        SkyType = SkyType.Gradient,
        SkyColor = "#4169E1",
        HorizonColor = "#FF7F50",
        HorizonBlend = 0.3,
        EnableAtmosphere = true,
        AtmosphereIntensity = 0.7,
        AtmosphereColor = "#5B9BD5",
        EnableStars = true,
        SunPosition = new Vector2(90, -3),
        EnableFog = false
    };

    /// <summary>
    /// Golden hour (late afternoon with warm light)
    /// </summary>
    public static SkyConfiguration GoldenHour => new()
    {
        SkyType = SkyType.Gradient,
        SkyColor = "#FFD700",
        HorizonColor = "#FFA500",
        HorizonBlend = 0.2,
        EnableAtmosphere = true,
        AtmosphereIntensity = 0.85,
        AtmosphereColor = "#FFAA00",
        EnableStars = false,
        SunPosition = new Vector2(250, 15),
        EnableFog = false
    };

    /// <summary>
    /// Arctic/Antarctic (polar region with unique light)
    /// </summary>
    public static SkyConfiguration Polar => new()
    {
        SkyType = SkyType.Gradient,
        SkyColor = "#B0E0E6",
        HorizonColor = "#AFEEEE",
        HorizonBlend = 0.1,
        EnableAtmosphere = true,
        AtmosphereIntensity = 0.5,
        AtmosphereColor = "#AFEEEE",
        EnableStars = false,
        SunPosition = new Vector2(180, 10),
        EnableFog = true,
        FogDensity = 0.4,
        FogColor = "#F0F8FF"
    };

    /// <summary>
    /// Desert/arid environment with intense sun
    /// </summary>
    public static SkyConfiguration Desert => new()
    {
        SkyType = SkyType.Atmosphere,
        SkyColor = "#87CEEB",
        HorizonColor = "#FFDEAD",
        HorizonBlend = 0.15,
        EnableAtmosphere = true,
        AtmosphereIntensity = 1.0,
        AtmosphereColor = "#F4A460",
        EnableStars = false,
        SunPosition = new Vector2(180, 75),
        EnableFog = true,
        FogDensity = 0.25,
        FogColor = "#FFE4B5"
    };

    /// <summary>
    /// Get all available presets as a dictionary
    /// </summary>
    public static Dictionary<string, SkyConfiguration> GetAllPresets()
    {
        return new Dictionary<string, SkyConfiguration>
        {
            { "Clear Day", ClearDay },
            { "Sunrise", Sunrise },
            { "Sunset", Sunset },
            { "Night", Night },
            { "Twilight", Twilight },
            { "Overcast", Overcast },
            { "Blue Hour", BlueHour },
            { "Golden Hour", GoldenHour },
            { "Polar", Polar },
            { "Desert", Desert }
        };
    }

    /// <summary>
    /// Get preset by name
    /// </summary>
    /// <param name="presetName">Name of the preset</param>
    /// <returns>Sky configuration or null if not found</returns>
    public static SkyConfiguration? GetPreset(string presetName)
    {
        var presets = GetAllPresets();
        return presets.TryGetValue(presetName, out var config) ? config : null;
    }

    /// <summary>
    /// Get all preset names
    /// </summary>
    /// <returns>List of preset names</returns>
    public static List<string> GetPresetNames()
    {
        return GetAllPresets().Keys.ToList();
    }
}
