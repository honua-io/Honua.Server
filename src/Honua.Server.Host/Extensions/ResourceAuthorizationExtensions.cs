// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring resource-based authorization.
/// </summary>
internal static class ResourceAuthorizationExtensions
{
    /// <summary>
    /// Adds resource-based authorization services to the service collection.
    /// </summary>
    public static IServiceCollection AddResourceAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<ResourceAuthorizationOptions>(
            configuration.GetSection(ResourceAuthorizationOptions.SectionName));

        // Register authorization cache
        services.AddSingleton<IResourceAuthorizationCache, ResourceAuthorizationCache>();

        // Register metrics
        services.AddSingleton<ResourceAuthorizationMetrics>();

        // Register resource authorization handlers
        services.AddSingleton<IResourceAuthorizationHandler, LayerAuthorizationHandler>();
        services.AddSingleton<IResourceAuthorizationHandler, CollectionAuthorizationHandler>();

        // Register authorization service
        services.AddSingleton<IResourceAuthorizationService, ResourceAuthorizationService>();

        // Register ASP.NET Core authorization handlers
        services.AddSingleton<IAuthorizationHandler, ReadLayerAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, WriteLayerAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, DeleteLayerAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, ReadCollectionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, WriteCollectionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, DeleteCollectionAuthorizationHandler>();

        return services;
    }
}
