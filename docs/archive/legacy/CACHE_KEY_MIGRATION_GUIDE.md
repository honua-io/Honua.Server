# Cache Key Migration Guide

## Overview

This guide explains how to migrate from the old cache key format to the new collision-resistant format introduced in P3 #49.

## Problem

The previous cache key implementation used only the filename to generate cache keys:

```
Old Format: {fileName}_{variable}_{timeIndex}
Example: temperature_air_temp_0.tif
```

This caused collisions when files with the same name existed in different directories:
- `/data/weather/temperature.nc` → `temperature_air_temp_0.tif`
- `/data/climate/temperature.nc` → `temperature_air_temp_0.tif` ⚠️ COLLISION!

## Solution

The new implementation includes a SHA256 hash of the full path:

```
New Format: {pathHash}_{fileName}_{variable}_{timeIndex}
Example: a1b2c3d4e5f6g7h8_temperature_air_temp_0.tif
```

The path hash is the first 16 hex characters (64 bits) of SHA256(path), providing strong collision resistance.

## Cache Key Format Comparison

### Before (Collision-Prone)
```csharp
private string GetCacheKeyFromUri(string sourceUri, CogConversionOptions options)
{
    var fileName = Path.GetFileNameWithoutExtension(sourceUri);
    var variable = options.VariableName ?? "default";
    var timeIndex = options.TimeIndex?.ToString() ?? "0";
    return $"{fileName}_{variable}_{timeIndex}";
}
```

### After (Collision-Resistant)
```csharp
private string GetCacheKeyFromUri(string sourceUri, CogConversionOptions options)
{
    return CacheKeyGenerator.GenerateCacheKey(sourceUri, options.VariableName, options.TimeIndex);
}
```

## Migration Options

### Option 1: Clear Existing Cache (Recommended)

The simplest approach is to clear the existing cache and let it rebuild naturally:

```bash
# Stop the application
systemctl stop honua-server

# Clear the COG cache directory
rm -rf /var/cache/honua/cog/*

# Start the application
systemctl start honua-server
```

**Pros:**
- Simple and safe
- Guarantees no collisions
- No risk of stale data

**Cons:**
- Temporary performance impact as cache rebuilds
- Requires re-conversion of source files

### Option 2: Gradual Migration (Zero Downtime)

Keep both old and new cache entries temporarily:

1. Deploy the new code
2. New cache entries use the new format
3. Old entries remain accessible
4. Set up a cleanup job to remove old entries after N days

```csharp
// Example cleanup script (run as scheduled task)
var cacheDir = "/var/cache/honua/cog";
var oldFormatPattern = "^[^_]+_[^_]+_[^_]+\\.tif$"; // No path hash prefix

foreach (var file in Directory.GetFiles(cacheDir, "*.tif"))
{
    var filename = Path.GetFileName(file);

    // Check if file uses old format (no 16-char hex prefix)
    if (Regex.IsMatch(filename, oldFormatPattern))
    {
        var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(file);

        // Delete files older than 7 days
        if (fileAge > TimeSpan.FromDays(7))
        {
            File.Delete(file);
        }
    }
}
```

**Pros:**
- Zero downtime
- No immediate performance impact
- Gradual transition

**Cons:**
- More complex
- Temporary storage overhead
- Requires cleanup script

### Option 3: Pre-Migration with Rehashing

For large deployments with extensive caches, pre-migrate files:

```bash
#!/bin/bash
# pre-migrate-cache.sh

CACHE_DIR="/var/cache/honua/cog"
LOG_FILE="/var/log/honua/cache-migration.log"

echo "Starting cache migration at $(date)" >> "$LOG_FILE"

# For each cached file
find "$CACHE_DIR" -name "*.tif" -type f | while read -r oldPath; do
    filename=$(basename "$oldPath")

    # Check if already in new format (has 16-char hex prefix)
    if [[ $filename =~ ^[0-9a-f]{16}_ ]]; then
        echo "Skipping $filename (already migrated)" >> "$LOG_FILE"
        continue
    fi

    # Parse old format: {fileName}_{variable}_{timeIndex}.tif
    # This requires custom logic based on your specific cache structure
    # For safety, we recommend Option 1 (clear cache) instead

    echo "Would migrate $filename (manual intervention required)" >> "$LOG_FILE"
done

echo "Migration scan complete at $(date)" >> "$LOG_FILE"
```

**Note:** Pre-migration is complex because the old cache keys don't include the original source path. **We recommend Option 1 instead.**

## Verification

After migration, verify the new cache keys:

```bash
# Check cache directory
ls -la /var/cache/honua/cog/

# New format should show files like:
# a1b2c3d4e5f6g7h8_temperature_air_temp_0.tif
# 3c5d7e9f1a2b4d6e_precipitation_precip_1.tif

# Old format would look like:
# temperature_air_temp_0.tif (NO PATH HASH!)
# precipitation_precip_1.tif
```

### API Verification

Check cache statistics via the admin API:

```bash
curl -X GET http://localhost:5000/admin/cache/statistics

# Expected response:
{
  "totalEntries": 42,
  "totalSizeBytes": 1073741824,
  "hitRate": 0.85,
  "lastCleanup": "2025-10-18T12:00:00Z",
  "cacheHits": 1000,
  "cacheMisses": 176,
  "collisionDetections": 0  # Should be 0 after migration!
}
```

## Collision Detection

The new system includes collision detection logging:

```csharp
// Automatically logs warnings if collisions are detected
[2025-10-18 12:00:00] [WARN] Potential cache key collision detected!
    Key: a1b2c3d4e5f6g7h8_temp_default_0,
    Existing Source: /data/dir1/temp.nc,
    New Source: /data/dir2/temp.nc
```

Monitor logs for collision warnings:

```bash
# Check for collision warnings
grep "collision detected" /var/log/honua/server.log

# If collisions are found after migration, investigate immediately!
```

## Backward Compatibility

The new implementation maintains backward compatibility for dataset-based keys:

```csharp
// Dataset ID-based keys (no path hash needed)
CacheKeyGenerator.GenerateCacheKeyFromDatasetId("weather-temp-2023", "air_temp", 0);
// Result: weather-temp-2023_air_temp_0

// Path-based keys (includes path hash)
CacheKeyGenerator.GenerateCacheKey("/data/weather/temp.nc", "air_temp", 0);
// Result: a1b2c3d4e5f6g7h8_temp_air_temp_0
```

## Performance Impact

### Storage
- Cache key length increased by 17 characters (16-char hash + underscore)
- Filesystem impact: negligible (modern filesystems handle long names efficiently)

### Computation
- SHA256 hashing: ~1-2 microseconds per key generation
- Impact: negligible compared to COG conversion time (seconds to minutes)

### Cache Hit Rate
- No impact on cache hit rate
- Improved reliability (no false hits from collisions)

## Monitoring

Set up monitoring for cache health:

```yaml
# prometheus-rules.yml
groups:
  - name: honua_cache
    rules:
      - alert: CacheCollisionDetected
        expr: honua_cache_collision_detections_total > 0
        for: 5m
        annotations:
          summary: "Cache key collisions detected"
          description: "{{ $value }} cache key collisions in the last 5 minutes"

      - alert: LowCacheHitRate
        expr: honua_cache_hit_rate < 0.7
        for: 10m
        annotations:
          summary: "Low cache hit rate"
          description: "Cache hit rate is {{ $value }}, expected > 0.7"
```

## Rollback Plan

If issues arise, rollback is straightforward:

1. Revert to previous deployment
2. Cache entries from new version remain but won't be found
3. Old cache entries will continue to work
4. Clear cache after stabilization

```bash
# Emergency rollback
kubectl rollout undo deployment/honua-server

# Or with systemd
systemctl stop honua-server
# Deploy previous version
systemctl start honua-server
```

## Best Practices

1. **Schedule Migration During Low Traffic**: Minimize user impact
2. **Monitor Collision Metrics**: Watch for unexpected collisions
3. **Backup Cache Before Migration**: Optional but recommended for large caches
4. **Test in Staging First**: Validate migration process
5. **Document Custom Paths**: If using non-standard cache locations

## Support

If you encounter issues during migration:

1. Check logs: `/var/log/honua/server.log`
2. Verify cache directory permissions
3. Monitor disk space
4. Review collision detection metrics

For further assistance, see:
- [Cache Architecture Documentation](./docs/rag/03-architecture/tile-caching.md)
- [Troubleshooting Guide](./docs/rag/05-02-common-issues.md)
- [GitHub Issues](https://github.com/honua/honua/issues)

## Summary

The cache key migration fixes a critical collision issue by including the full path hash. We recommend **Option 1 (Clear Cache)** for most deployments due to its simplicity and safety. Monitor collision detection metrics after migration to ensure the fix is effective.
