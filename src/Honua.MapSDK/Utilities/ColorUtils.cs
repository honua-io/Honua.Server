namespace Honua.MapSDK.Utilities;

/// <summary>
/// Utility methods for working with colors, palettes, and color scales.
/// </summary>
public static class ColorUtils
{
    /// <summary>
    /// Represents an RGB color.
    /// </summary>
    public record RgbColor(int R, int G, int B)
    {
        /// <summary>
        /// Converts the RGB color to a hex string.
        /// </summary>
        public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

        /// <summary>
        /// Converts the RGB color to an HSL color.
        /// </summary>
        public HslColor ToHsl()
        {
            var r = R / 255.0;
            var g = G / 255.0;
            var b = B / 255.0;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            var h = 0.0;
            var s = 0.0;
            var l = (max + min) / 2.0;

            if (delta != 0)
            {
                s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);

                if (max == r)
                    h = ((g - b) / delta + (g < b ? 6 : 0)) / 6;
                else if (max == g)
                    h = ((b - r) / delta + 2) / 6;
                else
                    h = ((r - g) / delta + 4) / 6;
            }

            return new HslColor(h * 360, s, l);
        }
    }

    /// <summary>
    /// Represents an HSL color.
    /// </summary>
    public record HslColor(double H, double S, double L)
    {
        /// <summary>
        /// Converts the HSL color to an RGB color.
        /// </summary>
        public RgbColor ToRgb()
        {
            var h = H / 360.0;
            var s = S;
            var l = L;

            if (s == 0)
            {
                var gray = (int)(l * 255);
                return new RgbColor(gray, gray, gray);
            }

            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;

            var r = HueToRgb(p, q, h + 1.0 / 3.0);
            var g = HueToRgb(p, q, h);
            var b = HueToRgb(p, q, h - 1.0 / 3.0);

            return new RgbColor((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }

    /// <summary>
    /// Parses a hex color string to an RGB color.
    /// </summary>
    /// <param name="hex">Hex color string (e.g., "#FF5733" or "FF5733").</param>
    /// <returns>RGB color.</returns>
    public static RgbColor ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            throw new ArgumentException("Invalid hex color format", nameof(hex));

        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);

        return new RgbColor(r, g, b);
    }

    /// <summary>
    /// Interpolates between two colors.
    /// </summary>
    /// <param name="color1">First color (hex string).</param>
    /// <param name="color2">Second color (hex string).</param>
    /// <param name="t">Interpolation factor (0.0 to 1.0).</param>
    /// <returns>Interpolated color as hex string.</returns>
    public static string Interpolate(string color1, string color2, double t)
    {
        var c1 = ParseHex(color1);
        var c2 = ParseHex(color2);

        var r = (int)(c1.R + (c2.R - c1.R) * t);
        var g = (int)(c1.G + (c2.G - c1.G) * t);
        var b = (int)(c1.B + (c2.B - c1.B) * t);

        return new RgbColor(r, g, b).ToHex();
    }

    /// <summary>
    /// Generates a color scale with the specified number of steps.
    /// </summary>
    /// <param name="startColor">Start color (hex string).</param>
    /// <param name="endColor">End color (hex string).</param>
    /// <param name="steps">Number of steps in the scale.</param>
    /// <returns>Array of color hex strings.</returns>
    public static string[] GenerateColorScale(string startColor, string endColor, int steps)
    {
        if (steps < 2)
            throw new ArgumentException("Steps must be at least 2", nameof(steps));

        var colors = new string[steps];
        for (var i = 0; i < steps; i++)
        {
            var t = i / (double)(steps - 1);
            colors[i] = Interpolate(startColor, endColor, t);
        }

        return colors;
    }

    /// <summary>
    /// Generates a diverging color scale (with a midpoint color).
    /// </summary>
    /// <param name="startColor">Start color (hex string).</param>
    /// <param name="midColor">Mid color (hex string).</param>
    /// <param name="endColor">End color (hex string).</param>
    /// <param name="steps">Number of steps in the scale (must be odd).</param>
    /// <returns>Array of color hex strings.</returns>
    public static string[] GenerateDivergingScale(string startColor, string midColor, string endColor, int steps)
    {
        if (steps < 3)
            throw new ArgumentException("Steps must be at least 3", nameof(steps));

        var colors = new List<string>();
        var midPoint = steps / 2;

        // First half
        for (var i = 0; i <= midPoint; i++)
        {
            var t = i / (double)midPoint;
            colors.Add(Interpolate(startColor, midColor, t));
        }

        // Second half
        for (var i = 1; i < steps - midPoint; i++)
        {
            var t = i / (double)(steps - midPoint - 1);
            colors.Add(Interpolate(midColor, endColor, t));
        }

        return colors.ToArray();
    }

    /// <summary>
    /// Predefined color palettes.
    /// </summary>
    public static class Palettes
    {
        /// <summary>
        /// Viridis color palette (perceptually uniform).
        /// </summary>
        public static readonly string[] Viridis = new[]
        {
            "#440154", "#482777", "#3F4A8A", "#31688E", "#26838F",
            "#1F9D8A", "#6CCE5A", "#B6DE2B", "#FEE825"
        };

        /// <summary>
        /// Plasma color palette.
        /// </summary>
        public static readonly string[] Plasma = new[]
        {
            "#0D0887", "#5B02A3", "#9A179B", "#CB4678", "#EB7852",
            "#FBB32F", "#F0F921"
        };

        /// <summary>
        /// Blues sequential palette.
        /// </summary>
        public static readonly string[] Blues = new[]
        {
            "#F7FBFF", "#DEEBF7", "#C6DBEF", "#9ECAE1", "#6BAED6",
            "#4292C6", "#2171B5", "#084594"
        };

        /// <summary>
        /// Reds sequential palette.
        /// </summary>
        public static readonly string[] Reds = new[]
        {
            "#FFF5F0", "#FEE0D2", "#FCBBA1", "#FC9272", "#FB6A4A",
            "#EF3B2C", "#CB181D", "#99000D"
        };

        /// <summary>
        /// RdYlGn diverging palette (Red-Yellow-Green).
        /// </summary>
        public static readonly string[] RdYlGn = new[]
        {
            "#A50026", "#D73027", "#F46D43", "#FDAE61", "#FEE08B",
            "#FFFFBF", "#D9EF8B", "#A6D96A", "#66BD63", "#1A9850", "#006837"
        };

        /// <summary>
        /// Category10 qualitative palette.
        /// </summary>
        public static readonly string[] Category10 = new[]
        {
            "#1F77B4", "#FF7F0E", "#2CA02C", "#D62728", "#9467BD",
            "#8C564B", "#E377C2", "#7F7F7F", "#BCBD22", "#17BECF"
        };
    }

    /// <summary>
    /// Calculates the relative luminance of a color (for contrast calculations).
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <returns>Relative luminance (0.0 to 1.0).</returns>
    public static double CalculateLuminance(string hex)
    {
        var rgb = ParseHex(hex);
        var r = rgb.R / 255.0;
        var g = rgb.G / 255.0;
        var b = rgb.B / 255.0;

        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Calculates the contrast ratio between two colors.
    /// </summary>
    /// <param name="color1">First color (hex string).</param>
    /// <param name="color2">Second color (hex string).</param>
    /// <returns>Contrast ratio (1 to 21).</returns>
    public static double CalculateContrastRatio(string color1, string color2)
    {
        var l1 = CalculateLuminance(color1);
        var l2 = CalculateLuminance(color2);

        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Checks if two colors meet WCAG AA contrast requirements.
    /// </summary>
    /// <param name="foreground">Foreground color (hex string).</param>
    /// <param name="background">Background color (hex string).</param>
    /// <param name="largeText">Whether the text is large (18pt+ or 14pt+ bold).</param>
    /// <returns>True if contrast meets WCAG AA; otherwise, false.</returns>
    public static bool MeetsWcagAA(string foreground, string background, bool largeText = false)
    {
        var ratio = CalculateContrastRatio(foreground, background);
        return largeText ? ratio >= 3.0 : ratio >= 4.5;
    }

    /// <summary>
    /// Checks if two colors meet WCAG AAA contrast requirements.
    /// </summary>
    /// <param name="foreground">Foreground color (hex string).</param>
    /// <param name="background">Background color (hex string).</param>
    /// <param name="largeText">Whether the text is large (18pt+ or 14pt+ bold).</param>
    /// <returns>True if contrast meets WCAG AAA; otherwise, false.</returns>
    public static bool MeetsWcagAAA(string foreground, string background, bool largeText = false)
    {
        var ratio = CalculateContrastRatio(foreground, background);
        return largeText ? ratio >= 4.5 : ratio >= 7.0;
    }

    /// <summary>
    /// Lightens a color by a percentage.
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <param name="percent">Percentage to lighten (0.0 to 1.0).</param>
    /// <returns>Lightened color as hex string.</returns>
    public static string Lighten(string hex, double percent)
    {
        var hsl = ParseHex(hex).ToHsl();
        var newL = Math.Min(1.0, hsl.L + percent);
        return new HslColor(hsl.H, hsl.S, newL).ToRgb().ToHex();
    }

    /// <summary>
    /// Darkens a color by a percentage.
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <param name="percent">Percentage to darken (0.0 to 1.0).</param>
    /// <returns>Darkened color as hex string.</returns>
    public static string Darken(string hex, double percent)
    {
        var hsl = ParseHex(hex).ToHsl();
        var newL = Math.Max(0.0, hsl.L - percent);
        return new HslColor(hsl.H, hsl.S, newL).ToRgb().ToHex();
    }

    /// <summary>
    /// Generates a complementary color.
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <returns>Complementary color as hex string.</returns>
    public static string GetComplementary(string hex)
    {
        var hsl = ParseHex(hex).ToHsl();
        var newH = (hsl.H + 180) % 360;
        return new HslColor(newH, hsl.S, hsl.L).ToRgb().ToHex();
    }
}
