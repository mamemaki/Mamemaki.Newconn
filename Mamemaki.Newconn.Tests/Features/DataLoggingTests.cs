using Mamemaki.Newconn.Tests.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Mamemaki.Newconn.Tests.Features;

public class DataLoggingTests
{
    TestServer CreateServer(Action<TestServerOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(config => config.AddInMemory().SetMinimumLevel(LogLevel.Trace));
        var serviceProvider = services.BuildServiceProvider();

        var options = new TestServerOptions(serviceProvider);
        configure.Invoke(options);
        return new TestServer(options);
    }

    [Fact]
    public async Task WithDefaults()
    {
        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseDataLogging();
            options.MiddlewaresOnClient.UseDataLogging();
        }))
        {
            await using (var session = await server.CreateSessionAsnc())
            {
                await session.ServerSendAsync("Hello"u8.ToArray());

                var buffer = await session.ClientReceiveAsync();
                var text = Encoding.UTF8.GetString(buffer);
                Assert.Equal("Hello", text);
            }

            var inMemLogger = server.Options.ServiceProvider.GetRequiredService<InMemoryLogger>();
            var lines = inMemLogger.RecordedDebugLogs.Select(s => s.Message).ToList();
            Assert.Equal(2, lines.Count);
            Assert.Equal("WriteAsync[5]\n48 65 6C 6C 6F                                     Hello", lines[0]);
            Assert.Equal("ReadAsync[5]\n48 65 6C 6C 6F                                     Hello", lines[1]);
        }
    }

    [Fact]
    public async Task WithFormatter()
    {
#pragma warning disable CA2254
        static void formatter(ILogger logger, string method, ReadOnlySpan<byte> buffer) =>
            logger.LogTrace($"{method}: {Encoding.UTF8.GetString(buffer)}");
#pragma warning restore CA2254

        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseDataLogging(formatter);
            options.MiddlewaresOnClient.UseDataLogging(formatter);
        }))
        {
            await using (var session = await server.CreateSessionAsnc())
            {
                await session.ServerSendAsync("Hello"u8.ToArray());

                var buffer = await session.ClientReceiveAsync();
                var text = Encoding.UTF8.GetString(buffer);
                Assert.Equal("Hello", text);
            }

            var inMemLogger = server.Options.ServiceProvider.GetRequiredService<InMemoryLogger>();
            var lines = inMemLogger.RecordedTraceLogs.Select(s => s.Message).ToList();
            Assert.Equal(2, lines.Count);
            Assert.Equal("WriteAsync: Hello", lines[0]);
            Assert.Equal("ReadAsync: Hello", lines[1]);
        }
    }
}
