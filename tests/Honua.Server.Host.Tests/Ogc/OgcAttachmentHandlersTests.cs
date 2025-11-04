using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Tests.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public sealed class OgcAttachmentHandlersTests
{
    private static readonly OgcCacheHeaderService CacheHeaderService = new(Options.Create(new CacheHeaderOptions()));

    [Fact]
    public async Task GetCollectionItemAttachment_ReturnsFile_WhenAttachmentExists()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var (featureContext, collectionId) = CreateFeatureContext();

        var resolverMock = CreateResolverMock(featureContext);
        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>();
        var attachmentStoreSelectorMock = new Mock<IAttachmentStoreSelector>();
        var attachmentStoreMock = new Mock<IAttachmentStore>();

        const string featureId = "feature-1";
        const string attachmentId = "attachment-123";
        var descriptor = new AttachmentDescriptor
        {
            AttachmentObjectId = 7,
            AttachmentId = attachmentId,
            ServiceId = featureContext.Service.Id,
            LayerId = featureContext.Layer.Id,
            FeatureId = featureId,
            Name = "photo.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 3,
            ChecksumSha256 = "abc123",
            StorageProvider = "test-storage",
            StorageKey = "attachments/photo.jpg",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        orchestratorMock
            .Setup(x => x.GetAsync(featureContext.Service.Id, featureContext.Layer.Id, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);

        var contentBytes = new byte[] { 1, 2, 3 };
        attachmentStoreMock
            .Setup(x => x.TryGetAsync(It.Is<AttachmentPointer>(p =>
                    p.StorageProvider == descriptor.StorageProvider &&
                    p.StorageKey == descriptor.StorageKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentReadResult
            {
                Content = new MemoryStream(contentBytes, writable: false),
                MimeType = descriptor.MimeType,
                FileName = "photo-from-store.jpg",
                SizeBytes = contentBytes.Length,
                ChecksumSha256 = descriptor.ChecksumSha256
            });

        attachmentStoreSelectorMock
            .Setup(x => x.Resolve(featureContext.Layer.Attachments.StorageProfileId!))
            .Returns(attachmentStoreMock.Object);

        var expectedEtag = CacheHeaderService.GenerateETag(descriptor.ChecksumSha256);

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItemAttachment(
            collectionId,
            featureId,
            attachmentId,
            httpContext.Request,
            resolverMock.Object,
            orchestratorMock.Object,
            attachmentStoreSelectorMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        await result.ExecuteAsync(httpContext).ConfigureAwait(false);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.ContentType.Should().Be(descriptor.MimeType);
        httpContext.Response.Headers["Content-Disposition"].ToString().Should().Contain("photo.jpg");
        httpContext.Response.Headers["ETag"].ToString().Should().Be(expectedEtag);
        httpContext.Response.Headers["Cache-Control"].Should().NotBeNullOrEmpty();
        httpContext.Response.Headers["Last-Modified"].Should().NotBeNullOrEmpty();

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new MemoryStream();
        await httpContext.Response.Body.CopyToAsync(reader).ConfigureAwait(false);
        reader.ToArray().Should().Equal(contentBytes);
    }

    [Fact]
    public async Task GetCollectionItemAttachment_ReturnsNotFound_WhenAttachmentMissing()
    {
        // Arrange
        var (featureContext, collectionId) = CreateFeatureContext();
        var resolverMock = CreateResolverMock(featureContext);
        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>();
        orchestratorMock
            .Setup(x => x.GetAsync(featureContext.Service.Id, featureContext.Layer.Id, "missing-attachment", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttachmentDescriptor?)null);

        var attachmentStoreSelectorMock = new Mock<IAttachmentStoreSelector>(MockBehavior.Strict);

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItemAttachment(
            collectionId,
            "feature-1",
            "missing-attachment",
            new DefaultHttpContext().Request,
            resolverMock.Object,
            orchestratorMock.Object,
            attachmentStoreSelectorMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task GetCollectionItemAttachment_ReturnsNotFound_WhenFeatureIdDoesNotMatch()
    {
        // Arrange
        var (featureContext, collectionId) = CreateFeatureContext();
        var resolverMock = CreateResolverMock(featureContext);
        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>();

        var descriptor = new AttachmentDescriptor
        {
            AttachmentObjectId = 8,
            AttachmentId = "attachment-xyz",
            ServiceId = featureContext.Service.Id,
            LayerId = featureContext.Layer.Id,
            FeatureId = "different-feature",
            Name = "dataset.zip",
            MimeType = "application/zip",
            SizeBytes = 1024,
            ChecksumSha256 = "deadbeef",
            StorageProvider = "test-storage",
            StorageKey = "attachments/dataset.zip",
            CreatedUtc = DateTimeOffset.UtcNow
        };

        orchestratorMock
            .Setup(x => x.GetAsync(featureContext.Service.Id, featureContext.Layer.Id, descriptor.AttachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);

        var attachmentStoreSelectorMock = new Mock<IAttachmentStoreSelector>(MockBehavior.Strict);

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItemAttachment(
            collectionId,
            "feature-1",
            descriptor.AttachmentId,
            new DefaultHttpContext().Request,
            resolverMock.Object,
            orchestratorMock.Object,
            attachmentStoreSelectorMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task GetCollectionItemAttachment_ReturnsProblem_WhenStorageProfileMissing()
    {
        // Arrange
        var attachments = new LayerAttachmentDefinition
        {
            Enabled = true,
            StorageProfileId = null,
            ExposeOgcLinks = true
        };
        var (featureContext, collectionId) = CreateFeatureContext(attachments);
        var resolverMock = CreateResolverMock(featureContext);
        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>();
        var descriptor = CreateAttachmentDescriptor(featureContext, "feature-1", "attachment-42");

        orchestratorMock
            .Setup(x => x.GetAsync(featureContext.Service.Id, featureContext.Layer.Id, descriptor.AttachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);

        var attachmentStoreSelectorMock = new Mock<IAttachmentStoreSelector>(MockBehavior.Strict);

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItemAttachment(
            collectionId,
            descriptor.FeatureId,
            descriptor.AttachmentId,
            new DefaultHttpContext().Request,
            resolverMock.Object,
            orchestratorMock.Object,
            attachmentStoreSelectorMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.ProblemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.ProblemDetails.Title.Should().Be("Attachment download unavailable");
        problem.ProblemDetails.Detail.Should().Be("Attachment storage profile is not configured for this layer.");
    }

    [Fact]
    public async Task GetCollectionItemAttachment_ReturnsProblem_WhenStorageProfileCannotBeResolved()
    {
        // Arrange
        var (featureContext, collectionId) = CreateFeatureContext();
        var resolverMock = CreateResolverMock(featureContext);
        var descriptor = CreateAttachmentDescriptor(featureContext, "feature-1", "attachment-9000");

        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>();
        orchestratorMock
            .Setup(x => x.GetAsync(featureContext.Service.Id, featureContext.Layer.Id, descriptor.AttachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);

        var attachmentStoreSelectorMock = new Mock<IAttachmentStoreSelector>();
        attachmentStoreSelectorMock
            .Setup(x => x.Resolve(featureContext.Layer.Attachments.StorageProfileId!))
            .Throws(new AttachmentStoreNotFoundException(featureContext.Layer.Attachments.StorageProfileId!));

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItemAttachment(
            collectionId,
            descriptor.FeatureId,
            descriptor.AttachmentId,
            new DefaultHttpContext().Request,
            resolverMock.Object,
            orchestratorMock.Object,
            attachmentStoreSelectorMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.ProblemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.ProblemDetails.Title.Should().Be("Attachment download unavailable");
        problem.ProblemDetails.Detail.Should().Be("Attachment storage profile could not be resolved.");
    }

    private static Mock<IFeatureContextResolver> CreateResolverMock(FeatureContext context)
    {
        var resolverMock = new Mock<IFeatureContextResolver>();
        resolverMock
            .Setup(x => x.ResolveAsync(context.Service.Id, context.Layer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        return resolverMock;
    }

    private static (FeatureContext Context, string CollectionId) CreateFeatureContext(LayerAttachmentDefinition? attachmentsOverride = null)
    {
        const string folderId = "default-folder";
        const string dataSourceId = "datasource-1";
        const string serviceId = "service-1";
        const string layerId = "layer-1";

        var dataSource = new DataSourceDefinition
        {
            Id = dataSourceId,
            Provider = "test-provider",
            ConnectionString = "Host=localhost;Database=test"
        };

        var metadata = new MetadataSnapshotBuilder()
            .WithFolder(folderId)
            .WithDataSource(dataSource)
            .WithService(serviceId, folderId, dataSourceId, "Test Service", configure: s =>
                s.WithOgc(o => o.WithCollectionsEnabled(true)))
            .WithLayer(layerId, serviceId, "Layer 1", configure: l =>
            {
                if (attachmentsOverride != null)
                {
                    l.WithAttachments(attachmentsOverride.StorageProfileId ?? "store-profile",
                        attachmentsOverride.Enabled,
                        attachmentsOverride.ExposeOgcLinks,
                        attachmentsOverride.MaxSizeMiB);
                }
                else
                {
                    l.WithAttachments("store-profile", enabled: true, exposeOgcLinks: true);
                }
            })
            .Build();

        var resolvedService = metadata.Services[0];
        var resolvedLayer = resolvedService.Layers[0];
        var provider = new Mock<IDataStoreProvider>(MockBehavior.Strict).Object;
        var context = new FeatureContext(metadata, resolvedService, resolvedLayer, dataSource, provider);
        var collectionId = $"{resolvedService.Id}::{resolvedLayer.Id}";
        return (context, collectionId);
    }

    private static AttachmentDescriptor CreateAttachmentDescriptor(FeatureContext context, string featureId, string attachmentId)
    {
        return new AttachmentDescriptor
        {
            AttachmentObjectId = 1,
            AttachmentId = attachmentId,
            ServiceId = context.Service.Id,
            LayerId = context.Layer.Id,
            FeatureId = featureId,
            Name = "report.pdf",
            MimeType = "application/pdf",
            SizeBytes = 2048,
            ChecksumSha256 = "cafebabe",
            StorageProvider = "test-storage",
            StorageKey = $"attachments/{attachmentId}.pdf",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
    }
}
