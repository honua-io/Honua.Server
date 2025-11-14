// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Honua.Server.Services.Styling;

/// <summary>
/// Analyzes feature data to determine optimal styling strategies
/// </summary>
public class DataAnalyzer
{
    // Classification thresholds
    private const int MaxCategoricalUniqueValues = 12;
    private const double CategoricalUniqueRatioThreshold = 0.1;
    private const double UniformDistributionSkewnessThreshold = 0.5;

    // String field categorization
    private const int MaxStringCategoricalUniqueValues = 50;
    private const double StringCategoricalRatioThreshold = 0.5;
    private const int MaxStringCategoriesToDisplay = 12;
    private const int MaxCategoryCountToReturn = 100;

    // Class count suggestions
    private const int MinClassCount = 5;
    private const int DefaultClassCount = 7;
    private const int MaxTemporalClasses = 10;
    private const int MaxSuggestedClasses = 10;

    // Sampling limits
    private const int DataTypeSampleSize = 100;
    private const int SemanticCategorySampleSize = 100;
    private const int GeometrySampleSize = 1000;
    private const int NearestNeighborSampleSize = 100;
    private const int DateIntervalSampleLimit = 100;

    // Type detection thresholds
    private const double TypeDetectionConfidenceThreshold = 0.8;

    // Geometry analysis thresholds
    private const int MinPointsForClustering = 100;
    private const double DensityThresholdForClustering = 0.001;
    private const int MinPointsForHeatmap = 1000;
    private const double DensityThresholdForHeatmap = 0.01;

    // Numeric comparison tolerance
    private const double NumericComparisonTolerance = 0.0001;

    /// <summary>
    /// Analyze a field to determine its characteristics for styling
    /// </summary>
    public FieldAnalysisResult AnalyzeField(IEnumerable<object?> values, string fieldName)
    {
        var valuesList = values.Where(v => v != null).ToList();
        var totalCount = valuesList.Count;

        if (totalCount == 0)
        {
            return new FieldAnalysisResult
            {
                FieldName = fieldName,
                DataType = DataType.Unknown,
                Classification = DataClassification.Categorical,
            };
        }

        // Detect data type
        var dataType = this.DetectDataType(valuesList);
        var uniqueValues = valuesList.Distinct().ToList();
        var uniqueCount = uniqueValues.Count;
        var uniqueRatio = (double)uniqueCount / totalCount;

        var result = new FieldAnalysisResult
        {
            FieldName = fieldName,
            DataType = dataType,
            TotalCount = totalCount,
            UniqueCount = uniqueCount,
            UniqueRatio = uniqueRatio,
        };

        // Determine classification and calculate statistics based on data type
        switch (dataType)
        {
            case DataType.Numeric:
                this.AnalyzeNumericField(valuesList, result);
                break;

            case DataType.DateTime:
                this.AnalyzeDateTimeField(valuesList, result);
                break;

            case DataType.String:
                this.AnalyzeStringField(valuesList, result);
                break;

            case DataType.Boolean:
                result.Classification = DataClassification.Categorical;
                result.IsBoolean = true;
                break;
        }

        return result;
    }

    /// <summary>
    /// Analyze numeric field
    /// </summary>
    private void AnalyzeNumericField(List<object?> values, FieldAnalysisResult result)
    {
        var numbers = values.Select(v => Convert.ToDouble(v)).OrderBy(n => n).ToList();

        result.MinValue = numbers.First();
        result.MaxValue = numbers.Last();
        result.Mean = numbers.Average();
        result.Median = this.CalculateMedian(numbers);
        result.StdDev = this.CalculateStdDev(numbers, result.Mean);

        // Determine if data is diverging (has meaningful zero or midpoint)
        var range = result.MaxValue - result.MinValue;
        var hasNegative = result.MinValue < 0;
        var hasPositive = result.MaxValue > 0;

        if (hasNegative && hasPositive)
        {
            result.Classification = DataClassification.Diverging;
            result.DivergingMidpoint = 0;
        }
        else if (result.UniqueCount <= MaxCategoricalUniqueValues && result.UniqueRatio < CategoricalUniqueRatioThreshold)
        {
            // Few unique values - treat as categorical
            result.Classification = DataClassification.Categorical;
            result.IsCategorical = true;
        }
        else
        {
            result.Classification = DataClassification.Sequential;
        }

        // Calculate distribution characteristics
        result.Skewness = this.CalculateSkewness(numbers, result.Mean, result.StdDev);
        result.IsUniformDistribution = Math.Abs(result.Skewness) < UniformDistributionSkewnessThreshold;

        // Suggest number of classes for classification
        result.SuggestedClasses = this.SuggestClassCount(result.UniqueCount, numbers.Count);
    }

    /// <summary>
    /// Analyze datetime field
    /// </summary>
    private void AnalyzeDateTimeField(List<object?> values, FieldAnalysisResult result)
    {
        var dates = values.Select(v =>
        {
            if (v is DateTime dt) return dt;
            if (v is DateTimeOffset dto) return dto.DateTime;
            if (v is string s && DateTime.TryParse(s, out var parsed)) return parsed;
            return DateTime.MinValue;
        }).Where(d => d != DateTime.MinValue).OrderBy(d => d).ToList();

        if (dates.Count > 0)
        {
            result.MinDate = dates.First();
            result.MaxDate = dates.Last();
            result.Classification = DataClassification.Temporal;

            // Detect time intervals
            if (dates.Count > 1)
            {
                var intervals = new List<TimeSpan>();
                for (int i = 1; i < Math.Min(dates.Count, DateIntervalSampleLimit); i++)
                {
                    intervals.Add(dates[i] - dates[i - 1]);
                }

                var avgInterval = TimeSpan.FromTicks((long)intervals.Average(ts => ts.Ticks));
                result.TemporalInterval = avgInterval;
            }
        }

        result.SuggestedClasses = Math.Min(result.UniqueCount, MaxTemporalClasses);
    }

    /// <summary>
    /// Analyze string field
    /// </summary>
    private void AnalyzeStringField(List<object?> values, FieldAnalysisResult result)
    {
        var strings = values.Select(v => v?.ToString() ?? "").ToList();

        // Calculate string length statistics
        var lengths = strings.Select(s => s.Length).ToList();
        result.AvgStringLength = lengths.Average();
        result.MaxStringLength = lengths.Max();

        // Check if it's a categorical field
        if (result.UniqueCount <= MaxStringCategoricalUniqueValues && result.UniqueRatio < StringCategoricalRatioThreshold)
        {
            result.Classification = DataClassification.Categorical;
            result.IsCategorical = true;
            result.SuggestedClasses = Math.Min(result.UniqueCount, MaxStringCategoriesToDisplay);

            // Get category distribution
            result.CategoryCounts = strings
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .Take(MaxCategoryCountToReturn)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        else
        {
            result.Classification = DataClassification.Categorical;
            result.IsCategorical = false; // Too many categories to visualize effectively
            result.SuggestedClasses = 0;
        }

        // Detect potential land use or demographic categories
        result.SemanticCategory = this.DetectSemanticCategory(strings);
    }

    /// <summary>
    /// Detect data type from sample values
    /// </summary>
    private DataType DetectDataType(List<object?> values)
    {
        if (values.Count == 0) return DataType.Unknown;

        var sample = values.Take(DataTypeSampleSize).ToList();
        var numericCount = 0;
        var dateCount = 0;
        var boolCount = 0;

        foreach (var value in sample)
        {
            if (value == null) continue;

            // Check for numeric
            if (value is int || value is long || value is float || value is double || value is decimal)
            {
                numericCount++;
                continue;
            }

            // Check for DateTime
            if (value is DateTime || value is DateTimeOffset)
            {
                dateCount++;
                continue;
            }

            // Check for boolean
            if (value is bool)
            {
                boolCount++;
                continue;
            }

            // Try parsing string values
            var strValue = value.ToString();
            if (double.TryParse(strValue, out _))
            {
                numericCount++;
            }
            else if (DateTime.TryParse(strValue, out _))
            {
                dateCount++;
            }
            else if (bool.TryParse(strValue, out _))
            {
                boolCount++;
            }
        }

        var total = sample.Count;
        if (numericCount > total * TypeDetectionConfidenceThreshold) return DataType.Numeric;
        if (dateCount > total * TypeDetectionConfidenceThreshold) return DataType.DateTime;
        if (boolCount > total * TypeDetectionConfidenceThreshold) return DataType.Boolean;

        return DataType.String;
    }

    /// <summary>
    /// Detect semantic category from string values
    /// </summary>
    private SemanticCategory DetectSemanticCategory(List<string> values)
    {
        var sample = values.Take(SemanticCategorySampleSize).Select(v => v.ToLowerInvariant()).ToList();

        // Land use keywords
        var landUseKeywords = new[] { "residential", "commercial", "industrial", "forest", "water", "agriculture", "urban", "rural", "park", "wetland" };
        if (sample.Any(s => landUseKeywords.Any(k => s.Contains(k))))
        {
            return SemanticCategory.LandUse;
        }

        // Demographics keywords
        var demographicsKeywords = new[] { "population", "age", "income", "education", "race", "ethnicity", "household" };
        if (sample.Any(s => demographicsKeywords.Any(k => s.Contains(k))))
        {
            return SemanticCategory.Demographics;
        }

        // Environmental keywords
        var environmentalKeywords = new[] { "temperature", "precipitation", "pollution", "air quality", "water quality", "emission", "climate" };
        if (sample.Any(s => environmentalKeywords.Any(k => s.Contains(k))))
        {
            return SemanticCategory.Environmental;
        }

        return SemanticCategory.Generic;
    }

    /// <summary>
    /// Suggest optimal number of classes for classification
    /// </summary>
    private int SuggestClassCount(int uniqueCount, int totalCount)
    {
        if (uniqueCount <= MinClassCount) return uniqueCount;
        if (uniqueCount <= MaxCategoricalUniqueValues) return Math.Min(uniqueCount, DefaultClassCount);

        // Use Sturges' formula for larger datasets
        var sturges = (int)Math.Ceiling(Math.Log2(totalCount) + 1);
        return Math.Clamp(sturges, MinClassCount, MaxSuggestedClasses);
    }

    /// <summary>
    /// Calculate median
    /// </summary>
    private double CalculateMedian(List<double> sortedNumbers)
    {
        int n = sortedNumbers.Count;
        if (n == 0) return 0;

        if (n % 2 == 1)
        {
            return sortedNumbers[n / 2];
        }
        else
        {
            return (sortedNumbers[n / 2 - 1] + sortedNumbers[n / 2]) / 2.0;
        }
    }

    /// <summary>
    /// Calculate standard deviation
    /// </summary>
    private double CalculateStdDev(List<double> numbers, double mean)
    {
        if (numbers.Count < 2) return 0;

        double sumSquaredDiff = numbers.Sum(n => Math.Pow(n - mean, 2));
        return Math.Sqrt(sumSquaredDiff / (numbers.Count - 1));
    }

    /// <summary>
    /// Calculate skewness
    /// </summary>
    private double CalculateSkewness(List<double> numbers, double mean, double stdDev)
    {
        if (numbers.Count < 3 || stdDev == 0) return 0;

        int n = numbers.Count;
        double sumCubedDiff = numbers.Sum(x => Math.Pow((x - mean) / stdDev, 3));
        return (n / ((n - 1.0) * (n - 2.0))) * sumCubedDiff;
    }

    /// <summary>
    /// Analyze geometry distribution for point density detection
    /// </summary>
    public GeometryAnalysisResult AnalyzeGeometryDistribution(IEnumerable<(double x, double y)> coordinates)
    {
        var points = coordinates.ToList();
        var count = points.Count;

        if (count == 0)
        {
            return new GeometryAnalysisResult
            {
                FeatureCount = 0,
                ShouldCluster = false
            };
        }

        // Calculate bounding box
        var minX = points.Min(p => p.x);
        var maxX = points.Max(p => p.x);
        var minY = points.Min(p => p.y);
        var maxY = points.Max(p => p.y);

        var area = (maxX - minX) * (maxY - minY);
        var density = area > 0 ? count / area : 0;

        // Calculate average nearest neighbor distance (sample for large datasets)
        var sampleSize = Math.Min(count, GeometrySampleSize);
        var sample = points.Take(sampleSize).ToList();
        var avgNearestDistance = this.CalculateAverageNearestNeighbor(sample);

        var result = new GeometryAnalysisResult
        {
            FeatureCount = count,
            BoundingBox = new[] { minX, minY, maxX, maxY, },
            Density = density,
            AverageNearestNeighborDistance = avgNearestDistance,
            ShouldCluster = count > MinPointsForClustering && density > DensityThresholdForClustering,
            ShouldUseHeatmap = count > MinPointsForHeatmap && density > DensityThresholdForHeatmap,
        };

        return result;
    }

    /// <summary>
    /// Calculate average nearest neighbor distance
    /// </summary>
    private double CalculateAverageNearestNeighbor(List<(double x, double y)> points)
    {
        if (points.Count < 2) return 0;

        var distances = new List<double>();

        for (int i = 0; i < Math.Min(points.Count, NearestNeighborSampleSize); i++)
        {
            var minDistance = double.MaxValue;
            for (int j = 0; j < points.Count; j++)
            {
                if (i == j) continue;

                var dx = points[i].x - points[j].x;
                var dy = points[i].y - points[j].y;
                var distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            if (minDistance < double.MaxValue)
            {
                distances.Add(minDistance);
            }
        }

        return distances.Any() ? distances.Average() : 0;
    }
}

/// <summary>
/// Result of field analysis
/// </summary>
public class FieldAnalysisResult
{
    public required string FieldName { get; set; }
    public DataType DataType { get; set; }
    public DataClassification Classification { get; set; }
    public int TotalCount { get; set; }
    public int UniqueCount { get; set; }
    public double UniqueRatio { get; set; }

    // Numeric statistics
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double Mean { get; set; }
    public double Median { get; set; }
    public double StdDev { get; set; }
    public double Skewness { get; set; }
    public bool IsUniformDistribution { get; set; }
    public double? DivergingMidpoint { get; set; }

    // DateTime statistics
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public TimeSpan? TemporalInterval { get; set; }

    // String statistics
    public double AvgStringLength { get; set; }
    public int MaxStringLength { get; set; }

    // Categorical information
    public bool IsCategorical { get; set; }
    public bool IsBoolean { get; set; }
    public Dictionary<string, int>? CategoryCounts { get; set; }
    public SemanticCategory SemanticCategory { get; set; }

    // Style suggestions
    public int SuggestedClasses { get; set; }

    /// <summary>
    /// Get recommended palette for this field
    /// </summary>
    public string GetRecommendedPalette()
    {
        return CartographicPalettes.GetRecommendedPalette(Classification, SuggestedClasses);
    }
}

/// <summary>
/// Result of geometry analysis
/// </summary>
public class GeometryAnalysisResult
{
    public int FeatureCount { get; set; }
    public double[]? BoundingBox { get; set; }
    public double Density { get; set; }
    public double AverageNearestNeighborDistance { get; set; }
    public bool ShouldCluster { get; set; }
    public bool ShouldUseHeatmap { get; set; }
}

/// <summary>
/// Data type enumeration
/// </summary>
public enum DataType
{
    Unknown,
    Numeric,
    String,
    DateTime,
    Boolean
}

/// <summary>
/// Semantic category for domain-specific styling
/// </summary>
public enum SemanticCategory
{
    Generic,
    LandUse,
    Demographics,
    Environmental
}
