// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// This file provides backwards compatibility by re-exporting ResiliencePolicies from Honua.Server.Core.
// The ResiliencePolicies class has been moved to Honua.Server.Core.Resilience to avoid circular dependencies.
// Update your using statements to: using Honua.Server.Core.Resilience;

[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(Honua.Server.Core.Resilience.ResiliencePolicies))]
