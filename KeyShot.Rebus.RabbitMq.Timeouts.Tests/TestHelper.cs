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
    
    public static void DeleteTestQueue()
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

        using var connection = connectionFactory.CreateConnection();
        using var model = connection.CreateModel();
        model.QueueDelete(TimeoutQueueName);
    }
}