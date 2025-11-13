// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Asp.Versioning;
using Honua.Server.Core.Models.Ifc;
using Honua.Server.Core.Services;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Host.API;

/// <summary>
/// IFC (Building Information Modeling) Import Controller
/// Provides endpoints for importing, validating, and extracting metadata from IFC files
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "RequireEditor")]
[Route("api/ifc")]
[Route("api/v{version:apiVersion}/ifc")]
[Produces("application/json")]
public sealed class IfcImportController : ControllerBase
{
    private readonly IIfcImportService ifcImportService;
    private readonly ILogger<IfcImportController> logger;

    // Maximum file size: 500 MB (configurable)
    private const long MaxFileSizeBytes = 500L * 1024 * 1024;

    public IfcImportController(
        IIfcImportService ifcImportService,
        ILogger<IfcImportController> logger)
    {
        this.ifcImportService = Guard.NotNull(ifcImportService);
        this.logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Import an IFC file into Honua
    /// </summary>
    /// <param name="file">IFC file to import (.ifc, .ifcxml, .ifczip)</param>
    /// <param name="targetServiceId">Target service ID to store features</param>
    /// <param name="targetLayerId">Target layer ID to store features</param>
    /// <param name="importGeometry">Whether to import 3D geometry</param>
    /// <param name="importProperties">Whether to import properties</param>
    /// <param name="importRelationships">Whether to import relationships</param>
    /// <param name="createGraphRelationships">Whether to create graph database relationships</param>
    /// <param name="maxEntities">Maximum number of entities to import (for testing)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Import result with statistics</returns>
    /// <response code="200">Import completed successfully</response>
    /// <response code="400">Invalid file or parameters</response>
    /// <response code="413">File too large</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("import")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(typeof(IfcImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IfcImportResult>> ImportIfcFile(
        IFormFile file,
        [FromForm] string targetServiceId,
        [FromForm] string targetLayerId,
        [FromForm] bool importGeometry = true,
        [FromForm] bool importProperties = true,
        [FromForm] bool importRelationships = true,
        [FromForm] bool createGraphRelationships = false,
        [FromForm] int? maxEntities = null,
        CancellationToken cancellationToken = default)
    {
        // Validate file
        if (file == null || file.Length == 0)
        {
            return this.BadRequest(new ProblemDetails
            {
                Title = "No file provided",
                Detail = "Please upload a valid IFC file",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
        {
            return this.StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Title = "File too large",
                Detail = $"Maximum file size is {MaxFileSizeBytes / (1024 * 1024)} MB",
                Status = StatusCodes.Status413PayloadTooLarge
            });
        }

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".ifc" && extension != ".ifcxml" && extension != ".ifczip")
        {
            return this.BadRequest(new ProblemDetails
            {
                Title = "Invalid file type",
                Detail = "Only .ifc, .ifcxml, and .ifczip files are supported",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(targetServiceId))
        {
            return this.BadRequest(new ProblemDetails
            {
                Title = "Missing target service ID",
                Detail = "targetServiceId is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (string.IsNullOrWhiteSpace(targetLayerId))
        {
            return this.BadRequest(new ProblemDetails
            {
                Title = "Missing target layer ID",
                Detail = "targetLayerId is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        this.logger.LogInformation(
            "Starting IFC import: File={FileName}, Size={Size} bytes, Service={ServiceId}, Layer={LayerId}",
            file.FileName, file.Length, targetServiceId, targetLayerId);

        // Create import options
        var options = new IfcImportOptions
        {
            TargetServiceId = targetServiceId,
            TargetLayerId = targetLayerId,
            ImportGeometry = importGeometry,
            ImportProperties = importProperties,
            ImportRelationships = importRelationships,
            CreateGraphRelationships = createGraphRelationships,
            MaxEntities = maxEntities
        };

        // Import the file
        IfcImportResult result;
        await using (var stream = file.OpenReadStream())
        {
            result = await this.ifcImportService.ImportIfcFileAsync(stream, options, cancellationToken);
        }

        if (result.Success)
        {
            this.logger.LogInformation(
                "IFC import completed: JobId={JobId}, FeaturesCreated={FeaturesCreated}, Duration={Duration}ms",
                result.ImportJobId, result.FeaturesCreated, result.Duration.TotalMilliseconds);
        }
        else
        {
            this.logger.LogWarning(
                "IFC import completed with errors: JobId={JobId}, Errors={ErrorCount}",
                result.ImportJobId, result.Errors.Count);
        }

        return this.Ok(result);
    }

    /// <summary>
    /// Validate an IFC file without importing it
    /// </summary>
    /// <param name="file">IFC file to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    /// <response code="200">Validation completed</response>
    /// <response code="400">Invalid file</response>
    [HttpPost("validate")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(typeof(IfcValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IfcValidationResult>> ValidateIfcFile(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return this.BadRequest(new ProblemDetails
            {
                Title = "No file provided",
                Detail = "Please upload a valid IFC file",
                Status = StatusCodes.Status400BadRequest
            });
        }

        this.logger.LogInformation("Validating IFC file: {FileName}, Size={Size} bytes",
            file.FileName, file.Length);

        IfcValidationResult result;
        await using (var stream = file.OpenReadStream())
        {
            result = await this.ifcImportService.ValidateIfcAsync(stream, cancellationToken);
        }

        this.logger.LogInformation("IFC validation completed: Valid={IsValid}, Schema={Schema}, Format={Format}",
            result.IsValid, result.SchemaVersion, result.FileFormat);

        return this.Ok(result);
    }

    /// <summary>
    /// Extract metadata from an IFC file without importing it
    /// </summary>
    /// <param name="file">IFC file to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IFC project metadata</returns>
    /// <response code="200">Metadata extracted successfully</response>
    /// <response code="400">Invalid file</response>
    [HttpPost("metadata")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(typeof(IfcProjectMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IfcProjectMetadata>> ExtractMetadata(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return this.BadRequest(new ProblemDetails
            {
                Title = "No file provided",
                Detail = "Please upload a valid IFC file",
                Status = StatusCodes.Status400BadRequest
            });
        }

        this.logger.LogInformation("Extracting metadata from IFC file: {FileName}", file.FileName);

        IfcProjectMetadata metadata;
        await using (var stream = file.OpenReadStream())
        {
            metadata = await this.ifcImportService.ExtractMetadataAsync(stream, cancellationToken);
        }

        this.logger.LogInformation("IFC metadata extracted: Project={ProjectName}, Schema={Schema}",
            metadata.ProjectName, metadata.SchemaVersion);

        return this.Ok(metadata);
    }

    /// <summary>
    /// Get supported IFC schema versions
    /// </summary>
    /// <returns>List of supported IFC versions</returns>
    /// <response code="200">List of supported versions</response>
    [HttpGet("versions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetSupportedVersions()
    {
        var versions = this.ifcImportService.GetSupportedSchemaVersions();
        return this.Ok(versions);
    }
}
