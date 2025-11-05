// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Host.Admin.Hubs;
using Honua.Server.Host.Admin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring Admin UI SignalR services.
/// </summary>
internal static class AdminSignalRServiceCollectionExtensions
{
    /// <summary>
    /// Adds SignalR services for Admin UI real-time updates.
    /// </summary>
    public static IServiceCollection AddAdminSignalR(this IServiceCollection services)
    {
        // Add SignalR services
        services.AddSignalR();

        // Add hosted service for metadata change notifications
        services.AddHostedService<MetadataChangeNotificationService>();

        return services;
    }
}
