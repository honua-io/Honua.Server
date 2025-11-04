// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Consolidated picture of the workspace, infrastructure, and risk posture used when generating plans.
/// </summary>
public sealed record ConsultantPlanningContext(
    ConsultantRequest Request,
    WorkspaceProfile Workspace,
    IReadOnlyList<ConsultantObservation> Observations,
    DateTimeOffset GeneratedAt);

public sealed record WorkspaceProfile(
    string RootPath,
    bool MetadataDetected,
    MetadataProfile? Metadata,
    InfrastructureInventory Infrastructure,
    IReadOnlyList<string> Tags);

public sealed record MetadataProfile(
    IReadOnlyList<ServiceProfile> Services,
    IReadOnlyList<DataSourceProfile> DataSources,
    IReadOnlyList<RasterDatasetProfile> RasterDatasets);

public sealed record ServiceProfile(
    string Id,
    string ServiceType,
    bool Enabled,
    string? DataSourceId,
    IReadOnlyList<string> LayerIds,
    IReadOnlyList<string> Protocols);

public sealed record DataSourceProfile(
    string Id,
    string Provider,
    bool HasConnectionString,
    IReadOnlyList<string> ReferencedBy);

public sealed record RasterDatasetProfile(
    string Id,
    string SourceType,
    IReadOnlyList<string> LinkedServices,
    IReadOnlyList<string> Styles);

public sealed record InfrastructureInventory(
    bool HasDockerCompose,
    bool HasKubernetesManifests,
    bool HasTerraform,
    bool HasHelmCharts,
    bool HasCiPipelines,
    bool HasMonitoringConfig,
    IReadOnlyList<string> DeploymentArtifacts,
    IReadOnlyList<string> PotentialCloudProviders);

public sealed record ConsultantObservation(
    string Id,
    string Severity,
    string Summary,
    string Detail,
    string Recommendation);

public interface IConsultantContextBuilder
{
    Task<ConsultantPlanningContext> BuildAsync(ConsultantRequest request, CancellationToken cancellationToken);
}
