// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Represents a guardrail violation detected during validation.
/// </summary>
public sealed record GuardrailViolation(string Field, string Message);
