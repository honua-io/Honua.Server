// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Import.Validation;

/// <summary>
/// Result of a type coercion attempt.
/// </summary>
public readonly record struct CoercionResult
{
    public bool Success { get; init; }
    public object? Value { get; init; }
    public string? ErrorMessage { get; init; }

    public static CoercionResult Successful(object? value) => new() { Success = true, Value = value };
    public static CoercionResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Provides type coercion for compatible field types during data ingestion.
/// </summary>
public static class TypeCoercion
{
    /// <summary>
    /// Attempts to coerce a value to the target type specified by the storage type string.
    /// </summary>
    public static CoercionResult TryCoerce(object? value, string targetStorageType)
    {
        if (value is null)
        {
            return CoercionResult.Successful(null);
        }

        var normalizedType = targetStorageType.Trim().ToLowerInvariant();

        return normalizedType switch
        {
            "integer" or "bigint" => CoerceToInt64(value),
            "smallint" => CoerceToInt16(value),
            "float" => CoerceToFloat(value),
            "double" => CoerceToDouble(value),
            "datetime" or "timestamp" => CoerceToDateTime(value),
            "uuid" or "uniqueidentifier" => CoerceToGuid(value),
            "blob" or "binary" or "bytea" or "varbinary" => CoerceToByteArray(value),
            "text" or "string" or "varchar" or "nvarchar" or "longtext" => CoerceToString(value),
            "geometry" => CoerceToGeometry(value),
            "boolean" or "bool" => CoerceToBoolean(value),
            _ => CoerceToString(value) // Default: try to convert to string
        };
    }

    private static CoercionResult CoerceToInt64(object value)
    {
        return value switch
        {
            long l => CoercionResult.Successful(l),
            int i => CoercionResult.Successful((long)i),
            short s => CoercionResult.Successful((long)s),
            byte b => CoercionResult.Successful((long)b),
            float f when f == Math.Floor(f) && f >= long.MinValue && f <= long.MaxValue => CoercionResult.Successful((long)f),
            double d when d == Math.Floor(d) && d >= long.MinValue && d <= long.MaxValue => CoercionResult.Successful((long)d),
            decimal dec when dec == Math.Floor(dec) && dec >= long.MinValue && dec <= long.MaxValue => CoercionResult.Successful((long)dec),
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) => CoercionResult.Successful(result),
            bool b => CoercionResult.Successful(b ? 1L : 0L),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to integer")
        };
    }

    private static CoercionResult CoerceToInt16(object value)
    {
        return value switch
        {
            short s => CoercionResult.Successful(s),
            byte b => CoercionResult.Successful((short)b),
            int i when i >= short.MinValue && i <= short.MaxValue => CoercionResult.Successful((short)i),
            long l when l >= short.MinValue && l <= short.MaxValue => CoercionResult.Successful((short)l),
            float f when f == Math.Floor(f) && f >= short.MinValue && f <= short.MaxValue => CoercionResult.Successful((short)f),
            double d when d == Math.Floor(d) && d >= short.MinValue && d <= short.MaxValue => CoercionResult.Successful((short)d),
            string s when short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) => CoercionResult.Successful(result),
            bool b => CoercionResult.Successful((short)(b ? 1 : 0)),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to smallint")
        };
    }

    private static CoercionResult CoerceToFloat(object value)
    {
        return value switch
        {
            float f => CoercionResult.Successful(f),
            double d when d >= float.MinValue && d <= float.MaxValue => CoercionResult.Successful((float)d),
            int i => CoercionResult.Successful((float)i),
            long l => CoercionResult.Successful((float)l),
            short s => CoercionResult.Successful((float)s),
            byte b => CoercionResult.Successful((float)b),
            decimal dec => CoercionResult.Successful((float)dec),
            string s when float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result) => CoercionResult.Successful(result),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to float")
        };
    }

    private static CoercionResult CoerceToDouble(object value)
    {
        return value switch
        {
            double d => CoercionResult.Successful(d),
            float f => CoercionResult.Successful((double)f),
            int i => CoercionResult.Successful((double)i),
            long l => CoercionResult.Successful((double)l),
            short s => CoercionResult.Successful((double)s),
            byte b => CoercionResult.Successful((double)b),
            decimal dec => CoercionResult.Successful((double)dec),
            string s when double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result) => CoercionResult.Successful(result),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to double")
        };
    }

    private static CoercionResult CoerceToDateTime(object value)
    {
        return value switch
        {
            DateTime dt => CoercionResult.Successful(dt),
            DateTimeOffset dto => CoercionResult.Successful(dto),
            string s when TryParseDateTime(s, out var result) => CoercionResult.Successful(result),
            long unixSeconds when unixSeconds > 0 && unixSeconds < 253402300799 => CoercionResult.Successful(DateTimeOffset.FromUnixTimeSeconds(unixSeconds)),
            int unixSeconds when unixSeconds > 0 => CoercionResult.Successful(DateTimeOffset.FromUnixTimeSeconds(unixSeconds)),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to datetime")
        };
    }

    private static bool TryParseDateTime(string value, out DateTimeOffset result)
    {
        if (value.IsNullOrWhiteSpace())
        {
            result = default;
            return false;
        }

        // Try ISO 8601 formats first
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
        {
            return true;
        }

        // Try parsing as DateTime
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            result = new DateTimeOffset(dt, TimeSpan.Zero);
            return true;
        }

        result = default;
        return false;
    }

    private static CoercionResult CoerceToGuid(object value)
    {
        return value switch
        {
            Guid g => CoercionResult.Successful(g),
            string s when Guid.TryParse(s, out var result) => CoercionResult.Successful(result),
            byte[] bytes when bytes.Length == 16 => CoercionResult.Successful(new Guid(bytes)),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to UUID")
        };
    }

    private static CoercionResult CoerceToByteArray(object value)
    {
        return value switch
        {
            byte[] bytes => CoercionResult.Successful(bytes),
            string s when TryParseBase64(s, out var bytes) => CoercionResult.Successful(bytes),
            string s when TryParseHex(s, out var bytes) => CoercionResult.Successful(bytes),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to byte array")
        };
    }

    private static bool TryParseBase64(string value, out byte[]? result)
    {
        try
        {
            if (!value.IsNullOrWhiteSpace())
            {
                result = Convert.FromBase64String(value);
                return true;
            }
        }
        catch
        {
            // Not valid base64
        }

        result = null;
        return false;
    }

    private static bool TryParseHex(string value, out byte[]? result)
    {
        if (value.IsNullOrWhiteSpace() || value.Length % 2 != 0)
        {
            result = null;
            return false;
        }

        try
        {
            var bytes = new byte[value.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            }
            result = bytes;
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    private static CoercionResult CoerceToString(object value)
    {
        return value switch
        {
            string s => CoercionResult.Successful(s),
            DateTime dt => CoercionResult.Successful(dt.ToString("O", CultureInfo.InvariantCulture)),
            DateTimeOffset dto => CoercionResult.Successful(dto.ToString("O", CultureInfo.InvariantCulture)),
            Guid g => CoercionResult.Successful(g.ToString("D")),
            byte[] bytes => CoercionResult.Successful(Convert.ToBase64String(bytes)),
            _ => CoercionResult.Successful(Convert.ToString(value, CultureInfo.InvariantCulture))
        };
    }

    private static CoercionResult CoerceToGeometry(object value)
    {
        // Geometry is stored as GeoJSON text in most cases
        return value switch
        {
            string s => CoercionResult.Successful(s), // Assume it's valid GeoJSON
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to geometry")
        };
    }

    private static CoercionResult CoerceToBoolean(object value)
    {
        return value switch
        {
            bool b => CoercionResult.Successful(b),
            int i => CoercionResult.Successful(i != 0),
            long l => CoercionResult.Successful(l != 0),
            short s => CoercionResult.Successful(s != 0),
            byte b => CoercionResult.Successful(b != 0),
            string s when bool.TryParse(s, out var result) => CoercionResult.Successful(result),
            string s when s.Equals("1", StringComparison.Ordinal) => CoercionResult.Successful(true),
            string s when s.Equals("0", StringComparison.Ordinal) => CoercionResult.Successful(false),
            string s when s.Equals("yes", StringComparison.OrdinalIgnoreCase) => CoercionResult.Successful(true),
            string s when s.Equals("no", StringComparison.OrdinalIgnoreCase) => CoercionResult.Successful(false),
            string s when s.Equals("y", StringComparison.OrdinalIgnoreCase) => CoercionResult.Successful(true),
            string s when s.Equals("n", StringComparison.OrdinalIgnoreCase) => CoercionResult.Successful(false),
            _ => CoercionResult.Failed($"Cannot coerce value of type '{value.GetType().Name}' to boolean")
        };
    }
}
