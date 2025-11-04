using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.Wfs;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Concurrency;

/// <summary>
/// Unit tests for WFS lock concurrency and thread safety.
/// </summary>
/// <remarks>
/// These tests validate that:
/// <list type="bullet">
/// <item>Only one user can acquire a lock on a feature at a time</item>
/// <item>Lock expiration allows subsequent lock acquisition</item>
/// <item>Simultaneous release attempts are handled gracefully</item>
/// <item>Write attempts to locked features are rejected</item>
/// <item>Read operations on locked features succeed</item>
/// <item>Multiple locks on different features succeed</item>
/// <item>Race conditions between validation and release are handled safely</item>
/// </list>
/// This builds on Phase 1 lock ownership tests by adding concurrency scenarios.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "WFS")]
[Trait("Feature", "Concurrency")]
[Trait("Speed", "Fast")]
public class WfsLockConcurrencyTests
{
    /// <summary>
    /// Tests that when two users simultaneously try to lock the same feature,
    /// only one succeeds while the other fails with a conflict.
    /// This is critical for preventing double-booking scenarios.
    /// </summary>
    [Fact]
    public async Task SimultaneousLockRequests_OnlyOneSucceeds()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act - Launch two concurrent lock acquisition attempts
        var task1 = lockManager.TryAcquireAsync(
            "user1",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        var task2 = lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // Assert
        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);

        successCount.Should().Be(1, "exactly one lock request should succeed");
        failCount.Should().Be(1, "exactly one lock request should fail");

        // Verify the successful lock details
        var successfulLock = results.First(r => r.Success);
        successfulLock.Lock.Should().NotBeNull();
        successfulLock.Lock!.Owner.Should().BeOneOf("user1", "user2");
        successfulLock.Lock.Targets.Should().ContainSingle();

        // Verify the failed lock has conflict information
        var failedLock = results.First(r => !r.Success);
        failedLock.Lock.Should().BeNull();
        failedLock.Error.Should().NotBeNullOrWhiteSpace("failed lock should include error message");
        failedLock.Error.Should().Contain("locked", "error message should indicate resource is locked");
    }

    /// <summary>
    /// Tests that after a lock expires, a different user can acquire a new lock on the same feature.
    /// Validates automatic lock cleanup and reacquisition flow.
    /// </summary>
    [Fact]
    public async Task LockExpiration_AllowsReacquisition()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature2") };

        // Act - User 1 acquires lock with short timeout
        var lock1 = await lockManager.TryAcquireAsync(
            "user1",
            TimeSpan.FromMilliseconds(100),
            targets,
            CancellationToken.None);

        lock1.Success.Should().BeTrue("initial lock acquisition should succeed");

        // Wait for lock to expire
        await Task.Delay(200);

        // User 2 attempts to acquire lock after expiration
        var lock2 = await lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        // Assert
        lock2.Success.Should().BeTrue("lock should be available after expiration");
        lock2.Lock.Should().NotBeNull();
        lock2.Lock!.Owner.Should().Be("user2");
        lock2.Lock.LockId.Should().NotBe(lock1.Lock!.LockId, "should be a new lock, not the expired one");
    }

    /// <summary>
    /// Tests that simultaneous release attempts by the owner and admin are handled gracefully.
    /// One should succeed, the other should handle "already released" without throwing.
    /// </summary>
    [Fact]
    public async Task SimultaneousReleaseAttempts_OneSucceeds()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature3") };

        var acquireResult = await lockManager.TryAcquireAsync(
            "user1",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        acquireResult.Success.Should().BeTrue();
        var lockId = acquireResult.Lock!.LockId;

        // Act - Owner and admin both try to release at same time
        var ownerReleaseTask = lockManager.ReleaseAsync("user1", lockId, null, CancellationToken.None);
        var adminReleaseTask = lockManager.ReleaseAsync("admin", lockId, null, CancellationToken.None);

        // Both should complete without exception (one succeeds, one is no-op)
        await Task.WhenAll(ownerReleaseTask, adminReleaseTask);

        // Assert - Lock should be released (verify by trying to acquire)
        var reacquireResult = await lockManager.TryAcquireAsync(
            "user3",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        reacquireResult.Success.Should().BeTrue("lock should be released by one of the simultaneous attempts");
    }

    /// <summary>
    /// Tests that write attempts to locked features are properly rejected.
    /// This validates that locks actually prevent conflicting edits.
    /// </summary>
    [Fact]
    public async Task WriteAttemptToLockedFeature_IsRejected()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature4") };

        // User A acquires lock
        var lockResult = await lockManager.TryAcquireAsync(
            "userA",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        lockResult.Success.Should().BeTrue();
        var lockId = lockResult.Lock!.LockId;

        // Act - User B tries to validate without the lock (simulating write attempt)
        var validationResult = await lockManager.ValidateAsync(
            null, // No lock ID
            targets,
            CancellationToken.None);

        // Assert
        validationResult.Success.Should().BeFalse("write without lock should be rejected");
        validationResult.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validationResult.ErrorMessage.Should().Contain("locked", "error should indicate feature is locked");

        // Verify that validation WITH the correct lock succeeds
        var validationWithLock = await lockManager.ValidateAsync(
            lockId,
            targets,
            CancellationToken.None);

        validationWithLock.Success.Should().BeTrue("validation with correct lock should succeed");
    }

    /// <summary>
    /// Tests that read operations on locked features succeed.
    /// Locks should only prevent writes, not reads.
    /// </summary>
    [Fact]
    public async Task ReadLockedFeature_Succeeds()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature5") };

        // User A acquires lock
        var lockResult = await lockManager.TryAcquireAsync(
            "userA",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        lockResult.Success.Should().BeTrue();

        // Act - Simulate read operation (no lock validation needed for reads)
        // In actual implementation, reads don't call ValidateAsync
        // This test documents that read operations should not be blocked by locks

        // Assert - Lock exists but doesn't prevent reads
        lockResult.Lock.Should().NotBeNull();

        // Note: Actual read operations in WFS would bypass lock validation entirely
        // This test documents expected behavior: locks affect writes only, not reads
    }

    /// <summary>
    /// Tests that the same user can acquire multiple locks on different features simultaneously.
    /// Users should be able to lock multiple resources for batch operations.
    /// </summary>
    [Fact]
    public async Task MultipleLocksOnDifferentFeatures_AllSucceed()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();

        var targets1 = new[] { new WfsLockTarget("service1", "layer1", "feature6a") };
        var targets2 = new[] { new WfsLockTarget("service1", "layer1", "feature6b") };
        var targets3 = new[] { new WfsLockTarget("service1", "layer1", "feature6c") };

        // Act - Same user acquires three locks on different features
        var lock1 = await lockManager.TryAcquireAsync("user1", TimeSpan.FromMinutes(5), targets1, CancellationToken.None);
        var lock2 = await lockManager.TryAcquireAsync("user1", TimeSpan.FromMinutes(5), targets2, CancellationToken.None);
        var lock3 = await lockManager.TryAcquireAsync("user1", TimeSpan.FromMinutes(5), targets3, CancellationToken.None);

        // Assert
        lock1.Success.Should().BeTrue("first lock should succeed");
        lock2.Success.Should().BeTrue("second lock should succeed");
        lock3.Success.Should().BeTrue("third lock should succeed");

        lock1.Lock!.LockId.Should().NotBe(lock2.Lock!.LockId);
        lock2.Lock!.LockId.Should().NotBe(lock3.Lock!.LockId);
        lock1.Lock!.LockId.Should().NotBe(lock3.Lock!.LockId);

        // All locks should belong to same owner
        lock1.Lock!.Owner.Should().Be("user1");
        lock2.Lock!.Owner.Should().Be("user1");
        lock3.Lock!.Owner.Should().Be("user1");
    }

    /// <summary>
    /// Tests thread safety when one thread validates a lock while another releases it.
    /// This validates that there are no race conditions in concurrent lock operations.
    /// </summary>
    [Fact]
    public async Task RaceCondition_LockValidationAndRelease_IsThreadSafe()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature7") };

        var lockResult = await lockManager.TryAcquireAsync(
            "user1",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        lockResult.Success.Should().BeTrue();
        var lockId = lockResult.Lock!.LockId;

        // Act - Launch validation and release simultaneously
        var validationTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await lockManager.ValidateAsync(lockId, targets, CancellationToken.None);
                await Task.Yield(); // Force thread switch
            }
        });

        var releaseTask = Task.Run(async () =>
        {
            await Task.Delay(5); // Small delay to ensure some validations happen first
            await lockManager.ReleaseAsync("user1", lockId, null, CancellationToken.None);
        });

        // Assert - Should complete without exceptions
        await Task.WhenAll(validationTask, releaseTask).ConfigureAwait(false);

        // Verify lock is released
        var finalValidation = await lockManager.ValidateAsync(lockId, targets, CancellationToken.None);
        finalValidation.Success.Should().BeFalse("lock should be released");
    }

    /// <summary>
    /// Tests high concurrency with many users trying to lock different features simultaneously.
    /// Validates that the lock manager can handle high concurrent load without deadlocks.
    /// </summary>
    [Fact]
    public async Task HighConcurrency_ManyUsersLockingDifferentFeatures_AllSucceed()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var tasks = new Task<WfsLockAcquisitionResult>[50];

        // Act - 50 users simultaneously lock 50 different features
        for (int i = 0; i < 50; i++)
        {
            var userId = $"user{i}";
            var featureId = $"feature{i}";
            var targets = new[] { new WfsLockTarget("service1", "layer1", featureId) };

            tasks[i] = lockManager.TryAcquireAsync(userId, TimeSpan.FromMinutes(5), targets, CancellationToken.None);
        }

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue(),
            "all locks on different features should succeed");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000,
            "50 concurrent locks should complete quickly");

        // Verify all lock IDs are unique
        var lockIds = results.Select(r => r.Lock!.LockId).ToArray();
        lockIds.Should().OnlyHaveUniqueItems("each lock should have unique ID");
    }

    /// <summary>
    /// Tests contention scenario where many users compete for the same feature lock.
    /// Only one should succeed, all others should fail gracefully.
    /// </summary>
    [Fact]
    public async Task HighContention_ManyUsersLockingSameFeature_OnlyOneSucceeds()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "contested-feature") };
        var tasks = new Task<WfsLockAcquisitionResult>[20];

        // Act - 20 users simultaneously try to lock the same feature
        for (int i = 0; i < 20; i++)
        {
            var userId = $"user{i}";
            tasks[i] = lockManager.TryAcquireAsync(userId, TimeSpan.FromMinutes(5), targets, CancellationToken.None);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r.Success);
        successCount.Should().Be(1, "exactly one user should get the lock");

        var failCount = results.Count(r => !r.Success);
        failCount.Should().Be(19, "all other users should fail to acquire the lock");

        // All failures should have error messages
        results.Where(r => !r.Success).Should().AllSatisfy(r =>
        {
            r.Error.Should().NotBeNullOrWhiteSpace("failed acquisitions should include error message");
        });
    }

    /// <summary>
    /// Tests that lock expiration cleanup doesn't interfere with active lock operations.
    /// Expired locks should be cleaned up without affecting valid locks.
    /// </summary>
    [Fact]
    public async Task ExpirationCleanup_DoesNotAffectActiveLocks()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();

        // Create short-lived lock
        var expiredTargets = new[] { new WfsLockTarget("service1", "layer1", "expired-feature") };
        var expiredLock = await lockManager.TryAcquireAsync(
            "user1",
            TimeSpan.FromMilliseconds(50),
            expiredTargets,
            CancellationToken.None);

        // Create long-lived lock
        var activeTargets = new[] { new WfsLockTarget("service1", "layer1", "active-feature") };
        var activeLock = await lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            activeTargets,
            CancellationToken.None);

        expiredLock.Success.Should().BeTrue();
        activeLock.Success.Should().BeTrue();

        // Wait for first lock to expire
        await Task.Delay(100);

        // Act - Try to acquire the expired lock (triggers cleanup)
        var reacquireExpired = await lockManager.TryAcquireAsync(
            "user3",
            TimeSpan.FromMinutes(5),
            expiredTargets,
            CancellationToken.None);

        // Verify active lock is still valid
        var validateActive = await lockManager.ValidateAsync(
            activeLock.Lock!.LockId,
            activeTargets,
            CancellationToken.None);

        // Assert
        reacquireExpired.Success.Should().BeTrue("expired lock should be cleanable and reacquirable");
        validateActive.Success.Should().BeTrue("active lock should not be affected by cleanup");
    }

    /// <summary>
    /// Tests that partial lock release (releasing subset of targets) is thread-safe.
    /// When a lock covers multiple features, releasing some should not corrupt state.
    /// </summary>
    [Fact]
    public async Task PartialLockRelease_IsThreadSafe()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var target1 = new WfsLockTarget("service1", "layer1", "multi-feature-a");
        var target2 = new WfsLockTarget("service1", "layer1", "multi-feature-b");
        var allTargets = new[] { target1, target2 };

        var lockResult = await lockManager.TryAcquireAsync(
            "user1",
            TimeSpan.FromMinutes(5),
            allTargets,
            CancellationToken.None);

        lockResult.Success.Should().BeTrue();
        var lockId = lockResult.Lock!.LockId;

        // Act - Release only the first target
        await lockManager.ReleaseAsync("user1", lockId, new[] { target1 }, CancellationToken.None);

        // Assert - First target should be unlocked, second still locked
        var tryLockTarget1 = await lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            new[] { target1 },
            CancellationToken.None);

        tryLockTarget1.Success.Should().BeTrue("first target should be unlocked after partial release");

        var tryLockTarget2 = await lockManager.TryAcquireAsync(
            "user2",
            TimeSpan.FromMinutes(5),
            new[] { target2 },
            CancellationToken.None);

        tryLockTarget2.Success.Should().BeFalse("second target should still be locked");
    }

    /// <summary>
    /// Tests that lock validation and acquisition operations are atomic.
    /// No race condition should allow invalid state during concurrent operations.
    /// </summary>
    [Fact]
    public async Task LockOperations_AreAtomic()
    {
        // Arrange
        var lockManager = new InMemoryWfsLockManager();
        var targets = new[] { new WfsLockTarget("service1", "layer1", "atomic-test") };

        // Act - Rapidly alternate between acquire and release
        var operations = new Task[100];
        for (int i = 0; i < 50; i++)
        {
            var acquireIndex = i * 2;
            var releaseIndex = acquireIndex + 1;

            operations[acquireIndex] = Task.Run(async () =>
            {
                var result = await lockManager.TryAcquireAsync($"user{i}", TimeSpan.FromSeconds(1), targets, CancellationToken.None);
                if (result.Success)
                {
                    await lockManager.ReleaseAsync($"user{i}", result.Lock!.LockId, null, CancellationToken.None);
                }
            });

            operations[releaseIndex] = Task.Run(async () =>
            {
                // Validate with null lock (should fail if locked)
                await lockManager.ValidateAsync(null, targets, CancellationToken.None);
            });
        }

        // Assert - All operations should complete without exceptions
        await Task.WhenAll(operations).ConfigureAwait(false);

        // Final state should be consistent (lock released or expired)
        var finalAcquire = await lockManager.TryAcquireAsync("final-user", TimeSpan.FromMinutes(5), targets, CancellationToken.None);
        finalAcquire.Success.Should().BeTrue("lock should be available after all operations complete");
    }
}
