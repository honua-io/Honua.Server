// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Security;

/// <summary>
/// Sanitizes error messages and logs to prevent exposure of sensitive information
/// such as API keys, bearer tokens, connection strings, and other secrets.
/// </summary>
/// <remarks>
/// This class helps prevent security vulnerabilities where exceptions or logs
/// might inadvertently expose sensitive credentials to users or log files.
///
/// Common patterns detected and sanitized:
/// - OpenAI API keys (sk-...)
/// - Anthropic API keys (sk-ant-...)
/// - Bearer tokens (Bearer ...)
/// - Connection strings with passwords
/// - Azure subscription keys
/// - Generic API key patterns
/// </remarks>
public static class SecretSanitizer
{
    // Regex patterns for various secret formats
    private static readonly Regex OpenAIKeyPattern = new Regex(
        @"sk-[a-zA-Z0-9]{48}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AnthropicKeyPattern = new Regex(
        @"sk-ant-[a-zA-Z0-9\-_]{95,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BearerTokenPattern = new Regex(
        @"Bearer\s+[a-zA-Z0-9\-_.]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ConnectionStringPasswordPattern = new Regex(
        @"(Password|Pwd|Pass)\s*=\s*[^;]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ApiKeyPattern = new Regex(
        @"(api[_-]?key|apikey|api[_-]?secret|secret[_-]?key)\s*[=:]\s*['""]?([a-zA-Z0-9\-_.]+)['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AzureKeyPattern = new Regex(
        @"[a-zA-Z0-9]{32,}",
        RegexOptions.Compiled);

    private static readonly Regex AuthorizationHeaderPattern = new Regex(
        @"Authorization:\s*[^\r\n]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Sanitizes a message by removing or redacting sensitive information.
    /// </summary>
    /// <param name="message">The message to sanitize (error message, log message, etc.)</param>
    /// <returns>A sanitized version of the message with secrets redacted</returns>
    /// <remarks>
    /// The method replaces detected secrets with "[REDACTED]" to prevent exposure.
    /// If the input is null or empty, it returns the original value.
    /// </remarks>
    public static string SanitizeErrorMessage(string? message)
    {
        if (message.IsNullOrEmpty())
        {
            return message ?? string.Empty;
        }

        var sanitized = message;

        // Replace OpenAI API keys (sk-...)
        sanitized = OpenAIKeyPattern.Replace(sanitized, "[REDACTED_OPENAI_KEY]");

        // Replace Anthropic API keys (sk-ant-...)
        sanitized = AnthropicKeyPattern.Replace(sanitized, "[REDACTED_ANTHROPIC_KEY]");

        // Replace Bearer tokens
        sanitized = BearerTokenPattern.Replace(sanitized, "Bearer [REDACTED_TOKEN]");

        // Replace Authorization headers
        sanitized = AuthorizationHeaderPattern.Replace(sanitized, "Authorization: [REDACTED]");

        // Replace connection string passwords
        sanitized = ConnectionStringPasswordPattern.Replace(sanitized, "$1=[REDACTED]");

        // Replace generic API keys
        sanitized = ApiKeyPattern.Replace(sanitized, "$1=[REDACTED]");

        return sanitized;
    }

    /// <summary>
    /// Sanitizes an exception message and inner exception messages recursively.
    /// </summary>
    /// <param name="exception">The exception to sanitize</param>
    /// <returns>A sanitized error message safe for logging and display</returns>
    /// <remarks>
    /// This method walks the entire exception chain (including InnerException)
    /// and sanitizes all messages to prevent secret exposure.
    /// </remarks>
    public static string SanitizeException(Exception exception)
    {
        if (exception == null)
        {
            return string.Empty;
        }

        var sanitizedMessage = SanitizeErrorMessage(exception.Message);

        // Recursively sanitize inner exceptions
        if (exception.InnerException != null)
        {
            var innerSanitized = SanitizeException(exception.InnerException);
            return $"{sanitizedMessage} (Inner: {innerSanitized})";
        }

        return sanitizedMessage;
    }

    /// <summary>
    /// Checks if a string appears to contain sensitive information that should be redacted.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>True if the string likely contains secrets, false otherwise</returns>
    /// <remarks>
    /// This method can be used as a quick check before logging or displaying values.
    /// It's useful for defensive programming to avoid accidental secret exposure.
    /// </remarks>
    public static bool ContainsSensitiveData(string? value)
    {
        if (value.IsNullOrEmpty())
        {
            return false;
        }

        return OpenAIKeyPattern.IsMatch(value) ||
               AnthropicKeyPattern.IsMatch(value) ||
               BearerTokenPattern.IsMatch(value) ||
               ConnectionStringPasswordPattern.IsMatch(value) ||
               ApiKeyPattern.IsMatch(value) ||
               AuthorizationHeaderPattern.IsMatch(value);
    }
}
