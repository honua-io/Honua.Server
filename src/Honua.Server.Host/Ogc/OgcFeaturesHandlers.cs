// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Host.Attachments;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Observability;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using static Honua.Server.Core.Serialization.JsonLdFeatureFormatter;
using static Honua.Server.Core.Serialization.GeoJsonTFeatureFormatter;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// OGC API - Features handlers for collection operations, feature retrieval, search, and mutations.
/// This class is split into multiple partial files for maintainability:
/// - OgcFeaturesHandlers.Styles.cs: Style operations
/// - OgcFeaturesHandlers.Items.cs: Feature collection retrieval
/// - OgcFeaturesHandlers.Search.cs: Cross-collection search
/// - OgcFeaturesHandlers.Mutations.cs: POST/PUT/PATCH/DELETE operations
/// - OgcFeaturesHandlers.Schema.cs: Queryables and schema
/// - OgcFeaturesHandlers.Attachments.cs: Attachment handling
/// </summary>
internal static partial class OgcFeaturesHandlers
{
    // Shared utilities and helper methods will be maintained in this main file
    // Individual operations are organized in separate partial class files
}
