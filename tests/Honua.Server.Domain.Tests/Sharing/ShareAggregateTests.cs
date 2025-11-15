// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain.Sharing;
using Honua.Server.Core.Domain.Sharing.Events;
using Xunit;

namespace Honua.Server.Domain.Tests.Sharing;

[Trait("Category", "Unit")]
public sealed class ShareAggregateTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_WithValidParameters_ShouldCreateShare()
    {
        // Arrange & Act
        var share = ShareAggregate.Create(
            mapId: "map-123",
            createdBy: "user-456",
            permission: SharePermission.View,
            allowGuestAccess: true);

        // Assert
        share.Should().NotBeNull();
        share.Id.Should().NotBeNullOrEmpty();
        share.MapId.Should().Be("map-123");
        share.CreatedBy.Should().Be("user-456");
        share.Permission.Should().Be(SharePermission.View);
        share.AllowGuestAccess.Should().BeTrue();
        share.IsActive.Should().BeTrue();
        share.AccessCount.Should().Be(0);
        share.LastAccessedAt.Should().BeNull();
        share.IsPasswordProtected.Should().BeFalse();
        share.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void Create_ShouldRaiseShareCreatedEvent()
    {
        // Arrange & Act
        var share = ShareAggregate.Create(
            mapId: "map-123",
            createdBy: "user-456",
            permission: SharePermission.Comment,
            allowGuestAccess: false,
            expiresAt: DateTime.UtcNow.AddDays(7));

        // Assert
        share.DomainEvents.Should().ContainSingle();
        var domainEvent = share.DomainEvents.First();
        domainEvent.Should().BeOfType<ShareCreatedEvent>();

        var shareCreatedEvent = (ShareCreatedEvent)domainEvent;
        shareCreatedEvent.Token.Should().Be(share.Id);
        shareCreatedEvent.MapId.Should().Be("map-123");
        shareCreatedEvent.CreatedBy.Should().Be("user-456");
        shareCreatedEvent.Permission.Should().Be(SharePermission.Comment);
        shareCreatedEvent.AllowGuestAccess.Should().BeFalse();
        shareCreatedEvent.IsPasswordProtected.Should().BeFalse();
    }

    [Fact]
    public void Create_WithPassword_ShouldSetPasswordProtected()
    {
        // Arrange
        var password = SharePassword.Create("secure-password");

        // Act
        var share = ShareAggregate.Create(
            mapId: "map-123",
            createdBy: "user-456",
            permission: SharePermission.View,
            password: password);

        // Assert
        share.IsPasswordProtected.Should().BeTrue();
        share.Password.Should().Be(password);

        var shareCreatedEvent = (ShareCreatedEvent)share.DomainEvents.First();
        shareCreatedEvent.IsPasswordProtected.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyMapId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareAggregate.Create(
            mapId: "",
            createdBy: "user-456",
            permission: SharePermission.View);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Map ID cannot be empty*")
            .And.ParamName.Should().Be("mapId");
    }

    [Fact]
    public void Create_WithNullMapId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareAggregate.Create(
            mapId: null!,
            createdBy: "user-456",
            permission: SharePermission.View);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Map ID cannot be empty*");
    }

    [Fact]
    public void Create_WithMapIdTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var longMapId = new string('x', 101);

        // Act
        Action act = () => ShareAggregate.Create(
            mapId: longMapId,
            createdBy: "user-456",
            permission: SharePermission.View);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Map ID must not exceed 100 characters*")
            .And.ParamName.Should().Be("mapId");
    }

    [Fact]
    public void Create_WithPastExpirationDate_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareAggregate.Create(
            mapId: "map-123",
            createdBy: "user-456",
            permission: SharePermission.View,
            expiresAt: DateTime.UtcNow.AddDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Expiration date must be in the future*")
            .And.ParamName.Should().Be("expiresAt");
    }

    [Fact]
    public void Create_WithNullCreatedBy_ShouldSucceed()
    {
        // Arrange & Act
        var share = ShareAggregate.Create(
            mapId: "map-123",
            createdBy: null,
            permission: SharePermission.View);

        // Assert
        share.Should().NotBeNull();
        share.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void Create_WithCustomConfiguration_ShouldUseProvidedConfiguration()
    {
        // Arrange
        var config = ShareConfiguration.Create("800px", "400px");

        // Act
        var share = ShareAggregate.Create(
            mapId: "map-123",
            createdBy: "user-456",
            permission: SharePermission.View,
            configuration: config);

        // Assert
        share.Configuration.Should().Be(config);
        share.Configuration.Width.Should().Be("800px");
        share.Configuration.Height.Should().Be("400px");
    }

    [Fact]
    public void Create_WithoutConfiguration_ShouldUseDefaultConfiguration()
    {
        // Arrange & Act
        var share = ShareAggregate.Create(
            mapId: "map-123",
            createdBy: "user-456",
            permission: SharePermission.View);

        // Assert
        share.Configuration.Should().NotBeNull();
        share.Configuration.Width.Should().Be("100%");
        share.Configuration.Height.Should().Be("600px");
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public void Deactivate_ActiveShare_ShouldDeactivateAndRaiseEvent()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.ClearDomainEvents(); // Clear creation event

        // Act
        share.Deactivate("user-456", "No longer needed");

        // Assert
        share.IsActive.Should().BeFalse();
        share.IsValid.Should().BeFalse();

        share.DomainEvents.Should().ContainSingle();
        var domainEvent = share.DomainEvents.First();
        domainEvent.Should().BeOfType<ShareDeactivatedEvent>();

        var deactivatedEvent = (ShareDeactivatedEvent)domainEvent;
        deactivatedEvent.Token.Should().Be(share.Id);
        deactivatedEvent.MapId.Should().Be(share.MapId);
        deactivatedEvent.DeactivatedBy.Should().Be("user-456");
        deactivatedEvent.Reason.Should().Be("No longer needed");
    }

    [Fact]
    public void Deactivate_AlreadyInactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");

        // Act
        Action act = () => share.Deactivate("user-456");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Share is already inactive");
    }

    [Fact]
    public void Deactivate_WithoutReason_ShouldSucceed()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        share.Deactivate("user-456");

        // Assert
        share.IsActive.Should().BeFalse();
        var deactivatedEvent = (ShareDeactivatedEvent)share.DomainEvents.Last();
        deactivatedEvent.Reason.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WithNullDeactivatedBy_ShouldSucceed()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        share.Deactivate(null);

        // Assert
        share.IsActive.Should().BeFalse();
        var deactivatedEvent = (ShareDeactivatedEvent)share.DomainEvents.Last();
        deactivatedEvent.DeactivatedBy.Should().BeNull();
    }

    #endregion

    #region Renew Tests

    [Fact]
    public void Renew_ActiveNonExpiredShare_ShouldUpdateExpirationAndRaiseEvent()
    {
        // Arrange
        var originalExpiration = DateTime.UtcNow.AddDays(7);
        var share = new ShareAggregateBuilder()
            .WithExpiresAt(originalExpiration)
            .Build();
        share.ClearDomainEvents();

        var newExpiration = DateTime.UtcNow.AddDays(14);

        // Act
        share.Renew(newExpiration, "user-456");

        // Assert
        share.ExpiresAt.Should().Be(newExpiration);
        share.IsExpired.Should().BeFalse();

        share.DomainEvents.Should().ContainSingle();
        var domainEvent = share.DomainEvents.First();
        domainEvent.Should().BeOfType<ShareRenewedEvent>();

        var renewedEvent = (ShareRenewedEvent)domainEvent;
        renewedEvent.Token.Should().Be(share.Id);
        renewedEvent.PreviousExpiresAt.Should().Be(originalExpiration);
        renewedEvent.NewExpiresAt.Should().Be(newExpiration);
        renewedEvent.RenewedBy.Should().Be("user-456");
    }

    [Fact]
    public void Renew_InactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");

        // Act
        Action act = () => share.Renew(DateTime.UtcNow.AddDays(7), "user-456");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot renew an inactive share");
    }

    [Fact]
    public void Renew_ExpiredShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateExpired();

        // Act
        Action act = () => share.Renew(DateTime.UtcNow.AddDays(7), "user-456");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot renew an expired share");
    }

    [Fact]
    public void Renew_WithPastDate_ShouldThrowArgumentException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        Action act = () => share.Renew(DateTime.UtcNow.AddDays(-1), "user-456");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("New expiration date must be in the future*")
            .And.ParamName.Should().Be("newExpiresAt");
    }

    [Fact]
    public void Renew_ToNeverExpire_ShouldSetExpirationToNull()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .ExpiresInDays(7)
            .Build();

        // Act
        share.Renew(null, "user-456");

        // Assert
        share.ExpiresAt.Should().BeNull();
        share.IsExpired.Should().BeFalse();
    }

    #endregion

    #region AddUserComment Tests

    [Fact]
    public void AddUserComment_WithCommentPermission_ShouldAddCommentAndRaiseEvent()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();
        share.ClearDomainEvents();

        // Act
        share.AddUserComment("user-123", "Great map!");

        // Assert
        share.Comments.Should().ContainSingle();
        var comment = share.Comments.First();
        comment.Author.Should().Be("user-123");
        comment.Text.Should().Be("Great map!");
        comment.IsGuest.Should().BeFalse();
        comment.IsApproved.Should().BeTrue();

        share.DomainEvents.Should().ContainSingle();
        var domainEvent = share.DomainEvents.First();
        domainEvent.Should().BeOfType<CommentAddedEvent>();

        var commentEvent = (CommentAddedEvent)domainEvent;
        commentEvent.ShareToken.Should().Be(share.Id);
        commentEvent.CommentId.Should().Be(comment.Id);
        commentEvent.Author.Should().Be("user-123");
        commentEvent.IsGuest.Should().BeFalse();
        commentEvent.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public void AddUserComment_WithEditPermission_ShouldSucceed()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithEditPermission();

        // Act
        share.AddUserComment("user-123", "Editing this map");

        // Assert
        share.Comments.Should().ContainSingle();
    }

    [Fact]
    public void AddUserComment_WithViewPermission_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault(); // View permission

        // Act
        Action act = () => share.AddUserComment("user-123", "Comment");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("View-only shares do not allow commenting");
    }

    [Fact]
    public void AddUserComment_OnInactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();
        share.Deactivate("user-456");

        // Act
        Action act = () => share.AddUserComment("user-123", "Comment");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot comment on an inactive share");
    }

    [Fact]
    public void AddUserComment_OnExpiredShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .Expired()
            .Build();

        // Act
        Action act = () => share.AddUserComment("user-123", "Comment");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot comment on an expired share");
    }

    [Fact]
    public void AddUserComment_WithLocationCoordinates_ShouldStoreLocation()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();

        // Act
        share.AddUserComment("user-123", "Point of interest", locationX: 45.5, locationY: -122.6);

        // Assert
        var comment = share.Comments.First();
        comment.Location.X.Should().Be(45.5);
        comment.Location.Y.Should().Be(-122.6);
    }

    [Fact]
    public void AddUserComment_WithParentId_ShouldCreateThreadedComment()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();
        share.AddUserComment("user-123", "Parent comment");
        var parentComment = share.Comments.First();

        // Act
        share.AddUserComment("user-456", "Reply", parentId: parentComment.Id);

        // Assert
        share.Comments.Should().HaveCount(2);
        var replyComment = share.Comments.Last();
        replyComment.ParentId.Should().Be(parentComment.Id);
    }

    #endregion

    #region AddGuestComment Tests

    [Fact]
    public void AddGuestComment_WithGuestAccessAndCommentPermission_ShouldAddComment()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .WithGuestAccess(true)
            .Build();
        share.ClearDomainEvents();

        // Act
        share.AddGuestComment(
            guestName: "John Doe",
            guestEmail: "john@example.com",
            text: "Nice work!",
            ipAddress: "192.168.1.1",
            userAgent: "Mozilla/5.0");

        // Assert
        share.Comments.Should().ContainSingle();
        var comment = share.Comments.First();
        comment.Author.Should().Be("John Doe");
        comment.Text.Should().Be("Nice work!");
        comment.IsGuest.Should().BeTrue();
        comment.IsApproved.Should().BeFalse(); // Guest comments require approval
        comment.GuestEmail.Should().Be("john@example.com");
        comment.IpAddress.Should().Be("192.168.1.1");
        comment.UserAgent.Should().Be("Mozilla/5.0");

        var commentEvent = (CommentAddedEvent)share.DomainEvents.First();
        commentEvent.IsGuest.Should().BeTrue();
        commentEvent.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void AddGuestComment_WithoutGuestAccess_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .WithGuestAccess(false)
            .Build();

        // Act
        Action act = () => share.AddGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Guest access is not allowed for this share");
    }

    [Fact]
    public void AddGuestComment_WithViewPermission_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.View)
            .WithGuestAccess(true)
            .Build();

        // Act
        Action act = () => share.AddGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("View-only shares do not allow commenting");
    }

    [Fact]
    public void AddGuestComment_OnInactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .WithGuestAccess(true)
            .Build();
        share.Deactivate("user-456");

        // Act
        Action act = () => share.AddGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot comment on an inactive share");
    }

    [Fact]
    public void AddGuestComment_OnExpiredShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .WithGuestAccess(true)
            .Expired()
            .Build();

        // Act
        Action act = () => share.AddGuestComment(
            "Guest",
            "guest@example.com",
            "Comment",
            "192.168.1.1",
            "Mozilla");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot comment on an expired share");
    }

    #endregion

    #region ValidatePassword Tests

    [Fact]
    public void ValidatePassword_WithoutPasswordProtection_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        var result = share.ValidatePassword("any-password");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidatePassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secure-password");

        // Act
        var result = share.ValidatePassword("secure-password");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidatePassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secure-password");

        // Act
        var result = share.ValidatePassword("wrong-password");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ChangePermission Tests

    [Fact]
    public void ChangePermission_OnActiveShare_ShouldUpdatePermission()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        share.ChangePermission(SharePermission.Edit);

        // Assert
        share.Permission.Should().Be(SharePermission.Edit);
    }

    [Fact]
    public void ChangePermission_OnInactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");

        // Act
        Action act = () => share.ChangePermission(SharePermission.Edit);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot change permission on an inactive share");
    }

    #endregion

    #region RecordAccess Tests

    [Fact]
    public void RecordAccess_ShouldIncrementAccessCount()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        var initialCount = share.AccessCount;

        // Act
        share.RecordAccess();

        // Assert
        share.AccessCount.Should().Be(initialCount + 1);
        share.LastAccessedAt.Should().NotBeNull();
        share.LastAccessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordAccess_MultipleAccesses_ShouldIncrementEachTime()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        share.RecordAccess();
        share.RecordAccess();
        share.RecordAccess();

        // Assert
        share.AccessCount.Should().Be(3);
    }

    [Fact]
    public void RecordAccess_ShouldUpdateLastAccessedTime()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.RecordAccess();
        var firstAccessTime = share.LastAccessedAt;

        System.Threading.Thread.Sleep(10); // Small delay

        // Act
        share.RecordAccess();

        // Assert
        share.LastAccessedAt.Should().BeAfter(firstAccessTime!.Value);
    }

    #endregion

    #region UpdateConfiguration Tests

    [Fact]
    public void UpdateConfiguration_OnActiveShare_ShouldUpdateConfiguration()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        var newConfig = ShareConfiguration.Create("1024px", "768px");

        // Act
        share.UpdateConfiguration(newConfig);

        // Assert
        share.Configuration.Should().Be(newConfig);
    }

    [Fact]
    public void UpdateConfiguration_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        Action act = () => share.UpdateConfiguration(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("newConfiguration");
    }

    [Fact]
    public void UpdateConfiguration_OnInactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");
        var newConfig = ShareConfiguration.Create("1024px", "768px");

        // Act
        Action act = () => share.UpdateConfiguration(newConfig);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot update configuration on an inactive share");
    }

    #endregion

    #region SetPassword Tests

    [Fact]
    public void SetPassword_OnActiveShare_ShouldSetPassword()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        var password = SharePassword.Create("new-password");

        // Act
        share.SetPassword(password);

        // Assert
        share.Password.Should().Be(password);
        share.IsPasswordProtected.Should().BeTrue();
    }

    [Fact]
    public void SetPassword_ToNull_ShouldRemovePasswordProtection()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected();

        // Act
        share.SetPassword(null);

        // Assert
        share.Password.Should().BeNull();
        share.IsPasswordProtected.Should().BeFalse();
    }

    [Fact]
    public void SetPassword_OnInactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");
        var password = SharePassword.Create("new-password");

        // Act
        Action act = () => share.SetPassword(password);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot change password on an inactive share");
    }

    #endregion

    #region Property Tests

    [Fact]
    public void IsExpired_WhenNoExpiration_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act & Assert
        share.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiresInFuture_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .ExpiresInDays(7)
            .Build();

        // Act & Assert
        share.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpired_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateExpired();

        // Act & Assert
        share.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenActiveAndNotExpired_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act & Assert
        share.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenInactive_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");

        // Act & Assert
        share.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateExpired();

        // Act & Assert
        share.IsValid.Should().BeFalse();
    }

    #endregion
}
