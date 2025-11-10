using HonuaField.Models.Symbology;
using Microsoft.Maui.Graphics;

namespace HonuaField.Controls;

/// <summary>
/// Control for previewing symbology styles
/// Displays visual preview of point, line, or polygon symbology
/// </summary>
public partial class SymbologyPreviewControl : ContentView
{
	public static readonly BindableProperty PointSymbologyProperty =
		BindableProperty.Create(
			nameof(PointSymbology),
			typeof(PointSymbology),
			typeof(SymbologyPreviewControl),
			null,
			propertyChanged: OnSymbologyChanged);

	public static readonly BindableProperty LineSymbologyProperty =
		BindableProperty.Create(
			nameof(LineSymbology),
			typeof(LineSymbology),
			typeof(SymbologyPreviewControl),
			null,
			propertyChanged: OnSymbologyChanged);

	public static readonly BindableProperty PolygonSymbologyProperty =
		BindableProperty.Create(
			nameof(PolygonSymbology),
			typeof(PolygonSymbology),
			typeof(SymbologyPreviewControl),
			null,
			propertyChanged: OnSymbologyChanged);

	public PointSymbology? PointSymbology
	{
		get => (PointSymbology?)GetValue(PointSymbologyProperty);
		set => SetValue(PointSymbologyProperty, value);
	}

	public LineSymbology? LineSymbology
	{
		get => (LineSymbology?)GetValue(LineSymbologyProperty);
		set => SetValue(LineSymbologyProperty, value);
	}

	public PolygonSymbology? PolygonSymbology
	{
		get => (PolygonSymbology?)GetValue(PolygonSymbologyProperty);
		set => SetValue(PolygonSymbologyProperty, value);
	}

	public SymbologyPreviewControl()
	{
		InitializeComponent();
		UpdatePreview();
	}

	private static void OnSymbologyChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is SymbologyPreviewControl control)
		{
			control.UpdatePreview();
		}
	}

	private void UpdatePreview()
	{
		// Hide all previews
		PointPreview.IsVisible = false;
		LinePreview.IsVisible = false;
		PolygonPreview.IsVisible = false;

		// Show and configure appropriate preview
		if (PointSymbology != null)
		{
			UpdatePointPreview();
		}
		else if (LineSymbology != null)
		{
			UpdateLinePreview();
		}
		else if (PolygonSymbology != null)
		{
			UpdatePolygonPreview();
		}
	}

	private void UpdatePointPreview()
	{
		if (PointSymbology == null)
			return;

		PointPreview.IsVisible = true;

		var fillColor = ParseColor(PointSymbology.Color);
		var strokeColor = ParseColor(PointSymbology.OutlineColor ?? "#FFFFFF");

		// Apply opacity
		if (PointSymbology.Opacity < 1.0)
		{
			fillColor = fillColor.WithAlpha((float)PointSymbology.Opacity);
		}

		PointCircle.Fill = new SolidColorBrush(fillColor);
		PointCircle.Stroke = new SolidColorBrush(strokeColor);
		PointCircle.StrokeThickness = PointSymbology.OutlineWidth;

		// Scale size
		var size = PointSymbology.Size * 2; // Scale up for visibility
		PointCircle.WidthRequest = size;
		PointCircle.HeightRequest = size;
	}

	private void UpdateLinePreview()
	{
		if (LineSymbology == null)
			return;

		LinePreview.IsVisible = true;

		var lineColor = ParseColor(LineSymbology.Color);

		// Apply opacity
		if (LineSymbology.Opacity < 1.0)
		{
			lineColor = lineColor.WithAlpha((float)LineSymbology.Opacity);
		}

		LineBox.Color = lineColor;
		LineBox.HeightRequest = Math.Max(2, LineSymbology.Width);
	}

	private void UpdatePolygonPreview()
	{
		if (PolygonSymbology == null)
			return;

		PolygonPreview.IsVisible = true;

		var fillColor = ParseColor(PolygonSymbology.FillColor);
		var strokeColor = ParseColor(PolygonSymbology.StrokeColor);

		// Apply opacities
		if (PolygonSymbology.FillOpacity < 1.0)
		{
			fillColor = fillColor.WithAlpha((float)PolygonSymbology.FillOpacity);
		}

		if (PolygonSymbology.StrokeOpacity < 1.0)
		{
			strokeColor = strokeColor.WithAlpha((float)PolygonSymbology.StrokeOpacity);
		}

		PolygonBorder.BackgroundColor = fillColor;
		PolygonBorder.Stroke = new SolidColorBrush(strokeColor);
		PolygonBorder.StrokeThickness = PolygonSymbology.StrokeWidth;
	}

	/// <summary>
	/// Parses a color string to MAUI Color
	/// </summary>
	private Color ParseColor(string? colorString)
	{
		if (string.IsNullOrWhiteSpace(colorString))
			return Colors.Blue;

		try
		{
			// Try to parse as hex color
			if (colorString.StartsWith("#"))
			{
				return Color.FromArgb(colorString);
			}

			// Try named colors
			var namedColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
			{
				{ "red", Colors.Red },
				{ "blue", Colors.Blue },
				{ "green", Colors.Green },
				{ "yellow", Colors.Yellow },
				{ "orange", Colors.Orange },
				{ "purple", Colors.Purple },
				{ "cyan", Colors.Cyan },
				{ "magenta", Colors.Magenta },
				{ "white", Colors.White },
				{ "black", Colors.Black },
				{ "gray", Colors.Gray },
				{ "brown", Colors.Brown },
				{ "pink", Colors.Pink }
			};

			if (namedColors.TryGetValue(colorString.ToLowerInvariant(), out var color))
				return color;

			// Try rgb/rgba format
			if (colorString.StartsWith("rgb"))
			{
				// Simple rgb/rgba parser
				var parts = colorString
					.Replace("rgb(", "")
					.Replace("rgba(", "")
					.Replace(")", "")
					.Split(',');

				if (parts.Length >= 3)
				{
					var r = int.Parse(parts[0].Trim());
					var g = int.Parse(parts[1].Trim());
					var b = int.Parse(parts[2].Trim());
					var a = parts.Length > 3 ? float.Parse(parts[3].Trim()) : 1.0f;

					return Color.FromRgba(r, g, b, a);
				}
			}
		}
		catch
		{
			// Fall through to default
		}

		return Colors.Blue;
	}
}
