// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.RegularExpressions;

namespace Honua.Cli.Services.Consultant;

internal static class SessionLogSanitizer
{
    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var sanitized = SensitiveAssignmentPattern.Replace(value, match =>
        {
            var key = match.Groups[1].Value;
            return $"{key}=<redacted>";
        });

        sanitized = UriCredentialPattern.Replace(sanitized, match =>
        {
            var prefix = match.Groups[1].Value;
            var suffix = match.Groups[3].Value;
            return string.Concat(prefix, "***", suffix);
        });

        sanitized = BearerPattern.Replace(sanitized, match => match.Groups[1].Value + "<redacted>");

        return sanitized;
    }

    private static readonly Regex SensitiveAssignmentPattern = new(
        "(?i)\\b(password|secret|token|apikey|api_key|accesskey|key|pwd)\\b\\s*[:=]\\s*([^\\s,]+)",
        RegexOptions.Compiled);

    private static readonly Regex UriCredentialPattern = new(
        "(://[^\\s:@/]+:)([^@/\\s]+)(@)",
        RegexOptions.Compiled);

    private static readonly Regex BearerPattern = new(
        "(Bearer\\s+)[A-Za-z0-9\\-\\._~\\+/]+=*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
}
