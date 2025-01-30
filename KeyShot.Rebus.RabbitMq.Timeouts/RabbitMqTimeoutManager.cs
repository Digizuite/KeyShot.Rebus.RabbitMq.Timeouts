using System.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Time;
using Rebus.Timeouts;

namespace KeyShot.Rebus.RabbitMq.Timeouts;

public sealed class RabbitMqTimeoutManager : ITimeoutManager, IInitializable, IDisposable, IAsyncDisposable
{
    private readonly ILog _log;
    private readonly ILog _timeoutConsumerLog;
    private TimeoutConsumer? _consumer;
    private readonly IRebusTime _rebusTime;
    private readonly RabbitMqTimeoutOptions _options;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public RabbitMqTimeoutManager(RabbitMqTimeoutOptions options, IRebusLoggerFactory loggerFactory,
        IRebusTime rebusTime)
    {
        _rebusTime = rebusTime;
        _options = options;
        _log = loggerFactory.GetLogger<RabbitMqTimeoutManager>();
        _timeoutConsumerLog = loggerFactory.GetLogger<TimeoutConsumer>();

    }

    public async Task Defer(DateTimeOffset approximateDueTime, Dictionary<string, string> headers, byte[] body)
    {
        _log.Debug("Deferring message with due time {dueTime}", approximateDueTime);
        

        try
        {
            await DeferCore(approximateDueTime, headers, body);
        }
        catch (RabbitMQClientException e)
        {
            _log.Warn(e, "RabbitMQ client exception encountered while attempting to defer message. Reinitializing client and trying again.");
            await InitializeAsync();

            await DeferCore(approximateDueTime, headers, body);
        }
    }

    private async ValueTask DeferCore(DateTimeOffset approximateDueTime, Dictionary<string, string> headers, byte[] body)
    {
        var consumer = RequireConsumer();
            
        var properties = new BasicProperties();
        properties.Headers = headers.ToDictionary(p => p.Key, object? (p) => p.Value);
        properties.Headers[RebusRabbitMqTimeoutHeaders.DueTime] = approximateDueTime.ToUnixTimeMilliseconds();

        await consumer.Channel.BasicPublishAsync(exchange: string.Empty, routingKey: _options.TimeoutQueueName, mandatory: true,
            properties, body);

        _log.Debug("Message deferred until {dueTime}", approximateDueTime);
    }

    public async Task<DueMessagesResult> GetDueMessages()
    {
        ForcedTestDelay();
        
        var consumer = RequireConsumer();
        if (!consumer.Channel.IsOpen)
        {
            _log.Debug("Consumer channel is closed, reinitializing");
            await InitializeAsync();
            consumer = RequireConsumer();
            ForcedTestDelay();
        }
        
        var now = _rebusTime.Now.ToUnixTimeMilliseconds();

        var messages = consumer.GetMessages()
            .Where(message => message.DueTime <= now)
            .Select(message =>
            {
                return new DueMessage(message.Headers, message.Body.ToArray(), async () =>
                {
                    await message.Ack();
                });
            })
            .ToList();

        return new DueMessagesResult(messages);
    }

    [Conditional("DEBUG")]
    private void ForcedTestDelay()
    {
        // Add a bit of delay for testing to ensure we actually have had time to 
        // receive the messages from Rabbit.
        Thread.Sleep(1000);
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
        InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_consumer is { } c)
            {
                _consumer = null;
                await c.DisposeAsync();
            }

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

            var connection = await connectionFactory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();
            await channel.BasicQosAsync(prefetchCount: _options.PrefetchCount, prefetchSize: 0, global: false);

            await channel.QueueDeclareAsync(_options.TimeoutQueueName, durable: true, exclusive: false,
                autoDelete: false,
                arguments: _options.QueueArguments);


            _consumer = new TimeoutConsumer(channel, connection, _timeoutConsumerLog);

            await channel.BasicConsumeAsync(_options.TimeoutQueueName, autoAck: false, _consumer);
        }
        finally
        {
            _initLock.Release();
        }
    }


    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumer is { } c)
        {
            await c.DisposeAsync();
        }
    }
}

public static class RebusRabbitMqTimeoutHeaders
{
    public const string DueTime = "x-rbs2-due";
}