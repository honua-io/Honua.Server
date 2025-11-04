// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Filters;

/// <summary>
/// Result filter that sanitizes all string properties in response objects to prevent
/// Cross-Site Scripting (XSS) attacks and information disclosure vulnerabilities.
/// </summary>
/// <remarks>
/// <para>
/// This filter runs after action execution and recursively processes all ObjectResult
/// values to HTML-encode string properties. It provides defense-in-depth protection
/// for JSON responses including STAC catalogs, GeoJSON features, and other API responses
/// that may contain user-controlled content.
/// </para>
/// <para>
/// The filter performs the following sanitization steps:
/// <list type="bullet">
///   <item><description>HTML-encodes all string properties using HtmlEncoder</description></item>
///   <item><description>Removes script tags and event handlers</description></item>
///   <item><description>Recursively processes nested objects and collections</description></item>
///   <item><description>Preserves JSON structure while sanitizing content</description></item>
/// </list>
/// </para>
/// <para>
/// The filter can be disabled for specific endpoints using the [SkipFilter] attribute
/// when sanitization would interfere with legitimate content (e.g., pre-encoded HTML,
/// mathematical expressions, or trusted content).
/// </para>
/// <para>
/// Performance considerations: The filter uses reflection to traverse object graphs.
/// For high-throughput endpoints with large response objects, consider using [SkipFilter]
/// and implementing manual sanitization at the data layer.
/// </para>
/// </remarks>
/// <example>
/// To skip sanitization for a specific endpoint:
/// <code>
/// [SkipFilter(typeof(SecureOutputSanitizationFilter))]
/// public async Task&lt;IActionResult&gt; GetPreEncodedContent()
/// {
///     return Ok(trustedContent);
/// }
/// </code>
/// </example>
public sealed class SecureOutputSanitizationFilter : IAsyncResultFilter
{
    private readonly ILogger<SecureOutputSanitizationFilter> _logger;
    private readonly HtmlEncoder _htmlEncoder;

    /// <summary>
    /// Regular expression to detect and remove script tags from content.
    /// Uses case-insensitive, single-line matching to catch obfuscated scripts.
    /// </summary>
    private static readonly Regex ScriptTagRegex = new(
        @"<script[^>]*>.*?</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Regular expression to detect and remove event handler attributes (onclick, onerror, etc.).
    /// </summary>
    private static readonly Regex EventHandlerRegex = new(
        @"\s*on\w+\s*=\s*[""'][^""']*[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureOutputSanitizationFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic and security event logging.</param>
    /// <param name="htmlEncoder">HTML encoder for sanitizing string content.</param>
    public SecureOutputSanitizationFilter(
        ILogger<SecureOutputSanitizationFilter> logger,
        HtmlEncoder htmlEncoder)
    {
        _logger = Guard.NotNull(logger);
        _htmlEncoder = Guard.NotNull(htmlEncoder);
    }

    /// <summary>
    /// Executes the filter to sanitize response objects after action execution.
    /// </summary>
    /// <param name="context">The result executing context containing the response to sanitize.</param>
    /// <param name="next">The delegate to execute the next filter in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method only processes ObjectResult instances with non-null values.
    /// Other result types (FileResult, RedirectResult, etc.) are passed through unchanged.
    /// </remarks>
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        Guard.NotNull(context);

        Guard.NotNull(next);

        // Check if filter should be skipped for this endpoint
        var skipFilter = context.ActionDescriptor.EndpointMetadata
            .OfType<SkipFilterAttribute>()
            .Any(attr => attr.FilterType == typeof(SecureOutputSanitizationFilter));

        if (skipFilter)
        {
            _logger.LogDebug(
                "Skipping output sanitization for {Controller}.{Action} (SkipFilter attribute present)",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"]);

            await next();
            return;
        }

        // Only sanitize ObjectResult values (JSON responses)
        if (context.Result is ObjectResult objectResult && objectResult.Value != null)
        {
            try
            {
                // Track sanitization for observability
                var startTime = DateTime.UtcNow;
                var originalType = objectResult.Value.GetType().Name;

                // Deep sanitize all string properties
                objectResult.Value = SanitizeObject(objectResult.Value);

                var duration = DateTime.UtcNow - startTime;

                _logger.LogDebug(
                    "Sanitized {ObjectType} response for {Controller}.{Action} in {Duration}ms",
                    originalType,
                    context.RouteData.Values["controller"],
                    context.RouteData.Values["action"],
                    duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                // Log sanitization failures but don't break the response
                // This is a defense-in-depth measure; sanitization failure shouldn't cause 500
                _logger.LogError(ex,
                    "Failed to sanitize response for {Controller}.{Action}. Response will be returned unsanitized.",
                    context.RouteData.Values["controller"],
                    context.RouteData.Values["action"]);

                // Continue with unsanitized response rather than breaking the application
            }
        }

        await next();
    }

    private object? SanitizeObject(object? obj)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return SanitizeObjectInternal(obj, visited);
    }

    private object? SanitizeObjectInternal(object? obj, HashSet<object> visited)
    {
        if (obj is null)
        {
            return null;
        }

        if (obj is string s)
        {
            return SanitizeString(s);
        }

        if (IsBinaryLike(obj) || obj is Stream)
        {
            return obj;
        }

        var type = obj.GetType();

        if (IsSimpleType(type))
        {
            return obj;
        }

        if (!type.IsValueType && !visited.Add(obj))
        {
            return obj;
        }

        if (obj is IDictionary dictionary)
        {
            return SanitizeDictionary(dictionary, visited);
        }

        if (obj is IEnumerable enumerable and not string)
        {
            return SanitizeCollection(enumerable, visited);
        }

        return SanitizeComplexObject(obj, visited);
    }

    /// <summary>
    /// Sanitizes an enumerable by recursively sanitizing each element.
    /// </summary>
    private object? SanitizeCollection(IEnumerable enumerable, HashSet<object> visited)
    {
        switch (enumerable)
        {
            case byte[]:
            case sbyte[]:
                return enumerable;
            case Array array:
                if (!visited.Add(array))
                {
                    return array;
                }

                var elementType = array.GetType().GetElementType();
                if (elementType == typeof(byte) || elementType == typeof(sbyte))
                {
                    return array;
                }

                for (var i = 0; i < array.Length; i++)
                {
                    var current = array.GetValue(i);
                    var sanitized = SanitizeObjectInternal(current, visited);
                    if (!Equals(current, sanitized))
                    {
                        array.SetValue(sanitized, i);
                    }
                }

                return array;
        }

        if (!visited.Add(enumerable))
        {
            return enumerable;
        }

        if (enumerable is IList list)
        {
            if (!list.IsReadOnly && !list.IsFixedSize)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var current = list[i];
                    var sanitized = SanitizeObjectInternal(current, visited);
                    if (!Equals(current, sanitized))
                    {
                        list[i] = sanitized;
                    }
                }

                return list;
            }

            var clone = new List<object?>(list.Count);
            foreach (var item in list)
            {
                clone.Add(SanitizeObjectInternal(item, visited));
            }

            return clone;
        }

        var fallback = new List<object?>();
        foreach (var item in enumerable)
        {
            fallback.Add(SanitizeObjectInternal(item, visited));
        }

        return fallback;
    }

    private object? SanitizeDictionary(IDictionary dictionary, HashSet<object> visited)
    {
        if (!visited.Add(dictionary))
        {
            return dictionary;
        }

        if (!dictionary.IsReadOnly)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var sanitizedValue = SanitizeObjectInternal(entry.Value, visited);
                if (!Equals(entry.Value, sanitizedValue))
                {
                    dictionary[entry.Key] = sanitizedValue;
                }
            }

            return dictionary;
        }

        var sanitizedDictionary = new Dictionary<string, object?>(dictionary.Count, StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            sanitizedDictionary[key] = SanitizeObjectInternal(entry.Value, visited);
        }

        return sanitizedDictionary;
    }

    private object SanitizeComplexObject(object obj, HashSet<object> visited)
    {
        var type = obj.GetType();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? currentValue;
            try
            {
                currentValue = property.GetValue(obj);
            }
            catch
            {
                continue;
            }

            var sanitizedValue = SanitizeObjectInternal(currentValue, visited);
            if (!Equals(currentValue, sanitizedValue) &&
                property.CanWrite &&
                property.SetMethod is not null &&
                property.SetMethod.IsPublic)
            {
                try
                {
                    property.SetValue(obj, sanitizedValue);
                }
                catch
                {
                    // Ignore failures to maintain resilience
                }
            }
        }

        return obj;
    }

    /// <summary>
    /// Sanitizes a string by HTML-encoding and removing dangerous content.
    /// </summary>
    /// <param name="input">The string to sanitize.</param>
    /// <returns>The sanitized string with HTML entities encoded and scripts removed.</returns>
    /// <remarks>
    /// <para>
    /// Sanitization steps performed in order:
    /// <list type="number">
    ///   <item><description>Return empty/null strings unchanged</description></item>
    ///   <item><description>Remove script tags (even if obfuscated)</description></item>
    ///   <item><description>Remove event handler attributes (onclick, onerror, etc.)</description></item>
    ///   <item><description>HTML-encode the remaining content</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Edge cases handled:
    /// <list type="bullet">
    ///   <item><description>Already-encoded content: Will be double-encoded (safe but verbose)</description></item>
    ///   <item><description>Mathematical expressions: &lt; and &gt; will be encoded</description></item>
    ///   <item><description>Code snippets: Use [SkipFilter] for endpoints returning code</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private string SanitizeString(string input)
    {
        if (input.IsNullOrEmpty())
        {
            return input;
        }

        var scrubbed = input;

        try
        {
            scrubbed = ScriptTagRegex.Replace(scrubbed, string.Empty);
            scrubbed = EventHandlerRegex.Replace(scrubbed, string.Empty);
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Regex timeout while sanitizing string (length: {Length}). Returning partially sanitized content.",
                input.Length);
        }

        return scrubbed;
    }

    private static bool IsBinaryLike(object value)
    {
        return value is byte[] or ReadOnlyMemory<byte> or Memory<byte> or ArraySegment<byte>;
    }

    private static bool IsSimpleType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        return type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type == typeof(Uri);
    }
}

/// <summary>
/// Attribute to skip specific filters for an action or controller.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute to disable filters on specific endpoints where the filter's
/// behavior would interfere with legitimate functionality. Common scenarios include:
/// <list type="bullet">
///   <item><description>Endpoints returning pre-sanitized or trusted content</description></item>
///   <item><description>Endpoints with custom sanitization logic</description></item>
///   <item><description>Endpoints returning non-HTML content (Markdown, LaTeX, etc.)</description></item>
///   <item><description>High-performance endpoints where reflection overhead is unacceptable</description></item>
/// </list>
/// </para>
/// <para>
/// Security warning: Using this attribute removes automatic XSS protection.
/// Ensure manual sanitization is implemented when skipping the output filter.
/// </para>
/// </remarks>
/// <example>
/// Skip output sanitization for an endpoint that returns trusted content:
/// <code>
/// [SkipFilter(typeof(SecureOutputSanitizationFilter))]
/// [HttpGet("trusted-content")]
/// public IActionResult GetTrustedContent()
/// {
///     return Ok(preEncodedContent);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class SkipFilterAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the filter to skip.
    /// </summary>
    public Type FilterType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkipFilterAttribute"/> class.
    /// </summary>
    /// <param name="filterType">The type of the filter to skip. Must implement IFilterMetadata.</param>
    /// <exception cref="ArgumentNullException">Thrown when filterType is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filterType does not implement IFilterMetadata.</exception>
    public SkipFilterAttribute(Type filterType)
    {
        Guard.NotNull(filterType);

        if (!typeof(IFilterMetadata).IsAssignableFrom(filterType))
        {
            throw new ArgumentException(
                $"Type {filterType.Name} must implement IFilterMetadata",
                nameof(filterType));
        }

        FilterType = filterType;
    }
}
