using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.OpenRosa;
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.OpenRosa;

[Trait("Category", "Unit")]
public class SqliteSubmissionRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteSubmissionRepository _repository;

    public SqliteSubmissionRepositoryTests()
    {
        // Use shared cache mode so all connections share the same in-memory database
        var connectionString = $"Data Source=SqliteSubmissionRepositoryTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _repository = new SqliteSubmissionRepository(connectionString);
    }

    public async Task InitializeAsync()
    {
        // Trigger schema initialization by calling GetAsync
        // This ensures the table is created before tests run
        _ = await _repository.GetAsync("nonexistent-id");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _connection?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullConnectionString_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new SqliteSubmissionRepository(null!));
        ex.ParamName.Should().Be("connectionString");
    }

    [Fact]
    public void Constructor_ValidConnectionString_CreatesInstance()
    {
        // Act
        var repository = new SqliteSubmissionRepository("Data Source=:memory:");

        // Assert
        repository.Should().NotBeNull();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ValidSubmission_StoresSuccessfully()
    {
        // Arrange
        var submission = CreateValidSubmission();

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(submission.Id);
        retrieved.InstanceId.Should().Be(submission.InstanceId);
        retrieved.FormId.Should().Be(submission.FormId);
        retrieved.LayerId.Should().Be(submission.LayerId);
        retrieved.ServiceId.Should().Be(submission.ServiceId);
        retrieved.SubmittedBy.Should().Be(submission.SubmittedBy);
        retrieved.Status.Should().Be(SubmissionStatus.Pending);
    }

    [Fact]
    public async Task CreateAsync_SubmissionWithXmlData_XmlPersistedCorrectly()
    {
        // Arrange
        var xmlData = XDocument.Parse(@"
            <data id=""tree_survey"">
                <meta>
                    <instanceID>uuid:12345-67890</instanceID>
                    <layerId>trees</layerId>
                    <serviceId>survey</serviceId>
                </meta>
                <species>Oak</species>
                <dbh_cm>45</dbh_cm>
            </data>");

        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = xmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.XmlData.Should().NotBeNull();
        retrieved.XmlData.ToString().Should().Contain("Oak");
        retrieved.XmlData.ToString().Should().Contain("uuid:12345-67890");
    }

    [Fact]
    public async Task CreateAsync_SubmissionWithGeometry_GeometryPersistedCorrectly()
    {
        // Arrange
        var point = new Point(-122.4194, 37.7749) { SRID = 4326 };
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = point,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Geometry.Should().NotBeNull();
        retrieved.Geometry.Should().BeOfType<Point>();
        var retrievedPoint = (Point)retrieved.Geometry!;
        retrievedPoint.X.Should().BeApproximately(-122.4194, 0.0001);
        retrievedPoint.Y.Should().BeApproximately(37.7749, 0.0001);
    }

    [Fact]
    public async Task CreateAsync_SubmissionWithAttachments_AttachmentsPersistedCorrectly()
    {
        // Arrange
        var attachments = new List<SubmissionAttachment>
        {
            new()
            {
                Filename = "photo1.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 1024000,
                StoragePath = "/storage/photo1.jpg"
            },
            new()
            {
                Filename = "signature.png",
                ContentType = "image/png",
                SizeBytes = 512000,
                StoragePath = "/storage/signature.png"
            }
        };

        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Attachments.Should().HaveCount(2);
        retrieved.Attachments[0].Filename.Should().Be("photo1.jpg");
        retrieved.Attachments[0].ContentType.Should().Be("image/jpeg");
        retrieved.Attachments[0].SizeBytes.Should().Be(1024000);
        retrieved.Attachments[1].Filename.Should().Be("signature.png");
    }

    [Fact]
    public async Task CreateAsync_SubmissionWithAttributes_AttributesPersistedCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, object?>
        {
            { "species", "Oak" },
            { "dbh_cm", 45 },
            { "health", "good" },
            { "count", 123L },
            { "measured", true }
        };

        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Attributes.Should().NotBeEmpty();
        retrieved.Attributes["species"].Should().Be("Oak");
        retrieved.Attributes["health"].Should().Be("good");
        retrieved.Attributes.Should().ContainKey("measured");
    }

    [Fact]
    public async Task CreateAsync_SubmissionWithDeviceId_DeviceIdPersisted()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = "device-12345",
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.DeviceId.Should().Be("device-12345");
    }

    [Fact]
    public async Task CreateAsync_SubmissionWithoutDeviceId_DeviceIdNull()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = null,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_DuplicateInstanceId_ThrowsSqliteException()
    {
        // Arrange
        var submission1 = CreateValidSubmission();
        var baseSubmission2 = CreateValidSubmission();
        var submission2 = new Submission
        {
            Id = Guid.NewGuid().ToString(),
            InstanceId = submission1.InstanceId, // Same instance ID
            FormId = baseSubmission2.FormId,
            FormVersion = baseSubmission2.FormVersion,
            LayerId = baseSubmission2.LayerId,
            ServiceId = baseSubmission2.ServiceId,
            SubmittedBy = baseSubmission2.SubmittedBy,
            SubmittedAt = baseSubmission2.SubmittedAt,
            DeviceId = baseSubmission2.DeviceId,
            Status = baseSubmission2.Status,
            ReviewedBy = baseSubmission2.ReviewedBy,
            ReviewedAt = baseSubmission2.ReviewedAt,
            ReviewNotes = baseSubmission2.ReviewNotes,
            XmlData = baseSubmission2.XmlData,
            Geometry = baseSubmission2.Geometry,
            Attributes = baseSubmission2.Attributes,
            Attachments = baseSubmission2.Attachments
        };

        // Act
        await _repository.CreateAsync(submission1);

        // Assert
        await Assert.ThrowsAsync<SqliteException>(() => _repository.CreateAsync(submission2));
    }

    [Fact]
    public async Task CreateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var submission = CreateValidSubmission();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _repository.CreateAsync(submission, cts.Token));
    }

    [Fact]
    public async Task CreateAsync_ConcurrentStores_NoCorruption()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(i =>
            {
                var baseSubmission = CreateValidSubmission();
                return new Submission
                {
                    Id = $"sub-{i}",
                    InstanceId = $"instance-{i}",
                    FormId = baseSubmission.FormId,
                    FormVersion = baseSubmission.FormVersion,
                    LayerId = baseSubmission.LayerId,
                    ServiceId = baseSubmission.ServiceId,
                    SubmittedBy = baseSubmission.SubmittedBy,
                    SubmittedAt = baseSubmission.SubmittedAt,
                    DeviceId = baseSubmission.DeviceId,
                    Status = baseSubmission.Status,
                    ReviewedBy = baseSubmission.ReviewedBy,
                    ReviewedAt = baseSubmission.ReviewedAt,
                    ReviewNotes = baseSubmission.ReviewNotes,
                    XmlData = baseSubmission.XmlData,
                    Geometry = baseSubmission.Geometry,
                    Attributes = baseSubmission.Attributes,
                    Attachments = baseSubmission.Attachments
                };
            })
            .Select(submission => Task.Run(() => _repository.CreateAsync(submission)))
            .ToList();

        // Act
        await Task.WhenAll(tasks);

        // Assert
        var items = await _repository.GetPendingAsync();
        items.Should().HaveCount(10);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_ExistingSubmission_ReturnsCorrectData()
    {
        // Arrange
        var submission = CreateValidSubmission();
        await _repository.CreateAsync(submission);

        // Act
        var retrieved = await _repository.GetAsync(submission.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(submission.Id);
        retrieved.InstanceId.Should().Be(submission.InstanceId);
        retrieved.Status.Should().Be(SubmissionStatus.Pending);
    }

    [Fact]
    public async Task GetAsync_NonExistentId_ReturnsNull()
    {
        // Act
        var retrieved = await _repository.GetAsync("non-existent-id");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_EmptyId_ReturnsNull()
    {
        // Act
        var retrieved = await _repository.GetAsync(string.Empty);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _repository.GetAsync("any-id", cts.Token));
    }

    #endregion

    #region GetPendingAsync Tests

    [Fact]
    public async Task GetPendingAsync_NoPendingSubmissions_ReturnsEmptyList()
    {
        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAsync_SinglePendingSubmission_ReturnsOne()
    {
        // Arrange
        var submission = CreateValidSubmission();
        await _repository.CreateAsync(submission);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        items.Should().HaveCount(1);
        items[0].Id.Should().Be(submission.Id);
        items[0].Status.Should().Be(SubmissionStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_MultiplePendingSubmissions_ReturnsAll()
    {
        // Arrange
        var submissions = Enumerable.Range(0, 5)
            .Select(i =>
            {
                var baseSubmission = CreateValidSubmission();
                return new Submission
                {
                    Id = $"sub-{i}",
                    InstanceId = $"instance-{i}",
                    FormId = baseSubmission.FormId,
                    FormVersion = baseSubmission.FormVersion,
                    LayerId = baseSubmission.LayerId,
                    ServiceId = baseSubmission.ServiceId,
                    SubmittedBy = baseSubmission.SubmittedBy,
                    SubmittedAt = baseSubmission.SubmittedAt,
                    DeviceId = baseSubmission.DeviceId,
                    Status = baseSubmission.Status,
                    ReviewedBy = baseSubmission.ReviewedBy,
                    ReviewedAt = baseSubmission.ReviewedAt,
                    ReviewNotes = baseSubmission.ReviewNotes,
                    XmlData = baseSubmission.XmlData,
                    Geometry = baseSubmission.Geometry,
                    Attributes = baseSubmission.Attributes,
                    Attachments = baseSubmission.Attachments
                };
            })
            .ToList();

        foreach (var submission in submissions)
        {
            await _repository.CreateAsync(submission);
        }

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetPendingAsync_OrderedBySubmittedAtDescending()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var base1 = CreateValidSubmission();
        var base2 = CreateValidSubmission();
        var base3 = CreateValidSubmission();
        var submissions = new[]
        {
            new Submission
            {
                Id = "sub-1",
                InstanceId = "inst-1",
                FormId = base1.FormId,
                FormVersion = base1.FormVersion,
                LayerId = base1.LayerId,
                ServiceId = base1.ServiceId,
                SubmittedBy = base1.SubmittedBy,
                SubmittedAt = now.AddMinutes(-30),
                DeviceId = base1.DeviceId,
                Status = base1.Status,
                ReviewedBy = base1.ReviewedBy,
                ReviewedAt = base1.ReviewedAt,
                ReviewNotes = base1.ReviewNotes,
                XmlData = base1.XmlData,
                Geometry = base1.Geometry,
                Attributes = base1.Attributes,
                Attachments = base1.Attachments
            },
            new Submission
            {
                Id = "sub-2",
                InstanceId = "inst-2",
                FormId = base2.FormId,
                FormVersion = base2.FormVersion,
                LayerId = base2.LayerId,
                ServiceId = base2.ServiceId,
                SubmittedBy = base2.SubmittedBy,
                SubmittedAt = now.AddMinutes(-10),
                DeviceId = base2.DeviceId,
                Status = base2.Status,
                ReviewedBy = base2.ReviewedBy,
                ReviewedAt = base2.ReviewedAt,
                ReviewNotes = base2.ReviewNotes,
                XmlData = base2.XmlData,
                Geometry = base2.Geometry,
                Attributes = base2.Attributes,
                Attachments = base2.Attachments
            },
            new Submission
            {
                Id = "sub-3",
                InstanceId = "inst-3",
                FormId = base3.FormId,
                FormVersion = base3.FormVersion,
                LayerId = base3.LayerId,
                ServiceId = base3.ServiceId,
                SubmittedBy = base3.SubmittedBy,
                SubmittedAt = now,
                DeviceId = base3.DeviceId,
                Status = base3.Status,
                ReviewedBy = base3.ReviewedBy,
                ReviewedAt = base3.ReviewedAt,
                ReviewNotes = base3.ReviewNotes,
                XmlData = base3.XmlData,
                Geometry = base3.Geometry,
                Attributes = base3.Attributes,
                Attachments = base3.Attachments
            }
        };

        foreach (var submission in submissions)
        {
            await _repository.CreateAsync(submission);
            await Task.Delay(10); // Ensure different timestamps
        }

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        items.Should().HaveCount(3);
        items[0].Id.Should().Be("sub-3"); // Most recent first
        items[1].Id.Should().Be("sub-2");
        items[2].Id.Should().Be("sub-1");
    }

    [Fact]
    public async Task GetPendingAsync_OnlyReturnsApprovedStatus()
    {
        // Arrange
        var basePending = CreateValidSubmission();
        var pendingSubmission = new Submission
        {
            Id = "pending-1",
            InstanceId = "inst-1",
            FormId = basePending.FormId,
            FormVersion = basePending.FormVersion,
            LayerId = basePending.LayerId,
            ServiceId = basePending.ServiceId,
            SubmittedBy = basePending.SubmittedBy,
            SubmittedAt = basePending.SubmittedAt,
            DeviceId = basePending.DeviceId,
            Status = basePending.Status,
            ReviewedBy = basePending.ReviewedBy,
            ReviewedAt = basePending.ReviewedAt,
            ReviewNotes = basePending.ReviewNotes,
            XmlData = basePending.XmlData,
            Geometry = basePending.Geometry,
            Attributes = basePending.Attributes,
            Attachments = basePending.Attachments
        };
        var baseApproved = CreateValidSubmission();
        var approvedSubmission = new Submission
        {
            Id = "approved-1",
            InstanceId = "inst-2",
            FormId = baseApproved.FormId,
            FormVersion = baseApproved.FormVersion,
            LayerId = baseApproved.LayerId,
            ServiceId = baseApproved.ServiceId,
            SubmittedBy = baseApproved.SubmittedBy,
            SubmittedAt = baseApproved.SubmittedAt,
            DeviceId = baseApproved.DeviceId,
            Status = SubmissionStatus.Approved,
            ReviewedBy = baseApproved.ReviewedBy,
            ReviewedAt = baseApproved.ReviewedAt,
            ReviewNotes = baseApproved.ReviewNotes,
            XmlData = baseApproved.XmlData,
            Geometry = baseApproved.Geometry,
            Attributes = baseApproved.Attributes,
            Attachments = baseApproved.Attachments
        };

        await _repository.CreateAsync(pendingSubmission);
        await _repository.CreateAsync(approvedSubmission);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        items.Should().HaveCount(1);
        items[0].Id.Should().Be("pending-1");
    }

    [Fact]
    public async Task GetPendingAsync_OnlyReturnsRejectedStatus()
    {
        // Arrange
        var basePending = CreateValidSubmission();
        var pendingSubmission = new Submission
        {
            Id = "pending-1",
            InstanceId = "inst-1",
            FormId = basePending.FormId,
            FormVersion = basePending.FormVersion,
            LayerId = basePending.LayerId,
            ServiceId = basePending.ServiceId,
            SubmittedBy = basePending.SubmittedBy,
            SubmittedAt = basePending.SubmittedAt,
            DeviceId = basePending.DeviceId,
            Status = basePending.Status,
            ReviewedBy = basePending.ReviewedBy,
            ReviewedAt = basePending.ReviewedAt,
            ReviewNotes = basePending.ReviewNotes,
            XmlData = basePending.XmlData,
            Geometry = basePending.Geometry,
            Attributes = basePending.Attributes,
            Attachments = basePending.Attachments
        };
        var baseRejected = CreateValidSubmission();
        var rejectedSubmission = new Submission
        {
            Id = "rejected-1",
            InstanceId = "inst-2",
            FormId = baseRejected.FormId,
            FormVersion = baseRejected.FormVersion,
            LayerId = baseRejected.LayerId,
            ServiceId = baseRejected.ServiceId,
            SubmittedBy = baseRejected.SubmittedBy,
            SubmittedAt = baseRejected.SubmittedAt,
            DeviceId = baseRejected.DeviceId,
            Status = SubmissionStatus.Rejected,
            ReviewedBy = baseRejected.ReviewedBy,
            ReviewedAt = baseRejected.ReviewedAt,
            ReviewNotes = baseRejected.ReviewNotes,
            XmlData = baseRejected.XmlData,
            Geometry = baseRejected.Geometry,
            Attributes = baseRejected.Attributes,
            Attachments = baseRejected.Attachments
        };

        await _repository.CreateAsync(pendingSubmission);
        await _repository.CreateAsync(rejectedSubmission);

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        items.Should().HaveCount(1);
        items[0].Id.Should().Be("pending-1");
    }

    [Fact]
    public async Task GetPendingAsync_WithLayerIdFilter_ReturnsOnlyMatchingLayer()
    {
        // Arrange
        var base1 = CreateValidSubmission();
        var submission1 = new Submission
        {
            Id = "sub-1",
            InstanceId = "inst-1",
            FormId = base1.FormId,
            FormVersion = base1.FormVersion,
            LayerId = "layer-A",
            ServiceId = base1.ServiceId,
            SubmittedBy = base1.SubmittedBy,
            SubmittedAt = base1.SubmittedAt,
            DeviceId = base1.DeviceId,
            Status = base1.Status,
            ReviewedBy = base1.ReviewedBy,
            ReviewedAt = base1.ReviewedAt,
            ReviewNotes = base1.ReviewNotes,
            XmlData = base1.XmlData,
            Geometry = base1.Geometry,
            Attributes = base1.Attributes,
            Attachments = base1.Attachments
        };
        var base2 = CreateValidSubmission();
        var submission2 = new Submission
        {
            Id = "sub-2",
            InstanceId = "inst-2",
            FormId = base2.FormId,
            FormVersion = base2.FormVersion,
            LayerId = "layer-B",
            ServiceId = base2.ServiceId,
            SubmittedBy = base2.SubmittedBy,
            SubmittedAt = base2.SubmittedAt,
            DeviceId = base2.DeviceId,
            Status = base2.Status,
            ReviewedBy = base2.ReviewedBy,
            ReviewedAt = base2.ReviewedAt,
            ReviewNotes = base2.ReviewNotes,
            XmlData = base2.XmlData,
            Geometry = base2.Geometry,
            Attributes = base2.Attributes,
            Attachments = base2.Attachments
        };
        var base3 = CreateValidSubmission();
        var submission3 = new Submission
        {
            Id = "sub-3",
            InstanceId = "inst-3",
            FormId = base3.FormId,
            FormVersion = base3.FormVersion,
            LayerId = "layer-A",
            ServiceId = base3.ServiceId,
            SubmittedBy = base3.SubmittedBy,
            SubmittedAt = base3.SubmittedAt,
            DeviceId = base3.DeviceId,
            Status = base3.Status,
            ReviewedBy = base3.ReviewedBy,
            ReviewedAt = base3.ReviewedAt,
            ReviewNotes = base3.ReviewNotes,
            XmlData = base3.XmlData,
            Geometry = base3.Geometry,
            Attributes = base3.Attributes,
            Attachments = base3.Attachments
        };

        await _repository.CreateAsync(submission1);
        await _repository.CreateAsync(submission2);
        await _repository.CreateAsync(submission3);

        // Act
        var items = await _repository.GetPendingAsync("layer-A");

        // Assert
        items.Should().HaveCount(2);
        items.Should().AllSatisfy(s => s.LayerId.Should().Be("layer-A"));
    }

    [Fact]
    public async Task GetPendingAsync_WithNonExistentLayerId_ReturnsEmptyList()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = "existing-layer",
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };
        await _repository.CreateAsync(submission);

        // Act
        var items = await _repository.GetPendingAsync("non-existent-layer");

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAsync_WithNullLayerId_ReturnsAllPending()
    {
        // Arrange
        var base1 = CreateValidSubmission();
        var submission1 = new Submission
        {
            Id = "sub-1",
            InstanceId = "inst-1",
            FormId = base1.FormId,
            FormVersion = base1.FormVersion,
            LayerId = "layer-A",
            ServiceId = base1.ServiceId,
            SubmittedBy = base1.SubmittedBy,
            SubmittedAt = base1.SubmittedAt,
            DeviceId = base1.DeviceId,
            Status = base1.Status,
            ReviewedBy = base1.ReviewedBy,
            ReviewedAt = base1.ReviewedAt,
            ReviewNotes = base1.ReviewNotes,
            XmlData = base1.XmlData,
            Geometry = base1.Geometry,
            Attributes = base1.Attributes,
            Attachments = base1.Attachments
        };
        var base2 = CreateValidSubmission();
        var submission2 = new Submission
        {
            Id = "sub-2",
            InstanceId = "inst-2",
            FormId = base2.FormId,
            FormVersion = base2.FormVersion,
            LayerId = "layer-B",
            ServiceId = base2.ServiceId,
            SubmittedBy = base2.SubmittedBy,
            SubmittedAt = base2.SubmittedAt,
            DeviceId = base2.DeviceId,
            Status = base2.Status,
            ReviewedBy = base2.ReviewedBy,
            ReviewedAt = base2.ReviewedAt,
            ReviewNotes = base2.ReviewNotes,
            XmlData = base2.XmlData,
            Geometry = base2.Geometry,
            Attributes = base2.Attributes,
            Attachments = base2.Attachments
        };

        await _repository.CreateAsync(submission1);
        await _repository.CreateAsync(submission2);

        // Act
        var items = await _repository.GetPendingAsync(null);

        // Assert
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPendingAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _repository.GetPendingAsync(null, cts.Token));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ApproveSubmission_StatusUpdated()
    {
        // Arrange
        var submission = CreateValidSubmission();
        await _repository.CreateAsync(submission);

        var updated = new Submission
        {
            Id = submission.Id,
            InstanceId = submission.InstanceId,
            FormId = submission.FormId,
            FormVersion = submission.FormVersion,
            LayerId = submission.LayerId,
            ServiceId = submission.ServiceId,
            SubmittedBy = submission.SubmittedBy,
            SubmittedAt = submission.SubmittedAt,
            DeviceId = submission.DeviceId,
            Status = SubmissionStatus.Approved,
            ReviewedBy = "admin@example.com",
            ReviewedAt = DateTimeOffset.UtcNow,
            ReviewNotes = "Looks good!",
            XmlData = submission.XmlData,
            Geometry = submission.Geometry,
            Attributes = submission.Attributes,
            Attachments = submission.Attachments
        };

        // Act
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(SubmissionStatus.Approved);
        retrieved.ReviewedBy.Should().Be("admin@example.com");
        retrieved.ReviewedAt.Should().NotBeNull();
        retrieved.ReviewNotes.Should().Be("Looks good!");
    }

    [Fact]
    public async Task UpdateAsync_RejectSubmission_StatusUpdated()
    {
        // Arrange
        var submission = CreateValidSubmission();
        await _repository.CreateAsync(submission);

        var updated = new Submission
        {
            Id = submission.Id,
            InstanceId = submission.InstanceId,
            FormId = submission.FormId,
            FormVersion = submission.FormVersion,
            LayerId = submission.LayerId,
            ServiceId = submission.ServiceId,
            SubmittedBy = submission.SubmittedBy,
            SubmittedAt = submission.SubmittedAt,
            DeviceId = submission.DeviceId,
            Status = SubmissionStatus.Rejected,
            ReviewedBy = "admin@example.com",
            ReviewedAt = DateTimeOffset.UtcNow,
            ReviewNotes = "Invalid data",
            XmlData = submission.XmlData,
            Geometry = submission.Geometry,
            Attributes = submission.Attributes,
            Attachments = submission.Attachments
        };

        // Act
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(SubmissionStatus.Rejected);
        retrieved.ReviewedBy.Should().Be("admin@example.com");
        retrieved.ReviewNotes.Should().Be("Invalid data");
    }

    [Fact]
    public async Task UpdateAsync_AlreadyApproved_Idempotent()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = SubmissionStatus.Approved,
            ReviewedBy = "admin@example.com",
            ReviewedAt = DateTimeOffset.UtcNow,
            ReviewNotes = "First approval",
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };
        await _repository.CreateAsync(submission);

        var updated = new Submission
        {
            Id = submission.Id,
            InstanceId = submission.InstanceId,
            FormId = submission.FormId,
            FormVersion = submission.FormVersion,
            LayerId = submission.LayerId,
            ServiceId = submission.ServiceId,
            SubmittedBy = submission.SubmittedBy,
            SubmittedAt = submission.SubmittedAt,
            DeviceId = submission.DeviceId,
            Status = submission.Status,
            ReviewedBy = submission.ReviewedBy,
            ReviewedAt = submission.ReviewedAt,
            ReviewNotes = "Second approval",
            XmlData = submission.XmlData,
            Geometry = submission.Geometry,
            Attributes = submission.Attributes,
            Attachments = submission.Attachments
        };

        // Act
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(SubmissionStatus.Approved);
        retrieved.ReviewNotes.Should().Be("Second approval");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentSubmission_DoesNotThrow()
    {
        // Arrange
        var submission = CreateValidSubmission();

        // Act & Assert
        await _repository.UpdateAsync(submission); // Should not throw
    }

    [Fact]
    public async Task UpdateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var submission = CreateValidSubmission();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _repository.UpdateAsync(submission, cts.Token));
    }

    [Fact]
    public async Task UpdateAsync_OnlyUpdatesReviewFields()
    {
        // Arrange
        var submission = CreateValidSubmission();
        await _repository.CreateAsync(submission);

        var updated = new Submission
        {
            Id = submission.Id,
            InstanceId = submission.InstanceId,
            FormId = submission.FormId,
            FormVersion = submission.FormVersion,
            LayerId = "changed-layer", // Try to change other fields (should not be updated)
            ServiceId = submission.ServiceId,
            SubmittedBy = "hacker@example.com",
            SubmittedAt = submission.SubmittedAt,
            DeviceId = submission.DeviceId,
            Status = SubmissionStatus.Approved,
            ReviewedBy = "admin@example.com",
            ReviewedAt = DateTimeOffset.UtcNow,
            ReviewNotes = submission.ReviewNotes,
            XmlData = submission.XmlData,
            Geometry = submission.Geometry,
            Attributes = submission.Attributes,
            Attachments = submission.Attachments
        };

        // Act
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(SubmissionStatus.Approved);
        retrieved.ReviewedBy.Should().Be("admin@example.com");
        // These should NOT change
        retrieved.SubmittedBy.Should().Be(submission.SubmittedBy);
        retrieved.LayerId.Should().Be(submission.LayerId);
    }

    #endregion

    #region Schema and Database Integrity Tests

    [Fact]
    public async Task EnsureSchemaAsync_CreatesTablesAndIndexes()
    {
        // Arrange
        var submission = CreateValidSubmission();

        // Act - First operation triggers schema initialization
        await _repository.CreateAsync(submission);

        // Assert - Verify table exists
        using var connection = new SqliteConnection(_connection.ConnectionString);
        await connection.OpenAsync();

        var tableCheckSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='openrosa_submissions'";
        using var cmd = new SqliteCommand(tableCheckSql, connection);
        var tableName = await cmd.ExecuteScalarAsync();

        tableName.Should().NotBeNull();
        tableName.Should().Be("openrosa_submissions");
    }

    [Fact]
    public async Task EnsureSchemaAsync_CreatesIndexes()
    {
        // Arrange
        var submission = CreateValidSubmission();

        // Act - First operation triggers schema initialization
        await _repository.CreateAsync(submission);

        // Assert - Verify indexes exist
        using var connection = new SqliteConnection(_connection.ConnectionString);
        await connection.OpenAsync();

        var indexCheckSql = "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'idx_submissions_%'";
        using var cmd = new SqliteCommand(indexCheckSql, connection);
        using var reader = await cmd.ExecuteReaderAsync();

        var indexes = new List<string>();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        indexes.Should().Contain("idx_submissions_status");
        indexes.Should().Contain("idx_submissions_layer");
        indexes.Should().Contain("idx_submissions_submitted_at");
    }

    [Fact]
    public async Task EnsureSchemaAsync_CalledMultipleTimes_IsIdempotent()
    {
        // Arrange
        var base1 = CreateValidSubmission();
        var submission1 = new Submission
        {
            Id = "sub-1",
            InstanceId = "inst-1",
            FormId = base1.FormId,
            FormVersion = base1.FormVersion,
            LayerId = base1.LayerId,
            ServiceId = base1.ServiceId,
            SubmittedBy = base1.SubmittedBy,
            SubmittedAt = base1.SubmittedAt,
            DeviceId = base1.DeviceId,
            Status = base1.Status,
            ReviewedBy = base1.ReviewedBy,
            ReviewedAt = base1.ReviewedAt,
            ReviewNotes = base1.ReviewNotes,
            XmlData = base1.XmlData,
            Geometry = base1.Geometry,
            Attributes = base1.Attributes,
            Attachments = base1.Attachments
        };
        var base2 = CreateValidSubmission();
        var submission2 = new Submission
        {
            Id = "sub-2",
            InstanceId = "inst-2",
            FormId = base2.FormId,
            FormVersion = base2.FormVersion,
            LayerId = base2.LayerId,
            ServiceId = base2.ServiceId,
            SubmittedBy = base2.SubmittedBy,
            SubmittedAt = base2.SubmittedAt,
            DeviceId = base2.DeviceId,
            Status = base2.Status,
            ReviewedBy = base2.ReviewedBy,
            ReviewedAt = base2.ReviewedAt,
            ReviewNotes = base2.ReviewNotes,
            XmlData = base2.XmlData,
            Geometry = base2.Geometry,
            Attributes = base2.Attributes,
            Attachments = base2.Attachments
        };

        // Act - Both operations will try to initialize schema
        await _repository.CreateAsync(submission1);
        await _repository.CreateAsync(submission2);

        // Assert - Both submissions should exist
        var retrieved1 = await _repository.GetAsync("sub-1");
        var retrieved2 = await _repository.GetAsync("sub-2");

        retrieved1.Should().NotBeNull();
        retrieved2.Should().NotBeNull();
    }

    [Fact]
    public async Task EnsureSchemaAsync_ConcurrentInitialization_HandledSafely()
    {
        // Arrange
        var submissions = Enumerable.Range(0, 20)
            .Select(i =>
            {
                var baseSubmission = CreateValidSubmission();
                return new Submission
                {
                    Id = $"sub-{i}",
                    InstanceId = $"instance-{i}",
                    FormId = baseSubmission.FormId,
                    FormVersion = baseSubmission.FormVersion,
                    LayerId = baseSubmission.LayerId,
                    ServiceId = baseSubmission.ServiceId,
                    SubmittedBy = baseSubmission.SubmittedBy,
                    SubmittedAt = baseSubmission.SubmittedAt,
                    DeviceId = baseSubmission.DeviceId,
                    Status = baseSubmission.Status,
                    ReviewedBy = baseSubmission.ReviewedBy,
                    ReviewedAt = baseSubmission.ReviewedAt,
                    ReviewNotes = baseSubmission.ReviewNotes,
                    XmlData = baseSubmission.XmlData,
                    Geometry = baseSubmission.Geometry,
                    Attributes = baseSubmission.Attributes,
                    Attachments = baseSubmission.Attachments
                };
            })
            .ToList();

        // Act - Concurrent initialization
        var tasks = submissions.Select(s => Task.Run(() => _repository.CreateAsync(s))).ToList();
        await Task.WhenAll(tasks);

        // Assert - All submissions should be created
        var items = await _repository.GetPendingAsync();
        items.Should().HaveCount(20);
    }

    #endregion

    #region SQL Injection Prevention Tests

    [Fact]
    public async Task CreateAsync_MaliciousIdField_ParameterizedSafely()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = "'; DROP TABLE openrosa_submissions; --",
            InstanceId = "safe-instance-id",
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert - Table should still exist and submission should be retrievable
        var retrieved = await _repository.GetAsync("'; DROP TABLE openrosa_submissions; --");
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("'; DROP TABLE openrosa_submissions; --");
    }

    [Fact]
    public async Task GetPendingAsync_MaliciousLayerId_ParameterizedSafely()
    {
        // Arrange
        var submission = CreateValidSubmission();
        await _repository.CreateAsync(submission);

        // Act
        var items = await _repository.GetPendingAsync("' OR '1'='1");

        // Assert - Should return empty (no layer matches), not all submissions
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_MaliciousReviewNotes_ParameterizedSafely()
    {
        // Arrange
        var submission = CreateValidSubmission();
        await _repository.CreateAsync(submission);

        // Create a second pending submission to verify SQL injection didn't delete it
        var baseSubmission = CreateValidSubmission();
        var otherSubmission = new Submission
        {
            Id = $"{submission.Id}-other",
            InstanceId = $"{submission.InstanceId}-other",
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };
        await _repository.CreateAsync(otherSubmission);

        var updated = new Submission
        {
            Id = submission.Id,
            InstanceId = submission.InstanceId,
            FormId = submission.FormId,
            FormVersion = submission.FormVersion,
            LayerId = submission.LayerId,
            ServiceId = submission.ServiceId,
            SubmittedBy = submission.SubmittedBy,
            SubmittedAt = submission.SubmittedAt,
            DeviceId = submission.DeviceId,
            Status = SubmissionStatus.Approved,
            ReviewedBy = "admin",
            ReviewedAt = DateTimeOffset.UtcNow,
            ReviewNotes = "'; DELETE FROM openrosa_submissions; --",
            XmlData = submission.XmlData,
            Geometry = submission.Geometry,
            Attributes = submission.Attributes,
            Attachments = submission.Attachments
        };

        // Act
        await _repository.UpdateAsync(updated);

        // Assert - All submissions should still exist (SQL injection didn't delete them)
        var items = await _repository.GetPendingAsync();
        items.Should().NotBeEmpty(); // otherSubmission should still exist

        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved!.ReviewNotes.Should().Be("'; DELETE FROM openrosa_submissions; --");
    }

    #endregion

    #region Data Type Handling Tests

    [Fact]
    public async Task CreateAsync_NullGeometry_HandledCorrectly()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = null,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Geometry.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_PolygonGeometry_PersistedCorrectly()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(-122.419, 37.774),
            new Coordinate(-122.418, 37.775),
            new Coordinate(-122.417, 37.774),
            new Coordinate(-122.419, 37.774) // Closed ring
        };
        var ring = new LinearRing(coords);
        var polygon = new Polygon(ring) { SRID = 4326 };

        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = polygon,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Geometry.Should().BeOfType<Polygon>();
        retrieved.Geometry!.SRID.Should().Be(4326);
    }

    [Fact]
    public async Task CreateAsync_LineStringGeometry_PersistedCorrectly()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(-122.419, 37.774),
            new Coordinate(-122.418, 37.775),
            new Coordinate(-122.417, 37.776)
        };
        var lineString = new LineString(coords) { SRID = 4326 };

        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = lineString,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Geometry.Should().BeOfType<LineString>();
        var retrievedLine = (LineString)retrieved.Geometry!;
        retrievedLine.Coordinates.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateAsync_EmptyAttributes_HandledCorrectly()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = new Dictionary<string, object?>(),
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Attributes.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_AttributesWithNullValues_HandledCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, object?>
        {
            { "field1", "value1" },
            { "field2", null },
            { "field3", 123 }
        };

        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Attributes.Should().ContainKey("field2");
        retrieved.Attributes["field2"].Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_EmptyAttachments_HandledCorrectly()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = Array.Empty<SubmissionAttachment>()
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_DateTimeOffsetPersistence_MaintainsPrecision()
    {
        // Arrange
        var submittedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.FromHours(-8));
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = submittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        // Allow small precision loss due to serialization
        retrieved!.SubmittedAt.Should().BeCloseTo(submittedAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [Fact]
    public async Task CreateAsync_VeryLongReviewNotes_HandledCorrectly()
    {
        // Arrange
        var longNotes = new string('A', 10000);
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = SubmissionStatus.Rejected,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = longNotes,
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.ReviewNotes.Should().HaveLength(10000);
    }

    [Fact]
    public async Task CreateAsync_UnicodeCharactersInFields_HandledCorrectly()
    {
        // Arrange
        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = "用户@example.com",
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = "测试 🌳 tree émoji",
            XmlData = baseSubmission.XmlData,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.SubmittedBy.Should().Be("用户@example.com");
        retrieved.ReviewNotes.Should().Contain("测试 🌳 tree émoji");
    }

    [Fact]
    public async Task CreateAsync_ComplexXmlDocument_HandledCorrectly()
    {
        // Arrange
        var complexXml = XDocument.Parse(@"
            <data id=""complex"">
                <meta>
                    <instanceID>uuid:complex-instance</instanceID>
                    <layerId>test</layerId>
                    <serviceId>test</serviceId>
                </meta>
                <nested>
                    <level1>
                        <level2>Deep value</level2>
                    </level1>
                </nested>
                <special-chars>&lt;&gt;&amp;&quot;&apos;</special-chars>
            </data>");

        var baseSubmission = CreateValidSubmission();
        var submission = new Submission
        {
            Id = baseSubmission.Id,
            InstanceId = baseSubmission.InstanceId,
            FormId = baseSubmission.FormId,
            FormVersion = baseSubmission.FormVersion,
            LayerId = baseSubmission.LayerId,
            ServiceId = baseSubmission.ServiceId,
            SubmittedBy = baseSubmission.SubmittedBy,
            SubmittedAt = baseSubmission.SubmittedAt,
            DeviceId = baseSubmission.DeviceId,
            Status = baseSubmission.Status,
            ReviewedBy = baseSubmission.ReviewedBy,
            ReviewedAt = baseSubmission.ReviewedAt,
            ReviewNotes = baseSubmission.ReviewNotes,
            XmlData = complexXml,
            Geometry = baseSubmission.Geometry,
            Attributes = baseSubmission.Attributes,
            Attachments = baseSubmission.Attachments
        };

        // Act
        await _repository.CreateAsync(submission);

        // Assert
        var retrieved = await _repository.GetAsync(submission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.XmlData.ToString().Should().Contain("Deep value");
        retrieved.XmlData.ToString().Should().Contain("special-chars");
    }

    [Fact]
    public async Task GetPendingAsync_LargeResultSet_HandledEfficiently()
    {
        // Arrange - Create 100 pending submissions
        var submissions = Enumerable.Range(0, 100)
            .Select(i =>
            {
                var baseSubmission = CreateValidSubmission();
                return new Submission
                {
                    Id = $"sub-{i:D3}",
                    InstanceId = $"instance-{i:D3}",
                    FormId = baseSubmission.FormId,
                    FormVersion = baseSubmission.FormVersion,
                    LayerId = baseSubmission.LayerId,
                    ServiceId = baseSubmission.ServiceId,
                    SubmittedBy = baseSubmission.SubmittedBy,
                    SubmittedAt = baseSubmission.SubmittedAt,
                    DeviceId = baseSubmission.DeviceId,
                    Status = baseSubmission.Status,
                    ReviewedBy = baseSubmission.ReviewedBy,
                    ReviewedAt = baseSubmission.ReviewedAt,
                    ReviewNotes = baseSubmission.ReviewNotes,
                    XmlData = baseSubmission.XmlData,
                    Geometry = baseSubmission.Geometry,
                    Attributes = baseSubmission.Attributes,
                    Attachments = baseSubmission.Attachments
                };
            })
            .ToList();

        foreach (var submission in submissions)
        {
            await _repository.CreateAsync(submission);
        }

        // Act
        var items = await _repository.GetPendingAsync();

        // Assert
        items.Should().HaveCount(100);
    }

    #endregion

    #region Helper Methods

    private static Submission CreateValidSubmission()
    {
        return new Submission
        {
            Id = Guid.NewGuid().ToString(),
            InstanceId = $"uuid:{Guid.NewGuid()}",
            FormId = "test_form",
            FormVersion = "1.0.0",
            LayerId = "test_layer",
            ServiceId = "test_service",
            SubmittedBy = "user@example.com",
            SubmittedAt = DateTimeOffset.UtcNow,
            DeviceId = null,
            Status = SubmissionStatus.Pending,
            ReviewedBy = null,
            ReviewedAt = null,
            ReviewNotes = null,
            XmlData = XDocument.Parse("<data id=\"test\"><meta><instanceID>uuid:test</instanceID></meta></data>"),
            Geometry = null,
            Attributes = new Dictionary<string, object?>
            {
                { "field1", "value1" },
                { "field2", 42 }
            },
            Attachments = Array.Empty<SubmissionAttachment>()
        };
    }


    #endregion
}
