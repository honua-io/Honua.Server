// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Honua.Server.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a username with validation.
/// Usernames must be alphanumeric with optional hyphens, underscores, and periods.
/// </summary>
public sealed partial record Username
{
    private const int MinLength = 3;
    private const int MaxLength = 30;

    /// <summary>
    /// Gets the username value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Username"/> record.
    /// </summary>
    /// <param name="value">The username value.</param>
    /// <exception cref="DomainException">Thrown when the username is invalid.</exception>
    public Username(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(
                "Username cannot be empty.",
                "USERNAME_EMPTY");
        }

        value = value.Trim();

        if (value.Length < MinLength)
        {
            throw new DomainException(
                $"Username must be at least {MinLength} characters long.",
                "USERNAME_TOO_SHORT");
        }

        if (value.Length > MaxLength)
        {
            throw new DomainException(
                $"Username cannot exceed {MaxLength} characters.",
                "USERNAME_TOO_LONG");
        }

        if (!UsernameRegex().IsMatch(value))
        {
            throw new DomainException(
                "Username can only contain letters, numbers, hyphens, underscores, and periods. It must start and end with a letter or number.",
                "USERNAME_INVALID_FORMAT");
        }

        if (value.Contains("..") || value.Contains("--") || value.Contains("__"))
        {
            throw new DomainException(
                "Username cannot contain consecutive special characters.",
                "USERNAME_CONSECUTIVE_SPECIAL_CHARS");
        }

        Value = value;
    }

    /// <summary>
    /// Creates a username from a string value.
    /// </summary>
    /// <param name="value">The username string.</param>
    /// <returns>A new <see cref="Username"/> instance.</returns>
    /// <exception cref="DomainException">Thrown when the username is invalid.</exception>
    public static Username From(string value) => new(value);

    /// <summary>
    /// Attempts to create a username from a string value.
    /// </summary>
    /// <param name="value">The username string.</param>
    /// <param name="username">When this method returns, contains the username if validation succeeded.</param>
    /// <returns>true if validation succeeded; otherwise, false.</returns>
    public static bool TryCreate(string value, out Username? username)
    {
        try
        {
            username = new Username(value);
            return true;
        }
        catch (DomainException)
        {
            username = null;
            return false;
        }
    }

    /// <summary>
    /// Returns the string representation of this username.
    /// </summary>
    /// <returns>The username value.</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="Username"/> to a string.
    /// </summary>
    /// <param name="username">The username to convert.</param>
    public static implicit operator string(Username username) => username.Value;

    /// <summary>
    /// Regular expression for validating usernames.
    /// Must start and end with alphanumeric character, can contain hyphens, underscores, and periods.
    /// </summary>
    [GeneratedRegex(
        @"^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$",
        RegexOptions.Compiled,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex UsernameRegex();
}
