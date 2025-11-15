// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a ShareToken entity.
/// Prevents mixing up share token IDs with other entity IDs and provides type safety.
/// </summary>
public sealed record ShareTokenId
{
    /// <summary>
    /// Gets the underlying GUID value of this share token ID.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShareTokenId"/> record.
    /// </summary>
    /// <param name="value">The GUID value for this share token ID.</param>
    /// <exception cref="DomainException">Thrown when the value is an empty GUID.</exception>
    public ShareTokenId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException(
                "Share token ID cannot be empty.",
                "SHARETOKENID_EMPTY");
        }

        Value = value;
    }

    /// <summary>
    /// Creates a new share token ID with a generated GUID.
    /// </summary>
    /// <returns>A new <see cref="ShareTokenId"/> instance with a unique value.</returns>
    public static ShareTokenId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a share token ID from a string representation of a GUID.
    /// </summary>
    /// <param name="value">The string representation of a GUID.</param>
    /// <returns>A new <see cref="ShareTokenId"/> instance.</returns>
    /// <exception cref="DomainException">Thrown when the string is not a valid GUID.</exception>
    public static ShareTokenId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new DomainException(
                $"Invalid share token ID format: {value}",
                "SHARETOKENID_INVALID_FORMAT");
        }

        return new ShareTokenId(guid);
    }

    /// <summary>
    /// Attempts to create a share token ID from a string representation of a GUID.
    /// </summary>
    /// <param name="value">The string representation of a GUID.</param>
    /// <param name="shareTokenId">When this method returns, contains the share token ID if parsing succeeded.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string value, out ShareTokenId? shareTokenId)
    {
        if (Guid.TryParse(value, out var guid) && guid != Guid.Empty)
        {
            shareTokenId = new ShareTokenId(guid);
            return true;
        }

        shareTokenId = null;
        return false;
    }

    /// <summary>
    /// Returns the string representation of this share token ID.
    /// </summary>
    /// <returns>The string representation of the underlying GUID.</returns>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Implicitly converts a <see cref="ShareTokenId"/> to a <see cref="Guid"/>.
    /// </summary>
    /// <param name="shareTokenId">The share token ID to convert.</param>
    public static implicit operator Guid(ShareTokenId shareTokenId) => shareTokenId.Value;
}
