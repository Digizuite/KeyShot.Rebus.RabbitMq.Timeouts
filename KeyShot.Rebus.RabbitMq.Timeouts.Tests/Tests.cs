using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Web;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Timeouts;
using Rebus.Timeouts;

namespace KeyShot.Rebus.RabbitMq.Timeouts.Tests;

[TestFixture]
[NonParallelizable]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class Tests : BasicStoreAndRetrieveOperations<TimeoutFactory>
{
    
}

[TestFixture]
[NonParallelizable]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class OtherTests : FixtureBase
{
    protected override void SetUp()
    {
        TestHelper.DeleteTestQueue();
        base.SetUp();
    }

    protected override void TearDown()
    {
        TestHelper.DeleteTestQueue();
        base.TearDown();
    }

    [Test]
    public async Task SurvivesDisconnects()
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
        }, consoleLoggerFactory, new FakeRebusTime());

        manager.Initialize();

        await manager.Defer(DateTimeOffset.UnixEpoch, new Dictionary<string, string>(), [1]);

        var due = await manager.GetDueMessages();

        Assert.That(due.Count, Is.EqualTo(1));
        Assert.That(due.Single().Body, Is.EqualTo(new byte[]{1}));
        await CompleteDueMessages(due);
        
        await KillConnection();
        
        await manager.Defer(DateTimeOffset.UnixEpoch, new Dictionary<string, string>(), [2]);
        
        due = await manager.GetDueMessages();
        Assert.That(due.Count, Is.EqualTo(1));
        await CompleteDueMessages(due);
        Assert.That(due.Single().Body, Is.EqualTo(new byte[]{2}));
        
        await manager.Defer(DateTimeOffset.UnixEpoch, new Dictionary<string, string>(), [3]);

        await KillConnection();
        
        due = await manager.GetDueMessages();
        
        Assert.That(due.Count, Is.EqualTo(1));
        await CompleteDueMessages(due);


    }

    private static async Task CompleteDueMessages(DueMessagesResult due)
    {
        foreach (var dueMessage in due)
        {
            await dueMessage.MarkAsCompleted();
        }
        await due.Complete();
    }

    private async Task KillConnection()
    {
        // Rabbit's management ui can take a while to update.
        await Task.Delay(5000);
        
        var httpClient = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, "http://localhost:15672/api/connections?columns=name");
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{TestHelper.Username}:{TestHelper.Password}")));

        var response = await httpClient.SendAsync(message);
        response.EnsureSuccessStatusCode();
        
        var connections = await response.Content.ReadFromJsonAsync<List<ConnectionResponse>>();
        
        foreach (var connectionResponse in connections!)
        {
            message = new HttpRequestMessage(HttpMethod.Delete, "http://localhost:15672/api/connections/" + connectionResponse.Name);
            Console.WriteLine($"Calling url {message.RequestUri}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{TestHelper.Username}:{TestHelper.Password}")));

            var res = await httpClient.SendAsync(message);
            res.EnsureSuccessStatusCode();
        }
        
        await Task.Delay(5000);
    }


    private class ConnectionResponse
    {
        public required string Name { get; set; }
    }
    
}