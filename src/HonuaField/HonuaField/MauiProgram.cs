using CommunityToolkit.Maui;
using HonuaField.Data;
using HonuaField.Services;
using HonuaField.ViewModels;
using HonuaField.Views;
using Microsoft.Extensions.Logging;
using Serilog;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace HonuaField;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		builder
			.UseMauiApp<App>()
			.UseSkiaSharp(true) // Required for Mapsui
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Configure Serilog
		ConfigureLogging(builder);

		// Register Services
		RegisterServices(builder.Services);

		// Register ViewModels
		RegisterViewModels(builder.Services);

		// Register Views
		RegisterViews(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void ConfigureLogging(MauiAppBuilder builder)
	{
		var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "honuafield-.txt");

		Log.Logger = new LoggerConfiguration()
#if DEBUG
			.MinimumLevel.Debug()
#else
			.MinimumLevel.Information()
#endif
			.Enrich.FromLogContext()
			.WriteTo.File(
				logPath,
				rollingInterval: RollingInterval.Day,
				retainedFileCountLimit: 7)
			.CreateLogger();

		builder.Logging.AddSerilog(Log.Logger);
	}

	private static void RegisterServices(IServiceCollection services)
	{
		// Core Services
		services.AddSingleton<INavigationService, NavigationService>();
		services.AddSingleton<IDatabaseService, HonuaField.Data.DatabaseService>();
		services.AddSingleton<IAuthenticationService, AuthenticationService>();
		services.AddSingleton<ISettingsService, SettingsService>();
		services.AddSingleton<IBiometricService, BiometricService>();

		// Data Repositories
		services.AddSingleton<HonuaField.Data.Repositories.IChangeRepository, HonuaField.Data.Repositories.ChangeRepository>();
		services.AddSingleton<HonuaField.Data.Repositories.IFeatureRepository, HonuaField.Data.Repositories.FeatureRepository>();
		services.AddSingleton<HonuaField.Data.Repositories.ICollectionRepository, HonuaField.Data.Repositories.CollectionRepository>();
		services.AddSingleton<HonuaField.Data.Repositories.IAttachmentRepository, HonuaField.Data.Repositories.AttachmentRepository>();
		services.AddSingleton<HonuaField.Data.Repositories.IMapRepository, HonuaField.Data.Repositories.MapRepository>();

		// API Services
		services.AddSingleton<IApiClient, ApiClient>();
		services.AddSingleton<IFeaturesService, FeaturesService>();
		services.AddSingleton<ICollectionsService, CollectionsService>();

		// Sync Services
		services.AddSingleton<ISyncService, SyncService>();
		services.AddSingleton<IConflictResolutionService, ConflictResolutionService>();

		// Location Services
		services.AddSingleton<ILocationService, LocationService>();
		services.AddSingleton<IGpsService, GpsService>();

		// Camera and Media Services
		services.AddSingleton<ICameraService, CameraService>();

		// Offline Map Services
		services.AddSingleton<IOfflineMapService, OfflineMapService>();
		services.AddSingleton<OfflineTileProviderFactory>();

		// Symbology Services
		services.AddSingleton<ISymbologyService, SymbologyService>();

		// Form Builder Service
		services.AddSingleton<IFormBuilderService, FormBuilderService>();
	}

	private static void RegisterViewModels(IServiceCollection services)
	{
		// Shell & Main
		services.AddSingleton<AppShellViewModel>();
		services.AddSingleton<MainViewModel>();

		// Authentication
		services.AddTransient<LoginViewModel>();
		services.AddTransient<OnboardingViewModel>();

		// Map & Features
		services.AddTransient<MapViewModel>();
		services.AddTransient<FeatureListViewModel>();
		services.AddTransient<FeatureDetailViewModel>();
		services.AddTransient<FeatureEditorViewModel>();

		// Settings
		services.AddTransient<SettingsViewModel>();
		services.AddTransient<ProfileViewModel>();
	}

	private static void RegisterViews(IServiceCollection services)
	{
		// Shell & Main
		services.AddSingleton<AppShell>();
		services.AddSingleton<MainPage>();

		// Authentication
		services.AddTransient<LoginPage>();
		services.AddTransient<OnboardingPage>();

		// Map & Features
		services.AddTransient<MapPage>();
		services.AddTransient<FeatureListPage>();
		services.AddTransient<FeatureDetailPage>();
		services.AddTransient<FeatureEditorPage>();

		// Settings
		services.AddTransient<SettingsPage>();
		services.AddTransient<ProfilePage>();
	}
}
