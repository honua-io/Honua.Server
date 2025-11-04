# Trusted Proxy Configuration Guide

## Overview

This guide explains how to securely configure trusted proxies to prevent **Host Header Injection** and **X-Forwarded-For spoofing attacks** (Security Issue #8).

---

## The Problem: Header Injection Attacks

Without proper validation, attackers can inject malicious headers to:

1. **Bypass rate limiting**: Set `X-Forwarded-For` to random IPs
2. **Evade security logs**: Hide their real IP address
3. **Abuse IP-based authentication**: Impersonate trusted IPs
4. **Cache poisoning**: Manipulate `X-Forwarded-Host` headers
5. **SSRF attacks**: Trick the application into making malicious requests

**Example Attack**:

```bash
# Attacker sends request with forged header
curl -H "X-Forwarded-For: 192.168.1.1" https://api.honua.io/wfs

# Without validation, the application trusts this header and:
# - Logs 192.168.1.1 as the client IP (hiding attacker's real IP)
# - Applies rate limits to 192.168.1.1 (bypassing attacker's IP limit)
# - Grants access if 192.168.1.1 is allowlisted
```

---

## The Solution: TrustedProxyValidator

The `TrustedProxyValidator` class ensures headers are ONLY trusted when requests originate from configured proxies.

### How It Works

1. Checks if the connection's `RemoteIpAddress` is in the trusted proxy list
2. If NOT trusted → Ignores all `X-Forwarded-*` headers and logs a warning
3. If trusted → Safely extracts the client IP from `X-Forwarded-For`
4. Validates IP format to prevent injection

---

## Configuration

### Basic Configuration (Individual IPs)

**appsettings.json**:

```json
{
  "TrustedProxies": [
    "10.0.0.5",        // Load balancer
    "172.16.0.10",     // Reverse proxy
    "::1"              // IPv6 localhost
  ]
}
```

### Advanced Configuration (CIDR Networks)

For cloud environments where proxy IPs may change:

```json
{
  "TrustedProxies": [
    "10.0.0.5"
  ],
  "TrustedProxyNetworks": [
    "10.0.0.0/24",           // Private subnet
    "172.31.0.0/16",         // AWS VPC default range
    "2001:db8::/32"          // IPv6 network
  ]
}
```

### Cloud Provider Examples

#### AWS (Behind Application Load Balancer)

```json
{
  "TrustedProxyNetworks": [
    "10.0.0.0/16",           // Your VPC CIDR
    "172.31.0.0/16"          // Default VPC CIDR
  ]
}
```

To find your ALB's IP addresses:

```bash
aws ec2 describe-network-interfaces \
  --filters "Name=description,Values=ELB app/your-alb-name/*" \
  --query 'NetworkInterfaces[*].PrivateIpAddress'
```

#### Azure (Behind Application Gateway)

```json
{
  "TrustedProxies": [
    "10.1.0.4"               // Application Gateway IP
  ]
}
```

Get Application Gateway IP:

```bash
az network application-gateway show \
  --name honua-appgw \
  --resource-group honua-rg \
  --query 'frontendIPConfigurations[0].privateIPAddress'
```

#### Cloudflare

Cloudflare provides [published IP ranges](https://www.cloudflare.com/ips/):

```json
{
  "TrustedProxyNetworks": [
    "173.245.48.0/20",
    "103.21.244.0/22",
    "103.22.200.0/22",
    "103.31.4.0/22",
    "141.101.64.0/18",
    "108.162.192.0/18",
    "190.93.240.0/20",
    "188.114.96.0/20",
    "197.234.240.0/22",
    "198.41.128.0/17",
    "162.158.0.0/15",
    "104.16.0.0/13",
    "104.24.0.0/14",
    "172.64.0.0/13",
    "131.0.72.0/22"
  ]
}
```

#### NGINX Reverse Proxy

```json
{
  "TrustedProxies": [
    "10.0.1.100"             // NGINX server IP
  ]
}
```

**NGINX Configuration** (ensure it sets headers):

```nginx
location / {
    proxy_pass http://honua-backend;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-Host $host;
}
```

---

## Service Registration

The `TrustedProxyValidator` must be registered in your dependency injection container.

**Program.cs** or **Startup.cs**:

```csharp
using Honua.Server.Host.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Register TrustedProxyValidator as singleton
builder.Services.AddSingleton<TrustedProxyValidator>();

// ... other service registrations

var app = builder.Build();

// Middleware order is important - use this early in the pipeline
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

// Rest of middleware pipeline
app.UseHonuaRateLimiting(builder.Configuration);
```

### Integration with ASP.NET Core Forwarded Headers

For full protection, combine with `ForwardedHeadersOptions`:

```csharp
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Clear defaults to use our trusted proxy configuration
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();

    // Add trusted proxy IPs
    var trustedProxies = builder.Configuration.GetSection("TrustedProxies").Get<string[]>() ?? Array.Empty<string>();
    foreach (var proxyIp in trustedProxies)
    {
        if (IPAddress.TryParse(proxyIp, out var ipAddress))
        {
            options.KnownProxies.Add(ipAddress);
        }
    }

    // Add trusted proxy networks
    var trustedNetworks = builder.Configuration.GetSection("TrustedProxyNetworks").Get<string[]>() ?? Array.Empty<string>();
    foreach (var network in trustedNetworks)
    {
        var parts = network.Split('/');
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var baseAddress) && int.TryParse(parts[1], out var prefixLength))
        {
            options.KnownNetworks.Add(new IPNetwork(baseAddress, prefixLength));
        }
    }
});
```

---

## Security Testing

### Test 1: Direct Request (No Proxy)

```bash
# Should return your actual IP, ignoring X-Forwarded-For
curl -H "X-Forwarded-For: 1.2.3.4" https://api.honua.io/health

# Check logs - should see warning about untrusted header
```

### Test 2: Request from Trusted Proxy

```bash
# From the proxy server
curl -H "X-Forwarded-For: 203.0.113.42" http://localhost:5000/health

# Should return 203.0.113.42 as client IP
```

### Test 3: Rate Limiting Bypass Attempt

```bash
# Try to bypass rate limit with random IPs
for i in {1..100}; do
  curl -H "X-Forwarded-For: 10.0.0.$i" https://api.honua.io/wfs
done

# Should get rate limited on YOUR real IP, not the forged IPs
```

---

## Monitoring and Alerts

### Log Monitoring

Watch for these security events:

```bash
# Untrusted proxy attempts
grep "X-Forwarded-For header received from untrusted IP" /var/log/honua/security.log

# Invalid IP formats
grep "Invalid IP address in X-Forwarded-For" /var/log/honua/security.log
```

### Recommended Alerts

1. **High volume of untrusted headers**: Possible attack in progress
2. **Trusted proxy sending invalid IPs**: Misconfiguration or compromise
3. **No trusted proxies configured**: Accidental deployment without proxy config

**Example Prometheus Alert**:

```yaml
- alert: UntrustedProxyHeaders
  expr: rate(honua_untrusted_proxy_header_total[5m]) > 10
  for: 5m
  annotations:
    summary: "High rate of X-Forwarded-For headers from untrusted sources"
    description: "Possible header injection attack in progress"
```

---

## Common Mistakes

### ❌ Mistake 1: Trusting All Private IPs

```json
{
  "TrustedProxyNetworks": [
    "10.0.0.0/8",           // DON'T: Too broad
    "172.16.0.0/12",        // DON'T: Entire private range
    "192.168.0.0/16"        // DON'T: All private networks
  ]
}
```

**Problem**: Attackers on your network can forge headers.

**Solution**: Only trust specific proxy IPs or narrow subnets.

### ❌ Mistake 2: Not Configuring Proxies in Production

```json
{
  "TrustedProxies": []      // DON'T: Empty in production behind ALB/proxy
}
```

**Problem**: Application uses proxy IP instead of client IP, breaking rate limiting and logs.

**Solution**: Always configure trusted proxies when deployed behind a reverse proxy.

### ❌ Mistake 3: Using String Comparison

```csharp
// DON'T: Case-sensitive string comparison
if (remoteIp == "10.0.0.5") { }

// DO: Use IPAddress.TryParse and comparison
if (IPAddress.TryParse(remoteIp, out var ip) && trustedProxies.Contains(ip)) { }
```

---

## Incident Response

### If Headers Are Being Abused

1. **Identify the attack pattern** in logs
2. **Block the attacker's real IP** (not the forged X-Forwarded-For value)
3. **Verify trusted proxy configuration** is correct
4. **Check if proxy is compromised** (if attacks come from trusted proxy)
5. **Rotate proxy IPs** if necessary

### Example: Finding Real Attacker IP

```bash
# Attacker spoofs X-Forwarded-For
# Real IP is in RemoteIpAddress field
grep "untrusted IP" /var/log/honua/security.log | awk '{print $NF}' | sort | uniq -c | sort -rn
```

---

## References

- [OWASP: Host Header Injection](https://owasp.org/www-project-web-security-testing-guide/latest/4-Web_Application_Security_Testing/07-Input_Validation_Testing/17-Testing_for_Host_Header_Injection)
- [PortSwigger: HTTP Host Header Attacks](https://portswigger.net/web-security/host-header)
- [CWE-290: Authentication Bypass by Spoofing](https://cwe.mitre.org/data/definitions/290.html)
- [ASP.NET Core: Forwarded Headers Middleware](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)

---

**Last Updated**: 2025-01-23
**Security Level**: CRITICAL
**Affected Components**: Rate Limiting, Authentication, Logging, IP-based Access Control
