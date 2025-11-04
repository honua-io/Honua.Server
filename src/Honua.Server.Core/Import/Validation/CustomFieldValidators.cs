// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Import.Validation;

/// <summary>
/// Custom validators for common field formats.
/// </summary>
public static partial class CustomFieldValidators
{
    // RFC 5322 compliant email regex (simplified)
    [GeneratedRegex(@"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$", RegexOptions.Compiled | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();

    // RFC 3986 URL regex
    [GeneratedRegex(@"^https?://[^\s/$.?#].[^\s]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlRegex();

    // E.164 phone number format (+[country code][number])
    [GeneratedRegex(@"^\+?[1-9]\d{1,14}$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhoneE164Regex();

    // General phone number (more lenient)
    [GeneratedRegex(@"^[\d\s\-\(\)\+\.]+$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhoneGeneralRegex();

    // US Postal Code (5 digits or 5+4 format)
    [GeneratedRegex(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UsPostalCodeRegex();

    // IPv4 Address
    [GeneratedRegex(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IPv4Regex();

    // IPv6 Address (simplified)
    [GeneratedRegex(@"^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$", RegexOptions.Compiled | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IPv6Regex();

    /// <summary>
    /// Validates an email address according to RFC 5322 (simplified).
    /// </summary>
    public static bool IsValidEmail(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            return EmailRegex().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a URL according to RFC 3986.
    /// </summary>
    public static bool IsValidUrl(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            return UrlRegex().IsMatch(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a phone number in E.164 format.
    /// </summary>
    public static bool IsValidPhoneE164(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            return PhoneE164Regex().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a phone number (lenient format allowing various separators).
    /// </summary>
    public static bool IsValidPhone(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Remove common separators
        var normalized = value.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "");

        // Must have at least 7 digits
        if (normalized.Length < 7)
        {
            return false;
        }

        try
        {
            return PhoneGeneralRegex().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a US postal code (5 digits or 5+4 format).
    /// </summary>
    public static bool IsValidUsPostalCode(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            return UsPostalCodeRegex().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates an IPv4 address.
    /// </summary>
    public static bool IsValidIPv4(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            return IPv4Regex().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates an IPv6 address.
    /// </summary>
    public static bool IsValidIPv6(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            return IPv6Regex().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a coordinate latitude value (-90 to 90).
    /// </summary>
    public static bool IsValidLatitude(double value)
    {
        return value >= -90.0 && value <= 90.0;
    }

    /// <summary>
    /// Validates a coordinate longitude value (-180 to 180).
    /// </summary>
    public static bool IsValidLongitude(double value)
    {
        return value >= -180.0 && value <= 180.0;
    }

    /// <summary>
    /// Validates a value against a custom regex pattern.
    /// PERFORMANCE: Uses cached compiled patterns to avoid repeated compilation overhead.
    /// </summary>
    public static bool MatchesPattern(string? value, string pattern)
    {
        if (value.IsNullOrWhiteSpace() || pattern.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            var regex = RegexCache.GetOrAdd(pattern, RegexOptions.Compiled, timeoutMilliseconds: 1000);
            return regex.IsMatch(value);
        }
        catch (Exception ex) when (ex is RegexMatchTimeoutException or ArgumentException)
        {
            return false;
        }
    }
}
