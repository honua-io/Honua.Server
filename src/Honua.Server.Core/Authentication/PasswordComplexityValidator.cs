// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

/// <summary>
/// Validates password complexity requirements to meet security best practices.
/// </summary>
public interface IPasswordComplexityValidator
{
    PasswordComplexityResult Validate(string password);
}

public sealed record PasswordComplexityResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PasswordComplexityResult Success() => new(true, Array.Empty<string>());
    public static PasswordComplexityResult Failure(params string[] errors) => new(false, errors);
}

public sealed class PasswordComplexityValidator : IPasswordComplexityValidator
{
    private readonly int _minimumLength;
    private readonly bool _requireUppercase;
    private readonly bool _requireLowercase;
    private readonly bool _requireDigit;
    private readonly bool _requireSpecialCharacter;

    public PasswordComplexityValidator(
        int minimumLength = 12,
        bool requireUppercase = true,
        bool requireLowercase = true,
        bool requireDigit = true,
        bool requireSpecialCharacter = true)
    {
        _minimumLength = minimumLength;
        _requireUppercase = requireUppercase;
        _requireLowercase = requireLowercase;
        _requireDigit = requireDigit;
        _requireSpecialCharacter = requireSpecialCharacter;
    }

    public PasswordComplexityResult Validate(string password)
    {
        if (password.IsNullOrEmpty())
        {
            return PasswordComplexityResult.Failure("Password is required.");
        }

        var errors = new List<string>();

        // Check minimum length
        if (password.Length < _minimumLength)
        {
            errors.Add($"Password must be at least {_minimumLength} characters long.");
        }

        // Check for uppercase letter
        if (_requireUppercase && !Regex.IsMatch(password, "[A-Z]"))
        {
            errors.Add("Password must contain at least one uppercase letter (A-Z).");
        }

        // Check for lowercase letter
        if (_requireLowercase && !Regex.IsMatch(password, "[a-z]"))
        {
            errors.Add("Password must contain at least one lowercase letter (a-z).");
        }

        // Check for digit
        if (_requireDigit && !Regex.IsMatch(password, "[0-9]"))
        {
            errors.Add("Password must contain at least one digit (0-9).");
        }

        // Check for special character
        if (_requireSpecialCharacter && !Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>?/~`]"))
        {
            errors.Add("Password must contain at least one special character (!@#$%^&* etc).");
        }

        // Check for common weak passwords
        if (IsCommonPassword(password))
        {
            errors.Add("Password is too common. Please choose a more unique password.");
        }

        return errors.Count == 0
            ? PasswordComplexityResult.Success()
            : PasswordComplexityResult.Failure(errors.ToArray());
    }

    private static bool IsCommonPassword(string password)
    {
        // Check against most common weak passwords
        var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "password123", "password123!", "123456", "12345678", "123456789",
            "qwerty", "abc123", "monkey", "1234567", "letmein",
            "trustno1", "dragon", "baseball", "iloveyou", "master",
            "sunshine", "ashley", "bailey", "passw0rd", "shadow",
            "123123", "654321", "superman", "qazwsx", "michael",
            "football", "admin", "admin123", "admin123!", "welcome", "welcome123", "welcome123!", "login", "starwars"
        };

        return commonPasswords.Contains(password);
    }
}
