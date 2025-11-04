// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Styling;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.GeoservicesREST.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// GeoServices REST Feature Server controller - main file containing DI, fields, and configuration.
/// This partial class is split across multiple files:
/// - GeoservicesRESTFeatureServerController.cs (this file): DI, fields, constants
/// - GeoservicesRESTFeatureServerController.Metadata.cs: Service and layer metadata endpoints
/// - GeoservicesRESTFeatureServerController.Query.cs: Query and related records operations
/// - GeoservicesRESTFeatureServerController.Export.cs: Export operations (Shapefile, CSV, KML, etc.)
/// - GeoservicesRESTFeatureServerController.Helpers.cs: Utility methods and converters
/// </summary>
[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("rest/services/{folderId}/{serviceId}/FeatureServer")]
[Route("rest/services/{serviceId}/FeatureServer")] // Support URLs without folderId
public sealed partial class GeoservicesRESTFeatureServerController : ControllerBase
{
    private const double GeoServicesVersion = 10.81;
    private const int DefaultMaxRecordCount = 1000;
    private const string GlobalIdFieldName = "globalId";

    private static readonly string[] DefaultAddPropertyNames = { "adds" };
    private static readonly string[] DefaultUpdatePropertyNames = { "updates" };
    private static readonly string[] DefaultDeletePropertyNames = { "deletes" };
    private static readonly string[] AddFeaturesPropertyNames = { "features", "adds" };
    private static readonly string[] UpdateFeaturesPropertyNames = { "features", "updates" };
    private static readonly string[] DeleteFeaturesPropertyNames = { "objectIds", "deletes" };

    private readonly ICatalogProjectionService _catalog;
    private readonly IFeatureRepository _repository;
    private readonly IFeatureAttachmentOrchestrator _attachmentOrchestrator;
    private readonly IAttachmentStoreSelector _attachmentStoreSelector;
    private readonly IShapefileExporter _shapefileExporter;
    private readonly ICsvExporter _csvExporter;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IGeoservicesAuditLogger _auditLogger;
    private readonly IGeoservicesEditingService _editingService;
    private readonly IGeoservicesQueryService _queryService;
    private readonly StreamingKmlWriter _streamingKmlWriter;
    private readonly ILogger<GeoservicesRESTFeatureServerController> _logger;

    public GeoservicesRESTFeatureServerController(
        ICatalogProjectionService catalog,
        IFeatureRepository repository,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IAttachmentStoreSelector attachmentStoreSelector,
        IShapefileExporter shapefileExporter,
        ICsvExporter csvExporter,
        IMetadataRegistry metadataRegistry,
        IGeoservicesAuditLogger auditLogger,
        IGeoservicesQueryService queryService,
        IGeoservicesEditingService editingService,
        StreamingKmlWriter streamingKmlWriter,
        ILogger<GeoservicesRESTFeatureServerController> logger)
    {
        _catalog = Guard.NotNull(catalog);
        _repository = Guard.NotNull(repository);
        _attachmentOrchestrator = Guard.NotNull(attachmentOrchestrator);
        _attachmentStoreSelector = Guard.NotNull(attachmentStoreSelector);
        _shapefileExporter = Guard.NotNull(shapefileExporter);
        _csvExporter = Guard.NotNull(csvExporter);
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _auditLogger = Guard.NotNull(auditLogger);
        _queryService = Guard.NotNull(queryService);
        _editingService = Guard.NotNull(editingService);
        _streamingKmlWriter = Guard.NotNull(streamingKmlWriter);
        _logger = Guard.NotNull(logger);
    }
}
