# Single Page Application (SPA) Integration Guide

## Overview

Honua fully supports Single Page Applications (SPAs) through built-in CORS configuration, JWT Bearer authentication, and multiple deployment architecture options. This guide provides complete integration examples for React, Vue, and Angular.

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [CORS Configuration](#cors-configuration)
3. [Authentication Setup](#authentication-setup)
4. [Framework Integration Examples](#framework-integration-examples)
5. [Deployment Architectures](#deployment-architectures)
6. [Service Worker Caching](#service-worker-caching)
7. [Troubleshooting](#troubleshooting)

---

## Quick Start

### 1. Enable CORS in Honua

Add CORS configuration to your `metadata.json`:

```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": [
        "http://localhost:3000",
        "http://localhost:5173",
        "https://app.example.com"
      ],
      "allowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "allowedHeaders": ["Content-Type", "Authorization", "X-Requested-With"],
      "exposedHeaders": ["X-Total-Count", "X-Page-Size", "X-Page-Number"],
      "allowCredentials": true,
      "maxAge": 3600
    }
  },
  "catalog": { ... },
  "services": [ ... ],
  "layers": [ ... ]
}
```

### 2. Test Cross-Origin Request

```javascript
fetch('https://api.example.com/geoservices/rest/services/parcels/FeatureServer/0/query?where=1=1&f=json')
  .then(res => res.json())
  .then(data => console.log('Features:', data.features));
```

If you see features, CORS is working! ✅

---

## CORS Configuration

### Basic Configuration

```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": ["https://app.example.com"]
    }
  }
}
```

### Development Configuration (Multiple Localhost Ports)

```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": [
        "http://localhost:3000",     // React (Create React App)
        "http://localhost:5173",     // Vite
        "http://localhost:4200",     // Angular
        "http://localhost:8080"      // Vue
      ]
    }
  }
}
```

### Wildcard Subdomain Configuration

```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": [
        "https://*.example.com"      // Matches app.example.com, staging.example.com, etc.
      ]
    }
  }
}
```

### Production Configuration

```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": ["https://app.example.com"],
      "allowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "allowedHeaders": ["Content-Type", "Authorization", "X-Requested-With"],
      "exposedHeaders": ["X-Total-Count", "X-Page-Size"],
      "allowCredentials": true,
      "maxAge": 3600
    }
  }
}
```

**Important:** Never use `"allowedOrigins": ["*"]` in production with `allowCredentials: true` - browsers will reject this.

---

## Authentication Setup

### JWT Bearer Token Authentication

Honua uses JWT Bearer tokens for authentication. Here's how to configure it:

#### 1. Obtain JWT Token

Your authentication server should issue JWT tokens:

```javascript
// Example: Login to get JWT token
const response = await fetch('https://auth.example.com/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'user', password: 'pass' })
});

const { token } = await response.json();
localStorage.setItem('auth_token', token);
```

#### 2. Include Token in Honua Requests

```javascript
const token = localStorage.getItem('auth_token');

const response = await fetch('https://api.example.com/geoservices/rest/services/parcels/FeatureServer/0/query', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  }
});
```

#### 3. Handle Token Expiration

```javascript
async function fetchWithAuth(url, options = {}) {
  const token = localStorage.getItem('auth_token');

  const response = await fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      'Authorization': `Bearer ${token}`
    }
  });

  if (response.status === 401) {
    // Token expired, redirect to login
    localStorage.removeItem('auth_token');
    window.location.href = '/login';
    return;
  }

  return response;
}
```

---

## Framework Integration Examples

### React with Axios

#### Installation

```bash
npm install axios
```

#### API Client Setup

```javascript
// src/api/honuaClient.js
import axios from 'axios';

const honuaClient = axios.create({
  baseURL: 'https://api.example.com',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json'
  }
});

// Request interceptor: Add JWT token
honuaClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('auth_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor: Handle token expiration
honuaClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('auth_token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default honuaClient;
```

#### Query Features

```javascript
// src/api/parcels.js
import honuaClient from './honuaClient';

export async function queryParcels(where = '1=1', outFields = '*') {
  const response = await honuaClient.get(
    '/geoservices/rest/services/parcels/FeatureServer/0/query',
    {
      params: {
        where,
        outFields,
        f: 'json',
        returnGeometry: true,
        spatialRel: 'esriSpatialRelIntersects'
      }
    }
  );
  return response.data;
}

export async function getParcelById(objectId) {
  const response = await honuaClient.get(
    `/geoservices/rest/services/parcels/FeatureServer/0/${objectId}`,
    { params: { f: 'json' } }
  );
  return response.data;
}
```

#### React Component

```javascript
// src/components/ParcelList.jsx
import React, { useEffect, useState } from 'react';
import { queryParcels } from '../api/parcels';

export default function ParcelList() {
  const [parcels, setParcels] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    async function loadParcels() {
      try {
        const data = await queryParcels('CITY = "Portland"', 'OBJECTID,ADDRESS,OWNER');
        setParcels(data.features);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    }
    loadParcels();
  }, []);

  if (loading) return <div>Loading parcels...</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <div>
      <h2>Parcels in Portland</h2>
      <ul>
        {parcels.map((feature) => (
          <li key={feature.attributes.OBJECTID}>
            {feature.attributes.ADDRESS} - Owner: {feature.attributes.OWNER}
          </li>
        ))}
      </ul>
    </div>
  );
}
```

---

### Vue with Pinia Store

#### Installation

```bash
npm install pinia
```

#### Pinia Store

```javascript
// src/stores/parcels.js
import { defineStore } from 'pinia';

export const useParcelsStore = defineStore('parcels', {
  state: () => ({
    features: [],
    loading: false,
    error: null
  }),

  actions: {
    async fetchParcels(where = '1=1') {
      this.loading = true;
      this.error = null;

      try {
        const token = localStorage.getItem('auth_token');
        const response = await fetch(
          `https://api.example.com/geoservices/rest/services/parcels/FeatureServer/0/query?where=${encodeURIComponent(where)}&outFields=*&f=json`,
          {
            headers: {
              'Authorization': `Bearer ${token}`,
              'Content-Type': 'application/json'
            }
          }
        );

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();
        this.features = data.features;
      } catch (err) {
        this.error = err.message;
      } finally {
        this.loading = false;
      }
    }
  }
});
```

#### Vue Component

```vue
<!-- src/components/ParcelList.vue -->
<template>
  <div>
    <h2>Parcels in Portland</h2>

    <div v-if="parcelsStore.loading">Loading parcels...</div>
    <div v-else-if="parcelsStore.error" class="error">
      Error: {{ parcelsStore.error }}
    </div>
    <ul v-else>
      <li v-for="feature in parcelsStore.features" :key="feature.attributes.OBJECTID">
        {{ feature.attributes.ADDRESS }} - Owner: {{ feature.attributes.OWNER }}
      </li>
    </ul>
  </div>
</template>

<script setup>
import { onMounted } from 'vue';
import { useParcelsStore } from '../stores/parcels';

const parcelsStore = useParcelsStore();

onMounted(() => {
  parcelsStore.fetchParcels('CITY = "Portland"');
});
</script>

<style scoped>
.error {
  color: red;
  padding: 1rem;
  border: 1px solid red;
  border-radius: 4px;
}
</style>
```

---

### Angular with HttpClient

#### Service

```typescript
// src/app/services/honua.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

export interface FeatureQueryResponse {
  features: Array<{
    attributes: Record<string, any>;
    geometry?: any;
  }>;
}

@Injectable({
  providedIn: 'root'
})
export class HonuaService {
  private baseUrl = 'https://api.example.com/geoservices/rest/services';

  constructor(private http: HttpClient) {}

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('auth_token');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': token ? `Bearer ${token}` : ''
    });
  }

  queryParcels(where: string = '1=1', outFields: string = '*'): Observable<FeatureQueryResponse> {
    const params = new HttpParams()
      .set('where', where)
      .set('outFields', outFields)
      .set('f', 'json')
      .set('returnGeometry', 'true');

    return this.http.get<FeatureQueryResponse>(
      `${this.baseUrl}/parcels/FeatureServer/0/query`,
      { headers: this.getHeaders(), params }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          localStorage.removeItem('auth_token');
          window.location.href = '/login';
        }
        return throwError(() => new Error(`Query failed: ${error.message}`));
      })
    );
  }

  getParcelById(objectId: number): Observable<any> {
    return this.http.get(
      `${this.baseUrl}/parcels/FeatureServer/0/${objectId}`,
      {
        headers: this.getHeaders(),
        params: { f: 'json' }
      }
    );
  }
}
```

#### Component

```typescript
// src/app/components/parcel-list/parcel-list.component.ts
import { Component, OnInit } from '@angular/core';
import { HonuaService, FeatureQueryResponse } from '../../services/honua.service';

@Component({
  selector: 'app-parcel-list',
  templateUrl: './parcel-list.component.html',
  styleUrls: ['./parcel-list.component.css']
})
export class ParcelListComponent implements OnInit {
  parcels: any[] = [];
  loading = true;
  error: string | null = null;

  constructor(private honuaService: HonuaService) {}

  ngOnInit(): void {
    this.loadParcels();
  }

  loadParcels(): void {
    this.honuaService.queryParcels('CITY = "Portland"', 'OBJECTID,ADDRESS,OWNER')
      .subscribe({
        next: (data: FeatureQueryResponse) => {
          this.parcels = data.features;
          this.loading = false;
        },
        error: (err) => {
          this.error = err.message;
          this.loading = false;
        }
      });
  }
}
```

#### Template

```html
<!-- src/app/components/parcel-list/parcel-list.component.html -->
<div>
  <h2>Parcels in Portland</h2>

  <div *ngIf="loading">Loading parcels...</div>
  <div *ngIf="error" class="error">Error: {{ error }}</div>

  <ul *ngIf="!loading && !error">
    <li *ngFor="let feature of parcels">
      {{ feature.attributes.ADDRESS }} - Owner: {{ feature.attributes.OWNER }}
    </li>
  </ul>
</div>
```

---

## Deployment Architectures

### Option 1: CORS Headers (Simplest)

**Architecture:**
- SPA: `https://app.example.com`
- API: `https://api.example.com`

**Pros:**
- Simple to set up
- Works immediately
- No additional infrastructure

**Cons:**
- Requires CORS preflight requests (adds latency)
- Cookies require `allowCredentials: true`

**Configuration:**

```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": ["https://app.example.com"],
      "allowCredentials": true
    }
  }
}
```

---

### Option 2: Subdomain Deployment (Recommended)

**Architecture:**
- SPA: `https://app.example.com` (S3 + CloudFront)
- API: `https://api.example.com` (Honua on ECS/AKS/Cloud Run)

**Pros:**
- Same root domain (`example.com`)
- Cookies work without CORS
- CDN caching for SPA assets

**Cons:**
- Requires DNS configuration
- Slightly more complex deployment

**Terraform Example (AWS CloudFront):**

```hcl
# SPA distribution (S3 origin)
resource "aws_cloudfront_distribution" "spa" {
  origin {
    domain_name = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id   = "spa-s3"

    s3_origin_config {
      origin_access_identity = aws_cloudfront_origin_access_identity.spa.cloudfront_access_identity_path
    }
  }

  aliases = ["app.example.com"]

  default_cache_behavior {
    target_origin_id       = "spa-s3"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]

    forwarded_values {
      query_string = false
      cookies { forward = "none" }
    }
  }

  viewer_certificate {
    acm_certificate_arn = aws_acm_certificate.app.arn
    ssl_support_method  = "sni-only"
  }
}

# API distribution (ALB origin)
resource "aws_cloudfront_distribution" "api" {
  origin {
    domain_name = aws_lb.honua.dns_name
    origin_id   = "honua-api"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  aliases = ["api.example.com"]

  default_cache_behavior {
    target_origin_id       = "honua-api"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods         = ["GET", "HEAD"]

    forwarded_values {
      query_string = true
      headers      = ["Authorization", "Accept", "Content-Type"]
      cookies { forward = "all" }
    }
  }

  viewer_certificate {
    acm_certificate_arn = aws_acm_certificate.api.arn
    ssl_support_method  = "sni-only"
  }
}
```

---

### Option 3: API Gateway Path Routing (Enterprise)

**Architecture:**
- Single domain: `https://app.example.com`
- Path routing:
  - `/` → SPA (S3)
  - `/api/*` → Honua API (ECS)
  - `/geoservices/*` → Honua GeoServices (ECS)

**Pros:**
- Single domain (no CORS needed)
- Simplified DNS
- Best for mobile apps (no preflight)

**Cons:**
- More complex CloudFront configuration
- Path conflicts possible

**Terraform Example (AWS CloudFront with Multi-Origin):**

```hcl
resource "aws_cloudfront_distribution" "unified" {
  # SPA origin (S3)
  origin {
    domain_name = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id   = "spa-s3"
  }

  # API origin (ALB)
  origin {
    domain_name = aws_lb.honua.dns_name
    origin_id   = "honua-api"
  }

  aliases = ["app.example.com"]

  # Default: Serve SPA
  default_cache_behavior {
    target_origin_id       = "spa-s3"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]

    forwarded_values {
      query_string = false
      cookies { forward = "none" }
    }
  }

  # Path: /api/* → Honua API
  ordered_cache_behavior {
    path_pattern     = "/api/*"
    target_origin_id = "honua-api"

    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods         = ["GET", "HEAD"]

    forwarded_values {
      query_string = true
      headers      = ["Authorization", "Accept", "Content-Type"]
      cookies { forward = "all" }
    }
  }

  # Path: /geoservices/* → Honua GeoServices
  ordered_cache_behavior {
    path_pattern     = "/geoservices/*"
    target_origin_id = "honua-api"

    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods         = ["GET", "HEAD"]

    forwarded_values {
      query_string = true
      headers      = ["Authorization"]
      cookies { forward = "none" }
    }
  }

  viewer_certificate {
    acm_certificate_arn = aws_acm_certificate.app.arn
    ssl_support_method  = "sni-only"
  }
}
```

---

## Service Worker Caching

Service Workers enable offline GIS tile and feature caching for PWAs.

### Service Worker Registration

```javascript
// src/serviceWorkerRegistration.js
export function register() {
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('/service-worker.js')
        .then(registration => {
          console.log('Service Worker registered:', registration);
        })
        .catch(error => {
          console.error('Service Worker registration failed:', error);
        });
    });
  }
}
```

### Service Worker Implementation

```javascript
// public/service-worker.js
const CACHE_VERSION = 'honua-gis-v1';
const TILE_CACHE = 'gis-tiles-v1';
const FEATURE_CACHE = 'gis-features-v1';

// Cache GIS tiles aggressively (they rarely change)
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // Strategy 1: Cache-first for map tiles
  if (url.pathname.includes('/MapServer/tile/')) {
    event.respondWith(
      caches.match(event.request).then(cachedResponse => {
        if (cachedResponse) {
          return cachedResponse;
        }

        return fetch(event.request).then(networkResponse => {
          return caches.open(TILE_CACHE).then(cache => {
            cache.put(event.request, networkResponse.clone());
            return networkResponse;
          });
        });
      })
    );
    return;
  }

  // Strategy 2: Network-first for feature queries (fresh data)
  if (url.pathname.includes('/FeatureServer/') && url.pathname.includes('/query')) {
    event.respondWith(
      fetch(event.request)
        .then(networkResponse => {
          // Cache successful responses
          if (networkResponse.ok) {
            caches.open(FEATURE_CACHE).then(cache => {
              cache.put(event.request, networkResponse.clone());
            });
          }
          return networkResponse;
        })
        .catch(() => {
          // Fallback to cache if offline
          return caches.match(event.request);
        })
    );
    return;
  }

  // Default: network-only for API requests
  event.respondWith(fetch(event.request));
});

// Clean up old caches
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames
          .filter(cacheName => cacheName !== TILE_CACHE && cacheName !== FEATURE_CACHE)
          .map(cacheName => caches.delete(cacheName))
      );
    })
  );
});
```

**Important:** Map tiles can be cached aggressively (weeks/months), but feature queries should use network-first to ensure data freshness.

---

## Troubleshooting

### CORS Error: "No 'Access-Control-Allow-Origin' header"

**Problem:** Browser blocks cross-origin request.

**Solution:**
1. Verify CORS is enabled in `metadata.json`:
   ```json
   { "server": { "cors": { "enabled": true } } }
   ```
2. Check `allowedOrigins` includes your SPA domain
3. Restart Honua server after metadata changes

### CORS Error: "Credentials flag is true, but Access-Control-Allow-Credentials is not true"

**Problem:** Using `credentials: 'include'` but `allowCredentials: false`.

**Solution:**
```json
{
  "server": {
    "cors": {
      "enabled": true,
      "allowedOrigins": ["https://app.example.com"],
      "allowCredentials": true
    }
  }
}
```

### CORS Error: "Origin '*' not allowed when credentials flag is true"

**Problem:** Using `allowedOrigins: ["*"]` with `allowCredentials: true`.

**Solution:** Specify exact origins:
```json
{
  "server": {
    "cors": {
      "allowedOrigins": ["https://app.example.com", "https://staging.example.com"]
    }
  }
}
```

### 401 Unauthorized

**Problem:** JWT token missing or expired.

**Solution:**
1. Check token exists: `localStorage.getItem('auth_token')`
2. Verify token is included in request headers:
   ```javascript
   headers: { 'Authorization': `Bearer ${token}` }
   ```
3. Implement token refresh logic

### OPTIONS Preflight Request Fails

**Problem:** Browser sends OPTIONS request before actual request, Honua rejects it.

**Solution:**
```json
{
  "server": {
    "cors": {
      "allowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "maxAge": 3600
    }
  }
}
```

### Wildcard Subdomain Not Working

**Problem:** `"https://*.example.com"` doesn't match `https://app.example.com`.

**Solution:** Ensure you're using Honua version with wildcard subdomain support (requires the fix in `MetadataCorsPolicyProvider.cs:55-70`).

---

## Best Practices

### Security

1. **Never use `allowedOrigins: ["*"]` in production**
2. **Always validate JWT tokens server-side**
3. **Use HTTPS in production** (required for secure cookies)
4. **Implement token refresh** to avoid expired token errors
5. **Use `httpOnly` cookies** for authentication if possible (protects against XSS)

### Performance

1. **Set `maxAge: 3600`** to cache preflight requests (reduces OPTIONS requests)
2. **Use CDN** (CloudFront, Azure Front Door) for SPA assets
3. **Cache GIS tiles aggressively** with Service Workers
4. **Use `outFields` parameter** to request only needed fields
5. **Implement pagination** for large feature queries

### Development Workflow

1. **Use `http://localhost:*` during development**
2. **Use wildcard subdomain for staging** (`https://*.staging.example.com`)
3. **Use specific origin in production** (`https://app.example.com`)
4. **Test with `curl -v`** to debug CORS headers:
   ```bash
   curl -v -H "Origin: https://app.example.com" \
     -H "Access-Control-Request-Method: GET" \
     -X OPTIONS \
     https://api.example.com/geoservices/rest/services/parcels/FeatureServer/0/query
   ```

---

## Next Steps

1. ✅ Add CORS to `metadata.json`
2. ✅ Test cross-origin requests from your SPA
3. ✅ Implement JWT Bearer authentication
4. ✅ Deploy with CloudFront/Azure Front Door for production
5. 📖 Read [SPA_CORS_STRATEGIES.md](./SPA_CORS_STRATEGIES.md) for advanced deployment patterns
6. 📖 Read [SPA_SUPPORT_STATUS.md](./SPA_SUPPORT_STATUS.md) for current implementation status

---

## Getting Help

If you encounter issues:

1. Check Honua logs for CORS errors
2. Use browser DevTools Network tab to inspect preflight requests
3. Test with `curl -v` to isolate browser vs server issues
4. Ask the Honua AI Consultant: `honua consult "Help me deploy my React app with CORS"`
