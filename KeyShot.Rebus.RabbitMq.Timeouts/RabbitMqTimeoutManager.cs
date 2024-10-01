using System.Text;
using RabbitMQ.Client;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Time;
using Rebus.Timeouts;

namespace KeyShot.Rebus.RabbitMq.Timeouts;

public sealed class RabbitMqTimeoutManager : ITimeoutManager, IInitializable, IDisposable
{
    private readonly ILog _log;
    private readonly ILog _timeoutConsumerLog;
    private TimeoutConsumer? _consumer;
    private readonly IRebusTime _rebusTime;
    private readonly RabbitMqTimeoutOptions _options;

    public RabbitMqTimeoutManager(RabbitMqTimeoutOptions options, IRebusLoggerFactory loggerFactory,
        IRebusTime rebusTime)
    {
        _rebusTime = rebusTime;
        _options = options;
        _log = loggerFactory.GetLogger<RabbitMqTimeoutManager>();
        _timeoutConsumerLog = loggerFactory.GetLogger<TimeoutConsumer>();

    }

    public Task Defer(DateTimeOffset approximateDueTime, Dictionary<string, string> headers, byte[] body)
    {
        _log.Debug("Deferring message with due time {dueTime}", approximateDueTime);
        
        var consumer = RequireConsumer();
        
        var properties = consumer.Model.CreateBasicProperties();
        properties.Headers = headers.ToDictionary(p => p.Key, p => (object)p.Value);
        properties.Headers[RebusRabbitMqTimeoutHeaders.DueTime] = approximateDueTime.ToUnixTimeMilliseconds();

        consumer.Model.BasicPublish(exchange: string.Empty, routingKey: _options.TimeoutQueueName, mandatory: true, properties, body);
        
        _log.Debug("Message deferred until {dueTime}", approximateDueTime);
        return Task.CompletedTask;
    }

    public Task<DueMessagesResult> GetDueMessages()
    {
        #if DEBUG
        // Add a bit of delay for testing to ensure we actually have had time to 
        // receive the messages from Rabbit.
        Thread.Sleep(1000);
        #endif
        
        var consumer = RequireConsumer();
        
        var now = _rebusTime.Now.ToUnixTimeMilliseconds();

        var messages = consumer.GetMessages()
            .Where(message => message.DueTime <= now)
            .Select(message =>
            {
                return new DueMessage(message.Headers, message.Body.ToArray(), () =>
                {
                    message.Ack();
                    return Task.CompletedTask;
                });
            });

        return Task.FromResult(new DueMessagesResult(messages));
    }

    private TimeoutConsumer RequireConsumer()
    {
        if (_consumer is { } consumer)
        {
            return consumer;
        }
        
        throw new InvalidOperationException("Timeout manager is not initialized");
    }

    public void Initialize()
    {
    
        
        var connectionFactory = new ConnectionFactory()
        {
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
            VirtualHost = _options.VHost,
            UserName = _options.Username,
            Password = _options.Password,
            Port = _options.Port,
            HostName = _options.HostName,
        };

        var connection = connectionFactory.CreateConnection();
        var channel = connection.CreateModel();
        channel.BasicQos(prefetchCount: _options.PrefetchCount, prefetchSize: 0, global: false);

        channel.QueueDeclare(_options.TimeoutQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: _options.QueueArguments);


        _consumer = new TimeoutConsumer(channel, connection, _timeoutConsumerLog);

        channel.BasicConsume(_options.TimeoutQueueName, autoAck: false, _consumer);
    }

    public void DeleteInputQueue()
    {
        var connectionFactory = new ConnectionFactory()
        {
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
            VirtualHost = _options.VHost,
            UserName = _options.Username,
            Password = _options.Password,
            Port = _options.Port,
            HostName = _options.HostName,
        };

        using var connection = connectionFactory.CreateConnection();
        using var model = connection.CreateModel();
        model.QueueDelete(_options.TimeoutQueueName);
    }


    public void Dispose()
    {
        _consumer?.Dispose();
    }
}

public static class RebusRabbitMqTimeoutHeaders
{
    public const string DueTime = "x-rbs2-due";
}