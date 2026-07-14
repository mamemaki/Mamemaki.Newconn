using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Internal;
using Mamemaki.Newconn.Servers;
using Mamemaki.Newconn.Tests.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.Metrics;

namespace Mamemaki.Newconn.Tests.Features;

public class ConnectionTimeoutTests
{
    TestServer CreateServer(Action<TestServerOptions> configure)
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<IMeterFactory>(new DefaultMeterFactory());
        services.TryAddSingleton<NewconnMetrics>();
        var serviceProvider = services.BuildServiceProvider();

        var options = new TestServerOptions(serviceProvider);
        configure.Invoke(options);
        return new TestServer(options);
    }

    [Fact]
    public async Task Timeout()
    {
        await using (var server = CreateServer(options =>
        {
            options.UseHeartbeat = true;
            options.HeartbeatInterval = TimeSpan.FromMilliseconds(20);
            options.MiddlewaresOnServer.UseConnectionTimeout();
        }))
        {
            await using (var session = await server.CreateSessionAsnc())
            {
                session.ServerConnection.Properties.Get<IConnectionTimeoutFeature>().SetTimeout(TimeSpan.FromMilliseconds(100));
                await session.ServerSendAsync("Hello"u8.ToArray());
                var result = await session.ServerConnection.Transport.Input.ReadAsync();
                Assert.True(result.IsCanceled);
            }
        }
    }
}
