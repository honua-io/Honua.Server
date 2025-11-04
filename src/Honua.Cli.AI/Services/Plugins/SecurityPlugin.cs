// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for security and credential management.
/// Provides AI with guidance on secure credential handling and access control.
/// </summary>
public sealed class SecurityPlugin
{
    [KernelFunction, Description("Recommends secure credential storage strategies and token management")]
    public string RecommendCredentialStrategy(
        [Description("Environment type: development, staging, or production")] string environment = "development",
        [Description("Credential type: database, api-key, or oauth")] string credentialType = "database")
    {
        var isProd = environment.Equals("production", StringComparison.OrdinalIgnoreCase);

        var strategies = new[]
        {
            new
            {
                category = "Storage",
                priority = isProd ? "Critical" : "High",
                recommendation = isProd
                    ? "Use encrypted secrets manager with hardware-backed encryption (AWS Secrets Manager, Azure Key Vault)"
                    : "Use Honua CLI encrypted file storage (~/.honua/secrets.enc)",
                command = isProd
                    ? "Configure cloud secrets manager integration"
                    : "honua secrets set DATABASE_PASSWORD"
            },
            new
            {
                category = "Rotation",
                priority = isProd ? "High" : "Medium",
                recommendation = isProd
                    ? "Implement automated credential rotation every 90 days"
                    : "Manually rotate credentials quarterly",
                command = "Set rotation policy in secrets manager"
            },
            new
            {
                category = "Access Scope",
                priority = "High",
                recommendation = credentialType switch
                {
                    "database" => "Use read-only credentials for query layers, read-write only for ingestion",
                    "api-key" => "Generate scoped API keys with minimum required permissions",
                    "oauth" => "Request only necessary OAuth scopes",
                    _ => "Apply principle of least privilege"
                },
                command = credentialType == "database"
                    ? "CREATE USER honua_readonly WITH PASSWORD 'xxx'; GRANT SELECT ON ALL TABLES IN SCHEMA public TO honua_readonly;"
                    : "Configure scoped permissions in provider settings"
            },
            new
            {
                category = "Token Duration",
                priority = "Medium",
                recommendation = isProd
                    ? "Use short-lived tokens (1 hour) with refresh mechanism"
                    : "Use medium-lived tokens (10 minutes) for development safety",
                command = "Configure token expiration in ISecretsManager options"
            }
        };

        return JsonSerializer.Serialize(new
        {
            environment,
            credentialType,
            isProduction = isProd,
            strategies
        });
    }

    [KernelFunction, Description("Validates credential requirements for a given deployment scenario")]
    public string ValidateCredentialRequirements(
        [Description("Database type: postgis or spatialite")] string databaseType = "postgis",
        [Description("Deployment mode: local or hosted")] string deploymentMode = "local")
    {
        var isPostGIS = databaseType.Equals("postgis", StringComparison.OrdinalIgnoreCase);
        var isHosted = deploymentMode.Equals("hosted", StringComparison.OrdinalIgnoreCase);

        var requirements = new System.Collections.Generic.List<object>();

        if (isPostGIS)
        {
            requirements.Add(new
            {
                credential = "PostgreSQL Connection String",
                required = true,
                scope = "Database access",
                example = "Host=localhost;Database=honua;Username=honua_user;Password=[from secrets]",
                storage = "honua secrets set POSTGRES_PASSWORD"
            });

            if (isHosted)
            {
                requirements.Add(new
                {
                    credential = "PostgreSQL SSL Certificate",
                    required = true,
                    scope = "Secure database connection",
                    example = "SSL Mode=Require;Root Certificate=/path/to/ca-cert.pem",
                    storage = "Store certificate in secure location, reference path in connection string"
                });
            }
        }
        else
        {
            requirements.Add(new
            {
                credential = "SpatiaLite Database Path",
                required = true,
                scope = "Local database file",
                example = "/var/lib/honua/data.db",
                storage = "File path only, no credentials needed"
            });
        }

        if (isHosted)
        {
            requirements.Add(new
            {
                credential = "API Bearer Token",
                required = true,
                scope = "Control plane authentication",
                example = "Bearer eyJhbGciOiJIUzI1NiIs...",
                storage = "honua secrets set HONUA_API_TOKEN"
            });

            requirements.Add(new
            {
                credential = "TLS Certificate",
                required = true,
                scope = "HTTPS serving",
                example = "Certificate: /etc/letsencrypt/live/domain/fullchain.pem",
                storage = "Managed by certbot or cloud provider"
            });
        }

        return JsonSerializer.Serialize(new
        {
            databaseType,
            deploymentMode,
            requirementCount = requirements.Count,
            requirements
        });
    }

    [KernelFunction, Description("Suggests authentication and authorization configuration for Honua services")]
    public string SuggestAuthConfiguration(
        [Description("Service visibility: public, internal, or private")] string visibility = "internal",
        [Description("Expected user count: small (<10), medium (<100), or large (100+)")] string userScale = "small")
    {
        var isPublic = visibility.Equals("public", StringComparison.OrdinalIgnoreCase);
        var isLarge = userScale.Equals("large", StringComparison.OrdinalIgnoreCase);

        var recommendations = new[]
        {
            new
            {
                category = "Authentication Mode",
                recommendation = isPublic
                    ? "Enable OAuth 2.0 with external identity provider (Auth0, Okta, Azure AD)"
                    : "Use Honua built-in authentication with bcrypt password hashing",
                command = isPublic
                    ? "Configure OAuth provider in appsettings.json"
                    : "honua auth bootstrap --mode Local"
            },
            new
            {
                category = "Authorization Model",
                recommendation = isLarge
                    ? "Implement role-based access control (RBAC) with custom roles"
                    : "Use simple role-based permissions (admin, datapublisher, viewer)",
                command = "honua auth create-user --username user1 --role datapublisher"
            },
            new
            {
                category = "API Security",
                recommendation = isPublic
                    ? "Enable rate limiting (100 req/min per IP), CORS restrictions, and API key rotation"
                    : "Enable API key authentication with liberal rate limits",
                command = isPublic
                    ? "Configure rate limiting middleware in Program.cs"
                    : "Configure bearer token authentication"
            },
            new
            {
                category = "Audit Logging",
                recommendation = isPublic || isLarge
                    ? "Enable comprehensive audit logs for authentication, authorization, and data access"
                    : "Enable basic authentication audit logs",
                command = "Configure logging level in appsettings.json: \"Honua.Authentication\": \"Information\""
            }
        };

        return JsonSerializer.Serialize(new
        {
            visibility,
            userScale,
            isPublicFacing = isPublic,
            recommendations
        });
    }

    [KernelFunction, Description("Provides security checklist for production deployments")]
    public string GetProductionSecurityChecklist()
    {
        var checklist = new[]
        {
            new
            {
                category = "Credentials",
                items = new[]
                {
                    "✓ All passwords stored in encrypted secrets manager",
                    "✓ No credentials in configuration files or environment variables",
                    "✓ Database connection strings use secure password injection",
                    "✓ API keys rotated within last 90 days"
                }
            },
            new
            {
                category = "Network Security",
                items = new[]
                {
                    "✓ HTTPS/TLS enabled with valid certificates",
                    "✓ Database connections use SSL/TLS",
                    "✓ Firewall rules restrict database access to application servers only",
                    "✓ Rate limiting configured for public endpoints"
                }
            },
            new
            {
                category = "Database Security",
                items = new[]
                {
                    "✓ Database users follow least privilege principle",
                    "✓ Read-only users for query-only operations",
                    "✓ Separate credentials for ingestion and serving",
                    "✓ Database audit logging enabled"
                }
            },
            new
            {
                category = "Authentication",
                items = new[]
                {
                    "✓ Strong password policy enforced (12+ chars, complexity requirements)",
                    "✓ Failed login attempt limiting/lockout configured",
                    "✓ Session tokens expire appropriately (1 hour for production)",
                    "✓ OAuth providers configured for external authentication"
                }
            },
            new
            {
                category = "Monitoring",
                items = new[]
                {
                    "✓ Security events logged (failed auth, unauthorized access attempts)",
                    "✓ Audit logs stored in tamper-proof location",
                    "✓ Alerts configured for suspicious activity",
                    "✓ Regular security log review process established"
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            title = "Honua Production Security Checklist",
            categories = checklist.Length,
            checklist
        });
    }

    [KernelFunction, Description("Audits security compliance and best practices")]
    public string AuditSecurityCompliance(
        [Description("Configuration data as JSON")] string configData = "{}")
    {
        return JsonSerializer.Serialize(new
        {
            securityChecklist = new[]
            {
                new { category = "Authentication", checks = new[] { "Tokens expire (max 24h)", "Strong password policy enforced", "MFA enabled for admin users", "OAuth/OIDC properly configured" } },
                new { category = "Authorization", checks = new[] { "Role-based access control (RBAC)", "Principle of least privilege", "Resource-level permissions", "Audit trail for access" } },
                new { category = "Data Protection", checks = new[] { "HTTPS/TLS enabled", "Database connections encrypted", "Secrets in vault (not config files)", "PII data encrypted at rest" } },
                new { category = "Network Security", checks = new[] { "Firewall rules configured", "Database not publicly accessible", "Rate limiting enabled", "DDoS protection active" } },
                new { category = "Compliance", checks = new[] { "GDPR compliance (if applicable)", "Data retention policies", "Security incident response plan", "Regular security audits" } }
            },
            owaspTop10 = new[]
            {
                new { risk = "A01:2021-Broken Access Control", mitigation = "Implement RBAC, validate permissions server-side" },
                new { risk = "A02:2021-Cryptographic Failures", mitigation = "Use TLS, encrypt sensitive data, secure key management" },
                new { risk = "A03:2021-Injection", mitigation = "Parameterized queries, input validation, ORM usage" },
                new { risk = "A05:2021-Security Misconfiguration", mitigation = "Secure defaults, remove unnecessary features, update regularly" },
                new { risk = "A07:2021-Identification and Authentication Failures", mitigation = "Strong password policy, MFA, secure session management" }
            },
            automatedScans = new[]
            {
                new { tool = "OWASP ZAP", usage = "Dynamic application security testing (DAST)" },
                new { tool = "SonarQube", usage = "Static application security testing (SAST)" },
                new { tool = "Snyk", usage = "Dependency vulnerability scanning" },
                new { tool = "Trivy", usage = "Container image vulnerability scanning" }
            }
        }, CliJsonOptions.Indented);
    }
}
