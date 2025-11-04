// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.GitOps;

/// <summary>
/// Watches a Git repository for changes and triggers reconciliation
/// Similar to ArgoCD's application controller
/// </summary>
public class GitWatcher : BackgroundService
{
    private readonly IGitRepository _repository;
    private readonly IReconciler _reconciler;
    private readonly GitWatcherOptions _options;
    private readonly ILogger<GitWatcher> _logger;
    private string? _lastKnownCommit;

    public GitWatcher(
        IGitRepository repository,
        IReconciler reconciler,
        GitWatcherOptions options,
        ILogger<GitWatcher> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "GitWatcher started. Watching branch '{Branch}' every {Interval} seconds",
            _options.Branch,
            _options.PollIntervalSeconds);

        // Get initial commit
        try
        {
            _lastKnownCommit = await _repository.GetCurrentCommitAsync(_options.Branch, stoppingToken);
            _logger.LogInformation("Initial commit: {Commit}", _lastKnownCommit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get initial commit");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollForChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for changes");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("GitWatcher stopped");
    }

    private async Task PollForChangesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Polling for changes on branch '{Branch}'", _options.Branch);

        // Pull latest changes
        await _repository.PullAsync(_options.Branch, cancellationToken);

        // Get current commit
        var currentCommit = await _repository.GetCurrentCommitAsync(_options.Branch, cancellationToken);

        // Check if commit changed
        if (_lastKnownCommit != currentCommit)
        {
            _logger.LogInformation(
                "Detected new commit: {OldCommit} -> {NewCommit}",
                _lastKnownCommit ?? "none",
                currentCommit);

            // Get commit info
            var commitInfo = await _repository.GetCommitInfoAsync(currentCommit, cancellationToken);

            _logger.LogInformation(
                "Commit by {Author}: {Message}",
                commitInfo.Author,
                commitInfo.Message.Split('\n')[0]);

            // Get changed files if we have a previous commit
            if (_lastKnownCommit != null)
            {
                var changedFiles = await _repository.GetChangedFilesAsync(
                    _lastKnownCommit,
                    currentCommit,
                    cancellationToken);

                _logger.LogInformation("Changed files: {Count}", changedFiles.Count);
                foreach (var file in changedFiles)
                {
                    _logger.LogDebug("  - {File}", file);
                }

                // Filter changed files by environment path
                var relevantFiles = changedFiles.FindAll(f =>
                    f.StartsWith($"environments/{_options.Environment}/") ||
                    f.StartsWith($"environments/common/"));

                if (relevantFiles.Count > 0)
                {
                    _logger.LogInformation(
                        "Found {Count} relevant files for environment '{Environment}'",
                        relevantFiles.Count,
                        _options.Environment);

                    // Trigger reconciliation
                    await _reconciler.ReconcileAsync(
                        _options.Environment,
                        currentCommit,
                        "GitWatcher",
                        cancellationToken);
                }
                else
                {
                    _logger.LogInformation(
                        "No relevant files changed for environment '{Environment}'",
                        _options.Environment);
                }
            }
            else
            {
                // First time seeing a commit, do full reconciliation
                _logger.LogInformation("First commit detected, performing full reconciliation");

                await _reconciler.ReconcileAsync(
                    _options.Environment,
                    currentCommit,
                    "GitWatcher",
                    cancellationToken);
            }

            _lastKnownCommit = currentCommit;
        }
        else
        {
            _logger.LogDebug("No new commits detected");
        }
    }
}

/// <summary>
/// Configuration options for GitWatcher
/// </summary>
public class GitWatcherOptions
{
    /// <summary>
    /// Branch to watch (e.g., "main", "production")
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Environment to reconcile (e.g., "production", "staging")
    /// </summary>
    public string Environment { get; set; } = "production";

    /// <summary>
    /// How often to poll for changes (in seconds)
    /// Default: 30 seconds
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 30;
}
