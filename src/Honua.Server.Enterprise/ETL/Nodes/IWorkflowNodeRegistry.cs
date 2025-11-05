// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Registry for workflow node implementations
/// </summary>
public interface IWorkflowNodeRegistry
{
    /// <summary>
    /// Registers a node implementation
    /// </summary>
    void RegisterNode(string nodeType, IWorkflowNode node);

    /// <summary>
    /// Gets a node implementation by type
    /// </summary>
    IWorkflowNode? GetNode(string nodeType);

    /// <summary>
    /// Gets all registered node types
    /// </summary>
    IEnumerable<string> GetAllNodeTypes();

    /// <summary>
    /// Checks if a node type is registered
    /// </summary>
    bool IsRegistered(string nodeType);
}

/// <summary>
/// Default implementation of workflow node registry
/// </summary>
public class WorkflowNodeRegistry : IWorkflowNodeRegistry
{
    private readonly Dictionary<string, IWorkflowNode> _nodes = new();

    public void RegisterNode(string nodeType, IWorkflowNode node)
    {
        _nodes[nodeType] = node;
    }

    public IWorkflowNode? GetNode(string nodeType)
    {
        return _nodes.TryGetValue(nodeType, out var node) ? node : null;
    }

    public IEnumerable<string> GetAllNodeTypes()
    {
        return _nodes.Keys;
    }

    public bool IsRegistered(string nodeType)
    {
        return _nodes.ContainsKey(nodeType);
    }
}
