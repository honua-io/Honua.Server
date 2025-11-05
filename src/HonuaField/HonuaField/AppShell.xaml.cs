using HonuaField.Views;

namespace HonuaField;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register routes for navigation
		Routing.RegisterRoute(nameof(FeatureDetailPage), typeof(FeatureDetailPage));
		Routing.RegisterRoute(nameof(FeatureEditorPage), typeof(FeatureEditorPage));
		Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
	}
}
