// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Geoprocessing;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Services.Geoprocessing;

/// <summary>
/// Client-side geoprocessing service using Turf.js
/// Provides high-performance spatial analysis directly in the browser
/// </summary>
public class GeoprocessingService : IGeoprocessingService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _jsModule;
    private bool _initialized = false;

    /// <summary>
    /// Creates a new geoprocessing service instance
    /// </summary>
    /// <param name="jsRuntime">JavaScript runtime for interop</param>
    public GeoprocessingService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Ensures the JavaScript module is loaded
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            try
            {
                _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    "./_content/Honua.MapSDK/js/honua-geoprocessing.js"
                );
                _initialized = true;
            }
            catch (Exception ex)
            {
                throw new GeoprocessingException(
                    "Failed to load geoprocessing module",
                    "MODULE_LOAD_ERROR",
                    ex
                );
            }
        }
    }

    // ========== Geometric Operations ==========

    /// <inheritdoc />
    public async Task<object> BufferAsync(object input, double distance, string units = "meters")
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("buffer", input, distance, units);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Buffer operation failed: {ex.Message}", "BUFFER_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> IntersectAsync(object layer1, object layer2)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("intersect", layer1, layer2);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Intersect operation failed: {ex.Message}", "INTERSECT_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> UnionAsync(object[] layers)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("union", new object[] { layers });
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Union operation failed: {ex.Message}", "UNION_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> DifferenceAsync(object layer1, object layer2)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("difference", layer1, layer2);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Difference operation failed: {ex.Message}", "DIFFERENCE_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> ClipAsync(object clip, object subject)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("clip", clip, subject);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Clip operation failed: {ex.Message}", "CLIP_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> SimplifyAsync(object geometry, double tolerance = 0.01, bool highQuality = false)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("simplify", geometry, tolerance, highQuality);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Simplify operation failed: {ex.Message}", "SIMPLIFY_ERROR", ex);
        }
    }

    // ========== Measurements ==========

    /// <inheritdoc />
    public async Task<double> AreaAsync(object polygon, string units = "meters")
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<double>("area", polygon, units);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Area calculation failed: {ex.Message}", "AREA_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<double> LengthAsync(object line, string units = "meters")
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<double>("length", line, units);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Length calculation failed: {ex.Message}", "LENGTH_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<double> DistanceAsync(Coordinate point1, Coordinate point2, string units = "meters")
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<double>("distance", point1, point2, units);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Distance calculation failed: {ex.Message}", "DISTANCE_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<double> PerimeterAsync(object polygon, string units = "meters")
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<double>("perimeter", polygon, units);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Perimeter calculation failed: {ex.Message}", "PERIMETER_ERROR", ex);
        }
    }

    // ========== Spatial Relationships ==========

    /// <inheritdoc />
    public async Task<bool> ContainsAsync(object container, object contained)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<bool>("contains", container, contained);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Contains test failed: {ex.Message}", "CONTAINS_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IntersectsAsync(object geometry1, object geometry2)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<bool>("intersects", geometry1, geometry2);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Intersects test failed: {ex.Message}", "INTERSECTS_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> WithinAsync(object inner, object outer)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<bool>("within", inner, outer);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Within test failed: {ex.Message}", "WITHIN_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> OverlapsAsync(object geometry1, object geometry2)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<bool>("overlaps", geometry1, geometry2);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Overlaps test failed: {ex.Message}", "OVERLAPS_ERROR", ex);
        }
    }

    // ========== Geometric Calculations ==========

    /// <inheritdoc />
    public async Task<object> CentroidAsync(object geometry)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("centroid", geometry);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Centroid calculation failed: {ex.Message}", "CENTROID_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> ConvexHullAsync(object points)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("convexHull", points);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Convex hull calculation failed: {ex.Message}", "CONVEX_HULL_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<double[]> BboxAsync(object geometry)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<double[]>("bbox", geometry);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Bbox calculation failed: {ex.Message}", "BBOX_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> EnvelopeAsync(object geometry)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("envelope", geometry);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Envelope calculation failed: {ex.Message}", "ENVELOPE_ERROR", ex);
        }
    }

    // ========== Advanced Operations ==========

    /// <inheritdoc />
    public async Task<object> VoronoiAsync(object points, double[]? bbox = null)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("voronoi", points, bbox);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Voronoi calculation failed: {ex.Message}", "VORONOI_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> DissolveAsync(object features, string? propertyName = null)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("dissolve", features, propertyName);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Dissolve operation failed: {ex.Message}", "DISSOLVE_ERROR", ex);
        }
    }

    /// <inheritdoc />
    public async Task<object> TransformAsync(object geometry, string transformFunction)
    {
        await EnsureInitializedAsync();
        try
        {
            if (_jsModule == null)
                throw new GeoprocessingException("Module not initialized", "NOT_INITIALIZED");

            return await _jsModule.InvokeAsync<object>("transform", geometry, transformFunction);
        }
        catch (JSException ex)
        {
            throw new GeoprocessingException($"Transform operation failed: {ex.Message}", "TRANSFORM_ERROR", ex);
        }
    }

    // ========== Batch Operations ==========

    /// <inheritdoc />
    public async Task<GeoprocessingResult> ExecuteOperationAsync(
        GeoprocessingOperationType operationType,
        GeoprocessingParameters parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new GeoprocessingResult
        {
            OperationType = operationType,
            InputParameters = new System.Collections.Generic.Dictionary<string, object>()
        };

        try
        {
            switch (operationType)
            {
                case GeoprocessingOperationType.Buffer:
                    ValidateParameter(parameters.Input, "Input");
                    ValidateParameter(parameters.Distance, "Distance");
                    result.InputParameters["input"] = parameters.Input!;
                    result.InputParameters["distance"] = parameters.Distance!.Value;
                    result.InputParameters["units"] = parameters.Units;
                    result.ResultGeometry = await BufferAsync(parameters.Input!, parameters.Distance!.Value, parameters.Units);
                    break;

                case GeoprocessingOperationType.Intersect:
                    ValidateParameter(parameters.Input, "Input");
                    ValidateParameter(parameters.SecondaryInput, "SecondaryInput");
                    result.InputParameters["layer1"] = parameters.Input!;
                    result.InputParameters["layer2"] = parameters.SecondaryInput!;
                    result.ResultGeometry = await IntersectAsync(parameters.Input!, parameters.SecondaryInput!);
                    break;

                case GeoprocessingOperationType.Union:
                    ValidateParameter(parameters.MultipleInputs, "MultipleInputs");
                    result.InputParameters["layers"] = parameters.MultipleInputs!;
                    result.ResultGeometry = await UnionAsync(parameters.MultipleInputs!);
                    break;

                case GeoprocessingOperationType.Difference:
                    ValidateParameter(parameters.Input, "Input");
                    ValidateParameter(parameters.SecondaryInput, "SecondaryInput");
                    result.InputParameters["layer1"] = parameters.Input!;
                    result.InputParameters["layer2"] = parameters.SecondaryInput!;
                    result.ResultGeometry = await DifferenceAsync(parameters.Input!, parameters.SecondaryInput!);
                    break;

                case GeoprocessingOperationType.Clip:
                    ValidateParameter(parameters.Input, "Input (clip boundary)");
                    ValidateParameter(parameters.SecondaryInput, "SecondaryInput (subject)");
                    result.InputParameters["clip"] = parameters.Input!;
                    result.InputParameters["subject"] = parameters.SecondaryInput!;
                    result.ResultGeometry = await ClipAsync(parameters.Input!, parameters.SecondaryInput!);
                    break;

                case GeoprocessingOperationType.Area:
                    ValidateParameter(parameters.Input, "Input");
                    result.InputParameters["polygon"] = parameters.Input!;
                    result.InputParameters["units"] = parameters.Units;
                    result.NumericResult = await AreaAsync(parameters.Input!, parameters.Units);
                    result.Units = parameters.Units;
                    break;

                case GeoprocessingOperationType.Length:
                    ValidateParameter(parameters.Input, "Input");
                    result.InputParameters["line"] = parameters.Input!;
                    result.InputParameters["units"] = parameters.Units;
                    result.NumericResult = await LengthAsync(parameters.Input!, parameters.Units);
                    result.Units = parameters.Units;
                    break;

                case GeoprocessingOperationType.Distance:
                    ValidateParameter(parameters.Point1, "Point1");
                    ValidateParameter(parameters.Point2, "Point2");
                    result.InputParameters["point1"] = parameters.Point1!;
                    result.InputParameters["point2"] = parameters.Point2!;
                    result.InputParameters["units"] = parameters.Units;
                    result.NumericResult = await DistanceAsync(parameters.Point1!, parameters.Point2!, parameters.Units);
                    result.Units = parameters.Units;
                    break;

                case GeoprocessingOperationType.Centroid:
                    ValidateParameter(parameters.Input, "Input");
                    result.InputParameters["geometry"] = parameters.Input!;
                    result.ResultGeometry = await CentroidAsync(parameters.Input!);
                    break;

                case GeoprocessingOperationType.ConvexHull:
                    ValidateParameter(parameters.Input, "Input");
                    result.InputParameters["points"] = parameters.Input!;
                    result.ResultGeometry = await ConvexHullAsync(parameters.Input!);
                    break;

                case GeoprocessingOperationType.Contains:
                    ValidateParameter(parameters.Input, "Input (container)");
                    ValidateParameter(parameters.SecondaryInput, "SecondaryInput (contained)");
                    result.InputParameters["container"] = parameters.Input!;
                    result.InputParameters["contained"] = parameters.SecondaryInput!;
                    result.BooleanResult = await ContainsAsync(parameters.Input!, parameters.SecondaryInput!);
                    break;

                case GeoprocessingOperationType.Intersects:
                    ValidateParameter(parameters.Input, "Input");
                    ValidateParameter(parameters.SecondaryInput, "SecondaryInput");
                    result.InputParameters["geometry1"] = parameters.Input!;
                    result.InputParameters["geometry2"] = parameters.SecondaryInput!;
                    result.BooleanResult = await IntersectsAsync(parameters.Input!, parameters.SecondaryInput!);
                    break;

                case GeoprocessingOperationType.Within:
                    ValidateParameter(parameters.Input, "Input (inner)");
                    ValidateParameter(parameters.SecondaryInput, "SecondaryInput (outer)");
                    result.InputParameters["inner"] = parameters.Input!;
                    result.InputParameters["outer"] = parameters.SecondaryInput!;
                    result.BooleanResult = await WithinAsync(parameters.Input!, parameters.SecondaryInput!);
                    break;

                case GeoprocessingOperationType.Voronoi:
                    ValidateParameter(parameters.Input, "Input");
                    result.InputParameters["points"] = parameters.Input!;
                    if (parameters.BoundingBox != null)
                        result.InputParameters["bbox"] = parameters.BoundingBox;
                    result.ResultGeometry = await VoronoiAsync(parameters.Input!, parameters.BoundingBox);
                    break;

                case GeoprocessingOperationType.Dissolve:
                    ValidateParameter(parameters.Input, "Input");
                    result.InputParameters["features"] = parameters.Input!;
                    if (parameters.PropertyName != null)
                        result.InputParameters["propertyName"] = parameters.PropertyName;
                    result.ResultGeometry = await DissolveAsync(parameters.Input!, parameters.PropertyName);
                    break;

                case GeoprocessingOperationType.Simplify:
                    ValidateParameter(parameters.Input, "Input");
                    result.InputParameters["geometry"] = parameters.Input!;
                    result.InputParameters["tolerance"] = parameters.Tolerance ?? 0.01;
                    result.InputParameters["highQuality"] = parameters.HighQuality;
                    result.ResultGeometry = await SimplifyAsync(parameters.Input!, parameters.Tolerance ?? 0.01, parameters.HighQuality);
                    break;

                default:
                    throw new GeoprocessingException($"Operation type {operationType} not implemented", "NOT_IMPLEMENTED");
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ErrorCode = ex is GeoprocessingException gex ? gex.ErrorCode : "UNKNOWN_ERROR";
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// Validates that a parameter is not null
    /// </summary>
    private void ValidateParameter(object? parameter, string parameterName)
    {
        if (parameter == null)
        {
            throw new GeoprocessingException(
                $"Required parameter '{parameterName}' is null",
                "INVALID_PARAMETER"
            );
        }
    }
}

/// <summary>
/// Exception thrown by geoprocessing operations
/// </summary>
public class GeoprocessingException : Exception
{
    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Creates a new geoprocessing exception
    /// </summary>
    public GeoprocessingException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a new geoprocessing exception with inner exception
    /// </summary>
    public GeoprocessingException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
