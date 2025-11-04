using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Honua.Server.Core.Tests.Security.Authentication;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class LocalTokenServiceTests
{
    [Fact]
    public async Task CreateTokenAsync_ShouldEmbedSubjectAndRoles()
    {
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"honua-tests-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempRoot);

        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                SessionLifetime = TimeSpan.FromMinutes(5),
                SigningKeyPath = System.IO.Path.Combine(tempRoot, "signing.key")
            }
        };

        var monitor = new TestOptionsMonitor(options);
        var signingKeyProvider = new LocalSigningKeyProvider(monitor, NullLogger<LocalSigningKeyProvider>.Instance);
        var service = new LocalTokenService(monitor, signingKeyProvider);

        try
        {
            var tokenString = await service.CreateTokenAsync("admin", new[] { "administrator", "viewer" });

            var handler = new JwtSecurityTokenHandler();
            handler.CanReadToken(tokenString).Should().BeTrue();
            var jwt = handler.ReadJwtToken(tokenString);
            jwt.Subject.Should().Be("admin");
            jwt.Claims.Should().Contain(c => c.Type == LocalAuthenticationDefaults.RoleClaimType && c.Value == "administrator");
            jwt.Claims.Should().Contain(c => c.Type == LocalAuthenticationDefaults.RoleClaimType && c.Value == "viewer");

            var key = await System.IO.File.ReadAllBytesAsync(options.Local.SigningKeyPath!);
            key.Should().NotBeNull();
            key.Length.Should().Be(32);
        }
        finally
        {
            try
            {
                if (options.Local.SigningKeyPath is { Length: > 0 } path && System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            finally
            {
                if (System.IO.Directory.Exists(tempRoot))
                {
                    System.IO.Directory.Delete(tempRoot, recursive: true);
                }
            }
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
