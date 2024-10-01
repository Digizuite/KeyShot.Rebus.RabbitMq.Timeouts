namespace KeyShot.Rebus.RabbitMq.Timeouts;

public sealed class RabbitMqTimeoutOptions
{
    public required string Username { get; set; }
    public required string Password { get; set; }

    public required string HostName { get; set; }
    public required int Port { get; set; }
    public required string VHost { get; set; }

    public required string TimeoutQueueName { get; set; }

    public ushort PrefetchCount { get; set; } = 100;

    public Dictionary<string, object> QueueArguments { get; set; } = new();
}