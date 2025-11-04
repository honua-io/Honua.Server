// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Service responsible for generating Docker Compose configurations.
/// </summary>
public sealed class DockerComposeConfigurationService
{
    private readonly ILogger<DockerComposeConfigurationService> _logger;

    public DockerComposeConfigurationService(ILogger<DockerComposeConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a Docker Compose configuration based on deployment analysis.
    /// </summary>
    public async Task<string> GenerateAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        _logger.LogDebug("Generating Docker Compose - RequiredServices count: {Count}", analysis.RequiredServices?.Count ?? 0);
        _logger.LogDebug("Generating Docker Compose - RequiredServices: {RequiredServices}", string.Join(", ", analysis.RequiredServices ?? new List<string>()));

        var hasPostgis = analysis.RequiredServices?.Any(s => s.Contains("postgis", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasMySQL = analysis.RequiredServices?.Any(s => s.Contains("mysql", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasSQLServer = analysis.RequiredServices?.Any(s => s.Contains("sql", StringComparison.OrdinalIgnoreCase) && s.Contains("server", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasRedis = analysis.RequiredServices?.Any(s => s.Contains("redis", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasNginx = analysis.RequiredServices?.Any(s => s.Contains("nginx", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasTraefik = analysis.RequiredServices?.Any(s => s.Contains("traefik", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasCaddy = analysis.RequiredServices?.Any(s => s.Contains("caddy", StringComparison.OrdinalIgnoreCase)) ?? false;

        // Observability services
        var hasPrometheus = analysis.RequiredServices?.Any(s => s.Contains("prometheus", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasGrafana = analysis.RequiredServices?.Any(s => s.Contains("grafana", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasAspireDashboard = analysis.RequiredServices?.Any(s => s.Contains("aspire", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasVictoriaMetrics = analysis.RequiredServices?.Any(s => s.Contains("victoria", StringComparison.OrdinalIgnoreCase)) ?? false;

        // Auto-detect observability stack from infrastructure needs
        var observabilityStack = analysis.InfrastructureNeeds?.ObservabilityStack?.ToLowerInvariant();
        if (observabilityStack == "prometheus-grafana")
        {
            hasPrometheus = true;
            hasGrafana = true;
        }
        else if (observabilityStack == "aspire-dashboard")
        {
            hasAspireDashboard = true;
        }
        else if (observabilityStack == "victoriametrics")
        {
            hasVictoriaMetrics = true;
        }

        _logger.LogDebug("Docker Compose service flags - Postgis: {HasPostgis}, MySQL: {HasMySQL}, SQLServer: {HasSQLServer}, Redis: {HasRedis}, Nginx: {HasNginx}",
            hasPostgis, hasMySQL, hasSQLServer, hasRedis, hasNginx);

        var builder = new StringBuilder();
        builder.AppendLine("version: '3.8'");
        builder.AppendLine();
        builder.AppendLine("services:");

        BuildHonuaService(builder, analysis, hasPostgis, hasMySQL, hasSQLServer, hasRedis);

        if (hasPostgis) BuildPostgisService(builder);
        if (hasMySQL) BuildMySQLService(builder);
        if (hasSQLServer) BuildSQLServerService(builder);
        if (hasRedis) BuildRedisService(builder);
        if (hasNginx) BuildNginxService(builder);
        if (hasTraefik) BuildTraefikService(builder);
        if (hasCaddy) BuildCaddyService(builder);
        if (hasPrometheus) BuildPrometheusService(builder);
        if (hasGrafana) BuildGrafanaService(builder, hasPrometheus);
        if (hasAspireDashboard) BuildAspireDashboardService(builder);
        if (hasVictoriaMetrics) BuildVictoriaMetricsService(builder);

        BuildNetworksSection(builder);
        BuildVolumesSection(builder, hasPostgis, hasMySQL, hasSQLServer, hasRedis, hasPrometheus, hasGrafana, hasVictoriaMetrics, hasCaddy);

        var dockerCompose = builder.ToString();
        dockerCompose = CleanupYaml(dockerCompose);

        return await Task.FromResult(dockerCompose);
    }

    private void BuildHonuaService(StringBuilder builder, DeploymentAnalysis analysis, bool hasPostgis, bool hasMySQL, bool hasSQLServer, bool hasRedis)
    {
        builder.AppendLine("  honua:");
        builder.AppendLine("    image: honuaio/honuaserver:latest");
        builder.AppendLine("    container_name: honua-server");
        builder.AppendLine("    ports:");
        builder.AppendLine($"      - \"{analysis.Port}:8080\"");
        builder.AppendLine("    environment:");
        builder.AppendLine("      - ASPNETCORE_ENVIRONMENT=development");

        if (hasPostgis)
        {
            builder.AppendLine("      - HONUA__DATABASE__PROVIDER=postgis");
            builder.AppendLine("      - HONUA__DATABASE__HOST=postgis");
            builder.AppendLine("      - HONUA__DATABASE__PORT=5432");
            builder.AppendLine("      - HONUA__DATABASE__DATABASE=honua");
            builder.AppendLine("      - HONUA__DATABASE__USERNAME=honua");
            builder.AppendLine("      - HONUA__DATABASE__PASSWORD=honua_password");
        }
        else if (hasMySQL)
        {
            builder.AppendLine("      - HONUA__DATABASE__PROVIDER=mysql");
            builder.AppendLine("      - HONUA__DATABASE__HOST=mysql");
            builder.AppendLine("      - HONUA__DATABASE__PORT=3306");
            builder.AppendLine("      - HONUA__DATABASE__DATABASE=honua");
            builder.AppendLine("      - HONUA__DATABASE__USERNAME=honua");
            builder.AppendLine("      - HONUA__DATABASE__PASSWORD=honua_password");
        }
        else if (hasSQLServer)
        {
            builder.AppendLine("      - HONUA__DATABASE__PROVIDER=sqlserver");
            builder.AppendLine("      - HONUA__DATABASE__HOST=sqlserver");
            builder.AppendLine("      - HONUA__DATABASE__PORT=1433");
            builder.AppendLine("      - HONUA__DATABASE__DATABASE=honua");
            builder.AppendLine("      - HONUA__DATABASE__USERNAME=sa");
            builder.AppendLine("      - HONUA__DATABASE__PASSWORD=YourStrong@Passw0rd");
        }

        if (hasRedis)
        {
            builder.AppendLine("      - HONUA__CACHE__PROVIDER=redis");
            builder.AppendLine("      - HONUA__CACHE__REDIS__HOST=redis");
            builder.AppendLine("      - HONUA__CACHE__REDIS__PORT=6379");
        }

        builder.AppendLine("    volumes:");
        builder.AppendLine("      - ./metadata.yaml:/app/metadata.yaml:ro");
        builder.AppendLine("      - ./appsettings.json:/app/appsettings.json:ro");

        var hasDependencies = hasPostgis || hasMySQL || hasSQLServer || hasRedis;
        if (hasDependencies)
        {
            builder.AppendLine("    depends_on:");
            if (hasPostgis) builder.AppendLine("      - postgis");
            if (hasMySQL) builder.AppendLine("      - mysql");
            if (hasSQLServer) builder.AppendLine("      - sqlserver");
            if (hasRedis) builder.AppendLine("      - redis");
        }

        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildPostgisService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  postgis:");
        builder.AppendLine("    image: postgis/postgis:16-3.4");
        builder.AppendLine("    container_name: honua-postgis");
        builder.AppendLine("    environment:");
        builder.AppendLine("      - POSTGRES_DB=honua");
        builder.AppendLine("      - POSTGRES_USER=honua");
        builder.AppendLine("      - POSTGRES_PASSWORD=honua_password");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"5432:5432\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - postgis-data:/var/lib/postgresql/data");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildMySQLService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  mysql:");
        builder.AppendLine("    image: mysql:8.0");
        builder.AppendLine("    container_name: honua-mysql");
        builder.AppendLine("    environment:");
        builder.AppendLine("      - MYSQL_ROOT_PASSWORD=honua_root_pass");
        builder.AppendLine("      - MYSQL_DATABASE=honua");
        builder.AppendLine("      - MYSQL_USER=honua");
        builder.AppendLine("      - MYSQL_PASSWORD=honua_password");
        builder.AppendLine("    command: --default-authentication-plugin=mysql_native_password");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"3306:3306\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - mysql-data:/var/lib/mysql");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildSQLServerService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  sqlserver:");
        builder.AppendLine("    image: mcr.microsoft.com/mssql/server:2022-latest");
        builder.AppendLine("    container_name: honua-sqlserver");
        builder.AppendLine("    environment:");
        builder.AppendLine("      - ACCEPT_EULA=Y");
        builder.AppendLine("      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd");
        builder.AppendLine("      - MSSQL_PID=Developer");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"1433:1433\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - sqlserver-data:/var/opt/mssql");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildRedisService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  redis:");
        builder.AppendLine("    image: redis:7-alpine");
        builder.AppendLine("    container_name: honua-redis");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"6379:6379\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - redis-data:/data");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildNginxService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  nginx:");
        builder.AppendLine("    image: nginx:alpine");
        builder.AppendLine("    container_name: honua-nginx");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"80:80\"");
        builder.AppendLine("      - \"443:443\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - ./nginx.conf:/etc/nginx/nginx.conf:ro");
        builder.AppendLine("    depends_on:");
        builder.AppendLine("      - honua");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildTraefikService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  traefik:");
        builder.AppendLine("    image: traefik:latest");
        builder.AppendLine("    container_name: honua-traefik");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"80:80\"");
        builder.AppendLine("      - \"443:443\"");
        builder.AppendLine("      - \"8080:8080\"  # Traefik dashboard");
        builder.AppendLine("    command:");
        builder.AppendLine("      - --api.insecure=true");
        builder.AppendLine("      - --providers.docker=true");
        builder.AppendLine("      - --providers.docker.exposedbydefault=false");
        builder.AppendLine("      - --entrypoints.web.address=:80");
        builder.AppendLine("      - --entrypoints.websecure.address=:443");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - /var/run/docker.sock:/var/run/docker.sock:ro");
        builder.AppendLine("    depends_on:");
        builder.AppendLine("      - honua");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildCaddyService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  caddy:");
        builder.AppendLine("    image: caddy:latest");
        builder.AppendLine("    container_name: honua-caddy");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"80:80\"");
        builder.AppendLine("      - \"443:443\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - ./Caddyfile:/etc/caddy/Caddyfile:ro");
        builder.AppendLine("      - caddy-data:/data");
        builder.AppendLine("      - caddy-config:/config");
        builder.AppendLine("    depends_on:");
        builder.AppendLine("      - honua");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildPrometheusService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  prometheus:");
        builder.AppendLine("    image: prom/prometheus:latest");
        builder.AppendLine("    container_name: honua-prometheus");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"9090:9090\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro");
        builder.AppendLine("      - prometheus-data:/prometheus");
        builder.AppendLine("    command:");
        builder.AppendLine("      - '--config.file=/etc/prometheus/prometheus.yml'");
        builder.AppendLine("      - '--storage.tsdb.path=/prometheus'");
        builder.AppendLine("    depends_on:");
        builder.AppendLine("      - honua");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildGrafanaService(StringBuilder builder, bool hasPrometheus)
    {
        builder.AppendLine();
        builder.AppendLine("  grafana:");
        builder.AppendLine("    image: grafana/grafana:latest");
        builder.AppendLine("    container_name: honua-grafana");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"3000:3000\"");
        builder.AppendLine("    environment:");
        builder.AppendLine("      - GF_SECURITY_ADMIN_PASSWORD=admin");
        builder.AppendLine("      - GF_SERVER_ROOT_URL=http://localhost:3000");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - grafana-data:/var/lib/grafana");
        if (hasPrometheus)
        {
            builder.AppendLine("    depends_on:");
            builder.AppendLine("      - prometheus");
        }
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildAspireDashboardService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  aspire-dashboard:");
        builder.AppendLine("    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.5");
        builder.AppendLine("    container_name: honua-aspire-dashboard");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"18888:18888\"");
        builder.AppendLine("      - \"18889:18889\"");
        builder.AppendLine("    environment:");
        builder.AppendLine("      - DOTNET_DASHBOARD_OTLP_ENDPOINT_URL=http://0.0.0.0:18889");
        builder.AppendLine("      - DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildVictoriaMetricsService(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("  victoriametrics:");
        builder.AppendLine("    image: victoriametrics/victoria-metrics:latest");
        builder.AppendLine("    container_name: honua-victoriametrics");
        builder.AppendLine("    ports:");
        builder.AppendLine("      - \"8428:8428\"");
        builder.AppendLine("    volumes:");
        builder.AppendLine("      - victoriametrics-data:/victoria-metrics-data");
        builder.AppendLine("    command:");
        builder.AppendLine("      - '--storageDataPath=/victoria-metrics-data'");
        builder.AppendLine("      - '--httpListenAddr=:8428'");
        builder.AppendLine("    networks:");
        builder.AppendLine("      - honua-network");
    }

    private void BuildNetworksSection(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("networks:");
        builder.AppendLine("  honua-network:");
        builder.AppendLine("    driver: bridge");
    }

    private void BuildVolumesSection(StringBuilder builder, bool hasPostgis, bool hasMySQL, bool hasSQLServer,
        bool hasRedis, bool hasPrometheus, bool hasGrafana, bool hasVictoriaMetrics, bool hasCaddy)
    {
        builder.AppendLine();
        builder.AppendLine("volumes:");
        if (hasPostgis) builder.AppendLine("  postgis-data:");
        if (hasMySQL) builder.AppendLine("  mysql-data:");
        if (hasSQLServer) builder.AppendLine("  sqlserver-data:");
        if (hasRedis) builder.AppendLine("  redis-data:");
        if (hasPrometheus) builder.AppendLine("  prometheus-data:");
        if (hasGrafana) builder.AppendLine("  grafana-data:");
        if (hasVictoriaMetrics) builder.AppendLine("  victoriametrics-data:");
        if (hasCaddy)
        {
            builder.AppendLine("  caddy-data:");
            builder.AppendLine("  caddy-config:");
        }
    }

    private string CleanupYaml(string yaml)
    {
        // Remove empty volumes: sections (when no volumes are defined)
        yaml = Regex.Replace(
            yaml,
            @"^volumes:\s*$\n(?=\S|\Z)",
            "",
            RegexOptions.Multiline);

        return yaml;
    }
}
