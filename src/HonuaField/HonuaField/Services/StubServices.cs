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

public interface IFeaturesService { }
public class FeaturesService : IFeaturesService { }

public interface ICollectionsService { }
public class CollectionsService : ICollectionsService { }

public interface ISyncService { }
public class SyncService : ISyncService { }

public interface IConflictResolutionService { }
public class ConflictResolutionService : IConflictResolutionService { }

public interface ILocationService { }
public class LocationService : ILocationService { }

public interface IGpsService { }
public class GpsService : IGpsService { }
