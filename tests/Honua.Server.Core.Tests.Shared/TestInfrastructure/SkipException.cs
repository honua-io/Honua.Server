using System;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Custom exception to skip tests when required infrastructure is not available.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message)
    {
    }
}
