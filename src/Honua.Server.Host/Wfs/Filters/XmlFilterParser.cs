// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Wfs.Filters;

internal static class XmlFilterParser
{
    public static QueryFilter Parse(string xml, LayerDefinition layer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        Guard.NotNull(layer);

        try
        {
            // Use secure XML parsing to prevent XXE attacks
            var document = SecureXmlSettings.ParseSecure(xml, LoadOptions.PreserveWhitespace);
            var filterElement = document.Root?.Name.LocalName == "Filter"
                ? document.Root
                : document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Filter");

            if (filterElement is null)
            {
                throw new InvalidOperationException("XML filter is missing the required <Filter> element.");
            }

            var firstChild = filterElement.Elements().FirstOrDefault();
            if (firstChild is null)
            {
                throw new InvalidOperationException("XML filter does not contain any filter expressions.");
            }

            var expression = ParseNode(firstChild, layer);
            return new QueryFilter(expression);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException("XML filter is not well-formed.", ex);
        }
    }

    private static QueryExpression ParseNode(XElement element, LayerDefinition layer)
    {
        return element.Name.LocalName switch
        {
            "And" => Combine(element, layer, QueryBinaryOperator.And),
            "Or" => Combine(element, layer, QueryBinaryOperator.Or),
            "Not" => ParseNot(element, layer),
            "PropertyIsEqualTo" => ParseComparison(element, layer, QueryBinaryOperator.Equal),
            "PropertyIsNotEqualTo" => ParseComparison(element, layer, QueryBinaryOperator.NotEqual),
            "PropertyIsLessThan" => ParseComparison(element, layer, QueryBinaryOperator.LessThan),
            "PropertyIsLessThanOrEqualTo" => ParseComparison(element, layer, QueryBinaryOperator.LessThanOrEqual),
            "PropertyIsGreaterThan" => ParseComparison(element, layer, QueryBinaryOperator.GreaterThan),
            "PropertyIsGreaterThanOrEqualTo" => ParseComparison(element, layer, QueryBinaryOperator.GreaterThanOrEqual),
            "PropertyIsNull" => ParseNull(element, layer),
            "PropertyIsLike" => ParseLike(element, layer),
            "PropertyIsBetween" => ParseBetween(element, layer),
            "BBOX" => ParseBBox(element, layer),
            "Intersects" => ParseSpatialOperator(element, layer, SpatialPredicate.Intersects),
            "Contains" => ParseSpatialOperator(element, layer, SpatialPredicate.Contains),
            "Within" => ParseSpatialOperator(element, layer, SpatialPredicate.Within),
            "Touches" => ParseSpatialOperator(element, layer, SpatialPredicate.Touches),
            "Crosses" => ParseSpatialOperator(element, layer, SpatialPredicate.Crosses),
            "Overlaps" => ParseSpatialOperator(element, layer, SpatialPredicate.Overlaps),
            "Disjoint" => ParseSpatialOperator(element, layer, SpatialPredicate.Disjoint),
            "Equals" => ParseSpatialOperator(element, layer, SpatialPredicate.Equals),
            "DWithin" => ParseDWithin(element, layer),
            "Beyond" => ParseBeyond(element, layer),
            "During" => ParseTemporalOperator(element, layer, "During"),
            "Before" => ParseTemporalOperator(element, layer, "Before"),
            "After" => ParseTemporalOperator(element, layer, "After"),
            "TEquals" => ParseTemporalOperator(element, layer, "TEquals"),
            _ => throw new InvalidOperationException($"Unsupported XML filter element '{element.Name.LocalName}'.")
        };
    }

    private static QueryExpression Combine(XElement element, LayerDefinition layer, QueryBinaryOperator @operator)
    {
        var children = element.Elements().Select(child => ParseNode(child, layer)).ToList();
        if (children.Count == 0)
        {
            throw new InvalidOperationException($"Element '{element.Name.LocalName}' requires at least one child expression.");
        }

        var result = children[0];
        for (var index = 1; index < children.Count; index++)
        {
            result = new QueryBinaryExpression(result, @operator, children[index]);
        }

        return result;
    }

    private static QueryExpression ParseNot(XElement element, LayerDefinition layer)
    {
        var child = element.Elements().FirstOrDefault()
            ?? throw new InvalidOperationException("<Not> must contain a child expression.");

        var expression = ParseNode(child, layer);
        return new QueryUnaryExpression(QueryUnaryOperator.Not, expression);
    }

    private static QueryExpression ParseComparison(XElement element, LayerDefinition layer, QueryBinaryOperator @operator)
    {
        var property = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException($"Element '{element.Name.LocalName}' is missing a property reference.");

        var literal = element.Elements().FirstOrDefault(child => child.Name.LocalName == "Literal")
            ?? throw new InvalidOperationException($"Element '{element.Name.LocalName}' is missing a literal value.");

        var propertyName = property.Value?.Trim() ?? string.Empty;

        // Check if propertyName contains a function call (e.g., "area(geom)" or "length(geom)")
        var leftExpression = ParsePropertyOrFunction(propertyName, layer);

        var literalText = literal.Value?.Trim() ?? string.Empty;

        // Determine the field type for type conversion
        string? fieldType = null;
        if (leftExpression is QueryFieldReference fieldRef)
        {
            var field = layer.Fields?.FirstOrDefault(f => string.Equals(f.Name, fieldRef.Name, StringComparison.OrdinalIgnoreCase));
            fieldType = field?.DataType;
        }
        else if (leftExpression is QueryFunctionExpression)
        {
            // Functions return numeric values for area, length, buffer distance
            fieldType = "double";
        }

        var typedValue = CqlFilterParserUtils.ConvertToFieldValue(fieldType, literalText);

        return new QueryBinaryExpression(
            leftExpression,
            @operator,
            new QueryConstant(typedValue));
    }

    /// <summary>
    /// Parses a property reference or function call from a string.
    /// Supports: "fieldName", "area(fieldName)", "length(fieldName)", "buffer(fieldName, distance)"
    /// </summary>
    private static QueryExpression ParsePropertyOrFunction(string propertyExpression, LayerDefinition layer)
    {
        var trimmed = propertyExpression.Trim();

        // Check if it's a function call (contains parentheses)
        var openParenIndex = trimmed.IndexOf('(');
        if (openParenIndex > 0 && trimmed.EndsWith(")"))
        {
            var functionName = trimmed.Substring(0, openParenIndex).Trim();
            var argsText = trimmed.Substring(openParenIndex + 1, trimmed.Length - openParenIndex - 2).Trim();

            // Parse function arguments (simple comma-separated list)
            var argTokens = argsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var arguments = new List<QueryExpression>();

            foreach (var argToken in argTokens)
            {
                // Try to parse as field reference or numeric literal
                if (double.TryParse(argToken, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var numValue))
                {
                    arguments.Add(new QueryConstant(numValue));
                }
                else
                {
                    // Resolve as field
                    var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, argToken);
                    arguments.Add(new QueryFieldReference(fieldName));
                }
            }

            return new QueryFunctionExpression(functionName.ToLowerInvariant(), arguments);
        }

        // Not a function - resolve as simple field reference
        var (resolvedFieldName, _) = CqlFilterParserUtils.ResolveField(layer, trimmed);
        return new QueryFieldReference(resolvedFieldName);
    }

    private static QueryExpression ParseNull(XElement element, LayerDefinition layer)
    {
        var property = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException("<PropertyIsNull> is missing a property reference.");

        var propertyName = property.Value?.Trim() ?? string.Empty;
        var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        return new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.Equal,
            new QueryConstant(null));
    }

    private static bool IsPropertyElement(XElement element)
    {
        var localName = element.Name.LocalName;
        return localName == "PropertyName" || localName == "ValueReference";
    }

    private static QueryExpression ParseBBox(XElement element, LayerDefinition layer)
    {
        // Parse property name
        var propertyElement = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException("BBOX requires a property reference.");

        var propertyName = propertyElement.Value?.Trim() ?? string.Empty;
        var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        // Find the envelope element (should be in GML namespace)
        var envelopeElement = element.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Envelope");

        if (envelopeElement == null)
        {
            throw new InvalidOperationException("BBOX requires gml:Envelope.");
        }

        // Parse envelope using GmlGeometryParser
        var geometry = GmlGeometryParser.Parse(envelopeElement, null);

        // BBOX is implemented as Intersects
        return new QuerySpatialExpression(
            SpatialPredicate.Intersects,
            new QueryFieldReference(fieldName),
            new QueryConstant(geometry));
    }

    private static QueryExpression ParseSpatialOperator(XElement element, LayerDefinition layer, SpatialPredicate predicate)
    {
        // First child should be property reference
        var propertyElement = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException($"{predicate} requires a property reference.");

        var propertyName = propertyElement.Value?.Trim() ?? string.Empty;
        var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        // Find geometry element (any element in GML namespace that's not the property)
        var geometryElement = element.Elements()
            .FirstOrDefault(e => e.Name.Namespace.NamespaceName.Contains("gml", StringComparison.OrdinalIgnoreCase)
                              || e.Name.LocalName is "Point" or "LineString" or "Polygon" or "MultiPoint"
                                  or "MultiLineString" or "MultiPolygon" or "Envelope");

        if (geometryElement == null)
        {
            throw new InvalidOperationException($"{predicate} requires a geometry literal.");
        }

        // Parse GML geometry
        var geometry = GmlGeometryParser.Parse(geometryElement, null);

        return new QuerySpatialExpression(
            predicate,
            new QueryFieldReference(fieldName),
            new QueryConstant(geometry));
    }

    private static QueryExpression ParseDWithin(XElement element, LayerDefinition layer)
    {
        // Parse property reference
        var propertyElement = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException("DWithin requires a property reference.");

        var propertyName = propertyElement.Value?.Trim() ?? string.Empty;
        var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        // Find geometry element
        var geometryElement = element.Elements()
            .FirstOrDefault(e => e.Name.Namespace.NamespaceName.Contains("gml", StringComparison.OrdinalIgnoreCase)
                              || e.Name.LocalName is "Point" or "LineString" or "Polygon" or "MultiPoint"
                                  or "MultiLineString" or "MultiPolygon" or "Envelope");

        if (geometryElement == null)
        {
            throw new InvalidOperationException("DWithin requires a geometry literal.");
        }

        // Parse distance element
        var distanceElement = element.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Distance");

        if (distanceElement == null)
        {
            throw new InvalidOperationException("DWithin requires a Distance element.");
        }

        var distanceText = distanceElement.Value?.Trim();
        if (string.IsNullOrEmpty(distanceText) || !double.TryParse(distanceText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var distance))
        {
            throw new InvalidOperationException("DWithin Distance must be a valid number.");
        }

        // Parse unit of measure (default to meters)
        var uom = distanceElement.Attribute("uom")?.Value ?? "meter";
        var distanceInMeters = ConvertDistanceToMeters(distance, uom);

        // Parse GML geometry
        var geometry = GmlGeometryParser.Parse(geometryElement, null);

        return new QuerySpatialExpression(
            SpatialPredicate.DWithin,
            new QueryFieldReference(fieldName),
            new QueryConstant(geometry),
            distanceInMeters);
    }

    private static double ConvertDistanceToMeters(double distance, string uom)
    {
        var normalizedUom = uom.Trim().ToLowerInvariant();

        return normalizedUom switch
        {
            "m" or "meter" or "metre" or "meters" or "metres" => distance,
            "km" or "kilometer" or "kilometre" or "kilometers" or "kilometres" => distance * 1000.0,
            "mi" or "mile" or "miles" => distance * 1609.344,
            "ft" or "foot" or "feet" => distance * 0.3048,
            "yd" or "yard" or "yards" => distance * 0.9144,
            "nmi" or "nauticalmile" or "nauticalmiles" => distance * 1852.0,
            _ => distance // Default to meters if unknown
        };
    }

    private static QueryExpression ParseBeyond(XElement element, LayerDefinition layer)
    {
        // Parse property reference
        var propertyElement = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException("Beyond requires a property reference.");

        var propertyName = propertyElement.Value?.Trim() ?? string.Empty;
        var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        // Find geometry element
        var geometryElement = element.Elements()
            .FirstOrDefault(e => e.Name.Namespace.NamespaceName.Contains("gml", StringComparison.OrdinalIgnoreCase)
                              || e.Name.LocalName is "Point" or "LineString" or "Polygon" or "MultiPoint"
                                  or "MultiLineString" or "MultiPolygon" or "Envelope");

        if (geometryElement == null)
        {
            throw new InvalidOperationException("Beyond requires a geometry literal.");
        }

        // Parse distance element
        var distanceElement = element.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Distance");

        if (distanceElement == null)
        {
            throw new InvalidOperationException("Beyond requires a Distance element.");
        }

        var distanceText = distanceElement.Value?.Trim();
        if (string.IsNullOrEmpty(distanceText) || !double.TryParse(distanceText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var distance))
        {
            throw new InvalidOperationException("Beyond Distance must be a valid number.");
        }

        // Parse unit of measure (default to meters)
        var uom = distanceElement.Attribute("uom")?.Value ?? "meter";
        var distanceInMeters = ConvertDistanceToMeters(distance, uom);

        // Parse GML geometry
        var geometry = GmlGeometryParser.Parse(geometryElement, null);

        return new QuerySpatialExpression(
            SpatialPredicate.Beyond,
            new QueryFieldReference(fieldName),
            new QueryConstant(geometry),
            distanceInMeters);
    }

    private static QueryExpression ParseLike(XElement element, LayerDefinition layer)
    {
        // Get PropertyName
        var propertyElement = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException("PropertyIsLike requires a property reference.");

        var propertyName = propertyElement.Value?.Trim() ?? string.Empty;
        var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        // Get Literal pattern
        var literalElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Literal")
            ?? throw new InvalidOperationException("PropertyIsLike requires a Literal pattern.");

        var pattern = literalElement.Value?.Trim() ?? string.Empty;

        // Get wildCard, singleChar, and escapeChar attributes
        var wildCard = element.Attribute("wildCard")?.Value ?? "*";
        var singleChar = element.Attribute("singleChar")?.Value ?? "?";
        var escapeChar = element.Attribute("escapeChar")?.Value ?? "\\";

        // Convert WFS pattern to SQL LIKE pattern
        var sqlPattern = ConvertWfsPatternToSqlLike(pattern, wildCard, singleChar, escapeChar);

        // Use the Like operator (assuming it's available)
        return new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.Like,
            new QueryConstant(sqlPattern));
    }

    private static string ConvertWfsPatternToSqlLike(string pattern, string wildCard, string singleChar, string escapeChar)
    {
        // Escape SQL LIKE special characters first
        var result = pattern
            .Replace("\\", "\\\\")  // Escape backslashes
            .Replace("%", "\\%")     // Escape SQL wildcard
            .Replace("_", "\\_");    // Escape SQL single char

        // Replace WFS wildcards with SQL LIKE wildcards
        if (wildCard != "%")
        {
            result = result.Replace(wildCard, "%");
        }

        if (singleChar != "_")
        {
            result = result.Replace(singleChar, "_");
        }

        return result;
    }

    private static QueryExpression ParseBetween(XElement element, LayerDefinition layer)
    {
        // Get PropertyName
        var propertyElement = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException("PropertyIsBetween requires a property reference.");

        var propertyName = propertyElement.Value?.Trim() ?? string.Empty;
        var (fieldName, fieldType) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        // Get LowerBoundary and UpperBoundary
        var lowerBoundaryElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "LowerBoundary")
            ?? throw new InvalidOperationException("PropertyIsBetween requires a LowerBoundary element.");

        var upperBoundaryElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "UpperBoundary")
            ?? throw new InvalidOperationException("PropertyIsBetween requires an UpperBoundary element.");

        // Extract literal values from boundaries
        var lowerLiteral = lowerBoundaryElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Literal")
            ?? throw new InvalidOperationException("LowerBoundary must contain a Literal.");

        var upperLiteral = upperBoundaryElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Literal")
            ?? throw new InvalidOperationException("UpperBoundary must contain a Literal.");

        var lowerValue = CqlFilterParserUtils.ConvertToFieldValue(fieldType, lowerLiteral.Value?.Trim() ?? string.Empty);
        var upperValue = CqlFilterParserUtils.ConvertToFieldValue(fieldType, upperLiteral.Value?.Trim() ?? string.Empty);

        // PropertyIsBetween is equivalent to: property >= lower AND property <= upper
        var lowerExpression = new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.GreaterThanOrEqual,
            new QueryConstant(lowerValue));

        var upperExpression = new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.LessThanOrEqual,
            new QueryConstant(upperValue));

        return new QueryBinaryExpression(lowerExpression, QueryBinaryOperator.And, upperExpression);
    }

    private static QueryExpression ParseTemporalOperator(XElement element, LayerDefinition layer, string operatorName)
    {
        // Get PropertyName (temporal field)
        var propertyElement = element.Elements().FirstOrDefault(IsPropertyElement)
            ?? throw new InvalidOperationException($"{operatorName} requires a property reference.");

        var propertyName = propertyElement.Value?.Trim() ?? string.Empty;
        var (fieldName, fieldType) = CqlFilterParserUtils.ResolveField(layer, propertyName);

        // Temporal operators expect datetime fields
        var normalizedType = fieldType?.Trim().ToLowerInvariant();
        if (normalizedType != "datetime" && normalizedType != "datetimeoffset" && normalizedType != "date" && normalizedType != "time")
        {
            throw new InvalidOperationException($"{operatorName} operator requires a temporal field (datetime, datetimeoffset, date, or time). Found: {fieldType ?? "null"}");
        }

        // Get temporal literal or period
        // For simplicity, we'll look for gml:TimePeriod or gml:TimeInstant
        var timeElement = element.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "TimePeriod" or "TimeInstant" or "Literal");

        if (timeElement == null)
        {
            throw new InvalidOperationException($"{operatorName} requires a temporal literal.");
        }

        // Parse based on operator type
        return operatorName switch
        {
            "During" => ParseDuringOperator(fieldName, timeElement),
            "Before" => ParseBeforeOperator(fieldName, timeElement),
            "After" => ParseAfterOperator(fieldName, timeElement),
            "TEquals" => ParseTEqualsOperator(fieldName, timeElement),
            _ => throw new InvalidOperationException($"Unsupported temporal operator: {operatorName}")
        };
    }

    private static QueryExpression ParseDuringOperator(string fieldName, XElement timeElement)
    {
        // During: field value is within the time period
        if (timeElement.Name.LocalName == "TimePeriod")
        {
            var begin = timeElement.Descendants().FirstOrDefault(e => e.Name.LocalName is "begin" or "beginPosition")?.Value?.Trim();
            var end = timeElement.Descendants().FirstOrDefault(e => e.Name.LocalName is "end" or "endPosition")?.Value?.Trim();

            if (string.IsNullOrEmpty(begin) || string.IsNullOrEmpty(end))
            {
                throw new InvalidOperationException("TimePeriod must have begin and end positions.");
            }

            var beginDate = DateTime.Parse(begin, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            var endDate = DateTime.Parse(end, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);

            // field >= begin AND field <= end
            var beginExpr = new QueryBinaryExpression(
                new QueryFieldReference(fieldName),
                QueryBinaryOperator.GreaterThanOrEqual,
                new QueryConstant(beginDate));

            var endExpr = new QueryBinaryExpression(
                new QueryFieldReference(fieldName),
                QueryBinaryOperator.LessThanOrEqual,
                new QueryConstant(endDate));

            return new QueryBinaryExpression(beginExpr, QueryBinaryOperator.And, endExpr);
        }
        else
        {
            throw new InvalidOperationException("During operator requires a TimePeriod.");
        }
    }

    private static QueryExpression ParseBeforeOperator(string fieldName, XElement timeElement)
    {
        // Before: field value is before the time instant
        var timeValue = ParseTimeInstantOrLiteral(timeElement);
        return new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.LessThan,
            new QueryConstant(timeValue));
    }

    private static QueryExpression ParseAfterOperator(string fieldName, XElement timeElement)
    {
        // After: field value is after the time instant
        var timeValue = ParseTimeInstantOrLiteral(timeElement);
        return new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.GreaterThan,
            new QueryConstant(timeValue));
    }

    private static QueryExpression ParseTEqualsOperator(string fieldName, XElement timeElement)
    {
        // TEquals: field value equals the time instant
        var timeValue = ParseTimeInstantOrLiteral(timeElement);
        return new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.Equal,
            new QueryConstant(timeValue));
    }

    private static DateTime ParseTimeInstantOrLiteral(XElement timeElement)
    {
        string? timeValue = null;

        if (timeElement.Name.LocalName == "TimeInstant")
        {
            timeValue = timeElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "timePosition")?.Value?.Trim();
        }
        else if (timeElement.Name.LocalName == "Literal")
        {
            timeValue = timeElement.Value?.Trim();
        }

        if (string.IsNullOrEmpty(timeValue))
        {
            throw new InvalidOperationException("Could not extract temporal value from element.");
        }

        return DateTime.Parse(timeValue, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}
