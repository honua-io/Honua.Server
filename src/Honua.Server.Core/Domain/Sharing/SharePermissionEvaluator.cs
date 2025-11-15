// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.Sharing;

/// <summary>
/// Domain service for evaluating share permissions and access rights.
/// Encapsulates complex permission logic that involves multiple aggregates or external context.
/// </summary>
public sealed class SharePermissionEvaluator
{
    /// <summary>
    /// Determines whether a user can access a share.
    /// </summary>
    /// <param name="share">The share to check</param>
    /// <param name="userId">The user ID (null for guest)</param>
    /// <param name="providedPassword">The password provided (if any)</param>
    /// <returns>True if access is allowed, false otherwise</returns>
    public bool CanAccess(ShareAggregate share, string? userId, string? providedPassword)
    {
        if (share == null)
            throw new ArgumentNullException(nameof(share));

        // Check basic validity
        if (!share.IsValid)
            return false;

        // Check if user is the owner (always allowed)
        if (!string.IsNullOrEmpty(userId) && userId == share.CreatedBy)
            return true;

        // Check guest access
        if (string.IsNullOrEmpty(userId) && !share.AllowGuestAccess)
            return false;

        // Check password protection
        if (share.IsPasswordProtected)
        {
            if (string.IsNullOrEmpty(providedPassword))
                return false;

            if (!share.ValidatePassword(providedPassword))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a user can comment on a share.
    /// </summary>
    /// <param name="share">The share to check</param>
    /// <param name="userId">The user ID (null for guest)</param>
    /// <param name="providedPassword">The password provided (if any)</param>
    /// <returns>True if commenting is allowed, false otherwise</returns>
    public bool CanComment(ShareAggregate share, string? userId, string? providedPassword)
    {
        if (share == null)
            throw new ArgumentNullException(nameof(share));

        // Must have access first
        if (!CanAccess(share, userId, providedPassword))
            return false;

        // Check permission level
        if (share.Permission == SharePermission.View)
            return false;

        return true;
    }

    /// <summary>
    /// Determines whether a user can edit through a share.
    /// </summary>
    /// <param name="share">The share to check</param>
    /// <param name="userId">The user ID (null for guest)</param>
    /// <param name="providedPassword">The password provided (if any)</param>
    /// <returns>True if editing is allowed, false otherwise</returns>
    public bool CanEdit(ShareAggregate share, string? userId, string? providedPassword)
    {
        if (share == null)
            throw new ArgumentNullException(nameof(share));

        // Must have access first
        if (!CanAccess(share, userId, providedPassword))
            return false;

        // Check permission level
        if (share.Permission != SharePermission.Edit)
            return false;

        return true;
    }

    /// <summary>
    /// Determines whether a user can manage a share (deactivate, renew, change permissions).
    /// Only the share owner can manage it.
    /// </summary>
    /// <param name="share">The share to check</param>
    /// <param name="userId">The user ID</param>
    /// <returns>True if management is allowed, false otherwise</returns>
    public bool CanManage(ShareAggregate share, string? userId)
    {
        if (share == null)
            throw new ArgumentNullException(nameof(share));

        // Must be authenticated
        if (string.IsNullOrEmpty(userId))
            return false;

        // Must be the owner
        return userId == share.CreatedBy;
    }

    /// <summary>
    /// Determines whether a user can approve comments on a share.
    /// Only the share owner can approve comments.
    /// </summary>
    /// <param name="share">The share to check</param>
    /// <param name="userId">The user ID</param>
    /// <returns>True if comment approval is allowed, false otherwise</returns>
    public bool CanApproveComments(ShareAggregate share, string? userId)
    {
        // Same as management - only owner can approve
        return CanManage(share, userId);
    }

    /// <summary>
    /// Determines the effective permission level for a user on a share.
    /// </summary>
    /// <param name="share">The share to check</param>
    /// <param name="userId">The user ID (null for guest)</param>
    /// <param name="providedPassword">The password provided (if any)</param>
    /// <returns>The effective permission level, or null if no access</returns>
    public SharePermission? GetEffectivePermission(ShareAggregate share, string? userId, string? providedPassword)
    {
        if (share == null)
            throw new ArgumentNullException(nameof(share));

        // Check if user can access
        if (!CanAccess(share, userId, providedPassword))
            return null;

        // Owner gets edit permission regardless of share permission
        if (!string.IsNullOrEmpty(userId) && userId == share.CreatedBy)
            return SharePermission.Edit;

        // Return the share's permission level
        return share.Permission;
    }

    /// <summary>
    /// Validates that a share access request is valid.
    /// Throws descriptive exceptions for different failure scenarios.
    /// </summary>
    /// <param name="share">The share to validate</param>
    /// <param name="userId">The user ID (null for guest)</param>
    /// <param name="providedPassword">The password provided (if any)</param>
    /// <exception cref="InvalidOperationException">Thrown when access is not allowed</exception>
    public void ValidateAccess(ShareAggregate share, string? userId, string? providedPassword)
    {
        if (share == null)
            throw new ArgumentNullException(nameof(share));

        if (!share.IsActive)
            throw new InvalidOperationException("This share has been deactivated");

        if (share.IsExpired)
            throw new InvalidOperationException("This share has expired");

        if (string.IsNullOrEmpty(userId) && !share.AllowGuestAccess)
            throw new InvalidOperationException("Guest access is not allowed for this share");

        if (share.IsPasswordProtected)
        {
            if (string.IsNullOrEmpty(providedPassword))
                throw new InvalidOperationException("This share is password protected");

            if (!share.ValidatePassword(providedPassword))
                throw new InvalidOperationException("Invalid password");
        }
    }

    /// <summary>
    /// Checks if a user has elevated permissions (comment or edit) on a share.
    /// </summary>
    /// <param name="share">The share to check</param>
    /// <param name="userId">The user ID (null for guest)</param>
    /// <param name="providedPassword">The password provided (if any)</param>
    /// <returns>True if user has comment or edit permission, false otherwise</returns>
    public bool HasElevatedPermissions(ShareAggregate share, string? userId, string? providedPassword)
    {
        var effectivePermission = GetEffectivePermission(share, userId, providedPassword);
        return effectivePermission == SharePermission.Comment || effectivePermission == SharePermission.Edit;
    }
}
