using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks.Frameworks.Kestrel;

internal class EchoServer : IEchoServer
{
    private readonly IWebHost host;

    public EchoServer(BenchmarkConfiguration config)
        : base()
    {
        host = WebHost.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddOptions<SocketTransportOptions>()
                    .Configure(options =>
                    {
                        //options.UnsafePreferInlineScheduling = true;
                        //options.MemoryPoolFactory = System.Buffers.PinnedBlockMemoryPoolFactory.Create;
                    });
            })
            .UseKestrel(options =>
            {
                options.ListenAnyIP(config.Port, builder =>
                {
                    builder.Run(OnConnectedAsync);
                });
            })
            .UseStartup<Startup>()
            .Build();
    }

    public class Startup
    {
        public void Configure()
        {
        }
    }

    public Task OnConnectedAsync(ConnectionContext connection)
    {
        return connection.Transport.Input.CopyToAsync(connection.Transport.Output);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await host.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await host.StopAsync(cancellationToken);
    }
}
