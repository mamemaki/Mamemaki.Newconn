using Mamemaki.Newconn.Servers;
using Mamemaki.Newconn.Tests.Internal;
using System.Text;

namespace Mamemaki.Newconn.Tests.Features;

public class ConnectionLimitTests
{
    TestServer CreateServer(Action<TestServerOptions> configure)
    {
        var options = new TestServerOptions();
        configure.Invoke(options);
        return new TestServer(options);
    }

    async Task<TestSession> CreateSessionAndHelloAsync(TestServer server)
    {
        var session = await server.CreateSessionAsnc();
        await session.ServerSendAsync("Hello"u8.ToArray());
        var buffer = await session.ClientReceiveAsync();
        var text = Encoding.UTF8.GetString(buffer);
        Assert.Equal("Hello", text);
        return session;
    }

    [Fact]
    public async Task Limit_3()
    {
        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseConnectionLimit(3);
        }))
        {
            await using var session1 = await CreateSessionAndHelloAsync(server);
            await using var session2 = await CreateSessionAndHelloAsync(server);
            await using var session3 = await CreateSessionAndHelloAsync(server);

            var ex = await Assert.ThrowsAsync<ConnectionAbortedException>(async () =>
            {
                await server.CreateSessionAsnc();
            });
            Assert.Equal("Rejected", ex.Message);

            await session2.DisposeAsync();
            await using var session4 = await CreateSessionAndHelloAsync(server);
        }
    }

    [Fact]
    public async Task Limit_0()
    {
        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseConnectionLimit(0);
        }))
        {
            var ex = await Assert.ThrowsAsync<ConnectionAbortedException>(async () =>
            {
                await server.CreateSessionAsnc();
            });
            Assert.Equal("Rejected", ex.Message);
        }
    }

    [Fact]
    public async Task Unlimited()
    {
        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseConnectionLimit(null);
        }))
        {
            await using var session1 = await CreateSessionAndHelloAsync(server);
            await using var session2 = await CreateSessionAndHelloAsync(server);
            await using var session3 = await CreateSessionAndHelloAsync(server);
        }
    }
}
