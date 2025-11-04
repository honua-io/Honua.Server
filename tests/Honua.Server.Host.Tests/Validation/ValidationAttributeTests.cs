using System.ComponentModel.DataAnnotations;
using Honua.Server.Host.Validation;
using Xunit;

namespace Honua.Server.Host.Tests.Validation;

[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class ValidationAttributeTests
{
    [Theory]
    [InlineData("valid_collection-123", true)]
    [InlineData("simple", true)]
    [InlineData("my-collection", true)]
    [InlineData("test_collection_123", true)]
    [InlineData("", true)] // Empty is allowed (use [Required] for enforcement)
    [InlineData("invalid collection", false)] // Spaces not allowed
    [InlineData("collection@name", false)] // @ not allowed
    [InlineData("../../../etc/passwd", false)] // Path traversal
    [InlineData("collection;DROP TABLE users;", false)] // SQL injection attempt
    public void CollectionNameAttribute_ValidatesCorrectly(string value, bool expectedValid)
    {
        var attribute = new CollectionNameAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(45.5, true)]
    [InlineData(90, true)]
    [InlineData(-90, true)]
    [InlineData(-45.5, true)]
    [InlineData(90.00001, false)]
    [InlineData(-90.00001, false)]
    [InlineData(91, false)]
    [InlineData(-91, false)]
    [InlineData(180, false)]
    public void LatitudeAttribute_ValidatesRange(double value, bool expectedValid)
    {
        var attribute = new LatitudeAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(180, true)]
    [InlineData(-180, true)]
    [InlineData(179.999999, true)]
    [InlineData(-179.999999, true)]
    [InlineData(180.00001, false)]
    [InlineData(-180.00001, false)]
    [InlineData(181, false)]
    [InlineData(-181, false)]
    public void LongitudeAttribute_ValidatesRange(double value, bool expectedValid)
    {
        var attribute = new LongitudeAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData("""{"type":"Point","coordinates":[0,0]}""", true)]
    [InlineData("""{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}""", true)]
    [InlineData("", true)] // Empty is allowed
    [InlineData(null, true)] // Null is allowed
    [InlineData("{}", false)] // Invalid GeoJSON
    [InlineData("not json", false)] // Not JSON
    [InlineData("""{"type":"InvalidType","coordinates":[0,0]}""", false)] // Invalid type
    public void GeoJsonAttribute_ValidatesGeoJson(string? value, bool expectedValid)
    {
        var attribute = new GeoJsonAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(30, true)]
    [InlineData(-1, false)]
    [InlineData(31, false)]
    public void ZoomLevelAttribute_ValidatesRange(int value, bool expectedValid)
    {
        var attribute = new ZoomLevelAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(512, true)]
    [InlineData(1024, true)]
    [InlineData(100, false)] // Not a standard size
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(8192, false)] // Too large
    public void TileSizeAttribute_ValidatesStandardSizes(int value, bool expectedValid)
    {
        var attribute = new TileSizeAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1024, true)]
    [InlineData(1024 * 1024, true)] // 1 MB
    [InlineData(1024 * 1024 * 100, true)] // 100 MB
    [InlineData(-1, false)] // Negative
    public void FileSizeAttribute_ValidatesSize()
    {
        var attribute = new FileSizeAttribute(1024 * 1024 * 100); // 100 MB max
        var context = new ValidationContext(new object());

        var validResult = attribute.GetValidationResult(1024L, context);
        Assert.Equal(ValidationResult.Success, validResult);

        var tooLargeResult = attribute.GetValidationResult(1024L * 1024 * 101, context);
        Assert.NotEqual(ValidationResult.Success, tooLargeResult);

        var negativeResult = attribute.GetValidationResult(-1L, context);
        Assert.NotEqual(ValidationResult.Success, negativeResult);
    }

    [Theory]
    [InlineData("2024-01-01T00:00:00Z", true)]
    [InlineData("2024-01-01", true)]
    [InlineData("2024-01-01T00:00:00Z/2024-12-31T23:59:59Z", true)] // Interval
    [InlineData("", true)] // Empty allowed
    [InlineData(null, true)] // Null allowed
    [InlineData("not a date", false)]
    [InlineData("2024-13-01", false)] // Invalid month
    [InlineData("2024-01-01/", false)] // Incomplete interval
    public void Iso8601DateTimeAttribute_ValidatesDateTimes(string? value, bool expectedValid)
    {
        var attribute = new Iso8601DateTimeAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/webp", true)]
    [InlineData("IMAGE/PNG", true)] // Case insensitive
    [InlineData("", true)] // Empty allowed
    [InlineData("image/gif", false)] // Not in allowed list
    [InlineData("application/json", false)]
    public void AllowedMimeTypesAttribute_ValidatesMimeTypes()
    {
        var attribute = new AllowedMimeTypesAttribute("image/png", "image/jpeg", "image/webp");
        var context = new ValidationContext(new object());

        foreach (var (value, expectedValid) in new[]
        {
            ("image/png", true),
            ("image/jpeg", true),
            ("image/webp", true),
            ("IMAGE/PNG", true),
            ("", true),
            ("image/gif", false),
            ("application/json", false)
        })
        {
            var result = attribute.GetValidationResult(value, context);
            Assert.Equal(expectedValid, result == ValidationResult.Success);
        }
    }

    [Theory]
    [InlineData("safe/path/to/file.txt", true)]
    [InlineData("file.txt", true)]
    [InlineData("", true)] // Empty allowed
    [InlineData("../../../etc/passwd", false)]
    [InlineData("..\\..\\..\\windows\\system32", false)]
    [InlineData("%2e%2e/etc/passwd", false)]
    [InlineData("file/../../../etc/passwd", false)]
    public void NoPathTraversalAttribute_DetectsPathTraversal(string value, bool expectedValid)
    {
        var attribute = new NoPathTraversalAttribute();
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Theory]
    [InlineData("normal string", true)]
    [InlineData("string with\nnewline", true)] // Newlines allowed
    [InlineData("string with\ttab", true)] // Tabs allowed
    [InlineData("", true)] // Empty allowed
    [InlineData("a\u0000b", false)] // Null character
    [InlineData("a\u0001b", false)] // Control character
    [InlineData("a\u001Fb", false)] // Control character
    public void SafeStringAttribute_DetectsControlCharacters(string value, bool expectedValid)
    {
        var attribute = new SafeStringAttribute(1000);
        var context = new ValidationContext(new object());
        var result = attribute.GetValidationResult(value, context);

        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    [Fact]
    public void SafeStringAttribute_ValidatesMaxLength()
    {
        var attribute = new SafeStringAttribute(10);
        var context = new ValidationContext(new object());

        var validResult = attribute.GetValidationResult("short", context);
        Assert.Equal(ValidationResult.Success, validResult);

        var tooLongResult = attribute.GetValidationResult("this is way too long for the limit", context);
        Assert.NotEqual(ValidationResult.Success, tooLongResult);
    }
}
