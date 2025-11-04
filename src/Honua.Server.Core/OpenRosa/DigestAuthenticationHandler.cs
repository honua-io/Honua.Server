// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Honua.Server.Core.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.OpenRosa;

/// <summary>
/// HTTP Digest authentication handler for OpenRosa API.
/// Required by OpenRosa specification for ODK Collect compatibility.
/// Implements secure digest authentication with HA1 hash storage and nonce replay protection.
/// </summary>
public class DigestAuthenticationHandler : AuthenticationHandler<DigestAuthenticationOptions>
{
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _cache;

    public DigestAuthenticationHandler(
        IOptionsMonitor<DigestAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserRepository userRepository,
        IMemoryCache cache)
        : base(options, logger, encoder)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.NoResult();
        }

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var digestParams = ParseDigestHeader(authHeader.Substring(7));

            if (!digestParams.TryGetValue("username", out var username) ||
                !digestParams.TryGetValue("realm", out var realm) ||
                !digestParams.TryGetValue("nonce", out var nonce) ||
                !digestParams.TryGetValue("uri", out var uri) ||
                !digestParams.TryGetValue("response", out var clientResponse))
            {
                return AuthenticateResult.Fail("Missing required Digest parameters");
            }

            // Validate nonce (time-based validation with replay protection)
            if (!ValidateNonce(nonce, out var nonceKey))
            {
                return AuthenticateResult.Fail("Nonce expired or invalid");
            }

            // Check for nonce replay
            if (_cache.TryGetValue(nonceKey, out _))
            {
                Logger.LogWarning("Nonce replay attack detected: {Nonce}", nonce);
                return AuthenticateResult.Fail("Nonce has already been used");
            }

            // Get user from repository
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user is null)
            {
                return AuthenticateResult.Fail("Invalid username or password");
            }

            // Compute expected response
            // user.Password should now contain the pre-computed HA1 hash (MD5(username:realm:password))
            var ha1 = user.Password;
            var ha2 = ComputeHA2(Request.Method, uri);
            var expectedResponse = ComputeResponse(ha1, nonce, ha2);

            if (!string.Equals(clientResponse, expectedResponse, StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.Fail("Invalid username or password");
            }

            // Mark nonce as used to prevent replay attacks
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Options.NonceLifetimeMinutes)
            };
            _cache.Set(nonceKey, true, cacheOptions);

            // Create claims principal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Digest authentication failed");
            return AuthenticateResult.Fail("Digest authentication error");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var nonce = GenerateNonce();
        var realm = Options.Realm;

        Response.StatusCode = 401;
        Response.Headers["WWW-Authenticate"] = $"Digest realm=\"{realm}\", nonce=\"{nonce}\", qop=\"auth\"";

        return Task.CompletedTask;
    }

    private Dictionary<string, string> ParseDigestHeader(string header)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var parts = header.Split(',');
        foreach (var part in parts)
        {
            var keyValue = part.Trim().Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim().Trim('"');
                result[key] = value;
            }
        }

        return result;
    }

    private string GenerateNonce()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var data = $"{timestamp}:{Convert.ToBase64String(randomBytes)}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
    }

    private bool ValidateNonce(string nonce, out string nonceKey)
    {
        nonceKey = string.Empty;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(nonce));
            var parts = decoded.Split(':');
            if (parts.Length < 1)
                return false;

            if (!long.TryParse(parts[0], out var timestamp))
                return false;

            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp;
            if (age < 0 || age > (Options.NonceLifetimeMinutes * 60))
                return false;

            // Create unique cache key for this nonce
            nonceKey = $"digest_nonce:{nonce}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Computes the HA1 hash for digest authentication.
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="realm">Authentication realm</param>
    /// <param name="password">Password (plaintext)</param>
    /// <returns>MD5 hash of username:realm:password</returns>
    /// <remarks>
    /// This method is public to allow pre-computation of HA1 hashes for storage.
    /// Implementations should store the result of this method, not plaintext passwords.
    /// </remarks>
    public static string ComputeHA1(string username, string realm, string password)
    {
        var data = $"{username}:{realm}:{password}";
        return ComputeMD5Hash(data);
    }

    private string ComputeHA2(string method, string uri)
    {
        var data = $"{method}:{uri}";
        return ComputeMD5Hash(data);
    }

    private string ComputeResponse(string ha1, string nonce, string ha2)
    {
        var data = $"{ha1}:{nonce}:{ha2}";
        return ComputeMD5Hash(data);
    }

    // MD5 is required by OpenRosa/ODK Digest Authentication specification (RFC 2617)
    // Not used for cryptographic security, only for legacy protocol compliance
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
    private static string ComputeMD5Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
#pragma warning restore CA5351
}

public class DigestAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Realm { get; set; } = "Honua OpenRosa";
    public int NonceLifetimeMinutes { get; set; } = 5;
}

/// <summary>
/// Simple user model for Digest authentication.
/// </summary>
public interface IUserRepository
{
    Task<DigestUser?> GetByUsernameAsync(string username);
}

public class DigestUser
{
    public required string Id { get; init; }
    public required string Username { get; init; }

    /// <summary>
    /// Pre-computed HA1 hash: MD5(username:realm:password).
    /// Use <see cref="DigestAuthenticationHandler.ComputeHA1"/> to generate this value.
    /// Never store plaintext passwords.
    /// </summary>
    public required string Password { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
