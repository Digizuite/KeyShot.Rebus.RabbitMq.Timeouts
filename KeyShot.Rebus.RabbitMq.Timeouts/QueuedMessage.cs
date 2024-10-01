using System.Text;

namespace KeyShot.Rebus.RabbitMq.Timeouts;

sealed class QueuedMessage
{
    public readonly ulong DeliveryTag;
    public readonly Dictionary<string, string> Headers;
    public readonly byte[] Body;
    private readonly TimeoutConsumer _consumer;
    public readonly long DueTime;

    public QueuedMessage(ulong deliveryTag, IDictionary<string, object> headers, ReadOnlyMemory<byte> body,
        TimeoutConsumer consumer)
    {
        DeliveryTag = deliveryTag;

        Headers = new();
        foreach (var pair in headers)
        {
            if (pair.Key == RebusRabbitMqTimeoutHeaders.DueTime)
            {
                continue;
            }
            
            if(pair.Value is byte[] bytes)
            {
                Headers.Add(pair.Key, Encoding.UTF8.GetString(bytes));
                continue;
            }
            Headers.Add(pair.Key, pair.Value.ToString()!);
        }
        
        
        DueTime = (long)headers[RebusRabbitMqTimeoutHeaders.DueTime];
        
        
        _consumer = consumer;
        Body = body.ToArray();
    }

    public void Ack()
    {
        _consumer.RemoveFromQueue(DeliveryTag);
        
        _consumer.Model.BasicAck(DeliveryTag, multiple: false);
    }
}