# Quick Start Guide for New Developers

Get up and running with Honua Server development in 5 minutes!

## Prerequisites

Before starting, ensure you have:

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Docker Desktop** - [Download](https://www.docker.com/products/docker-desktop)
- **Git** - [Download](https://git-scm.com/downloads)
- **IDE** - [VS Code](https://code.visualstudio.com/), [Visual Studio 2022](https://visualstudio.microsoft.com/), or [Rider](https://www.jetbrains.com/rider/)

## 5-Minute Setup

### 1. Clone the Repository

```bash
git clone https://github.com/honua-io/Honua.Server.git
cd Honua.Server
```

### 2. Run Setup Script

**Linux/macOS:**
```bash
./scripts/setup-dev.sh
```

**Windows:**
```powershell
.\scripts\setup-dev.ps1
```

This script will:
- ✅ Check for required dependencies
- ✅ Install Git pre-commit hooks
- ✅ Start PostgreSQL and Redis in Docker
- ✅ Restore NuGet packages
- ✅ Build the solution
- ✅ Run unit tests

### 3. Start the Server

```bash
dotnet run --project src/Honua.Server.Host
```

The server will start at:
- **API**: http://localhost:8080
- **Swagger UI**: http://localhost:8080/swagger
- **Health Check**: http://localhost:8080/healthz/live

### 4. Verify It's Working

Open your browser and navigate to:
- http://localhost:8080/swagger

You should see the Swagger UI with all available API endpoints.

**Try making a request:**
```bash
curl http://localhost:8080/healthz/live
```

Expected response:
```json
{
  "status": "Healthy",
  "timestamp": "2024-11-11T10:00:00Z"
}
```

## First Code Changes

Let's make a simple change to understand the development workflow.

### Walkthrough: Add a New API Endpoint

#### 1. Create a New Feature Service

Create a file: `src/Honua.Server.Core/Services/HelloService.cs`

```csharp
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Services;

public interface IHelloService
{
    string GetGreeting(string name);
}

public class HelloService : IHelloService
{
    private readonly ILogger<HelloService> _logger;

    public HelloService(ILogger<HelloService> logger)
    {
        _logger = logger;
    }

    public string GetGreeting(string name)
    {
        _logger.LogInformation("Generating greeting for {Name}", name);
        return $"Hello, {name}! Welcome to Honua Server!";
    }
}
```

#### 2. Register the Service

Add to `src/Honua.Server.Host/Program.cs` in the service registration section:

```csharp
// Add this line with other service registrations
builder.Services.AddScoped<IHelloService, HelloService>();
```

#### 3. Add an API Endpoint

Add to `src/Honua.Server.Host/Program.cs` in the endpoint mapping section:

```csharp
// Add this endpoint mapping
app.MapGet("/api/hello/{name}", (string name, IHelloService helloService) =>
{
    var greeting = helloService.GetGreeting(name);
    return Results.Ok(new { message = greeting });
})
.WithName("SayHello")
.WithTags("Demo")
.Produces<object>(200);
```

#### 4. Test Your Changes

**Restart the server:**
```bash
# Press Ctrl+C to stop
dotnet run --project src/Honua.Server.Host
```

**Test the new endpoint:**
```bash
curl http://localhost:8080/api/hello/Developer
```

Expected response:
```json
{
  "message": "Hello, Developer! Welcome to Honua Server!"
}
```

**Or visit in browser:**
- http://localhost:8080/api/hello/YourName

#### 5. Format Your Code

Before committing, format your code:

```bash
dotnet format
```

## Running Your First Test

### Understanding Test Structure

Tests are organized by category:
- **Unit Tests**: Fast, isolated tests (`Category=Unit`)
- **Integration Tests**: Tests with database (`Category=Integration`)
- **OGC Conformance Tests**: Standard compliance tests (`Category=OGC`)

### Run All Tests

```bash
dotnet test
```

### Run Only Unit Tests (Fast)

```bash
dotnet test --filter "Category=Unit"
```

### Write a Test for Your New Service

Create: `tests/Honua.Server.Core.Tests/Services/HelloServiceTests.cs`

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Honua.Server.Core.Services;

namespace Honua.Server.Core.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public class HelloServiceTests
{
    private readonly Mock<ILogger<HelloService>> _loggerMock;
    private readonly HelloService _service;

    public HelloServiceTests()
    {
        _loggerMock = new Mock<ILogger<HelloService>>();
        _service = new HelloService(_loggerMock.Object);
    }

    [TestMethod]
    public void GetGreeting_WithName_ReturnsPersonalizedGreeting()
    {
        // Arrange
        var name = "Alice";

        // Act
        var result = _service.GetGreeting(name);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains(name));
        Assert.IsTrue(result.Contains("Hello"));
    }

    [TestMethod]
    public void GetGreeting_WithEmptyName_ReturnsGreeting()
    {
        // Arrange
        var name = "";

        // Act
        var result = _service.GetGreeting(name);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Hello"));
    }
}
```

### Run Your New Test

```bash
dotnet test --filter "FullyQualifiedName~HelloServiceTests"
```

Expected output:
```
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2
```

## Making Your First Pull Request

### 1. Create a Feature Branch

Follow our branch naming convention:

```bash
git checkout -b feature/add-hello-endpoint
```

### 2. Stage Your Changes

```bash
git add .
```

### 3. Commit Your Changes

Follow our commit message guidelines:

```bash
git commit -m "feat(api): add hello endpoint with personalized greeting

Add new /api/hello/{name} endpoint that returns a personalized
greeting message. Includes service layer implementation and unit tests.

Closes #123"
```

**Note:** The pre-commit hook will automatically:
- Check code formatting
- Build the project
- Run unit tests

If any check fails, fix the issues and try again.

### 4. Push Your Branch

```bash
git push origin feature/add-hello-endpoint
```

### 5. Create a Pull Request

1. Go to https://github.com/honua-io/Honua.Server
2. Click "Compare & pull request"
3. Fill out the PR template:

```markdown
## Summary
Added a new hello endpoint that returns personalized greetings

## Changes
- Added HelloService with greeting logic
- Added /api/hello/{name} endpoint
- Added unit tests for HelloService

## Testing
- Tested manually with curl
- Added unit tests with 100% coverage
- All existing tests pass

## Checklist
- [x] Tests added/updated
- [x] Documentation updated
- [x] Code follows style guidelines
- [x] All CI checks passing
```

4. Click "Create pull request"

## Project Structure Overview

Understanding where things live:

```
Honua.Server/
├── src/
│   ├── Honua.Server.Core/          # Core business logic
│   │   ├── Services/               # Business services
│   │   ├── Models/                 # Domain models
│   │   ├── DataProviders/          # Data access layer
│   │   └── Extensions/             # Extension methods
│   │
│   ├── Honua.Server.Host/          # Main application
│   │   ├── Program.cs              # Application entry point
│   │   ├── appsettings.json        # Configuration
│   │   └── Properties/             # Launch settings
│   │
│   ├── Honua.MapSDK/               # Map components (Blazor)
│   ├── HonuaField/                 # Mobile app (.NET MAUI)
│   └── Honua.Cli/                  # Command-line tools
│
├── tests/
│   ├── Honua.Server.Core.Tests/    # Unit tests
│   └── Honua.Server.Integration.Tests/  # Integration tests
│
├── docs/                           # Documentation
│   ├── development/                # Developer guides
│   ├── api/                        # API documentation
│   └── deployment/                 # Deployment guides
│
├── scripts/                        # Development scripts
│   ├── setup-dev.sh                # Setup for Linux/macOS
│   ├── setup-dev.ps1               # Setup for Windows
│   ├── run-tests.sh                # Run tests
│   └── format-code.sh              # Format code
│
├── .vscode/                        # VS Code configuration
│   ├── launch.json                 # Debug configurations
│   ├── tasks.json                  # Tasks
│   └── settings.json               # Workspace settings
│
├── docker-compose.yml              # Local development stack
├── CONTRIBUTING.md                 # Contribution guidelines
└── README.md                       # Project overview
```

## Common Development Tasks

### Building

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/Honua.Server.Host

# Build in Release mode
dotnet build -c Release
```

### Running Tests

```bash
# All tests
dotnet test

# Unit tests only (fast)
dotnet test --filter "Category=Unit"

# Integration tests (requires Docker)
dotnet test --filter "Category=Integration"

# Specific test class
dotnet test --filter "FullyQualifiedName~FeatureServiceTests"

# With code coverage
./scripts/run-tests.sh
```

### Code Formatting

```bash
# Check formatting
dotnet format --verify-no-changes

# Apply formatting
dotnet format

# Or use script
./scripts/format-code.sh
```

### Database Management

```bash
# Reset database (WARNING: Destroys all data)
./scripts/reset-db.sh

# Load test data
./scripts/seed-data.sh

# Run migrations
dotnet ef database update --project src/Honua.Server.Host
```

### Docker Commands

```bash
# Start all services
docker compose up -d

# Start specific services
docker compose up -d postgres redis

# View logs
docker compose logs -f honua-server

# Stop services
docker compose down

# Reset everything (removes volumes)
docker compose down -v
```

### Working with Dependencies

```bash
# Check for outdated packages
dotnet list package --outdated

# Update specific package
dotnet add package Npgsql --version 8.0.0

# Restore packages
dotnet restore
```

## IDE-Specific Setup

### Visual Studio Code

**Recommended Extensions** (auto-suggested when you open the project):
- C# Dev Kit
- C#
- EditorConfig for VS Code
- Docker
- GitLens

**Keyboard Shortcuts:**
- `F5` - Start debugging
- `Ctrl+Shift+B` - Build
- `Ctrl+Shift+P` - Command palette
- `F12` - Go to definition
- `Shift+F12` - Find all references

### Visual Studio 2022

**Useful Windows:**
- Solution Explorer (`Ctrl+Alt+L`)
- Error List (`Ctrl+\, E`)
- Package Manager Console (`Ctrl+\, Ctrl+`)
- Test Explorer (`Ctrl+E, T`)

**Productivity Tips:**
- Right-click project → Manage NuGet Packages
- Use Ctrl+. for quick actions
- Ctrl+K, Ctrl+D to format document

### JetBrains Rider

**Productivity Features:**
- Alt+Enter for quick fixes
- Ctrl+Shift+A for actions
- Double Shift for search everywhere
- Ctrl+T for Go to Type

## Debugging Basics

### Set a Breakpoint

1. Open a file (e.g., `HelloService.cs`)
2. Click in the left margin next to a line number (red dot appears)
3. Start debugging (`F5`)
4. Make a request that hits that code
5. Execution pauses at breakpoint

### Inspect Variables

While debugging:
- **Hover** over variables to see values
- **Watch window** to monitor specific expressions
- **Immediate/Debug Console** to execute code

### Common Debug Configurations

**VS Code** (`.vscode/launch.json`):
- "Debug Honua Server" - Start with debugging
- "Debug Tests" - Debug current test file
- "Attach to Docker" - Debug containerized app

See [debugging.md](debugging.md) for detailed debugging guide.

## Getting Help

### Documentation

- **CONTRIBUTING.md** - Full contribution guidelines
- **docs/development/debugging.md** - Debugging guide
- **docs/api/** - API documentation
- **docs/README.md** - Documentation index

### Community

- **GitHub Discussions** - Ask questions, share ideas
  - https://github.com/honua-io/Honua.Server/discussions

- **GitHub Issues** - Report bugs, request features
  - https://github.com/honua-io/Honua.Server/issues

### Code Style

Our code follows:
- **.editorconfig** - Automated formatting rules
- **C# Coding Conventions** - Microsoft guidelines
- **Pre-commit hooks** - Automatic quality checks

## Next Steps

Now that you're set up, explore:

1. **[CONTRIBUTING.md](../../CONTRIBUTING.md)** - Detailed contribution guidelines
2. **[debugging.md](debugging.md)** - Advanced debugging techniques
3. **[docs/architecture/](../architecture/)** - Understand the architecture
4. **[docs/api/](../api/)** - Learn the API structure
5. **Pick an issue** - Find "good first issue" labels on GitHub

## Quick Reference

### Essential Commands

| Task | Command |
|------|---------|
| Start server | `dotnet run --project src/Honua.Server.Host` |
| Run tests | `dotnet test` |
| Format code | `dotnet format` |
| Build | `dotnet build` |
| Start Docker | `docker compose up -d` |
| Reset database | `./scripts/reset-db.sh` |

### File Locations

| What | Where |
|------|-------|
| Main application | `src/Honua.Server.Host/Program.cs` |
| Core services | `src/Honua.Server.Core/Services/` |
| Tests | `tests/Honua.Server.Core.Tests/` |
| Configuration | `src/Honua.Server.Host/appsettings.json` |
| Docker setup | `docker-compose.yml` |

### Port Mappings

| Service | Port | URL |
|---------|------|-----|
| Honua Server | 8080 | http://localhost:8080 |
| Swagger UI | 8080 | http://localhost:8080/swagger |
| PostgreSQL | 5432 | localhost:5432 |
| Redis | 6379 | localhost:6379 |
| Seq (logs) | 5341 | http://localhost:5341 |
| Jaeger (traces) | 16686 | http://localhost:16686 |

---

**Welcome to Honua Server development!** 🌍

Have questions? Check out [CONTRIBUTING.md](../../CONTRIBUTING.md) or ask in [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions).
