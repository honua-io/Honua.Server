// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Honua.Server.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing an email address with validation.
/// Ensures email addresses conform to RFC 5322 standards.
/// </summary>
public sealed partial record EmailAddress
{
    private const int MaxLength = 254; // RFC 5321
    private const int MaxLocalPartLength = 64; // RFC 5321

    /// <summary>
    /// Gets the email address value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the local part of the email address (before the @ symbol).
    /// </summary>
    public string LocalPart { get; }

    /// <summary>
    /// Gets the domain part of the email address (after the @ symbol).
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAddress"/> record.
    /// </summary>
    /// <param name="value">The email address value.</param>
    /// <exception cref="DomainException">Thrown when the email address is invalid.</exception>
    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(
                "Email address cannot be empty.",
                "EMAIL_EMPTY");
        }

        value = value.Trim().ToLowerInvariant();

        if (value.Length > MaxLength)
        {
            throw new DomainException(
                $"Email address cannot exceed {MaxLength} characters.",
                "EMAIL_TOO_LONG");
        }

        if (!EmailRegex().IsMatch(value))
        {
            throw new DomainException(
                "Email address format is invalid.",
                "EMAIL_INVALID_FORMAT");
        }

        var parts = value.Split('@');
        if (parts.Length != 2)
        {
            throw new DomainException(
                "Email address must contain exactly one @ symbol.",
                "EMAIL_INVALID_FORMAT");
        }

        LocalPart = parts[0];
        Domain = parts[1];

        if (LocalPart.Length > MaxLocalPartLength)
        {
            throw new DomainException(
                $"Email local part cannot exceed {MaxLocalPartLength} characters.",
                "EMAIL_LOCAL_PART_TOO_LONG");
        }

        Value = value;
    }

    /// <summary>
    /// Creates an email address from a string value.
    /// </summary>
    /// <param name="value">The email address string.</param>
    /// <returns>A new <see cref="EmailAddress"/> instance.</returns>
    /// <exception cref="DomainException">Thrown when the email address is invalid.</exception>
    public static EmailAddress From(string value) => new(value);

    /// <summary>
    /// Attempts to create an email address from a string value.
    /// </summary>
    /// <param name="value">The email address string.</param>
    /// <param name="emailAddress">When this method returns, contains the email address if validation succeeded.</param>
    /// <returns>true if validation succeeded; otherwise, false.</returns>
    public static bool TryCreate(string value, out EmailAddress? emailAddress)
    {
        try
        {
            emailAddress = new EmailAddress(value);
            return true;
        }
        catch (DomainException)
        {
            emailAddress = null;
            return false;
        }
    }

    /// <summary>
    /// Returns the string representation of this email address.
    /// </summary>
    /// <returns>The email address value.</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts an <see cref="EmailAddress"/> to a string.
    /// </summary>
    /// <param name="emailAddress">The email address to convert.</param>
    public static implicit operator string(EmailAddress emailAddress) => emailAddress.Value;

    /// <summary>
    /// Regular expression for validating email addresses.
    /// Follows RFC 5322 simplified pattern for practical email validation.
    /// </summary>
    [GeneratedRegex(
        @"^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailRegex();
}
