// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Xunit;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Tests for ConversationStore - stores and retrieves conversation records.
/// </summary>
[Trait("Category", "Unit")]
public class ConversationStoreTests
{
    [Fact]
    public async Task SaveConversationAsync_NewConversation_StoresSuccessfully()
    {
        // Arrange
        var store = new InMemoryConversationStore();
        var conversation = new ConversationRecord
        {
            ConversationId = "conv-123",
            CustomerId = "customer-123",
            MessagesJson = "[]",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await store.SaveConversationAsync(conversation);

        // Assert
        var retrieved = await store.GetConversationAsync("conv-123");
        retrieved.Should().NotBeNull();
        retrieved!.ConversationId.Should().Be("conv-123");
        retrieved.CustomerId.Should().Be("customer-123");
    }

    [Fact]
    public async Task SaveConversationAsync_UpdateExisting_UpdatesRecord()
    {
        // Arrange
        var store = new InMemoryConversationStore();
        var conversation = new ConversationRecord
        {
            ConversationId = "conv-456",
            CustomerId = "customer-456",
            MessagesJson = "[]",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await store.SaveConversationAsync(conversation);

        var updatedConversation = conversation with
        {
            Status = "completed",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        // Act
        await store.SaveConversationAsync(updatedConversation);

        // Assert
        var retrieved = await store.GetConversationAsync("conv-456");
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task GetConversationAsync_NonExistent_ReturnsNull()
    {
        // Arrange
        var store = new InMemoryConversationStore();

        // Act
        var result = await store.GetConversationAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveConversationAsync_WithRequirements_StoresJson()
    {
        // Arrange
        var store = new InMemoryConversationStore();
        var requirements = new BuildRequirements
        {
            Protocols = new System.Collections.Generic.List<string> { "WFS", "WMS" },
            CloudProvider = "aws",
            Tier = "pro"
        };

        var conversation = new ConversationRecord
        {
            ConversationId = "conv-789",
            CustomerId = "customer-789",
            MessagesJson = "[]",
            RequirementsJson = JsonSerializer.Serialize(requirements),
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        // Act
        await store.SaveConversationAsync(conversation);

        // Assert
        var retrieved = await store.GetConversationAsync("conv-789");
        retrieved.Should().NotBeNull();
        retrieved!.RequirementsJson.Should().NotBeNullOrEmpty();

        var deserializedRequirements = JsonSerializer.Deserialize<BuildRequirements>(retrieved.RequirementsJson!);
        deserializedRequirements.Should().NotBeNull();
        deserializedRequirements!.Tier.Should().Be("pro");
    }
}

/// <summary>
/// In-memory implementation of IConversationStore for testing.
/// </summary>
public class InMemoryConversationStore : IConversationStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ConversationRecord> _conversations = new();

    public Task SaveConversationAsync(ConversationRecord conversation, System.Threading.CancellationToken cancellationToken = default)
    {
        _conversations.AddOrUpdate(conversation.ConversationId, conversation, (key, old) => conversation);
        return Task.CompletedTask;
    }

    public Task<ConversationRecord?> GetConversationAsync(string conversationId, System.Threading.CancellationToken cancellationToken = default)
    {
        _conversations.TryGetValue(conversationId, out var conversation);
        return Task.FromResult(conversation);
    }
}
