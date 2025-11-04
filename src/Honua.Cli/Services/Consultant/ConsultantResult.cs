// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Cli.Services.Consultant;

public sealed record ConsultantResult(
    bool Success,
    string Summary,
    ConsultantPlan Plan,
    bool Approved,
    bool Executed);
