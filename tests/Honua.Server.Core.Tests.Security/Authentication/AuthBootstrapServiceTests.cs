using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Tests.Security.Authentication;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class AuthBootstrapServiceTests
{
    [Fact]
    public async Task BootstrapAsync_LocalMode_CreatesAdministrator()
    {
        var repository = new FakeAuthRepository();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Bootstrap = new HonuaAuthenticationOptions.BootstrapOptions
            {
                AdminPassword = "Sup3rSecure!",
                AdminUsername = "root",
                AdminEmail = "root@example.com"
            }
        };

        var passwordHasher = new FakePasswordHasher();
        var service = new AuthBootstrapService(repository, passwordHasher, new TestOptionsMonitor(options), NullLogger<AuthBootstrapService>.Instance);

        var result = await service.BootstrapAsync();

        result.Status.Should().Be(AuthBootstrapStatus.Completed);
        result.GeneratedSecret.Should().BeNull();
        repository.EnsureInitializedCalls.Should().Be(1);
        repository.CreatedLocal.Should().BeTrue();
        repository.CompletedMode.Should().Be("Local");
    }

    [Fact]
    public async Task BootstrapAsync_WhenAlreadyCompleted_NoAdditionalChanges()
    {
        var repository = new FakeAuthRepository
        {
            BootstrapState = new BootstrapState(true, "Local")
        };
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Bootstrap = new HonuaAuthenticationOptions.BootstrapOptions
            {
                AdminPassword = "Sup3rSecure!"
            }
        };

        var passwordHasher = new FakePasswordHasher();
        var service = new AuthBootstrapService(repository, passwordHasher, new TestOptionsMonitor(options), NullLogger<AuthBootstrapService>.Instance);

        var result = await service.BootstrapAsync();

        result.Status.Should().Be(AuthBootstrapStatus.Completed);
        repository.CreatedLocal.Should().BeFalse();
        repository.MarkCompletedCalls.Should().Be(0);
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public int EnsureInitializedCalls { get; private set; }
        public bool CreatedLocal { get; private set; }
        public int MarkCompletedCalls { get; private set; }
        public BootstrapState BootstrapState { get; set; } = new(false, null);
        public string? CompletedMode { get; private set; }

        public ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitializedCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<BootstrapState> GetBootstrapStateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(BootstrapState);
        }

        public ValueTask MarkBootstrapCompletedAsync(string mode, CancellationToken cancellationToken = default)
        {
            MarkCompletedCalls++;
            CompletedMode = mode;
            return ValueTask.CompletedTask;
        }

        public ValueTask CreateLocalAdministratorAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, CancellationToken cancellationToken = default)
        {
            CreatedLocal = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask CreateOidcAdministratorAsync(string subject, string? username, string? email, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<AuthUserCredentials?> GetCredentialsByUsernameAsync(string username, CancellationToken cancellationToken = default) => ValueTask.FromResult<AuthUserCredentials?>(null);

        public ValueTask<AuthUserCredentials?> GetCredentialsByIdAsync(string userId, CancellationToken cancellationToken = default) => ValueTask.FromResult<AuthUserCredentials?>(null);

        public ValueTask UpdateLoginFailureAsync(string userId, int failedAttempts, DateTimeOffset failedAtUtc, bool lockUser, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask UpdateLoginSuccessAsync(string userId, DateTimeOffset loginAtUtc, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<string> CreateLocalUserAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask SetLocalUserPasswordAsync(string userId, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask AssignRolesAsync(string userId, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsAsync(string userId, int limit = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<AuditRecord>>(Array.Empty<AuditRecord>());
        }

        public ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsByActionAsync(string action, int limit = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<AuditRecord>>(Array.Empty<AuditRecord>());
        }

        public ValueTask<IReadOnlyList<AuditRecord>> GetRecentFailedAuthenticationsAsync(TimeSpan window, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<AuditRecord>>(Array.Empty<AuditRecord>());
        }

        public ValueTask<int> PurgeOldAuditRecordsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(0);
        }

        public ValueTask<AuthUser?> GetUserAsync(string username, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public PasswordHashResult HashPassword(string password)
        {
            var hash = Encoding.UTF8.GetBytes("hash-" + password);
            var salt = Encoding.UTF8.GetBytes("salt");
            return new PasswordHashResult(hash, salt, "FAKE", "");
        }

        public bool VerifyPassword(string password, byte[] hash, byte[] salt, string algorithm, string parameters)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<HonuaAuthenticationOptions>
    {
        private HonuaAuthenticationOptions _value;

        public TestOptionsMonitor(HonuaAuthenticationOptions value)
        {
            _value = value;
        }

        public HonuaAuthenticationOptions CurrentValue => _value;

        public HonuaAuthenticationOptions Get(string? name) => _value;

        public IDisposable OnChange(Action<HonuaAuthenticationOptions, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
