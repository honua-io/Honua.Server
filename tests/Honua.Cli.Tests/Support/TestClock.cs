using System;
using Honua.Cli.Services;

namespace Honua.Cli.Tests.Support;

public sealed class TestClock : ISystemClock
{
    public TestClock(DateTimeOffset now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; set; }
}
