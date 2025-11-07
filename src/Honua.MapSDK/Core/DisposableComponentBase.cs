using Microsoft.AspNetCore.Components;
using Honua.MapSDK.Core;

namespace Honua.MapSDK.Core;

/// <summary>
/// Base class for MapSDK components that provides disposal pattern implementation.
/// Automatically unsubscribes from ComponentBus and disposes resources.
/// </summary>
public abstract class DisposableComponentBase : ComponentBase, IDisposable
{
    private bool _disposed;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly List<Action> _cleanupActions = new();

    /// <summary>
    /// Gets or sets the ComponentBus for inter-component communication.
    /// </summary>
    [Inject]
    protected ComponentBus ComponentBus { get; set; } = default!;

    /// <summary>
    /// Subscribes to a message type and automatically unsubscribes on disposal.
    /// </summary>
    /// <typeparam name="TMessage">The message type to subscribe to.</typeparam>
    /// <param name="handler">The message handler.</param>
    protected void SubscribeToMessage<TMessage>(Action<TMessage> handler)
    {
        var subscription = ComponentBus.Subscribe(handler);
        _subscriptions.Add(subscription);
    }

    /// <summary>
    /// Registers a cleanup action to be executed on disposal.
    /// </summary>
    /// <param name="cleanup">The cleanup action.</param>
    protected void RegisterCleanup(Action cleanup)
    {
        _cleanupActions.Add(cleanup);
    }

    /// <summary>
    /// Registers a disposable resource to be disposed on component disposal.
    /// </summary>
    /// <param name="disposable">The disposable resource.</param>
    protected void RegisterDisposable(IDisposable disposable)
    {
        _subscriptions.Add(disposable);
    }

    /// <summary>
    /// Called when the component is being disposed.
    /// Override this method to add custom disposal logic.
    /// </summary>
    protected virtual void OnDispose()
    {
        // Override in derived classes
    }

    /// <summary>
    /// Disposes the component and all registered resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            // Call derived class disposal logic
            OnDispose();

            // Execute cleanup actions
            foreach (var cleanup in _cleanupActions)
            {
                try
                {
                    cleanup();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }

            // Dispose all subscriptions
            foreach (var subscription in _subscriptions)
            {
                try
                {
                    subscription.Dispose();
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }

            _subscriptions.Clear();
            _cleanupActions.Clear();
        }
        finally
        {
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
