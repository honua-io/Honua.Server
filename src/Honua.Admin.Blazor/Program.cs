// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components;
using Honua.Admin.Blazor.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add HttpContextAccessor for token access
builder.Services.AddHttpContextAccessor();

// Add authentication and authorization
builder.Services.AddAuthenticationCore();
builder.Services.AddAuthorizationCore();

// Register custom authentication state provider
builder.Services.AddScoped<AdminAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<AdminAuthenticationStateProvider>());

// Register authentication service
builder.Services.AddScoped<AuthenticationService>();

// Register bearer token handler for HttpClient
builder.Services.AddTransient<BearerTokenHandler>();

// Configure HttpClient for authentication endpoint (no bearer token)
builder.Services.AddHttpClient("AuthApi", client =>
{
    var baseUrl = builder.Configuration["AdminApi:BaseUrl"] ?? "https://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure HttpClient for Admin API with bearer token
builder.Services.AddHttpClient("AdminApi", client =>
{
    var baseUrl = builder.Configuration["AdminApi:BaseUrl"] ?? "https://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<BearerTokenHandler>();

// Register pre-configured HttpClient for easier injection
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("AdminApi");
});

// Register UI state services
builder.Services.AddScoped<NavigationState>();
builder.Services.AddScoped<EditorState>();
builder.Services.AddScoped<NotificationService>();

// Register API clients
builder.Services.AddScoped<ServiceApiClient>();
builder.Services.AddScoped<LayerApiClient>();
builder.Services.AddScoped<FolderApiClient>();
builder.Services.AddScoped<ImportApiClient>();

// Register SignalR hub service for real-time updates
builder.Services.AddScoped<MetadataHubService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
