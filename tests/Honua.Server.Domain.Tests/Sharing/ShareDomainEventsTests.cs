// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain.Sharing;
using Honua.Server.Core.Domain.Sharing.Events;
using Xunit;

namespace Honua.Server.Domain.Tests.Sharing;

[Trait("Category", "Unit")]
public sealed class ShareDomainEventsTests
{
    #region ShareCreatedEvent Tests

    [Fact]
    public void ShareCreatedEvent_ShouldContainAllRequiredProperties()
    {
        // Arrange
        var token = "test-token";
        var mapId = "map-123";
        var createdBy = "user-456";
        var permission = SharePermission.Comment;
        var allowGuestAccess = true;
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var isPasswordProtected = true;

        // Act
        var evt = new ShareCreatedEvent(
            token,
            mapId,
            createdBy,
            permission,
            allowGuestAccess,
            expiresAt,
            isPasswordProtected);

        // Assert
        evt.Should().NotBeNull();
        evt.Token.Should().Be(token);
        evt.MapId.Should().Be(mapId);
        evt.CreatedBy.Should().Be(createdBy);
        evt.Permission.Should().Be(permission);
        evt.AllowGuestAccess.Should().Be(allowGuestAccess);
        evt.ExpiresAt.Should().Be(expiresAt);
        evt.IsPasswordProtected.Should().Be(isPasswordProtected);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ShareCreatedEvent_WithNullCreatedBy_ShouldSucceed()
    {
        // Arrange & Act
        var evt = new ShareCreatedEvent(
            "token",
            "map-id",
            null,
            SharePermission.View,
            true,
            null,
            false);

        // Assert
        evt.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void ShareCreatedEvent_WithNullExpiresAt_ShouldSucceed()
    {
        // Arrange & Act
        var evt = new ShareCreatedEvent(
            "token",
            "map-id",
            "user-123",
            SharePermission.View,
            true,
            null,
            false);

        // Assert
        evt.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ShareCreatedEvent_MultipleInstances_ShouldHaveUniqueEventIds()
    {
        // Arrange & Act
        var evt1 = new ShareCreatedEvent("t1", "m1", "u1", SharePermission.View, true, null, false);
        var evt2 = new ShareCreatedEvent("t2", "m2", "u2", SharePermission.View, true, null, false);

        // Assert
        evt1.EventId.Should().NotBe(evt2.EventId);
    }

    #endregion

    #region ShareDeactivatedEvent Tests

    [Fact]
    public void ShareDeactivatedEvent_ShouldContainAllRequiredProperties()
    {
        // Arrange
        var token = "test-token";
        var mapId = "map-123";
        var deactivatedBy = "user-456";
        var reason = "No longer needed";

        // Act
        var evt = new ShareDeactivatedEvent(
            token,
            mapId,
            deactivatedBy,
            reason);

        // Assert
        evt.Should().NotBeNull();
        evt.Token.Should().Be(token);
        evt.MapId.Should().Be(mapId);
        evt.DeactivatedBy.Should().Be(deactivatedBy);
        evt.Reason.Should().Be(reason);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ShareDeactivatedEvent_WithNullDeactivatedBy_ShouldSucceed()
    {
        // Arrange & Act
        var evt = new ShareDeactivatedEvent(
            "token",
            "map-id",
            null,
            "reason");

        // Assert
        evt.DeactivatedBy.Should().BeNull();
    }

    [Fact]
    public void ShareDeactivatedEvent_WithNullReason_ShouldSucceed()
    {
        // Arrange & Act
        var evt = new ShareDeactivatedEvent(
            "token",
            "map-id",
            "user-123",
            null);

        // Assert
        evt.Reason.Should().BeNull();
    }

    [Fact]
    public void ShareDeactivatedEvent_MultipleInstances_ShouldHaveUniqueEventIds()
    {
        // Arrange & Act
        var evt1 = new ShareDeactivatedEvent("t1", "m1", "u1", null);
        var evt2 = new ShareDeactivatedEvent("t2", "m2", "u2", null);

        // Assert
        evt1.EventId.Should().NotBe(evt2.EventId);
    }

    #endregion

    #region ShareRenewedEvent Tests

    [Fact]
    public void ShareRenewedEvent_ShouldContainAllRequiredProperties()
    {
        // Arrange
        var token = "test-token";
        var mapId = "map-123";
        var previousExpiresAt = DateTime.UtcNow.AddDays(7);
        var newExpiresAt = DateTime.UtcNow.AddDays(14);
        var renewedBy = "user-456";

        // Act
        var evt = new ShareRenewedEvent(
            token,
            mapId,
            previousExpiresAt,
            newExpiresAt,
            renewedBy);

        // Assert
        evt.Should().NotBeNull();
        evt.Token.Should().Be(token);
        evt.MapId.Should().Be(mapId);
        evt.PreviousExpiresAt.Should().Be(previousExpiresAt);
        evt.NewExpiresAt.Should().Be(newExpiresAt);
        evt.RenewedBy.Should().Be(renewedBy);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ShareRenewedEvent_WithNullPreviousExpiresAt_ShouldSucceed()
    {
        // Arrange & Act
        var evt = new ShareRenewedEvent(
            "token",
            "map-id",
            null,
            DateTime.UtcNow.AddDays(7),
            "user-123");

        // Assert
        evt.PreviousExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ShareRenewedEvent_WithNullNewExpiresAt_ShouldSucceed()
    {
        // Arrange & Act
        var evt = new ShareRenewedEvent(
            "token",
            "map-id",
            DateTime.UtcNow.AddDays(7),
            null,
            "user-123");

        // Assert
        evt.NewExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ShareRenewedEvent_WithNullRenewedBy_ShouldSucceed()
    {
        // Arrange & Act
        var evt = new ShareRenewedEvent(
            "token",
            "map-id",
            null,
            DateTime.UtcNow.AddDays(7),
            null);

        // Assert
        evt.RenewedBy.Should().BeNull();
    }

    [Fact]
    public void ShareRenewedEvent_MultipleInstances_ShouldHaveUniqueEventIds()
    {
        // Arrange & Act
        var evt1 = new ShareRenewedEvent("t1", "m1", null, null, "u1");
        var evt2 = new ShareRenewedEvent("t2", "m2", null, null, "u2");

        // Assert
        evt1.EventId.Should().NotBe(evt2.EventId);
    }

    #endregion

    #region CommentAddedEvent Tests

    [Fact]
    public void CommentAddedEvent_ShouldContainAllRequiredProperties()
    {
        // Arrange
        var shareToken = "share-token";
        var mapId = "map-123";
        var commentId = "comment-456";
        var author = "user-789";
        var isGuest = false;
        var text = "Great work!";
        var requiresApproval = false;

        // Act
        var evt = new CommentAddedEvent(
            shareToken,
            mapId,
            commentId,
            author,
            isGuest,
            text,
            requiresApproval);

        // Assert
        evt.Should().NotBeNull();
        evt.ShareToken.Should().Be(shareToken);
        evt.MapId.Should().Be(mapId);
        evt.CommentId.Should().Be(commentId);
        evt.Author.Should().Be(author);
        evt.IsGuest.Should().Be(isGuest);
        evt.Text.Should().Be(text);
        evt.RequiresApproval.Should().Be(requiresApproval);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CommentAddedEvent_ForUserComment_ShouldHaveCorrectFlags()
    {
        // Arrange & Act
        var evt = new CommentAddedEvent(
            "share-token",
            "map-id",
            "comment-id",
            "user-123",
            isGuest: false,
            "Comment text",
            requiresApproval: false);

        // Assert
        evt.IsGuest.Should().BeFalse();
        evt.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public void CommentAddedEvent_ForGuestComment_ShouldHaveCorrectFlags()
    {
        // Arrange & Act
        var evt = new CommentAddedEvent(
            "share-token",
            "map-id",
            "comment-id",
            "Guest User",
            isGuest: true,
            "Guest comment",
            requiresApproval: true);

        // Assert
        evt.IsGuest.Should().BeTrue();
        evt.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void CommentAddedEvent_MultipleInstances_ShouldHaveUniqueEventIds()
    {
        // Arrange & Act
        var evt1 = new CommentAddedEvent("t1", "m1", "c1", "a1", false, "text1", false);
        var evt2 = new CommentAddedEvent("t2", "m2", "c2", "a2", false, "text2", false);

        // Assert
        evt1.EventId.Should().NotBe(evt2.EventId);
    }

    #endregion

    #region Integration Tests - Events Raised by Aggregate

    [Fact]
    public void ShareAggregate_Create_ShouldRaiseShareCreatedEvent()
    {
        // Arrange & Act
        var share = ShareAggregate.Create(
            "map-123",
            "user-456",
            SharePermission.View);

        // Assert
        share.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShareCreatedEvent>();
    }

    [Fact]
    public void ShareAggregate_Deactivate_ShouldRaiseShareDeactivatedEvent()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.ClearDomainEvents();

        // Act
        share.Deactivate("user-456", "Test reason");

        // Assert
        share.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShareDeactivatedEvent>();
    }

    [Fact]
    public void ShareAggregate_Renew_ShouldRaiseShareRenewedEvent()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.ClearDomainEvents();

        // Act
        share.Renew(DateTime.UtcNow.AddDays(14), "user-456");

        // Assert
        share.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShareRenewedEvent>();
    }

    [Fact]
    public void ShareAggregate_AddUserComment_ShouldRaiseCommentAddedEvent()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();
        share.ClearDomainEvents();

        // Act
        share.AddUserComment("user-123", "Great work!");

        // Assert
        share.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CommentAddedEvent>();
    }

    [Fact]
    public void ShareAggregate_AddGuestComment_ShouldRaiseCommentAddedEvent()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .WithGuestAccess(true)
            .Build();
        share.ClearDomainEvents();

        // Act
        share.AddGuestComment(
            "Guest",
            "guest@example.com",
            "Nice map!",
            "192.168.1.1",
            "Mozilla");

        // Assert
        share.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CommentAddedEvent>();
    }

    [Fact]
    public void ShareAggregate_MultipleActions_ShouldAccumulateDomainEvents()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .Build();

        // Act
        share.AddUserComment("user-123", "Comment 1");
        share.AddUserComment("user-456", "Comment 2");
        share.Renew(DateTime.UtcNow.AddDays(7), "user-123");

        // Assert
        share.DomainEvents.Should().HaveCount(4); // 1 creation + 2 comments + 1 renewal
        share.DomainEvents.Should().ContainSingle(e => e is ShareCreatedEvent);
        share.DomainEvents.Should().Contain(e => e is CommentAddedEvent)
            .Which.Should().HaveCount(2);
        share.DomainEvents.Should().ContainSingle(e => e is ShareRenewedEvent);
    }

    [Fact]
    public void ShareAggregate_ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.AddUserComment("user-123", "Comment");
        share.DomainEvents.Should().NotBeEmpty();

        // Act
        share.ClearDomainEvents();

        // Assert
        share.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Event Property Tests

    [Fact]
    public void DomainEvents_ShouldHaveUniqueEventIds()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();
        share.AddUserComment("user-123", "Comment 1");
        share.AddUserComment("user-456", "Comment 2");

        // Act
        var eventIds = share.DomainEvents.Select(e => e.EventId).ToList();

        // Assert
        eventIds.Should().OnlyHaveUniqueItems();
        eventIds.Should().AllSatisfy(id => id.Should().NotBeEmpty());
    }

    [Fact]
    public void DomainEvents_ShouldHaveOccurredOnTimestamp()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        var events = share.DomainEvents;

        // Assert
        events.Should().AllSatisfy(evt =>
        {
            evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        });
    }

    #endregion
}
