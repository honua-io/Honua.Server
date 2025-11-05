// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for audit log operations.
/// </summary>
public sealed class AuditLogApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuditLogApiClient> _logger;

    public AuditLogApiClient(IHttpClientFactory httpClientFactory, ILogger<AuditLogApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Queries audit log events with filtering and pagination.
    /// </summary>
    public async Task<AuditLogResult?> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/query", query, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AuditLogResult>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying audit log");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific audit event by ID.
    /// </summary>
    public async Task<AuditEvent?> GetByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/admin/audit/{eventId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuditEvent>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit event {EventId}", eventId);
            throw;
        }
    }

    /// <summary>
    /// Gets audit log statistics for a time period.
    /// </summary>
    public async Task<AuditLogStatistics?> GetStatisticsAsync(
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>();
            if (startTime.HasValue)
                queryParams.Add($"startTime={startTime.Value:O}");
            if (endTime.HasValue)
                queryParams.Add($"endTime={endTime.Value:O}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"/api/admin/audit/statistics{queryString}", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AuditLogStatistics>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit statistics");
            throw;
        }
    }

    /// <summary>
    /// Exports audit log to CSV format.
    /// </summary>
    public async Task<byte[]?> ExportToCsvAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/export/csv", query, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit log to CSV");
            throw;
        }
    }

    /// <summary>
    /// Exports audit log to JSON format.
    /// </summary>
    public async Task<byte[]?> ExportToJsonAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/export/json", query, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit log to JSON");
            throw;
        }
    }

    /// <summary>
    /// Archives old audit events.
    /// </summary>
    public async Task<long> ArchiveEventsAsync(int olderThanDays, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/admin/audit/archive?olderThanDays={olderThanDays}",
                null,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ArchiveResult>(cancellationToken: cancellationToken);
            return result?.ArchivedCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving audit events");
            throw;
        }
    }

    private sealed class ArchiveResult
    {
        public long ArchivedCount { get; set; }
    }
}
