// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Components.Map;
using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Honua.MapSDK.Services;
using Honua.Server.Core.LocationServices;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Components.Controls;

/// <summary>
/// Geocoding search control with autocomplete, history, and map integration.
/// </summary>
public partial class GeocodingSearch : ComponentBase
{
    [Inject] private GeocodingSearchService SearchService { get; set; } = null!;
    [Inject] private ComponentBus Bus { get; set; } = null!;
    [Inject] private ILogger<GeocodingSearch> Logger { get; set; } = null!;

    /// <summary>
    /// Reference to the HonuaMap instance to integrate with.
    /// </summary>
    [Parameter]
    public HonuaMap? Map { get; set; }

    /// <summary>
    /// Map ID to target for fly-to operations. If not specified, uses Map.Id.
    /// </summary>
    [Parameter]
    public string? MapId { get; set; }

    /// <summary>
    /// Geocoding provider key to use (e.g., "azure-maps", "nominatim").
    /// If null, uses the first available provider or Options.DefaultProvider.
    /// </summary>
    [Parameter]
    public string? Provider { get; set; }

    /// <summary>
    /// Search configuration options.
    /// </summary>
    [Parameter]
    public GeocodingSearchOptions Options { get; set; } = GeocodingSearchOptions.Default;

    /// <summary>
    /// Whether to show the provider selector dropdown.
    /// </summary>
    [Parameter]
    public bool ShowProviderSelector { get; set; }

    /// <summary>
    /// CSS class to apply to the container.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>
    /// Event fired when a search result is selected.
    /// </summary>
    [Parameter]
    public EventCallback<SearchResult> OnResultSelected { get; set; }

    /// <summary>
    /// Event fired when search query changes.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnSearchQueryChanged { get; set; }

    private string _searchQuery = string.Empty;
    private string _selectedProvider = string.Empty;
    private List<SearchResult> _searchResults = new();
    private List<SearchHistoryItem> _historyItems = new();
    private List<IGeocodingProvider> _availableProviders = new();
    private bool _showResults = false;
    private bool _showNoResults = false;
    private bool _isSearching = false;
    private bool _searchThisArea = true;
    private int _selectedResultIndex = -1;
    private double[]? _currentMapBounds;
    private double[]? _currentMapCenter;
    private CancellationTokenSource? _searchCts;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<GeocodingSearch>? _dotNetRef;

    protected override async Task OnInitializedAsync()
    {
        // Get available providers
        _availableProviders = SearchService.GetAvailableProviders().ToList();

        if (!_availableProviders.Any())
        {
            Logger.LogWarning("No geocoding providers available");
            return;
        }

        // Set initial provider
        _selectedProvider = Provider ?? Options.DefaultProvider ?? _availableProviders.First().ProviderKey;

        // Override options if parameters are set
        if (ShowProviderSelector)
        {
            Options.ShowProviderSelector = true;
        }

        // Load search history
        if (Options.EnableHistory)
        {
            _historyItems = await SearchService.GetSearchHistoryAsync();
        }

        // Subscribe to map extent changes
        if (Map != null)
        {
            Bus.Subscribe<MapExtentChangedMessage>(OnMapExtentChanged);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // Load JavaScript module
                _dotNetRef = DotNetObjectReference.Create(this);
                _jsModule = await JS.InvokeAsync<IJSObjectReference>(
                    "import",
                    "./_content/Honua.MapSDK/js/geocoding-search.js"
                );

                // Initialize keyboard navigation
                if (Options.EnableKeyboardNavigation)
                {
                    await _jsModule.InvokeVoidAsync("initializeKeyboardNavigation", _containerRef, _dotNetRef);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize geocoding search JavaScript module");
            }
        }
    }

    private async Task OnMapExtentChanged(MessageArgs<MapExtentChangedMessage> args)
    {
        var message = args.Message;
        if (MapId != null && message.MapId != MapId)
            return;

        if (Map != null && message.MapId != Map.Id)
            return;

        _currentMapBounds = message.Bounds;
        _currentMapCenter = message.Center;

        // Re-search if "search this area" is enabled and there's an active query
        if (_searchThisArea && !string.IsNullOrWhiteSpace(_searchQuery) && Options.BiasToViewport)
        {
            await PerformSearchAsync();
        }
    }

    private async Task PerformSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults.Clear();
            _showResults = false;
            _showNoResults = false;
            StateHasChanged();
            return;
        }

        // Enforce minimum character requirement
        if (_searchQuery.Length < Options.AutocompleteMinChars)
        {
            _searchResults.Clear();
            _showResults = false;
            _showNoResults = false;
            StateHasChanged();
            return;
        }

        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        try
        {
            // Debounce delay
            await Task.Delay(Options.DebounceDelay, _searchCts.Token);

            _isSearching = true;
            StateHasChanged();

            // Perform search
            var bounds = _searchThisArea && Options.BiasToViewport ? _currentMapBounds : null;
            var center = _searchThisArea && Options.BiasToViewport ? _currentMapCenter : null;

            _searchResults = await SearchService.SearchAsync(
                _searchQuery,
                Options,
                _selectedProvider,
                bounds,
                center,
                _searchCts.Token
            );

            _showResults = true;
            _showNoResults = !_searchResults.Any();
            _selectedResultIndex = -1;

            await OnSearchQueryChanged.InvokeAsync(_searchQuery);
        }
        catch (TaskCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Search failed for query: {Query}", _searchQuery);
            _searchResults.Clear();
            _showNoResults = true;
        }
        finally
        {
            _isSearching = false;
            StateHasChanged();
        }
    }

    private async Task SelectResult(SearchResult result)
    {
        Logger.LogDebug("Selected result: {Address}", result.DisplayAddress);

        // Add to history
        if (Options.EnableHistory)
        {
            await SearchService.AddToHistoryAsync(_searchQuery, result, Options.MaxHistoryItems);
            _historyItems = await SearchService.GetSearchHistoryAsync();
        }

        // Hide results
        _showResults = false;
        _searchQuery = result.DisplayAddress;
        StateHasChanged();

        // Add marker to map
        if (Options.AddMarkerOnSelect && _jsModule != null)
        {
            try
            {
                var mapId = GetTargetMapId();
                if (!string.IsNullOrEmpty(mapId))
                {
                    await _jsModule.InvokeVoidAsync("addMarker",
                        mapId,
                        result.Result.Longitude,
                        result.Result.Latitude,
                        result.DisplayAddress,
                        Options.ClearPreviousMarkers
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to add marker to map");
            }
        }

        // Fly to result
        if (Options.FlyToResultOnSelect)
        {
            await FlyToResult(result);
        }

        // Fire event
        await OnResultSelected.InvokeAsync(result);
    }

    private async Task SelectHistoryItem(SearchHistoryItem item)
    {
        _searchQuery = item.Query;

        // Create a search result from history item
        var result = new SearchResult
        {
            Result = new Server.Core.LocationServices.Models.GeocodingResult
            {
                FormattedAddress = item.FormattedAddress,
                Longitude = item.Longitude,
                Latitude = item.Latitude
            },
            DisplayAddress = item.FormattedAddress,
            RelevanceScore = 100,
            Icon = "history"
        };

        await SelectResult(result);
    }

    private async Task FlyToResult(SearchResult result)
    {
        var mapId = GetTargetMapId();
        if (string.IsNullOrEmpty(mapId))
            return;

        var message = new FlyToRequestMessage
        {
            MapId = mapId,
            Center = new[] { result.Result.Longitude, result.Result.Latitude },
            Zoom = Options.FlyToZoomLevel,
            Duration = Options.FlyToDuration
        };

        await Bus.PublishAsync(message, "GeocodingSearch");
    }

    private async Task CopyCoordinates(SearchResult result)
    {
        try
        {
            var coords = $"{result.Result.Latitude:F6}, {result.Result.Longitude:F6}";
            if (_jsModule != null)
            {
                await _jsModule.InvokeVoidAsync("copyToClipboard", coords);
                Logger.LogDebug("Copied coordinates: {Coords}", coords);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to copy coordinates");
        }
    }

    private async Task ClearSearch()
    {
        _searchQuery = string.Empty;
        _searchResults.Clear();
        _showResults = false;
        _showNoResults = false;
        _selectedResultIndex = -1;
        StateHasChanged();

        await OnSearchQueryChanged.InvokeAsync(_searchQuery);
    }

    private async Task ClearHistory()
    {
        await SearchService.ClearHistoryAsync();
        _historyItems.Clear();
        StateHasChanged();
    }

    private Task HandleInputFocus()
    {
        // Show history when input is focused with empty query
        if (string.IsNullOrWhiteSpace(_searchQuery) && _historyItems.Any())
        {
            _showResults = true;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    private async Task HandleKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (!Options.EnableKeyboardNavigation)
            return;

        switch (e.Key)
        {
            case "ArrowDown":
                if (_searchResults.Any() && _selectedResultIndex < _searchResults.Count - 1)
                {
                    _selectedResultIndex++;
                    StateHasChanged();
                }
                break;

            case "ArrowUp":
                if (_selectedResultIndex > 0)
                {
                    _selectedResultIndex--;
                    StateHasChanged();
                }
                break;

            case "Enter":
                if (_selectedResultIndex >= 0 && _selectedResultIndex < _searchResults.Count)
                {
                    await SelectResult(_searchResults[_selectedResultIndex]);
                }
                break;

            case "Escape":
                _showResults = false;
                StateHasChanged();
                break;
        }
    }

    private string GetTargetMapId()
    {
        return MapId ?? Map?.Id ?? string.Empty;
    }

    /// <summary>
    /// Called from JavaScript when search query changes.
    /// </summary>
    [JSInvokable]
    public async Task OnSearchQueryChangedInternal(string query)
    {
        _searchQuery = query;
        await PerformSearchAsync();
    }

    /// <summary>
    /// Public API: Programmatically set search query and perform search.
    /// </summary>
    public async Task SetSearchQueryAsync(string query)
    {
        _searchQuery = query;
        await PerformSearchAsync();
    }

    /// <summary>
    /// Public API: Clear search and results.
    /// </summary>
    public async Task ClearAsync()
    {
        await ClearSearch();
    }

    /// <summary>
    /// Public API: Focus the search input.
    /// </summary>
    public async Task FocusAsync()
    {
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("focusInput", _containerRef);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        if (_jsModule != null)
        {
            try
            {
                await _jsModule.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _dotNetRef?.Dispose();
    }
}
