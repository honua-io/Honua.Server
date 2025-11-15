// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain;

/// <summary>
/// Marker interface for all domain events.
/// Domain events represent something meaningful that happened in the domain.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when this domain event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this domain event.
    /// </summary>
    Guid EventId { get; }
}

/// <summary>
/// Base record for domain events with automatic timestamp and event ID generation.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();
}
