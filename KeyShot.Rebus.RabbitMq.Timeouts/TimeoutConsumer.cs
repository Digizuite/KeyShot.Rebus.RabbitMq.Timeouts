using System.Collections.Concurrent;
using RabbitMQ.Client;
using Rebus.Logging;

namespace KeyShot.Rebus.RabbitMq.Timeouts;

sealed class TimeoutConsumer : DefaultBasicConsumer, IDisposable
{
    private readonly IConnection _connection;
    private readonly ConcurrentDictionary<ulong, QueuedMessage> _queuedMessages = new();
    private readonly ILog _log;

    public TimeoutConsumer(IModel model, IConnection connection, ILog log) : base(model)
    {
        _connection = connection;
        _log = log;
    }

    public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
        string routingKey,
        IBasicProperties properties, ReadOnlyMemory<byte> body)
    {
        _log.Debug("Received message with delivery tag {deliveryTag}", deliveryTag);
        var message = new QueuedMessage(deliveryTag, properties.Headers, body, this);
        _queuedMessages.TryAdd(deliveryTag, message);
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
        _connection.Abort();
    }
}