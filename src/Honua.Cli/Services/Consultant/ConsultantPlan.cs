// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;

namespace Honua.Cli.Services.Consultant;

public sealed record ConsultantPlan(
    IReadOnlyList<ConsultantPlanStep> Steps,
    string? ExecutiveSummary = null,
    string? Confidence = null,
    IReadOnlyList<ConsultantObservation>? HighlightedObservations = null,
    IReadOnlyList<string>? RecommendedPatternIds = null)
{
    public static ConsultantPlan Empty { get; } = new ConsultantPlan(
        Array.Empty<ConsultantPlanStep>(),
        ExecutiveSummary: null,
        Confidence: null,
        HighlightedObservations: Array.Empty<ConsultantObservation>(),
        RecommendedPatternIds: Array.Empty<string>());
}

public sealed record ConsultantPlanStep(
    string Skill,
    string Action,
    IReadOnlyDictionary<string, string> Inputs,
    string? Description = null,
    string? Category = null,
    string? Rationale = null,
    string? SuccessCriteria = null,
    string? Risk = null,
    IReadOnlyList<string>? Dependencies = null);
