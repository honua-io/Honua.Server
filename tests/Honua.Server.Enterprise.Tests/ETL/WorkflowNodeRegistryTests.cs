// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

[Trait("Category", "Unit")]
public sealed class WorkflowNodeRegistryTests
{
    [Fact]
    public void RegisterNode_ValidNode_Succeeds()
    {
        var registry = new WorkflowNodeRegistry();
        var node = new TestNode();

        registry.RegisterNode("test.node", node);

        Assert.True(registry.IsRegistered("test.node"));
    }

    [Fact]
    public void RegisterNode_DuplicateType_Overwrites()
    {
        var registry = new WorkflowNodeRegistry();
        var node1 = new TestNode();
        var node2 = new TestNode();

        registry.RegisterNode("test.node", node1);
        registry.RegisterNode("test.node", node2);

        var retrieved = registry.GetNode("test.node");
        Assert.Same(node2, retrieved);
    }

    [Fact]
    public void GetNode_ExistingType_ReturnsNode()
    {
        var registry = new WorkflowNodeRegistry();
        var node = new TestNode();
        registry.RegisterNode("test.node", node);

        var retrieved = registry.GetNode("test.node");

        Assert.NotNull(retrieved);
        Assert.Same(node, retrieved);
    }

    [Fact]
    public void GetNode_NonExistentType_ReturnsNull()
    {
        var registry = new WorkflowNodeRegistry();

        var retrieved = registry.GetNode("nonexistent");

        Assert.Null(retrieved);
    }

    [Fact]
    public void IsRegistered_ExistingType_ReturnsTrue()
    {
        var registry = new WorkflowNodeRegistry();
        var node = new TestNode();
        registry.RegisterNode("test.node", node);

        Assert.True(registry.IsRegistered("test.node"));
    }

    [Fact]
    public void IsRegistered_NonExistentType_ReturnsFalse()
    {
        var registry = new WorkflowNodeRegistry();

        Assert.False(registry.IsRegistered("nonexistent"));
    }

    [Fact]
    public void GetAllNodeTypes_EmptyRegistry_ReturnsEmpty()
    {
        var registry = new WorkflowNodeRegistry();

        var types = registry.GetAllNodeTypes().ToList();

        Assert.Empty(types);
    }

    [Fact]
    public void GetAllNodeTypes_MultipleNodes_ReturnsAllTypes()
    {
        var registry = new WorkflowNodeRegistry();
        registry.RegisterNode("test.node1", new TestNode());
        registry.RegisterNode("test.node2", new TestNode());
        registry.RegisterNode("test.node3", new TestNode());

        var types = registry.GetAllNodeTypes().ToList();

        Assert.Equal(3, types.Count);
        Assert.Contains("test.node1", types);
        Assert.Contains("test.node2", types);
        Assert.Contains("test.node3", types);
    }

    [Fact]
    public void RegisterNode_CaseSensitive_TreatsDifferently()
    {
        var registry = new WorkflowNodeRegistry();
        var node1 = new TestNode();
        var node2 = new TestNode();

        registry.RegisterNode("test.node", node1);
        registry.RegisterNode("TEST.NODE", node2);

        Assert.True(registry.IsRegistered("test.node"));
        Assert.True(registry.IsRegistered("TEST.NODE"));
        Assert.NotSame(registry.GetNode("test.node"), registry.GetNode("TEST.NODE"));
    }

    [Fact]
    public void GetAllNodeTypes_AfterOverwrite_ReturnsUniqueTypes()
    {
        var registry = new WorkflowNodeRegistry();
        registry.RegisterNode("test.node", new TestNode());
        registry.RegisterNode("test.node", new TestNode()); // Overwrite

        var types = registry.GetAllNodeTypes().ToList();

        Assert.Single(types);
        Assert.Equal("test.node", types[0]);
    }

    #region Test Node Implementation

    private class TestNode : WorkflowNodeBase
    {
        public TestNode() : base(NullLogger<TestNode>.Instance) { }

        public override string NodeType => "test.node";
        public override string DisplayName => "Test Node";
        public override string Description => "A test node";

        public override Task<NodeExecutionResult> ExecuteAsync(
            NodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeExecutionResult { Success = true });
        }
    }

    #endregion
}
