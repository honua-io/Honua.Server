namespace HonuaField.Services;

/// <summary>
/// Implementation of INavigationService using .NET MAUI Shell navigation
/// </summary>
public class NavigationService : INavigationService
{
	public async Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null)
	{
		if (parameters != null)
		{
			await Shell.Current.GoToAsync(route, parameters);
		}
		else
		{
			await Shell.Current.GoToAsync(route);
		}
	}

	public async Task GoBackAsync()
	{
		await Shell.Current.GoToAsync("..");
	}

	public async Task NavigateToAsync(string route, bool clearStack, IDictionary<string, object>? parameters = null)
	{
		if (clearStack)
		{
			// Use /// to clear the navigation stack
			route = "///" + route.TrimStart('/');
		}

		await NavigateToAsync(route, parameters);
	}
}
