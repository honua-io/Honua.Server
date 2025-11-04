using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.Wfs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

/// <summary>
/// Tests for WFS lock ownership validation functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "WFS")]
[Trait("Speed", "Fast")]
public class WfsLockTests
{
    private readonly InMemoryWfsLockManager _lockManager;

    public WfsLockTests()
    {
        _lockManager = new InMemoryWfsLockManager();
    }

    [Fact]
    public async Task UserCanReleaseTheirOwnLock()
    {
        // Arrange
        var owner = "user1";
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        var acquireResult = await _lockManager.TryAcquireAsync(
            owner,
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        acquireResult.Success.Should().BeTrue();
        acquireResult.Lock.Should().NotBeNull();
        var lockId = acquireResult.Lock!.LockId;

        // Act - User releases their own lock
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            owner,
            lockId,
            null,
            CancellationToken.None);

        // Assert - Should succeed without exception
        await releaseAction.Should().NotThrowAsync();

        // Verify lock is released by attempting to acquire again
        var secondAcquire = await _lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        secondAcquire.Success.Should().BeTrue("the lock should have been released");
    }

    [Fact]
    public async Task UserCannotReleaseAnotherUsersLock()
    {
        // Arrange
        var owner = "user1";
        var otherUser = "user2";
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature2") };

        var acquireResult = await _lockManager.TryAcquireAsync(
            owner,
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        acquireResult.Success.Should().BeTrue();
        acquireResult.Lock.Should().NotBeNull();
        var lockId = acquireResult.Lock!.LockId;

        // Act - Different user attempts to release the lock
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            otherUser,
            lockId,
            null,
            CancellationToken.None);

        // Assert - Should throw UnauthorizedAccessException
        await releaseAction.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"Lock {lockId} is owned by {owner}, cannot be released by {otherUser}");

        // Verify lock is still held
        var validationResult = await _lockManager.ValidateAsync(lockId, targets, CancellationToken.None);
        validationResult.Success.Should().BeTrue("the lock should still be held");
    }

    [Fact]
    public async Task AdminCanReleaseAnyLock()
    {
        // Arrange
        var owner = "user1";
        var admin = "admin1";
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature3") };

        var acquireResult = await _lockManager.TryAcquireAsync(
            owner,
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        acquireResult.Success.Should().BeTrue();
        acquireResult.Lock.Should().NotBeNull();
        var lockId = acquireResult.Lock!.LockId;

        // Act - Admin releases another user's lock
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            admin,
            lockId,
            null,
            CancellationToken.None);

        // Assert - Should succeed without exception
        await releaseAction.Should().NotThrowAsync();

        // Verify lock is released
        var secondAcquire = await _lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        secondAcquire.Success.Should().BeTrue("admin should have been able to release the lock");
    }

    [Fact]
    public async Task PartialRelease_ValidatesOwnership()
    {
        // Arrange
        var owner = "user1";
        var otherUser = "user2";
        var target1 = new WfsLockTarget("service1", "layer1", "feature4a");
        var target2 = new WfsLockTarget("service1", "layer1", "feature4b");
        var targets = new[] { target1, target2 };

        var acquireResult = await _lockManager.TryAcquireAsync(
            owner,
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        acquireResult.Success.Should().BeTrue();
        acquireResult.Lock.Should().NotBeNull();
        var lockId = acquireResult.Lock!.LockId;

        // Act - Different user attempts partial release
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            otherUser,
            lockId,
            new[] { target1 },
            CancellationToken.None);

        // Assert - Should throw UnauthorizedAccessException
        await releaseAction.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"Lock {lockId} is owned by {owner}, cannot be released by {otherUser}");

        // Verify both targets are still locked
        var validationResult = await _lockManager.ValidateAsync(lockId, targets, CancellationToken.None);
        validationResult.Success.Should().BeTrue("the lock should still be held");
    }

    [Fact]
    public async Task LockWithoutOwner_CanBeReleasedByAnyone_BackwardCompatibility()
    {
        // This test simulates backward compatibility with locks that don't have owner info
        // In practice, this scenario shouldn't occur since we always set owner now,
        // but we test the behavior for robustness

        // Arrange
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature5") };

        // Acquire a lock with empty owner (simulating legacy scenario)
        var acquireResult = await _lockManager.TryAcquireAsync(
            "",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        acquireResult.Success.Should().BeTrue();
        acquireResult.Lock.Should().NotBeNull();
        var lockId = acquireResult.Lock!.LockId;

        // Act - Any user should be able to release a lock without owner
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            "anyuser",
            lockId,
            null,
            CancellationToken.None);

        // Assert - Should succeed without exception
        await releaseAction.Should().NotThrowAsync();

        // Verify lock is released
        var secondAcquire = await _lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        secondAcquire.Success.Should().BeTrue("lock without owner should have been releasable");
    }

    [Fact]
    public async Task OwnershipValidation_IsCaseInsensitive()
    {
        // Arrange
        var owner = "User1";
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature6") };

        var acquireResult = await _lockManager.TryAcquireAsync(
            owner,
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        acquireResult.Success.Should().BeTrue();
        acquireResult.Lock.Should().NotBeNull();
        var lockId = acquireResult.Lock!.LockId;

        // Act - User releases with different casing
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            "user1",  // Different casing from "User1"
            lockId,
            null,
            CancellationToken.None);

        // Assert - Should succeed without exception (case-insensitive comparison)
        await releaseAction.Should().NotThrowAsync();

        // Verify lock is released
        var secondAcquire = await _lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        secondAcquire.Success.Should().BeTrue("owner name comparison should be case-insensitive");
    }

    [Fact]
    public async Task MultipleUsers_CannotReleaseEachOthersLocks()
    {
        // Arrange
        var user1 = "user1";
        var user2 = "user2";
        var user3 = "user3";

        var targets1 = new[] { new WfsLockTarget("service1", "layer1", "feature7") };
        var targets2 = new[] { new WfsLockTarget("service1", "layer1", "feature8") };

        var acquire1 = await _lockManager.TryAcquireAsync(
            user1,
            TimeSpan.FromMinutes(5),
            targets1,
            CancellationToken.None);

        var acquire2 = await _lockManager.TryAcquireAsync(
            user2,
            TimeSpan.FromMinutes(5),
            targets2,
            CancellationToken.None);

        acquire1.Success.Should().BeTrue();
        acquire2.Success.Should().BeTrue();

        var lockId1 = acquire1.Lock!.LockId;
        var lockId2 = acquire2.Lock!.LockId;

        // Act & Assert - user2 cannot release user1's lock
        var release1ByUser2 = async () => await _lockManager.ReleaseAsync(
            user2,
            lockId1,
            null,
            CancellationToken.None);

        await release1ByUser2.Should().ThrowAsync<UnauthorizedAccessException>();

        // Act & Assert - user3 cannot release user1's lock
        var release1ByUser3 = async () => await _lockManager.ReleaseAsync(
            user3,
            lockId1,
            null,
            CancellationToken.None);

        await release1ByUser3.Should().ThrowAsync<UnauthorizedAccessException>();

        // Act & Assert - user1 cannot release user2's lock
        var release2ByUser1 = async () => await _lockManager.ReleaseAsync(
            user1,
            lockId2,
            null,
            CancellationToken.None);

        await release2ByUser1.Should().ThrowAsync<UnauthorizedAccessException>();

        // But each user can release their own lock
        await _lockManager.ReleaseAsync(user1, lockId1, null, CancellationToken.None);
        await _lockManager.ReleaseAsync(user2, lockId2, null, CancellationToken.None);

        // Verify both locks are released
        var reacquire1 = await _lockManager.TryAcquireAsync(
            user3,
            TimeSpan.FromMinutes(5),
            targets1,
            CancellationToken.None);

        var reacquire2 = await _lockManager.TryAcquireAsync(
            user3,
            TimeSpan.FromMinutes(5),
            targets2,
            CancellationToken.None);

        reacquire1.Success.Should().BeTrue();
        reacquire2.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AdminOverride_WorksForMultipleUsers()
    {
        // Arrange
        var admin = "admin1";
        var user1 = "user1";
        var user2 = "user2";

        var targets1 = new[] { new WfsLockTarget("service1", "layer1", "feature9") };
        var targets2 = new[] { new WfsLockTarget("service1", "layer1", "feature10") };

        var acquire1 = await _lockManager.TryAcquireAsync(
            user1,
            TimeSpan.FromMinutes(5),
            targets1,
            CancellationToken.None);

        var acquire2 = await _lockManager.TryAcquireAsync(
            user2,
            TimeSpan.FromMinutes(5),
            targets2,
            CancellationToken.None);

        acquire1.Success.Should().BeTrue();
        acquire2.Success.Should().BeTrue();

        var lockId1 = acquire1.Lock!.LockId;
        var lockId2 = acquire2.Lock!.LockId;

        // Act - Admin releases both locks
        await _lockManager.ReleaseAsync(admin, lockId1, null, CancellationToken.None);
        await _lockManager.ReleaseAsync(admin, lockId2, null, CancellationToken.None);

        // Assert - Both locks should be released
        var reacquire1 = await _lockManager.TryAcquireAsync(
            user1,
            TimeSpan.FromMinutes(5),
            targets1,
            CancellationToken.None);

        var reacquire2 = await _lockManager.TryAcquireAsync(
            user2,
            TimeSpan.FromMinutes(5),
            targets2,
            CancellationToken.None);

        reacquire1.Success.Should().BeTrue("admin should be able to release any lock");
        reacquire2.Success.Should().BeTrue("admin should be able to release any lock");
    }

    [Fact]
    public async Task NonExistentLock_DoesNotThrowOnRelease()
    {
        // Arrange
        var nonExistentLockId = Guid.NewGuid().ToString("N");

        // Act
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            "someuser",
            nonExistentLockId,
            null,
            CancellationToken.None);

        // Assert - Should not throw (gracefully handles missing lock)
        await releaseAction.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EmptyLockId_DoesNotThrowOnRelease()
    {
        // Act
        var releaseAction = async () => await _lockManager.ReleaseAsync(
            "someuser",
            "",
            null,
            CancellationToken.None);

        // Assert - Should not throw (gracefully handles empty lock ID)
        await releaseAction.Should().NotThrowAsync();
    }
}
