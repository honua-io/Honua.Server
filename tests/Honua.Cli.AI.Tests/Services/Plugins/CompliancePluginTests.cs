using System;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Honua.Cli.AI.Services.Plugins;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Plugins;

/// <summary>
/// Comprehensive tests for CompliancePlugin - critical standards validation plugin.
/// Tests OGC compliance, STAC validation, GeoJSON validation, and security auditing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Priority", "Critical")]
[Trait("Plugin", "Compliance")]
public class CompliancePluginTests
{
    private readonly CompliancePlugin _plugin;

    public CompliancePluginTests()
    {
        _plugin = new CompliancePlugin();
    }

    #region ValidateOgcApiFeatures Tests

    [Fact]
    public void ValidateOgcApiFeatures_ReturnsCompleteValidation()
    {
        // Arrange
        var endpointUrl = "https://api.example.com";

        // Act
        var result = _plugin.ValidateOgcApiFeatures(endpointUrl);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("endpointUrl").GetString().Should().Be(endpointUrl);
        root.TryGetProperty("conformanceClasses", out _).Should().BeTrue();
        root.TryGetProperty("validationChecks", out _).Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiFeatures_IncludesCoreConformanceClass()
    {
        // Act
        var result = _plugin.ValidateOgcApiFeatures("https://api.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var conformanceClasses = json.RootElement.GetProperty("conformanceClasses");

        var hasCore = false;
        foreach (var cc in conformanceClasses.EnumerateArray())
        {
            if (cc.GetProperty("id").GetString() == "core")
            {
                hasCore = true;
                cc.GetProperty("required").GetBoolean().Should().BeTrue();
                cc.GetProperty("uri").GetString().Should().Contain("ogcapi-features-1");

                var tests = cc.GetProperty("tests");
                tests.GetArrayLength().Should().BeGreaterThan(0);

                var testList = tests.EnumerateArray().Select(t => t.GetString()).ToList();
                testList.Should().Contain("Landing page");
                testList.Should().Contain("Collections");
            }
        }

        hasCore.Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiFeatures_IncludesGeoJSONConformanceClass()
    {
        // Act
        var result = _plugin.ValidateOgcApiFeatures("https://api.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var conformanceClasses = json.RootElement.GetProperty("conformanceClasses");

        var hasGeoJSON = false;
        foreach (var cc in conformanceClasses.EnumerateArray())
        {
            if (cc.GetProperty("id").GetString() == "geojson")
            {
                hasGeoJSON = true;
                cc.GetProperty("required").GetBoolean().Should().BeTrue();
            }
        }

        hasGeoJSON.Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiFeatures_IncludesHTMLConformanceClass()
    {
        // Act
        var result = _plugin.ValidateOgcApiFeatures("https://api.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var conformanceClasses = json.RootElement.GetProperty("conformanceClasses");

        var hasHTML = false;
        foreach (var cc in conformanceClasses.EnumerateArray())
        {
            if (cc.GetProperty("id").GetString() == "html")
            {
                hasHTML = true;
                cc.GetProperty("required").GetBoolean().Should().BeFalse(); // HTML is optional
            }
        }

        hasHTML.Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiFeatures_IncludesValidationChecks()
    {
        // Act
        var result = _plugin.ValidateOgcApiFeatures("https://api.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var validationChecks = json.RootElement.GetProperty("validationChecks");

        validationChecks.GetArrayLength().Should().BeGreaterThan(0);

        foreach (var check in validationChecks.EnumerateArray())
        {
            check.TryGetProperty("check", out _).Should().BeTrue();
            check.TryGetProperty("command", out _).Should().BeTrue();
            check.GetProperty("check").GetString().Should().NotBeNullOrEmpty();
            check.GetProperty("command").GetString().Should().Contain("curl");
        }
    }

    [Fact]
    public void ValidateOgcApiFeatures_ValidatesLandingPage()
    {
        // Act
        var result = _plugin.ValidateOgcApiFeatures("https://api.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var validationChecks = json.RootElement.GetProperty("validationChecks");

        var hasLandingPageCheck = false;
        foreach (var check in validationChecks.EnumerateArray())
        {
            if (check.GetProperty("check").GetString()!.Contains("Landing page"))
            {
                hasLandingPageCheck = true;
            }
        }

        hasLandingPageCheck.Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiFeatures_ValidatesConformanceEndpoint()
    {
        // Act
        var result = _plugin.ValidateOgcApiFeatures("https://api.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var validationChecks = json.RootElement.GetProperty("validationChecks");

        var hasConformanceCheck = false;
        foreach (var check in validationChecks.EnumerateArray())
        {
            if (check.GetProperty("check").GetString()!.Contains("Conformance"))
            {
                hasConformanceCheck = true;
                check.GetProperty("command").GetString().Should().Contain("/conformance");
            }
        }

        hasConformanceCheck.Should().BeTrue();
    }

    #endregion

    #region ValidateOgcApiTiles Tests

    [Fact]
    public void ValidateOgcApiTiles_ReturnsCompleteValidation()
    {
        // Arrange
        var endpointUrl = "https://tiles.example.com";

        // Act
        var result = _plugin.ValidateOgcApiTiles(endpointUrl);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("endpointUrl").GetString().Should().Be(endpointUrl);
        root.TryGetProperty("requirements", out _).Should().BeTrue();
        root.TryGetProperty("mvtValidation", out _).Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiTiles_IncludesTileMatrixSetsRequirement()
    {
        // Act
        var result = _plugin.ValidateOgcApiTiles("https://tiles.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("requirements");

        var hasTileMatrixSets = false;
        foreach (var req in requirements.EnumerateArray())
        {
            if (req.GetProperty("requirement").GetString()!.Contains("Tile Matrix Sets"))
            {
                hasTileMatrixSets = true;
                req.GetProperty("endpoint").GetString().Should().Be("/tileMatrixSets");
            }
        }

        hasTileMatrixSets.Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiTiles_IncludesWebMercatorQuadRequirement()
    {
        // Act
        var result = _plugin.ValidateOgcApiTiles("https://tiles.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("requirements");

        var hasWebMercatorQuad = false;
        foreach (var req in requirements.EnumerateArray())
        {
            if (req.GetProperty("requirement").GetString()!.Contains("WebMercatorQuad"))
            {
                hasWebMercatorQuad = true;
            }
        }

        hasWebMercatorQuad.Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiTiles_IncludesCacheHeadersRequirement()
    {
        // Act
        var result = _plugin.ValidateOgcApiTiles("https://tiles.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("requirements");

        var hasCacheHeaders = false;
        foreach (var req in requirements.EnumerateArray())
        {
            if (req.GetProperty("requirement").GetString()!.Contains("Cache headers"))
            {
                hasCacheHeaders = true;
                req.GetProperty("check").GetString().Should().Contain("Cache-Control");
                req.GetProperty("check").GetString().Should().Contain("ETag");
            }
        }

        hasCacheHeaders.Should().BeTrue();
    }

    [Fact]
    public void ValidateOgcApiTiles_IncludesMVTValidation()
    {
        // Act
        var result = _plugin.ValidateOgcApiTiles("https://tiles.example.com");

        // Assert
        var json = JsonDocument.Parse(result);
        var mvtValidation = json.RootElement.GetProperty("mvtValidation");

        mvtValidation.GetProperty("format").GetString().Should().Contain("Mapbox Vector Tile");

        var checks = mvtValidation.GetProperty("checks");
        checks.GetArrayLength().Should().BeGreaterThan(0);

        var checkList = checks.EnumerateArray().Select(c => c.GetString()).ToList();
        checkList.Should().Contain(c => c!.Contains("Protocol Buffer"));
    }

    #endregion

    #region CheckStacCompliance Tests

    [Fact]
    public void CheckStacCompliance_ReturnsCompleteValidation()
    {
        // Arrange
        var catalogData = @"{""stac_version"":""1.0.0"",""type"":""Catalog""}";

        // Act
        var result = _plugin.CheckStacCompliance(catalogData);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.TryGetProperty("stacVersion", out _).Should().BeTrue();
        root.TryGetProperty("validationTools", out _).Should().BeTrue();
        root.TryGetProperty("requiredFields", out _).Should().BeTrue();
        root.TryGetProperty("extensions", out _).Should().BeTrue();
    }

    [Fact]
    public void CheckStacCompliance_IncludesValidationTools()
    {
        // Act
        var result = _plugin.CheckStacCompliance();

        // Assert
        var json = JsonDocument.Parse(result);
        var validationTools = json.RootElement.GetProperty("validationTools");

        validationTools.GetArrayLength().Should().BeGreaterThan(0);

        var toolNames = validationTools.EnumerateArray()
            .Select(t => t.GetProperty("tool").GetString())
            .ToList();

        toolNames.Should().Contain("stac-validator");
        toolNames.Should().Contain("pystac");
    }

    [Fact]
    public void CheckStacCompliance_ValidationToolsHaveCommands()
    {
        // Act
        var result = _plugin.CheckStacCompliance();

        // Assert
        var json = JsonDocument.Parse(result);
        var validationTools = json.RootElement.GetProperty("validationTools");

        foreach (var tool in validationTools.EnumerateArray())
        {
            tool.TryGetProperty("tool", out _).Should().BeTrue();
            tool.TryGetProperty("command", out _).Should().BeTrue();
            tool.TryGetProperty("install", out _).Should().BeTrue();
        }
    }

    [Fact]
    public void CheckStacCompliance_IncludesRequiredFields()
    {
        // Act
        var result = _plugin.CheckStacCompliance();

        // Assert
        var json = JsonDocument.Parse(result);
        var requiredFields = json.RootElement.GetProperty("requiredFields");

        requiredFields.GetArrayLength().Should().BeGreaterThan(0);

        var fieldNames = requiredFields.EnumerateArray()
            .Select(f => f.GetProperty("field").GetString())
            .ToList();

        fieldNames.Should().Contain("stac_version");
        fieldNames.Should().Contain("type");
        fieldNames.Should().Contain("id");
        fieldNames.Should().Contain("description");
        fieldNames.Should().Contain("links");
    }

    [Fact]
    public void CheckStacCompliance_RequiredFieldsHaveTypes()
    {
        // Act
        var result = _plugin.CheckStacCompliance();

        // Assert
        var json = JsonDocument.Parse(result);
        var requiredFields = json.RootElement.GetProperty("requiredFields");

        foreach (var field in requiredFields.EnumerateArray())
        {
            field.GetProperty("field").GetString().Should().NotBeNullOrEmpty();
            field.GetProperty("type").GetString().Should().NotBeNullOrEmpty();
            field.GetProperty("example").GetString().Should().NotBeNullOrEmpty();
            field.TryGetProperty("required", out _).Should().BeTrue();
        }
    }

    [Fact]
    public void CheckStacCompliance_IncludesStacExtensions()
    {
        // Act
        var result = _plugin.CheckStacCompliance();

        // Assert
        var json = JsonDocument.Parse(result);
        var extensions = json.RootElement.GetProperty("extensions");

        extensions.GetArrayLength().Should().BeGreaterThan(0);

        var extensionList = extensions.EnumerateArray().Select(e => e.GetString()).ToList();
        extensionList.Should().Contain(e => e!.Contains("projection"));
        extensionList.Should().Contain(e => e!.Contains("eo"));
    }

    [Fact]
    public void CheckStacCompliance_DefaultParameter_ReturnsValidJson()
    {
        // Act
        var result = _plugin.CheckStacCompliance();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    #endregion

    #region ValidateGeoJSON Tests

    [Fact]
    public void ValidateGeoJSON_ReturnsCompleteValidation()
    {
        // Arrange
        var geoJson = @"{""type"":""Feature"",""geometry"":{""type"":""Point"",""coordinates"":[0,0]}}";

        // Act
        var result = _plugin.ValidateGeoJSON(geoJson);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.TryGetProperty("rfc7946Requirements", out _).Should().BeTrue();
        root.TryGetProperty("validationTools", out _).Should().BeTrue();
        root.TryGetProperty("commonIssues", out _).Should().BeTrue();
    }

    [Fact]
    public void ValidateGeoJSON_IncludesRFC7946Requirements()
    {
        // Act
        var result = _plugin.ValidateGeoJSON("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("rfc7946Requirements");

        requirements.GetArrayLength().Should().BeGreaterThan(0);

        foreach (var req in requirements.EnumerateArray())
        {
            req.TryGetProperty("rule", out _).Should().BeTrue();
            req.TryGetProperty("requirement", out _).Should().BeTrue();
            req.TryGetProperty("common_mistake", out _).Should().BeTrue();
        }
    }

    [Fact]
    public void ValidateGeoJSON_SpecifiesCoordinateOrder()
    {
        // Act
        var result = _plugin.ValidateGeoJSON("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("rfc7946Requirements");

        var hasCoordinateOrder = false;
        foreach (var req in requirements.EnumerateArray())
        {
            if (req.GetProperty("rule").GetString() == "Coordinate order")
            {
                hasCoordinateOrder = true;
                req.GetProperty("requirement").GetString().Should().Contain("Longitude, Latitude");
                req.GetProperty("common_mistake").GetString().Should().Contain("Reversed");
            }
        }

        hasCoordinateOrder.Should().BeTrue();
    }

    [Fact]
    public void ValidateGeoJSON_SpecifiesCRS()
    {
        // Act
        var result = _plugin.ValidateGeoJSON("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("rfc7946Requirements");

        var hasCRS = false;
        foreach (var req in requirements.EnumerateArray())
        {
            if (req.GetProperty("rule").GetString() == "CRS")
            {
                hasCRS = true;
                req.GetProperty("requirement").GetString().Should().Contain("WGS84");
                req.GetProperty("requirement").GetString().Should().Contain("EPSG:4326");
            }
        }

        hasCRS.Should().BeTrue();
    }

    [Fact]
    public void ValidateGeoJSON_SpecifiesWindingOrder()
    {
        // Act
        var result = _plugin.ValidateGeoJSON("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("rfc7946Requirements");

        var hasWindingOrder = false;
        foreach (var req in requirements.EnumerateArray())
        {
            if (req.GetProperty("rule").GetString() == "Winding order")
            {
                hasWindingOrder = true;
                req.GetProperty("requirement").GetString().Should().Contain("Right-hand rule");
            }
        }

        hasWindingOrder.Should().BeTrue();
    }

    [Fact]
    public void ValidateGeoJSON_IncludesValidationTools()
    {
        // Act
        var result = _plugin.ValidateGeoJSON("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var validationTools = json.RootElement.GetProperty("validationTools");

        validationTools.GetArrayLength().Should().BeGreaterThan(0);

        var toolNames = validationTools.EnumerateArray()
            .Select(t => t.GetProperty("tool").GetString())
            .ToList();

        toolNames.Should().Contain("geojsonhint");
        toolNames.Should().Contain("geojson.io");
        toolNames.Should().Contain("GDAL");
    }

    [Fact]
    public void ValidateGeoJSON_IncludesCommonIssues()
    {
        // Act
        var result = _plugin.ValidateGeoJSON("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var commonIssues = json.RootElement.GetProperty("commonIssues");

        commonIssues.GetArrayLength().Should().BeGreaterThan(0);

        foreach (var issue in commonIssues.EnumerateArray())
        {
            issue.TryGetProperty("issue", out _).Should().BeTrue();
            issue.TryGetProperty("fix", out _).Should().BeTrue();
            issue.GetProperty("issue").GetString().Should().NotBeNullOrEmpty();
            issue.GetProperty("fix").GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ValidateGeoJSON_IncludesSelfIntersectingPolygonsIssue()
    {
        // Act
        var result = _plugin.ValidateGeoJSON("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var commonIssues = json.RootElement.GetProperty("commonIssues");

        var hasSelfIntersecting = false;
        foreach (var issue in commonIssues.EnumerateArray())
        {
            if (issue.GetProperty("issue").GetString()!.Contains("Self-intersecting"))
            {
                hasSelfIntersecting = true;
            }
        }

        hasSelfIntersecting.Should().BeTrue();
    }

    #endregion

    #region AuditSecurityCompliance Tests

    [Fact]
    public void AuditSecurityCompliance_ReturnsCompleteAudit()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.TryGetProperty("securityChecklist", out _).Should().BeTrue();
        root.TryGetProperty("owaspTop10", out _).Should().BeTrue();
        root.TryGetProperty("automatedScans", out _).Should().BeTrue();
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesSecurityCategories()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var securityChecklist = json.RootElement.GetProperty("securityChecklist");

        securityChecklist.GetArrayLength().Should().BeGreaterThan(0);

        var categories = securityChecklist.EnumerateArray()
            .Select(c => c.GetProperty("category").GetString())
            .ToList();

        categories.Should().Contain("Authentication");
        categories.Should().Contain("Authorization");
        categories.Should().Contain("Data Protection");
        categories.Should().Contain("Network Security");
        categories.Should().Contain("Compliance");
    }

    [Fact]
    public void AuditSecurityCompliance_AuthenticationCategoryHasChecks()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var securityChecklist = json.RootElement.GetProperty("securityChecklist");

        foreach (var category in securityChecklist.EnumerateArray())
        {
            if (category.GetProperty("category").GetString() == "Authentication")
            {
                var checks = category.GetProperty("checks");
                checks.GetArrayLength().Should().BeGreaterThan(0);

                var checkList = checks.EnumerateArray().Select(c => c.GetString()).ToList();
                checkList.Should().Contain(c => c!.Contains("Tokens expire"));
                checkList.Should().Contain(c => c!.Contains("password policy"));
            }
        }
    }

    [Fact]
    public void AuditSecurityCompliance_AuthorizationCategoryHasRBAC()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var securityChecklist = json.RootElement.GetProperty("securityChecklist");

        foreach (var category in securityChecklist.EnumerateArray())
        {
            if (category.GetProperty("category").GetString() == "Authorization")
            {
                var checks = category.GetProperty("checks");
                var checkList = checks.EnumerateArray().Select(c => c.GetString()).ToList();
                checkList.Should().Contain(c => c!.Contains("RBAC"));
                checkList.Should().Contain(c => c!.Contains("least privilege"));
            }
        }
    }

    [Fact]
    public void AuditSecurityCompliance_DataProtectionIncludesEncryption()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var securityChecklist = json.RootElement.GetProperty("securityChecklist");

        foreach (var category in securityChecklist.EnumerateArray())
        {
            if (category.GetProperty("category").GetString() == "Data Protection")
            {
                var checks = category.GetProperty("checks");
                var checkList = checks.EnumerateArray().Select(c => c.GetString()).ToList();
                checkList.Should().Contain(c => c!.Contains("HTTPS/TLS"));
                checkList.Should().Contain(c => c!.Contains("encrypted"));
            }
        }
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesOwaspTop10()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var owaspTop10 = json.RootElement.GetProperty("owaspTop10");

        owaspTop10.GetArrayLength().Should().BeGreaterThan(0);

        foreach (var owasp in owaspTop10.EnumerateArray())
        {
            owasp.TryGetProperty("risk", out _).Should().BeTrue();
            owasp.TryGetProperty("mitigation", out _).Should().BeTrue();
            owasp.GetProperty("risk").GetString().Should().Contain("A0");
            owasp.GetProperty("risk").GetString().Should().Contain("2021");
        }
    }

    [Fact]
    public void AuditSecurityCompliance_CoversBrokenAccessControl()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var owaspTop10 = json.RootElement.GetProperty("owaspTop10");

        var hasBrokenAccessControl = false;
        foreach (var owasp in owaspTop10.EnumerateArray())
        {
            if (owasp.GetProperty("risk").GetString()!.Contains("Broken Access Control"))
            {
                hasBrokenAccessControl = true;
                owasp.GetProperty("mitigation").GetString().Should().Contain("RBAC");
            }
        }

        hasBrokenAccessControl.Should().BeTrue();
    }

    [Fact]
    public void AuditSecurityCompliance_CoversCryptographicFailures()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var owaspTop10 = json.RootElement.GetProperty("owaspTop10");

        var hasCryptoFailures = false;
        foreach (var owasp in owaspTop10.EnumerateArray())
        {
            if (owasp.GetProperty("risk").GetString()!.Contains("Cryptographic Failures"))
            {
                hasCryptoFailures = true;
                owasp.GetProperty("mitigation").GetString().Should().Contain("TLS");
            }
        }

        hasCryptoFailures.Should().BeTrue();
    }

    [Fact]
    public void AuditSecurityCompliance_CoversInjection()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var owaspTop10 = json.RootElement.GetProperty("owaspTop10");

        var hasInjection = false;
        foreach (var owasp in owaspTop10.EnumerateArray())
        {
            if (owasp.GetProperty("risk").GetString()!.Contains("Injection"))
            {
                hasInjection = true;
                owasp.GetProperty("mitigation").GetString().Should().Contain("Parameterized queries");
            }
        }

        hasInjection.Should().BeTrue();
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesAutomatedScanTools()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var automatedScans = json.RootElement.GetProperty("automatedScans");

        automatedScans.GetArrayLength().Should().BeGreaterThan(0);

        var toolNames = automatedScans.EnumerateArray()
            .Select(s => s.GetProperty("tool").GetString())
            .ToList();

        toolNames.Should().Contain("OWASP ZAP");
        toolNames.Should().Contain("SonarQube");
        toolNames.Should().Contain("Snyk");
        toolNames.Should().Contain("Trivy");
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesDAST()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var automatedScans = json.RootElement.GetProperty("automatedScans");

        var hasDAST = false;
        foreach (var scan in automatedScans.EnumerateArray())
        {
            if (scan.GetProperty("usage").GetString()!.Contains("DAST"))
            {
                hasDAST = true;
            }
        }

        hasDAST.Should().BeTrue("DAST (Dynamic Application Security Testing) should be included");
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesSAST()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var automatedScans = json.RootElement.GetProperty("automatedScans");

        var hasSAST = false;
        foreach (var scan in automatedScans.EnumerateArray())
        {
            if (scan.GetProperty("usage").GetString()!.Contains("SAST"))
            {
                hasSAST = true;
            }
        }

        hasSAST.Should().BeTrue("SAST (Static Application Security Testing) should be included");
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesDependencyScanning()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var automatedScans = json.RootElement.GetProperty("automatedScans");

        var hasDependencyScanning = false;
        foreach (var scan in automatedScans.EnumerateArray())
        {
            if (scan.GetProperty("usage").GetString()!.Contains("vulnerability"))
            {
                hasDependencyScanning = true;
            }
        }

        hasDependencyScanning.Should().BeTrue();
    }

    [Fact]
    public void AuditSecurityCompliance_JsonIsIndented()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        result.Should().Contain("\n");
        result.Should().Contain("  ");
    }

    [Fact]
    public void AuditSecurityCompliance_DefaultParameter_ReturnsValidJson()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    #endregion

    #region Integration and Edge Case Tests

    [Fact]
    public void AllMethods_ReturnValidJson()
    {
        // Act & Assert
        var methods = new Func<string>[]
        {
            () => _plugin.ValidateOgcApiFeatures("https://api.example.com"),
            () => _plugin.ValidateOgcApiTiles("https://tiles.example.com"),
            () => _plugin.CheckStacCompliance(),
            () => _plugin.ValidateGeoJSON("{}"),
            () => _plugin.AuditSecurityCompliance()
        };

        foreach (var method in methods)
        {
            var result = method();
            result.Should().NotBeNullOrEmpty();
            var action = () => JsonDocument.Parse(result);
            action.Should().NotThrow();
        }
    }

    [Fact]
    public void CompliancePlugin_IsSealed()
    {
        // Assert
        typeof(CompliancePlugin).IsSealed.Should().BeTrue(
            "Compliance plugins should be sealed to prevent tampering");
    }

    [Fact]
    public void CompliancePlugin_HasKernelFunctionAttributes()
    {
        // Assert
        var methods = typeof(CompliancePlugin).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var publicMethods = methods.Where(m =>
            !m.IsSpecialName &&
            m.DeclaringType == typeof(CompliancePlugin)).ToList();

        publicMethods.Count.Should().BeGreaterThan(0);

        foreach (var method in publicMethods)
        {
            var hasKernelFunction = method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "KernelFunctionAttribute");

            hasKernelFunction.Should().BeTrue(
                $"Method {method.Name} should have KernelFunction attribute");
        }
    }

    [Theory]
    [InlineData("https://api.example.com")]
    [InlineData("http://localhost:5000")]
    [InlineData("https://example.com/api/v1")]
    public void ValidateOgcApiFeatures_HandlesVariousUrls(string url)
    {
        // Act
        var result = _plugin.ValidateOgcApiFeatures(url);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("endpointUrl").GetString().Should().Be(url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData(@"{""stac_version"":""1.0.0""}")]
    public void CheckStacCompliance_HandlesVariousInputs(string input)
    {
        // Act
        var result = _plugin.CheckStacCompliance(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData(@"{""type"":""Feature""}")]
    public void ValidateGeoJSON_HandlesVariousInputs(string input)
    {
        // Act
        var result = _plugin.ValidateGeoJSON(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData(@"{""config"":""test""}")]
    public void AuditSecurityCompliance_HandlesVariousInputs(string input)
    {
        // Act
        var result = _plugin.AuditSecurityCompliance(input);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    [Fact]
    public void AllJsonOutputs_AreIndented()
    {
        // Act
        var results = new[]
        {
            _plugin.ValidateOgcApiFeatures("https://api.example.com"),
            _plugin.ValidateOgcApiTiles("https://tiles.example.com"),
            _plugin.CheckStacCompliance(),
            _plugin.ValidateGeoJSON("{}"),
            _plugin.AuditSecurityCompliance()
        };

        // Assert
        foreach (var result in results)
        {
            result.Should().Contain("\n", "JSON should be indented");
            result.Should().Contain("  ", "JSON should have proper indentation");
        }
    }

    #endregion
}
