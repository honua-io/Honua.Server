using System;
using System.Text.Json;
using FluentAssertions;
using Honua.Cli.AI.Services.Plugins;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Plugins;

/// <summary>
/// Comprehensive tests for SecurityPlugin - critical security operations plugin.
/// Tests credential management, authentication strategies, and security compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Priority", "Critical")]
[Trait("Plugin", "Security")]
public class SecurityPluginTests
{
    private readonly SecurityPlugin _plugin;

    public SecurityPluginTests()
    {
        _plugin = new SecurityPlugin();
    }

    #region RecommendCredentialStrategy Tests

    [Fact]
    public void RecommendCredentialStrategy_ProductionDatabase_ReturnsSecureStrategy()
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy("production", "database");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("environment").GetString().Should().Be("production");
        root.GetProperty("credentialType").GetString().Should().Be("database");
        root.GetProperty("isProduction").GetBoolean().Should().BeTrue();

        var strategies = root.GetProperty("strategies");
        strategies.GetArrayLength().Should().BeGreaterThan(0);

        // Verify production-specific recommendations
        var storage = strategies[0];
        storage.GetProperty("category").GetString().Should().Be("Storage");
        storage.GetProperty("priority").GetString().Should().Be("Critical");
        storage.GetProperty("recommendation").GetString().Should().Contain("encrypted");
        storage.GetProperty("recommendation").GetString().Should().Contain("hardware-backed");
    }

    [Fact]
    public void RecommendCredentialStrategy_DevelopmentDatabase_ReturnsLessStrictStrategy()
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy("development", "database");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("environment").GetString().Should().Be("development");
        root.GetProperty("isProduction").GetBoolean().Should().BeFalse();

        var strategies = root.GetProperty("strategies");
        var storage = strategies[0];
        storage.GetProperty("priority").GetString().Should().Be("High");
        storage.GetProperty("recommendation").GetString().Should().Contain("Honua CLI");
    }

    [Fact]
    public void RecommendCredentialStrategy_ApiKey_ReturnsApiKeySpecificGuidance()
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy("production", "api-key");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("credentialType").GetString().Should().Be("api-key");

        var strategies = root.GetProperty("strategies");
        var accessScope = strategies[2];
        accessScope.GetProperty("category").GetString().Should().Be("Access Scope");
        accessScope.GetProperty("recommendation").GetString().Should().Contain("scoped API keys");
    }

    [Fact]
    public void RecommendCredentialStrategy_OAuth_ReturnsOAuthSpecificGuidance()
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy("production", "oauth");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("credentialType").GetString().Should().Be("oauth");

        var strategies = root.GetProperty("strategies");
        var accessScope = strategies[2];
        accessScope.GetProperty("recommendation").GetString().Should().Contain("OAuth scopes");
    }

    [Theory]
    [InlineData("PRODUCTION")]
    [InlineData("Production")]
    [InlineData("production")]
    public void RecommendCredentialStrategy_CaseInsensitiveEnvironment_WorksCorrectly(string environment)
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy(environment, "database");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("isProduction").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void RecommendCredentialStrategy_ProductionEnvironment_HasRotationPolicy()
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy("production", "database");

        // Assert
        var json = JsonDocument.Parse(result);
        var strategies = json.RootElement.GetProperty("strategies");

        var rotation = strategies[1];
        rotation.GetProperty("category").GetString().Should().Be("Rotation");
        rotation.GetProperty("priority").GetString().Should().Be("High");
        rotation.GetProperty("recommendation").GetString().Should().Contain("90 days");
    }

    [Fact]
    public void RecommendCredentialStrategy_ProductionEnvironment_HasShortLivedTokens()
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy("production", "database");

        // Assert
        var json = JsonDocument.Parse(result);
        var strategies = json.RootElement.GetProperty("strategies");

        var tokenDuration = strategies[3];
        tokenDuration.GetProperty("category").GetString().Should().Be("Token Duration");
        tokenDuration.GetProperty("recommendation").GetString().Should().Contain("1 hour");
    }

    [Fact]
    public void RecommendCredentialStrategy_DefaultParameters_ReturnsValidJson()
    {
        // Act
        var result = _plugin.RecommendCredentialStrategy();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    #endregion

    #region ValidateCredentialRequirements Tests

    [Fact]
    public void ValidateCredentialRequirements_PostGISLocal_ReturnsCorrectRequirements()
    {
        // Act
        var result = _plugin.ValidateCredentialRequirements("postgis", "local");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("databaseType").GetString().Should().Be("postgis");
        root.GetProperty("deploymentMode").GetString().Should().Be("local");

        var requirements = root.GetProperty("requirements");
        requirements.GetArrayLength().Should().BeGreaterThan(0);

        // Should have PostgreSQL requirement but not SSL certificate
        var hasPostgresReq = false;
        var hasSslReq = false;

        for (int i = 0; i < requirements.GetArrayLength(); i++)
        {
            var req = requirements[i];
            var credential = req.GetProperty("credential").GetString();
            if (credential!.Contains("PostgreSQL Connection String")) hasPostgresReq = true;
            if (credential.Contains("SSL Certificate")) hasSslReq = true;
        }

        hasPostgresReq.Should().BeTrue();
        hasSslReq.Should().BeFalse(); // Local deployment doesn't require SSL
    }

    [Fact]
    public void ValidateCredentialRequirements_PostGISHosted_IncludesSSLRequirements()
    {
        // Act
        var result = _plugin.ValidateCredentialRequirements("postgis", "hosted");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("requirements");

        var hasSslReq = false;
        var hasApiToken = false;
        var hasTlsCert = false;

        for (int i = 0; i < requirements.GetArrayLength(); i++)
        {
            var req = requirements[i];
            var credential = req.GetProperty("credential").GetString();
            if (credential!.Contains("SSL Certificate")) hasSslReq = true;
            if (credential.Contains("API Bearer Token")) hasApiToken = true;
            if (credential.Contains("TLS Certificate")) hasTlsCert = true;
        }

        hasSslReq.Should().BeTrue();
        hasApiToken.Should().BeTrue();
        hasTlsCert.Should().BeTrue();
    }

    [Fact]
    public void ValidateCredentialRequirements_SpatiaLiteLocal_ReturnsFilePathOnly()
    {
        // Act
        var result = _plugin.ValidateCredentialRequirements("spatialite", "local");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("requirements");

        // Should have SpatiaLite path requirement
        var req = requirements[0];
        req.GetProperty("credential").GetString().Should().Contain("SpatiaLite");
        req.GetProperty("storage").GetString().Should().Contain("no credentials needed");
    }

    [Fact]
    public void ValidateCredentialRequirements_SpatiaLiteHosted_IncludesHostingRequirements()
    {
        // Act
        var result = _plugin.ValidateCredentialRequirements("spatialite", "hosted");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("requirements");

        requirements.GetArrayLength().Should().BeGreaterThan(1); // Should have hosting requirements

        var hasApiToken = false;
        for (int i = 0; i < requirements.GetArrayLength(); i++)
        {
            var req = requirements[i];
            var credential = req.GetProperty("credential").GetString();
            if (credential!.Contains("API Bearer Token")) hasApiToken = true;
        }

        hasApiToken.Should().BeTrue();
    }

    [Theory]
    [InlineData("POSTGIS", "LOCAL")]
    [InlineData("PostGIS", "Local")]
    [InlineData("postgis", "local")]
    public void ValidateCredentialRequirements_CaseInsensitive_WorksCorrectly(string dbType, string mode)
    {
        // Act
        var result = _plugin.ValidateCredentialRequirements(dbType, mode);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateCredentialRequirements_DefaultParameters_ReturnsValidJson()
    {
        // Act
        var result = _plugin.ValidateCredentialRequirements();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateCredentialRequirements_AllRequirementsHaveRequiredFields()
    {
        // Act
        var result = _plugin.ValidateCredentialRequirements("postgis", "hosted");

        // Assert
        var json = JsonDocument.Parse(result);
        var requirements = json.RootElement.GetProperty("requirements");

        for (int i = 0; i < requirements.GetArrayLength(); i++)
        {
            var req = requirements[i];
            req.GetProperty("credential").GetString().Should().NotBeNullOrEmpty();
            req.GetProperty("scope").GetString().Should().NotBeNullOrEmpty();
            req.GetProperty("example").GetString().Should().NotBeNullOrEmpty();
            req.GetProperty("storage").GetString().Should().NotBeNullOrEmpty();
            req.TryGetProperty("required", out _).Should().BeTrue();
        }
    }

    #endregion

    #region SuggestAuthConfiguration Tests

    [Fact]
    public void SuggestAuthConfiguration_PublicService_ReturnsOAuthRecommendation()
    {
        // Act
        var result = _plugin.SuggestAuthConfiguration("public", "large");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("visibility").GetString().Should().Be("public");
        root.GetProperty("isPublicFacing").GetBoolean().Should().BeTrue();

        var recommendations = root.GetProperty("recommendations");
        var authMode = recommendations[0];
        authMode.GetProperty("category").GetString().Should().Be("Authentication Mode");
        authMode.GetProperty("recommendation").GetString().Should().Contain("OAuth 2.0");
    }

    [Fact]
    public void SuggestAuthConfiguration_InternalService_ReturnsBuiltInAuth()
    {
        // Act
        var result = _plugin.SuggestAuthConfiguration("internal", "small");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("isPublicFacing").GetBoolean().Should().BeFalse();

        var recommendations = root.GetProperty("recommendations");
        var authMode = recommendations[0];
        authMode.GetProperty("recommendation").GetString().Should().Contain("built-in authentication");
    }

    [Fact]
    public void SuggestAuthConfiguration_LargeUserScale_ReturnsCustomRBAC()
    {
        // Act
        var result = _plugin.SuggestAuthConfiguration("internal", "large");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var recommendations = json.RootElement.GetProperty("recommendations");

        var authzModel = recommendations[1];
        authzModel.GetProperty("category").GetString().Should().Be("Authorization Model");
        authzModel.GetProperty("recommendation").GetString().Should().Contain("custom roles");
    }

    [Fact]
    public void SuggestAuthConfiguration_SmallUserScale_ReturnsSimpleRoles()
    {
        // Act
        var result = _plugin.SuggestAuthConfiguration("internal", "small");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var recommendations = json.RootElement.GetProperty("recommendations");

        var authzModel = recommendations[1];
        authzModel.GetProperty("recommendation").GetString().Should().Contain("simple role-based");
    }

    [Fact]
    public void SuggestAuthConfiguration_PublicService_IncludesRateLimiting()
    {
        // Act
        var result = _plugin.SuggestAuthConfiguration("public", "small");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var recommendations = json.RootElement.GetProperty("recommendations");

        var apiSecurity = recommendations[2];
        apiSecurity.GetProperty("category").GetString().Should().Be("API Security");
        apiSecurity.GetProperty("recommendation").GetString().Should().Contain("rate limiting");
        apiSecurity.GetProperty("recommendation").GetString().Should().Contain("CORS");
    }

    [Fact]
    public void SuggestAuthConfiguration_PublicOrLargeScale_EnablesComprehensiveAuditing()
    {
        // Act
        var publicResult = _plugin.SuggestAuthConfiguration("public", "small");
        var largeResult = _plugin.SuggestAuthConfiguration("internal", "large");

        // Assert - both should have comprehensive audit logs
        var publicJson = JsonDocument.Parse(publicResult);
        var publicRecommendations = publicJson.RootElement.GetProperty("recommendations");
        var publicAudit = publicRecommendations[3];
        publicAudit.GetProperty("recommendation").GetString().Should().Contain("comprehensive audit logs");

        var largeJson = JsonDocument.Parse(largeResult);
        var largeRecommendations = largeJson.RootElement.GetProperty("recommendations");
        var largeAudit = largeRecommendations[3];
        largeAudit.GetProperty("recommendation").GetString().Should().Contain("comprehensive audit logs");
    }

    [Fact]
    public void SuggestAuthConfiguration_DefaultParameters_ReturnsValidJson()
    {
        // Act
        var result = _plugin.SuggestAuthConfiguration();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("PUBLIC", "LARGE")]
    [InlineData("Public", "Large")]
    [InlineData("public", "large")]
    public void SuggestAuthConfiguration_CaseInsensitive_WorksCorrectly(string visibility, string scale)
    {
        // Act
        var result = _plugin.SuggestAuthConfiguration(visibility, scale);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("isPublicFacing").GetBoolean().Should().BeTrue();
    }

    #endregion

    #region GetProductionSecurityChecklist Tests

    [Fact]
    public void GetProductionSecurityChecklist_ReturnsCompleteChecklist()
    {
        // Act
        var result = _plugin.GetProductionSecurityChecklist();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.GetProperty("title").GetString().Should().Be("Honua Production Security Checklist");
        root.GetProperty("categories").GetInt32().Should().BeGreaterThan(0);

        var checklist = root.GetProperty("checklist");
        checklist.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetProductionSecurityChecklist_IncludesCredentialsCategory()
    {
        // Act
        var result = _plugin.GetProductionSecurityChecklist();

        // Assert
        var json = JsonDocument.Parse(result);
        var checklist = json.RootElement.GetProperty("checklist");

        var hasCredentials = false;
        for (int i = 0; i < checklist.GetArrayLength(); i++)
        {
            var category = checklist[i];
            if (category.GetProperty("category").GetString() == "Credentials")
            {
                hasCredentials = true;
                var items = category.GetProperty("items");
                items.GetArrayLength().Should().BeGreaterThan(0);
                items[0].GetString().Should().Contain("✓");
            }
        }

        hasCredentials.Should().BeTrue();
    }

    [Fact]
    public void GetProductionSecurityChecklist_IncludesNetworkSecurityCategory()
    {
        // Act
        var result = _plugin.GetProductionSecurityChecklist();

        // Assert
        var json = JsonDocument.Parse(result);
        var checklist = json.RootElement.GetProperty("checklist");

        var hasNetworkSecurity = false;
        for (int i = 0; i < checklist.GetArrayLength(); i++)
        {
            var category = checklist[i];
            if (category.GetProperty("category").GetString() == "Network Security")
            {
                hasNetworkSecurity = true;
                var items = category.GetProperty("items");
                items.GetArrayLength().Should().BeGreaterThan(0);

                var hasHttps = false;
                for (int j = 0; j < items.GetArrayLength(); j++)
                {
                    if (items[j].GetString()!.Contains("HTTPS/TLS"))
                    {
                        hasHttps = true;
                        break;
                    }
                }
                hasHttps.Should().BeTrue();
            }
        }

        hasNetworkSecurity.Should().BeTrue();
    }

    [Fact]
    public void GetProductionSecurityChecklist_IncludesDatabaseSecurityCategory()
    {
        // Act
        var result = _plugin.GetProductionSecurityChecklist();

        // Assert
        var json = JsonDocument.Parse(result);
        var checklist = json.RootElement.GetProperty("checklist");

        var hasDatabaseSecurity = false;
        for (int i = 0; i < checklist.GetArrayLength(); i++)
        {
            var category = checklist[i];
            if (category.GetProperty("category").GetString() == "Database Security")
            {
                hasDatabaseSecurity = true;
                var items = category.GetProperty("items");
                items.GetArrayLength().Should().BeGreaterThan(0);
            }
        }

        hasDatabaseSecurity.Should().BeTrue();
    }

    [Fact]
    public void GetProductionSecurityChecklist_IncludesAuthenticationCategory()
    {
        // Act
        var result = _plugin.GetProductionSecurityChecklist();

        // Assert
        var json = JsonDocument.Parse(result);
        var checklist = json.RootElement.GetProperty("checklist");

        var hasAuthentication = false;
        for (int i = 0; i < checklist.GetArrayLength(); i++)
        {
            var category = checklist[i];
            if (category.GetProperty("category").GetString() == "Authentication")
            {
                hasAuthentication = true;
                var items = category.GetProperty("items");
                items.GetArrayLength().Should().BeGreaterThan(0);
            }
        }

        hasAuthentication.Should().BeTrue();
    }

    [Fact]
    public void GetProductionSecurityChecklist_IncludesMonitoringCategory()
    {
        // Act
        var result = _plugin.GetProductionSecurityChecklist();

        // Assert
        var json = JsonDocument.Parse(result);
        var checklist = json.RootElement.GetProperty("checklist");

        var hasMonitoring = false;
        for (int i = 0; i < checklist.GetArrayLength(); i++)
        {
            var category = checklist[i];
            if (category.GetProperty("category").GetString() == "Monitoring")
            {
                hasMonitoring = true;
                var items = category.GetProperty("items");
                items.GetArrayLength().Should().BeGreaterThan(0);
            }
        }

        hasMonitoring.Should().BeTrue();
    }

    [Fact]
    public void GetProductionSecurityChecklist_AllItemsAreCheckboxFormat()
    {
        // Act
        var result = _plugin.GetProductionSecurityChecklist();

        // Assert
        var json = JsonDocument.Parse(result);
        var checklist = json.RootElement.GetProperty("checklist");

        for (int i = 0; i < checklist.GetArrayLength(); i++)
        {
            var category = checklist[i];
            var items = category.GetProperty("items");

            for (int j = 0; j < items.GetArrayLength(); j++)
            {
                items[j].GetString().Should().StartWith("✓");
            }
        }
    }

    #endregion

    #region AuditSecurityCompliance Tests

    [Fact]
    public void AuditSecurityCompliance_ReturnsCompleteAuditStructure()
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
    public void AuditSecurityCompliance_IncludesAuthenticationChecks()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var checklist = json.RootElement.GetProperty("securityChecklist");

        var hasAuth = false;
        for (int i = 0; i < checklist.GetArrayLength(); i++)
        {
            var category = checklist[i];
            if (category.GetProperty("category").GetString() == "Authentication")
            {
                hasAuth = true;
                var checks = category.GetProperty("checks");
                checks.GetArrayLength().Should().BeGreaterThan(0);
            }
        }

        hasAuth.Should().BeTrue();
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesOwaspTop10()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var owasp = json.RootElement.GetProperty("owaspTop10");

        owasp.GetArrayLength().Should().BeGreaterThan(0);

        // Verify structure of OWASP entries
        for (int i = 0; i < owasp.GetArrayLength(); i++)
        {
            var entry = owasp[i];
            entry.GetProperty("risk").GetString().Should().NotBeNullOrEmpty();
            entry.GetProperty("mitigation").GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void AuditSecurityCompliance_CoversOwaspBrokenAccessControl()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var owasp = json.RootElement.GetProperty("owaspTop10");

        var hasBrokenAccessControl = false;
        for (int i = 0; i < owasp.GetArrayLength(); i++)
        {
            var entry = owasp[i];
            if (entry.GetProperty("risk").GetString()!.Contains("Broken Access Control"))
            {
                hasBrokenAccessControl = true;
                entry.GetProperty("mitigation").GetString().Should().Contain("RBAC");
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
        var owasp = json.RootElement.GetProperty("owaspTop10");

        var hasCryptoFailures = false;
        for (int i = 0; i < owasp.GetArrayLength(); i++)
        {
            var entry = owasp[i];
            if (entry.GetProperty("risk").GetString()!.Contains("Cryptographic Failures"))
            {
                hasCryptoFailures = true;
                entry.GetProperty("mitigation").GetString().Should().Contain("TLS");
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
        var owasp = json.RootElement.GetProperty("owaspTop10");

        var hasInjection = false;
        for (int i = 0; i < owasp.GetArrayLength(); i++)
        {
            var entry = owasp[i];
            if (entry.GetProperty("risk").GetString()!.Contains("Injection"))
            {
                hasInjection = true;
                entry.GetProperty("mitigation").GetString().Should().Contain("Parameterized queries");
            }
        }

        hasInjection.Should().BeTrue();
    }

    [Fact]
    public void AuditSecurityCompliance_IncludesAutomatedScanningTools()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var scans = json.RootElement.GetProperty("automatedScans");

        scans.GetArrayLength().Should().BeGreaterThan(0);

        // Verify structure
        for (int i = 0; i < scans.GetArrayLength(); i++)
        {
            var scan = scans[i];
            scan.GetProperty("tool").GetString().Should().NotBeNullOrEmpty();
            scan.GetProperty("usage").GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void AuditSecurityCompliance_RecommendsSASTTool()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var scans = json.RootElement.GetProperty("automatedScans");

        var hasSAST = false;
        for (int i = 0; i < scans.GetArrayLength(); i++)
        {
            var scan = scans[i];
            if (scan.GetProperty("usage").GetString()!.Contains("SAST"))
            {
                hasSAST = true;
                break;
            }
        }

        hasSAST.Should().BeTrue("SAST (Static Application Security Testing) should be recommended");
    }

    [Fact]
    public void AuditSecurityCompliance_RecommendsDependencyScanning()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var scans = json.RootElement.GetProperty("automatedScans");

        var hasDependencyScanning = false;
        for (int i = 0; i < scans.GetArrayLength(); i++)
        {
            var scan = scans[i];
            if (scan.GetProperty("usage").GetString()!.Contains("Dependency") ||
                scan.GetProperty("usage").GetString()!.Contains("vulnerability"))
            {
                hasDependencyScanning = true;
                break;
            }
        }

        hasDependencyScanning.Should().BeTrue("Dependency vulnerability scanning should be recommended");
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

    [Fact]
    public void AuditSecurityCompliance_EmptyConfig_ReturnsValidJson()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("");

        // Assert - Should still work with empty string
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    [Fact]
    public void AuditSecurityCompliance_JsonIsIndented()
    {
        // Act
        var result = _plugin.AuditSecurityCompliance("{}");

        // Assert
        result.Should().Contain("\n"); // Should have newlines (indented)
        result.Should().Contain("  "); // Should have indentation
    }

    #endregion

    #region Integration and Edge Case Tests

    [Fact]
    public void AllMethods_ReturnValidJson()
    {
        // Act & Assert
        var methods = new Func<string>[]
        {
            () => _plugin.RecommendCredentialStrategy(),
            () => _plugin.ValidateCredentialRequirements(),
            () => _plugin.SuggestAuthConfiguration(),
            () => _plugin.GetProductionSecurityChecklist(),
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
    public void SecurityPlugin_IsSealed()
    {
        // Assert
        typeof(SecurityPlugin).IsSealed.Should().BeTrue("Security plugins should be sealed to prevent tampering");
    }

    [Fact]
    public void SecurityPlugin_HasKernelFunctionAttributes()
    {
        // Assert
        var methods = typeof(SecurityPlugin).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var publicMethods = methods.Where(m =>
            !m.IsSpecialName &&
            m.DeclaringType == typeof(SecurityPlugin)).ToList();

        publicMethods.Count.Should().BeGreaterThan(0);

        foreach (var method in publicMethods)
        {
            var hasKernelFunction = method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "KernelFunctionAttribute");

            hasKernelFunction.Should().BeTrue(
                $"Method {method.Name} should have KernelFunction attribute");
        }
    }

    #endregion
}
