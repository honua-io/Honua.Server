using HonuaField.Services;

namespace HonuaField;

public partial class App : Application
{
	private readonly IAuthenticationService _authService;
	private readonly INavigationService _navigationService;
	private readonly IDatabaseService _databaseService;

	public App(
		IAuthenticationService authService,
		INavigationService navigationService,
		IDatabaseService databaseService)
	{
		InitializeComponent();

		_authService = authService;
		_navigationService = navigationService;
		_databaseService = databaseService;

		MainPage = new AppShell();
	}

	protected override async void OnStart()
	{
		base.OnStart();

		// Initialize database
		await _databaseService.InitializeAsync();

		// Check authentication status
		var isAuthenticated = await _authService.IsAuthenticatedAsync();

		if (!isAuthenticated)
		{
			await _navigationService.NavigateToAsync("//Login");
		}
		else
		{
			await _navigationService.NavigateToAsync("//Main");
		}
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		// Save any pending data
	}

	protected override void OnResume()
	{
		base.OnResume();
		// Refresh data if needed
	}
}
