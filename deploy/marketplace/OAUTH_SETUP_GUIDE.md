# OAuth/OIDC Setup Guide for Cloud Marketplace Deployments

## ⚠️ IMPORTANT: OAuth is REQUIRED for Cloud Marketplace Deployments

For security and compliance reasons, **local username/password authentication is DISABLED** in all cloud marketplace deployments of Honua IO. You **MUST** configure OAuth/OIDC authentication using one of the supported identity providers.

## Supported Identity Providers

### Cloud Provider IdPs
- **AWS Cognito** (recommended for AWS deployments)
- **Azure Active Directory / Entra ID** (recommended for Azure deployments)
- **Google Identity Platform** (recommended for GCP deployments)

### Third-Party IdPs
- **Okta**
- **Auth0**
- **OneLogin**
- **Custom OIDC Provider** (any OpenID Connect compatible provider)

---

## Option 1: AWS Cognito (Automatic Setup)

**Best for**: AWS marketplace deployments, simplest option

### Deployment
When deploying from AWS Marketplace, select `cognito` as the OAuth provider:

```yaml
Parameters:
  OAuthProvider: cognito
  # Leave other OAuth parameters empty - they will be auto-created
```

The CloudFormation template will automatically create:
- ✅ Cognito User Pool with secure password policy
- ✅ User Pool Client with OAuth 2.0 configuration
- ✅ User Pool Domain for hosted UI
- ✅ MFA configuration (optional, TOTP-based)
- ✅ Email verification
- ✅ Client secret stored in Secrets Manager

### Post-Deployment Steps

1. **Access Cognito Console**:
   ```bash
   # Get User Pool ID from CloudFormation outputs
   aws cloudformation describe-stacks \
     --stack-name honua-server \
     --query 'Stacks[0].Outputs[?OutputKey==`CognitoUserPoolId`].OutputValue' \
     --output text
   ```

2. **Create Your First User**:
   ```bash
   aws cognito-idp admin-create-user \
     --user-pool-id <user-pool-id> \
     --username admin@example.com \
     --user-attributes Name=email,Value=admin@example.com Name=email_verified,Value=true \
     --message-action SUPPRESS

   # Set permanent password
   aws cognito-idp admin-set-user-password \
     --user-pool-id <user-pool-id> \
     --username admin@example.com \
     --password 'YourSecurePassword123!' \
     --permanent
   ```

3. **Access Honua IO**:
   - Navigate to your Honua IO URL
   - You'll be redirected to Cognito hosted UI
   - Sign in with the credentials you created

### Cognito Hosted UI Customization

```bash
# Update User Pool Client with custom logo
aws cognito-idp update-user-pool-client \
  --user-pool-id <user-pool-id> \
  --client-id <client-id> \
  --supported-identity-providers COGNITO \
  --callback-urls "https://your-domain.com/oauth/callback" \
  --logout-urls "https://your-domain.com/logout"
```

---

## Option 2: Azure Active Directory / Entra ID

**Best for**: Azure marketplace deployments, enterprise SSO

### Prerequisites
- Azure AD tenant
- Global Administrator or Application Administrator role

### Setup Steps

#### 1. Register Application in Azure AD

```bash
# Using Azure CLI
az ad app create \
  --display-name "Honua IO Server" \
  --sign-in-audience AzureADMyOrg \
  --web-redirect-uris "https://your-honua-url.com/oauth/callback" \
  --enable-id-token-issuance true
```

Or via Azure Portal:
1. Navigate to **Azure Active Directory** > **App registrations**
2. Click **New registration**
3. Name: "Honua IO Server"
4. Supported account types: **Single tenant**
5. Redirect URI: **Web** - `https://your-honua-url.com/oauth/callback`
6. Click **Register**

#### 2. Configure Authentication

1. Go to **Authentication** tab
2. Under **Implicit grant and hybrid flows**:
   - ✅ ID tokens
   - ✅ Access tokens
3. Under **Advanced settings**:
   - Allow public client flows: **No**
4. Click **Save**

#### 3. Create Client Secret

1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Description: "Honua IO OAuth Secret"
4. Expires: **24 months** (recommended)
5. Click **Add**
6. **IMPORTANT**: Copy the secret value immediately (you won't see it again)

#### 4. Configure API Permissions

1. Go to **API permissions**
2. Click **Add a permission** > **Microsoft Graph**
3. Select **Delegated permissions**:
   - ✅ `openid`
   - ✅ `profile`
   - ✅ `email`
   - ✅ `User.Read`
4. Click **Add permissions**
5. Click **Grant admin consent** (if you have permissions)

#### 5. Deployment Configuration

```json
{
  "parameters": {
    "OAuthProvider": {
      "value": "azure-ad"
    },
    "OAuthDomain": {
      "value": "login.microsoftonline.com"
    },
    "OAuthClientId": {
      "value": "<application-client-id>"
    },
    "OAuthClientSecret": {
      "value": "<client-secret-value>"
    },
    "OAuthIssuer": {
      "value": "https://login.microsoftonline.com/<tenant-id>/v2.0"
    },
    "OAuthScopes": {
      "value": "openid profile email"
    }
  }
}
```

#### 6. (Optional) Configure Group Claims

To use Azure AD groups for role-based access:

1. Go to **Token configuration**
2. Click **Add groups claim**
3. Select group types to include
4. Under **ID token**, select **Group ID**

---

## Option 3: Google Identity Platform

**Best for**: GCP marketplace deployments, Google Workspace integration

### Setup Steps

#### 1. Create OAuth 2.0 Client ID

1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Navigate to **APIs & Services** > **Credentials**
3. Click **Create Credentials** > **OAuth client ID**
4. Application type: **Web application**
5. Name: "Honua IO Server"
6. Authorized redirect URIs:
   - `https://your-honua-url.com/oauth/callback`
7. Click **Create**
8. Copy **Client ID** and **Client Secret**

#### 2. Configure OAuth Consent Screen

1. Go to **OAuth consent screen**
2. User Type: **Internal** (for Google Workspace) or **External**
3. App information:
   - App name: "Honua IO"
   - Support email: your-email@domain.com
4. Scopes:
   - `openid`
   - `https://www.googleapis.com/auth/userinfo.email`
   - `https://www.googleapis.com/auth/userinfo.profile`
5. Save and continue

#### 3. Deployment Configuration

```yaml
Parameters:
  OAuthProvider: google
  OAuthDomain: accounts.google.com
  OAuthClientId: <your-client-id>.apps.googleusercontent.com
  OAuthClientSecret: <your-client-secret>
  OAuthIssuer: https://accounts.google.com
  OAuthScopes: "openid profile email"
```

---

## Option 4: Okta

**Best for**: Enterprise deployments, advanced identity features

### Setup Steps

#### 1. Create Application in Okta

1. Log in to **Okta Admin Console**
2. Navigate to **Applications** > **Applications**
3. Click **Create App Integration**
4. Sign-in method: **OIDC - OpenID Connect**
5. Application type: **Web Application**
6. Click **Next**

#### 2. Configure Application

1. App integration name: "Honua IO Server"
2. Grant type:
   - ✅ Authorization Code
   - ✅ Refresh Token
3. Sign-in redirect URIs:
   - `https://your-honua-url.com/oauth/callback`
4. Sign-out redirect URIs:
   - `https://your-honua-url.com/logout`
5. Controlled access: Choose appropriate option
6. Click **Save**

#### 3. Get Credentials

1. Copy **Client ID**
2. Copy **Client secret**
3. Note your **Okta domain** (e.g., `dev-12345.okta.com`)

#### 4. Deployment Configuration

```yaml
Parameters:
  OAuthProvider: okta
  OAuthDomain: dev-12345.okta.com
  OAuthClientId: <okta-client-id>
  OAuthClientSecret: <okta-client-secret>
  OAuthIssuer: https://dev-12345.okta.com
  OAuthScopes: "openid profile email"
```

---

## Option 5: Auth0

**Best for**: Rapid deployment, social logins, passwordless

### Setup Steps

#### 1. Create Application

1. Log in to **Auth0 Dashboard**
2. Navigate to **Applications** > **Applications**
3. Click **Create Application**
4. Name: "Honua IO Server"
5. Application type: **Regular Web Applications**
6. Click **Create**

#### 2. Configure Application

1. Go to **Settings** tab
2. Application URIs:
   - Allowed Callback URLs: `https://your-honua-url.com/oauth/callback`
   - Allowed Logout URLs: `https://your-honua-url.com/logout`
   - Allowed Web Origins: `https://your-honua-url.com`
3. Click **Save Changes**

#### 3. Get Credentials

1. Copy **Domain** (e.g., `your-tenant.auth0.com`)
2. Copy **Client ID**
3. Copy **Client Secret**

#### 4. Deployment Configuration

```yaml
Parameters:
  OAuthProvider: auth0
  OAuthDomain: your-tenant.auth0.com
  OAuthClientId: <auth0-client-id>
  OAuthClientSecret: <auth0-client-secret>
  OAuthIssuer: https://your-tenant.auth0.com/
  OAuthScopes: "openid profile email"
```

---

## Option 6: Custom OIDC Provider

**Best for**: Enterprise with existing identity infrastructure

### Requirements

Your OIDC provider must support:
- ✅ OpenID Connect Discovery (`.well-known/openid-configuration`)
- ✅ Authorization Code Flow
- ✅ ID Token with `sub`, `email`, `name` claims
- ✅ PKCE (recommended but not required)

### Deployment Configuration

```yaml
Parameters:
  OAuthProvider: custom
  OAuthDomain: your-idp.example.com
  OAuthClientId: <your-client-id>
  OAuthClientSecret: <your-client-secret>
  OAuthIssuer: https://your-idp.example.com
  OAuthScopes: "openid profile email"
```

### Validation

Test your OIDC provider:

```bash
# Check discovery endpoint
curl https://your-idp.example.com/.well-known/openid-configuration

# Verify required endpoints are present:
# - authorization_endpoint
# - token_endpoint
# - userinfo_endpoint
# - jwks_uri
```

---

## Security Best Practices

### 1. Client Secret Management

**NEVER** commit client secrets to version control. Always use:
- AWS Secrets Manager (for AWS deployments)
- Azure Key Vault (for Azure deployments)
- GCP Secret Manager (for GCP deployments)
- Environment variables with restricted access

### 2. Redirect URI Validation

Always use **exact match** redirect URIs:
- ✅ `https://honua.example.com/oauth/callback`
- ❌ `https://honua.example.com/*` (wildcard - insecure)
- ❌ `http://honua.example.com/oauth/callback` (non-HTTPS - insecure)

### 3. Token Validation

Honua IO automatically validates:
- ✅ Token signature (using IdP's JWKS)
- ✅ Token expiration
- ✅ Issuer claim matches configured issuer
- ✅ Audience claim matches client ID
- ✅ Token not used before valid time

### 4. MFA/2FA

**Highly recommended** to enable MFA in your IdP:
- AWS Cognito: Software TOTP MFA
- Azure AD: Microsoft Authenticator, SMS, or TOTP
- Google: Google Authenticator or hardware keys
- Okta: Okta Verify, Google Authenticator, or YubiKey
- Auth0: Guardian, Google Authenticator, or SMS

### 5. Session Management

Default session configuration:
- **Session timeout**: 1 hour (ID token validity)
- **Refresh token**: 30 days
- **Sliding expiration**: Enabled

---

## Troubleshooting

### Error: "OAuth not configured"

**Cause**: OAuth parameters not set or invalid

**Solution**:
1. Verify all required OAuth parameters are set
2. Check CloudFormation/ARM template outputs
3. Verify secrets are correctly stored

```bash
# AWS
aws secretsmanager get-secret-value --secret-id honua-oauth-secret

# Azure
az keyvault secret show --vault-name honua-kv --name oauth-client-secret
```

### Error: "Invalid redirect URI"

**Cause**: Redirect URI mismatch between IdP and Honua configuration

**Solution**:
1. Verify LoadBalancer URL matches configured redirect URI
2. Update IdP configuration to include actual URL
3. Ensure HTTPS is used (not HTTP)

```bash
# Get actual LoadBalancer URL
kubectl get service honua-gateway -n honua-system -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'
```

### Error: "Invalid client credentials"

**Cause**: Client ID or secret incorrect

**Solution**:
1. Verify client ID and secret are correctly copied
2. Check for extra whitespace or line breaks
3. Regenerate client secret if needed
4. Update Kubernetes secret:

```bash
kubectl create secret generic honua-secrets \
  --from-literal=oauth-client-secret='<new-secret>' \
  --namespace=honua-system \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Error: "Token validation failed"

**Cause**: Clock skew or invalid issuer

**Solution**:
1. Verify issuer URL exactly matches IdP configuration
2. Check system time is synchronized (NTP)
3. Verify IdP's JWKS endpoint is accessible

```bash
# Test JWKS endpoint
curl https://your-idp.example.com/.well-known/jwks.json
```

---

## Testing OAuth Configuration

### 1. Basic Flow Test

```bash
# Access Honua IO
curl -L https://your-honua-url.com

# You should be redirected to IdP login page
# Expected redirect: https://your-idp.com/authorize?client_id=...
```

### 2. Token Validation Test

```bash
# After successful login, check application logs
kubectl logs -n honua-system deployment/honua-server | grep -i oauth

# Look for:
# [INFO] OAuth token validated successfully
# [INFO] User authenticated: user@example.com
```

### 3. Session Test

1. Log in to Honua IO
2. Navigate to a protected resource
3. Wait for token expiration (1 hour)
4. Verify automatic refresh or re-authentication

---

## Multi-Tenancy with OAuth

For multi-tenant deployments, you can:

### Option A: Single IdP with Tenant Claims

Configure your IdP to include tenant information in the ID token:

```json
{
  "sub": "user-123",
  "email": "user@tenant1.com",
  "tenant_id": "tenant-1",
  "tenant_name": "Acme Corp"
}
```

### Option B: Separate IdP per Tenant

Deploy separate Honua instances with different OAuth configurations:

```
Tenant 1: honua-tenant1.example.com → tenant1-idp.okta.com
Tenant 2: honua-tenant2.example.com → tenant2-idp.okta.com
```

---

## Migration from Local Auth

If you have an existing Honua deployment with local authentication:

### WARNING ⚠️

Migrating to OAuth will **disable all local accounts**. Plan accordingly:

1. **Export user list** before migration
2. **Create corresponding OAuth accounts** in your IdP
3. **Map user IDs** to maintain data ownership
4. **Test with pilot group** before full migration
5. **Communicate changes** to all users

### Migration Script

```bash
# 1. Export existing users
kubectl exec -n honua-system deployment/honua-server -- \
  psql $DATABASE_URL -c "SELECT email, name, role FROM users" > users.csv

# 2. Create users in your IdP (example for Cognito)
while IFS=',' read -r email name role; do
  aws cognito-idp admin-create-user \
    --user-pool-id <pool-id> \
    --username "$email" \
    --user-attributes Name=email,Value="$email" Name=name,Value="$name"
done < users.csv

# 3. Update Honua deployment with OAuth configuration
kubectl apply -f honua-oauth-config.yaml

# 4. Restart pods
kubectl rollout restart deployment/honua-server -n honua-system
```

---

## Support

For OAuth-related issues:

1. **Check logs**:
   ```bash
   kubectl logs -n honua-system deployment/honua-server | grep -i auth
   ```

2. **Verify configuration**:
   ```bash
   kubectl get configmap honua-config -n honua-system -o yaml
   ```

3. **Test IdP connectivity**:
   ```bash
   kubectl exec -n honua-system deployment/honua-server -- \
     curl https://your-idp.example.com/.well-known/openid-configuration
   ```

4. **Contact support**: support@honua.io with:
   - Deployment logs
   - OAuth provider type
   - Error messages
   - CloudFormation/ARM template parameters (redacted secrets)

---

## Additional Resources

- [OAuth 2.0 RFC 6749](https://tools.ietf.org/html/rfc6749)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [AWS Cognito Documentation](https://docs.aws.amazon.com/cognito/)
- [Azure AD Authentication Documentation](https://docs.microsoft.com/azure/active-directory/develop/)
- [Google Identity Platform](https://developers.google.com/identity)
- [Okta Developer Documentation](https://developer.okta.com/)
- [Auth0 Documentation](https://auth0.com/docs)
