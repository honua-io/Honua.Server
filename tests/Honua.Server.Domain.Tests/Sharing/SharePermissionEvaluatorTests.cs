// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain.Sharing;
using Xunit;

namespace Honua.Server.Domain.Tests.Sharing;

[Trait("Category", "Unit")]
public sealed class SharePermissionEvaluatorTests
{
    private readonly SharePermissionEvaluator _evaluator = new();

    #region CanAccess Tests

    [Fact]
    public void CanAccess_ActiveShareWithoutPassword_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        var result = _evaluator.CanAccess(share, "user-123", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccess_InactiveShare_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");

        // Act
        var result = _evaluator.CanAccess(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccess_ExpiredShare_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateExpired();

        // Act
        var result = _evaluator.CanAccess(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccess_OwnerAlwaysHasAccess_EvenIfExpired()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .Expired()
            .Build();

        // Act
        var result = _evaluator.CanAccess(share, "owner-123", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccess_OwnerAlwaysHasAccess_EvenIfInactive()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .Build();
        share.Deactivate("owner-123");

        // Act
        var result = _evaluator.CanAccess(share, "owner-123", null);

        // Assert
        result.Should().BeFalse(); // Even owners can't access inactive shares
    }

    [Fact]
    public void CanAccess_GuestWithGuestAccessAllowed_ShouldReturnTrue()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithGuestAccess(true)
            .Build();

        // Act
        var result = _evaluator.CanAccess(share, null, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccess_GuestWithGuestAccessDisallowed_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithGuestAccess(false)
            .Build();

        // Act
        var result = _evaluator.CanAccess(share, null, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccess_PasswordProtected_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secret-password");

        // Act
        var result = _evaluator.CanAccess(share, "user-123", "secret-password");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccess_PasswordProtected_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secret-password");

        // Act
        var result = _evaluator.CanAccess(share, "user-123", "wrong-password");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccess_PasswordProtected_WithoutPassword_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secret-password");

        // Act
        var result = _evaluator.CanAccess(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccess_WithNullShare_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => _evaluator.CanAccess(null!, "user-123", null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("share");
    }

    #endregion

    #region CanComment Tests

    [Fact]
    public void CanComment_WithCommentPermission_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();

        // Act
        var result = _evaluator.CanComment(share, "user-123", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanComment_WithEditPermission_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithEditPermission();

        // Act
        var result = _evaluator.CanComment(share, "user-123", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanComment_WithViewPermission_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault(); // View permission

        // Act
        var result = _evaluator.CanComment(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanComment_OnInactiveShare_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();
        share.Deactivate("user-456");

        // Act
        var result = _evaluator.CanComment(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanComment_OnExpiredShare_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .Expired()
            .Build();

        // Act
        var result = _evaluator.CanComment(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanComment_GuestWithGuestAccess_ShouldReturnTrue()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .WithGuestAccess(true)
            .Build();

        // Act
        var result = _evaluator.CanComment(share, null, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanComment_GuestWithoutGuestAccess_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .WithGuestAccess(false)
            .Build();

        // Act
        var result = _evaluator.CanComment(share, null, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanComment_WithNullShare_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => _evaluator.CanComment(null!, "user-123", null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("share");
    }

    #endregion

    #region CanEdit Tests

    [Fact]
    public void CanEdit_WithEditPermission_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithEditPermission();

        // Act
        var result = _evaluator.CanEdit(share, "user-123", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEdit_WithCommentPermission_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();

        // Act
        var result = _evaluator.CanEdit(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanEdit_WithViewPermission_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault(); // View permission

        // Act
        var result = _evaluator.CanEdit(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanEdit_OnInactiveShare_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithEditPermission();
        share.Deactivate("user-456");

        // Act
        var result = _evaluator.CanEdit(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanEdit_OnExpiredShare_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithPermission(SharePermission.Edit)
            .Expired()
            .Build();

        // Act
        var result = _evaluator.CanEdit(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanEdit_WithNullShare_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => _evaluator.CanEdit(null!, "user-123", null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("share");
    }

    #endregion

    #region CanManage Tests

    [Fact]
    public void CanManage_Owner_ShouldReturnTrue()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .Build();

        // Act
        var result = _evaluator.CanManage(share, "owner-123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanManage_NonOwner_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .Build();

        // Act
        var result = _evaluator.CanManage(share, "other-user");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanManage_Guest_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .Build();

        // Act
        var result = _evaluator.CanManage(share, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanManage_ShareWithNullCreator_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy(null)
            .Build();

        // Act
        var result = _evaluator.CanManage(share, "user-123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanManage_WithNullShare_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => _evaluator.CanManage(null!, "user-123");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("share");
    }

    #endregion

    #region CanApproveComments Tests

    [Fact]
    public void CanApproveComments_Owner_ShouldReturnTrue()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .Build();

        // Act
        var result = _evaluator.CanApproveComments(share, "owner-123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanApproveComments_NonOwner_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .Build();

        // Act
        var result = _evaluator.CanApproveComments(share, "other-user");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetEffectivePermission Tests

    [Fact]
    public void GetEffectivePermission_Owner_ShouldReturnEditPermission()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .WithPermission(SharePermission.View)
            .Build();

        // Act
        var result = _evaluator.GetEffectivePermission(share, "owner-123", null);

        // Assert
        result.Should().Be(SharePermission.Edit);
    }

    [Fact]
    public void GetEffectivePermission_NonOwner_ShouldReturnSharePermission()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithCreatedBy("owner-123")
            .WithPermission(SharePermission.Comment)
            .Build();

        // Act
        var result = _evaluator.GetEffectivePermission(share, "other-user", null);

        // Assert
        result.Should().Be(SharePermission.Comment);
    }

    [Fact]
    public void GetEffectivePermission_InactiveShare_ShouldReturnNull()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");

        // Act
        var result = _evaluator.GetEffectivePermission(share, "user-123", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetEffectivePermission_ExpiredShare_ShouldReturnNull()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateExpired();

        // Act
        var result = _evaluator.GetEffectivePermission(share, "user-123", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetEffectivePermission_GuestWithoutAccess_ShouldReturnNull()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithGuestAccess(false)
            .Build();

        // Act
        var result = _evaluator.GetEffectivePermission(share, null, null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetEffectivePermission_GuestWithAccess_ShouldReturnSharePermission()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithGuestAccess(true)
            .WithPermission(SharePermission.Comment)
            .Build();

        // Act
        var result = _evaluator.GetEffectivePermission(share, null, null);

        // Assert
        result.Should().Be(SharePermission.Comment);
    }

    [Fact]
    public void GetEffectivePermission_WithNullShare_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => _evaluator.GetEffectivePermission(null!, "user-123", null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("share");
    }

    #endregion

    #region ValidateAccess Tests

    [Fact]
    public void ValidateAccess_ValidShare_ShouldNotThrow()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();

        // Act
        Action act = () => _evaluator.ValidateAccess(share, "user-123", null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAccess_InactiveShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault();
        share.Deactivate("user-456");

        // Act
        Action act = () => _evaluator.ValidateAccess(share, "user-123", null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("This share has been deactivated");
    }

    [Fact]
    public void ValidateAccess_ExpiredShare_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateExpired();

        // Act
        Action act = () => _evaluator.ValidateAccess(share, "user-123", null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("This share has expired");
    }

    [Fact]
    public void ValidateAccess_GuestWithoutAccess_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithGuestAccess(false)
            .Build();

        // Act
        Action act = () => _evaluator.ValidateAccess(share, null, null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Guest access is not allowed for this share");
    }

    [Fact]
    public void ValidateAccess_PasswordProtectedWithoutPassword_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secret");

        // Act
        Action act = () => _evaluator.ValidateAccess(share, "user-123", null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("This share is password protected");
    }

    [Fact]
    public void ValidateAccess_PasswordProtectedWithIncorrectPassword_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secret");

        // Act
        Action act = () => _evaluator.ValidateAccess(share, "user-123", "wrong");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Invalid password");
    }

    [Fact]
    public void ValidateAccess_PasswordProtectedWithCorrectPassword_ShouldNotThrow()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreatePasswordProtected("secret");

        // Act
        Action act = () => _evaluator.ValidateAccess(share, "user-123", "secret");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAccess_WithNullShare_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => _evaluator.ValidateAccess(null!, "user-123", null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("share");
    }

    #endregion

    #region HasElevatedPermissions Tests

    [Fact]
    public void HasElevatedPermissions_WithCommentPermission_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithCommentPermission();

        // Act
        var result = _evaluator.HasElevatedPermissions(share, "user-123", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasElevatedPermissions_WithEditPermission_ShouldReturnTrue()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateWithEditPermission();

        // Act
        var result = _evaluator.HasElevatedPermissions(share, "user-123", null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasElevatedPermissions_WithViewPermission_ShouldReturnFalse()
    {
        // Arrange
        var share = ShareAggregateBuilder.CreateDefault(); // View permission

        // Act
        var result = _evaluator.HasElevatedPermissions(share, "user-123", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasElevatedPermissions_WithNoAccess_ShouldReturnFalse()
    {
        // Arrange
        var share = new ShareAggregateBuilder()
            .WithGuestAccess(false)
            .Build();

        // Act
        var result = _evaluator.HasElevatedPermissions(share, null, null);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
