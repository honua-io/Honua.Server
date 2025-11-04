// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Agents;

public sealed class AgentCapabilityOptions
{
    public List<TaskTypeCapability> TaskTypes { get; set; } = AgentCapabilityDefaults.CreateDefaultTaskTypes();

    public List<AgentCapability> Agents { get; set; } = AgentCapabilityDefaults.CreateDefaultAgents();
}

public sealed class TaskTypeCapability
{
    public string Name { get; set; } = string.Empty;

    public List<string> Keywords { get; set; } = new();
}

public sealed class AgentCapability
{
    public string AgentName { get; set; } = string.Empty;

    public List<string> Specializations { get; set; } = new();

    public double Priority { get; set; } = 1.0;
}

internal static class AgentCapabilityDefaults
{
    public static List<TaskTypeCapability> CreateDefaultTaskTypes() => new()
    {
        new TaskTypeCapability
        {
            Name = "deployment",
            Keywords = new List<string>{ "deploy", "deployment", "infrastructure", "provision", "setup", "install", "terraform", "aws", "azure", "gcp", "cloud", "kubernetes", "k8s", "docker", "compose", "helm", "ec2", "rds", "s3", "create", "generate", "manifest" }
        },
        new TaskTypeCapability
        {
            Name = "security",
            Keywords = new List<string>{ "security", "hardening", "secure", "authentication", "authorization", "encrypt" }
        },
        new TaskTypeCapability
        {
            Name = "performance",
            Keywords = new List<string>{ "performance", "optimize", "slow", "speed", "cache", "index", "tune" }
        },
        new TaskTypeCapability
        {
            Name = "troubleshooting",
            Keywords = new List<string>{ "error", "issue", "problem", "debug", "troubleshoot", "fix", "broken" }
        },
        new TaskTypeCapability
        {
            Name = "migration",
            Keywords = new List<string>{ "migrate", "migration", "import", "transfer", "move", "upgrade" }
        },
        new TaskTypeCapability
        {
            Name = "configuration",
            Keywords = new List<string>{ "configure", "config", "setting", "parameter", "customize" }
        }
    };

    public static List<AgentCapability> CreateDefaultAgents() => new()
    {
        new AgentCapability
        {
            AgentName = "DeploymentConfigurationAgent",
            Specializations = new List<string>{ "deployment", "configuration" }
        },
        new AgentCapability
        {
            AgentName = "SecurityHardeningAgent",
            Specializations = new List<string>{ "security" }
        },
        new AgentCapability
        {
            AgentName = "PerformanceOptimizationAgent",
            Specializations = new List<string>{ "performance" }
        },
        new AgentCapability
        {
            AgentName = "TroubleshootingAgent",
            Specializations = new List<string>{ "troubleshooting" }
        },
        new AgentCapability
        {
            AgentName = "MigrationImportAgent",
            Specializations = new List<string>{ "migration" }
        },
        new AgentCapability
        {
            AgentName = "HonuaUpgradeAgent",
            Specializations = new List<string>{ "deployment", "migration" }
        }
    };
}
