// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Processes.State;

/// <summary>
/// State object for Certificate Renewal Process workflow.
/// Tracks TLS/SSL certificate lifecycle management with Let's Encrypt and ACM.
/// Persists across step invocations for checkpointing and resume.
/// </summary>
public class CertificateRenewalState
{
    public string RenewalId { get; set; } = string.Empty;
    public List<string> DomainNames { get; set; } = new();
    public string CertificateProvider { get; set; } = "LetsEncrypt"; // LetsEncrypt, ACM, ZeroSSL
    public string ValidationMethod { get; set; } = "DNS-01"; // DNS-01, HTTP-01
    public List<CertificateInfo> ExpiringCertificates { get; set; } = new();
    public List<CertificateInfo> NewCertificates { get; set; } = new();
    public Dictionary<string, string> DnsChallengeRecords { get; set; } = new();
    public List<DeploymentTarget> UpdatedTargets { get; set; } = new();
    public Dictionary<string, DateTime> NewExpiryDates { get; set; } = new();
    public DateTime RenewalStartTime { get; set; }
    public DateTime? RenewalCompleteTime { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Scanning, Validating, Requesting, Issuing, Deploying, Complete, Failed
    public bool RollbackPerformed { get; set; }
    public List<string> RenewedDomains { get; set; } = new();
    public List<string> FailedDomains { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int CheckWindowDays { get; set; } = 30; // Default: check for certs expiring in next 30 days
}

/// <summary>
/// Information about a TLS/SSL certificate.
/// </summary>
public class CertificateInfo
{
    public string Domain { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public DateTime IssueDate { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public List<string> SubjectAlternativeNames { get; set; } = new();
    public string KeyType { get; set; } = "RSA"; // RSA, ECDSA
    public int KeySize { get; set; } = 2048;
    public string CertificatePath { get; set; } = string.Empty;
    public bool IsWildcard { get; set; }
}

/// <summary>
/// Target system where certificate needs to be deployed.
/// </summary>
public class DeploymentTarget
{
    public string TargetType { get; set; } = string.Empty; // LoadBalancer, IngressController, CDN, WebServer
    public string TargetName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool DeploymentSuccessful { get; set; }
    public DateTime? DeploymentTime { get; set; }
    public string? DeploymentError { get; set; }
}
