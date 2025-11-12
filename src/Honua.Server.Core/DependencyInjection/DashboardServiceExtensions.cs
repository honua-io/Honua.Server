// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Data.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering dashboard services.
/// </summary>
public static class DashboardServiceExtensions
{
    /// <summary>
    /// Add dashboard services to the service collection.
    /// </summary>
    public static IServiceCollection AddDashboardServices(
        this IServiceCollection services,
        string connectionString)
    {
        // Register dashboard repository
        services.AddScoped<IDashboardRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PostgresDashboardRepository>>();
            return new PostgresDashboardRepository(connectionString, logger);
        });

        return services;
    }
}
