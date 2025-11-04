using System;
using System.IO;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Raster.Caching;
using Xunit;

namespace Honua.Server.Core.Tests.Integration.PropertyTests;

/// <summary>
/// Property-based tests for path traversal prevention in file operations.
/// Tests the CacheKeyNormalizer.SanitizeForFilesystem method and related path operations.
/// </summary>
[Trait("Category", "Unit")]
public class PathTraversalPropertyTests
{
    [Property(MaxTest = 500)]
    public Property Sanitize_ShouldPreventDirectoryTraversal()
    {
        return Prop.ForAll(
            GeneratePathTraversalAttempt(),
            maliciousPath =>
            {
                var sanitized = CacheKeyNormalizer.SanitizeForFilesystem(maliciousPath);

                // Should not contain path separators
                Assert.DoesNotContain('/', sanitized);
                Assert.DoesNotContain('\\', sanitized);

                // Ensure no path segment equals '.' or '..'
                var segments = sanitized.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    Assert.NotEqual("..", segment);
                    Assert.NotEqual(".", segment);
                }

                return true;
            });
    }

    [Property(MaxTest = 500)]
    public Property Sanitize_ShouldRemoveInvalidFileNameCharacters()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    return CacheKeyNormalizer.SanitizeForFilesystem(input) == "default";
                }

                var sanitized = CacheKeyNormalizer.SanitizeForFilesystem(input);
                var invalidChars = Path.GetInvalidFileNameChars();

                // Verify no invalid filename characters
                foreach (var ch in invalidChars)
                {
                    Assert.DoesNotContain(ch, sanitized);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property Sanitize_ShouldProduceValidFileName()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            input =>
            {
                var sanitized = CacheKeyNormalizer.SanitizeForFilesystem(input);

                // Result should be a valid filename
                Assert.NotNull(sanitized);
                Assert.NotEmpty(sanitized);

                // Should not introduce path separators when used as a filename
                Assert.DoesNotContain('/', sanitized);
                Assert.DoesNotContain('\\', sanitized);

                // Should not throw when creating a path
                var exception = Record.Exception(() => Path.Combine("base", "subdir", sanitized));
                Assert.Null(exception);

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property Sanitize_ShouldBeDeterministic()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            input =>
            {
                var result1 = CacheKeyNormalizer.SanitizeForFilesystem(input);
                var result2 = CacheKeyNormalizer.SanitizeForFilesystem(input);

                Assert.Equal(result1, result2);

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property Sanitize_ShouldConvertToLowercase()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrWhiteSpace(s)),
            input =>
            {
                var sanitized = CacheKeyNormalizer.SanitizeForFilesystem(input);

                // All alphabetic characters should be lowercase
                Assert.Equal(sanitized, sanitized.ToLowerInvariant());

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property GetRelativePath_ShouldNeverEscapeBaseDirectory()
    {
        return Prop.ForAll(
            GenerateMaliciousTileCacheKey(),
            key =>
            {
                var relativePath = RasterTileCachePathHelper.GetRelativePath(key, '/');

                // Should not contain absolute path markers
                Assert.DoesNotMatch(@"^/", relativePath);
                Assert.DoesNotMatch(@"^[A-Za-z]:", relativePath);
                Assert.DoesNotMatch(@"^\\\\", relativePath); // UNC paths

                var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    Assert.NotEqual("..", segment);
                    Assert.NotEqual(".", segment);
                }

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property ResolveExtension_ShouldNotAllowExecutableExtensions()
    {
        return Prop.ForAll(
            GenerateMaliciousFormat(),
            format =>
            {
                var extension = RasterTileCachePathHelper.ResolveExtension(format);

                // Should not produce executable extensions
                var dangerousExtensions = new[] { "exe", "dll", "bat", "cmd", "sh", "ps1", "vbs", "js", "jar" };
                foreach (var dangerous in dangerousExtensions)
                {
                    Assert.NotEqual(dangerous, extension.TrimStart('.'));
                }

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property GetDatasetPrefix_ShouldBeSafeForConcatenation()
    {
        return Prop.ForAll(
            GeneratePathTraversalAttempt(),
            datasetId =>
            {
                var prefix = RasterTileCachePathHelper.GetDatasetPrefix(datasetId, '/');

                Assert.EndsWith("/", prefix);

                var segment = prefix.TrimEnd('/');
                Assert.NotEqual("..", segment);
                Assert.NotEqual(".", segment);
                Assert.DoesNotContain('/', segment);
                Assert.DoesNotContain('\\', segment);

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property Sanitize_ShouldHandleUnicodePathTraversalAttempts()
    {
        return Prop.ForAll(
            GenerateUnicodePathTraversal(),
            unicodePath =>
            {
                var sanitized = CacheKeyNormalizer.SanitizeForFilesystem(unicodePath);

                // Sanitized strings should not include path separators
                Assert.DoesNotContain('/', sanitized);
                Assert.DoesNotContain('\\', sanitized);

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property Sanitize_ShouldHandleNullByteInjection()
    {
        return Prop.ForAll(
            GenerateNullByteInjection(),
            maliciousInput =>
            {
                var sanitized = CacheKeyNormalizer.SanitizeForFilesystem(maliciousInput);

                // Should not contain null bytes
                Assert.DoesNotContain('\0', sanitized);

                return true;
            });
    }

    // FsCheck Generators

    private static Arbitrary<string> GeneratePathTraversalAttempt()
    {
        var traversalPatterns = new[]
        {
            "../",
            "..\\",
            "../../../etc/passwd",
            "..\\..\\..\\windows\\system32",
            "./../../secret",
            "....//",
            "..../",
            "..;/",
            "..%2F",
            "..%252F",
            "%2e%2e%2f",
            "%2e%2e/",
            "..%5c",
            "..\u2215", // Division slash
            "..\u2216", // Set minus
            "/..",
            "\\..",
            "/./../",
            "/.././",
            "/./././../",
            "..%00.jpg",
            "../../etc/passwd%00.png",
            "~/../etc/passwd",
            "./../",
            "....",
            "..../..../",
            "\u002e\u002e\u002f", // Unicode dots and slash
            "\uFF0E\uFF0E\uFF0F", // Fullwidth dots and slash
            "..\\..\\..\\",
            "/../",
            "/../../",
            "file://../../etc/passwd",
            "...\\...\\",
            ".%252e/"
        };

        return Arb.From(Gen.Elements(traversalPatterns));
    }

    private static Arbitrary<RasterTileCacheKey> GenerateMaliciousTileCacheKey()
    {
        var gen = from dataset in GeneratePathTraversalAttempt().Generator
                  from matrix in GeneratePathTraversalAttempt().Generator
                  from style in GeneratePathTraversalAttempt().Generator
                  from zoom in Gen.Choose(0, 25)
                  from row in Gen.Choose(0, 10000)
                  from col in Gen.Choose(0, 10000)
                  select new RasterTileCacheKey(
                      dataset,
                      matrix,
                      zoom,
                      row,
                      col,
                      style,
                      "image/png",
                      false,
                      256);

        return Arb.From(gen);
    }

    private static Arbitrary<string> GenerateMaliciousFormat()
    {
        var maliciousFormats = new[]
        {
            "image/png;.exe",
            "../../../etc/passwd",
            "image/jpeg%00.exe",
            "image/png\0.bat",
            "../../backdoor.sh",
            "image/png;cmd",
            "application/x-executable",
            "text/x-shellscript"
        };

        return Arb.From(Gen.Elements(maliciousFormats));
    }

    private static Arbitrary<string> GenerateUnicodePathTraversal()
    {
        var unicodeTraversals = new[]
        {
            "\uFF0E\uFF0E\uFF0F", // Fullwidth dots and slash
            "\u2024\u2024\u2215", // Dot leaders and division slash
            "\u002E\u002E\u2215",
            "\uFF0E\u002E/",
            "\u2025\u2215", // Two dot leader and division slash
            ".\u2024/",
            "\uFE52\uFE52\uFF0F" // Small full stops
        };

        return Arb.From(Gen.Elements(unicodeTraversals));
    }

    private static Arbitrary<string> GenerateNullByteInjection()
    {
        var nullByteAttacks = new[]
        {
            "file.png\0.exe",
            "image\0../../etc/passwd",
            "data\0DROP TABLE",
            "test.jpg\0.bat",
            "file\0\0\0.png",
            "valid.png\0; rm -rf /",
            "image.png\0<script>alert(1)</script>"
        };

        return Arb.From(Gen.Elements(nullByteAttacks));
    }
}
