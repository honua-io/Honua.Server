using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HonuaField.Tests.Integration.Infrastructure;

/// <summary>
/// Base class for integration tests providing database setup, cleanup, and service provider configuration
/// Implements IAsyncLifetime for xUnit async setup/teardown
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
	protected HonuaFieldDatabase Database { get; private set; } = null!;
	protected IServiceProvider ServiceProvider { get; private set; } = null!;
	protected string DatabasePath { get; private set; } = null!;
	protected string TestDataDirectory { get; private set; } = null!;
	protected TestDataBuilder DataBuilder { get; private set; } = null!;

	// Repository instances for convenience
	protected IFeatureRepository FeatureRepository { get; private set; } = null!;
	protected ICollectionRepository CollectionRepository { get; private set; } = null!;
	protected IAttachmentRepository AttachmentRepository { get; private set; } = null!;
	protected IChangeRepository ChangeRepository { get; private set; } = null!;
	protected IMapRepository MapRepository { get; private set; } = null!;

	/// <summary>
	/// Initialize test infrastructure before each test
	/// Called by xUnit before each test method
	/// </summary>
	public virtual async Task InitializeAsync()
	{
		// Create temporary database path
		DatabasePath = Path.Combine(Path.GetTempPath(), $"honua_test_{Guid.NewGuid()}.db");

		// Create temporary test data directory
		TestDataDirectory = Path.Combine(Path.GetTempPath(), $"honua_test_data_{Guid.NewGuid()}");
		Directory.CreateDirectory(TestDataDirectory);

		// Initialize database
		Database = new HonuaFieldDatabase(DatabasePath);
		await Database.InitializeAsync();

		// Configure service provider with real services
		var services = new ServiceCollection();
		ConfigureServices(services);
		ServiceProvider = services.BuildServiceProvider();

		// Get repository instances
		FeatureRepository = ServiceProvider.GetRequiredService<IFeatureRepository>();
		CollectionRepository = ServiceProvider.GetRequiredService<ICollectionRepository>();
		AttachmentRepository = ServiceProvider.GetRequiredService<IAttachmentRepository>();
		ChangeRepository = ServiceProvider.GetRequiredService<IChangeRepository>();
		MapRepository = ServiceProvider.GetRequiredService<IMapRepository>();

		// Initialize test data builder
		DataBuilder = new TestDataBuilder(TestDataDirectory);

		// Call custom initialization for derived classes
		await OnInitializeAsync();
	}

	/// <summary>
	/// Cleanup test infrastructure after each test
	/// Called by xUnit after each test method
	/// </summary>
	public virtual async Task DisposeAsync()
	{
		// Call custom cleanup for derived classes
		await OnDisposeAsync();

		// Close database
		if (Database != null)
		{
			await Database.CloseAsync();
		}

		// Cleanup database file
		if (File.Exists(DatabasePath))
		{
			try
			{
				File.Delete(DatabasePath);
			}
			catch
			{
				// Ignore file deletion errors
			}
		}

		// Cleanup test data directory
		if (Directory.Exists(TestDataDirectory))
		{
			try
			{
				Directory.Delete(TestDataDirectory, recursive: true);
			}
			catch
			{
				// Ignore directory deletion errors
			}
		}

		// Dispose service provider
		if (ServiceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Configure services for dependency injection
	/// Override to add custom service configurations
	/// </summary>
	protected virtual void ConfigureServices(IServiceCollection services)
	{
		// Register database
		services.AddSingleton(Database);

		// Register repositories
		services.AddSingleton<IFeatureRepository, FeatureRepository>();
		services.AddSingleton<ICollectionRepository, CollectionRepository>();
		services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
		services.AddSingleton<IChangeRepository, ChangeRepository>();
		services.AddSingleton<IMapRepository, MapRepository>();

		// Note: Services like SyncService, AuthenticationService, etc. should be registered
		// by derived test classes as needed, often with mocked dependencies
	}

	/// <summary>
	/// Custom initialization hook for derived classes
	/// Override to perform additional setup
	/// </summary>
	protected virtual Task OnInitializeAsync()
	{
		return Task.CompletedTask;
	}

	/// <summary>
	/// Custom cleanup hook for derived classes
	/// Override to perform additional cleanup
	/// </summary>
	protected virtual Task OnDisposeAsync()
	{
		return Task.CompletedTask;
	}

	/// <summary>
	/// Clear all data from database while keeping schema
	/// Useful for resetting state between test sections
	/// </summary>
	protected async Task ClearDatabaseAsync()
	{
		await Database.ClearAllDataAsync();
	}

	/// <summary>
	/// Get database statistics
	/// </summary>
	protected async Task<DatabaseStats> GetDatabaseStatsAsync()
	{
		return await Database.GetStatsAsync();
	}

	/// <summary>
	/// Create a test file with content
	/// File will be created in the test data directory
	/// </summary>
	protected string CreateTestFile(string filename, byte[] content)
	{
		var filepath = Path.Combine(TestDataDirectory, filename);
		File.WriteAllBytes(filepath, content);
		return filepath;
	}

	/// <summary>
	/// Create a test file with text content
	/// File will be created in the test data directory
	/// </summary>
	protected string CreateTestFile(string filename, string content)
	{
		var filepath = Path.Combine(TestDataDirectory, filename);
		File.WriteAllText(filepath, content);
		return filepath;
	}

	/// <summary>
	/// Assert that a file exists
	/// </summary>
	protected void AssertFileExists(string filepath)
	{
		if (!File.Exists(filepath))
		{
			throw new FileNotFoundException($"Expected file does not exist: {filepath}");
		}
	}

	/// <summary>
	/// Assert that a directory exists
	/// </summary>
	protected void AssertDirectoryExists(string directoryPath)
	{
		if (!Directory.Exists(directoryPath))
		{
			throw new DirectoryNotFoundException($"Expected directory does not exist: {directoryPath}");
		}
	}
}
