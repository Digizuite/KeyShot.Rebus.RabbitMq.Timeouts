using Rebus.Logging;
using Rebus.Tests.Contracts.Timeouts;
using Rebus.Timeouts;

namespace KeyShot.Rebus.RabbitMq.Timeouts.Tests;

public class TimeoutFactory : ITimeoutManagerFactory
{
    readonly FakeRebusTime _fakeRebusTime = new FakeRebusTime();
    private RabbitMqTimeoutManager _manager = null!;

    public ITimeoutManager Create()
    {
        var consoleLoggerFactory = new ConsoleLoggerFactory(true);
        var manager = new RabbitMqTimeoutManager(new RabbitMqTimeoutOptions()
        {
            HostName = "localhost",
            Port = 5672,
            Username = "user",
            Password = "passw0rd",
            VHost = "dev",
            TimeoutQueueName = "rebusTimeouts",
        }, consoleLoggerFactory, _fakeRebusTime);

        manager.Initialize();

        _manager = manager;
        
        return manager;
    }

    public void Cleanup()
    {
        _manager.DeleteInputQueue();
    }

    public string GetDebugInfo()
    {
        return "";
    }

    public void FakeIt(DateTimeOffset fakeTime)
    {
        _fakeRebusTime.SetNow(fakeTime);
    }
}