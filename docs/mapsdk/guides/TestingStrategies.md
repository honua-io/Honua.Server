# Testing Strategies Guide

This guide covers comprehensive testing strategies for Honua.MapSDK applications, including unit testing, integration testing, E2E testing, and CI/CD integration.

---

## Table of Contents

1. [Unit Testing Components](#unit-testing-components)
2. [Integration Testing](#integration-testing)
3. [E2E Testing Approaches](#e2e-testing-approaches)
4. [Mock Strategies](#mock-strategies)
5. [Test Data Generation](#test-data-generation)
6. [CI/CD Integration](#cicd-integration)

---

## Unit Testing Components

### Setup bUnit

```bash
dotnet add package bUnit
dotnet add package bUnit.web
dotnet add package Moq
```

### Basic Component Test

```csharp
using Bunit;
using Xunit;
using Honua.MapSDK.Components;

public class MapComponentTests : TestContext
{
    [Fact]
    public void Map_Renders_WithDefaultParameters()
    {
        // Arrange & Act
        var cut = RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.Center, new[] { -122.4194, 37.7749 })
            .Add(p => p.Zoom, 12));

        // Assert
        cut.MarkupMatches(@"<div id=""test-map"" class=""honua-map""></div>");
    }

    [Fact]
    public void Map_Fires_OnMapReady_Event()
    {
        // Arrange
        var mapReadyFired = false;
        MapReadyMessage? receivedMessage = null;

        // Act
        var cut = RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.OnMapReady, msg =>
            {
                mapReadyFired = true;
                receivedMessage = msg;
            }));

        // Simulate map ready
        cut.Instance.TriggerMapReady();

        // Assert
        Assert.True(mapReadyFired);
        Assert.NotNull(receivedMessage);
        Assert.Equal("test-map", receivedMessage.MapId);
    }
}
```

### Testing Component Parameters

```csharp
[Theory]
[InlineData(0, 0, 2)]
[InlineData(-122.4194, 37.7749, 12)]
[InlineData(139.6917, 35.6895, 15)]
public void Map_Accepts_Various_Centers_And_Zooms(double lon, double lat, double zoom)
{
    // Arrange & Act
    var cut = RenderComponent<HonuaMap>(parameters => parameters
        .Add(p => p.Id, "test-map")
        .Add(p => p.Center, new[] { lon, lat })
        .Add(p => p.Zoom, zoom));

    // Assert
    Assert.Equal(lon, cut.Instance.Center[0]);
    Assert.Equal(lat, cut.Instance.Center[1]);
    Assert.Equal(zoom, cut.Instance.Zoom);
}
```

### Testing Data Grid

```csharp
public class DataGridTests : TestContext
{
    [Fact]
    public void DataGrid_Renders_Items()
    {
        // Arrange
        var testData = new List<TestItem>
        {
            new TestItem { Id = "1", Name = "Item 1" },
            new TestItem { Id = "2", Name = "Item 2" }
        };

        // Act
        var cut = RenderComponent<HonuaDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, testData)
            .Add(p => p.Columns, new List<DataGridColumn<TestItem>>
            {
                new DataGridColumn<TestItem>("Id", item => item.Id),
                new DataGridColumn<TestItem>("Name", item => item.Name)
            }));

        // Assert
        var rows = cut.FindAll("tr.mud-table-row");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void DataGrid_Filters_Items()
    {
        // Arrange
        var testData = Enumerable.Range(1, 100)
            .Select(i => new TestItem { Id = i.ToString(), Name = $"Item {i}" })
            .ToList();

        // Act
        var cut = RenderComponent<HonuaDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, testData)
            .Add(p => p.ShowSearch, true));

        var searchBox = cut.Find("input[type='text']");
        searchBox.Change("Item 5");

        // Assert
        var visibleRows = cut.FindAll("tr.mud-table-row");
        Assert.True(visibleRows.Count < 100);
    }
}
```

---

## Integration Testing

### Setup WebApplicationFactory

```csharp
public class CustomWebApplicationFactory<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real database
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // Seed test data
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            SeedTestData(db);
        });
    }

    private void SeedTestData(ApplicationDbContext db)
    {
        db.Features.AddRange(new[]
        {
            new Feature { Id = "1", Name = "Test Feature 1" },
            new Feature { Id = "2", Name = "Test Feature 2" }
        });
        db.SaveChanges();
    }
}
```

### API Integration Tests

```csharp
public class FeatureApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FeatureApiTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetFeatures_Returns_OkResult()
    {
        // Act
        var response = await _client.GetAsync("/api/features");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var features = JsonSerializer.Deserialize<List<Feature>>(content);
        Assert.NotNull(features);
        Assert.NotEmpty(features);
    }

    [Fact]
    public async Task PostFeature_Creates_NewFeature()
    {
        // Arrange
        var newFeature = new Feature
        {
            Name = "New Test Feature",
            Type = "Point"
        };

        var json = JsonSerializer.Serialize(newFeature);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/features", content);

        // Assert
        response.EnsureSuccessStatusCode();
        var createdFeature = JsonSerializer.Deserialize<Feature>(
            await response.Content.ReadAsStringAsync());
        Assert.NotNull(createdFeature);
        Assert.Equal(newFeature.Name, createdFeature.Name);
    }
}
```

---

## E2E Testing Approaches

### Playwright Setup

```bash
dotnet add package Microsoft.Playwright
pwsh bin/Debug/net8.0/playwright.ps1 install
```

### Playwright Tests

```csharp
using Microsoft.Playwright;
using Xunit;

public class MapE2ETests : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private IPage _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = false,
            SlowMo = 100
        });
        _page = await _browser.NewPageAsync();
    }

    [Fact]
    public async Task Map_Loads_And_Displays()
    {
        // Navigate to map page
        await _page.GotoAsync("https://localhost:5001/map");

        // Wait for map to load
        await _page.WaitForSelectorAsync("#map-canvas");

        // Verify map is visible
        var mapElement = await _page.QuerySelectorAsync("#map-canvas");
        Assert.NotNull(mapElement);

        // Take screenshot
        await _page.ScreenshotAsync(new() { Path = "map-loaded.png" });
    }

    [Fact]
    public async Task Map_Pan_Updates_Coordinates()
    {
        await _page.GotoAsync("https://localhost:5001/map");
        await _page.WaitForSelectorAsync("#map-canvas");

        // Get initial coordinates
        var initialCoords = await _page.TextContentAsync("#coordinates");

        // Pan map
        await _page.Mouse.MoveAsync(400, 300);
        await _page.Mouse.DownAsync();
        await _page.Mouse.MoveAsync(200, 300);
        await _page.Mouse.UpAsync();

        // Wait for coordinates to update
        await _page.WaitForTimeoutAsync(500);
        var newCoords = await _page.TextContentAsync("#coordinates");

        Assert.NotEqual(initialCoords, newCoords);
    }

    [Fact]
    public async Task Search_Finds_And_Navigates_To_Location()
    {
        await _page.GotoAsync("https://localhost:5001/map");

        // Type in search box
        await _page.FillAsync("#search-input", "New York");

        // Click search button
        await _page.ClickAsync("#search-button");

        // Wait for results
        await _page.WaitForSelectorAsync(".search-result");

        // Click first result
        await _page.ClickAsync(".search-result:first-child");

        // Wait for map to fly to location
        await _page.WaitForTimeoutAsync(2000);

        // Verify zoom level changed
        var zoomLevel = await _page.TextContentAsync("#zoom-level");
        Assert.Contains("12", zoomLevel);
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }
}
```

### Selenium Alternative

```csharp
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

public class MapSeleniumTests : IDisposable
{
    private readonly IWebDriver _driver;

    public MapSeleniumTests()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");
        _driver = new ChromeDriver(options);
    }

    [Fact]
    public void Map_Loads_Successfully()
    {
        _driver.Navigate().GoToUrl("https://localhost:5001/map");

        var mapElement = _driver.FindElement(By.Id("map-canvas"));
        Assert.NotNull(mapElement);
        Assert.True(mapElement.Displayed);
    }

    public void Dispose()
    {
        _driver.Quit();
    }
}
```

---

## Mock Strategies

### Mock Feature Service

```csharp
public class MockFeatureService : IFeatureService
{
    private readonly List<Feature> _features = new()
    {
        new Feature { Id = "1", Name = "Mock Feature 1", Type = "Point" },
        new Feature { Id = "2", Name = "Mock Feature 2", Type = "Polygon" }
    };

    public Task<List<Feature>> GetFeaturesAsync()
    {
        return Task.FromResult(_features);
    }

    public Task<Feature?> GetFeatureAsync(string id)
    {
        return Task.FromResult(_features.FirstOrDefault(f => f.Id == id));
    }

    public Task<Feature> SaveFeatureAsync(Feature feature)
    {
        _features.Add(feature);
        return Task.FromResult(feature);
    }
}
```

### Using Moq

```csharp
using Moq;

[Fact]
public async Task Component_Loads_Features_On_Init()
{
    // Arrange
    var mockService = new Mock<IFeatureService>();
    mockService.Setup(s => s.GetFeaturesAsync())
        .ReturnsAsync(new List<Feature>
        {
            new Feature { Id = "1", Name = "Test" }
        });

    Services.AddSingleton(mockService.Object);

    // Act
    var cut = RenderComponent<FeatureList>();

    // Assert
    mockService.Verify(s => s.GetFeaturesAsync(), Times.Once);
    Assert.Contains("Test", cut.Markup);
}
```

### Mock ComponentBus

```csharp
public class MockComponentBus : IComponentBus
{
    public List<IComponentBusMessage> PublishedMessages { get; } = new();

    public void Publish<T>(T message) where T : IComponentBusMessage
    {
        PublishedMessages.Add(message);
    }

    public void Subscribe<T>(Action<MessageContext<T>> handler) where T : IComponentBusMessage
    {
        // Implementation
    }

    public void Unsubscribe<T>(Action<MessageContext<T>> handler) where T : IComponentBusMessage
    {
        // Implementation
    }
}

[Fact]
public void Component_Publishes_Message_On_Click()
{
    // Arrange
    var mockBus = new MockComponentBus();
    Services.AddSingleton<IComponentBus>(mockBus);

    var cut = RenderComponent<FeatureCard>();

    // Act
    cut.Find("button").Click();

    // Assert
    Assert.Single(mockBus.PublishedMessages);
    Assert.IsType<FeatureClickedMessage>(mockBus.PublishedMessages[0]);
}
```

---

## Test Data Generation

### Bogus Library

```csharp
// Install: dotnet add package Bogus

using Bogus;

public class TestDataGenerator
{
    public static List<Feature> GenerateFeatures(int count = 100)
    {
        var faker = new Faker<Feature>()
            .RuleFor(f => f.Id, f => f.Random.Guid().ToString())
            .RuleFor(f => f.Name, f => f.Address.StreetAddress())
            .RuleFor(f => f.Type, f => f.PickRandom("Point", "LineString", "Polygon"))
            .RuleFor(f => f.Properties, f => new FeatureProperties
            {
                Name = f.Company.CompanyName(),
                Description = f.Lorem.Sentence(),
                Category = f.PickRandom("Residential", "Commercial", "Industrial")
            })
            .RuleFor(f => f.Geometry, f => new FeatureGeometry
            {
                Type = "Point",
                Coordinates = new List<double[]>
                {
                    new[] { f.Address.Longitude(), f.Address.Latitude() }
                }
            });

        return faker.Generate(count);
    }

    public static List<Property> GenerateProperties(int count = 50)
    {
        var faker = new Faker<Property>()
            .RuleFor(p => p.Id, f => f.Random.Guid().ToString())
            .RuleFor(p => p.Address, f => f.Address.StreetAddress())
            .RuleFor(p => p.City, f => f.Address.City())
            .RuleFor(p => p.State, f => f.Address.StateAbbr())
            .RuleFor(p => p.ZipCode, f => f.Address.ZipCode())
            .RuleFor(p => p.Price, f => f.Random.Decimal(100000, 2000000))
            .RuleFor(p => p.Bedrooms, f => f.Random.Int(1, 6))
            .RuleFor(p => p.Bathrooms, f => f.Random.Double(1, 5))
            .RuleFor(p => p.SquareFeet, f => f.Random.Int(800, 5000))
            .RuleFor(p => p.Latitude, f => f.Address.Latitude())
            .RuleFor(p => p.Longitude, f => f.Address.Longitude());

        return faker.Generate(count);
    }
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
# .github/workflows/test.yml
name: Test

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Run Unit Tests
      run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

    - name: Upload Coverage
      uses: codecov/codecov-action@v3
      with:
        files: ./coverage.cobertura.xml

    - name: Install Playwright
      run: pwsh tests/E2E.Tests/bin/Debug/net8.0/playwright.ps1 install

    - name: Run E2E Tests
      run: dotnet test tests/E2E.Tests --no-build

    - name: Upload Screenshots
      if: failure()
      uses: actions/upload-artifact@v3
      with:
        name: playwright-screenshots
        path: tests/E2E.Tests/screenshots/
```

### Azure DevOps Pipeline

```yaml
# azure-pipelines.yml
trigger:
- main
- develop

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '8.0.x'

- task: DotNetCoreCLI@2
  displayName: 'Restore packages'
  inputs:
    command: 'restore'

- task: DotNetCoreCLI@2
  displayName: 'Build'
  inputs:
    command: 'build'
    arguments: '--configuration Release'

- task: DotNetCoreCLI@2
  displayName: 'Run Tests'
  inputs:
    command: 'test'
    arguments: '--configuration Release --collect:"XPlat Code Coverage"'

- task: PublishCodeCoverageResults@1
  inputs:
    codeCoverageTool: 'Cobertura'
    summaryFileLocation: '$(Agent.TempDirectory)/**/*.cobertura.xml'
```

---

## Best Practices

### Testing Checklist

- [ ] Unit test all public methods
- [ ] Test component rendering with various parameters
- [ ] Test event handlers and callbacks
- [ ] Mock external dependencies
- [ ] Test error conditions and edge cases
- [ ] Integration test API endpoints
- [ ] E2E test critical user flows
- [ ] Generate realistic test data
- [ ] Run tests in CI/CD pipeline
- [ ] Maintain >80% code coverage
- [ ] Test accessibility
- [ ] Test responsive design

---

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*
