using System;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Tests.Shared;

public static class TestChangeTokens
{
    public static IChangeToken Noop { get; } = new StaticChangeToken();

    private sealed class StaticChangeToken : IChangeToken
    {
        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
            => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
