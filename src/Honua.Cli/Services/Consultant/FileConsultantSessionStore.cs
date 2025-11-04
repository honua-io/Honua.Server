// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// File-based implementation of consultant session storage.
/// Stores sessions in ~/.honua/consultant-sessions/
/// </summary>
public sealed class FileConsultantSessionStore : IConsultantSessionStore
{
    private readonly ILogger<FileConsultantSessionStore> _logger;
    private readonly string _sessionsDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileConsultantSessionStore(ILogger<FileConsultantSessionStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _sessionsDirectory = Path.Combine(homeDir, ".honua", "consultant-sessions");

        // Ensure directory exists
        Directory.CreateDirectory(_sessionsDirectory);
    }

    public async Task SaveSessionAsync(
        string sessionId,
        ConsultantPlan plan,
        ConsultantPlanningContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionData = new SessionData
            {
                SessionId = sessionId,
                Plan = plan,
                Context = context,
                CreatedAt = DateTime.UtcNow
            };

            var filePath = GetSessionFilePath(sessionId);
            var json = JsonSerializer.Serialize(sessionData, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Saved consultant session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save consultant session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<(ConsultantPlan Plan, ConsultantPlanningContext Context)?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetSessionFilePath(sessionId);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var sessionData = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);

            if (sessionData == null)
            {
                _logger.LogWarning("Failed to deserialize session {SessionId}", sessionId);
                return null;
            }

            _logger.LogInformation("Retrieved consultant session {SessionId}", sessionId);
            return (sessionData.Plan, sessionData.Context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve consultant session {SessionId}", sessionId);
            return null;
        }
    }

    public Task<List<string>> GetRecentSessionsAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(count)
                .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                .ToList();

            return Task.FromResult(sessionFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent sessions");
            return Task.FromResult(new List<string>());
        }
    }

    public Task DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetSessionFilePath(sessionId);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted consultant session {SessionId}", sessionId);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete consultant session {SessionId}", sessionId);
            throw;
        }
    }

    private string GetSessionFilePath(string sessionId)
    {
        // Sanitize session ID to prevent path traversal
        var safeSessionId = string.Concat(sessionId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        return Path.Combine(_sessionsDirectory, $"{safeSessionId}.json");
    }

    private sealed class SessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public ConsultantPlan Plan { get; set; } = null!;
        public ConsultantPlanningContext Context { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
