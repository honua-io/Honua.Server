// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Cli.Services.Consultant;

public interface IConsultantPlanFormatter
{
    void Render(ConsultantPlan plan, ConsultantRequest request);
}
