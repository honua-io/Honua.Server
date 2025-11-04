using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authentication;

public sealed class SqliteAuthRepositoryTests
{
    [Fact]
    public async Task EnsureInitializedAsync_CreatesDatabaseFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "honua-auth-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var storePath = Path.Combine(tempDir, "auth.db");

        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                Provider = "sqlite",
                StorePath = storePath
            }
        };

        var repository = new SqliteAuthRepository(
            tempDir,
            new TestOptionsMonitor(options),
            NullLogger<SqliteAuthRepository>.Instance,
            metrics: null,
            dataAccessOptions: Options.Create(new DataAccessOptions()));

        await repository.EnsureInitializedAsync();

        File.Exists(storePath).Should().BeTrue();
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<HonuaAuthenticationOptions>
    {
        private HonuaAuthenticationOptions _current;

        public TestOptionsMonitor(HonuaAuthenticationOptions value)
        {
            _current = value;
        }

        public HonuaAuthenticationOptions CurrentValue => _current;

        public HonuaAuthenticationOptions Get(string? name) => _current;

        public IDisposable OnChange(Action<HonuaAuthenticationOptions, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
