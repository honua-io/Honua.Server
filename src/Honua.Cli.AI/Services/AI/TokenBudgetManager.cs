// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Manages token budgets for LLM requests to control costs.
/// </summary>
/// <remarks>
/// Tracks token usage at multiple levels:
/// - Per request (prevent single massive request)
/// - Per user/session (prevent abuse)
/// - Per day (global budget)
///
/// Token estimation:
/// - Uses approximation: 1 token ≈ 4 characters for English text
/// - For accurate counting, integrate tiktoken or similar library
///
/// Cost tracking:
/// - Tracks input and output tokens separately
/// - Calculates estimated costs based on provider pricing
/// </remarks>
public sealed class TokenBudgetManager
{
    private readonly ILogger<TokenBudgetManager> _logger;
    private readonly TokenBudgetOptions _options;
    private readonly ConcurrentDictionary<string, UserTokenUsage> _userUsage = new();
    private long _dailyTokensUsed = 0;
    private DateTime _dailyResetTime = DateTime.UtcNow.Date.AddDays(1);

    public TokenBudgetManager(
        ILogger<TokenBudgetManager> logger,
        TokenBudgetOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new TokenBudgetOptions();
    }

    /// <summary>
    /// Checks if a request can proceed within budget limits.
    /// </summary>
    /// <param name="userId">User or session identifier</param>
    /// <param name="systemPrompt">System prompt text</param>
    /// <param name="userPrompt">User prompt text</param>
    /// <param name="estimatedResponse">Estimated response tokens</param>
    /// <returns>Budget check result with details</returns>
    public BudgetCheckResult CanProcessRequest(
        string userId,
        string systemPrompt,
        string userPrompt,
        int estimatedResponse = 500)
    {
        CheckAndResetDaily();

        var inputTokens = EstimateTokens(systemPrompt) + EstimateTokens(userPrompt);
        var totalRequestTokens = inputTokens + estimatedResponse;

        // Check per-request limit
        if (totalRequestTokens > _options.MaxTokensPerRequest)
        {
            _logger.LogWarning(
                "Request exceeds per-request token limit. Estimated: {Tokens}, Limit: {Limit}",
                totalRequestTokens, _options.MaxTokensPerRequest);

            return BudgetCheckResult.Denied(
                $"Request would use {totalRequestTokens} tokens, exceeding limit of {_options.MaxTokensPerRequest}",
                estimatedTokens: totalRequestTokens);
        }

        // Check user limit
        var userUsage = _userUsage.GetOrAdd(userId, _ => new UserTokenUsage());
        var userTotal = userUsage.TotalTokens + totalRequestTokens;

        if (userTotal > _options.MaxTokensPerUser)
        {
            _logger.LogWarning(
                "User {UserId} would exceed token limit. Current: {Current}, Requested: {Requested}, Limit: {Limit}",
                userId, userUsage.TotalTokens, totalRequestTokens, _options.MaxTokensPerUser);

            return BudgetCheckResult.Denied(
                $"User has used {userUsage.TotalTokens} tokens. Request would exceed daily limit of {_options.MaxTokensPerUser}",
                estimatedTokens: totalRequestTokens,
                userId: userId,
                currentUserUsage: userUsage.TotalTokens);
        }

        // Check daily global limit
        var dailyTotal = Interlocked.Read(ref _dailyTokensUsed) + totalRequestTokens;

        if (dailyTotal > _options.MaxTokensPerDay)
        {
            _logger.LogWarning(
                "Daily token limit would be exceeded. Current: {Current}, Requested: {Requested}, Limit: {Limit}",
                _dailyTokensUsed, totalRequestTokens, _options.MaxTokensPerDay);

            return BudgetCheckResult.Denied(
                $"Daily token limit of {_options.MaxTokensPerDay} would be exceeded",
                estimatedTokens: totalRequestTokens,
                currentDailyUsage: _dailyTokensUsed);
        }

        _logger.LogDebug(
            "Request approved. User: {UserId}, Request tokens: {Tokens}, User total: {UserTotal}, Daily total: {DailyTotal}",
            userId, totalRequestTokens, userTotal, dailyTotal);

        return BudgetCheckResult.CreateApproved(
            estimatedTokens: totalRequestTokens,
            userId: userId,
            currentUserUsage: userUsage.TotalTokens,
            currentDailyUsage: _dailyTokensUsed);
    }

    /// <summary>
    /// Records actual token usage after an LLM request completes.
    /// </summary>
    public void TrackUsage(
        string userId,
        string provider,
        string model,
        int inputTokens,
        int outputTokens)
    {
        var totalTokens = inputTokens + outputTokens;
        var cost = CalculateCost(provider, model, inputTokens, outputTokens);

        // Update user usage
        var userUsage = _userUsage.GetOrAdd(userId, _ => new UserTokenUsage());
        userUsage.TotalTokens += totalTokens;
        userUsage.TotalCost += cost;
        userUsage.RequestCount++;
        userUsage.LastRequestTime = DateTime.UtcNow;

        // Update daily usage
        Interlocked.Add(ref _dailyTokensUsed, totalTokens);

        _logger.LogInformation(
            "LLM usage tracked. Provider: {Provider}, Model: {Model}, User: {UserId}, " +
            "Input: {InputTokens}t, Output: {OutputTokens}t, Cost: ${Cost:F4}",
            provider, model, userId, inputTokens, outputTokens, cost);

        // Log warning if approaching limits
        if (userUsage.TotalTokens > _options.MaxTokensPerUser * 0.8)
        {
            _logger.LogWarning(
                "User {UserId} has used {Percentage:P0} of their daily token budget ({Used}/{Limit})",
                userId, userUsage.TotalTokens / (double)_options.MaxTokensPerUser,
                userUsage.TotalTokens, _options.MaxTokensPerUser);
        }

        if (_dailyTokensUsed > _options.MaxTokensPerDay * 0.8)
        {
            _logger.LogWarning(
                "Daily token usage is at {Percentage:P0} of budget ({Used}/{Limit})",
                _dailyTokensUsed / (double)_options.MaxTokensPerDay,
                _dailyTokensUsed, _options.MaxTokensPerDay);
        }
    }

    /// <summary>
    /// Gets usage statistics for a user.
    /// </summary>
    public UserTokenUsage? GetUserUsage(string userId)
    {
        return _userUsage.TryGetValue(userId, out var usage) ? usage : null;
    }

    /// <summary>
    /// Gets daily usage statistics.
    /// </summary>
    public DailyUsageStats GetDailyStats()
    {
        CheckAndResetDaily();

        return new DailyUsageStats
        {
            TotalTokens = _dailyTokensUsed,
            TotalUsers = _userUsage.Count,
            ResetTime = _dailyResetTime,
            TopUsers = _userUsage
                .OrderByDescending(kvp => kvp.Value.TotalTokens)
                .Take(10)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    /// <summary>
    /// Resets user budget for a specific user (admin operation).
    /// </summary>
    public void ResetUserBudget(string userId)
    {
        if (_userUsage.TryRemove(userId, out var removed))
        {
            _logger.LogInformation(
                "Reset budget for user {UserId}. Previous usage: {Tokens} tokens, ${Cost:F4}",
                userId, removed.TotalTokens, removed.TotalCost);
        }
    }

    private void CheckAndResetDaily()
    {
        if (DateTime.UtcNow >= _dailyResetTime)
        {
            var previousUsage = Interlocked.Exchange(ref _dailyTokensUsed, 0);
            _dailyResetTime = DateTime.UtcNow.Date.AddDays(1);
            _userUsage.Clear();

            _logger.LogInformation(
                "Daily token budget reset. Previous day usage: {Tokens} tokens",
                previousUsage);
        }
    }

    /// <summary>
    /// Estimates token count from text.
    /// </summary>
    /// <remarks>
    /// Uses approximation: 1 token ≈ 4 characters for English text.
    /// For accurate counting, integrate tiktoken library.
    /// </remarks>
    private static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        // Rough approximation: 1 token ≈ 4 characters
        // This is conservative for English text
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    /// <summary>
    /// Calculates cost based on provider pricing.
    /// </summary>
    private static decimal CalculateCost(string provider, string model, int inputTokens, int outputTokens)
    {
        var pricing = GetPricing(provider, model);
        return (inputTokens * pricing.InputCostPerToken) + (outputTokens * pricing.OutputCostPerToken);
    }

    private static (decimal InputCostPerToken, decimal OutputCostPerToken) GetPricing(string provider, string model)
    {
        // Pricing as of 2025 ($/1K tokens)
        var lowerProvider = provider.ToLowerInvariant();
        var lowerModel = model.ToLowerInvariant();

        if (lowerProvider.Contains("openai") || lowerProvider.Contains("azure"))
        {
            if (lowerModel.Contains("gpt-4-turbo") || lowerModel.Contains("gpt-4o"))
            {
                return (0.01m / 1000, 0.03m / 1000); // $0.01 input, $0.03 output per 1K tokens
            }
            if (lowerModel.Contains("gpt-4"))
            {
                return (0.03m / 1000, 0.06m / 1000); // $0.03 input, $0.06 output
            }
            if (lowerModel.Contains("gpt-3.5-turbo"))
            {
                return (0.0005m / 1000, 0.0015m / 1000); // $0.0005 input, $0.0015 output
            }
        }
        else if (lowerProvider.Contains("anthropic"))
        {
            if (lowerModel.Contains("claude-3-opus"))
            {
                return (0.015m / 1000, 0.075m / 1000); // $0.015 input, $0.075 output
            }
            if (lowerModel.Contains("claude-3-sonnet"))
            {
                return (0.003m / 1000, 0.015m / 1000); // $0.003 input, $0.015 output
            }
            if (lowerModel.Contains("claude-3-haiku"))
            {
                return (0.00025m / 1000, 0.00125m / 1000); // $0.00025 input, $0.00125 output
            }
        }

        // Default conservative estimate
        return (0.01m / 1000, 0.03m / 1000);
    }
}

/// <summary>
/// Configuration options for token budget management.
/// </summary>
public sealed class TokenBudgetOptions
{
    /// <summary>
    /// Maximum tokens allowed per single request.
    /// Default: 10,000 tokens (~7,500 words)
    /// </summary>
    public int MaxTokensPerRequest { get; init; } = 10_000;

    /// <summary>
    /// Maximum tokens allowed per user per day.
    /// Default: 100,000 tokens (~75,000 words)
    /// </summary>
    public int MaxTokensPerUser { get; init; } = 100_000;

    /// <summary>
    /// Maximum tokens allowed globally per day.
    /// Default: 1,000,000 tokens (~$30/day for GPT-4o)
    /// </summary>
    public int MaxTokensPerDay { get; init; } = 1_000_000;
}

/// <summary>
/// Result of a budget check.
/// </summary>
public sealed class BudgetCheckResult
{
    public bool Approved { get; init; }
    public string? DenialReason { get; init; }
    public int EstimatedTokens { get; init; }
    public string? UserId { get; init; }
    public long CurrentUserUsage { get; init; }
    public long CurrentDailyUsage { get; init; }

    public static BudgetCheckResult CreateApproved(
        int estimatedTokens,
        string? userId = null,
        long currentUserUsage = 0,
        long currentDailyUsage = 0)
    {
        return new BudgetCheckResult
        {
            Approved = true,
            EstimatedTokens = estimatedTokens,
            UserId = userId,
            CurrentUserUsage = currentUserUsage,
            CurrentDailyUsage = currentDailyUsage
        };
    }

    public static BudgetCheckResult Denied(
        string reason,
        int estimatedTokens = 0,
        string? userId = null,
        long currentUserUsage = 0,
        long currentDailyUsage = 0)
    {
        return new BudgetCheckResult
        {
            Approved = false,
            DenialReason = reason,
            EstimatedTokens = estimatedTokens,
            UserId = userId,
            CurrentUserUsage = currentUserUsage,
            CurrentDailyUsage = currentDailyUsage
        };
    }
}

/// <summary>
/// Tracks token usage for a user.
/// </summary>
public sealed class UserTokenUsage
{
    public long TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public int RequestCount { get; set; }
    public DateTime LastRequestTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Daily usage statistics.
/// </summary>
public sealed class DailyUsageStats
{
    public long TotalTokens { get; init; }
    public int TotalUsers { get; init; }
    public DateTime ResetTime { get; init; }
    public Dictionary<string, UserTokenUsage> TopUsers { get; init; } = new();
}
