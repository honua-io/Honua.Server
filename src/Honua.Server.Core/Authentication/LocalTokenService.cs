// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

public sealed class LocalTokenService : ILocalTokenService
{
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly ILocalSigningKeyProvider _signingKeyProvider;

    public LocalTokenService(
        IOptionsMonitor<HonuaAuthenticationOptions> options,
        ILocalSigningKeyProvider signingKeyProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _signingKeyProvider = signingKeyProvider ?? throw new ArgumentNullException(nameof(signingKeyProvider));
    }

    public async Task<string> CreateTokenAsync(
        string subject,
        IReadOnlyCollection<string> roles,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(subject);
        Guard.NotNull(roles);

        var key = await _signingKeyProvider.GetSigningKeyAsync(cancellationToken).ConfigureAwait(false);
        var securityKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var options = _options.CurrentValue;
        var configuredLifetime = options.Local.SessionLifetime <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(30)
            : options.Local.SessionLifetime;

        var effectiveLifetime = lifetime.HasValue && lifetime.Value > TimeSpan.Zero
            ? TimeSpan.FromMinutes(Math.Min(lifetime.Value.TotalMinutes, configuredLifetime.TotalMinutes))
            : configuredLifetime;

        var expires = now.Add(effectiveLifetime);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
        {
            if (role.HasValue())
            {
                claims.Add(new Claim(LocalAuthenticationDefaults.RoleClaimType, role));
            }
        }

        var token = new JwtSecurityToken(
            issuer: LocalAuthenticationDefaults.Issuer,
            audience: LocalAuthenticationDefaults.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class LocalAuthenticationDefaults
{
    public const string Issuer = "honua-local";
    public const string Audience = "honua-local";
    public const string RoleClaimType = "honua_role";
}
