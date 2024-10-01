using Rebus.Tests.Contracts.Timeouts;

namespace KeyShot.Rebus.RabbitMq.Timeouts.Tests;

[TestFixture]
[NonParallelizable]
public class Tests : BasicStoreAndRetrieveOperations<TimeoutFactory>
{
}