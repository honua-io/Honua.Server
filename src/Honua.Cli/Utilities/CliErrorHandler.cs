// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Honua.Cli.Utilities;

/// <summary>
/// Provides centralized error handling for CLI commands with consistent user-facing messages
/// and detailed logging for troubleshooting.
/// </summary>
/// <remarks>
/// This utility implements the CLI error sanitization pattern from the Unified Error Handling Architecture.
/// It ensures that:
/// <list type="bullet">
/// <item>Raw exception messages are never shown to users</item>
/// <item>ProblemDetails responses from the server are parsed and displayed in a user-friendly format</item>
/// <item>Full exception details are logged for troubleshooting</item>
/// <item>Consistent exit codes are returned (0 = success, 1 = error)</item>
/// </list>
/// </remarks>
public static class CliErrorHandler
{
    /// <summary>
    /// Executes a CLI operation with comprehensive error handling.
    /// </summary>
    /// <param name="operation">The async operation to execute. Should return 0 on success, 1 on error.</param>
    /// <param name="logger">Logger instance for recording detailed error information.</param>
    /// <param name="operationName">Human-readable name of the operation being performed (e.g., "data-ingestion").</param>
    /// <returns>Exit code: 0 for success, 1 for error.</returns>
    /// <remarks>
    /// This method wraps CLI command execution to provide:
    /// <list type="bullet">
    /// <item>Automatic parsing of ProblemDetails from HTTP responses</item>
    /// <item>User-friendly error messages via Spectre.Console</item>
    /// <item>Detailed logging of exceptions for troubleshooting</item>
    /// <item>Consistent error handling across all CLI commands</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public override async Task&lt;int&gt; ExecuteAsync(CommandContext context, Settings settings)
    /// {
    ///     return await CliErrorHandler.ExecuteWithErrorHandlingAsync(
    ///         async () =>
    ///         {
    ///             var result = await _client.IngestDataAsync(settings.FilePath);
    ///             AnsiConsole.MarkupLine($"[green]Success:[/] Job {result.JobId} created");
    ///             return 0;
    ///         },
    ///         _logger,
    ///         "data-ingestion");
    /// }
    /// </code>
    /// </example>
    public static async Task<int> ExecuteWithErrorHandlingAsync(
        Func<Task<int>> operation,
        ILogger logger,
        string operationName)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operationName));

        try
        {
            return await operation();
        }
        catch (HttpRequestException ex)
        {
            // Try to parse ProblemDetails from the HTTP response
            if (TryParseProblemDetails(ex.Message, out var problemDetails))
            {
                DisplayProblemDetails(problemDetails);

                // Log with structured data for troubleshooting
                logger.LogWarning(ex,
                    "HTTP request failed for {Operation}. Status: {Status}, Title: {Title}",
                    operationName,
                    problemDetails.Status,
                    problemDetails.Title);
            }
            else
            {
                // Generic network error message
                AnsiConsole.MarkupLine("[red]Error:[/] Unable to connect to the server");
                AnsiConsole.MarkupLine("[yellow]Tip:[/] Verify the server is running and the --host URL is correct");

                logger.LogDebug(ex, "HTTP request failed for {Operation}", operationName);
            }

            return 1;
        }
        catch (OperationCanceledException)
        {
            // Don't treat cancellation as an error - user requested it
            AnsiConsole.MarkupLine("[yellow]Operation cancelled by user[/]");
            logger.LogInformation("Operation {Operation} was cancelled by user", operationName);
            return 1;
        }
        catch (Exception ex)
        {
            // Catch-all for unexpected errors
            AnsiConsole.MarkupLine("[red]Error:[/] An unexpected error occurred");
            AnsiConsole.MarkupLine("[yellow]Tip:[/] Check the logs for more details");

            logger.LogError(ex, "Unexpected error in {Operation}", operationName);
            return 1;
        }
    }

    /// <summary>
    /// Displays ProblemDetails information to the user in a formatted, user-friendly manner.
    /// </summary>
    /// <param name="problemDetails">The parsed ProblemDetails object to display.</param>
    private static void DisplayProblemDetails(ProblemDetailsDto problemDetails)
    {
        // Display the main error title
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(problemDetails.Title ?? "An error occurred")}");

        // Display additional details if available
        if (!string.IsNullOrWhiteSpace(problemDetails.Detail))
        {
            AnsiConsole.MarkupLine($"[yellow]Details:[/] {Markup.Escape(problemDetails.Detail)}");
        }

        // Display validation errors if present
        if (problemDetails.Errors?.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Validation Errors:[/]");
            foreach (var (field, errors) in problemDetails.Errors)
            {
                foreach (var error in errors)
                {
                    AnsiConsole.MarkupLine($"  • {Markup.Escape(field)}: {Markup.Escape(error)}");
                }
            }
        }

        // Display request ID if available (useful for support)
        if (!string.IsNullOrWhiteSpace(problemDetails.Instance))
        {
            AnsiConsole.MarkupLine($"[dim]Request ID: {Markup.Escape(problemDetails.Instance)}[/]");
        }
    }

    /// <summary>
    /// Attempts to parse ProblemDetails JSON from an exception message.
    /// </summary>
    /// <param name="message">The exception message that may contain JSON.</param>
    /// <param name="problemDetails">The parsed ProblemDetails if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    /// <remarks>
    /// This method extracts JSON from HttpRequestException messages which often include
    /// the response body. It uses a regex pattern to find JSON-like content and attempts
    /// to deserialize it as ProblemDetails.
    /// </remarks>
    private static bool TryParseProblemDetails(string message, out ProblemDetailsDto problemDetails)
    {
        problemDetails = null!;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        try
        {
            // Try to extract JSON from exception message
            // HttpRequestException often includes the response body in the message
            var jsonMatch = Regex.Match(message, @"\{.*\}", RegexOptions.Singleline);
            if (!jsonMatch.Success)
                return false;

            var options = new JsonSerializerOptions(JsonSerializerOptionsRegistry.DevTooling)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            problemDetails = JsonSerializer.Deserialize<ProblemDetailsDto>(jsonMatch.Value, options);
            return problemDetails != null;
        }
        catch (JsonException)
        {
            // Not valid JSON or doesn't match ProblemDetails schema
            return false;
        }
        catch (Exception)
        {
            // Any other parsing error
            return false;
        }
    }
}

/// <summary>
/// Data transfer object for parsing ProblemDetails responses from the server.
/// </summary>
/// <remarks>
/// This DTO matches the RFC 7807 Problem Details for HTTP APIs standard used by ASP.NET Core.
/// See: https://tools.ietf.org/html/rfc7807
/// </remarks>
public class ProblemDetailsDto
{
    /// <summary>
    /// A URI reference that identifies the problem type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public int? Status { get; set; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// A URI reference that identifies the specific occurrence of the problem.
    /// Often used as a request/trace ID for correlation.
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    /// <summary>
    /// Validation errors keyed by field name (for ValidationProblemDetails responses).
    /// </summary>
    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; set; }
}
