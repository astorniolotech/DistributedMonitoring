using DistributedMonitoring.Domain.Interfaces;

namespace DistributedMonitoring.Infrastructure;

/// <summary>
/// In-memory message bus for internal communication between components
/// </summary>
public class MessageBus : IMessageBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _lock = new();

    public void Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var messageType = typeof(T);
            if (!_subscribers.ContainsKey(messageType))
            {
                _subscribers[messageType] = new List<Delegate>();
            }
            _subscribers[messageType].Add(handler);
        }
    }

    public void Publish<T>(T message)
    {
        if (message == null)
            return;

        List<Delegate>? handlers;
        lock (_lock)
        {
            var messageType = typeof(T);
            if (!_subscribers.TryGetValue(messageType, out handlers))
                return;

            // Create a copy to avoid modification during iteration
            handlers = new List<Delegate>(handlers);
        }

        // Invoke handlers outside the lock
        foreach (var handler in handlers)
        {
            try
            {
                ((Action<T>)handler).Invoke(message);
            }
            catch (Exception ex)
            {
                // Log error but don't stop other handlers
                Console.WriteLine($"Error in message handler: {ex.Message}");
            }
        }
    }
}
