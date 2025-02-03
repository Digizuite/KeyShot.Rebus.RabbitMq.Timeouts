using RabbitMQ.Client;

namespace KeyShot.Rebus.RabbitMq.Timeouts.Tests;

public static class TestHelper
{
    public const string TimeoutQueueName = "rebusTimeouts";
    public const string Username = "user";
    public const string Password = "passw0rd";
    public const string Vhost = "dev";
    public const int Port = 5672;
    public const string HostName = "localhost";
    
    public static async Task DeleteTestQueue()
    {
        
        var connectionFactory = new ConnectionFactory()
        {
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
            VirtualHost = Vhost,
            UserName = Username,
            Password = Password,
            Port = Port,
            HostName = HostName,
        };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var model = await connection.CreateChannelAsync();
        await model.QueueDeleteAsync(TimeoutQueueName);
    }
}