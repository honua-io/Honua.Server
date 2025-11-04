## SAML Single Sign-On (SSO)

Enterprise-grade SAML 2.0 authentication for secure single sign-on with popular identity providers.

### Overview

The SAML SSO feature enables enterprise customers to integrate Honua Server with their existing identity infrastructure, including:

- **Azure Active Directory (Azure AD / Entra ID)**
- **Okta**
- **OneLogin**
- **Auth0**
- **Google Workspace**
- **ADFS**
- **Ping Identity**
- **Any SAML 2.0 compliant IdP**

**Key Features:**
- SAML 2.0 Service Provider (SP) implementation
- Per-tenant IdP configuration
- Just-in-Time (JIT) user provisioning
- Attribute mapping customization
- Metadata exchange (SP metadata + IdP metadata import)
- Both HTTP-POST and HTTP-Redirect bindings
- Session management and replay attack prevention
- Automatic session cleanup
- Support for IdP-initiated and SP-initiated SSO
- Role-based access control (RBAC) integration

### Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                       User Browser                                │
└────────────┬─────────────────────────────────────┬───────────────┘
             │                                     │
             │ 1. Access protected                 │ 5. POST SAML
             │    resource                         │    assertion
             │                                     │
             ▼                                     │
┌────────────────────────────────────────────┐    │
│        Honua Server (Service Provider)      │    │
│                                             │    │
│  ┌──────────────────────────────────────┐  │    │
│  │  SamlService                         │  │    │
│  │  • Generate AuthnRequest             │  │    │
│  │  • Validate assertions               │  │    │
│  │  • Generate SP metadata              │  │    │
│  └───────┬──────────────────────────────┘  │    │
│          │                                  │    │
│          ▼                                  │    │
│  ┌──────────────────────────────────────┐  │    │
│  │  SamlSessionStore                    │  │    │
│  │  • Track authentication sessions     │  │    │
│  │  • Prevent replay attacks            │  │    │
│  └──────────────────────────────────────┘  │    │
│          │                                  │    │
│          ▼                                  │    │
│  ┌──────────────────────────────────────┐  │    │
│  │  SamlUserProvisioningService         │  │    │
│  │  • JIT user creation                 │  │    │
│  │  • User-IdP mapping                  │  │    │
│  │  • Attribute mapping                 │  │    │
│  └──────────────────────────────────────┘  │    │
│                                             │    │
│  Endpoints:                                 │    │
│  • /auth/saml/login (SSO initiation)        │    │
│  • /auth/saml/acs (Assertion Consumer)      │◄───┘
│  • /auth/saml/metadata (SP metadata)        │
└────────────┬────────────────────────────────┘
             │
             │ 2. Redirect to IdP
             │    with AuthnRequest
             │
             ▼
┌────────────────────────────────────────────┐
│    Identity Provider (IdP)                  │
│    • Azure AD / Okta / OneLogin / etc.     │
│                                             │
│    3. User authenticates                    │
│    4. Generate SAML assertion               │
└─────────────────────────────────────────────┘
```

### Database Schema

The SAML SSO feature uses three main tables:

**`saml_identity_providers`**
- Stores per-tenant IdP configurations
- Includes entity ID, SSO URL, signing certificate
- Attribute mappings and JIT provisioning settings

**`saml_sessions`**
- Temporary storage for authentication sessions
- Prevents SAML replay attacks
- Automatically cleaned up after expiration

**`saml_user_mappings`**
- Maps SAML NameIDs to internal user accounts
- Tracks session index for Single Logout
- Records last login timestamps

### Configuration

#### 1. Enable SAML SSO

Add to `appsettings.json`:

```json
{
  "Saml": {
    "Enabled": true,
    "ServiceProvider": {
      "EntityId": "https://your-domain.com",
      "BaseUrl": "https://your-domain.com",
      "AssertionConsumerServicePath": "/auth/saml/acs",
      "MetadataPath": "/auth/saml/metadata",
      "OrganizationName": "Your Organization",
      "OrganizationDisplayName": "Your Organization",
      "OrganizationUrl": "https://your-domain.com",
      "TechnicalContactEmail": "tech@your-domain.com",
      "SupportContactEmail": "support@your-domain.com",
      "AuthnRequestValidityMinutes": 5
    },
    "SessionTimeoutMinutes": 60,
    "MaximumClockSkewMinutes": 5,
    "EnableDebugLogging": false
  }
}
```

#### 2. Register SAML Services

In `Program.cs`:

```csharp
// Add SAML SSO (Enterprise feature)
builder.Services.AddSamlSso(builder.Configuration);
```

#### 3. Map SAML Endpoints

In `Program.cs`:

```csharp
app.MapSamlEndpoints();
```

### IdP Configuration

#### Azure Active Directory (Entra ID)

**1. Register Application in Azure AD**

```bash
# Azure Portal
1. Navigate to Azure Active Directory > Enterprise Applications
2. Click "New application" > "Create your own application"
3. Name: "Honua Server"
4. Select "Integrate any other application (Non-gallery)"
```

**2. Configure SAML**

```yaml
Basic SAML Configuration:
  Identifier (Entity ID): https://your-domain.com
  Reply URL (ACS): https://your-domain.com/auth/saml/acs
  Sign on URL: https://your-domain.com/auth/saml/login

Attributes & Claims:
  Required claims:
    - emailaddress: user.mail
    - givenname: user.givenname
    - surname: user.surname
    - name: user.displayname

SAML Signing Certificate:
  Download: Certificate (Base64)
```

**3. Import IdP Metadata to Honua**

```bash
# Download Federation Metadata XML from Azure AD
curl https://login.microsoftonline.com/{tenant-id}/federationmetadata/2007-06/federationmetadata.xml \
  -o azure-ad-metadata.xml

# Import via Admin API
POST /api/admin/saml/idp
Content-Type: application/json

{
  "tenantId": "your-tenant-id",
  "name": "Azure AD",
  "metadataXml": "<EntityDescriptor>...</EntityDescriptor>"
}
```

**4. Database Configuration (Alternative)**

```sql
INSERT INTO saml_identity_providers (
  id,
  tenant_id,
  name,
  entity_id,
  single_sign_on_service_url,
  signing_certificate,
  enabled,
  created_at,
  updated_at
) VALUES (
  gen_random_uuid(),
  'your-tenant-id',
  'Azure AD',
  'https://sts.windows.net/{tenant-id}/',
  'https://login.microsoftonline.com/{tenant-id}/saml2',
  '-----BEGIN CERTIFICATE-----
  MIIDPjCCAiqgAwIBAgIQsRiM0jheFZhKk49YD4peDDANBgkqhkiG9w0BAQUFADA7
  ...
  -----END CERTIFICATE-----',
  true,
  NOW(),
  NOW()
);
```

#### Okta

**1. Create SAML App in Okta**

```bash
# Okta Admin Console
1. Applications > Create App Integration
2. Select "SAML 2.0"
3. App name: "Honua Server"
```

**2. SAML Settings**

```yaml
General:
  Single sign on URL: https://your-domain.com/auth/saml/acs
  Audience URI (SP Entity ID): https://your-domain.com
  Default RelayState: (leave empty)
  Name ID format: EmailAddress
  Application username: Email

Attribute Statements:
  email: user.email
  firstName: user.firstName
  lastName: user.lastName
  displayName: user.displayName

Group Attribute Statements (optional):
  groups: Matches regex: .*
```

**3. Get IdP Metadata**

```bash
# In Okta app, go to "Sign On" tab
# Right-click "Identity Provider metadata" > Copy link address

# Import to Honua
curl https://your-okta-domain.okta.com/app/{app-id}/sso/saml/metadata \
  -o okta-metadata.xml
```

#### Google Workspace

**1. Add SAML App in Google Admin**

```bash
# Google Admin Console
1. Apps > Web and mobile apps
2. Add app > Add custom SAML app
3. App name: "Honua Server"
```

**2. Google IdP Information**

```
SSO URL: Copy this value
Entity ID: Copy this value
Certificate: Download
```

**3. Service Provider Details**

```yaml
ACS URL: https://your-domain.com/auth/saml/acs
Entity ID: https://your-domain.com
Start URL: https://your-domain.com/auth/saml/login
Name ID format: EMAIL
Name ID: Basic Information > Primary email
```

**4. Attribute Mapping**

```
Google Directory attributes → App attributes
Primary email → email
First name → firstName
Last name → lastName
```

### Just-in-Time (JIT) User Provisioning

JIT provisioning automatically creates user accounts on first login via SAML.

**Configuration per IdP:**

```json
{
  "enableJitProvisioning": true,
  "defaultRole": "User",
  "attributeMappings": {
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "email",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname": "firstName",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname": "lastName",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "displayName"
  }
}
```

**Workflow:**

1. User authenticates via IdP
2. SAML assertion validated
3. Check if user exists (by NameID)
4. If new user:
   - Extract attributes from assertion
   - Create user account with `defaultRole`
   - Create SAML mapping entry
5. If existing user:
   - Update last login timestamp
   - Update session index
6. Sign in user with claims

**Disable JIT Provisioning:**

If you prefer manual user provisioning:

```json
{
  "enableJitProvisioning": false
}
```

Users must be pre-created in the database before they can log in via SAML.

### Attribute Mapping

Map SAML attributes to user profile fields:

**Standard SAML Attributes:**

```json
{
  "attributeMappings": {
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "email",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname": "firstName",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname": "lastName",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "displayName",
    "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups": "groups"
  }
}
```

**Custom Attributes:**

```json
{
  "attributeMappings": {
    "department": "department",
    "employeeId": "employeeId",
    "manager": "manager",
    "costCenter": "costCenter"
  }
}
```

### SSO Flows

#### SP-Initiated SSO (Recommended)

User starts at service provider (Honua):

```
1. User visits: https://your-domain.com/auth/saml/login
2. Honua generates AuthnRequest
3. Browser redirected to IdP with SAML request
4. User authenticates at IdP
5. IdP generates SAML assertion
6. Browser POST assertion to /auth/saml/acs
7. Honua validates assertion and provisions user
8. User signed in and redirected to application
```

**Initiate SSO:**

```bash
GET /auth/saml/login?returnUrl=/dashboard
```

#### IdP-Initiated SSO

User starts at identity provider:

```
1. User clicks "Honua Server" in IdP portal
2. IdP generates unsolicited SAML assertion
3. Browser POST assertion to /auth/saml/acs
4. Honua validates assertion (if allowed)
5. User signed in
```

**Enable IdP-Initiated SSO:**

```sql
UPDATE saml_identity_providers
SET allow_unsolicited_authn_response = true
WHERE tenant_id = 'your-tenant-id';
```

**Security Note:** IdP-initiated SSO is less secure. Prefer SP-initiated SSO when possible.

### Security Considerations

**1. Certificate Validation**

Always validate IdP signing certificates:

```json
{
  "wantAssertionsSigned": true,
  "signAuthenticationRequests": true
}
```

**2. Clock Skew**

Allow reasonable clock skew for time validation:

```json
{
  "MaximumClockSkewMinutes": 5
}
```

**3. Replay Attack Prevention**

SAML sessions are stored and marked as consumed after use:

```csharp
// Session automatically expires after configured period
var session = await _sessionStore.GetSessionByRequestIdAsync(requestId);
if (session.Consumed)
{
    throw new SecurityException("SAML replay attack detected");
}
await _sessionStore.ConsumeSessionAsync(requestId);
```

**4. HTTPS Required**

All SAML endpoints must use HTTPS in production:

```json
{
  "ServiceProvider": {
    "BaseUrl": "https://your-domain.com"  // Must be HTTPS
  }
}
```

**5. Audience Validation**

Assertions are validated for correct audience:

```csharp
var audiences = assertion.Conditions.AudienceRestrictions;
if (!audiences.Contains(serviceProvider.EntityId))
{
    throw new SecurityException("Assertion not intended for this SP");
}
```

### Troubleshooting

#### Enable Debug Logging

```json
{
  "Saml": {
    "EnableDebugLogging": true
  },
  "Logging": {
    "LogLevel": {
      "Honua.Server.Enterprise.Authentication": "Debug"
    }
  }
}
```

This logs full SAML requests and responses for debugging.

#### Common Issues

**"SAML session not found or expired"**

- Check `AuthnRequestValidityMinutes` configuration
- Verify system clocks are synchronized (NTP)
- Check `MaximumClockSkewMinutes` setting

**"SAML assertion signature validation failed"**

- Verify signing certificate is correct
- Check certificate format (must be PEM with headers)
- Ensure certificate is not expired
- Verify IdP is using correct signing key

**"Assertion not intended for this service provider"**

- Verify `EntityId` matches in both SP and IdP configuration
- Check AudienceRestriction in SAML assertion

**"NameID not found in assertion"**

- Configure IdP to include NameID in assertion
- Check NameID format matches configuration
- Verify attribute mappings are correct

#### Viewing SAML Assertions

Use browser developer tools to inspect SAML POST:

```bash
# Network tab > Filter by "acs"
# View Form Data:
SAMLResponse: PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZ...

# Decode (Base64):
echo "PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZ..." | base64 -d
```

Or use online SAML decoder: https://www.samltool.com/decode.php

### Monitoring and Metrics

**Track SAML Events:**

```csharp
// Log all SAML authentication attempts
_logger.LogInformation(
    "SAML authentication for user {NameId} in tenant {TenantId}: {Status}",
    nameId, tenantId, status);
```

**Key Metrics to Monitor:**

- SAML authentication attempts (total, success, failure)
- JIT provisioning events (new users created)
- Session validation errors
- Certificate expiration warnings
- Average SSO completion time

**Audit Log Integration:**

All SAML events should be recorded in the audit log:

```csharp
await _auditLog.RecordAsync(new AuditEvent
{
    EventType = "saml.authentication",
    UserId = userId,
    TenantId = tenantId,
    Success = true,
    Metadata = new {
        NameId = nameId,
        IdpName = idpConfig.Name,
        IsNewUser = isNewUser
    }
});
```

### Multi-Tenant Considerations

Each tenant can have its own IdP configuration:

**Per-Tenant IdPs:**

```sql
-- Tenant A uses Azure AD
INSERT INTO saml_identity_providers (tenant_id, name, entity_id, ...)
VALUES ('tenant-a-id', 'Azure AD', 'https://sts.windows.net/...', ...);

-- Tenant B uses Okta
INSERT INTO saml_identity_providers (tenant_id, name, entity_id, ...)
VALUES ('tenant-b-id', 'Okta', 'http://www.okta.com/...', ...);
```

**Tenant Resolution:**

Tenant is determined from:
1. Subdomain (e.g., `tenant-a.honua.io`)
2. Custom domain mapping
3. Relay state parameter

### Testing

**Test SAML Flow:**

```bash
# 1. Initiate SSO
curl -v https://your-domain.com/auth/saml/login

# 2. Get SP metadata
curl https://your-domain.com/auth/saml/metadata

# 3. Test with SAML test tool
# Use https://samltest.id for testing
```

**SAML Validators:**

- https://www.samltool.com/validate_response.php
- https://www.samltool.com/validate_authn_req.php

### Related Documentation

- [EnterpriseReady SSO Guide](https://www.enterpriseready.io/features/single-sign-on/)
- [SAML 2.0 Specification](http://docs.oasis-open.org/security/saml/Post2.0/sstc-saml-tech-overview-2.0.html)
- [Azure AD SAML Protocol](https://docs.microsoft.com/en-us/azure/active-directory/develop/single-sign-on-saml-protocol)
- [Okta SAML Documentation](https://developer.okta.com/docs/concepts/saml/)

### Support

For SAML SSO setup assistance:
- Contact: support@honua.io
- Documentation: https://docs.honua.io/enterprise/saml-sso
- Community: https://community.honua.io
