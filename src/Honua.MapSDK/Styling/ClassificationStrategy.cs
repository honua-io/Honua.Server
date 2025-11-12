// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.MapSDK.Styling;

/// <summary>
/// Classification methods for creating choropleth maps
/// </summary>
public static class ClassificationStrategy
{
    /// <summary>
    /// Classify data using the specified method
    /// </summary>
    public static double[] Classify(IEnumerable<double> values, int classCount, ClassificationMethod method)
    {
        var data = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();

        if (data.Count == 0 || classCount < 2)
        {
            return Array.Empty<double>();
        }

        return method switch
        {
            ClassificationMethod.EqualInterval => EqualInterval(data, classCount),
            ClassificationMethod.Quantile => Quantile(data, classCount),
            ClassificationMethod.Jenks => JenksNaturalBreaks(data, classCount),
            ClassificationMethod.StandardDeviation => StandardDeviation(data, classCount),
            ClassificationMethod.GeometricInterval => GeometricInterval(data, classCount),
            ClassificationMethod.Logarithmic => Logarithmic(data, classCount),
            _ => EqualInterval(data, classCount)
        };
    }

    /// <summary>
    /// Equal Interval - divides the range into equal-sized subranges
    /// </summary>
    public static double[] EqualInterval(List<double> sortedData, int classCount)
    {
        if (sortedData.Count == 0) return Array.Empty<double>();

        var min = sortedData.First();
        var max = sortedData.Last();
        var range = max - min;

        if (range == 0) return new[] { min };

        var breaks = new double[classCount - 1];
        var interval = range / classCount;

        for (int i = 0; i < classCount - 1; i++)
        {
            breaks[i] = min + (interval * (i + 1));
        }

        return breaks;
    }

    /// <summary>
    /// Quantile - each class contains the same number of features
    /// </summary>
    public static double[] Quantile(List<double> sortedData, int classCount)
    {
        if (sortedData.Count == 0) return Array.Empty<double>();
        if (sortedData.Count < classCount) return sortedData.Distinct().Take(classCount - 1).ToArray();

        var breaks = new double[classCount - 1];
        var itemsPerClass = sortedData.Count / (double)classCount;

        for (int i = 0; i < classCount - 1; i++)
        {
            var position = (int)Math.Ceiling(itemsPerClass * (i + 1));
            if (position >= sortedData.Count) position = sortedData.Count - 1;
            breaks[i] = sortedData[position];
        }

        return breaks.Distinct().ToArray();
    }

    /// <summary>
    /// Jenks Natural Breaks - minimizes variance within classes and maximizes variance between classes
    /// Uses the Jenks-Fisher algorithm for optimal classification
    /// </summary>
    public static double[] JenksNaturalBreaks(List<double> sortedData, int classCount)
    {
        if (sortedData.Count == 0) return Array.Empty<double>();
        if (sortedData.Count < classCount) return sortedData.Distinct().Take(classCount - 1).ToArray();

        int n = sortedData.Count;

        // Initialize matrices
        var lowerClassLimits = new int[n + 1, classCount + 1];
        var variance = new double[n + 1, classCount + 1];

        // Initialize variance matrix with infinity
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= classCount; j++)
            {
                variance[i, j] = double.PositiveInfinity;
            }
        }

        // One class case
        for (int i = 1; i <= n; i++)
        {
            var sum = 0.0;
            var sumSquares = 0.0;

            for (int j = 0; j < i; j++)
            {
                sum += sortedData[j];
                sumSquares += sortedData[j] * sortedData[j];
            }

            var mean = sum / i;
            variance[i, 1] = sumSquares - i * mean * mean;
            lowerClassLimits[i, 1] = 0;
        }

        // Multiple classes
        for (int k = 2; k <= classCount; k++)
        {
            for (int i = k; i <= n; i++)
            {
                for (int j = k - 1; j < i; j++)
                {
                    var sum = 0.0;
                    var sumSquares = 0.0;
                    var count = i - j;

                    for (int m = j; m < i; m++)
                    {
                        sum += sortedData[m];
                        sumSquares += sortedData[m] * sortedData[m];
                    }

                    var mean = sum / count;
                    var classVariance = sumSquares - count * mean * mean;
                    var totalVariance = variance[j, k - 1] + classVariance;

                    if (totalVariance < variance[i, k])
                    {
                        variance[i, k] = totalVariance;
                        lowerClassLimits[i, k] = j;
                    }
                }
            }
        }

        // Extract break points
        var breaks = new double[classCount - 1];
        var breakIndex = classCount - 1;
        var currentIndex = n;

        for (int k = classCount; k > 1; k--)
        {
            currentIndex = lowerClassLimits[currentIndex, k];
            breaks[--breakIndex] = sortedData[currentIndex];
        }

        return breaks;
    }

    /// <summary>
    /// Standard Deviation - classifies based on standard deviations from the mean
    /// </summary>
    public static double[] StandardDeviation(List<double> sortedData, int classCount)
    {
        if (sortedData.Count == 0) return Array.Empty<double>();

        var mean = sortedData.Average();
        var stdDev = Math.Sqrt(sortedData.Sum(v => Math.Pow(v - mean, 2)) / sortedData.Count);

        if (stdDev == 0) return new[] { mean };

        var breaks = new List<double>();
        var halfClasses = classCount / 2;

        // Generate breaks above and below mean
        for (int i = -halfClasses; i <= halfClasses; i++)
        {
            if (i == 0) continue; // Skip the mean itself as a break
            var breakValue = mean + (i * stdDev);
            if (breakValue > sortedData.First() && breakValue < sortedData.Last())
            {
                breaks.Add(breakValue);
            }
        }

        return breaks.OrderBy(b => b).Take(classCount - 1).ToArray();
    }

    /// <summary>
    /// Geometric Interval - creates classes based on geometric progression
    /// Good for data with exponential distribution
    /// </summary>
    public static double[] GeometricInterval(List<double> sortedData, int classCount)
    {
        if (sortedData.Count == 0) return Array.Empty<double>();

        var min = sortedData.First();
        var max = sortedData.Last();

        if (min <= 0)
        {
            // Shift data to make all values positive
            var shift = Math.Abs(min) + 1;
            min += shift;
            max += shift;
        }

        if (min == max) return new[] { sortedData.First() };

        var geometricMean = Math.Pow(max / min, 1.0 / classCount);
        var breaks = new double[classCount - 1];

        for (int i = 0; i < classCount - 1; i++)
        {
            breaks[i] = min * Math.Pow(geometricMean, i + 1);

            // Shift back if we had to adjust
            if (sortedData.First() <= 0)
            {
                breaks[i] -= (Math.Abs(sortedData.First()) + 1);
            }
        }

        return breaks;
    }

    /// <summary>
    /// Logarithmic - creates classes using logarithmic scale
    /// Excellent for highly skewed data
    /// </summary>
    public static double[] Logarithmic(List<double> sortedData, int classCount)
    {
        if (sortedData.Count == 0) return Array.Empty<double>();

        var min = sortedData.First();
        var max = sortedData.Last();

        // Adjust for non-positive values
        var shift = 0.0;
        if (min <= 0)
        {
            shift = Math.Abs(min) + 1;
            min += shift;
            max += shift;
        }

        if (min == max) return new[] { sortedData.First() };

        var logMin = Math.Log10(min);
        var logMax = Math.Log10(max);
        var logRange = logMax - logMin;

        var breaks = new double[classCount - 1];
        var interval = logRange / classCount;

        for (int i = 0; i < classCount - 1; i++)
        {
            var logBreak = logMin + (interval * (i + 1));
            breaks[i] = Math.Pow(10, logBreak) - shift;
        }

        return breaks;
    }

    /// <summary>
    /// Calculate Goodness of Variance Fit (GVF) to evaluate classification quality
    /// Returns a value between 0 and 1, where 1 is perfect fit
    /// </summary>
    public static double CalculateGVF(List<double> sortedData, double[] breaks)
    {
        if (sortedData.Count == 0 || breaks.Length == 0) return 0;

        var mean = sortedData.Average();
        var SDAM = sortedData.Sum(v => Math.Pow(v - mean, 2)); // Sum of Squared Deviations from Array Mean

        if (SDAM == 0) return 1.0;

        // Calculate SDCM (Sum of Squared Deviations from Class Means)
        var SDCM = 0.0;
        var allBreaks = new List<double> { sortedData.First() };
        allBreaks.AddRange(breaks);
        allBreaks.Add(sortedData.Last());

        for (int i = 0; i < allBreaks.Count - 1; i++)
        {
            var classValues = sortedData.Where(v => v >= allBreaks[i] && v < allBreaks[i + 1]).ToList();
            if (classValues.Count > 0)
            {
                var classMean = classValues.Average();
                SDCM += classValues.Sum(v => Math.Pow(v - classMean, 2));
            }
        }

        return 1 - (SDCM / SDAM);
    }

    /// <summary>
    /// Find the optimal number of classes by comparing GVF scores
    /// </summary>
    public static int FindOptimalClassCount(List<double> sortedData, ClassificationMethod method, int minClasses = 3, int maxClasses = 10)
    {
        if (sortedData.Count < minClasses) return sortedData.Count;

        var bestGVF = 0.0;
        var bestClassCount = minClasses;

        for (int classCount = minClasses; classCount <= maxClasses; classCount++)
        {
            var breaks = Classify(sortedData, classCount, method);
            var gvf = CalculateGVF(sortedData, breaks);

            if (gvf > bestGVF)
            {
                bestGVF = gvf;
                bestClassCount = classCount;
            }

            // If we achieve very high GVF, no need to continue
            if (bestGVF > 0.95) break;
        }

        return bestClassCount;
    }

    /// <summary>
    /// Get recommended classification method based on data characteristics
    /// </summary>
    public static ClassificationMethod GetRecommendedMethod(List<double> sortedData)
    {
        if (sortedData.Count == 0) return ClassificationMethod.EqualInterval;

        var mean = sortedData.Average();
        var stdDev = Math.Sqrt(sortedData.Sum(v => Math.Pow(v - mean, 2)) / sortedData.Count);

        if (stdDev == 0) return ClassificationMethod.EqualInterval;

        // Calculate skewness
        var skewness = sortedData.Sum(v => Math.Pow((v - mean) / stdDev, 3)) / sortedData.Count;

        // Highly skewed data
        if (Math.Abs(skewness) > 1.5)
        {
            return ClassificationMethod.Logarithmic;
        }

        // Moderately skewed
        if (Math.Abs(skewness) > 0.5)
        {
            return ClassificationMethod.Jenks;
        }

        // Check for exponential distribution
        var min = sortedData.First();
        var max = sortedData.Last();
        if (min > 0 && max / min > 100)
        {
            return ClassificationMethod.GeometricInterval;
        }

        // Normal distribution - Jenks works well
        return ClassificationMethod.Jenks;
    }
}

/// <summary>
/// Classification methods for choropleth mapping
/// </summary>
public enum ClassificationMethod
{
    /// <summary>
    /// Equal Interval - divides range into equal-sized subranges
    /// </summary>
    EqualInterval,

    /// <summary>
    /// Quantile - each class contains the same number of features
    /// </summary>
    Quantile,

    /// <summary>
    /// Jenks Natural Breaks - minimizes within-class variance
    /// </summary>
    Jenks,

    /// <summary>
    /// Standard Deviation - classes based on standard deviations from mean
    /// </summary>
    StandardDeviation,

    /// <summary>
    /// Geometric Interval - classes based on geometric progression
    /// </summary>
    GeometricInterval,

    /// <summary>
    /// Logarithmic - classes using logarithmic scale
    /// </summary>
    Logarithmic
}
