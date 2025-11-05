namespace HonuaField.Services;

// Stub service interfaces and implementations for Sprint 1
// These will be fully implemented in later sprints

public interface IDatabaseService
{
	Task InitializeAsync();
}

public class DatabaseService : IDatabaseService
{
	public Task InitializeAsync() => Task.CompletedTask;
}

public interface IApiClient
{
	Task<T?> GetAsync<T>(string endpoint, string? accessToken = null);
	Task<T?> PostAsync<T>(string endpoint, object data, string? accessToken = null);
}

public class ApiClient : IApiClient
{
	public Task<T?> GetAsync<T>(string endpoint, string? accessToken = null) => Task.FromResult(default(T));
	public Task<T?> PostAsync<T>(string endpoint, object data, string? accessToken = null) => Task.FromResult(default(T));
}

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
