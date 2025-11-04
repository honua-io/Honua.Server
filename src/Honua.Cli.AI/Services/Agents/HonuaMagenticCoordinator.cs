// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#pragma warning disable SKEXP0110 // Suppress experimental API warnings for SK Agent Framework

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Configuration;
using Honua.Cli.AI.Services.Guards;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.Services.Processes.Steps.Upgrade;
using Honua.Cli.AI.Services.Processes.Steps.Metadata;
using Honua.Cli.AI.Services.Processes.Steps.GitOps;
using Honua.Cli.AI.Services.Processes.Steps.Benchmark;
using Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// GroupChat-based agent coordinator for Honua's 28 specialized agents.
/// Uses SK's GroupChatOrchestration with intelligent manager for dynamic multi-agent orchestration.
/// </summary>
public sealed class HonuaMagenticCoordinator : IAgentCoordinator
{
    private readonly HonuaAgentFactory _agentFactory;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly IInputGuard _inputGuard;
    private readonly IOutputGuard _outputGuard;
    private readonly ILogger<HonuaMagenticCoordinator> _logger;
    private readonly AgentActivitySource _agentActivitySource;
    private readonly ParameterExtractionService _parameterExtraction;
    private readonly IProcessStateStore _processStateStore;
    private readonly IAgentSelectionService _agentSelectionService;
    private readonly AgentSelectionOptions _agentSelectionOptions;

    // Magentic orchestration components
    private readonly Agent[] _allAgents;

    // Session state
    private readonly AgentInteractionHistory _history;

    public HonuaMagenticCoordinator(
        HonuaAgentFactory agentFactory,
        Kernel kernel,
        IChatCompletionService chatCompletion,
        IInputGuard inputGuard,
        IOutputGuard outputGuard,
        ILogger<HonuaMagenticCoordinator> logger,
        AgentActivitySource agentActivitySource,
        ParameterExtractionService parameterExtraction,
        IProcessStateStore processStateStore,
        IAgentSelectionService agentSelectionService,
        Microsoft.Extensions.Options.IOptions<AgentSelectionOptions> agentSelectionOptions)
    {
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
        _inputGuard = inputGuard ?? throw new ArgumentNullException(nameof(inputGuard));
        _outputGuard = outputGuard ?? throw new ArgumentNullException(nameof(outputGuard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentActivitySource = agentActivitySource ?? throw new ArgumentNullException(nameof(agentActivitySource));
        _parameterExtraction = parameterExtraction ?? throw new ArgumentNullException(nameof(parameterExtraction));
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
        _agentSelectionService = agentSelectionService ?? throw new ArgumentNullException(nameof(agentSelectionService));
        _agentSelectionOptions = agentSelectionOptions?.Value ?? throw new ArgumentNullException(nameof(agentSelectionOptions));

        // Create all 28 specialized agents
        _allAgents = _agentFactory.CreateAllAgents();

        // Initialize session history
        _history = new AgentInteractionHistory
        {
            SessionId = Guid.NewGuid().ToString(),
            Interactions = new List<AgentInteraction>()
        };

        _logger.LogInformation(
            "HonuaMagenticCoordinator initialized with {AgentCount} specialized agents. Intelligent selection: {IntelligentSelection}",
            _allAgents.Length,
            _agentSelectionOptions.EnableIntelligentSelection);
    }

    /// <summary>
    /// Process a user request using GroupChat orchestration.
    /// The HonuaGroupChatManager dynamically selects which of the 28 agents to invoke.
    /// </summary>
    public async Task<AgentCoordinatorResult> ProcessRequestAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = _agentActivitySource.StartOrchestration(request, _allAgents.Length);
        activity?.SetTag("session.id", context.SessionId);
        activity?.SetTag("dry_run", context.DryRun);

        var startTime = DateTime.UtcNow;
        var steps = new List<AgentStepResult>();
        var agentsInvolved = new List<string>();

        try
        {
            // Step 1: Input Guard (check for malicious prompts)
            _logger.LogDebug("Running input guard on user request");
            var inputGuardResult = await _inputGuard.ValidateInputAsync(
                request,
                context.SessionId,
                cancellationToken);

            if (!inputGuardResult.IsSafe)
            {
                activity?.SetTag("guard.blocked", true);
                activity?.SetTag("guard.reason", inputGuardResult.Explanation);

                _logger.LogWarning("Input guard blocked request: {Reason}", inputGuardResult.Explanation);
                return new AgentCoordinatorResult
                {
                    Success = false,
                    ErrorMessage = $"Request blocked by input guard: {inputGuardResult.Explanation}",
                    Response = "Your request contains potentially unsafe content and cannot be processed. Please rephrase your request."
                };
            }

            // Step 2: Check if this is a long-running workflow that requires a process
            var processResult = await TryStartProcessAsync(request, context, cancellationToken);
            if (processResult != null)
            {
                if (processResult.ProcessId != null)
                {
                    _logger.LogInformation("Started long-running process {ProcessId} for request", processResult.ProcessId);
                }
                return processResult;
            }

            // Step 3: Intelligent agent selection
            _logger.LogDebug("Selecting relevant agents for request using intelligent selection");
            var selectedAgents = await _agentSelectionService.SelectRelevantAgentsAsync(
                request,
                _allAgents,
                _agentSelectionOptions.MaxAgentsPerRequest,
                cancellationToken);

            _logger.LogInformation(
                "Selected {SelectedCount} agents from {TotalCount} available: {AgentNames}",
                selectedAgents.Count,
                _allAgents.Length,
                string.Join(", ", selectedAgents.Select(a => a.Name)));

            activity?.SetTag("agents.selected_count", selectedAgents.Count);
            activity?.SetTag("agents.total_available", _allAgents.Length);
            activity?.SetTag("agents.reduction_percentage",
                ((double)(_allAgents.Length - selectedAgents.Count) / _allAgents.Length * 100).ToString("F1"));

            // Step 4: Create agent group chat with selected agents
            var chat = new AgentGroupChat(selectedAgents.ToArray());

            // Step 5: Create response callback to track agent interactions
            ChatHistory responseHistory = [];
            ValueTask ResponseCallback(ChatMessageContent response)
            {
                responseHistory.Add(response);

                // Track which agent responded
                if (response.AuthorName != null)
                {
                    agentsInvolved.Add(response.AuthorName);

                    _logger.LogDebug(
                        "Agent {AgentName} responded: {MessagePreview}",
                        response.AuthorName,
                        response.Content?.Substring(0, Math.Min(100, response.Content?.Length ?? 0)));

                    // Record agent step
                    steps.Add(new AgentStepResult
                    {
                        AgentName = response.AuthorName,
                        Action = "Process",
                        Success = true,
                        Message = response.Content ?? string.Empty,
                        Duration = TimeSpan.Zero  // SK doesn't expose per-agent timing
                    });
                }

                return ValueTask.CompletedTask;
            }

            // Step 6: Enrich request with context
            var enrichedRequest = EnrichRequestWithContext(request, context);

            // Step 7: Invoke agent group chat
            _logger.LogInformation("Invoking AgentGroupChat for request: {RequestPreview}",
                request.Substring(0, Math.Min(100, request.Length)));

            using var orchestrationActivity = _agentActivitySource.activitySource.StartActivity("AgentGroupChat");
            orchestrationActivity?.SetTag("max_iterations", 20);

            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, enrichedRequest));
            await foreach (var response in chat.InvokeAsync(cancellationToken))
            {
                await ResponseCallback(response);
            }

            var finalResponse = ExtractFinalResponse(responseHistory);

            // Step 8: Output Guard (check for hallucinations, dangerous operations)
            _logger.LogDebug("Running output guard on agent response");
            var outputGuardResult = await _outputGuard.ValidateOutputAsync(
                finalResponse,
                "GroupChatOrchestration",
                request,
                cancellationToken);

            if (!outputGuardResult.IsSafe)
            {
                activity?.SetTag("output_guard.blocked", true);
                activity?.SetTag("output_guard.reason", outputGuardResult.Explanation);

                _logger.LogWarning("Output guard blocked response: {Reason}", outputGuardResult.Explanation);
                return new AgentCoordinatorResult
                {
                    Success = false,
                    ErrorMessage = $"Response blocked by output guard: {outputGuardResult.Explanation}",
                    Response = "The generated response contains potentially unsafe operations and cannot be provided. Please try rephrasing your request.",
                    AgentsInvolved = agentsInvolved.Distinct().ToList(),
                    Steps = steps
                };
            }

            // Step 9: Build result
            var duration = DateTime.UtcNow - startTime;
            activity?.SetTag("duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("agents_invoked", agentsInvolved.Distinct().Count());

            var result = new AgentCoordinatorResult
            {
                Success = true,
                Response = finalResponse,
                AgentsInvolved = agentsInvolved.Distinct().ToList(),
                Steps = steps,
                Warnings = outputGuardResult.DetectedIssues.ToList()
            };

            // Record interaction in history
            _history.Interactions.Add(new AgentInteraction
            {
                Timestamp = DateTime.UtcNow,
                UserRequest = request,
                AgentsUsed = result.AgentsInvolved,
                Success = true,
                Response = finalResponse
            });

            _logger.LogInformation(
                "Request processed successfully in {Duration}ms using {AgentCount} agents: {Agents}",
                duration.TotalMilliseconds,
                result.AgentsInvolved.Count,
                string.Join(", ", result.AgentsInvolved));

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);

            _logger.LogError(ex, "Error processing request with GroupChat orchestration");

            var failedInteraction = new AgentInteraction
            {
                Timestamp = DateTime.UtcNow,
                UserRequest = request,
                AgentsUsed = agentsInvolved.Distinct().ToList(),
                Success = false,
                Response = $"Error: {ex.Message}"
            };
            _history.Interactions.Add(failedInteraction);

            return new AgentCoordinatorResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Response = $"An error occurred while processing your request: {ex.Message}",
                AgentsInvolved = agentsInvolved.Distinct().ToList(),
                Steps = steps
            };
        }
    }

    /// <summary>
    /// Gets the agent coordination history for the current session.
    /// </summary>
    public Task<AgentInteractionHistory> GetHistoryAsync()
    {
        return Task.FromResult(_history);
    }

    /// <summary>
    /// Enriches the user request with execution context for better agent decision-making.
    /// </summary>
    private string EnrichRequestWithContext(string request, AgentExecutionContext context)
    {
        var sb = new StringBuilder();

        // Core request
        sb.AppendLine(request);
        sb.AppendLine();

        // Context information
        sb.AppendLine("**Execution Context:**");
        sb.AppendLine($"- Workspace: {context.WorkspacePath}");
        sb.AppendLine($"- Dry Run: {context.DryRun}");
        sb.AppendLine($"- Require Approval: {context.RequireApproval}");

        if (context.ConversationHistory.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**Previous Conversation:**");
            foreach (var previous in context.ConversationHistory.TakeLast(3))
            {
                sb.AppendLine($"- {previous}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extracts the final response from the Magentic orchestration ChatHistory.
    /// </summary>
    private string ExtractFinalResponse(ChatHistory responseHistory)
    {
        if (responseHistory.Count == 0)
        {
            return "No response generated by agents.";
        }

        // Combine all agent responses into a single coherent response
        var sb = new StringBuilder();
        foreach (var message in responseHistory)
        {
            if (message.Content.HasValue())
            {
                sb.AppendLine(message.Content);
                sb.AppendLine();
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Attempts to start a long-running process if the request matches a workflow pattern.
    /// Returns null if no process is needed.
    /// </summary>
    private async Task<AgentCoordinatorResult?> TryStartProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Detect workflow type from request
        var workflowType = DetectWorkflowType(request);
        if (workflowType == null)
        {
            return null; // Not a long-running workflow
        }

        _logger.LogInformation("Detected long-running workflow: {WorkflowType}", workflowType);

        try
        {
            // Get process builder
            var processBuilder = workflowType switch
            {
                "deployment" => DeploymentProcess.BuildProcess(),
                "upgrade" => UpgradeProcess.BuildProcess(),
                "metadata" => MetadataProcess.BuildProcess(),
                "gitops" => GitOpsProcess.BuildProcess(),
                "benchmark" => BenchmarkProcess.BuildProcess(),
                "network-diagnostics" => NetworkDiagnosticsProcess.BuildProcess(),
                "certificate-renewal" => CertificateRenewalProcess.BuildProcess(),
                _ => null
            };

            if (processBuilder == null)
            {
                _logger.LogWarning("Failed to create process builder for workflow type: {WorkflowType}", workflowType);
                return null;
            }

            // Create initial event using LLM-based parameter extraction
            var initialEvent = workflowType switch
            {
                "deployment" => await CreateDeploymentEventAsync(request, context, cancellationToken),
                "upgrade" => await CreateUpgradeEventAsync(request, context, cancellationToken),
                "metadata" => await CreateMetadataEventAsync(request, context, cancellationToken),
                "gitops" => await CreateGitOpsEventAsync(request, context, cancellationToken),
                "benchmark" => await CreateBenchmarkEventAsync(request, context, cancellationToken),
                "certificate-renewal" => await CreateCertificateRenewalEventAsync(request, context, cancellationToken),
                "network-diagnostics" => await CreateNetworkDiagnosticsEventAsync(request, context, cancellationToken),
                _ => null
            };

            if (initialEvent == null)
            {
                _logger.LogWarning("Failed to create initial event for workflow type: {WorkflowType}", workflowType);
                return null;
            }

            // Build the process
            var process = processBuilder.Build();

            // Generate process ID before starting
            var processId = Guid.NewGuid().ToString();

            // Start the process with the kernel and initial event
            _logger.LogInformation("Starting process {ProcessId} for workflow {WorkflowType}", processId, workflowType);

            // Track process start
            var processInfo = new ProcessInfo
            {
                ProcessId = processId,
                WorkflowType = workflowType,
                Status = "Running",
                StartTime = DateTime.UtcNow,
                CurrentStep = "Initializing"
            };
            await _processStateStore.SaveProcessAsync(processInfo, cancellationToken);

            // Start the process execution using Local/InProcess runtime
            // SK 1.66.0-alpha uses: StartAsync(Kernel kernel, KernelProcessEvent initialEvent, CancellationToken cancellationToken = default)
            _logger.LogInformation("Starting process {ProcessId} for workflow {WorkflowType}", processId, workflowType);
            _logger.LogDebug("LLM-extracted parameters for {WorkflowType}: {Parameters}", workflowType, initialEvent.Data);

            // Run the process in the background so we don't block the response
            // Track process execution with telemetry and comprehensive error handling
            _ = Task.Run(async () =>
            {
                using var processActivity = _agentActivitySource.activitySource.StartActivity($"Process.{workflowType}");
                processActivity?.SetTag("process.id", processId);
                processActivity?.SetTag("process.workflow_type", workflowType);

                try
                {
                    _logger.LogDebug("Executing process {ProcessId} with initial event {EventId}", processId, initialEvent.Id);

                    // Start the process with the kernel and initial event (LocalRuntime)
                    // The LocalRuntime extension provides: StartAsync(Kernel kernel, KernelProcessEvent initialEvent)
                    // Returns LocalKernelProcessContext which manages the process execution
                    var processContext = await process.StartAsync(_kernel, initialEvent);

                    _logger.LogInformation("Process {ProcessId} execution completed successfully", processId);

                    // Update status to completed
                    await _processStateStore.UpdateProcessStatusAsync(
                        processId,
                        "Completed",
                        completionPercentage: 100,
                        cancellationToken: cancellationToken);

                    processActivity?.SetTag("process.status", "completed");
                    _logger.LogInformation("Process {ProcessId} completed successfully", processId);
                }
                catch (Exception ex)
                {
                    processActivity?.SetTag("error", true);
                    processActivity?.SetTag("error.type", ex.GetType().Name);
                    processActivity?.SetTag("error.message", ex.Message);
                    processActivity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);

                    _logger.LogError(ex, "Process {ProcessId} failed with error: {ErrorMessage}", processId, ex.Message);

                    try
                    {
                        await _processStateStore.UpdateProcessStatusAsync(
                            processId,
                            "Failed",
                            errorMessage: ex.Message,
                            cancellationToken: cancellationToken);
                    }
                    catch (Exception stateEx)
                    {
                        // If we can't update state, log it but don't throw
                        _logger.LogError(stateEx, "Failed to update process state for failed process {ProcessId}", processId);
                    }
                }
            }, cancellationToken);

            return new AgentCoordinatorResult
            {
                Success = true,
                Response = $"Started {workflowType} process (ID: {processId}). This is a long-running workflow. Use the process ID to track progress.",
                ProcessId = processId,
                AgentsInvolved = new List<string> { $"{workflowType}Process" },
                Steps = new List<AgentStepResult>
                {
                    new AgentStepResult
                    {
                        AgentName = $"{workflowType}Process",
                        Action = "StartProcess",
                        Success = true,
                        Message = $"Process {processId} started successfully",
                        Duration = TimeSpan.Zero
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process for workflow type: {WorkflowType}", workflowType);
            return new AgentCoordinatorResult
            {
                Success = false,
                ErrorMessage = $"Failed to start {workflowType} process: {ex.Message}",
                Response = $"An error occurred while starting the {workflowType} process. Please try again or contact support."
            };
        }
    }

    /// <summary>
    /// Detects if the user request requires a long-running workflow process.
    /// Returns the workflow type or null if no process is needed.
    /// </summary>
    private string? DetectWorkflowType(string request)
    {
        var requestLower = request.ToLowerInvariant();

        // Deployment keywords
        if (requestLower.Contains("deploy") && (requestLower.Contains("infrastructure") ||
            requestLower.Contains("cloud") || requestLower.Contains("aws") ||
            requestLower.Contains("azure") || requestLower.Contains("gcp")))
        {
            return "deployment";
        }

        // Upgrade keywords
        if ((requestLower.Contains("upgrade") || requestLower.Contains("update")) &&
            requestLower.Contains("version"))
        {
            return "upgrade";
        }

        // Metadata keywords
        if (requestLower.Contains("metadata") || requestLower.Contains("stac") ||
            requestLower.Contains("extract") && requestLower.Contains("raster"))
        {
            return "metadata";
        }

        // GitOps keywords
        if (requestLower.Contains("gitops") || (requestLower.Contains("git") &&
            requestLower.Contains("sync")))
        {
            return "gitops";
        }

        // Benchmark keywords
        if (requestLower.Contains("benchmark") || requestLower.Contains("load test") ||
            requestLower.Contains("performance test"))
        {
            return "benchmark";
        }

        // Certificate renewal keywords
        if (requestLower.Contains("certificate") || requestLower.Contains("cert") ||
            requestLower.Contains("ssl") || requestLower.Contains("tls"))
        {
            if (requestLower.Contains("renew") || requestLower.Contains("renewal") ||
                requestLower.Contains("expire") || requestLower.Contains("expiring") ||
                requestLower.Contains("update") || requestLower.Contains("refresh"))
            {
                return "certificate-renewal";
            }
        }

        // Network diagnostics keywords
        if (requestLower.Contains("network") || requestLower.Contains("connectivity") ||
            requestLower.Contains("connection") || requestLower.Contains("timeout") ||
            requestLower.Contains("dns") || requestLower.Contains("firewall") ||
            requestLower.Contains("port") || requestLower.Contains("latency") ||
            requestLower.Contains("traceroute") || requestLower.Contains("ping"))
        {
            if (requestLower.Contains("diagnose") || requestLower.Contains("diagnostic") ||
                requestLower.Contains("troubleshoot") || requestLower.Contains("debug") ||
                requestLower.Contains("issue") || requestLower.Contains("problem") ||
                requestLower.Contains("test") || requestLower.Contains("check"))
            {
                return "network-diagnostics";
            }
        }

        return null;
    }

    /// <summary>
    /// Creates initial event for deployment process.
    /// </summary>
    private async Task<KernelProcessEvent> CreateDeploymentEventAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        // Use LLM to extract deployment parameters from request
        var deploymentRequest = await _parameterExtraction.ExtractDeploymentParametersAsync(request, cancellationToken);

        return new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = deploymentRequest
        };
    }

    /// <summary>
    /// Creates initial event for upgrade process.
    /// </summary>
    private async Task<KernelProcessEvent> CreateUpgradeEventAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        // Use LLM to extract upgrade parameters from request
        var upgradeRequest = await _parameterExtraction.ExtractUpgradeParametersAsync(request, cancellationToken);

        return new KernelProcessEvent
        {
            Id = "StartUpgrade",
            Data = upgradeRequest
        };
    }

    /// <summary>
    /// Creates initial event for metadata process.
    /// </summary>
    private async Task<KernelProcessEvent> CreateMetadataEventAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        // Use LLM to extract metadata parameters from request
        var metadataRequest = await _parameterExtraction.ExtractMetadataParametersAsync(request, cancellationToken);

        return new KernelProcessEvent
        {
            Id = "StartMetadataExtraction",
            Data = metadataRequest
        };
    }

    /// <summary>
    /// Creates initial event for GitOps process.
    /// </summary>
    private async Task<KernelProcessEvent> CreateGitOpsEventAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        // Use LLM to extract GitOps parameters from request
        var gitOpsRequest = await _parameterExtraction.ExtractGitOpsParametersAsync(request, cancellationToken);

        return new KernelProcessEvent
        {
            Id = "StartGitOps",
            Data = gitOpsRequest
        };
    }

    /// <summary>
    /// Creates initial event for benchmark process.
    /// </summary>
    private async Task<KernelProcessEvent> CreateBenchmarkEventAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        // Use LLM to extract benchmark parameters from request
        var benchmarkRequest = await _parameterExtraction.ExtractBenchmarkParametersAsync(request, cancellationToken);

        return new KernelProcessEvent
        {
            Id = "StartBenchmark",
            Data = benchmarkRequest
        };
    }

    /// <summary>
    /// Creates initial event for certificate renewal process.
    /// </summary>
    private async Task<KernelProcessEvent> CreateCertificateRenewalEventAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        // Use LLM to extract certificate renewal parameters from request
        var certRenewalRequest = await _parameterExtraction.ExtractCertificateRenewalParametersAsync(request, cancellationToken);

        return new KernelProcessEvent
        {
            Id = "StartCertificateRenewal",
            Data = certRenewalRequest
        };
    }

    /// <summary>
    /// Creates initial event for network diagnostics process.
    /// </summary>
    private async Task<KernelProcessEvent> CreateNetworkDiagnosticsEventAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        // Use LLM to extract network diagnostics parameters from request
        var diagnosticsRequest = await _parameterExtraction.ExtractNetworkDiagnosticsParametersAsync(request, cancellationToken);

        return new KernelProcessEvent
        {
            Id = "StartNetworkDiagnostics",
            Data = diagnosticsRequest
        };
    }

    /// <summary>
    /// Gets the status of a running process.
    /// </summary>
    public async Task<ProcessStatusResult> GetProcessStatusAsync(string processId, CancellationToken cancellationToken = default)
    {
        var processInfo = await _processStateStore.GetProcessAsync(processId, cancellationToken);

        if (processInfo != null)
        {
            return new ProcessStatusResult
            {
                ProcessId = processId,
                Found = true,
                Status = processInfo.Status,
                CurrentStep = processInfo.CurrentStep,
                CompletionPercentage = processInfo.CompletionPercentage,
                ErrorMessage = processInfo.ErrorMessage
            };
        }

        return new ProcessStatusResult
        {
            ProcessId = processId,
            Found = false,
            ErrorMessage = "Process not found"
        };
    }
}

/// <summary>
/// Result object for process status queries.
/// </summary>
public class ProcessStatusResult
{
    public string ProcessId { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string Status { get; set; } = "Unknown";
    public string CurrentStep { get; set; } = string.Empty;
    public int CompletionPercentage { get; set; }
    public string? ErrorMessage { get; set; }
}


// TODO: Implement HonuaGroupChatManager that uses LLM to dynamically select agents
// For now, using RoundRobinGroupChatManager as a simple fallback
