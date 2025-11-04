// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

public sealed class PasswordHasher : IPasswordHasher
{
    private const string Argon2AlgorithmName = "Argon2id";
    private const string Pbkdf2AlgorithmName = "PBKDF2-SHA256";

    private static readonly IReadOnlyDictionary<string, Func<string, int>> ParameterParsers = new Dictionary<string, Func<string, int>>(StringComparer.OrdinalIgnoreCase)
    {
        ["timeCost"] = value => int.Parse(value, CultureInfo.InvariantCulture),
        ["memoryCost"] = value => int.Parse(value, CultureInfo.InvariantCulture),
        ["parallelism"] = value => int.Parse(value, CultureInfo.InvariantCulture),
        ["hashLength"] = value => int.Parse(value, CultureInfo.InvariantCulture),
        ["iterations"] = value => int.Parse(value, CultureInfo.InvariantCulture),
        ["hashSize"] = value => int.Parse(value, CultureInfo.InvariantCulture),
        ["saltSize"] = value => int.Parse(value, CultureInfo.InvariantCulture)
    };

    private static readonly int DefaultParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4);

    public PasswordHashResult HashPassword(string password)
    {
        Guard.NotNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(16);
        var argon2Params = new Argon2Parameters(
            timeCost: 4,
            memoryCost: 64 * 1024,
            parallelism: DefaultParallelism,
            hashLength: 32);

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            Iterations = argon2Params.TimeCost,
            MemorySize = argon2Params.MemoryCost,
            DegreeOfParallelism = argon2Params.Parallelism
        };

        var hash = argon2.GetBytes(argon2Params.HashLength);

        var serializedParameters = SerializeParameters(new Dictionary<string, object>
        {
            ["timeCost"] = argon2Params.TimeCost,
            ["memoryCost"] = argon2Params.MemoryCost,
            ["parallelism"] = argon2Params.Parallelism,
            ["hashLength"] = argon2Params.HashLength,
            ["saltSize"] = salt.Length
        });

        return new PasswordHashResult(hash, salt, Argon2AlgorithmName, serializedParameters);
    }

    public bool VerifyPassword(string password, byte[] hash, byte[] salt, string algorithm, string parameters)
    {
        Guard.NotNull(hash);
        Guard.NotNull(salt);
        Guard.NotNullOrWhiteSpace(algorithm);

        return algorithm switch
        {
            Argon2AlgorithmName => VerifyArgon2(password, hash, salt, parameters),
            Pbkdf2AlgorithmName => VerifyPbkdf2(password, hash, salt, parameters),
            _ => throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Unsupported password hash algorithm '{0}'.", algorithm))
        };
    }

    private static bool VerifyArgon2(string password, byte[] hash, byte[] salt, string parameters)
    {
        if (hash.Length == 0)
        {
            return false;
        }

        var parsed = ParseParameters(parameters);
        var config = new Argon2Parameters(
            timeCost: parsed.GetValueOrDefault("timeCost", 4),
            memoryCost: parsed.GetValueOrDefault("memoryCost", 64 * 1024),
            parallelism: parsed.GetValueOrDefault("parallelism", DefaultParallelism),
            hashLength: parsed.GetValueOrDefault("hashLength", hash.Length));

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            Iterations = config.TimeCost,
            MemorySize = config.MemoryCost,
            DegreeOfParallelism = config.Parallelism
        };

        var candidate = argon2.GetBytes(config.HashLength);
        var candidateSpan = candidate.AsSpan();
        if (candidateSpan.Length != hash.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(candidateSpan, hash);
    }

    private static bool VerifyPbkdf2(string password, byte[] hash, byte[] salt, string parameters)
    {
        if (hash.Length == 0)
        {
            return false;
        }

        var parsed = ParseParameters(parameters);
        var iterations = parsed.GetValueOrDefault("iterations", 210_000);
        var hashSize = parsed.GetValueOrDefault("hashSize", hash.Length);

        var computed = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, hashSize);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    private static Dictionary<string, int> ParseParameters(string parameters)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (parameters.IsNullOrWhiteSpace())
        {
            return result;
        }

        var pairs = parameters.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var kvp = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kvp.Length != 2)
            {
                continue;
            }

            if (ParameterParsers.TryGetValue(kvp[0], out var parser))
            {
                result[kvp[0]] = parser(kvp[1]);
            }
        }

        return result;
    }

    private static string SerializeParameters(IReadOnlyDictionary<string, object> parameters)
    {
        var builder = new StringBuilder();
        var first = true;
        foreach (var kvp in parameters)
        {
            if (!first)
            {
                builder.Append(';');
            }

            builder.Append(kvp.Key);
            builder.Append('=');
            builder.Append(Convert.ToString(kvp.Value, CultureInfo.InvariantCulture));
            first = false;
        }

        return builder.ToString();
    }

    private readonly struct Argon2Parameters
    {
        public Argon2Parameters(int timeCost, int memoryCost, int parallelism, int hashLength)
        {
            TimeCost = timeCost;
            MemoryCost = memoryCost;
            Parallelism = parallelism;
            HashLength = hashLength;
        }

        public int TimeCost { get; }

        public int MemoryCost { get; }

        public int Parallelism { get; }

        public int HashLength { get; }
    }
}

public sealed record PasswordHashResult(byte[] Hash, byte[] Salt, string Algorithm, string Parameters);
