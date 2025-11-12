// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Honua.Server.Core.Configuration.V2.Services;

/// <summary>
/// Discovers and loads service registrations from assemblies.
/// </summary>
public sealed class ServiceRegistrationDiscovery
{
    private readonly Dictionary<string, IServiceRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDiscovered = false;

    /// <summary>
    /// Discover service registrations from the specified assemblies.
    /// </summary>
    public void DiscoverServices(params Assembly[] assemblies)
    {
        if (_isDiscovered)
        {
            return; // Already discovered
        }

        var assembliesToScan = assemblies.Length > 0
            ? assemblies
            : new[] { Assembly.GetExecutingAssembly() };

        foreach (var assembly in assembliesToScan)
        {
            DiscoverServicesInAssembly(assembly);
        }

        _isDiscovered = true;
    }

    /// <summary>
    /// Discover service registrations from all loaded assemblies.
    /// </summary>
    public void DiscoverAllServices()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("Honua.Server") == true)
            .ToArray();

        DiscoverServices(assemblies);
    }

    /// <summary>
    /// Get a service registration by ID.
    /// </summary>
    public IServiceRegistration? GetService(string serviceId)
    {
        return _registrations.TryGetValue(serviceId, out var registration)
            ? registration
            : null;
    }

    /// <summary>
    /// Get all discovered service registrations.
    /// </summary>
    public IReadOnlyDictionary<string, IServiceRegistration> GetAllServices()
    {
        return _registrations;
    }

    /// <summary>
    /// Check if a service is registered.
    /// </summary>
    public bool HasService(string serviceId)
    {
        return _registrations.ContainsKey(serviceId);
    }

    private void DiscoverServicesInAssembly(Assembly assembly)
    {
        try
        {
            var serviceTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IServiceRegistration).IsAssignableFrom(t))
                .Select(t => new
                {
                    Type = t,
                    Attribute = t.GetCustomAttribute<ServiceRegistrationAttribute>()
                })
                .Where(x => x.Attribute != null)
                .OrderBy(x => x.Attribute!.Priority)
                .ToList();

            foreach (var serviceType in serviceTypes)
            {
                try
                {
                    var instance = (IServiceRegistration)Activator.CreateInstance(serviceType.Type)!;
                    var serviceId = serviceType.Attribute!.ServiceId;

                    if (_registrations.ContainsKey(serviceId))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate service registration for '{serviceId}' in assembly {assembly.GetName().Name}");
                    }

                    _registrations[serviceId] = instance;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to instantiate service registration {serviceType.Type.Name}: {ex.Message}", ex);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types couldn't be loaded - log but continue
            var loadedTypes = ex.Types.Where(t => t != null);
            // Could log warning here
        }
    }
}
