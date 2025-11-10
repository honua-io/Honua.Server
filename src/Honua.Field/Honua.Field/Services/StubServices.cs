// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace HonuaField.Services;

// Stub service interfaces and implementations for Sprint 1
// These will be fully implemented in later sprints

public interface IDatabaseService
{
	Task InitializeAsync();
	HonuaField.Data.HonuaFieldDatabase GetDatabase();
}

// DatabaseService implementation is now in Data/DatabaseService.cs

public interface IApiClient
{
	Task<T?> GetAsync<T>(string endpoint, string? accessToken = null);
	Task<T?> PostAsync<T>(string endpoint, object data, string? accessToken = null);
	Task<T?> PutAsync<T>(string endpoint, object data, string? accessToken = null);
	Task<bool> DeleteAsync(string endpoint, string? accessToken = null);
}

// ApiClient implementation is now in ApiClient.cs

// FeaturesService is now implemented in:
// - Services/IFeaturesService.cs
// - Services/FeaturesService.cs

// ICollectionsService and CollectionsService are now implemented in:
// - Services/ICollectionsService.cs
// - Services/CollectionsService.cs

// ISyncService and SyncService are now implemented in:
// - Services/ISyncService.cs
// - Services/SyncService.cs

// IConflictResolutionService and ConflictResolutionService are now implemented in:
// - Services/IConflictResolutionService.cs
// - Services/ConflictResolutionService.cs

// ILocationService and LocationService are now implemented in:
// - Services/ILocationService.cs
// - Services/LocationService.cs

// IGpsService and GpsService are now implemented in:
// - Services/IGpsService.cs
// - Services/GpsService.cs
