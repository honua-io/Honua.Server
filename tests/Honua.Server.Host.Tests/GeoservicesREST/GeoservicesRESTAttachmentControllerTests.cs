using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.GeoservicesREST.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Speed", "Fast")]
public sealed class GeoservicesRESTAttachmentControllerTests
{
    private const string ServiceId = "transportation";
    private const string LayerId = "roads";

    [Fact]
    public async Task QueryAttachmentsAsync_ShouldReturnAttachmentMetadata()
    {
        // Arrange
        var (controller, httpContext, catalogMock, orchestratorMock) = CreateControllerWithContext(attachmentsEnabled: true);

        var descriptors = new List<AttachmentDescriptor>
        {
            CreateDescriptor(attachmentObjectId: 10, attachmentId: "att-1", featureId: "1", featureGlobalId: "guid-1"),
            CreateDescriptor(attachmentObjectId: 11, attachmentId: "att-2", featureId: "1", featureGlobalId: "guid-1")
        };

        orchestratorMock
            .Setup(o => o.ListAsync(ServiceId, LayerId, "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptors);

        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");
        httpContext.Request.QueryString = new QueryString("?objectIds=1");

        // Act
        var result = await controller.QueryAttachmentsAsync(null, ServiceId, 0, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<GeoservicesQueryAttachmentsResponse>().Subject;

        payload.HasAttachments.Should().BeTrue();
        payload.AttachmentGroups.Should().ContainSingle(group => group.ObjectId == 1);
        var group = payload.AttachmentGroups.First();
        group.GlobalId.Should().Be("guid-1");
        group.AttachmentInfos.Should().HaveCount(2);
        group.AttachmentInfos.Select(info => info.Id).Should().BeEquivalentTo(new[] { 10, 11 });
        group.AttachmentInfos.All(info => info.Url.Contains("/rest/services/transportation/FeatureServer/0/1/attachments/")).Should().BeTrue();

        orchestratorMock.VerifyAll();
        catalogMock.VerifyAll();
    }

    [Fact]
    public async Task QueryAttachmentsAsync_WhenAttachmentsDisabled_ShouldReturnBadRequest()
    {
        // Arrange
        var (controller, httpContext, _, _) = CreateControllerWithContext(attachmentsEnabled: false);
        httpContext.Request.QueryString = new QueryString("?objectIds=1");

        // Act
        var result = await controller.QueryAttachmentsAsync(null, ServiceId, 0, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddAttachmentAsync_ShouldForwardUploadToOrchestrator()
    {
        // Arrange
        var (controller, httpContext, catalogMock, orchestratorMock) = CreateControllerWithContext(attachmentsEnabled: true);

        var fileBytes = Encoding.UTF8.GetBytes("hello world");
        var stream = new MemoryStream(fileBytes);
        var formFile = new FormFile(stream, 0, stream.Length, "attachment", "notes.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var formFields = new Dictionary<string, StringValues>
        {
            ["objectId"] = "1",
            ["globalId"] = "guid-1"
        };

        var files = new FormFileCollection { formFile };
        var form = new FormCollection(formFields, files);

        httpContext.Request.ContentType = "multipart/form-data; boundary=----test";
        httpContext.Features.Set<IFormFeature>(new FormFeature(form));
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }, "Test"));

        FeatureAttachmentOperationResult? capturedResult = null;
        orchestratorMock
            .Setup(o => o.AddAsync(It.IsAny<AddFeatureAttachmentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AddFeatureAttachmentRequest, CancellationToken>((request, _) =>
            {
                request.ServiceId.Should().Be(ServiceId);
                request.LayerId.Should().Be(LayerId);
                request.FeatureId.Should().Be("1");
                request.FeatureGlobalId.Should().Be("guid-1");
            })
            .ReturnsAsync(() =>
            {
                capturedResult = FeatureAttachmentOperationResult.SuccessResult(
                    FeatureAttachmentOperation.Add,
                    CreateDescriptor(attachmentObjectId: 99, attachmentId: "att-new", featureId: "1", featureGlobalId: "guid-1"));
                return capturedResult;
            });

        // Act
        var result = await controller.AddAttachmentAsync(null, ServiceId, 0, CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<GeoservicesAddAttachmentResponse>().Subject;
        payload.Result.Success.Should().BeTrue();
        payload.Result.Id.Should().Be(99);
        payload.Result.GlobalId.Should().Be("att-new");

        capturedResult.Should().NotBeNull();
        orchestratorMock.VerifyAll();
        catalogMock.VerifyAll();
    }

    #region Test Infrastructure

    private static (GeoservicesRESTFeatureServerController Controller,
        DefaultHttpContext HttpContext,
        Mock<ICatalogProjectionService> CatalogMock,
        Mock<IFeatureAttachmentOrchestrator> OrchestratorMock)
        CreateControllerWithContext(bool attachmentsEnabled)
    {
        var layer = CreateLayerWithAttachments(attachmentsEnabled);
        var layerView = GeoservicesTestFactory.CreateLayerView(layer);
        var serviceDefinition = GeoservicesTestFactory.CreateServiceDefinition(layers: new[] { layer }) with
        {
            Id = ServiceId
        };
        var serviceView = GeoservicesTestFactory.CreateServiceView(serviceDefinition, new[] { layerView });

        var catalogMock = new Mock<ICatalogProjectionService>(MockBehavior.Strict);
        catalogMock
            .Setup(c => c.GetService(It.Is<string>(id => id.Equals(ServiceId, StringComparison.OrdinalIgnoreCase))))
            .Returns(serviceView);

        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>(MockBehavior.Strict);
        var repositoryMock = new Mock<IFeatureRepository>(MockBehavior.Strict);
        var attachmentStoreSelectorMock = new Mock<IAttachmentStoreSelector>(MockBehavior.Strict);
        var shapefileExporterMock = new Mock<IShapefileExporter>(MockBehavior.Strict);
        var csvExporterMock = new Mock<ICsvExporter>(MockBehavior.Strict);
        var metadataRegistryMock = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        var auditLoggerMock = new Mock<IGeoservicesAuditLogger>(MockBehavior.Strict);
        var queryServiceMock = new Mock<IGeoservicesQueryService>(MockBehavior.Strict);
        var editingServiceMock = new Mock<IGeoservicesEditingService>(MockBehavior.Strict);

        var streamingWriter = new StreamingKmlWriter(Mock.Of<ILogger<StreamingKmlWriter>>());

        var controller = new GeoservicesRESTFeatureServerController(
            catalogMock.Object,
            repositoryMock.Object,
            orchestratorMock.Object,
            attachmentStoreSelectorMock.Object,
            shapefileExporterMock.Object,
            csvExporterMock.Object,
            metadataRegistryMock.Object,
            auditLoggerMock.Object,
            queryServiceMock.Object,
            editingServiceMock.Object,
            streamingWriter,
            Mock.Of<ILogger<GeoservicesRESTFeatureServerController>>());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["controller"] = "GeoservicesRESTFeatureServer";
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            ActionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "GeoservicesRESTFeatureServer",
                ActionName = "QueryAttachments"
            }
        };

        return (controller, httpContext, catalogMock, orchestratorMock);
    }

    private static LayerDefinition CreateLayerWithAttachments(bool enabled)
    {
        var layer = GeoservicesTestFactory.CreateLayerDefinition();

        return layer with
        {
            ServiceId = ServiceId,
            Id = LayerId,
            Attachments = new LayerAttachmentDefinition
            {
                Enabled = enabled,
                StorageProfileId = "primary",
                RequireGlobalIds = false,
                ReturnPresignedUrls = false,
                ExposeOgcLinks = true
            }
        };
    }

    private static AttachmentDescriptor CreateDescriptor(int attachmentObjectId, string attachmentId, string featureId, string featureGlobalId)
    {
        return new AttachmentDescriptor
        {
            AttachmentObjectId = attachmentObjectId,
            AttachmentId = attachmentId,
            ServiceId = ServiceId,
            LayerId = LayerId,
            FeatureId = featureId,
            FeatureGlobalId = featureGlobalId,
            Name = $"{attachmentId}.bin",
            MimeType = "application/octet-stream",
            SizeBytes = 128,
            ChecksumSha256 = "abc123",
            StorageProvider = "default",
            StorageKey = $"attachments/{attachmentId}",
            CreatedUtc = DateTimeOffset.UtcNow
        };
    }

    #endregion
}
