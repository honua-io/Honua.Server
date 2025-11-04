// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.GitOps;

/// <summary>
/// Abstraction for Git repository operations
/// </summary>
public interface IGitRepository
{
    /// <summary>
    /// Get the current HEAD commit SHA
    /// </summary>
    Task<string> GetCurrentCommitAsync(string branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pull latest changes from remote
    /// </summary>
    Task PullAsync(string branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of changed files between two commits
    /// </summary>
    Task<List<string>> GetChangedFilesAsync(string fromCommit, string toCommit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get file content at specific commit
    /// </summary>
    Task<string> GetFileContentAsync(string path, string? commit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if repository is clean (no uncommitted changes)
    /// </summary>
    Task<bool> IsCleanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get commit information
    /// </summary>
    Task<GitCommitInfo> GetCommitInfoAsync(string commit, CancellationToken cancellationToken = default);
}

public class GitCommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<string> ChangedFiles { get; set; } = new();
}
