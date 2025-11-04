// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Enterprise.GitOps;

/// <summary>
/// Git repository implementation using LibGit2Sharp
/// </summary>
public class LibGit2SharpRepository : IGitRepository
{
    private readonly string _repositoryPath;
    private readonly string? _username;
    private readonly string? _password;

    public LibGit2SharpRepository(string repositoryPath, string? username = null, string? password = null)
    {
        _repositoryPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
        _username = username;
        _password = password;

        if (!Repository.IsValid(_repositoryPath))
        {
            throw new ArgumentException($"Path is not a valid Git repository: {repositoryPath}", nameof(repositoryPath));
        }
    }

    public Task<string> GetCurrentCommitAsync(string branch, CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_repositoryPath);

        var branchObj = repo.Branches[branch] ?? repo.Branches[$"origin/{branch}"];
        if (branchObj == null)
        {
            throw new InvalidOperationException($"Branch '{branch}' not found");
        }

        return Task.FromResult(branchObj.Tip.Sha);
    }

    public Task PullAsync(string branch, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var repo = new Repository(_repositoryPath);

            var options = new PullOptions
            {
                FetchOptions = new FetchOptions
                {
                    CredentialsProvider = GetCredentialsHandler()
                }
            };

            var signature = new Signature(
                new Identity("Honua GitOps", "gitops@honua.io"),
                DateTimeOffset.Now);

            Commands.Pull(repo, signature, options);
        }, cancellationToken);
    }

    public Task<List<string>> GetChangedFilesAsync(string fromCommit, string toCommit, CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_repositoryPath);

        var fromCommitObj = repo.Lookup<Commit>(fromCommit);
        var toCommitObj = repo.Lookup<Commit>(toCommit);

        if (fromCommitObj == null || toCommitObj == null)
        {
            throw new InvalidOperationException($"Commit not found: {fromCommit} or {toCommit}");
        }

        var changes = repo.Diff.Compare<TreeChanges>(fromCommitObj.Tree, toCommitObj.Tree);

        var changedFiles = changes
            .Select(c => c.Path)
            .ToList();

        return Task.FromResult(changedFiles);
    }

    public Task<string> GetFileContentAsync(string path, string? commit = null, CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_repositoryPath);

        Commit commitObj;
        if (commit != null)
        {
            commitObj = repo.Lookup<Commit>(commit);
            if (commitObj == null)
            {
                throw new InvalidOperationException($"Commit not found: {commit}");
            }
        }
        else
        {
            commitObj = repo.Head.Tip;
        }

        var entry = commitObj[path];
        if (entry == null || entry.TargetType != TreeEntryTargetType.Blob)
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var blob = (Blob)entry.Target;
        return Task.FromResult(blob.GetContentText());
    }

    public Task<bool> IsCleanAsync(CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_repositoryPath);
        var status = repo.RetrieveStatus();
        return Task.FromResult(!status.IsDirty);
    }

    public Task<GitCommitInfo> GetCommitInfoAsync(string commit, CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_repositoryPath);

        var commitObj = repo.Lookup<Commit>(commit);
        if (commitObj == null)
        {
            throw new InvalidOperationException($"Commit not found: {commit}");
        }

        var changedFiles = new List<string>();
        if (commitObj.Parents.Any())
        {
            var parent = commitObj.Parents.First();
            var changes = repo.Diff.Compare<TreeChanges>(parent.Tree, commitObj.Tree);
            changedFiles = changes.Select(c => c.Path).ToList();
        }

        return Task.FromResult(new GitCommitInfo
        {
            Sha = commitObj.Sha,
            Author = commitObj.Author.Name,
            Message = commitObj.Message,
            Timestamp = commitObj.Author.When.UtcDateTime,
            ChangedFiles = changedFiles
        });
    }

    private LibGit2Sharp.Handlers.CredentialsHandler? GetCredentialsHandler()
    {
        if (_username.IsNullOrEmpty() || _password.IsNullOrEmpty())
        {
            return null;
        }

        return (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials
            {
                Username = _username,
                Password = _password
            };
    }
}
