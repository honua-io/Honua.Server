// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Aggregates workspace signals, metadata, and infrastructure inventory for consultant planning.
/// </summary>
public sealed class ConsultantContextBuilder : IConsultantContextBuilder
{
    private static readonly string[] MetadataCandidates =
    {
        "metadata.yaml",
        "metadata.yml",
        "metadata.json",
        Path.Combine("config", "metadata.yaml"),
        Path.Combine("config", "metadata.yml"),
        Path.Combine("config", "metadata.json"),
        Path.Combine("metadata", "metadata.yaml"),
        Path.Combine("metadata", "metadata.yml"),
        Path.Combine("metadata", "metadata.json")
    };

    public async Task<ConsultantPlanningContext> BuildAsync(ConsultantRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var workspacePath = request.WorkspacePath.IsNullOrWhiteSpace()
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(request.WorkspacePath);

        var metadataProfile = await TryLoadMetadataAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        var infrastructure = ScanInfrastructure(workspacePath);
        var observations = BuildObservations(metadataProfile, infrastructure, workspacePath);
        var tags = BuildTags(metadataProfile, infrastructure);

        var workspaceProfile = new WorkspaceProfile(
            RootPath: workspacePath,
            MetadataDetected: metadataProfile != null,
            Metadata: metadataProfile,
            Infrastructure: infrastructure,
            Tags: tags);

        return new ConsultantPlanningContext(
            request,
            workspaceProfile,
            observations,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<ConsultantObservation> BuildObservations(
        MetadataProfile? metadata,
        InfrastructureInventory infrastructure,
        string workspacePath)
    {
        var observations = new List<ConsultantObservation>();

        if (metadata is null)
        {
            observations.Add(new ConsultantObservation(
                "metadata_missing",
                "high",
                "Metadata catalogue not found",
                "No Honua metadata catalogue was detected in the workspace. Without metadata the platform cannot expose OGC services or STAC collections.",
                "Create or import a metadata.yaml or metadata.json file and commit it to source control."));
        }
        else if (metadata.Services.Count == 0)
        {
            observations.Add(new ConsultantObservation(
                "metadata_empty",
                "medium",
                "Metadata catalogue does not define any services",
                "The metadata file was found but does not declare any services or layers.",
                "Run the metadata scaffolding commands or import existing services before deploying."));
        }
        else
        {
            var disabledServices = metadata.Services.Where(s => !s.Enabled).Select(s => s.Id).ToArray();
            if (disabledServices.Length == metadata.Services.Count)
            {
                observations.Add(new ConsultantObservation(
                    "metadata_services_disabled",
                    "medium",
                    "All services in metadata are disabled",
                    "Each service entry is currently disabled. Deployments will expose no APIs.",
                    "Enable the appropriate services in metadata or mark intentionally disabled ones with documentation."));
            }

            var orphanedLayers = metadata.Services
                .SelectMany(s => s.LayerIds.Select(layer => (service: s, layer)))
                .Where(tuple => metadata.RasterDatasets.All(r => r.Id != tuple.layer))
                .ToList();
            if (orphanedLayers.Count > 0)
            {
                observations.Add(new ConsultantObservation(
                    "metadata_orphan_layers",
                    "low",
                    "Some layers do not link to datasets",
                    $"Layers without backing data: {string.Join(", ", orphanedLayers.Select(o => $"{o.service.Id}:{o.layer}"))}",
                    "Review metadata to ensure each layer references a configured data source or raster dataset."));
            }

            var insecureConnections = metadata.DataSources
                .Where(ds => !ds.HasConnectionString)
                .Select(ds => ds.Id)
                .ToArray();
            if (insecureConnections.Length > 0)
            {
                observations.Add(new ConsultantObservation(
                    "datasource_missing_credentials",
                    "high",
                    "One or more data sources do not define a connection string",
                    $"Data sources missing credentials: {string.Join(", ", insecureConnections)}",
                    "Populate secure connection strings (preferably using environment variables or secret stores)."));
            }
        }

        if (!infrastructure.HasMonitoringConfig)
        {
            observations.Add(new ConsultantObservation(
                "monitoring_missing",
                "medium",
                "No observability configuration detected",
                "The workspace does not define Prometheus/Grafana dashboards or logging sinks.",
                "Add monitoring configuration (Prometheus scrape config, Grafana dashboards, log shipping) before production deployment."));
        }

        if (!infrastructure.HasTerraform && !infrastructure.HasKubernetesManifests && !infrastructure.HasDockerCompose)
        {
            observations.Add(new ConsultantObservation(
                "deployment_artifacts_missing",
                "medium",
                "Deployment artifacts not detected",
                "No Terraform, Kubernetes manifests, or docker-compose files were found.",
                "Generate deployment blueprints (docker-compose for local dev, Helm/Terraform for cloud environments)."));
        }

        if (!Directory.Exists(Path.Combine(workspacePath, ".github")) && infrastructure.HasCiPipelines == false)
        {
            observations.Add(new ConsultantObservation(
                "ci_missing",
                "low",
                "Continuous integration pipeline not detected",
                "Automated testing or deployment workflows were not found.",
                "Add CI/CD workflows to lint metadata, validate schemas, and run automated integration tests."));
        }

        return observations;
    }

    private static IReadOnlyList<string> BuildTags(MetadataProfile? metadata, InfrastructureInventory infrastructure)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (metadata != null)
        {
            if (metadata.Services.Any(s => s.Protocols.Contains("ogcapi-features", StringComparer.OrdinalIgnoreCase)))
            {
                tags.Add("ogc-api");
            }

            if (metadata.RasterDatasets.Count > 0)
            {
                tags.Add("raster");
            }
        }

        if (infrastructure.PotentialCloudProviders.Count > 0)
        {
            foreach (var provider in infrastructure.PotentialCloudProviders)
            {
                tags.Add(provider);
            }
        }

        if (infrastructure.HasTerraform)
        {
            tags.Add("terraform");
        }

        if (infrastructure.HasKubernetesManifests)
        {
            tags.Add("kubernetes");
        }

        return tags.ToArray();
    }

    private static InfrastructureInventory ScanInfrastructure(string workspacePath)
    {
        bool HasFiles(string pattern)
        {
            try
            {
                return Directory.EnumerateFiles(workspacePath, pattern, SearchOption.AllDirectories).Any();
            }
            catch (Exception)
            {
                return false;
            }
        }

        bool hasDockerCompose = HasFiles("docker-compose*.yml") || HasFiles("docker-compose*.yaml");
        bool hasKubernetes = HasFiles("*.k8s.yaml") || HasFiles("kubernetes*.yaml") || Directory.Exists(Path.Combine(workspacePath, "k8s"));
        bool hasTerraform = HasFiles("*.tf");
        bool hasHelm = Directory.Exists(Path.Combine(workspacePath, "charts")) || HasFiles("Chart.yaml");
        bool hasCi = Directory.Exists(Path.Combine(workspacePath, ".github", "workflows")) || Directory.Exists(Path.Combine(workspacePath, ".gitlab", "ci"));
        bool hasMonitoring = HasFiles("prometheus.yml") || HasFiles("grafana-dashboard*.json") || HasFiles("otel-collector*.yaml") || HasFiles("observability.json");

        var deploymentArtifacts = new List<string>();
        if (hasDockerCompose)
        {
            deploymentArtifacts.Add("docker-compose");
        }
        if (hasKubernetes)
        {
            deploymentArtifacts.Add("kubernetes");
        }
        if (hasTerraform)
        {
            deploymentArtifacts.Add("terraform");
        }
        if (hasHelm)
        {
            deploymentArtifacts.Add("helm");
        }

        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (HasFiles("aws-*.tf"))
        {
            providers.Add("aws");
        }
        if (HasFiles("azurerm_*.tf"))
        {
            providers.Add("azure");
        }
        if (HasFiles("google_*.tf"))
        {
            providers.Add("gcp");
        }

        return new InfrastructureInventory(
            HasDockerCompose: hasDockerCompose,
            HasKubernetesManifests: hasKubernetes,
            HasTerraform: hasTerraform,
            HasHelmCharts: hasHelm,
            HasCiPipelines: hasCi,
            HasMonitoringConfig: hasMonitoring,
            DeploymentArtifacts: deploymentArtifacts,
            PotentialCloudProviders: providers.ToArray());
    }

    private static async Task<MetadataProfile?> TryLoadMetadataAsync(string workspacePath, CancellationToken cancellationToken)
    {
        foreach (var relativePath in MetadataCandidates)
        {
            var candidate = Path.Combine(workspacePath, relativePath);
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                var extension = Path.GetExtension(candidate).ToLowerInvariant();
                MetadataSnapshot snapshot;

                // TODO: Update to use HclMetadataProvider with Configuration V2
                // JsonMetadataProvider and YamlMetadataProvider have been removed
                throw new NotSupportedException(
                    "ConsultantContextBuilder requires migration to Configuration V2. " +
                    "Legacy metadata providers (JSON/YAML) have been removed. Use HclMetadataProvider instead.");

                return BuildMetadataProfile(snapshot);
            }
            catch (Exception)
            {
                // ignored - fall back to next candidate
            }
        }

        return null;
    }

    private static MetadataProfile BuildMetadataProfile(MetadataSnapshot snapshot)
    {
        var services = snapshot.Services.Select(service => new ServiceProfile(
            service.Id,
            service.ServiceType,
            service.Enabled,
            service.DataSourceId,
            service.Layers.Select(layer => layer.Id).ToArray(),
            BuildProtocolList(service))).ToArray();

        var dataSources = snapshot.DataSources.Select(ds => new DataSourceProfile(
            ds.Id,
            ds.Provider,
            ds.ConnectionString.HasValue(),
            services.Where(s => string.Equals(s.DataSourceId, ds.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Id)
                    .ToArray())).ToArray();

        var rasterDatasets = snapshot.RasterDatasets.Select(raster => new RasterDatasetProfile(
            raster.Id,
            raster.Source.Type,
            services.Where(s => s.LayerIds.Contains(raster.Id, StringComparer.OrdinalIgnoreCase)).Select(s => s.Id).ToArray(),
            raster.Styles.StyleIds.ToArray())).ToArray();

        return new MetadataProfile(services, dataSources, rasterDatasets);
    }

    private static IReadOnlyList<string> BuildProtocolList(ServiceDefinition service)
    {
        var protocols = new List<string>();

        if (service.Ogc.CollectionsEnabled)
        {
            protocols.Add("ogcapi-features");
        }
        if (service.Ogc.WmtsEnabled)
        {
            protocols.Add("ogcapi-tiles");
        }
        if (service.Ogc.WmsEnabled)
        {
            protocols.Add("wms");
        }
        if (service.Ogc.WfsEnabled)
        {
            protocols.Add("wfs");
        }
        if (service.Layers.Any(layer => layer.Stac?.Enabled == true))
        {
            protocols.Add("stac");
        }

        return protocols;
    }
}
