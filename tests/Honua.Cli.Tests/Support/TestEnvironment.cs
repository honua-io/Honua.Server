using System;
using System.IO;
using Honua.Cli.Services;

namespace Honua.Cli.Tests.Support;

public sealed class TestEnvironment : IHonuaCliEnvironment
{
    public TestEnvironment(string root)
    {
        ConfigRoot = root ?? throw new ArgumentNullException(nameof(root));
        SnapshotsRoot = Path.Combine(ConfigRoot, "snapshots");
        LogsRoot = Path.Combine(ConfigRoot, "logs");
    }

    public string ConfigRoot { get; }
    public string SnapshotsRoot { get; }
    public string LogsRoot { get; }

    public void EnsureInitialized()
    {
        Directory.CreateDirectory(ConfigRoot);
        Directory.CreateDirectory(SnapshotsRoot);
        Directory.CreateDirectory(LogsRoot);
    }

    public string ResolveWorkspacePath(string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return Directory.GetCurrentDirectory();
        }

        var path = Path.GetFullPath(requestedPath);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        return path;
    }
}
