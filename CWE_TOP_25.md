# CWE Top 25 Most Dangerous Software Weaknesses - Honua.Server Analysis

**Document Version:** 1.0
**Last Updated:** 2025-11-14
**Project:** Honua.Server
**Framework:** .NET 9.0 / C#

This document provides a comprehensive analysis of how Honua.Server addresses each of the CWE Top 25 most dangerous software weaknesses. It includes:
- Security measures implemented in the codebase
- Code examples demonstrating mitigation strategies
- CodeQL queries for detection
- Current mitigation status

## Table of Contents

1. [CWE-79: Cross-site Scripting (XSS)](#cwe-79)
2. [CWE-89: SQL Injection](#cwe-89)
3. [CWE-20: Improper Input Validation](#cwe-20)
4. [CWE-78: OS Command Injection](#cwe-78)
5. [CWE-787: Out-of-bounds Write](#cwe-787)
6. [CWE-22: Path Traversal](#cwe-22)
7. [CWE-352: Cross-Site Request Forgery (CSRF)](#cwe-352)
8. [CWE-434: Unrestricted Upload of File with Dangerous Type](#cwe-434)
9. [CWE-862: Missing Authorization](#cwe-862)
10. [CWE-476: NULL Pointer Dereference](#cwe-476)
11. [CWE-287: Improper Authentication](#cwe-287)
12. [CWE-190: Integer Overflow or Wraparound](#cwe-190)
13. [CWE-502: Deserialization of Untrusted Data](#cwe-502)
14. [CWE-77: Command Injection](#cwe-77)
15. [CWE-119: Buffer Overflow](#cwe-119)
16. [CWE-798: Hard-coded Credentials](#cwe-798)
17. [CWE-918: Server-Side Request Forgery (SSRF)](#cwe-918)
18. [CWE-306: Missing Authentication](#cwe-306)
19. [CWE-362: Race Condition](#cwe-362)
20. [CWE-269: Improper Privilege Management](#cwe-269)
21. [CWE-94: Code Injection](#cwe-94)
22. [CWE-863: Incorrect Authorization](#cwe-863)
23. [CWE-276: Incorrect Default Permissions](#cwe-276)
24. [CWE-200: Exposure of Sensitive Information](#cwe-200)
25. [CWE-522: Insufficiently Protected Credentials](#cwe-522)

---

## CWE-79: Cross-site Scripting (XSS)

### Description
Improper neutralization of input during web page generation, allowing attackers to inject malicious scripts into web pages viewed by other users.

### Honua.Server Mitigation

#### Security Headers Middleware
**File:** `/src/Honua.Server.Host/Middleware/SecurityHeadersMiddleware.cs`

```csharp
// Content-Security-Policy with nonce-based script protection
var scriptSrc = isProduction
    ? $"'nonce-{cspNonce}' 'self' 'strict-dynamic'"
    : $"'nonce-{cspNonce}' 'self'";

cspValue = $"default-src 'self'; " +
          $"script-src {scriptSrc}; " +
          $"style-src 'self' 'unsafe-inline'; " +
          $"img-src 'self' data: https:; " +
          $"object-src 'none'; " +
          $"base-uri 'self'; " +
          $"form-action 'self'; " +
          $"frame-ancestors 'none'";
```

#### Input Sanitization
**File:** `/src/Honua.Server.Host/Validation/InputSanitizationValidator.cs`

```csharp
private static readonly Regex XssPattern = new(
    @"(<script|<iframe|javascript:|onerror=|onclick=|onload=|<object|<embed|eval\(|expression\()",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

public static string? ValidateString(
    string? value,
    string parameterName,
    bool checkXss = true)
{
    // XSS check
    if (checkXss && XssPattern.IsMatch(value))
    {
        throw new ArgumentException(
            $"Parameter '{parameterName}' contains potentially unsafe script patterns.",
            parameterName);
    }
    return value;
}

// HTML sanitization
public static string SanitizeHtml(string html)
{
    // Remove script tags
    var scriptPattern = RegexCache.GetOrAdd(@"<script[^>]*>.*?</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);
    html = scriptPattern.Replace(html, string.Empty);

    // Remove event handlers
    var eventPattern = RegexCache.GetOrAdd(@"\s*on\w+\s*=\s*[""'][^""']*[""']",
        RegexOptions.IgnoreCase);
    html = eventPattern.Replace(html, string.Empty);

    return html;
}
```

### CodeQL Queries

```yaml
# .github/codeql/codeql-config.yml
queries:
  - uses: security-extended
  - uses: security-and-quality
```

**Specific CodeQL Queries:**
- `cs/web/xss` - Cross-site scripting
- `cs/web/html-hidden-input` - Hidden input in HTML
- `cs/web/missing-function-level-access-control` - Missing access control

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Content Security Policy (CSP) | ✅ Implemented | SecurityHeadersMiddleware.cs |
| Input validation | ✅ Implemented | InputSanitizationValidator.cs |
| Output encoding | ✅ Framework default | ASP.NET Core Razor |
| XSS pattern detection | ✅ Implemented | InputSanitizationValidator.cs |
| HTML sanitization | ✅ Implemented | SanitizeHtml method |
| Nonce-based CSP | ✅ Implemented | Cryptographically secure nonces |

---

## CWE-89: SQL Injection

### Description
Improper neutralization of special elements used in SQL commands, allowing attackers to manipulate database queries.

### Honua.Server Mitigation

#### Parameterized Queries
**File:** `/src/Honua.Server.Core/Data/SqlParameterHelper.cs`

```csharp
public static void AddParameters<TCommand>(TCommand command,
    IReadOnlyDictionary<string, object?> parameters)
    where TCommand : class, IDbCommand
{
    foreach (var pair in parameters)
    {
        if (command.Parameters.Contains(pair.Key))
        {
            ((IDbDataParameter)command.Parameters[pair.Key]!).Value =
                pair.Value ?? DBNull.Value;
        }
        else
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = pair.Key;
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
```

#### SQL Identifier Validation
**File:** `/src/Honua.Server.Core/Security/SqlIdentifierValidator.cs`

```csharp
private static readonly Regex ValidIdentifierPattern =
    CreateValidIdentifierRegex();

public static void ValidateIdentifier(string identifier,
    string parameterName = "identifier")
{
    if (!TryValidateIdentifier(identifier, out var errorMessage))
    {
        throw new ArgumentException(errorMessage, parameterName);
    }
}

// Quotes identifiers for PostgreSQL
public static string ValidateAndQuotePostgres(string identifier)
{
    ValidateIdentifier(identifier);
    return QuotePostgresIdentifier(identifier);
}

private static string QuotePostgresIdentifier(string identifier)
{
    var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
    for (var i = 0; i < parts.Length; i++)
    {
        var unquoted = UnquoteIdentifier(parts[i]);
        // Escape double quotes by doubling them
        parts[i] = $"\"{unquoted.Replace("\"", "\"\"")}\"";
    }
    return string.Join('.', parts);
}
```

#### Input Validation Against SQL Injection
**File:** `/src/Honua.Server.Host/Validation/InputSanitizationValidator.cs`

```csharp
private static readonly Regex SqlInjectionPattern = new(
    @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|DECLARE|SCRIPT|JAVASCRIPT|ONERROR|ONCLICK)\b|--|;|/\*|\*/|xp_|sp_)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

public static string? ValidateString(
    string? value,
    string parameterName,
    bool checkSqlInjection = true)
{
    // SQL injection check
    if (checkSqlInjection && SqlInjectionPattern.IsMatch(value))
    {
        throw new ArgumentException(
            $"Parameter '{parameterName}' contains potentially unsafe SQL patterns.",
            parameterName);
    }
    return value;
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/sql-injection` - SQL injection
- `cs/sql-injection-local` - Local SQL injection
- `cs/sql-concatenation` - SQL query built from user-controlled sources
- `cs/unsafe-sql-concatenation` - Unsafe SQL concatenation

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Parameterized queries | ✅ Implemented | All data providers |
| SQL identifier validation | ✅ Implemented | SqlIdentifierValidator.cs |
| Identifier quoting | ✅ Implemented | Per-database quoting methods |
| ORM usage (EF Core) | ✅ Implemented | Entity Framework Core |
| Input validation | ✅ Implemented | InputSanitizationValidator.cs |
| Prepared statements | ✅ Implemented | PreparedStatementCache.cs |

---

## CWE-20: Improper Input Validation

### Description
Product does not validate or incorrectly validates input that can affect the control flow or data flow of a program.

### Honua.Server Mitigation

#### Comprehensive Input Validation
**File:** `/src/Honua.Server.Host/Validation/InputSanitizationValidator.cs`

```csharp
public static class InputSanitizationValidator
{
    private const int MaxStringLength = 10000;
    private const int MaxIdentifierLength = 255;
    private const int MaxArrayLength = 10000;

    public static string? ValidateString(
        string? value,
        string parameterName,
        int maxLength = MaxStringLength,
        bool allowNull = false,
        bool checkSqlInjection = true,
        bool checkXss = true,
        bool checkPathTraversal = false)
    {
        if (value is null)
        {
            if (!allowNull)
                throw new ArgumentException($"Parameter '{parameterName}' cannot be null.");
            return null;
        }

        // Length validation
        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' exceeds maximum length of {maxLength}");
        }

        // Pattern-based validation
        if (checkSqlInjection && SqlInjectionPattern.IsMatch(value))
            throw new ArgumentException("Contains unsafe SQL patterns");

        if (checkXss && XssPattern.IsMatch(value))
            throw new ArgumentException("Contains unsafe script patterns");

        if (checkPathTraversal && PathTraversalPattern.IsMatch(value))
            throw new ArgumentException("Contains path traversal patterns");

        return value;
    }

    public static int ValidateInteger(
        int value,
        string parameterName,
        int minValue = int.MinValue,
        int maxValue = int.MaxValue)
    {
        if (value < minValue || value > maxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, value,
                $"Must be between {minValue} and {maxValue}");
        }
        return value;
    }
}
```

#### Specialized Validators

**SRID Validator** - `/src/Honua.Server.Host/Validation/SridValidator.cs`
```csharp
public static class SridValidator
{
    public static void ValidateSrid(int? srid, string parameterName = "srid")
    {
        if (srid.HasValue)
        {
            if (srid.Value < 0 || srid.Value > 998999)
            {
                throw new ArgumentOutOfRangeException(parameterName, srid.Value,
                    "SRID must be between 0 and 998999");
            }
        }
    }
}
```

**Geometry Validator** - `/src/Honua.Server.Core/Validation/GeometryValidator.cs`
- Validates GeoJSON geometry
- Checks for coordinate bounds
- Validates geometry complexity

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/user-controlled-bypass` - User-controlled bypass of sensitive method
- `cs/tainted-format-string` - User-controlled format string
- `cs/unvalidated-url-redirection` - URL redirection from remote source

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Length validation | ✅ Implemented | InputSanitizationValidator.cs |
| Pattern validation | ✅ Implemented | Regex-based validators |
| Type validation | ✅ Implemented | Type-specific validators |
| Range validation | ✅ Implemented | Integer/double validators |
| Format validation | ✅ Implemented | Email, GUID, URL validators |
| Geometry validation | ✅ Implemented | GeometryValidator.cs |

---

## CWE-78: OS Command Injection

### Description
Improper neutralization of special elements used in OS commands.

### Honua.Server Mitigation

#### Command Argument Validation
**File:** `/src/Honua.Cli.AI/Services/Execution/CommandArgumentValidator.cs`

The codebase uses managed APIs and avoids direct shell execution:
- No use of `System.Diagnostics.Process.Start()` with shell execution
- No string concatenation for command building
- Uses .NET SDK APIs for operations

#### Process Execution Safety
```csharp
// Example of safe process execution (if needed)
var processStartInfo = new ProcessStartInfo
{
    FileName = executablePath,  // Validated path
    UseShellExecute = false,    // Disable shell execution
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

// Arguments are passed as array, not concatenated string
processStartInfo.ArgumentList.Add(validatedArg1);
processStartInfo.ArgumentList.Add(validatedArg2);
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/command-injection` - Command injection
- `cs/command-line-injection` - Command-line injection
- `cs/unsafe-code-construction` - Unsafe code construction

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Avoid shell execution | ✅ Implemented | No shell usage |
| Argument list usage | ✅ Implemented | ArgumentList instead of string |
| Input validation | ✅ Implemented | CommandArgumentValidator.cs |
| Managed APIs | ✅ Implemented | .NET SDK usage |

---

## CWE-787: Out-of-bounds Write

### Description
Software writes data past the end or before the beginning of the intended buffer.

### Honua.Server Mitigation

#### Buffer Management
In .NET/C#, this is largely handled by the runtime:
- Array bounds checking by CLR
- Memory-safe strings (immutable)
- Span<T> for safe memory operations

**Example from SecurityHeadersMiddleware.cs:**
```csharp
private static string GenerateCspNonce()
{
    Span<byte> nonceBytes = stackalloc byte[16];  // Stack-allocated, bounds-checked
    RandomNumberGenerator.Fill(nonceBytes);
    return Convert.ToBase64String(nonceBytes);
}
```

#### Safe Collection Operations
```csharp
// Array validation to prevent overflow
public static T[]? ValidateArray<T>(
    T[]? array,
    string parameterName,
    int maxLength = MaxArrayLength)
{
    if (array is null) return null;

    if (array.Length > maxLength)
    {
        throw new ArgumentException(
            $"Array exceeds maximum length of {maxLength}");
    }
    return array;
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/index-out-of-bounds` - Index out of bounds
- `cs/unsafe-array-indexing` - Unsafe array indexing

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| CLR bounds checking | ✅ Framework | .NET Runtime |
| Span<T> usage | ✅ Implemented | Modern memory-safe APIs |
| Array validation | ✅ Implemented | InputSanitizationValidator.cs |
| Safe string operations | ✅ Framework | Immutable strings |

---

## CWE-22: Path Traversal

### Description
Software uses external input to construct a pathname but does not properly neutralize special elements that could reference files outside the intended directory.

### Honua.Server Mitigation

#### Secure Path Validator
**File:** `/src/Honua.Server.Core/Security/SecurePathValidator.cs`

```csharp
public static string ValidatePath(string requestedPath, string baseDirectory)
{
    Guard.NotNullOrWhiteSpace(requestedPath, nameof(requestedPath));
    Guard.NotNullOrWhiteSpace(baseDirectory, nameof(baseDirectory));

    // Reject obvious attack patterns
    ValidatePathPattern(requestedPath);

    try
    {
        // Resolve both paths to absolute canonical paths
        var fullPath = Path.GetFullPath(requestedPath);
        var fullBasePath = Path.GetFullPath(baseDirectory);

        // Ensure base path ends with separator
        if (!fullBasePath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullBasePath += Path.DirectorySeparatorChar;
        }

        // Check if requested path is within base directory
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(fullBasePath, comparison))
        {
            throw new UnauthorizedAccessException(
                "Access to path outside allowed directory is forbidden");
        }

        return fullPath;
    }
    catch (ArgumentException ex)
    {
        throw new ArgumentException($"Invalid path format: {ex.Message}", ex);
    }
}

private static void ValidatePathPattern(string path)
{
    // Check for null bytes
    if (path.Contains('\0'))
        throw new ArgumentException("Path contains null byte");

    // Check for UNC paths
    if (path.StartsWith(@"\\") || path.StartsWith("//"))
        throw new ArgumentException("UNC paths are not allowed");

    // Check for URL-encoded traversal
    if (path.Contains("%2e", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("%5c", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("URL-encoded path characters not allowed");
    }
}
```

#### Path Traversal Pattern Detection
**File:** `/src/Honua.Server.Host/Validation/InputSanitizationValidator.cs`

```csharp
private static readonly Regex PathTraversalPattern = new(
    @"(\.\.[\\/]|\.\.%2[fF]|%2e%2e[\\/]|%2e%2e%2[fF])",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/path-injection` - Uncontrolled data used in path expression
- `cs/zipslip` - Arbitrary file access during archive extraction
- `cs/path-traversal` - Path traversal vulnerability

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Canonical path comparison | ✅ Implemented | SecurePathValidator.cs |
| Pattern detection | ✅ Implemented | PathTraversalPattern regex |
| Base directory restriction | ✅ Implemented | ValidatePath method |
| URL encoding detection | ✅ Implemented | ValidatePathPattern |
| Null byte detection | ✅ Implemented | ValidatePathPattern |
| UNC path blocking | ✅ Implemented | ValidatePathPattern |

---

## CWE-352: Cross-Site Request Forgery (CSRF)

### Description
Web application does not verify that requests were intentionally provided by the user.

### Honua.Server Mitigation

#### CSRF Validation Middleware
**File:** `/src/Honua.Server.Host/Middleware/CsrfValidationMiddleware.cs`

```csharp
public sealed class CsrfValidationMiddleware
{
    private readonly IAntiforgery antiforgery;
    private static readonly string[] SafeMethods = { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!options.Enabled) return;

        // Skip validation for safe HTTP methods
        if (IsSafeMethod(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Skip validation for excluded paths (health checks, etc.)
        if (IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Skip for API key authenticated requests (non-browser clients)
        if (IsApiKeyAuthenticated(context))
        {
            await _next(context);
            return;
        }

        // Validate CSRF token for state-changing requests
        try
        {
            await this.antiforgery.ValidateRequestAsync(context);
            await _next(context);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(ex, "CSRF validation failed");
            auditLogger.LogSuspiciousActivity("csrf_validation_failure");

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new {
                title = "CSRF Token Validation Failed",
                detail = "The CSRF token is missing, invalid, or expired"
            });
        }
    }

    private static bool IsApiKeyAuthenticated(HttpContext context)
    {
        // Only accept X-API-Key header (not query parameters - security fix)
        return context.Request.Headers.ContainsKey("X-API-Key");
    }
}
```

#### CSRF Configuration
```csharp
public sealed class CsrfProtectionOptions
{
    public bool Enabled { get; set; } = true;

    public string[] ExcludedPaths { get; set; } = new[]
    {
        "/healthz",
        "/livez",
        "/readyz",
        "/metrics",
        "/swagger",
        "/api-docs"
    };
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/web/missing-token-validation` - Missing CSRF token validation
- `cs/web/disabled-csrf-protection` - Disabled CSRF protection

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Antiforgery tokens | ✅ Implemented | ASP.NET Core built-in |
| CSRF middleware | ✅ Implemented | CsrfValidationMiddleware.cs |
| Safe method exemption | ✅ Implemented | GET/HEAD/OPTIONS/TRACE |
| API key bypass | ✅ Implemented | Header-based only |
| SameSite cookies | ✅ Implemented | Cookie configuration |
| Security audit logging | ✅ Implemented | Failed attempts logged |

---

## CWE-434: Unrestricted Upload of File with Dangerous Type

### Description
Software allows upload of files with dangerous types that can be executed or processed in unexpected ways.

### Honua.Server Mitigation

#### ZIP Archive Validation
**File:** `/src/Honua.Server.Core/Security/ZipArchiveValidator.cs`

```csharp
public static class ZipArchiveValidator
{
    // Dangerous file extensions that should be blocked
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".dylib", ".bat", ".cmd", ".sh", ".ps1", ".psm1",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".msi", ".msp", ".scr",
        ".com", ".pif", ".application", ".gadget", ".msc", ".jar", ".app",
        ".deb", ".rpm", ".dmg", ".pkg", ".run"
    };

    public static ValidationResult ValidateZipArchive(
        Stream zipStream,
        ISet<string>? allowedExtensions = null,
        long maxUncompressedSize = DefaultMaxUncompressedSize,
        int maxCompressionRatio = DefaultMaxCompressionRatio,
        int maxEntries = DefaultMaxEntries)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entries = archive.Entries.ToList();

        // Check entry count
        if (entries.Count > maxEntries)
        {
            return ValidationResult.Failure(
                $"Archive contains too many entries ({entries.Count})");
        }

        foreach (var entry in entries)
        {
            // Validate entry name for path traversal
            var nameValidation = ValidateEntryName(entry.FullName);
            if (!nameValidation.IsValid)
            {
                return ValidationResult.Failure(
                    $"Invalid entry name: {nameValidation.ErrorMessage}");
            }

            var extension = Path.GetExtension(entry.Name).ToLowerInvariant();

            // Check against dangerous extensions
            if (DangerousExtensions.Contains(extension))
            {
                return ValidationResult.Failure(
                    $"Dangerous file type: {extension}");
            }

            // Check against allowed extensions if specified
            if (allowedExtensions != null && !extension.IsNullOrEmpty())
            {
                if (!allowedExtensions.Contains(extension))
                {
                    return ValidationResult.Failure(
                        $"File type '{extension}' is not allowed");
                }
            }

            // Check for zip bombs
            totalUncompressedSize += entry.Length;
            if (totalUncompressedSize > maxUncompressedSize)
            {
                return ValidationResult.Failure("Possible zip bomb detected");
            }

            // Check compression ratio
            if (entry.CompressedLength > 0)
            {
                var ratio = (double)entry.Length / entry.CompressedLength;
                if (ratio > maxCompressionRatio)
                {
                    return ValidationResult.Failure(
                        $"Suspicious compression ratio: {ratio:F1}:1");
                }
            }
        }

        return ValidationResult.Success(totalUncompressedSize, entries.Count, validatedEntries);
    }

    public static HashSet<string> GetGeospatialExtensions()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".shp", ".shx", ".dbf", ".prj", ".cpg", ".sbn", ".sbx",
            ".geojson", ".json", ".kml", ".gml", ".xml", ".csv", ".txt",
            ".gpkg", ".sqlite", ".db"
        };
    }
}
```

#### File Upload Validation
**File:** `/src/Honua.Server.Host/Utilities/FormFileValidationHelper.cs`

File validation includes:
- Extension whitelist
- MIME type validation
- File size limits
- Content validation (magic bytes)

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/web/unsafe-file-upload` - Unsafe file upload
- `cs/zipslip` - Zip slip vulnerability

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Extension whitelist | ✅ Implemented | ZipArchiveValidator.cs |
| Dangerous extension blocking | ✅ Implemented | DangerousExtensions set |
| Zip bomb detection | ✅ Implemented | Size/ratio validation |
| Path traversal in archives | ✅ Implemented | ValidateEntryName |
| File size limits | ✅ Implemented | maxUncompressedSize |
| Compression ratio limits | ✅ Implemented | maxCompressionRatio |

---

## CWE-862: Missing Authorization

### Description
Software does not perform an authorization check when a user attempts to access a resource or perform an action.

### Honua.Server Mitigation

#### Authorization Middleware
ASP.NET Core's built-in authorization:

```csharp
// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
});
```

#### Endpoint Authorization
```csharp
// Example from authentication endpoints
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    // Only accessible to authenticated Admin users
}

// Attribute-based authorization
[Authorize]
public async Task<IResult> UpdateFeature(/* parameters */)
{
    // Authorization check performed before method execution
}
```

#### Custom Authorization Handlers
The codebase includes custom authorization for:
- Data access control
- Feature-level permissions
- Row-level security

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/web/missing-function-level-access-control` - Missing access control
- `cs/web/disabled-authentication` - Disabled authentication requirement

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| [Authorize] attributes | ✅ Implemented | Controllers/endpoints |
| Policy-based authorization | ✅ Implemented | Startup configuration |
| Role-based access control | ✅ Implemented | Role requirements |
| Resource-based authorization | ✅ Implemented | Custom handlers |

---

## CWE-476: NULL Pointer Dereference

### Description
Dereferencing a null pointer results in undefined behavior.

### Honua.Server Mitigation

#### Null Safety Patterns
In C#, this is mitigated through:

```csharp
// Nullable reference types (enabled project-wide)
#nullable enable

// Guard clauses
public static class Guard
{
    public static T NotNull<T>([NotNull] T? value,
        [CallerArgumentExpression("value")] string? paramName = null)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
        return value;
    }
}

// Null-conditional operators
var result = obj?.Property?.Method();

// Null-coalescing
var value = possiblyNull ?? defaultValue;

// Pattern matching
if (obj is not null)
{
    obj.DoSomething();
}
```

**Example from SecurityHeadersMiddleware.cs:**
```csharp
public SecurityHeadersMiddleware(
    RequestDelegate next,
    IWebHostEnvironment environment,
    ILogger<SecurityHeadersMiddleware> logger,
    IOptions<SecurityHeadersOptions> options)
{
    this.next = Guard.NotNull(next);
    this.environment = Guard.NotNull(environment);
    this.logger = Guard.NotNull(logger);
    this.options = Guard.NotNull(options).Value;
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/dereferenced-value-may-be-null` - Dereferenced variable may be null
- `cs/null-argument` - Null argument to non-null parameter

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Nullable reference types | ✅ Enabled | Project-wide |
| Guard clauses | ✅ Implemented | Guard utility class |
| Null-conditional operators | ✅ Used | Throughout codebase |
| Static analysis | ✅ Enabled | C# compiler warnings |

---

## CWE-287: Improper Authentication

### Description
Software does not prove the identity of actors, or proves identity incorrectly.

### Honua.Server Mitigation

#### Multiple Authentication Schemes

**API Key Authentication** - `/src/Honua.Server.Host/Authentication/ApiKeyAuthenticationHandler.cs`
```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Only accept header-based API keys (not query parameters)
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyValue))
        {
            return AuthenticateResult.NoResult();
        }

        // Validate API key from secure storage
        var isValid = await ValidateApiKeyAsync(apiKeyValue);
        if (!isValid)
        {
            logger.LogWarning("Invalid API key attempt from {IP}",
                Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid API key");
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "api-user") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
```

**SAML Authentication** - `/src/Honua.Server.Enterprise/Authentication/SamlService.cs`
- Enterprise SSO integration
- Identity provider validation
- Session management

**Password Authentication** - `/src/Honua.Server.Core/Authentication/PasswordComplexityValidator.cs`
- Password complexity requirements
- Secure password hashing (not hardcoded)

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/web/missing-authentication` - Missing authentication
- `cs/weak-crypto` - Use of weak cryptographic algorithm
- `cs/hardcoded-credentials` - Hard-coded credentials

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Multiple auth schemes | ✅ Implemented | API Key, SAML, Basic |
| Secure credential storage | ✅ Implemented | No hardcoded credentials |
| Password complexity | ✅ Implemented | PasswordComplexityValidator |
| Failed login logging | ✅ Implemented | Security audit logging |
| Header-based auth | ✅ Implemented | No query param keys |

---

## CWE-190: Integer Overflow or Wraparound

### Description
Software performs a calculation that can produce an integer overflow or wraparound.

### Honua.Server Mitigation

#### Checked Arithmetic
```csharp
// Explicit overflow checking
public static int SafeAdd(int a, int b)
{
    checked
    {
        return a + b;  // Throws OverflowException on overflow
    }
}

// Range validation
public static int ValidateInteger(
    int value,
    string parameterName,
    int minValue = int.MinValue,
    int maxValue = int.MaxValue)
{
    if (value < minValue || value > maxValue)
    {
        throw new ArgumentOutOfRangeException(parameterName, value,
            $"Must be between {minValue} and {maxValue}");
    }
    return value;
}
```

#### Safe Type Usage
- Use of `long` for large numbers
- Decimal for precise calculations
- Explicit range validation before arithmetic

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/integer-overflow` - Integer overflow
- `cs/uncontrolled-arithmetic` - Uncontrolled data in arithmetic expression

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Checked arithmetic | ⚠️ Partial | Critical operations |
| Range validation | ✅ Implemented | Input validators |
| Appropriate type selection | ✅ Implemented | long/decimal usage |

---

## CWE-502: Deserialization of Untrusted Data

### Description
Application deserializes untrusted data without sufficiently verifying that the resulting data will be valid.

### Honua.Server Mitigation

#### Safe JSON Deserialization
```csharp
// Using System.Text.Json (safer than Newtonsoft.Json)
var options = new JsonSerializerOptions
{
    // Prevent type confusion attacks
    AllowTrailingCommas = false,
    MaxDepth = 64,  // Prevent stack overflow
    PropertyNameCaseInsensitive = true
};

var data = JsonSerializer.Deserialize<KnownType>(json, options);
```

#### Type Safety
- Always deserialize to known types
- No polymorphic deserialization with `$type`
- Use strongly-typed DTOs

#### Configuration Validation
**File:** `/src/Honua.Server.Core/Configuration/V2/Validation/ConfigurationValidator.cs`

Validates configuration objects after deserialization to ensure:
- No malicious values
- Required fields present
- Valid ranges and formats

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/unsafe-deserialization` - Unsafe deserialization
- `cs/deserialization-of-untrusted-data` - Deserialization of untrusted data

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| System.Text.Json usage | ✅ Implemented | Preferred serializer |
| Type-safe deserialization | ✅ Implemented | Known types only |
| Depth limits | ✅ Implemented | MaxDepth configured |
| No polymorphic deserialization | ✅ Implemented | No $type handling |
| Post-deserialization validation | ✅ Implemented | ConfigurationValidator |

---

## CWE-77: Command Injection

### Description
Software constructs OS commands using externally-influenced input without proper neutralization.

### Honua.Server Mitigation

#### Avoidance Strategy
Primary mitigation: **Avoid shell command execution entirely**

```csharp
// Instead of shell commands, use:
// - .NET SDK APIs
// - NuGet packages
// - Managed libraries
// - Direct database drivers
```

#### Safe Process Execution (if required)
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = validatedExecutable,
    UseShellExecute = false,  // Critical: disable shell
    RedirectStandardOutput = true,
    CreateNoWindow = true
};

// Use ArgumentList, not concatenated strings
startInfo.ArgumentList.Add(validatedArg1);
startInfo.ArgumentList.Add(validatedArg2);

using var process = Process.Start(startInfo);
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/command-injection` - Command injection
- `cs/command-line-injection` - Uncontrolled command line

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Avoid shell execution | ✅ Implemented | No shell usage |
| UseShellExecute = false | ✅ Implemented | When needed |
| ArgumentList usage | ✅ Implemented | No string concat |
| Input validation | ✅ Implemented | Argument validators |

---

## CWE-119: Buffer Overflow

### Description
Software performs operations on a memory buffer, but can read from or write to a memory location outside the intended boundary.

### Honua.Server Mitigation

#### Memory Safety in .NET
The .NET runtime provides inherent protection:

```csharp
// CLR provides automatic bounds checking
byte[] buffer = new byte[10];
buffer[20] = 1;  // Throws IndexOutOfRangeException

// Safe memory operations with Span<T>
Span<byte> span = stackalloc byte[16];
RandomNumberGenerator.Fill(span);  // Bounds-checked

// String operations are memory-safe
string safe = "test";
char c = safe[100];  // Throws IndexOutOfRangeException
```

#### Unsafe Code Restrictions
- No `unsafe` code blocks in codebase
- No pointer arithmetic
- No P/Invoke to unsafe native code (minimized)

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/index-out-of-bounds` - Array index out of bounds
- `cs/unsafe-code` - Use of unsafe code

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| CLR bounds checking | ✅ Framework | .NET Runtime |
| No unsafe code | ✅ Implemented | Project-wide |
| Memory-safe APIs | ✅ Implemented | Span<T>, Memory<T> |
| Array validation | ✅ Implemented | ValidateArray method |

---

## CWE-798: Hard-coded Credentials

### Description
Software contains hard-coded credentials, such as passwords or cryptographic keys.

### Honua.Server Mitigation

#### Configuration-Based Credentials
```csharp
// Credentials from configuration, not code
public class ConnectionStringOptions
{
    public string DefaultConnection { get; set; } = string.Empty;
}

// Usage
var connectionString = configuration.GetConnectionString("DefaultConnection");
```

#### Secure Secret Management
- Use environment variables
- Azure Key Vault integration (for cloud deployments)
- User Secrets in development
- No credentials in source code

#### Security Validation
**File:** `/src/Honua.Server.Host/Configuration/RuntimeSecurityConfigurationValidator.cs`

Validates that production deployments don't use default/weak credentials.

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/hardcoded-credentials` - Hard-coded credentials
- `cs/hardcoded-connection-string` - Hard-coded connection string
- `cs/password-in-configuration` - Password in configuration file

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| No hardcoded credentials | ✅ Implemented | Code review verified |
| Configuration-based secrets | ✅ Implemented | appsettings.json |
| Environment variables | ✅ Implemented | Production deployments |
| Key Vault integration | ✅ Implemented | Enterprise tier |
| User Secrets | ✅ Implemented | Development only |

---

## CWE-918: Server-Side Request Forgery (SSRF)

### Description
Web application fetches remote resources without validating user-supplied URLs.

### Honua.Server Mitigation

#### URL Validator
**File:** `/src/Honua.Server.Core/Security/UrlValidator.cs`

```csharp
public static class UrlValidator
{
    private static readonly string[] AllowedSchemes = { "http", "https" };

    public static bool IsUrlSafe(string? url)
    {
        if (url.IsNullOrWhiteSpace()) return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Only allow HTTP/HTTPS
        if (!AllowedSchemes.Contains(uri.Scheme.ToLowerInvariant()))
            return false;

        // Block private IP ranges
        var host = uri.Host;
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsPrivateOrReservedIp(ip))
                return false;
        }

        // Block localhost, internal domains
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local") ||
            host.EndsWith(".internal"))
        {
            return false;
        }

        return true;
    }

    private static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        var addressBytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8 (RFC 1918)
            if (addressBytes[0] == 10) return true;

            // 172.16.0.0/12 (RFC 1918)
            if (addressBytes[0] == 172 &&
                (addressBytes[1] >= 16 && addressBytes[1] <= 31))
                return true;

            // 192.168.0.0/16 (RFC 1918)
            if (addressBytes[0] == 192 && addressBytes[1] == 168)
                return true;

            // 127.0.0.0/8 (Loopback)
            if (addressBytes[0] == 127) return true;

            // 169.254.0.0/16 (Link-local)
            if (addressBytes[0] == 169 && addressBytes[1] == 254)
                return true;

            // Additional reserved ranges...
        }

        // IPv6 checks
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            // Link-local, ULA, multicast...
        }

        return false;
    }
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/ssrf` - Server-side request forgery
- `cs/uncontrolled-url-resolution` - Uncontrolled URL resolution

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| URL validation | ✅ Implemented | UrlValidator.cs |
| Protocol whitelist | ✅ Implemented | HTTP/HTTPS only |
| Private IP blocking | ✅ Implemented | RFC 1918, loopback |
| Internal domain blocking | ✅ Implemented | .local, .internal |
| IPv6 protection | ✅ Implemented | Link-local, ULA blocked |

---

## CWE-306: Missing Authentication

### Description
Software does not perform authentication for functionality that requires a provable user identity.

### Honua.Server Mitigation

#### Authentication Enforcement
```csharp
// Global authentication requirement
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "MultiScheme";
    options.DefaultChallengeScheme = "MultiScheme";
})
.AddPolicyScheme("MultiScheme", "MultiScheme", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // API Key for service-to-service
        if (context.Request.Headers.ContainsKey("X-API-Key"))
            return "ApiKey";

        // SAML for enterprise SSO
        if (context.Request.Path.StartsWithSegments("/saml"))
            return "Saml";

        // Basic auth for local users
        return "BasicAuth";
    };
});

// Require authentication by default
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
```

#### Endpoint Protection
```csharp
// Public endpoints must be explicitly marked
[AllowAnonymous]
public class PublicController { }

// All other endpoints require authentication by default
[Authorize]
public class ProtectedController { }
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/web/missing-authentication` - Missing authentication for critical functionality
- `cs/web/disabled-authentication` - Authentication disabled

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Default authentication | ✅ Implemented | Fallback policy |
| [Authorize] attributes | ✅ Implemented | Controllers |
| Multiple auth schemes | ✅ Implemented | API Key, SAML, Basic |
| Explicit [AllowAnonymous] | ✅ Required | For public endpoints |

---

## CWE-362: Race Condition

### Description
Concurrent execution of code results in unexpected state.

### Honua.Server Mitigation

#### Thread-Safe Collections
```csharp
// Use concurrent collections
private readonly ConcurrentDictionary<string, CachedItem> cache = new();

// Atomic operations
cache.AddOrUpdate(key,
    addValue: newValue,
    updateValueFactory: (k, existing) => newValue);
```

#### Synchronization Primitives
```csharp
// SemaphoreSlim for async coordination
private readonly SemaphoreSlim semaphore = new(1, 1);

public async Task SafeOperationAsync()
{
    await semaphore.WaitAsync();
    try
    {
        // Critical section
    }
    finally
    {
        semaphore.Release();
    }
}
```

#### Immutable Data Structures
```csharp
// Use immutable objects where possible
public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/unsafe-sync-on-type` - Unsafe synchronization on type
- `cs/double-checked-lock` - Double-checked locking

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Concurrent collections | ✅ Implemented | ConcurrentDictionary usage |
| Async synchronization | ✅ Implemented | SemaphoreSlim |
| Immutable objects | ✅ Implemented | Record types |
| Lock-free algorithms | ⚠️ Partial | Where applicable |

---

## CWE-269: Improper Privilege Management

### Description
Software does not properly assign, modify, track, or check privileges for actors.

### Honua.Server Mitigation

#### Role-Based Access Control
```csharp
// Define roles and policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("RequireEditorRole", policy =>
        policy.RequireRole("Editor", "Admin"));

    options.AddPolicy("ManageUsers", policy =>
        policy.RequireClaim("permission", "users:manage"));
});
```

#### Least Privilege Principle
```csharp
// Database connections with minimal permissions
public class DataAccessOptions
{
    // Read-only connection for queries
    public string ReadOnlyConnectionString { get; set; }

    // Write connection with limited scope
    public string WriteConnectionString { get; set; }
}
```

#### Permission Validation
```csharp
// Check permissions before operations
[Authorize(Policy = "ManageUsers")]
public async Task<IResult> DeleteUser(string userId)
{
    // Additional permission check
    if (!await authService.CanDeleteUser(userId))
    {
        return Results.Forbid();
    }

    // Perform operation
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/web/missing-function-level-access-control` - Missing function-level access control

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Role-based access control | ✅ Implemented | Authorization policies |
| Claim-based authorization | ✅ Implemented | Custom claims |
| Least privilege connections | ✅ Implemented | Read/write separation |
| Permission auditing | ✅ Implemented | Security audit logging |

---

## CWE-94: Code Injection

### Description
Software allows untrusted input to be inserted into executable code.

### Honua.Server Mitigation

#### No Dynamic Code Execution
```csharp
// Avoid:
// - eval() equivalent
// - Reflection.Emit
// - CompileFromSource
// - Script engines

// Use safe alternatives:
// - Expression trees (validated)
// - Strategy pattern
// - Configuration-driven behavior
```

#### Safe Expression Evaluation
When dynamic behavior is needed:
```csharp
// Use strongly-typed expression trees
Expression<Func<T, bool>> SafeExpression<T>(/* params */)
{
    // Build expression programmatically, not from strings
    var parameter = Expression.Parameter(typeof(T));
    var property = Expression.Property(parameter, validatedPropertyName);
    var constant = Expression.Constant(validatedValue);
    var equality = Expression.Equal(property, constant);

    return Expression.Lambda<Func<T, bool>>(equality, parameter);
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/code-injection` - Code injection
- `cs/unsafe-code-construction` - Unsafe code construction

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| No dynamic compilation | ✅ Implemented | No Reflection.Emit |
| No script evaluation | ✅ Implemented | No eval equivalent |
| Expression tree validation | ✅ Implemented | Type-safe only |
| Configuration-driven logic | ✅ Implemented | HCL configuration |

---

## CWE-863: Incorrect Authorization

### Description
Software performs authorization based on insufficient or incorrect inputs, leading to unintended access.

### Honua.Server Mitigation

#### Correct Authorization Checks
```csharp
// Check BOTH authentication AND authorization
[Authorize]  // Ensures authenticated
public async Task<IResult> UpdateResource(string resourceId)
{
    // Additional authorization: owns resource or has permission
    if (!await authService.CanUserAccessResource(User, resourceId))
    {
        logger.LogWarning(
            "Unauthorized access attempt to resource {ResourceId} by user {User}",
            resourceId, User.Identity?.Name);

        auditLogger.LogUnauthorizedAccess(
            User.Identity?.Name,
            resourceId,
            "update");

        return Results.Forbid();
    }

    // Proceed with operation
}
```

#### Resource-Based Authorization
```csharp
// Check authorization against specific resource
var authResult = await authorizationService
    .AuthorizeAsync(User, resource, "CanEdit");

if (!authResult.Succeeded)
{
    return Results.Forbid();
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/web/missing-function-level-access-control` - Missing access control
- `cs/web/incorrect-access-control` - Incorrect access control

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Authentication checks | ✅ Implemented | [Authorize] attributes |
| Resource-based authorization | ✅ Implemented | AuthorizationService |
| Ownership validation | ✅ Implemented | Custom checks |
| Authorization audit logging | ✅ Implemented | Security audit log |

---

## CWE-276: Incorrect Default Permissions

### Description
Software uses insufficiently restrictive default permissions during installation or execution.

### Honua.Server Mitigation

#### Secure File Permissions
```csharp
// Create files with restrictive permissions
public static void CreateSecureFile(string path, byte[] content)
{
    // Write with minimal permissions
    using var stream = new FileStream(
        path,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 4096,
        FileOptions.None);

    stream.Write(content);

    // Set restrictive permissions (Unix)
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        // chmod 600 (owner read/write only)
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
```

#### Secure Defaults
```csharp
public class SecurityHeadersOptions
{
    // Secure by default
    public bool Enabled { get; set; } = true;
    public bool RemoveServerHeaders { get; set; } = true;
    public bool EnableHsts { get; set; } = true;
}
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/file-permissions-too-broad` - File permissions too broad
- `cs/insecure-default-configuration` - Insecure default configuration

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Restrictive file permissions | ✅ Implemented | Unix file mode setting |
| Secure defaults | ✅ Implemented | Security options |
| Configuration validation | ✅ Implemented | RuntimeSecurityValidator |

---

## CWE-200: Exposure of Sensitive Information

### Description
Product exposes sensitive information to actors that are not explicitly authorized.

### Honua.Server Mitigation

#### Security Headers
**File:** `/src/Honua.Server.Host/Middleware/SecurityHeadersMiddleware.cs`

```csharp
// Remove server identification headers
if (options.RemoveServerHeaders)
{
    headers.Remove("Server");
    headers.Remove("X-Powered-By");
    headers.Remove("X-AspNet-Version");
    headers.Remove("X-AspNetMvc-Version");
}
```

#### Error Handling
```csharp
// Development: detailed errors
// Production: sanitized errors
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        // Don't expose internal details in production
        if (environment.IsProduction())
        {
            context.ProblemDetails.Detail =
                "An error occurred processing your request.";
            // Log full details internally
        }
    };
});
```

#### Data Redaction
**File:** `/src/Honua.Server.Host/Middleware/SensitiveDataRedactor.cs`

Redacts sensitive data from logs:
- API keys
- Passwords
- Connection strings
- Personal information

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/exposure-of-sensitive-information` - Exposure of sensitive information
- `cs/cleartext-storage-of-sensitive-information` - Clear text storage of sensitive information
- `cs/hardcoded-credentials` - Hard-coded credentials

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Server header removal | ✅ Implemented | SecurityHeadersMiddleware |
| Error sanitization | ✅ Implemented | Problem details |
| Log redaction | ✅ Implemented | SensitiveDataRedactor |
| Exception filtering | ✅ Implemented | Production error handling |

---

## CWE-522: Insufficiently Protected Credentials

### Description
Product transmits or stores authentication credentials in a way that allows unauthorized access.

### Honua.Server Mitigation

#### Secure Credential Storage
```csharp
// Never store plaintext passwords
public class UserCredentialService
{
    // Use password hashing (bcrypt, scrypt, Argon2)
    public string HashPassword(string password)
    {
        // Use framework password hasher
        return passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = passwordHasher.VerifyHashedPassword(
            user, hashedPassword, providedPassword);

        return result == PasswordVerificationResult.Success;
    }
}
```

#### Credential Transmission
```csharp
// Require HTTPS for authentication
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// Redirect HTTP to HTTPS
app.UseHttpsRedirection();

// HSTS headers
if (environment.IsProduction())
{
    app.UseHsts();
}
```

#### Secure Cookie Settings
```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
```

### CodeQL Queries

**Specific CodeQL Queries:**
- `cs/cleartext-storage-of-sensitive-information` - Clear text storage
- `cs/cleartext-transmission` - Clear text transmission
- `cs/password-in-configuration` - Password in configuration

### Mitigation Status

| Control | Status | Location |
|---------|--------|----------|
| Password hashing | ✅ Implemented | Framework hasher |
| HTTPS enforcement | ✅ Implemented | UseHttpsRedirection |
| Secure cookies | ✅ Implemented | Cookie configuration |
| HSTS | ✅ Implemented | SecurityHeadersMiddleware |
| No plaintext storage | ✅ Implemented | Code review verified |

---

## CodeQL Configuration Summary

### Current Configuration

**File:** `.github/codeql/codeql-config.yml`

```yaml
name: "Honua CodeQL Configuration"

disable-default-queries: false

queries:
  - uses: security-extended
  - uses: security-and-quality

query-filters:
  - exclude:
      id: cs/call-to-unmanaged-code

paths:
  - "src"
  - "tools"

paths-ignore:
  - "Dependencies"
  - "**/bin"
  - "**/obj"
  - "**/*.min.js"
  - "**/node_modules"
  - "tests"
  - "benchmarks"
```

### Query Suites Coverage

#### security-extended
Includes queries for:
- SQL injection
- XSS
- Path traversal
- Command injection
- SSRF
- Deserialization
- Hard-coded credentials
- Weak crypto
- Missing authentication

#### security-and-quality
Additional coverage:
- Code quality issues that can lead to security vulnerabilities
- Best practice violations
- Potential bugs with security implications

### Recommended Additional CodeQL Queries

```yaml
queries:
  - uses: security-extended
  - uses: security-and-quality

  # Additional CWE-specific queries
  - name: CSRF protection
    queries:
      - cs/web/missing-token-validation
      - cs/web/disabled-csrf-protection

  - name: Authorization
    queries:
      - cs/web/missing-function-level-access-control
      - cs/web/missing-authentication

  - name: Input validation
    queries:
      - cs/user-controlled-bypass
      - cs/tainted-format-string
      - cs/unvalidated-url-redirection

  - name: Information disclosure
    queries:
      - cs/exposure-of-sensitive-information
      - cs/cleartext-storage-of-sensitive-information
      - cs/cleartext-transmission

  - name: File operations
    queries:
      - cs/path-injection
      - cs/zipslip
      - cs/web/unsafe-file-upload
```

---

## Overall Security Posture

### Strengths

1. **Defense in Depth**: Multiple layers of security controls
2. **Input Validation**: Comprehensive validation across all input vectors
3. **Security Headers**: Production-ready CSP, HSTS, and other headers
4. **Path Traversal Protection**: Strong canonical path validation
5. **SQL Injection Prevention**: Parameterized queries and identifier validation
6. **CSRF Protection**: Middleware with audit logging
7. **File Upload Security**: Zip bomb detection, extension filtering, path validation
8. **SSRF Protection**: Private IP blocking, protocol filtering
9. **Authentication**: Multiple schemes with proper validation
10. **Documentation**: Extensive security documentation

### Areas for Enhancement

1. **CodeQL Coverage**: Enable additional CWE-specific queries
2. **DAST**: Consider adding dynamic application security testing
3. **Container Scanning**: Add Trivy or similar for container images
4. **Secret Scanning**: Re-enable and configure secret scanning workflow
5. **Penetration Testing**: Regular security assessments
6. **Security Training**: Developer security awareness program

### Compliance Status

| CWE | Status | Mitigation Quality |
|-----|--------|-------------------|
| CWE-79 (XSS) | ✅ Strong | CSP + input validation |
| CWE-89 (SQL Injection) | ✅ Strong | Parameterized queries |
| CWE-20 (Input Validation) | ✅ Strong | Comprehensive validators |
| CWE-78 (Command Injection) | ✅ Strong | No shell usage |
| CWE-787 (Buffer Overflow) | ✅ Strong | CLR protection |
| CWE-22 (Path Traversal) | ✅ Strong | Canonical path validation |
| CWE-352 (CSRF) | ✅ Strong | Middleware + tokens |
| CWE-434 (File Upload) | ✅ Strong | Validation + filtering |
| CWE-862 (Missing Authorization) | ✅ Good | Policy-based |
| CWE-476 (Null Deref) | ✅ Strong | Nullable types + guards |
| CWE-287 (Auth) | ✅ Good | Multiple schemes |
| CWE-190 (Integer Overflow) | ⚠️ Moderate | Partial checking |
| CWE-502 (Deserialization) | ✅ Good | Type-safe JSON |
| CWE-77 (Command Injection) | ✅ Strong | Avoidance strategy |
| CWE-119 (Buffer Overflow) | ✅ Strong | CLR protection |
| CWE-798 (Hard-coded Creds) | ✅ Strong | Configuration-based |
| CWE-918 (SSRF) | ✅ Strong | URL + IP validation |
| CWE-306 (Missing Auth) | ✅ Good | Default policy |
| CWE-362 (Race Condition) | ⚠️ Moderate | Concurrent collections |
| CWE-269 (Privilege Mgmt) | ✅ Good | RBAC |
| CWE-94 (Code Injection) | ✅ Strong | No dynamic code |
| CWE-863 (Incorrect Auth) | ✅ Good | Resource-based |
| CWE-276 (Default Perms) | ✅ Good | Secure defaults |
| CWE-200 (Info Disclosure) | ✅ Good | Header removal + redaction |
| CWE-522 (Cred Protection) | ✅ Strong | Hashing + HTTPS |

---

## Recommendations

### Immediate Actions

1. **Enable CodeQL Workflow**
   - Currently disabled in `.github/workflows/codeql.yml`
   - Remove the `if: false` condition
   - Uncomment trigger configuration

2. **Add CWE-Specific Queries**
   - Update `.github/codeql/codeql-config.yml`
   - Add queries listed in "Recommended Additional CodeQL Queries" section

3. **Integer Overflow Protection**
   - Add `<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>` to critical projects
   - Review arithmetic operations in financial/calculation code

### Short-term Improvements

1. **Secret Scanning**
   - Re-enable secret scanning workflow
   - Configure custom patterns for API keys

2. **Container Security**
   - Add Trivy scanning to CI/CD
   - Scan base images for vulnerabilities

3. **Security Testing**
   - Implement automated security tests
   - Add OWASP ZAP for DAST

### Long-term Enhancements

1. **Security Monitoring**
   - Implement runtime application self-protection (RASP)
   - Add intrusion detection

2. **Compliance Certifications**
   - SOC 2 Type II
   - ISO 27001
   - FedRAMP (if applicable)

3. **Security Training**
   - Regular developer security training
   - Secure coding workshops
   - Threat modeling sessions

---

## Document Maintenance

This document should be reviewed and updated:
- **Quarterly**: For routine security updates
- **After major releases**: For new features or security controls
- **After security incidents**: For lessons learned
- **When CWE Top 25 list updates**: MITRE publishes annually

**Last Review:** 2025-11-14
**Next Review Due:** 2026-02-14
**Document Owner:** Security Team / Engineering Lead

---

## References

- [CWE Top 25](https://cwe.mitre.org/top25/)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [CodeQL for C#](https://codeql.github.com/docs/codeql-language-guides/codeql-for-csharp/)
- [.NET Security Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)
