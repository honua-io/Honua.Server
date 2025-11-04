// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using Spectre.Console;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant;

public sealed class TableConsultantPlanFormatter : IConsultantPlanFormatter
{
    private readonly IAnsiConsole _console;

    public TableConsultantPlanFormatter(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public void Render(ConsultantPlan plan, ConsultantRequest request)
    {
        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Step");
        table.AddColumn("Skill");
        table.AddColumn("Action");
        table.AddColumn("Category");
        table.AddColumn("Inputs");
        table.AddColumn("Intent / Success Criteria");
        table.AddColumn("Risk");

        var index = 1;
        foreach (var step in plan.Steps)
        {
            var inputs = step.Inputs.Count == 0
                ? "—"
                : string.Join("\n", step.Inputs.Select(pair => $"{pair.Key}: {pair.Value}"));

            var successCriteria = step.SuccessCriteria.IsNullOrWhiteSpace()
                ? step.Description ?? "—"
                : step.SuccessCriteria;

            table.AddRow(
                index.ToString(),
                step.Skill,
                step.Action,
                step.Category ?? "—",
                inputs,
                successCriteria.IsNullOrWhiteSpace() ? "—" : successCriteria,
                step.Risk.IsNullOrWhiteSpace() ? "—" : step.Risk);

            index++;
        }

        var title = request.DryRun ? "Planned Steps (Dry Run)" : "Planned Steps";
        table.Title = new TableTitle(title);

        _console.Write(table);

        if (plan.ExecutiveSummary.HasValue())
        {
            _console.WriteLine();
            _console.MarkupLine("[bold]Executive summary[/]:");
            _console.WriteLine(plan.ExecutiveSummary);
        }

        if (plan.Confidence.HasValue())
        {
            _console.MarkupLine($"Confidence: {plan.Confidence}");
        }
    }
}
