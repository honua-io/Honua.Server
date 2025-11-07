// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcFeaturesAttachmentHandlerTests
{
    private readonly OgcFeaturesAttachmentHandler _handler;

    public OgcFeaturesAttachmentHandlerTests()
    {
        _handler = new OgcFeaturesAttachmentHandler();
    }

    [Fact]
    public void ShouldExposeAttachmentLinks_WithDisabledAttachments_ReturnsFalse()
    {
        // Arrange
        var service = new ServiceDefinition { Id = "service1" };
        var layer = new LayerDefinition
        {
            Id = "layer1",
            Attachments = new AttachmentConfiguration { Enabled = false, ExposeOgcLinks = true }
        };

        // Act
        var result = _handler.ShouldExposeAttachmentLinks(service, layer);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldExposeAttachmentLinks_WithEnabledAttachmentsButNoOgcLinks_ReturnsFalse()
    {
        // Arrange
        var service = new ServiceDefinition { Id = "service1" };
        var layer = new LayerDefinition
        {
            Id = "layer1",
            Attachments = new AttachmentConfiguration { Enabled = true, ExposeOgcLinks = false }
        };

        // Act
        var result = _handler.ShouldExposeAttachmentLinks(service, layer);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldExposeAttachmentLinks_WithEnabledAttachmentsAndOgcLinks_ReturnsTrue()
    {
        // Arrange
        var service = new ServiceDefinition { Id = "service1" };
        var layer = new LayerDefinition
        {
            Id = "layer1",
            Attachments = new AttachmentConfiguration { Enabled = true, ExposeOgcLinks = true }
        };

        // Act
        var result = _handler.ShouldExposeAttachmentLinks(service, layer);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldExposeAttachmentLinks_WithRootService_ReturnsTrue()
    {
        // Arrange - root service without FolderId
        var service = new ServiceDefinition { Id = "service1", FolderId = null };
        var layer = new LayerDefinition
        {
            Id = "layer1",
            Attachments = new AttachmentConfiguration { Enabled = true, ExposeOgcLinks = true }
        };

        // Act
        var result = _handler.ShouldExposeAttachmentLinks(service, layer);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ResolveLayerIndex_WithLayerInService_ReturnsCorrectIndex()
    {
        // Arrange
        var layer1 = new LayerDefinition { Id = "layer1" };
        var layer2 = new LayerDefinition { Id = "layer2" };
        var layer3 = new LayerDefinition { Id = "layer3" };
        var service = new ServiceDefinition
        {
            Id = "service1",
            Layers = new List<LayerDefinition> { layer1, layer2, layer3 }
        };

        // Act
        var index = _handler.ResolveLayerIndex(service, layer2);

        // Assert
        Assert.Equal(1, index);
    }

    [Fact]
    public void ResolveLayerIndex_WithLayerNotInService_ReturnsMinusOne()
    {
        // Arrange
        var layer1 = new LayerDefinition { Id = "layer1" };
        var layer2 = new LayerDefinition { Id = "layer2" };
        var service = new ServiceDefinition
        {
            Id = "service1",
            Layers = new List<LayerDefinition> { layer1 }
        };

        // Act
        var index = _handler.ResolveLayerIndex(service, layer2);

        // Assert
        Assert.Equal(-1, index);
    }

    [Fact]
    public void ResolveLayerIndex_WithNullLayers_ReturnsMinusOne()
    {
        // Arrange
        var layer = new LayerDefinition { Id = "layer1" };
        var service = new ServiceDefinition
        {
            Id = "service1",
            Layers = null
        };

        // Act
        var index = _handler.ResolveLayerIndex(service, layer);

        // Assert
        Assert.Equal(-1, index);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithNoFeatureId_ReturnsEmptyList()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var service = new ServiceDefinition { Id = "service1" };
        var layer = new LayerDefinition { Id = "layer1" };
        var components = new FeatureComponents(
            FeatureId: null,
            RawId: null,
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            CancellationToken.None);

        // Assert
        Assert.Empty(links);
        mockOrchestrator.Verify(o => o.ListAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithNoAttachments_ReturnsEmptyList()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var service = new ServiceDefinition { Id = "service1" };
        var layer = new LayerDefinition { Id = "layer1" };
        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();
        mockOrchestrator.Setup(o => o.ListAsync("service1", "layer1", "123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AttachmentDescriptor>());

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            CancellationToken.None);

        // Assert
        Assert.Empty(links);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithAttachments_ReturnsLinks()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var service = new ServiceDefinition { Id = "service1", Layers = new List<LayerDefinition>() };
        var layer = new LayerDefinition { Id = "layer1" };
        service.Layers.Add(layer);

        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var descriptors = new List<AttachmentDescriptor>
        {
            new AttachmentDescriptor
            {
                FeatureId = "123",
                AttachmentId = "att1",
                AttachmentObjectId = 1,
                Name = "photo.jpg",
                MimeType = "image/jpeg"
            }
        };

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();
        mockOrchestrator.Setup(o => o.ListAsync("service1", "layer1", "123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptors);

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            CancellationToken.None);

        // Assert
        Assert.Single(links);
        Assert.Equal("enclosure", links[0].Rel);
        Assert.Equal("image/jpeg", links[0].Type);
        Assert.Equal("photo.jpg", links[0].Title);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithPreloadedDescriptors_DoesNotCallOrchestrator()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var service = new ServiceDefinition { Id = "service1", Layers = new List<LayerDefinition>() };
        var layer = new LayerDefinition { Id = "layer1" };
        service.Layers.Add(layer);

        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var descriptors = new List<AttachmentDescriptor>
        {
            new AttachmentDescriptor
            {
                FeatureId = "123",
                AttachmentId = "att1",
                AttachmentObjectId = 1,
                Name = "photo.jpg",
                MimeType = "image/jpeg"
            }
        };

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            descriptors,
            CancellationToken.None);

        // Assert
        Assert.Single(links);
        mockOrchestrator.Verify(o => o.ListAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithMissingMimeType_UsesDefault()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var service = new ServiceDefinition { Id = "service1", Layers = new List<LayerDefinition>() };
        var layer = new LayerDefinition { Id = "layer1" };
        service.Layers.Add(layer);

        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var descriptors = new List<AttachmentDescriptor>
        {
            new AttachmentDescriptor
            {
                FeatureId = "123",
                AttachmentId = "att1",
                AttachmentObjectId = 1,
                Name = "document",
                MimeType = null
            }
        };

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            descriptors,
            CancellationToken.None);

        // Assert
        Assert.Single(links);
        Assert.Equal("application/octet-stream", links[0].Type);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithMissingName_UsesDefaultTitle()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var service = new ServiceDefinition { Id = "service1", Layers = new List<LayerDefinition>() };
        var layer = new LayerDefinition { Id = "layer1" };
        service.Layers.Add(layer);

        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var descriptors = new List<AttachmentDescriptor>
        {
            new AttachmentDescriptor
            {
                FeatureId = "123",
                AttachmentId = "att1",
                AttachmentObjectId = 5,
                Name = null,
                MimeType = "image/jpeg"
            }
        };

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            descriptors,
            CancellationToken.None);

        // Assert
        Assert.Single(links);
        Assert.Equal("Attachment 5", links[0].Title);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithMultipleAttachments_ReturnsAllLinks()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        var service = new ServiceDefinition { Id = "service1", Layers = new List<LayerDefinition>() };
        var layer = new LayerDefinition { Id = "layer1" };
        service.Layers.Add(layer);

        var components = new FeatureComponents(
            FeatureId: "123",
            RawId: "123",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var descriptors = new List<AttachmentDescriptor>
        {
            new AttachmentDescriptor
            {
                FeatureId = "123",
                AttachmentId = "att1",
                AttachmentObjectId = 1,
                Name = "photo1.jpg",
                MimeType = "image/jpeg"
            },
            new AttachmentDescriptor
            {
                FeatureId = "123",
                AttachmentId = "att2",
                AttachmentObjectId = 2,
                Name = "photo2.png",
                MimeType = "image/png"
            },
            new AttachmentDescriptor
            {
                FeatureId = "123",
                AttachmentId = "att3",
                AttachmentObjectId = 3,
                Name = "document.pdf",
                MimeType = "application/pdf"
            }
        };

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            descriptors,
            CancellationToken.None);

        // Assert
        Assert.Equal(3, links.Count);
        Assert.All(links, link => Assert.Equal("enclosure", link.Rel));
    }

    [Fact]
    public void ResolveLayerIndex_WithCaseInsensitiveMatch_ReturnsIndex()
    {
        // Arrange
        var layer1 = new LayerDefinition { Id = "Layer1" };
        var layer2 = new LayerDefinition { Id = "LAYER2" };
        var service = new ServiceDefinition
        {
            Id = "service1",
            Layers = new List<LayerDefinition> { layer1, layer2 }
        };
        var searchLayer = new LayerDefinition { Id = "layer2" }; // lowercase

        // Act
        var index = _handler.ResolveLayerIndex(service, searchLayer);

        // Assert
        Assert.Equal(1, index);
    }

    [Fact]
    public async Task CreateAttachmentLinksAsync_WithEmptyFeatureId_ReturnsEmptyList()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var service = new ServiceDefinition { Id = "service1" };
        var layer = new LayerDefinition { Id = "layer1" };
        var components = new FeatureComponents(
            FeatureId: "",
            RawId: "",
            Properties: new Dictionary<string, object?>(),
            Geometry: null);

        var mockOrchestrator = new Mock<IFeatureAttachmentOrchestrator>();

        // Act
        var links = await _handler.CreateAttachmentLinksAsync(
            context.Request,
            service,
            layer,
            "collection1",
            components,
            mockOrchestrator.Object,
            CancellationToken.None);

        // Assert
        Assert.Empty(links);
    }
}
