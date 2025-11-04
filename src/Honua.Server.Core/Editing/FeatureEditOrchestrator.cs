// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Editing;

public interface IFeatureEditOrchestrator
{
    Task<FeatureEditBatchResult> ExecuteAsync(FeatureEditBatch batch, CancellationToken cancellationToken = default);
}

public sealed class FeatureEditOrchestrator : IFeatureEditOrchestrator
{
    private readonly IFeatureRepository _repository;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IFeatureEditAuthorizationService _authorizationService;
    private readonly IFeatureEditConstraintValidator _constraintValidator;
    private readonly IFeatureContextResolver _contextResolver;
    private readonly ILogger<FeatureEditOrchestrator> _logger;

    public FeatureEditOrchestrator(
        IFeatureRepository repository,
        IMetadataRegistry metadataRegistry,
        IFeatureEditAuthorizationService authorizationService,
        IFeatureEditConstraintValidator constraintValidator,
        IFeatureContextResolver contextResolver,
        ILogger<FeatureEditOrchestrator> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _constraintValidator = constraintValidator ?? throw new ArgumentNullException(nameof(constraintValidator));
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FeatureEditBatchResult> ExecuteAsync(FeatureEditBatch batch, CancellationToken cancellationToken = default)
    {
        if (batch is null)
        {
            throw new ArgumentNullException(nameof(batch));
        }

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        // Determine if we need a transaction by checking first command's provider
        IDataStoreTransaction? transaction = null;
        FeatureContext? firstContext = null;

        try
        {
            // Get first command's context to determine if provider supports transactions
            if (batch.Commands.Count > 0 && batch.RollbackOnFailure)
            {
                // BUG FIX #28: Validate all commands target the same data source before opening transaction
                // Mixed data sources would cause some commands to execute outside the transaction
                var firstCommand = batch.Commands[0];
                if (batch.Commands.Count > 1)
                {
                    var firstServiceId = firstCommand.ServiceId;
                    var firstLayerId = firstCommand.LayerId;

                    for (var i = 1; i < batch.Commands.Count; i++)
                    {
                        var cmd = batch.Commands[i];
                        if (!string.Equals(cmd.ServiceId, firstServiceId, StringComparison.Ordinal) ||
                            !string.Equals(cmd.LayerId, firstLayerId, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"Cannot execute edit batch with RollbackOnFailure=true across multiple data sources. " +
                                $"Command 0 targets '{firstServiceId}/{firstLayerId}', but command {i} targets '{cmd.ServiceId}/{cmd.LayerId}'. " +
                                $"Either use the same layer for all commands or set RollbackOnFailure=false.");
                        }
                    }

                    _logger.LogDebug(
                        "Validated that all {Count} commands target the same layer: {ServiceId}/{LayerId}",
                        batch.Commands.Count,
                        firstServiceId,
                        firstLayerId);
                }

                if (TryResolveLayer(snapshot, firstCommand, out var firstLayer, out _))
                {
                    firstContext = await _contextResolver.ResolveAsync(
                        firstCommand.ServiceId,
                        firstCommand.LayerId,
                        cancellationToken).ConfigureAwait(false);

                    // Start transaction if provider supports it
                    transaction = await firstContext.Provider.BeginTransactionAsync(
                        firstContext.DataSource,
                        cancellationToken).ConfigureAwait(false);

                    // BUG FIX #29: Fail fast when provider doesn't support transactions but RollbackOnFailure=true
                    // This prevents false confidence that the batch will be atomic when it won't be
                    if (transaction == null)
                    {
                        throw new InvalidOperationException(
                            $"Cannot execute edit batch with RollbackOnFailure=true on data source '{firstCommand.ServiceId}/{firstCommand.LayerId}' " +
                            $"because the provider does not support transactions. Either use a provider with transaction support or set RollbackOnFailure=false.");
                    }

                    _logger.LogInformation(
                        "Started transaction for edit batch with {Count} commands on service {ServiceId}",
                        batch.Commands.Count,
                        firstCommand.ServiceId);
                }
            }
            else if (batch.Commands.Count > 0 && !batch.RollbackOnFailure)
            {
                // Log a warning if commands span multiple data sources without rollback protection
                var serviceLayerPairs = batch.Commands
                    .Select(c => $"{c.ServiceId}/{c.LayerId}")
                    .Distinct()
                    .ToList();

                if (serviceLayerPairs.Count > 1)
                {
                    _logger.LogWarning(
                        "Edit batch with RollbackOnFailure=false spans {Count} different layers: {Layers}. " +
                        "Partial failures may leave data in an inconsistent state.",
                        serviceLayerPairs.Count,
                        string.Join(", ", serviceLayerPairs));
                }
            }

            var results = new List<FeatureEditCommandResult>(batch.Commands.Count);

            for (var index = 0; index < batch.Commands.Count; index++)
            {
                var command = batch.Commands[index];
                if (!TryResolveLayer(snapshot, command, out var layerDefinition, out var layerFailure))
                {
                    results.Add(layerFailure!);
                    if (batch.RollbackOnFailure)
                    {
                        if (transaction != null)
                        {
                            _logger.LogWarning(
                                "Command {Index} failed to resolve layer. Rolling back transaction.",
                                index);
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }

                        AppendAbortResults(batch, results, index + 1);
                        return new FeatureEditBatchResult(results);
                    }

                    continue;
                }

                var authResult = _authorizationService.Authorize(command, layerDefinition!, batch.UserRoles, batch.IsAuthenticated);
                if (!authResult.IsAuthorized)
                {
                    results.Add(FeatureEditCommandResult.CreateFailure(command, authResult.Error!));
                    if (batch.RollbackOnFailure)
                    {
                        if (transaction != null)
                        {
                            _logger.LogWarning(
                                "Command {Index} authorization failed. Rolling back transaction.",
                                index);
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }

                        AppendAbortResults(batch, results, index + 1);
                        return new FeatureEditBatchResult(results);
                    }

                    continue;
                }

                FeatureEditError? validationError = command switch
                {
                    AddFeatureCommand addCommand => _constraintValidator.Validate(addCommand, layerDefinition!),
                    UpdateFeatureCommand updateCommand => _constraintValidator.Validate(updateCommand, layerDefinition!),
                    _ => null
                };

                if (validationError is not null)
                {
                    results.Add(FeatureEditCommandResult.CreateFailure(command, validationError));
                    if (batch.RollbackOnFailure)
                    {
                        if (transaction != null)
                        {
                            _logger.LogWarning(
                                "Command {Index} validation failed. Rolling back transaction.",
                                index);
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }

                        AppendAbortResults(batch, results, index + 1);
                        return new FeatureEditBatchResult(results);
                    }

                    continue;
                }

                try
                {
                    var commandResult = await ExecuteCommandAsync(command, layerDefinition!, transaction, cancellationToken).ConfigureAwait(false);
                    results.Add(commandResult);
                    if (!commandResult.Success && batch.RollbackOnFailure)
                    {
                        if (transaction != null)
                        {
                            _logger.LogWarning(
                                "Command {Index} execution failed with RollbackOnFailure=true. Rolling back transaction.",
                                index);
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }

                        AppendAbortResults(batch, results, index + 1);
                        return new FeatureEditBatchResult(results);
                    }
                }
                catch (Exceptions.ConcurrencyException ex)
                {
                    // Concurrent modification detected - return specific error code
                    _logger.LogWarning(
                        ex,
                        "Concurrent modification detected for command {Index}. EntityId={EntityId}, Expected={Expected}, Actual={Actual}",
                        index, ex.EntityId, ex.ExpectedVersion, ex.ActualVersion);

                    var error = new FeatureEditError("version_conflict", ex.Message, new Dictionary<string, string?>
                    {
                        ["entityId"] = ex.EntityId,
                        ["entityType"] = ex.EntityType,
                        ["expectedVersion"] = ex.ExpectedVersion?.ToString(),
                        ["actualVersion"] = ex.ActualVersion?.ToString()
                    });
                    results.Add(FeatureEditCommandResult.CreateFailure(command, error));

                    if (batch.RollbackOnFailure)
                    {
                        if (transaction != null)
                        {
                            _logger.LogWarning(
                                "Concurrency conflict on command {Index}. Rolling back transaction.",
                                index);
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }

                        AppendAbortResults(batch, results, index + 1);
                        return new FeatureEditBatchResult(results);
                    }
                }
                catch (Exception ex)
                {
                    var error = new FeatureEditError("edit_failed", ex.Message);
                    results.Add(FeatureEditCommandResult.CreateFailure(command, error));
                    if (batch.RollbackOnFailure)
                    {
                        if (transaction != null)
                        {
                            _logger.LogError(
                                ex,
                                "Command {Index} threw exception with RollbackOnFailure=true. Rolling back transaction.",
                                index);
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }

                        AppendAbortResults(batch, results, index + 1);
                        return new FeatureEditBatchResult(results);
                    }
                }
            }

            // All commands succeeded - commit transaction
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Successfully committed transaction for edit batch with {Count} commands",
                    batch.Commands.Count);
            }

            return new FeatureEditBatchResult(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during edit batch execution");

            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning("Rolled back transaction due to unexpected error");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction after unexpected error");
                }
            }

            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private bool TryResolveLayer(MetadataSnapshot snapshot, FeatureEditCommand command, out LayerDefinition? layerDefinition, out FeatureEditCommandResult? failure)
    {
        if (!snapshot.TryGetLayer(command.ServiceId, command.LayerId, out var layer))
        {
            layerDefinition = null;
            failure = FeatureEditCommandResult.CreateFailure(
                command,
                new FeatureEditError("layer_not_found", $"Layer '{command.ServiceId}/{command.LayerId}' was not found."));
            return false;
        }

        layerDefinition = layer;
        failure = null;
        return true;
    }

    private async Task<FeatureEditCommandResult> ExecuteCommandAsync(FeatureEditCommand command, LayerDefinition layer, IDataStoreTransaction? transaction, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case AddFeatureCommand addCommand:
                return await ExecuteAddAsync(addCommand, layer, transaction, cancellationToken);
            case UpdateFeatureCommand updateCommand:
                return await ExecuteUpdateAsync(updateCommand, layer, transaction, cancellationToken);
            case DeleteFeatureCommand deleteCommand:
                return await ExecuteDeleteAsync(deleteCommand, layer, transaction, cancellationToken);
            default:
                return FeatureEditCommandResult.CreateFailure(command, FeatureEditError.NotImplemented);
        }
    }

    private async Task<FeatureEditCommandResult> ExecuteAddAsync(AddFeatureCommand command, LayerDefinition layer, IDataStoreTransaction? transaction, CancellationToken cancellationToken)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (command.Attributes is not null)
        {
            foreach (var pair in command.Attributes)
            {
                attributes[pair.Key] = pair.Value;
            }
        }
        foreach (var pair in layer.Editing.Constraints.DefaultValues)
        {
            if (!attributes.ContainsKey(pair.Key))
            {
                attributes[pair.Key] = pair.Value;
            }
        }

        var record = new FeatureRecord(attributes);
        var created = await _repository.CreateAsync(command.ServiceId, command.LayerId, record, transaction, cancellationToken).ConfigureAwait(false);
        var featureId = ExtractFeatureId(created, layer.IdField);
        return FeatureEditCommandResult.CreateSuccess(command, featureId, created.Version);
    }

    private async Task<FeatureEditCommandResult> ExecuteUpdateAsync(UpdateFeatureCommand command, LayerDefinition layer, IDataStoreTransaction? transaction, CancellationToken cancellationToken)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (command.Attributes is not null)
        {
            foreach (var pair in command.Attributes)
            {
                attributes[pair.Key] = pair.Value;
            }
        }

        // Include version for optimistic concurrency control
        var record = new FeatureRecord(attributes, command.Version);
        var updated = await _repository.UpdateAsync(command.ServiceId, command.LayerId, command.FeatureId, record, transaction, cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            return FeatureEditCommandResult.CreateFailure(command, new FeatureEditError("not_found", "Feature to update was not found."));
        }

        var featureId = ExtractFeatureId(updated, layer.IdField) ?? command.FeatureId;
        return FeatureEditCommandResult.CreateSuccess(command, featureId, updated.Version);
    }

    private async Task<FeatureEditCommandResult> ExecuteDeleteAsync(DeleteFeatureCommand command, LayerDefinition layer, IDataStoreTransaction? transaction, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(command.ServiceId, command.LayerId, command.FeatureId, transaction, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return FeatureEditCommandResult.CreateFailure(command, new FeatureEditError("not_found", "Feature to delete was not found."));
        }

        return FeatureEditCommandResult.CreateSuccess(command, command.FeatureId);
    }

    private static void AppendAbortResults(FeatureEditBatch batch, List<FeatureEditCommandResult> results, int startIndex)
    {
        for (var i = startIndex; i < batch.Commands.Count; i++)
        {
            var command = batch.Commands[i];
            var error = new FeatureEditError("batch_aborted", "Batch aborted due to earlier failure.");
            results.Add(FeatureEditCommandResult.CreateFailure(command, error));
        }
    }

    private static string? ExtractFeatureId(FeatureRecord record, string idField)
    {
        if (record.Attributes.TryGetValue(idField, out var value) && value is not null)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return null;
    }
}
