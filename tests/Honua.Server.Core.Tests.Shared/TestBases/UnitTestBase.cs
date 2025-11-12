// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Shared.TestBases;

/// <summary>
/// Base class for unit tests with common mocking infrastructure.
/// </summary>
public abstract class UnitTestBase : IDisposable
{
    protected IMemoryCache MemoryCache { get; }
    protected Mock<ILoggerFactory> MockLoggerFactory { get; }

    protected UnitTestBase()
    {
        MemoryCache = new MemoryCache(new MemoryCacheOptions());
        MockLoggerFactory = new Mock<ILoggerFactory>();
    }

    protected Mock<ILogger<T>> CreateMockLogger<T>()
    {
        var mockLogger = new Mock<ILogger<T>>();
        MockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        return mockLogger;
    }

    public virtual void Dispose()
    {
        MemoryCache?.Dispose();
        GC.SuppressFinalize(this);
    }
}
