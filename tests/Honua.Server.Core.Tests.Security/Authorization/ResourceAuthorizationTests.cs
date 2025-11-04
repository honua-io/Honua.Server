using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authorization;

[Trait("Category", "Unit")]
public sealed class ResourceAuthorizationTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly IMeterFactory _meterFactory;

    public ResourceAuthorizationTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        _meterFactory = new TestMeterFactory();
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
    }

    [Fact]
    public void ResourcePolicy_MatchesResource_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            ResourcePattern = "weather:temperature"
        };

        // Act
        var result = policy.MatchesResource("weather:temperature");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ResourcePolicy_MatchesResource_WildcardMatch_ReturnsTrue()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            ResourcePattern = "weather:*"
        };

        // Act
        var result = policy.MatchesResource("weather:temperature");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ResourcePolicy_MatchesResource_NoMatch_ReturnsFalse()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            ResourcePattern = "weather:*"
        };

        // Act
        var result = policy.MatchesResource("traffic:incidents");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ResourcePolicy_AllowsOperation_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            AllowedOperations = new List<string> { "read", "write" }
        };

        // Act
        var result = policy.AllowsOperation("read");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ResourcePolicy_AllowsOperation_WildcardMatch_ReturnsTrue()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            AllowedOperations = new List<string> { "*" }
        };

        // Act
        var result = policy.AllowsOperation("delete");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ResourcePolicy_AllowsOperation_NoMatch_ReturnsFalse()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            AllowedOperations = new List<string> { "read" }
        };

        // Act
        var result = policy.AllowsOperation("write");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ResourcePolicy_AppliesTo_UserHasRole_ReturnsTrue()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            Roles = new List<string> { "administrator", "datapublisher" }
        };
        var userRoles = new[] { "datapublisher" };

        // Act
        var result = policy.AppliesTo(userRoles);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ResourcePolicy_AppliesTo_UserDoesNotHaveRole_ReturnsFalse()
    {
        // Arrange
        var policy = new ResourcePolicy
        {
            Roles = new List<string> { "administrator" }
        };
        var userRoles = new[] { "viewer" };

        // Act
        var result = policy.AppliesTo(userRoles);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task LayerAuthorizationHandler_AuthorizeAsync_WithMatchingPolicy_Succeeds()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>
            {
                new()
                {
                    Id = "test-policy",
                    ResourceType = "layer",
                    ResourcePattern = "weather:*",
                    AllowedOperations = new List<string> { "read" },
                    Roles = new List<string> { "viewer" },
                    Enabled = true,
                    Priority = 10
                }
            }
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);
        var handler = new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result = await handler.AuthorizeAsync(user, "layer", "weather:temperature", "read");

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task LayerAuthorizationHandler_AuthorizeAsync_NoMatchingPolicy_Fails()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);
        var handler = new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result = await handler.AuthorizeAsync(user, "layer", "weather:temperature", "read");

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LayerAuthorizationHandler_AuthorizeAsync_AdministratorRole_AlwaysSucceeds()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);
        var handler = new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options);

        var user = CreateUser("admin-user", "administrator");

        // Act
        var result = await handler.AuthorizeAsync(user, "layer", "weather:temperature", "write");

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task LayerAuthorizationHandler_AuthorizeAsync_AuthorizationDisabled_AlwaysSucceeds()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = false
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);
        var handler = new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result = await handler.AuthorizeAsync(user, "layer", "weather:temperature", "read");

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CollectionAuthorizationHandler_AuthorizeAsync_WithMatchingPolicy_Succeeds()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>
            {
                new()
                {
                    Id = "test-policy",
                    ResourceType = "collection",
                    ResourcePattern = "*",
                    AllowedOperations = new List<string> { "read" },
                    Roles = new List<string> { "viewer" },
                    Enabled = true,
                    Priority = 10
                }
            }
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);
        var handler = new CollectionAuthorizationHandler(cache, metrics, NullLogger<CollectionAuthorizationHandler>.Instance, options);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result = await handler.AuthorizeAsync(user, "collection", "my-collection", "read");

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ResourceAuthorizationCache_CachesResults()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            CacheDurationSeconds = 300,
            MaxCacheSize = 100
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);

        var result = ResourceAuthorizationResult.Success();
        var cacheKey = ResourceAuthorizationCache.BuildCacheKey("user1", "layer", "test-layer", "read");

        // Act
        cache.Set(cacheKey, result);
        var retrieved = cache.TryGet(cacheKey, out var cachedResult);

        // Assert
        Assert.True(retrieved);
        Assert.NotNull(cachedResult);
        Assert.True(cachedResult.Succeeded);
        Assert.True(cachedResult.FromCache);
    }

    [Fact]
    public void ResourceAuthorizationCache_InvalidatesResource()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            CacheDurationSeconds = 300
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);

        var result = ResourceAuthorizationResult.Success();
        var cacheKey = ResourceAuthorizationCache.BuildCacheKey("user1", "layer", "test-layer", "read");

        cache.Set(cacheKey, result);

        // Act
        cache.InvalidateResource("layer", "test-layer");
        var retrieved = cache.TryGet(cacheKey, out _);

        // Assert
        Assert.False(retrieved);
    }

    [Fact]
    public async Task ResourceAuthorizationService_AuthorizeAsync_DelegatesToCorrectHandler()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>
            {
                new()
                {
                    Id = "test-policy",
                    ResourceType = "layer",
                    ResourcePattern = "*",
                    AllowedOperations = new List<string> { "read" },
                    Roles = new List<string> { "viewer" },
                    Enabled = true
                }
            }
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);

        var handlers = new List<IResourceAuthorizationHandler>
        {
            new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options),
            new CollectionAuthorizationHandler(cache, metrics, NullLogger<CollectionAuthorizationHandler>.Instance, options)
        };

        var service = new ResourceAuthorizationService(handlers, cache, NullLogger<ResourceAuthorizationService>.Instance);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result = await service.AuthorizeAsync(user, "layer", "test-layer", "read");

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ResourceAuthorizationService_AuthorizeAsync_UnknownResourceType_Fails()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions { Enabled = true });
        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);

        var handlers = new List<IResourceAuthorizationHandler>
        {
            new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options)
        };

        var service = new ResourceAuthorizationService(handlers, cache, NullLogger<ResourceAuthorizationService>.Instance);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result = await service.AuthorizeAsync(user, "unknown-type", "test-resource", "read");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("No authorization handler found", result.FailureReason);
    }

    [Fact]
    public async Task LayerAuthorizationHandler_AuthorizeAsync_PolicyPriority_HigherPriorityWins()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>
            {
                new()
                {
                    Id = "low-priority",
                    ResourceType = "layer",
                    ResourcePattern = "*",
                    AllowedOperations = new List<string> { "read" },
                    Roles = new List<string> { "viewer" },
                    Enabled = true,
                    Priority = 10
                },
                new()
                {
                    Id = "high-priority",
                    ResourceType = "layer",
                    ResourcePattern = "weather:*",
                    AllowedOperations = new List<string> { "read" },
                    Roles = new List<string> { "viewer" },
                    Enabled = true,
                    Priority = 100
                }
            }
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);
        var handler = new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result = await handler.AuthorizeAsync(user, "layer", "weather:temperature", "read");

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task LayerAuthorizationHandler_AuthorizeAsync_UsesCache_SecondCallFromCache()
    {
        // Arrange
        var options = CreateOptions(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>
            {
                new()
                {
                    Id = "test-policy",
                    ResourceType = "layer",
                    ResourcePattern = "*",
                    AllowedOperations = new List<string> { "read" },
                    Roles = new List<string> { "viewer" },
                    Enabled = true
                }
            }
        });

        var cache = new ResourceAuthorizationCache(_memoryCache, NullLogger<ResourceAuthorizationCache>.Instance, options);
        var metrics = new ResourceAuthorizationMetrics(_meterFactory);
        var handler = new LayerAuthorizationHandler(cache, metrics, NullLogger<LayerAuthorizationHandler>.Instance, options);

        var user = CreateUser("test-user", "viewer");

        // Act
        var result1 = await handler.AuthorizeAsync(user, "layer", "test-layer", "read");
        var result2 = await handler.AuthorizeAsync(user, "layer", "test-layer", "read");

        // Assert
        Assert.True(result1.Succeeded);
        Assert.False(result1.FromCache);
        Assert.True(result2.Succeeded);
        Assert.True(result2.FromCache);
    }

    private static ClaimsPrincipal CreateUser(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, "TestAuthenticationType");
        return new ClaimsPrincipal(identity);
    }

    private static IOptionsMonitor<ResourceAuthorizationOptions> CreateOptions(ResourceAuthorizationOptions options)
    {
        var mock = new TestOptionsMonitor<ResourceAuthorizationOptions>(options);
        return mock;
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public TestOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new Meter(options.Name);

        public void Dispose() { }
    }
}
