// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.Metadata;

namespace Honua.Cli.Services.Consultant;

public sealed class BootstrapConsultantPlanner : IConsultantPlanner
{
    private readonly ISystemClock _clock;

    public BootstrapConsultantPlanner(ISystemClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ConsultantPlan> CreatePlanAsync(ConsultantPlanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var request = context.Request;

        var steps = new List<ConsultantPlanStep>
        {
            new(
                "SafetySkill",
                "CreateSnapshot",
                new Dictionary<string, string>
                {
                    ["label"] = $"consultant-{_clock.UtcNow:yyyyMMddHHmmss}",
                    ["workspace"] = request.WorkspacePath
                },
                "Capture a reversible snapshot before executing the plan.",
                Category: "safety",
                Rationale: "Ensure the deployment can be rolled back if subsequent automation fails.",
                SuccessCriteria: "Snapshot stored and referenced in change log."),
            new(
                "SafetySkill",
                "RunPreflightValidation",
                new Dictionary<string, string>
                {
                    ["workspace"] = request.WorkspacePath
                },
                "Validate metadata and health probes prior to execution.",
                Category: "validation",
                Rationale: "Surface blocking issues before infrastructure is mutated.",
                SuccessCriteria: "Preflight report shows no blockers.")
        };

        if (request.Prompt?.Contains("postgis", StringComparison.OrdinalIgnoreCase) == true)
        {
            steps.Add(new ConsultantPlanStep(
                "DataSourceSkill",
                "ConnectPostgis",
                new Dictionary<string, string>
                {
                    ["workspace"] = request.WorkspacePath,
                    ["dryRun"] = request.DryRun ? "true" : "false"
                },
                "Provision or update the PostGIS datasource connection.",
                Category: "database",
                Rationale: "Honua services require a healthy transactional store.",
                SuccessCriteria: "PostGIS connection validated and extensions enabled."));
        }
        else if (request.Prompt?.Contains("sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            steps.Add(new ConsultantPlanStep(
                "DataSourceSkill",
                "ConnectSpatialite",
                new Dictionary<string, string>
                {
                    ["workspace"] = request.WorkspacePath,
                    ["dryRun"] = request.DryRun ? "true" : "false"
                },
                "Provision or update the SpatiaLite datasource connection.",
                Category: "database",
                Rationale: "Ensure lightweight deployments still have a spatial-capable catalog.",
                SuccessCriteria: "SpatiaLite file accessible and schema migrated."));
        }
        else
        {
            steps.Add(new ConsultantPlanStep(
                "DocumentationSkill",
                "ShowBlueprintOptions",
                new Dictionary<string, string>
                {
                    ["topic"] = "provisioning"
                },
                "Surface relevant runbooks when no datasource was detected in the prompt.",
                Category: "documentation",
                Rationale: "Provide next best actions when intent is ambiguous.",
                SuccessCriteria: "Operator selects an appropriate blueprint."));
        }

        steps.Add(new ConsultantPlanStep(
            "DeploymentSkill",
            request.DryRun ? "SelectDeploymentBlueprint" : "RunDeployment",
            new Dictionary<string, string>
            {
                ["mode"] = request.DryRun ? "plan" : "apply"
            },
            request.DryRun
                ? "Produce a deployment plan without applying changes."
                : "Apply the selected deployment blueprint and stream progress.",
            Category: "deployment",
            Rationale: "Drive the desired change once prerequisites are satisfied.",
            SuccessCriteria: request.DryRun ? "Plan artifacts generated" : "Deployment tasks complete without critical errors."));

        return Task.FromResult(new ConsultantPlan(steps));
    }
}
