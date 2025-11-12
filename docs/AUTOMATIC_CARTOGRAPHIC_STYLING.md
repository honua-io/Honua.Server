# Automatic Cartographic Styling System

Honua's Automatic Cartographic Styling system intelligently generates professional map styles based on data characteristics, eliminating the need for manual styling configuration while ensuring cartographically sound visualizations.

## Features

### 1. Data-Aware Styling
- **Automatic data type detection**: Numeric, categorical, temporal, boolean
- **Statistical analysis**: Mean, median, standard deviation, skewness
- **Distribution analysis**: Identifies data patterns and recommends optimal classification
- **Semantic detection**: Recognizes land use, demographics, environmental data

### 2. Classification Methods
- **Jenks Natural Breaks**: Minimizes variance within classes (optimal for most data)
- **Quantile**: Equal number of features per class
- **Equal Interval**: Equal-sized ranges
- **Standard Deviation**: Classes based on standard deviations from mean
- **Geometric Interval**: For exponentially distributed data
- **Logarithmic**: For highly skewed data

### 3. Professional Color Palettes
Over 30 ColorBrewer-based palettes:
- **Sequential**: Blues, Greens, Reds, YellowGreenBlue, etc.
- **Diverging**: Spectral, RedBlue, BrownTeal, PurpleGreen
- **Qualitative**: Set1, Set2, Paired, Accent
- **Context-specific**: LandUse, Demographics, Environmental, Traffic
- All palettes are colorblind-safe and print-friendly

### 4. Style Templates
Pre-built templates for common use cases:
- Simple Points, Lines, Polygons
- Choropleth Maps
- Proportional Symbols
- Road Networks
- Land Use/Land Cover
- Administrative Boundaries
- Environmental Zones
- Heatmaps

### 5. Intelligent Defaults
- **Point data**: Circles with size/color scaling, automatic clustering for high-density
- **Line data**: Width and color by category or value
- **Polygon data**: Fill with borders, opacity-aware
- **Temporal data**: Timeline-based color progression

## Architecture

### Core Components

```
Honua.MapSDK.Styling/
├── CartographicPalettes.cs      # 30+ professional color palettes
├── DataAnalyzer.cs              # Data type detection and statistics
├── ClassificationStrategy.cs    # Jenks, Quantile, etc.
├── StyleGeneratorService.cs     # Main style generation engine
├── StyleTemplateLibrary.cs      # Pre-built style templates
└── Components/
    └── Styling/
        ├── StyleEditor.razor    # Interactive style editor UI
        └── SymbolEditor.razor   # Symbol property editor
```

## Usage Examples

### 1. C# Service Usage

#### Generate Simple Style

```csharp
using Honua.MapSDK.Styling;

var styleGenerator = new StyleGeneratorService();

var request = new StyleGenerationRequest
{
    StyleId = "my-style",
    Title = "My Map Layer",
    GeometryType = "polygon",
    BaseColor = "#3B82F6",
    Opacity = 0.7
};

var result = styleGenerator.GenerateStyle(request);
var styleDefinition = result.StyleDefinition;
var mapLibreStyle = result.MapLibreStyle;
```

#### Generate Choropleth from Data

```csharp
// Sample population data
var populationData = new[] { 1000.0, 5000, 12000, 25000, 50000, 100000 };

var request = new StyleGenerationRequest
{
    Title = "Population Density",
    GeometryType = "polygon",
    FieldName = "population",
    FieldValues = populationData.Cast<object?>(),
    ColorPalette = "YlOrRd",
    ClassCount = 5,
    ClassificationMethod = ClassificationMethod.Jenks
};

var result = styleGenerator.GenerateStyle(request);

// Access analysis results
Console.WriteLine($"Data classification: {result.FieldAnalysis?.Classification}");
Console.WriteLine($"Suggested classes: {result.FieldAnalysis?.SuggestedClasses}");
Console.WriteLine($"Recommendations: {string.Join(", ", result.Recommendations)}");
```

#### Generate Categorical Style

```csharp
var landUseTypes = new[] { "residential", "commercial", "industrial", "forest", "water" };

var request = new StyleGenerationRequest
{
    Title = "Land Use",
    GeometryType = "polygon",
    FieldName = "land_use",
    FieldValues = landUseTypes.Cast<object?>(),
    ColorPalette = "LandUse"
};

var result = styleGenerator.GenerateStyle(request);
// Automatically creates unique value renderer with appropriate colors
```

### 2. API Usage

#### Generate Style via REST API

```bash
POST /api/StyleGeneration/generate
Content-Type: application/json

{
  "geometryType": "polygon",
  "title": "Population Choropleth",
  "fieldName": "population",
  "fieldValues": [1000, 5000, 12000, 25000, 50000],
  "colorPalette": "Blues",
  "classCount": 5,
  "classificationMethod": "Jenks"
}
```

#### Generate from Layer Data

```bash
POST /api/StyleGeneration/generate-from-layer
Content-Type: application/json

{
  "serviceId": "census",
  "layerId": "counties",
  "fieldName": "population",
  "sampleSize": 1000,
  "colorPalette": "YlOrRd",
  "classCount": 7
}
```

#### Get Available Palettes

```bash
GET /api/StyleGeneration/palettes

# Response:
[
  {
    "name": "Blues",
    "colors7": ["#EFF3FF", "#C6DBEF", "#9ECAE1", "#6BAED6", "#4292C6", "#2171B5", "#084594"],
    "colors5": ["#EFF3FF", "#BDD7E7", "#6BAED6", "#3182BD", "#08519C"]
  },
  ...
]
```

#### Apply Style Template

```bash
POST /api/StyleGeneration/templates/choropleth/apply
Content-Type: application/json

{
  "styleId": "my-choropleth",
  "title": "Population Map",
  "colorPalette": "YlOrRd",
  "classCount": 7
}
```

### 3. Blazor Component Usage

```razor
@using Honua.MapSDK.Components.Styling
@using Honua.Server.Core.Metadata

<StyleEditor
    Title="Create Map Style"
    ShowTemplates="true"
    InitialStyle="@_currentStyle"
    OnStyleApplied="@HandleStyleApplied"
    OnCancelled="@HandleCancel" />

@code {
    private StyleDefinition? _currentStyle;

    private async Task HandleStyleApplied(StyleDefinition style)
    {
        // Apply the style to your map layer
        await MapService.ApplyStyleAsync(style);
    }

    private void HandleCancel()
    {
        // Handle cancellation
    }
}
```

### 4. Classification Examples

#### Jenks Natural Breaks (Recommended for Most Data)

```csharp
var data = new[] { 10.2, 15.7, 23.4, 31.8, 45.2, 67.9, 89.1, 102.3 };
var breaks = ClassificationStrategy.Classify(data, 5, ClassificationMethod.Jenks);

// Result: Natural groupings minimizing within-class variance
// Example breaks: [23.4, 45.2, 67.9, 89.1]
```

#### Quantile (Equal Feature Counts)

```csharp
var data = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
var breaks = ClassificationStrategy.Classify(data, 4, ClassificationMethod.Quantile);

// Result: [3, 5, 8] - approximately equal number of values in each class
```

#### Find Optimal Classification

```csharp
var data = GetYourData();
var method = ClassificationStrategy.GetRecommendedMethod(data);
var optimalClasses = ClassificationStrategy.FindOptimalClassCount(data, method);

Console.WriteLine($"Recommended: {method} with {optimalClasses} classes");
```

### 5. Color Palette Examples

#### Sequential Palettes (Low to High Values)

```csharp
// Blues - for water depth, temperature
var blues = CartographicPalettes.GetPalette("Blues", 7);

// YellowGreenBlue - for elevation, precipitation
var ylgnbu = CartographicPalettes.GetPalette("YellowGreenBlue", 7);

// Reds - for heat, population density
var reds = CartographicPalettes.GetPalette("Reds", 7);
```

#### Diverging Palettes (Values Around Midpoint)

```csharp
// Spectral - for deviation from average
var spectral = CartographicPalettes.GetPalette("Spectral", 9);

// RedBlue - for change data (negative to positive)
var redBlue = CartographicPalettes.GetPalette("RedBlue", 9);
```

#### Qualitative Palettes (Categories)

```csharp
// Set1 - for distinct categories (up to 9)
var set1 = CartographicPalettes.GetPalette("Set1", 5);

// Paired - for paired categories (up to 12)
var paired = CartographicPalettes.GetPalette("Paired", 8);
```

#### Context-Specific Palettes

```csharp
// Land Use - pre-configured for LULC data
var landUse = CartographicPalettes.GetPalette("LandUse", 8);

// Demographics - optimized for population data
var demographics = CartographicPalettes.GetPalette("Demographics", 7);

// Environmental - for environmental indicators
var environmental = CartographicPalettes.GetPalette("Environmental", 9);

// Traffic - green (good) to red (congested)
var traffic = CartographicPalettes.GetPalette("Traffic", 5);
```

### 6. Template Library Usage

```csharp
// Get available templates
var templates = StyleTemplateLibrary.GetTemplateNames();

// Get templates for specific geometry
var polygonTemplates = StyleTemplateLibrary.GetTemplatesByGeometry("polygon");

// Apply a template
var options = new StyleTemplateOptions
{
    StyleId = "roads-style",
    Title = "Road Network",
    ColorPalette = "Set1",
    ClassificationField = "road_type"
};

var style = StyleTemplateLibrary.ApplyTemplate("road-network", options);
```

## MapLibre GL JS Integration

The generated styles are fully compatible with MapLibre GL JS:

```javascript
// Style generated by Honua
const generatedStyle = await fetch('/api/StyleGeneration/generate', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    geometryType: 'polygon',
    fieldName: 'population',
    fieldValues: populationData,
    colorPalette: 'YlOrRd',
    classCount: 7
  })
});

const { mapLibreStyle } = await generatedStyle.json();

// Apply to MapLibre map
map.addLayer(mapLibreStyle.layers[0]);
```

## Best Practices

### 1. Choose Appropriate Color Schemes

**Sequential palettes** for ordered data (low to high):
- Use single-hue (Blues, Greens) for simple relationships
- Use multi-hue (YellowGreenBlue) for broader ranges

**Diverging palettes** for data with meaningful midpoint:
- Use when showing deviation from average
- Use for change data (growth/decline, profit/loss)

**Qualitative palettes** for categorical data:
- Limit to 12 categories maximum
- Use Set1 for maximum distinction (up to 9 categories)
- Use Paired for related categories

### 2. Classification Method Selection

- **Default to Jenks** for most datasets
- Use **Quantile** when you need equal representation
- Use **Equal Interval** for easily understood ranges
- Use **Logarithmic** for highly skewed data (e.g., population in cities)
- Use **Standard Deviation** for normally distributed data

### 3. Class Count Guidelines

- **3-5 classes**: Simple patterns, general audience
- **6-7 classes**: Balanced detail and readability (recommended)
- **8-10 classes**: Complex patterns, expert audience
- **11-12 classes**: Maximum supported (use sparingly)

### 4. Accessibility

All palettes are designed to be:
- **Colorblind-safe**: Distinguishable by most color vision deficiencies
- **Print-friendly**: Work in grayscale
- **WCAG compliant**: Meet accessibility standards

### 5. Performance Considerations

- For **high-density point data** (>10,000 features):
  - Consider using heatmaps instead of individual points
  - Enable clustering automatically suggested by the system

- For **large polygon datasets**:
  - Limit unique value categories to 12
  - Use simpler stroke widths

## Advanced Features

### Goodness of Variance Fit (GVF)

Evaluate classification quality:

```csharp
var data = GetYourData();
var breaks = ClassificationStrategy.Classify(data, 7, ClassificationMethod.Jenks);
var gvf = ClassificationStrategy.CalculateGVF(data, breaks);

Console.WriteLine($"GVF Score: {gvf:P1}"); // e.g., "GVF Score: 94.5%"
// Higher is better; >90% is excellent
```

### Data Analysis

```csharp
var analyzer = new DataAnalyzer();
var analysis = analyzer.AnalyzeField(values, "fieldName");

Console.WriteLine($"Data Type: {analysis.DataType}");
Console.WriteLine($"Classification: {analysis.Classification}");
Console.WriteLine($"Mean: {analysis.Mean:F2}");
Console.WriteLine($"Std Dev: {analysis.StdDev:F2}");
Console.WriteLine($"Skewness: {analysis.Skewness:F2}");
Console.WriteLine($"Recommended Classes: {analysis.SuggestedClasses}");
Console.WriteLine($"Recommended Palette: {analysis.GetRecommendedPalette()}");
```

### Geometry Analysis

```csharp
var coordinates = features.Select(f => (f.X, f.Y));
var geometryAnalysis = analyzer.AnalyzeGeometryDistribution(coordinates);

Console.WriteLine($"Feature Count: {geometryAnalysis.FeatureCount}");
Console.WriteLine($"Density: {geometryAnalysis.Density}");
Console.WriteLine($"Should Cluster: {geometryAnalysis.ShouldCluster}");
Console.WriteLine($"Should Use Heatmap: {geometryAnalysis.ShouldUseHeatmap}");
```

## OGC Standards Compatibility

The system generates styles compatible with:
- **OGC SLD/SE** (Styled Layer Descriptor/Symbology Encoding)
- **MapLibre Style Specification v8**
- **Esri Drawing Info** (for ArcGIS compatibility)
- **KML Styles**

## Caching and Performance

Styles can be cached for reuse:

```csharp
// Generate once
var style = styleGenerator.GenerateStyle(request);

// Save to repository
await styleRepository.CreateAsync(style.StyleDefinition);

// Reuse later
var cachedStyle = await styleRepository.GetAsync(style.StyleId);
```

## Troubleshooting

### Issue: Colors don't distinguish well

**Solution**: Try a different palette type or increase class count

```csharp
// If qualitative palette doesn't work well, try sequential
ColorPalette = "Blues" // instead of "Set1"
```

### Issue: Too many categories

**Solution**: Group into broader categories or use graduated colors

```csharp
// Limit to top categories
var topCategories = categoryData.Take(12);
```

### Issue: Skewed data visualization

**Solution**: Use logarithmic classification

```csharp
ClassificationMethod = ClassificationMethod.Logarithmic
```

## API Reference

Full API documentation: `/swagger` (when running Honua Server)

Key endpoints:
- `POST /api/StyleGeneration/generate` - Generate style from data
- `POST /api/StyleGeneration/generate-from-layer` - Generate from layer
- `GET /api/StyleGeneration/palettes` - List color palettes
- `GET /api/StyleGeneration/templates` - List style templates
- `POST /api/StyleGeneration/analyze-field` - Analyze field data
- `POST /api/StyleGeneration/recommend-classification` - Get classification recommendations

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
