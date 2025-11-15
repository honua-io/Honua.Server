// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain.Sharing;
using Xunit;

namespace Honua.Server.Domain.Tests.Sharing;

[Trait("Category", "Unit")]
public sealed class ShareCommentTests
{
    #region CreateUserComment Tests

    [Fact]
    public void CreateUserComment_WithValidParameters_ShouldCreateComment()
    {
        // Arrange & Act
        var comment = ShareComment.CreateUserComment("user-123", "Great work!");

        // Assert
        comment.Should().NotBeNull();
        comment.Id.Should().NotBeNullOrEmpty();
        comment.Author.Should().Be("user-123");
        comment.Text.Should().Be("Great work!");
        comment.IsGuest.Should().BeFalse();
        comment.IsApproved.Should().BeTrue(); // User comments auto-approved
        comment.IsDeleted.Should().BeFalse();
        comment.GuestEmail.Should().BeNull();
        comment.IpAddress.Should().BeNull();
        comment.UserAgent.Should().BeNull();
        comment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateUserComment_WithLocationCoordinates_ShouldStoreLocation()
    {
        // Arrange & Act
        var comment = ShareComment.CreateUserComment(
            "user-123",
            "Point of interest",
            locationX: 45.5,
            locationY: -122.6);

        // Assert
        comment.Location.X.Should().Be(45.5);
        comment.Location.Y.Should().Be(-122.6);
    }

    [Fact]
    public void CreateUserComment_WithParentId_ShouldSetParentId()
    {
        // Arrange & Act
        var comment = ShareComment.CreateUserComment(
            "user-123",
            "Reply to comment",
            parentId: "parent-comment-id");

        // Assert
        comment.ParentId.Should().Be("parent-comment-id");
    }

    [Fact]
    public void CreateUserComment_WithEmptyAuthor_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateUserComment("", "Comment text");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Author cannot be empty*")
            .And.ParamName.Should().Be("author");
    }

    [Fact]
    public void CreateUserComment_WithNullAuthor_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateUserComment(null!, "Comment text");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Author cannot be empty*");
    }

    [Fact]
    public void CreateUserComment_WithWhitespaceAuthor_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateUserComment("   ", "Comment text");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Author cannot be empty*");
    }

    [Fact]
    public void CreateUserComment_WithAuthorTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var longAuthor = new string('x', 201);

        // Act
        Action act = () => ShareComment.CreateUserComment(longAuthor, "Comment text");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Author name must not exceed 200 characters*")
            .And.ParamName.Should().Be("author");
    }

    [Fact]
    public void CreateUserComment_WithEmptyText_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateUserComment("user-123", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Comment text cannot be empty*")
            .And.ParamName.Should().Be("text");
    }

    [Fact]
    public void CreateUserComment_WithNullText_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateUserComment("user-123", null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Comment text cannot be empty*");
    }

    [Fact]
    public void CreateUserComment_WithWhitespaceText_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateUserComment("user-123", "   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Comment text cannot be empty*");
    }

    [Fact]
    public void CreateUserComment_WithTextTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var longText = new string('x', 5001);

        // Act
        Action act = () => ShareComment.CreateUserComment("user-123", longText);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Comment text must not exceed 5000 characters*")
            .And.ParamName.Should().Be("text");
    }

    [Fact]
    public void CreateUserComment_WithMaxLengthText_ShouldSucceed()
    {
        // Arrange
        var maxLengthText = new string('x', 5000);

        // Act
        var comment = ShareComment.CreateUserComment("user-123", maxLengthText);

        // Assert
        comment.Should().NotBeNull();
        comment.Text.Length.Should().Be(5000);
    }

    #endregion

    #region CreateGuestComment Tests

    [Fact]
    public void CreateGuestComment_WithValidParameters_ShouldCreateComment()
    {
        // Arrange & Act
        var comment = ShareComment.CreateGuestComment(
            "John Doe",
            "john@example.com",
            "Nice work!",
            "192.168.1.1",
            "Mozilla/5.0");

        // Assert
        comment.Should().NotBeNull();
        comment.Id.Should().NotBeNullOrEmpty();
        comment.Author.Should().Be("John Doe");
        comment.Text.Should().Be("Nice work!");
        comment.IsGuest.Should().BeTrue();
        comment.IsApproved.Should().BeFalse(); // Guest comments require approval
        comment.IsDeleted.Should().BeFalse();
        comment.GuestEmail.Should().Be("john@example.com");
        comment.IpAddress.Should().Be("192.168.1.1");
        comment.UserAgent.Should().Be("Mozilla/5.0");
        comment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateGuestComment_WithNullEmail_ShouldSucceed()
    {
        // Arrange & Act
        var comment = ShareComment.CreateGuestComment(
            "Anonymous",
            null,
            "Comment without email",
            "192.168.1.1",
            "Mozilla/5.0");

        // Assert
        comment.Should().NotBeNull();
        comment.GuestEmail.Should().BeNull();
    }

    [Fact]
    public void CreateGuestComment_WithInvalidEmail_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateGuestComment(
            "Guest",
            "not-an-email",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid email format*")
            .And.ParamName.Should().Be("guestEmail");
    }

    [Fact]
    public void CreateGuestComment_WithValidEmail_ShouldAcceptEmail()
    {
        // Arrange & Act
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "valid@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        comment.GuestEmail.Should().Be("valid@example.com");
    }

    [Fact]
    public void CreateGuestComment_WithLongIpAddress_ShouldTruncateTo45Chars()
    {
        // Arrange
        var longIp = new string('x', 50);

        // Act
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            longIp,
            "Mozilla");

        // Assert
        comment.IpAddress.Should().HaveLength(45);
    }

    [Fact]
    public void CreateGuestComment_WithLongUserAgent_ShouldTruncateTo500Chars()
    {
        // Arrange
        var longUserAgent = new string('x', 600);

        // Act
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            longUserAgent);

        // Assert
        comment.UserAgent.Should().HaveLength(500);
    }

    [Fact]
    public void CreateGuestComment_WithNullIpAddress_ShouldSucceed()
    {
        // Arrange & Act
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            null,
            "Mozilla");

        // Assert
        comment.IpAddress.Should().BeNull();
    }

    [Fact]
    public void CreateGuestComment_WithNullUserAgent_ShouldSucceed()
    {
        // Arrange & Act
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            null);

        // Assert
        comment.UserAgent.Should().BeNull();
    }

    [Fact]
    public void CreateGuestComment_WithLocationAndParent_ShouldSetAllProperties()
    {
        // Arrange & Act
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Reply with location",
            "192.168.1.1",
            "Mozilla",
            parentId: "parent-id",
            locationX: 10.5,
            locationY: 20.5);

        // Assert
        comment.ParentId.Should().Be("parent-id");
        comment.Location.X.Should().Be(10.5);
        comment.Location.Y.Should().Be(20.5);
    }

    [Fact]
    public void CreateGuestComment_WithEmptyGuestName_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateGuestComment(
            "",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Author cannot be empty*");
    }

    [Fact]
    public void CreateGuestComment_WithEmptyText_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "",
            "192.168.1.1",
            "Mozilla");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Comment text cannot be empty*");
    }

    #endregion

    #region Approve Tests

    [Fact]
    public void Approve_GuestComment_ShouldSetIsApprovedTrue()
    {
        // Arrange
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        comment.IsApproved.Should().BeFalse(); // Precondition

        // Act
        comment.Approve();

        // Assert
        comment.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void Approve_AlreadyApprovedComment_ShouldRemainApproved()
    {
        // Arrange
        var comment = ShareComment.CreateUserComment("user-123", "Comment");
        comment.IsApproved.Should().BeTrue(); // Precondition

        // Act
        comment.Approve();

        // Assert
        comment.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void Approve_DeletedComment_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");
        comment.Delete();

        // Act
        Action act = () => comment.Approve();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot approve a deleted comment");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_ShouldSetIsDeletedTrue()
    {
        // Arrange
        var comment = ShareComment.CreateUserComment("user-123", "Comment");
        comment.IsDeleted.Should().BeFalse(); // Precondition

        // Act
        comment.Delete();

        // Assert
        comment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Delete_AlreadyDeletedComment_ShouldRemainDeleted()
    {
        // Arrange
        var comment = ShareComment.CreateUserComment("user-123", "Comment");
        comment.Delete();

        // Act
        comment.Delete();

        // Assert
        comment.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Restore Tests

    [Fact]
    public void Restore_DeletedComment_ShouldSetIsDeletedFalse()
    {
        // Arrange
        var comment = ShareComment.CreateUserComment("user-123", "Comment");
        comment.Delete();
        comment.IsDeleted.Should().BeTrue(); // Precondition

        // Act
        comment.Restore();

        // Assert
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Restore_NonDeletedComment_ShouldRemainNotDeleted()
    {
        // Arrange
        var comment = ShareComment.CreateUserComment("user-123", "Comment");
        comment.IsDeleted.Should().BeFalse(); // Precondition

        // Act
        comment.Restore();

        // Assert
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Restore_ThenApprove_ShouldWork()
    {
        // Arrange
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");
        comment.Delete();

        // Act
        comment.Restore();
        comment.Approve();

        // Assert
        comment.IsDeleted.Should().BeFalse();
        comment.IsApproved.Should().BeTrue();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void CreateUserComment_WithMaxLengthAuthor_ShouldSucceed()
    {
        // Arrange
        var maxAuthor = new string('x', 200);

        // Act
        var comment = ShareComment.CreateUserComment(maxAuthor, "Comment");

        // Assert
        comment.Author.Length.Should().Be(200);
    }

    [Fact]
    public void CreateGuestComment_WithEmptyWhitespaceEmail_ShouldSucceed()
    {
        // Arrange & Act
        var comment = ShareComment.CreateGuestComment(
            "Guest",
            "   ",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        comment.GuestEmail.Should().BeNull();
    }

    [Fact]
    public void CreateUserComment_GeneratesUniqueIds_ForMultipleComments()
    {
        // Arrange & Act
        var comment1 = ShareComment.CreateUserComment("user-123", "Comment 1");
        var comment2 = ShareComment.CreateUserComment("user-123", "Comment 2");
        var comment3 = ShareComment.CreateUserComment("user-123", "Comment 3");

        // Assert
        comment1.Id.Should().NotBe(comment2.Id);
        comment2.Id.Should().NotBe(comment3.Id);
        comment1.Id.Should().NotBe(comment3.Id);
    }

    [Fact]
    public void CreateGuestComment_GeneratesUniqueIds_ForMultipleComments()
    {
        // Arrange & Act
        var comment1 = ShareComment.CreateGuestComment("Guest1", null, "C1", "192.168.1.1", "M1");
        var comment2 = ShareComment.CreateGuestComment("Guest2", null, "C2", "192.168.1.2", "M2");
        var comment3 = ShareComment.CreateGuestComment("Guest3", null, "C3", "192.168.1.3", "M3");

        // Assert
        comment1.Id.Should().NotBe(comment2.Id);
        comment2.Id.Should().NotBe(comment3.Id);
        comment1.Id.Should().NotBe(comment3.Id);
    }

    #endregion
}
