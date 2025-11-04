// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services;

public interface IHonuaCliEnvironment
{
    string ConfigRoot { get; }
    string SnapshotsRoot { get; }
    string LogsRoot { get; }

    string ResolveWorkspacePath(string? requestedPath);
    void EnsureInitialized();
}

public sealed class HonuaCliEnvironment : IHonuaCliEnvironment
{
    private const string DefaultFolderName = "Honua";
    private readonly string _configRoot;

    public HonuaCliEnvironment()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("HONUA_HOME");
        if (overrideRoot.HasValue())
        {
            _configRoot = Path.GetFullPath(overrideRoot);
        }
        else
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (basePath.IsNullOrWhiteSpace())
            {
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            _configRoot = Path.Combine(basePath, DefaultFolderName);
        }
    }

    public string ConfigRoot => _configRoot;
    public string SnapshotsRoot => Path.Combine(_configRoot, "snapshots");
    public string LogsRoot => Path.Combine(_configRoot, "logs");

    public void EnsureInitialized()
    {
        EnsureDirectorySecure(_configRoot);
        EnsureDirectorySecure(SnapshotsRoot);
        EnsureDirectorySecure(LogsRoot);
    }

    public string ResolveWorkspacePath(string? requestedPath)
    {
        if (requestedPath.IsNullOrWhiteSpace())
        {
            return Directory.GetCurrentDirectory();
        }

        var fullPath = Path.GetFullPath(requestedPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Workspace path '{fullPath}' does not exist.");
        }

        return fullPath;
    }

    private static void EnsureDirectorySecure(string path)
    {
        FilePermissionHelper.EnsureDirectorySecure(path);
    }
}
