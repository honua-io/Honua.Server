// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Common;

namespace Honua.Server.Core.Domain.Sharing;

/// <summary>
/// Entity representing a comment on a shared map.
/// This is part of the Share aggregate and cannot exist independently.
/// </summary>
public sealed class ShareComment : Entity<string>
{
    /// <summary>
    /// Gets the author of the comment (user ID or guest name)
    /// </summary>
    public string Author { get; private set; }

    /// <summary>
    /// Gets whether this is a guest comment
    /// </summary>
    public bool IsGuest { get; private set; }

    /// <summary>
    /// Gets the guest email (for notifications)
    /// </summary>
    public string? GuestEmail { get; private set; }

    /// <summary>
    /// Gets the comment text
    /// </summary>
    public string Text { get; private set; }

    /// <summary>
    /// Gets when the comment was created
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets whether the comment has been approved
    /// </summary>
    public bool IsApproved { get; private set; }

    /// <summary>
    /// Gets whether the comment has been deleted
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Gets the parent comment ID for threaded discussions
    /// </summary>
    public string? ParentId { get; private set; }

    /// <summary>
    /// Gets the location coordinates for map annotations
    /// </summary>
    public (double? X, double? Y) Location { get; private set; }

    /// <summary>
    /// Gets the IP address for spam prevention
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Gets the user agent for spam prevention
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    private ShareComment() : base(string.Empty)
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new comment from an authenticated user
    /// </summary>
    /// <param name="author">The user ID of the author</param>
    /// <param name="text">The comment text</param>
    /// <param name="parentId">Optional parent comment ID for threading</param>
    /// <param name="locationX">Optional X coordinate for map annotation</param>
    /// <param name="locationY">Optional Y coordinate for map annotation</param>
    /// <returns>A new ShareComment instance</returns>
    /// <exception cref="ArgumentException">Thrown when input is invalid</exception>
    public static ShareComment CreateUserComment(
        string author,
        string text,
        string? parentId = null,
        double? locationX = null,
        double? locationY = null)
    {
        ValidateCommentText(text);
        ValidateAuthor(author);

        return new ShareComment
        {
            Id = Guid.NewGuid().ToString(),
            Author = author,
            IsGuest = false,
            GuestEmail = null,
            Text = text,
            CreatedAt = DateTime.UtcNow,
            IsApproved = true, // User comments are auto-approved
            IsDeleted = false,
            ParentId = parentId,
            Location = (locationX, locationY),
            IpAddress = null,
            UserAgent = null
        };
    }

    /// <summary>
    /// Creates a new comment from a guest
    /// </summary>
    /// <param name="guestName">The name of the guest</param>
    /// <param name="guestEmail">The email of the guest</param>
    /// <param name="text">The comment text</param>
    /// <param name="ipAddress">The IP address of the guest</param>
    /// <param name="userAgent">The user agent of the guest</param>
    /// <param name="parentId">Optional parent comment ID for threading</param>
    /// <param name="locationX">Optional X coordinate for map annotation</param>
    /// <param name="locationY">Optional Y coordinate for map annotation</param>
    /// <returns>A new ShareComment instance</returns>
    /// <exception cref="ArgumentException">Thrown when input is invalid</exception>
    public static ShareComment CreateGuestComment(
        string guestName,
        string? guestEmail,
        string text,
        string? ipAddress,
        string? userAgent,
        string? parentId = null,
        double? locationX = null,
        double? locationY = null)
    {
        ValidateCommentText(text);
        ValidateAuthor(guestName);

        if (!string.IsNullOrWhiteSpace(guestEmail) && !IsValidEmail(guestEmail))
            throw new ArgumentException("Invalid email format", nameof(guestEmail));

        return new ShareComment
        {
            Id = Guid.NewGuid().ToString(),
            Author = guestName,
            IsGuest = true,
            GuestEmail = guestEmail,
            Text = text,
            CreatedAt = DateTime.UtcNow,
            IsApproved = false, // Guest comments require approval
            IsDeleted = false,
            ParentId = parentId,
            Location = (locationX, locationY),
            IpAddress = ipAddress?.Length > 45 ? ipAddress[..45] : ipAddress,
            UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent
        };
    }

    /// <summary>
    /// Approves the comment
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when comment is already deleted</exception>
    public void Approve()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot approve a deleted comment");

        IsApproved = true;
    }

    /// <summary>
    /// Marks the comment as deleted
    /// </summary>
    public void Delete()
    {
        IsDeleted = true;
    }

    /// <summary>
    /// Restores a deleted comment
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
    }

    /// <summary>
    /// Validates comment text
    /// </summary>
    private static void ValidateCommentText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Comment text cannot be empty", nameof(text));

        if (text.Length > 5000)
            throw new ArgumentException("Comment text must not exceed 5000 characters", nameof(text));
    }

    /// <summary>
    /// Validates author name
    /// </summary>
    private static void ValidateAuthor(string author)
    {
        if (string.IsNullOrWhiteSpace(author))
            throw new ArgumentException("Author cannot be empty", nameof(author));

        if (author.Length > 200)
            throw new ArgumentException("Author name must not exceed 200 characters", nameof(author));
    }

    /// <summary>
    /// Simple email validation
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
