using Honua.Server.Core.Models.Ifc;

namespace Honua.Server.Core.Services;

/// <summary>
/// Service for importing IFC (Building Information Modeling) files into Honua
/// </summary>
public interface IIfcImportService
{
    /// <summary>
    /// Import an IFC file and create features in Honua
    /// </summary>
    /// <param name="ifcFileStream">Stream containing the IFC file data</param>
    /// <param name="options">Import options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Import result with statistics and any warnings/errors</returns>
    Task<IfcImportResult> ImportIfcFileAsync(
        Stream ifcFileStream,
        IfcImportOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate an IFC file without importing it
    /// </summary>
    /// <param name="ifcFileStream">Stream containing the IFC file data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<IfcValidationResult> ValidateIfcAsync(
        Stream ifcFileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract metadata from an IFC file without importing
    /// </summary>
    /// <param name="ifcFileStream">Stream containing the IFC file data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IFC project metadata</returns>
    Task<IfcProjectMetadata> ExtractMetadataAsync(
        Stream ifcFileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get supported IFC schema versions
    /// </summary>
    /// <returns>List of supported IFC versions</returns>
    IEnumerable<string> GetSupportedSchemaVersions();
}
