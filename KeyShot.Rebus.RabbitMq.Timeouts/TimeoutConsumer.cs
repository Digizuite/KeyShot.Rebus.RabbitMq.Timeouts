using System.Collections.Concurrent;
using RabbitMQ.Client;
using Rebus.Logging;

namespace KeyShot.Rebus.RabbitMq.Timeouts;

sealed class TimeoutConsumer : AsyncDefaultBasicConsumer, IDisposable, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly ConcurrentDictionary<ulong, QueuedMessage> _queuedMessages = new();
    private readonly ILog _log;

    public TimeoutConsumer(IChannel model, IConnection connection, ILog log) : base(model)
    {
        _connection = connection;
        _log = log;
    }


    public override Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
        string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        _log.Debug("Received message with delivery tag {deliveryTag}", deliveryTag);
        var message = new QueuedMessage(deliveryTag, properties.Headers, body.ToArray(), this);
        _queuedMessages.TryAdd(deliveryTag, message);
        return Task.CompletedTask;
    }

    public IEnumerable<QueuedMessage> GetMessages()
    {
        return _queuedMessages.Values;
    }

    public void RemoveFromQueue(ulong deliveryTag)
    {
        _queuedMessages.TryRemove(deliveryTag, out _);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.AbortAsync();
    }
}