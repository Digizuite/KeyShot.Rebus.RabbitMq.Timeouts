using Rebus.Time;

namespace KeyShot.Rebus.RabbitMq.Timeouts.Tests;

class FakeRebusTime : IRebusTime
{
    Func<DateTimeOffset> _nowFactory = () => DateTimeOffset.Now;

    public DateTimeOffset Now => _nowFactory();

    public void SetNow(DateTimeOffset fakeTime) => _nowFactory = () => fakeTime;
}