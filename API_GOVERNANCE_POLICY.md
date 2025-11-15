# API Governance and Breaking Change Policy

## Table of Contents

1. [Overview](#overview)
2. [API Versioning Strategy](#api-versioning-strategy)
3. [Breaking Changes Definition](#breaking-changes-definition)
4. [Non-Breaking Changes](#non-breaking-changes)
5. [Deprecation Process](#deprecation-process)
6. [Version Support Policy](#version-support-policy)
7. [API Design Standards](#api-design-standards)
8. [Change Management Process](#change-management-process)
9. [Documentation Requirements](#documentation-requirements)
10. [Examples](#examples)

---

## Overview

This document defines the API governance policies and breaking change management procedures for the Honua Server project. These policies ensure API stability, backward compatibility, and predictable evolution paths for API consumers.

**Key Principles:**

- **Stability First**: Existing integrations must not break unexpectedly
- **Clear Communication**: Changes are clearly documented and communicated
- **Graceful Transitions**: Adequate deprecation periods for breaking changes
- **Consistent Standards**: Follow industry best practices and established guidelines

---

## API Versioning Strategy

Honua Server uses **URL path versioning** with a semantic versioning approach for API endpoints.

### Versioning Pattern

```
/api/v{major}.{minor}/resource
```

**Examples:**
- `/api/v1.0/shares`
- `/api/v1.1/shares`
- `/api/v2.0/shares`

### Version Components

| Component | Purpose | When to Increment | Example |
|-----------|---------|-------------------|---------|
| **Major** | Breaking changes that require client modifications | Incompatible API changes | v1 → v2 |
| **Minor** | Backward-compatible feature additions | New optional features | v1.0 → v1.1 |
| **Patch** | Bug fixes and internal improvements | Bug fixes only (not in URL) | Internal only |

### Versioning Rules

1. **Major Version** - Increment when introducing breaking changes:
   - Removing or renaming endpoints
   - Changing request/response schemas incompatibly
   - Changing authentication mechanisms
   - Example: `/api/v1.0/shares` → `/api/v2.0/shares`

2. **Minor Version** - Increment when adding backward-compatible features:
   - Adding new optional fields
   - Adding new endpoints
   - Adding new optional query parameters
   - Example: `/api/v1.0/shares` → `/api/v1.1/shares`

3. **Patch Version** - Internal only, not reflected in URL:
   - Bug fixes
   - Performance improvements
   - Internal refactoring
   - Tracked in API documentation and changelog

### Version Header

All API responses include version information:

```http
API-Version: 1.0
API-Supported-Versions: 1.0, 1.1, 2.0
```

---

## Breaking Changes Definition

A **breaking change** is any modification that requires API consumers to update their integration code to maintain functionality.

### What Constitutes a Breaking Change

#### 1. Resource and Field Changes

**Breaking:**
- Removing a resource endpoint
- Renaming a resource endpoint
- Removing a field from response
- Renaming a field in request or response
- Moving a field to a different location in the response hierarchy

**Examples:**

```json
// Breaking: Field renamed
// v1.0
{ "userId": "123" }

// v2.0
{ "userIdentifier": "123" }  // BREAKING
```

```json
// Breaking: Field removed
// v1.0
{ "name": "John", "email": "john@example.com" }

// v2.0
{ "name": "John" }  // BREAKING: email removed
```

#### 2. Data Type and Format Changes

**Breaking:**
- Changing field data type (string → number, object → array, etc.)
- Changing date/time format
- Changing number precision or constraints
- Changing string encoding or format

**Examples:**

```json
// Breaking: Type change
// v1.0
{ "id": "123" }

// v2.0
{ "id": 123 }  // BREAKING: string → number
```

```json
// Breaking: Format change
// v1.0
{ "createdAt": "2025-11-14T12:00:00Z" }  // ISO 8601

// v2.0
{ "createdAt": "1731585600" }  // BREAKING: Unix timestamp
```

#### 3. Request Parameter Changes

**Breaking:**
- Adding a required request parameter (header, query, body field)
- Making an optional parameter required
- Removing support for a parameter value
- Changing parameter validation rules (making them more restrictive)

**Examples:**

```http
// Breaking: New required parameter
// v1.0
POST /api/v1.0/shares
{ "name": "My Share" }

// v2.0
POST /api/v2.0/shares
{ "name": "My Share", "type": "public" }  // BREAKING: type now required
```

#### 4. HTTP Method Changes

**Breaking:**
- Removing an HTTP method from an endpoint
- Changing the HTTP method for an operation
- Changing the semantics of an HTTP method

**Examples:**

```http
// Breaking: Method removed
// v1.0
DELETE /api/v1.0/shares/{id}  // Supported

// v2.0
DELETE /api/v2.0/shares/{id}  // BREAKING: No longer supported
```

#### 5. Error Response Changes

**Breaking:**
- Changing error response structure
- Removing error codes
- Changing HTTP status codes for existing scenarios
- Changing error message formats (if clients parse them)

**Examples:**

```json
// Breaking: Error structure change
// v1.0
{
  "error": "Resource not found",
  "code": 404
}

// v2.0 (RFC 7807)
{
  "type": "https://api.honua.io/problems/not-found",
  "title": "Resource not found",
  "status": 404
}  // BREAKING: Structure changed
```

#### 6. Authentication and Authorization Changes

**Breaking:**
- Changing authentication method (API key → OAuth)
- Adding authentication to previously public endpoints
- Changing required scopes or permissions
- Removing support for an authentication method

#### 7. Enumeration Changes

**Breaking:**
- Removing enum values
- Changing enum value semantics
- Renaming enum values

**Examples:**

```json
// Breaking: Enum value removed
// v1.0
{ "status": "active" | "inactive" | "pending" }

// v2.0
{ "status": "active" | "inactive" }  // BREAKING: pending removed
```

#### 8. Behavioral Changes

**Breaking:**
- Changing default values
- Changing pagination behavior
- Changing sort order
- Changing rate limit thresholds (making them more restrictive)
- Changing idempotency guarantees

---

## Non-Breaking Changes

Changes that can be made without incrementing the major version or breaking existing clients.

### What Can Be Done Without a Major Version Bump

#### 1. Additive Changes

**Non-Breaking:**
- Adding new optional fields to requests
- Adding new fields to responses (clients must ignore unknown fields)
- Adding new resources or endpoints
- Adding new HTTP methods to existing resources
- Adding new optional query parameters
- Adding new optional headers

**Examples:**

```json
// Non-breaking: New optional field
// v1.0
POST /api/v1.0/shares
{ "name": "My Share" }

// v1.1
POST /api/v1.1/shares
{
  "name": "My Share",
  "description": "Optional new field"  // OK: Optional
}
```

```json
// Non-breaking: New response field
// v1.0
{
  "id": "123",
  "name": "My Share"
}

// v1.1
{
  "id": "123",
  "name": "My Share",
  "metadata": { ... }  // OK: Clients should ignore
}
```

#### 2. Enum Expansions

**Non-Breaking (with proper modeling):**
- Adding new enum values when using `modelAsString` (x-ms-enum extensible)

**Examples:**

```json
// Non-breaking: New enum value (with x-ms-enum modelAsString: true)
// v1.0
{ "status": "active" | "inactive" }

// v1.1
{ "status": "active" | "inactive" | "archived" }  // OK if modeled as string
```

**OpenAPI Definition:**

```yaml
status:
  type: string
  enum:
    - active
    - inactive
    - archived
  x-ms-enum:
    name: ShareStatus
    modelAsString: true  # Allows future expansion
```

#### 3. Relaxing Constraints

**Non-Breaking:**
- Making required parameters optional
- Relaxing validation rules (e.g., longer max length)
- Expanding accepted value ranges
- Increasing rate limits

**Examples:**

```json
// Non-breaking: Making field optional
// v1.0
POST /api/v1.0/shares
{
  "name": "Required",
  "description": "Required"
}

// v1.1
POST /api/v1.1/shares
{
  "name": "Required",
  "description": "Now optional"  // OK: Less restrictive
}
```

#### 4. Documentation and Metadata

**Non-Breaking:**
- Improving error messages
- Adding examples and documentation
- Adding deprecation warnings (with proper headers)
- Clarifying existing behavior

---

## Deprecation Process

When a breaking change is necessary, follow this deprecation process to provide clients with adequate time to migrate.

### Deprecation Timeline

| Phase | Duration | Support Level |
|-------|----------|---------------|
| **Active** | Ongoing | Full support: new features, bug fixes, security patches |
| **Deprecated** | Minimum 180 days | Security fixes only, no new features |
| **End-of-Life** | After sunset date | No support, may be removed |

### Minimum Deprecation Period

**180 days** (6 months) from deprecation announcement to sunset date.

### Deprecation Notification

#### 1. Response Headers

Deprecated endpoints must return the following headers:

```http
HTTP/1.1 200 OK
Deprecation: true
Sunset: Wed, 14 May 2026 23:59:59 GMT
Link: </api/v2.0/shares>; rel="alternate"; title="Replacement endpoint"
Link: </docs/migration-guides/v1-to-v2>; rel="help"; title="Migration guide"
API-Version: 1.0
API-Supported-Versions: 1.0, 1.1, 2.0
```

**Header Descriptions:**

- **Deprecation**: RFC 8594 - Indicates the resource is deprecated
- **Sunset**: RFC 8594 - Date when the resource will be sunset
- **Link (alternate)**: Points to the replacement endpoint
- **Link (help)**: Points to migration documentation

#### 2. Documentation Updates

- Add deprecation notice to OpenAPI/Swagger spec
- Update API documentation with deprecation banner
- Add migration guide to documentation site
- Publish changelog entry

**OpenAPI Example:**

```yaml
paths:
  /api/v1.0/shares:
    get:
      deprecated: true
      summary: Get shares (DEPRECATED - Use v2.0)
      description: |
        **DEPRECATED**: This endpoint is deprecated and will be removed on 2026-05-14.
        Please migrate to `/api/v2.0/shares`.

        Migration guide: https://docs.honua.io/migration/v1-to-v2
```

#### 3. Client Communication

- Email notification to registered API consumers
- Blog post announcement
- Release notes entry
- Support ticket system notification

### Migration Guide Requirements

Every deprecated API version must have a migration guide that includes:

1. **What's Changing**: Clear description of breaking changes
2. **Why It's Changing**: Rationale for the change
3. **Migration Steps**: Step-by-step guide to update client code
4. **Code Examples**: Before/after code snippets
5. **Timeline**: Deprecation date and sunset date
6. **Support**: Contact information for migration assistance

**Migration Guide Template:**

```markdown
# Migration Guide: v1.0 to v2.0

## Timeline
- **Deprecation Date**: 2025-11-14
- **Sunset Date**: 2026-05-14
- **Support Period**: 180 days

## Breaking Changes

### 1. Share Status Field Type Change

**What Changed**: The `status` field changed from string to object.

**Why**: To provide more detailed status information.

**Before (v1.0):**
```json
{
  "id": "123",
  "status": "active"
}
```

**After (v2.0):**
```json
{
  "id": "123",
  "status": {
    "state": "active",
    "reason": null,
    "updatedAt": "2025-11-14T12:00:00Z"
  }
}
```

**Migration Steps:**
1. Update your client to parse the new status object structure
2. Access status state via `status.state` instead of `status`
3. Optionally use `status.reason` and `status.updatedAt` for additional context

## Support
Contact api-support@honua.io for migration assistance.
```

---

## Version Support Policy

Honua Server maintains multiple API versions concurrently with varying levels of support.

### Support Levels

#### 1. Current Major Version (Full Support)

- **Support**: New features, bug fixes, security patches
- **Duration**: Until next major version is released + 12 months
- **SLA**: Standard SLA applies
- **Example**: v2.0 (current)

#### 2. Previous Major Version (Security Fixes Only)

- **Support**: Security patches only, no new features
- **Duration**: 12 months after new major version release
- **SLA**: Security patches within 30 days
- **Example**: v1.x (security fixes only)

#### 3. Deprecated Versions (Sunset Period)

- **Support**: Security fixes for critical vulnerabilities only
- **Duration**: 180 days from deprecation announcement
- **SLA**: Best effort for critical security issues
- **Example**: v1.0 (deprecated, sunset in 90 days)

#### 4. End-of-Life Versions (No Support)

- **Support**: None
- **Duration**: Indefinite (may be removed)
- **SLA**: No SLA
- **Example**: v0.9 (EOL, may return 410 Gone)

### Version Support Timeline Example

```
Timeline →
|-------- v1.0 Active (24 months) --------|
                                    |-- v2.0 Released
                                    |-------- v2.0 Active ------→
                                    |-- v1.x Security Fixes (12 months) --|
                                                                    |-- v1.x EOL
```

### Version Sunset Process

1. **T-180 days**: Deprecation announced, headers added
2. **T-90 days**: Email reminder to API consumers
3. **T-30 days**: Final warning email
4. **T-0 days**: Version sunset
   - Option A: Return `410 Gone` status
   - Option B: Redirect to latest version (if compatible)
   - Option C: Remove endpoint entirely

**Sunset Response Example:**

```http
HTTP/1.1 410 Gone
Content-Type: application/problem+json

{
  "type": "https://api.honua.io/problems/endpoint-sunset",
  "title": "Endpoint Sunset",
  "status": 410,
  "detail": "This endpoint was sunset on 2026-05-14. Please migrate to /api/v2.0/shares.",
  "instance": "/api/v1.0/shares/123",
  "sunset-date": "2026-05-14",
  "migration-guide": "https://docs.honua.io/migration/v1-to-v2"
}
```

---

## API Design Standards

Honua Server follows industry best practices for API design, primarily based on Microsoft and Google guidelines.

### Design Guidelines

1. **Microsoft REST API Guidelines**
   - Primary reference: https://github.com/microsoft/api-guidelines
   - Azure-specific patterns where applicable
   - Long-running operations (LRO) pattern
   - OData filtering and pagination conventions

2. **Google API Design Guide**
   - Secondary reference: https://cloud.google.com/apis/design
   - Resource-oriented design
   - Standard methods (List, Get, Create, Update, Delete)
   - Custom methods when standard methods don't fit

3. **RFC 7807 Problem Details**
   - Standard error response format
   - Consistent error structure across all endpoints

### Resource Naming Conventions

#### URL Segments

Use **kebab-case** for URL path segments:

```
✓ Good: /api/v1.0/geoprocessing-jobs
✗ Bad:  /api/v1.0/GeoprocessingJobs
✗ Bad:  /api/v1.0/geoprocessing_jobs
```

#### Collection Resources

Use **plural nouns** for collections:

```
✓ Good: /api/v1.0/shares
✓ Good: /api/v1.0/shares/{id}/comments
✗ Bad:  /api/v1.0/share
✗ Bad:  /api/v1.0/shares/{id}/comment
```

#### JSON Properties

Use **camelCase** for JSON property names:

```json
✓ Good:
{
  "shareId": "123",
  "createdAt": "2025-11-14T12:00:00Z",
  "ownerEmail": "user@example.com"
}

✗ Bad:
{
  "ShareId": "123",         // PascalCase
  "created_at": "...",      // snake_case
  "owner-email": "..."      // kebab-case
}
```

### HTTP Methods

Use standard HTTP methods with consistent semantics:

| Method | Purpose | Idempotent | Safe | Example |
|--------|---------|-----------|------|---------|
| GET | Retrieve resource(s) | Yes | Yes | `GET /shares` |
| POST | Create new resource | No | No | `POST /shares` |
| PUT | Replace entire resource | Yes | No | `PUT /shares/123` |
| PATCH | Partial update | No* | No | `PATCH /shares/123` |
| DELETE | Remove resource | Yes | No | `DELETE /shares/123` |
| HEAD | Get metadata only | Yes | Yes | `HEAD /shares/123` |
| OPTIONS | Get allowed methods | Yes | Yes | `OPTIONS /shares` |

*PATCH can be made idempotent with proper design

### Status Codes

Use HTTP status codes consistently:

#### Success Codes

| Code | Meaning | Use Case |
|------|---------|----------|
| 200 OK | Success with body | GET, PATCH, PUT with response |
| 201 Created | Resource created | POST creating new resource |
| 202 Accepted | Async operation started | Long-running operations |
| 204 No Content | Success without body | DELETE, PUT/PATCH with no response |

#### Client Error Codes

| Code | Meaning | Use Case |
|------|---------|----------|
| 400 Bad Request | Invalid request | Validation failure |
| 401 Unauthorized | Authentication required | Missing/invalid credentials |
| 403 Forbidden | Insufficient permissions | Authorization failure |
| 404 Not Found | Resource doesn't exist | Invalid resource ID |
| 409 Conflict | Resource state conflict | Concurrent modification |
| 410 Gone | Resource permanently removed | Sunset endpoint |
| 422 Unprocessable Entity | Semantic validation failure | Business rule violation |
| 429 Too Many Requests | Rate limit exceeded | Throttling |

#### Server Error Codes

| Code | Meaning | Use Case |
|------|---------|----------|
| 500 Internal Server Error | Unexpected error | Unhandled exception |
| 502 Bad Gateway | Upstream failure | Dependent service error |
| 503 Service Unavailable | Temporary unavailability | Maintenance, overload |
| 504 Gateway Timeout | Upstream timeout | Dependent service timeout |

### Error Response Format (RFC 7807)

All error responses must use the Problem Details format:

```json
{
  "type": "https://api.honua.io/problems/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1.0/shares",
  "errors": {
    "name": ["The name field is required."],
    "expiresAt": ["The expiration date must be in the future."]
  }
}
```

**Required Fields:**

- `type`: URI identifying the problem type
- `title`: Short, human-readable summary
- `status`: HTTP status code
- `detail`: Human-readable explanation

**Optional Fields:**

- `instance`: URI reference to the specific occurrence
- `errors`: Validation errors (for 400/422 responses)
- Additional custom fields as needed

### Pagination

Use consistent pagination across all collection endpoints:

#### Request Parameters

```
GET /api/v1.0/shares?page=2&pageSize=50&sortBy=createdAt&sortOrder=desc
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| page | integer | 1 | Page number (1-indexed) |
| pageSize | integer | 20 | Items per page (max 100) |
| sortBy | string | id | Field to sort by |
| sortOrder | string | asc | Sort direction (asc/desc) |

#### Response Format

```json
{
  "data": [...],
  "pagination": {
    "page": 2,
    "pageSize": 50,
    "totalItems": 237,
    "totalPages": 5,
    "hasNextPage": true,
    "hasPreviousPage": true
  },
  "links": {
    "self": "/api/v1.0/shares?page=2&pageSize=50",
    "first": "/api/v1.0/shares?page=1&pageSize=50",
    "prev": "/api/v1.0/shares?page=1&pageSize=50",
    "next": "/api/v1.0/shares?page=3&pageSize=50",
    "last": "/api/v1.0/shares?page=5&pageSize=50"
  }
}
```

### Filtering and Searching

Use OData-inspired query parameters:

```
GET /api/v1.0/shares?$filter=status eq 'active' and createdAt gt '2025-01-01'
GET /api/v1.0/shares?$search=project
GET /api/v1.0/shares?$select=id,name,createdAt
```

---

## Change Management Process

All API changes follow a structured review and approval process.

### 1. Design Review

**Required for**: All API changes (breaking and non-breaking)

**Process**:
1. Create API design proposal document
2. Submit for peer review (minimum 2 reviewers)
3. Address feedback and iterate
4. Document decision in ADR (Architecture Decision Record)

**Design Proposal Template**:

```markdown
# API Design Proposal: [Feature Name]

## Context
[Why is this change needed?]

## Proposed Changes
[Detailed description of API changes]

## Breaking Change Analysis
- [ ] Breaking change: Yes/No
- [ ] Justification: [If yes, why is breaking change necessary?]
- [ ] Migration path: [How will clients migrate?]

## Alternatives Considered
[What other approaches were considered and why were they rejected?]

## OpenAPI Spec
[Include OpenAPI specification]

## Examples
[Request/response examples]

## Implementation Notes
[Technical considerations]
```

### 2. Architecture Approval

**Required for**: Breaking changes only

**Process**:
1. Design review must be completed first
2. Submit to architecture review board
3. Present breaking change justification
4. Discuss migration strategy
5. Obtain approval before implementation

**Approval Criteria**:
- Breaking change is necessary (no non-breaking alternative)
- Migration path is clear and documented
- Deprecation timeline is reasonable (minimum 180 days)
- Customer impact is understood and acceptable

### 3. Implementation

**Process**:
1. Implement API changes
2. Write comprehensive unit and integration tests
3. Update OpenAPI specification
4. Write XML documentation comments
5. Create code examples
6. Submit pull request for code review

### 4. Documentation

**Required before release**:
- [ ] OpenAPI/Swagger spec updated
- [ ] API reference documentation updated
- [ ] Migration guide created (if breaking change)
- [ ] Changelog entry added
- [ ] Release notes updated

### 5. Communication

**For breaking changes**:
1. Publish deprecation notice (T-180 days before sunset)
2. Email registered API consumers
3. Publish blog post
4. Add deprecation headers to responses
5. Send reminder emails (T-90 days and T-30 days)

### 6. Release

**Process**:
1. Deploy to staging environment
2. Run smoke tests against staging
3. Deploy to production during maintenance window
4. Monitor for errors and performance issues
5. Communicate release completion

---

## Documentation Requirements

All public APIs must be thoroughly documented before release.

### 1. OpenAPI/Swagger Specification

**Required**: Every endpoint must have complete OpenAPI 3.0 specification

**Minimum Requirements**:
- Operation summary and description
- Request parameters (path, query, header, body)
- Request body schema with examples
- Response schemas for all status codes
- Response examples
- Authentication requirements
- Deprecation status (if applicable)

**Example**:

```yaml
openapi: 3.0.0
info:
  title: Honua Server API
  version: 1.0.0

paths:
  /api/v1.0/shares:
    get:
      summary: List shares
      description: |
        Retrieves a paginated list of shares accessible to the authenticated user.

        Supports filtering, sorting, and searching via query parameters.
      operationId: listShares
      tags:
        - Shares
      security:
        - BearerAuth: []
      parameters:
        - name: page
          in: query
          description: Page number (1-indexed)
          required: false
          schema:
            type: integer
            minimum: 1
            default: 1
        - name: pageSize
          in: query
          description: Number of items per page
          required: false
          schema:
            type: integer
            minimum: 1
            maximum: 100
            default: 20
        - name: $filter
          in: query
          description: OData-style filter expression
          required: false
          schema:
            type: string
          example: "status eq 'active'"
      responses:
        '200':
          description: Success
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ShareListResponse'
              example:
                data:
                  - id: "share_123"
                    name: "Q4 2025 Report"
                    status: "active"
                    createdAt: "2025-11-14T12:00:00Z"
                pagination:
                  page: 1
                  pageSize: 20
                  totalItems: 42
                  totalPages: 3
        '401':
          $ref: '#/components/responses/Unauthorized'
        '500':
          $ref: '#/components/responses/InternalServerError'

components:
  securitySchemes:
    BearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT

  schemas:
    ShareListResponse:
      type: object
      required:
        - data
        - pagination
      properties:
        data:
          type: array
          items:
            $ref: '#/components/schemas/Share'
        pagination:
          $ref: '#/components/schemas/PaginationMetadata'

    Share:
      type: object
      required:
        - id
        - name
        - status
        - createdAt
      properties:
        id:
          type: string
          description: Unique share identifier
          example: "share_123"
        name:
          type: string
          description: Display name of the share
          example: "Q4 2025 Report"
        status:
          type: string
          description: Current status of the share
          enum:
            - active
            - inactive
            - expired
          x-ms-enum:
            name: ShareStatus
            modelAsString: true
        createdAt:
          type: string
          format: date-time
          description: ISO 8601 timestamp when share was created

  responses:
    Unauthorized:
      description: Authentication required
      content:
        application/problem+json:
          schema:
            $ref: '#/components/schemas/ProblemDetails'

    InternalServerError:
      description: Internal server error
      content:
        application/problem+json:
          schema:
            $ref: '#/components/schemas/ProblemDetails'
```

### 2. XML Documentation Comments

**Required**: All public controllers, methods, and models must have XML comments

**Example**:

```csharp
/// <summary>
/// API controller for managing shares.
/// </summary>
/// <remarks>
/// Provides endpoints for creating, retrieving, updating, and deleting shares.
/// All endpoints require authentication.
/// </remarks>
[ApiController]
[Route("api/v{version:apiVersion}/shares")]
[Authorize]
public class SharesController : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated list of shares.
    /// </summary>
    /// <remarks>
    /// Returns all shares accessible to the authenticated user.
    /// Supports filtering using OData syntax via the $filter query parameter.
    ///
    /// Example filters:
    /// - status eq 'active'
    /// - createdAt gt '2025-01-01'
    /// - status eq 'active' and name contains 'report'
    /// </remarks>
    /// <param name="page">Page number (1-indexed). Default: 1</param>
    /// <param name="pageSize">Items per page (1-100). Default: 20</param>
    /// <param name="filter">OData filter expression</param>
    /// <param name="sortBy">Field to sort by. Default: id</param>
    /// <param name="sortOrder">Sort direction (asc/desc). Default: asc</param>
    /// <returns>Paginated list of shares</returns>
    /// <response code="200">Success - Returns paginated share list</response>
    /// <response code="400">Bad Request - Invalid query parameters</response>
    /// <response code="401">Unauthorized - Authentication required</response>
    /// <response code="500">Internal Server Error - Unexpected error occurred</response>
    [HttpGet]
    [ProducesResponseType(typeof(ShareListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ShareListResponse>> ListShares(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? filter = null,
        [FromQuery] string sortBy = "id",
        [FromQuery] string sortOrder = "asc")
    {
        // Implementation
    }
}
```

### 3. Example Requests and Responses

**Required**: Provide realistic examples for all common scenarios

**Example Documentation**:

```markdown
## Create Share

Creates a new share.

### Request

```http
POST /api/v1.0/shares HTTP/1.1
Host: api.honua.io
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "name": "Q4 2025 Financial Report",
  "description": "Quarterly financial results and analysis",
  "expiresAt": "2026-01-31T23:59:59Z",
  "permissions": {
    "allowDownload": true,
    "allowComment": false
  }
}
```

### Success Response

```http
HTTP/1.1 201 Created
Location: /api/v1.0/shares/share_abc123
Content-Type: application/json

{
  "id": "share_abc123",
  "name": "Q4 2025 Financial Report",
  "description": "Quarterly financial results and analysis",
  "status": "active",
  "expiresAt": "2026-01-31T23:59:59Z",
  "createdAt": "2025-11-14T12:30:45Z",
  "createdBy": "user_123",
  "permissions": {
    "allowDownload": true,
    "allowComment": false
  },
  "links": {
    "self": "/api/v1.0/shares/share_abc123",
    "comments": "/api/v1.0/shares/share_abc123/comments"
  }
}
```

### Error Response (Validation Failure)

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://api.honua.io/problems/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1.0/shares",
  "errors": {
    "name": ["The name field is required."],
    "expiresAt": ["The expiration date must be in the future."]
  }
}
```
```

### 4. Authentication Requirements

**Required**: Document authentication method and required scopes/permissions

**Example**:

```markdown
## Authentication

All API endpoints require authentication using JWT bearer tokens.

### Obtaining a Token

```http
POST /auth/token HTTP/1.1
Content-Type: application/json

{
  "username": "user@example.com",
  "password": "secure_password"
}
```

Response:

```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

### Using the Token

Include the token in the Authorization header:

```http
GET /api/v1.0/shares HTTP/1.1
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Required Scopes

| Endpoint | Required Scope | Description |
|----------|----------------|-------------|
| GET /shares | shares:read | Read access to shares |
| POST /shares | shares:write | Create new shares |
| PUT /shares/{id} | shares:write | Update existing shares |
| DELETE /shares/{id} | shares:delete | Delete shares |
| GET /admin/* | admin | Administrative access |
```

---

## Examples

### Example 1: Adding a Non-Breaking Field

**Scenario**: Add an optional `tags` field to shares

**Version**: v1.0 → v1.1 (minor version bump)

**Change Type**: Non-breaking (additive change)

**Implementation**:

```csharp
// v1.1 - Add optional tags field
public class Share
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }

    // New optional field in v1.1
    public List<string>? Tags { get; set; }  // Nullable, optional
}
```

**OpenAPI Update**:

```yaml
# v1.1 OpenAPI spec
Share:
  type: object
  required:
    - id
    - name
    - status
    - createdAt
  properties:
    id:
      type: string
    name:
      type: string
    status:
      type: string
    createdAt:
      type: string
      format: date-time
    tags:  # New in v1.1
      type: array
      items:
        type: string
      description: Optional tags for categorization (added in v1.1)
```

**Migration**: None required - clients can ignore the new field

---

### Example 2: Breaking Change - Field Type Modification

**Scenario**: Change `expiresAt` from string to object with additional metadata

**Version**: v1.0 → v2.0 (major version bump, breaking change)

**Change Type**: Breaking (changes field type)

**v1.0 Response**:

```json
{
  "id": "share_123",
  "name": "My Share",
  "expiresAt": "2026-01-31T23:59:59Z"
}
```

**v2.0 Response**:

```json
{
  "id": "share_123",
  "name": "My Share",
  "expiration": {
    "expiresAt": "2026-01-31T23:59:59Z",
    "autoRenew": false,
    "notifyBeforeExpiry": true,
    "notifyDays": 7
  }
}
```

**Migration Steps**:

1. Deploy v2.0 alongside v1.0 (both versions active)
2. Add deprecation headers to v1.0:
   ```http
   Deprecation: true
   Sunset: Wed, 14 May 2026 23:59:59 GMT
   Link: </api/v2.0/shares>; rel="alternate"
   ```
3. Publish migration guide
4. Email API consumers
5. Wait 180 days minimum
6. Sunset v1.0

**Migration Guide**:

```markdown
### Expiration Field Changes

**What Changed**: The `expiresAt` field has been replaced with an `expiration` object.

**Before (v1.0)**:
```json
{
  "expiresAt": "2026-01-31T23:59:59Z"
}
```

**After (v2.0)**:
```json
{
  "expiration": {
    "expiresAt": "2026-01-31T23:59:59Z",
    "autoRenew": false,
    "notifyBeforeExpiry": true,
    "notifyDays": 7
  }
}
```

**Migration Code**:

```javascript
// Before (v1.0)
const expirationDate = response.expiresAt;

// After (v2.0)
const expirationDate = response.expiration.expiresAt;
const autoRenew = response.expiration.autoRenew;
```
```

---

### Example 3: Deprecating an Endpoint

**Scenario**: Deprecate `GET /api/v1.0/shares/{id}/metadata` in favor of including metadata in main share response

**Version**: v1.0 (deprecated) → v1.1 (replacement)

**Process**:

1. **Add metadata to main endpoint in v1.1**:
   ```http
   GET /api/v1.1/shares/{id}
   ```
   Response includes metadata field:
   ```json
   {
     "id": "share_123",
     "name": "My Share",
     "metadata": {
       "fileCount": 42,
       "totalSize": 1048576,
       "lastModified": "2025-11-14T12:00:00Z"
     }
   }
   ```

2. **Add deprecation headers to old endpoint**:
   ```http
   GET /api/v1.0/shares/{id}/metadata

   HTTP/1.1 200 OK
   Deprecation: true
   Sunset: Wed, 14 May 2026 23:59:59 GMT
   Link: </api/v1.1/shares/{id}>; rel="alternate"; title="Use main share endpoint"
   ```

3. **After sunset date, return 410 Gone**:
   ```http
   GET /api/v1.0/shares/{id}/metadata

   HTTP/1.1 410 Gone
   Content-Type: application/problem+json

   {
     "type": "https://api.honua.io/problems/endpoint-sunset",
     "title": "Endpoint Sunset",
     "status": 410,
     "detail": "This endpoint was sunset on 2026-05-14. Metadata is now included in the main share response.",
     "instance": "/api/v1.0/shares/share_123/metadata",
     "migration": {
       "replacement": "/api/v1.1/shares/share_123",
       "guide": "https://docs.honua.io/migration/metadata-endpoint"
     }
   }
   ```

---

## Enforcement

This policy is enforced through:

1. **Code Review**: All API changes reviewed against this policy
2. **Automated Testing**: OpenAPI spec validation in CI/CD pipeline
3. **Architecture Review Board**: Approval required for breaking changes
4. **Documentation Requirements**: PR cannot merge without updated docs
5. **Version Header Validation**: Automated checks for required headers

---

## References

- [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines)
- [Google API Design Guide](https://cloud.google.com/apis/design)
- [RFC 7807 - Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [RFC 8594 - Sunset HTTP Header](https://tools.ietf.org/html/rfc8594)
- [Semantic Versioning 2.0.0](https://semver.org/)
- [OpenAPI Specification 3.0](https://swagger.io/specification/)

---

## Document Metadata

- **Version**: 1.0
- **Last Updated**: 2025-11-14
- **Owner**: API Platform Team
- **Review Cycle**: Quarterly
- **Next Review**: 2026-02-14

---

## Changelog

### Version 1.0 (2025-11-14)
- Initial policy document
- Defined versioning strategy
- Established breaking change criteria
- Documented deprecation process
- Defined support policy
- Established design standards
