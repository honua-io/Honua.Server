namespace HonuaField.Services;

/// <summary>
/// Service for handling navigation throughout the app
/// </summary>
public interface INavigationService
{
	/// <summary>
	/// Navigate to a route
	/// </summary>
	Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null);

	/// <summary>
	/// Go back to previous page
	/// </summary>
	Task GoBackAsync();

	/// <summary>
	/// Navigate to a route and remove previous pages from stack
	/// </summary>
	Task NavigateToAsync(string route, bool clearStack, IDictionary<string, object>? parameters = null);
}
