using Rebus.Config;
using Rebus.Logging;
using Rebus.Time;
using Rebus.Timeouts;

namespace KeyShot.Rebus.RabbitMq.Timeouts;

public static class TimeoutConfigurationExtensions
{
    /// <summary>
    /// Configures RabbitMQ as a Timeout storage
    /// </summary>
    public static void StoreInRabbitMq(this StandardConfigurer<ITimeoutManager> configurer,
        RabbitMqTimeoutOptions options)
    {
        if (options == null!)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (configurer == null!)
        {
            throw new ArgumentNullException(nameof(configurer));
        }
        
        configurer.Register(c =>
        {
            var rebusTime = c.Get<IRebusTime>();
            var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();

            return new RabbitMqTimeoutManager(options, rebusLoggerFactory, rebusTime);
        });
    }
}