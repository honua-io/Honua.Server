using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.OpenRosa;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.OpenRosa;

[Trait("Category", "Unit")]
public class SubmissionProcessorTests
{
    private readonly Mock<IMetadataRegistry> _mockMetadataRegistry;
    private readonly Mock<IFeatureEditOrchestrator> _mockEditOrchestrator;
    private readonly Mock<ISubmissionRepository> _mockSubmissionRepository;
    private readonly Mock<ILogger<SubmissionProcessor>> _mockLogger;

    public SubmissionProcessorTests()
    {
        _mockMetadataRegistry = new Mock<IMetadataRegistry>();
        _mockEditOrchestrator = new Mock<IFeatureEditOrchestrator>();
        _mockSubmissionRepository = new Mock<ISubmissionRepository>();
        _mockLogger = new Mock<ILogger<SubmissionProcessor>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullMetadataRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SubmissionProcessor(null!, _mockEditOrchestrator.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("metadata");
    }

    [Fact]
    public void Constructor_NullEditOrchestrator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SubmissionProcessor(_mockMetadataRegistry.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("editOrchestrator");
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Act
        var processor = new SubmissionProcessor(
            _mockMetadataRegistry.Object,
            _mockEditOrchestrator.Object,
            _mockSubmissionRepository.Object);

        // Assert
        processor.Should().NotBeNull();
    }

    #endregion

    #region ProcessAsync - Missing Required Fields

    [Fact]
    public async Task ProcessAsync_MissingInstanceId_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocument(instanceId: null, layerId: "test-layer", serviceId: "test-service");
        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("instanceID");
    }

    [Fact]
    public async Task ProcessAsync_MissingLayerId_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocument(instanceId: "test-123", layerId: null, serviceId: "test-service");
        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("layerId");
    }

    [Fact]
    public async Task ProcessAsync_MissingServiceId_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocument(instanceId: "test-123", layerId: "test-layer", serviceId: null);
        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("serviceId");
    }

    #endregion

    #region ProcessAsync - Layer Not Found

    [Fact]
    public async Task ProcessAsync_LayerNotFound_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocument(instanceId: "test-123", layerId: "missing-layer", serviceId: "test-service");
        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var snapshot = CreateMetadataSnapshot(hasLayer: false);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ProcessAsync_OpenRosaNotEnabled_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocument(instanceId: "test-123", layerId: "test-layer", serviceId: "test-service");
        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Point", openRosaEnabled: false);
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("not enabled");
    }

    #endregion

    #region ProcessAsync - Direct Mode with Point Geometry

    [Fact]
    public async Task ProcessAsync_DirectMode_ValidGeopointSubmission_InsertsFeature()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-123",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak",
                ["dbh_cm"] = "25"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user",
            DeviceId = "device-001"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct");

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(
                It.IsAny<FeatureEditBatch>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ResultType.Should().Be(SubmissionResultType.DirectPublished);
        result.InstanceId.Should().Be("test-123");

        _mockEditOrchestrator.Verify(x => x.ExecuteAsync(
            It.Is<FeatureEditBatch>(b =>
                b.Commands.Count == 1 &&
                b.Commands[0] is AddFeatureCommand &&
                ((AddFeatureCommand)b.Commands[0]).ServiceId == "field-surveys" &&
                ((AddFeatureCommand)b.Commands[0]).LayerId == "trees"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_ValidGeopointSubmission_CreatesCorrectPointGeometry()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-123",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 100 5",
                ["species"] = "Pine"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Point", geometryField: "geometry", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Point? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(
                It.IsAny<FeatureEditBatch>(),
                It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedGeometry = command.Attributes["geometry"] as Point;
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedGeometry.Should().NotBeNull();
        capturedGeometry!.Y.Should().BeApproximately(37.7749, 0.0001); // Latitude
        capturedGeometry.X.Should().BeApproximately(-122.4194, 0.0001); // Longitude
        capturedGeometry.SRID.Should().Be(4326);
    }

    #endregion

    #region ProcessAsync - Direct Mode with LineString Geometry

    [Fact]
    public async Task ProcessAsync_DirectMode_ValidGeotraceSubmission_CreatesLineString()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-456",
            layerId: "trails",
            serviceId: "recreation",
            data: new Dictionary<string, string>
            {
                ["path"] = "37.7749 -122.4194 0 0;37.7750 -122.4195 0 0;37.7751 -122.4196 0 0",
                ["name"] = "Mountain Trail"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "LineString",
            geometryField: "path",
            openRosaMode: "direct");

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        LineString? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(
                It.IsAny<FeatureEditBatch>(),
                It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedGeometry = command.Attributes["geometry"] as LineString;
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ResultType.Should().Be(SubmissionResultType.DirectPublished);
        capturedGeometry.Should().NotBeNull();
        capturedGeometry!.NumPoints.Should().Be(3);
        capturedGeometry.SRID.Should().Be(4326);
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_GeotraceWithTwoPoints_CreatesLineString()
    {
        // Arrange: Two points is the minimum for a valid LineString
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-789",
            layerId: "trails",
            serviceId: "recreation",
            data: new Dictionary<string, string>
            {
                ["path"] = "37.7749 -122.4194 0 0;37.7750 -122.4195 0 0",
                ["name"] = "Short Path"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "LineString", geometryField: "path", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        LineString? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedGeometry = command.Attributes["geometry"] as LineString;
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedGeometry.Should().NotBeNull();
        capturedGeometry!.NumPoints.Should().Be(2);
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_GeotraceWithSinglePoint_DoesNotCreateGeometry()
    {
        // Arrange: Single point cannot form a LineString
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-single",
            layerId: "trails",
            serviceId: "recreation",
            data: new Dictionary<string, string>
            {
                ["path"] = "37.7749 -122.4194 0 0",
                ["name"] = "Invalid Path"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "LineString", geometryField: "path", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        object? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                command.Attributes.TryGetValue("geometry", out capturedGeometry);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedGeometry.Should().BeNull();
    }

    #endregion

    #region ProcessAsync - Direct Mode with Polygon Geometry

    [Fact]
    public async Task ProcessAsync_DirectMode_ValidGeoshapeSubmission_CreatesPolygon()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "parcel-001",
            layerId: "parcels",
            serviceId: "cadastre",
            data: new Dictionary<string, string>
            {
                ["boundary"] = "37.7749 -122.4194 0 0;37.7750 -122.4194 0 0;37.7750 -122.4195 0 0;37.7749 -122.4195 0 0",
                ["owner"] = "John Doe"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Polygon",
            geometryField: "boundary",
            openRosaMode: "direct");

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Polygon? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedGeometry = command.Attributes["geometry"] as Polygon;
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ResultType.Should().Be(SubmissionResultType.DirectPublished);
        capturedGeometry.Should().NotBeNull();
        capturedGeometry!.SRID.Should().Be(4326);
        capturedGeometry.Shell.NumPoints.Should().Be(5); // 4 points + 1 closing point
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_GeoshapeNotClosed_AutomaticallyClosesRing()
    {
        // Arrange: The submission doesn't close the ring, processor should close it
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "parcel-002",
            layerId: "parcels",
            serviceId: "cadastre",
            data: new Dictionary<string, string>
            {
                // Ring not closed - first and last points are different
                ["boundary"] = "37.7749 -122.4194 0 0;37.7750 -122.4194 0 0;37.7750 -122.4195 0 0;37.7749 -122.4195 0 0",
                ["owner"] = "Jane Doe"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Polygon", geometryField: "boundary", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Polygon? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedGeometry = command.Attributes["geometry"] as Polygon;
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedGeometry.Should().NotBeNull();
        capturedGeometry!.Shell.IsClosed.Should().BeTrue();
        capturedGeometry.Shell.NumPoints.Should().Be(5); // 4 + closing point
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_GeoshapeWithTwoPoints_DoesNotCreateGeometry()
    {
        // Arrange: Need at least 3 points for a polygon
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "parcel-invalid",
            layerId: "parcels",
            serviceId: "cadastre",
            data: new Dictionary<string, string>
            {
                ["boundary"] = "37.7749 -122.4194 0 0;37.7750 -122.4194 0 0",
                ["owner"] = "Invalid"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Polygon", geometryField: "boundary", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        object? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                command.Attributes.TryGetValue("geometry", out capturedGeometry);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedGeometry.Should().BeNull();
    }

    #endregion

    #region ProcessAsync - Invalid Geometry Formats

    [Fact]
    public async Task ProcessAsync_DirectMode_InvalidGeopointFormat_CreatesFeatureWithoutGeometry()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-invalid-geo",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "invalid coordinates",
                ["species"] = "Oak"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Point", geometryField: "geometry", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        object? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                command.Attributes.TryGetValue("geometry", out capturedGeometry);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedGeometry.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_EmptyGeometry_CreatesFeatureWithoutGeometry()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-empty-geo",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "",
                ["species"] = "Pine"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Point", geometryField: "geometry", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        object? capturedGeometry = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                command.Attributes.TryGetValue("geometry", out capturedGeometry);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedGeometry.Should().BeNull();
    }

    #endregion

    #region ProcessAsync - Staged Mode

    [Fact]
    public async Task ProcessAsync_StagedMode_ValidSubmission_SavesToRepository()
    {
        // Arrange
        var processor = CreateProcessor(withRepository: true);
        var xml = CreateXmlDocumentWithData(
            instanceId: "staged-001",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "field-worker",
            DeviceId = "tablet-005"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "staged");

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Submission? capturedSubmission = null;
        _mockSubmissionRepository.Setup(x => x.CreateAsync(
                It.IsAny<Submission>(),
                It.IsAny<CancellationToken>()))
            .Callback<Submission, CancellationToken>((sub, ct) => capturedSubmission = sub)
            .Returns(Task.CompletedTask);

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ResultType.Should().Be(SubmissionResultType.StagedForReview);
        result.InstanceId.Should().Be("staged-001");

        _mockSubmissionRepository.Verify(x => x.CreateAsync(
            It.IsAny<Submission>(),
            It.IsAny<CancellationToken>()), Times.Once);

        capturedSubmission.Should().NotBeNull();
        capturedSubmission!.InstanceId.Should().Be("staged-001");
        capturedSubmission.LayerId.Should().Be("trees");
        capturedSubmission.ServiceId.Should().Be("field-surveys");
        capturedSubmission.SubmittedBy.Should().Be("field-worker");
        capturedSubmission.DeviceId.Should().Be("tablet-005");
        capturedSubmission.Status.Should().Be(SubmissionStatus.Pending);
        capturedSubmission.Geometry.Should().BeOfType<Point>();
    }

    [Fact]
    public async Task ProcessAsync_StagedMode_WithoutRepository_ReturnsRejectedResult()
    {
        // Arrange: Processor created without repository for staged mode
        var processor = CreateProcessor(withRepository: false);
        var xml = CreateXmlDocumentWithData(
            instanceId: "staged-no-repo",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Point", geometryField: "geometry", openRosaMode: "staged");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("ISubmissionRepository");
    }

    [Fact]
    public async Task ProcessAsync_StagedMode_WithAttachments_SavesAttachmentMetadata()
    {
        // Arrange
        var processor = CreateProcessor(withRepository: true);
        var xml = CreateXmlDocumentWithData(
            instanceId: "staged-with-attachments",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak",
                ["photo"] = "tree_photo.jpg"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "field-worker",
            Attachments = new List<AttachmentFile>
            {
                new()
                {
                    Filename = "tree_photo.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = 102400,
                    OpenStreamAsync = ct => Task.FromResult(System.IO.Stream.Null)
                },
                new()
                {
                    Filename = "tree_measurements.csv",
                    ContentType = "text/csv",
                    SizeBytes = 2048,
                    OpenStreamAsync = ct => Task.FromResult(System.IO.Stream.Null)
                }
            }
        };

        var layer = CreateLayerDefinition(geometryType: "Point", geometryField: "geometry", openRosaMode: "staged");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Submission? capturedSubmission = null;
        _mockSubmissionRepository.Setup(x => x.CreateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()))
            .Callback<Submission, CancellationToken>((sub, ct) => capturedSubmission = sub)
            .Returns(Task.CompletedTask);

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedSubmission.Should().NotBeNull();
        capturedSubmission!.Attachments.Should().HaveCount(2);

        var photoAttachment = capturedSubmission.Attachments.First(a => a.Filename == "tree_photo.jpg");
        photoAttachment.ContentType.Should().Be("image/jpeg");
        photoAttachment.SizeBytes.Should().Be(102400);
        photoAttachment.StoragePath.Should().Be("openrosa/staged-with-attachments/tree_photo.jpg");

        var csvAttachment = capturedSubmission.Attachments.First(a => a.Filename == "tree_measurements.csv");
        csvAttachment.ContentType.Should().Be("text/csv");
        csvAttachment.SizeBytes.Should().Be(2048);
    }

    #endregion

    #region ProcessAsync - Unknown Mode

    [Fact]
    public async Task ProcessAsync_UnknownMode_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-unknown-mode",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "invalid-mode");

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("Unknown OpenRosa mode");
    }

    #endregion

    #region ProcessAsync - Field Value Parsing

    [Fact]
    public async Task ProcessAsync_DirectMode_ParsesIntegerFields_Correctly()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-int",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["dbh_cm"] = "42",
                ["age_years"] = "150"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct",
            fields: new List<FieldDefinition>
            {
                new() { Name = "dbh_cm", DataType = "int" },
                new() { Name = "age_years", DataType = "integer" }
            });

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Dictionary<string, object?>? capturedAttributes = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedAttributes = new Dictionary<string, object?>(command.Attributes);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedAttributes.Should().NotBeNull();
        capturedAttributes!["dbh_cm"].Should().Be(42);
        capturedAttributes["age_years"].Should().Be(150);
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_ParsesDoubleFields_Correctly()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-double",
            layerId: "measurements",
            serviceId: "surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["temperature"] = "23.5",
                ["humidity"] = "65.75"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct",
            fields: new List<FieldDefinition>
            {
                new() { Name = "temperature", DataType = "double" },
                new() { Name = "humidity", DataType = "float" }
            });

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Dictionary<string, object?>? capturedAttributes = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedAttributes = new Dictionary<string, object?>(command.Attributes);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedAttributes.Should().NotBeNull();
        capturedAttributes!["temperature"].Should().Be(23.5);
        capturedAttributes["humidity"].Should().Be(65.75);
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_ParsesBooleanFields_Correctly()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-bool",
            layerId: "inspections",
            serviceId: "surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["is_compliant"] = "true",
                ["needs_repair"] = "false"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct",
            fields: new List<FieldDefinition>
            {
                new() { Name = "is_compliant", DataType = "bool" },
                new() { Name = "needs_repair", DataType = "boolean" }
            });

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Dictionary<string, object?>? capturedAttributes = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedAttributes = new Dictionary<string, object?>(command.Attributes);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedAttributes.Should().NotBeNull();
        capturedAttributes!["is_compliant"].Should().Be(true);
        capturedAttributes["needs_repair"].Should().Be(false);
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_ParsesStringFields_Correctly()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-string",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Quercus robur",
                ["notes"] = "Healthy specimen"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct",
            fields: new List<FieldDefinition>
            {
                new() { Name = "species", DataType = "string" },
                new() { Name = "notes", DataType = "string" }
            });

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Dictionary<string, object?>? capturedAttributes = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedAttributes = new Dictionary<string, object?>(command.Attributes);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedAttributes.Should().NotBeNull();
        capturedAttributes!["species"].Should().Be("Quercus robur");
        capturedAttributes["notes"].Should().Be("Healthy specimen");
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_EmptyFieldValues_ResultInNull()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-empty",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "",
                ["dbh_cm"] = ""
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct",
            fields: new List<FieldDefinition>
            {
                new() { Name = "species", DataType = "string" },
                new() { Name = "dbh_cm", DataType = "int" }
            });

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Dictionary<string, object?>? capturedAttributes = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedAttributes = new Dictionary<string, object?>(command.Attributes);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedAttributes.Should().NotBeNull();
        capturedAttributes!["species"].Should().BeNull();
        capturedAttributes["dbh_cm"].Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_SkipsMetaElement()
    {
        // Arrange: Ensure meta element is not processed as an attribute
        var processor = CreateProcessor();
        var xml = CreateXmlDocument(instanceId: "test-meta", layerId: "trees", serviceId: "field-surveys");
        xml.Root!.Add(new XElement("geometry", "37.7749 -122.4194 0 0"));
        xml.Root.Add(new XElement("species", "Oak"));

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct",
            fields: new List<FieldDefinition>
            {
                new() { Name = "species", DataType = "string" }
            });

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Dictionary<string, object?>? capturedAttributes = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedAttributes = new Dictionary<string, object?>(command.Attributes);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedAttributes.Should().NotBeNull();
        capturedAttributes!.Should().ContainKey("species");
        capturedAttributes.Should().NotContainKey("meta");
    }

    [Fact]
    public async Task ProcessAsync_DirectMode_SkipsUnknownFields()
    {
        // Arrange: Fields not in layer definition should be skipped
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-unknown-field",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak",
                ["unknown_field"] = "some value"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(
            geometryType: "Point",
            geometryField: "geometry",
            openRosaMode: "direct",
            fields: new List<FieldDefinition>
            {
                new() { Name = "species", DataType = "string" }
            });

        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        Dictionary<string, object?>? capturedAttributes = null;
        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .Callback<FeatureEditBatch, CancellationToken>((batch, ct) =>
            {
                var command = (AddFeatureCommand)batch.Commands[0];
                capturedAttributes = new Dictionary<string, object?>(command.Attributes);
            })
            .ReturnsAsync(new FeatureEditBatchResult(new List<FeatureEditCommandResult>()));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        capturedAttributes.Should().NotBeNull();
        capturedAttributes!.Should().ContainKey("species");
        capturedAttributes.Should().NotContainKey("unknown_field");
    }

    #endregion

    #region ProcessAsync - Exception Handling

    [Fact]
    public async Task ProcessAsync_MetadataRegistryThrows_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-exception",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Metadata unavailable"));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("Submission processing failed");
        result.ErrorMessage.Should().Contain("Metadata unavailable");
    }

    [Fact]
    public async Task ProcessAsync_EditOrchestratorThrows_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor();
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-orchestrator-error",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Point", geometryField: "geometry", openRosaMode: "direct");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        _mockEditOrchestrator.Setup(x => x.ExecuteAsync(It.IsAny<FeatureEditBatch>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("Submission processing failed");
    }

    [Fact]
    public async Task ProcessAsync_SubmissionRepositoryThrows_ReturnsRejectedResult()
    {
        // Arrange
        var processor = CreateProcessor(withRepository: true);
        var xml = CreateXmlDocumentWithData(
            instanceId: "test-repo-error",
            layerId: "trees",
            serviceId: "field-surveys",
            data: new Dictionary<string, string>
            {
                ["geometry"] = "37.7749 -122.4194 0 0",
                ["species"] = "Oak"
            });

        var request = new SubmissionRequest
        {
            XmlDocument = xml,
            SubmittedBy = "test-user"
        };

        var layer = CreateLayerDefinition(geometryType: "Point", geometryField: "geometry", openRosaMode: "staged");
        var snapshot = CreateMetadataSnapshot(layer: layer);
        _mockMetadataRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        _mockSubmissionRepository.Setup(x => x.CreateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Repository save failed"));

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ResultType.Should().Be(SubmissionResultType.Rejected);
        result.ErrorMessage.Should().Contain("Submission processing failed");
    }

    #endregion

    #region Helper Methods

    private SubmissionProcessor CreateProcessor(bool withRepository = false)
    {
        return new SubmissionProcessor(
            _mockMetadataRegistry.Object,
            _mockEditOrchestrator.Object,
            withRepository ? _mockSubmissionRepository.Object : null);
    }

    private static XDocument CreateXmlDocument(string? instanceId, string? layerId, string? serviceId)
    {
        var doc = new XDocument(
            new XElement("data",
                new XAttribute("id", "test_form"),
                new XElement("meta",
                    instanceId != null ? new XElement("instanceID", instanceId) : null,
                    layerId != null ? new XElement("layerId", layerId) : null,
                    serviceId != null ? new XElement("serviceId", serviceId) : null,
                    new XElement("formDate", DateTimeOffset.UtcNow.ToString("o"))
                )
            )
        );

        return doc;
    }

    private static XDocument CreateXmlDocumentWithData(
        string? instanceId,
        string? layerId,
        string? serviceId,
        Dictionary<string, string> data)
    {
        var doc = CreateXmlDocument(instanceId, layerId, serviceId);

        foreach (var kvp in data)
        {
            doc.Root!.Add(new XElement(kvp.Key, kvp.Value));
        }

        return doc;
    }

    private static LayerDefinition CreateLayerDefinition(
        string geometryType = "Point",
        string geometryField = "geometry",
        bool openRosaEnabled = true,
        string openRosaMode = "direct",
        List<FieldDefinition>? fields = null)
    {
        return new LayerDefinition
        {
            Id = "trees",
            ServiceId = "field-surveys",
            Title = "Tree Inventory",
            GeometryType = geometryType,
            GeometryField = geometryField,
            IdField = "id",
            Fields = fields ?? new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "string" },
                new() { Name = "species", DataType = "string" },
                new() { Name = "dbh_cm", DataType = "int" }
            },
            OpenRosa = openRosaEnabled
                ? new OpenRosaLayerDefinition
                {
                    Enabled = true,
                    Mode = openRosaMode,
                    FormId = "tree_survey_v1",
                    FormVersion = "1.0.0"
                }
                : null
        };
    }

    private static MetadataSnapshot CreateMetadataSnapshot(
        LayerDefinition? layer = null,
        bool hasLayer = true)
    {
        var catalog = new CatalogDefinition { Id = "test-catalog", Title = "Test Catalog" };
        var dataSource = new DataSourceDefinition
        {
            Id = "test-ds",
            Provider = "postgis",
            ConnectionString = "Host=localhost;Database=test"
        };
        var service = new ServiceDefinition
        {
            Id = "field-surveys",
            Title = "Field Surveys",
            FolderId = "folder-1",
            ServiceType = "FeatureServer",
            DataSourceId = "test-ds"
        };

        var layers = hasLayer && layer != null
            ? new List<LayerDefinition> { layer }
            : new List<LayerDefinition>();

        return new MetadataSnapshot(
            catalog: catalog,
            folders: new List<FolderDefinition> { new() { Id = "folder-1", Title = "Test Folder" } },
            dataSources: new List<DataSourceDefinition> { dataSource },
            services: new List<ServiceDefinition> { service },
            layers: layers
        );
    }

    #endregion
}
