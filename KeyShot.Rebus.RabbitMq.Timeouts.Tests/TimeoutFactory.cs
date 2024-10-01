using Rebus.Logging;
using Rebus.Tests.Contracts.Timeouts;
using Rebus.Timeouts;

namespace KeyShot.Rebus.RabbitMq.Timeouts.Tests;

public class TimeoutFactory : ITimeoutManagerFactory
{
    readonly FakeRebusTime _fakeRebusTime = new FakeRebusTime();

    public ITimeoutManager Create()
    {
        var consoleLoggerFactory = new ConsoleLoggerFactory(true);
        var manager = new RabbitMqTimeoutManager(new RabbitMqTimeoutOptions()
        {
            HostName = TestHelper.HostName,
            Port = TestHelper.Port,
            Username = TestHelper.Username,
            Password = TestHelper.Password,
            VHost = TestHelper.Vhost,
            TimeoutQueueName = TestHelper.TimeoutQueueName,
        }, consoleLoggerFactory, _fakeRebusTime);

        manager.Initialize();
        
        return manager;
    }

    public void Cleanup()
    {
        TestHelper.DeleteTestQueue();   
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