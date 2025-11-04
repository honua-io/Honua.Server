// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;

namespace Honua.Cli.Services.Consultant;

public sealed record ConsultantRequest(
    string? Prompt,
    bool DryRun,
    bool AutoApprove,
    bool SuppressLogging,
    string WorkspacePath,
    bool Verbose = false,
    ConsultantExecutionMode Mode = ConsultantExecutionMode.Auto,
    bool TrustHighConfidence = false,
    ConsultantPlan? PreviousPlan = null,
    List<string>? ConversationHistory = null);
