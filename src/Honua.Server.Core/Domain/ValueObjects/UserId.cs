// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a User entity.
/// Prevents mixing up user IDs with other entity IDs and provides type safety.
/// </summary>
public sealed record UserId
{
    /// <summary>
    /// Gets the underlying GUID value of this user ID.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserId"/> record.
    /// </summary>
    /// <param name="value">The GUID value for this user ID.</param>
    /// <exception cref="DomainException">Thrown when the value is an empty GUID.</exception>
    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException(
                "User ID cannot be empty.",
                "USERID_EMPTY");
        }

        Value = value;
    }

    /// <summary>
    /// Creates a new user ID with a generated GUID.
    /// </summary>
    /// <returns>A new <see cref="UserId"/> instance with a unique value.</returns>
    public static UserId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a user ID from a string representation of a GUID.
    /// </summary>
    /// <param name="value">The string representation of a GUID.</param>
    /// <returns>A new <see cref="UserId"/> instance.</returns>
    /// <exception cref="DomainException">Thrown when the string is not a valid GUID.</exception>
    public static UserId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new DomainException(
                $"Invalid user ID format: {value}",
                "USERID_INVALID_FORMAT");
        }

        return new UserId(guid);
    }

    /// <summary>
    /// Attempts to create a user ID from a string representation of a GUID.
    /// </summary>
    /// <param name="value">The string representation of a GUID.</param>
    /// <param name="userId">When this method returns, contains the user ID if parsing succeeded.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string value, out UserId? userId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            userId = new UserId(guid);
            return true;
        }

        userId = null;
        return false;
    }

    /// <summary>
    /// Returns the string representation of this user ID.
    /// </summary>
    /// <returns>The string representation of the underlying GUID.</returns>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Implicitly converts a <see cref="UserId"/> to a <see cref="Guid"/>.
    /// </summary>
    /// <param name="userId">The user ID to convert.</param>
    public static implicit operator Guid(UserId userId) => userId.Value;
}
