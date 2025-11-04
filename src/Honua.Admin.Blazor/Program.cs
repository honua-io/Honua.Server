// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components;
using Honua.Admin.Blazor.Shared.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add HttpContextAccessor for token access
builder.Services.AddHttpContextAccessor();

// Configure HttpClient for Admin API with bearer token
builder.Services.AddHttpClient("AdminApi", client =>
{
    var baseUrl = builder.Configuration["AdminApi:BaseUrl"] ?? "https://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});
// TODO: Add BearerTokenDelegatingHandler after auth setup
// .AddHttpMessageHandler<BearerTokenDelegatingHandler>();

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

// TODO: Add authentication after auth setup
// builder.Services.AddAuthentication(...)
// builder.Services.AddAuthorization(...)

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

// TODO: Add after auth setup
// app.UseAuthentication();
// app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
