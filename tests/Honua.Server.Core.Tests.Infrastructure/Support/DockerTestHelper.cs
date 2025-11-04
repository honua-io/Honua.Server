using System;
using System.Diagnostics;

namespace Honua.Server.Core.Tests.Infrastructure.Support;

internal static class DockerTestHelper
{
    private static readonly Lazy<bool> DockerAvailable = new(CheckDockerAvailability);

    public static bool IsDockerAvailable => DockerAvailable.Value;

    private static bool CheckDockerAvailability()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format '{{json .}}'",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
