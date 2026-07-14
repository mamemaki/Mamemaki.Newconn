using Mamemaki.Newconn;
using Mamemaki.Newconn.Hosting;
using Microsoft.Extensions.Hosting;

namespace Benchmarks.Frameworks.Newconn;

internal class EchoServer : IEchoServer
{
    private readonly IHost host;

    public EchoServer(BenchmarkConfiguration config)
        : base()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.UseNewconnServer(server =>
        {
            server.SocketListenAnyIP<Connection>(config.Port, builder =>
            {
                builder.Run(OnConnectionAsync);
            });
        });
        host = builder.Build();
    }

    protected Task OnConnectionAsync(Connection connection, CancellationToken cancellationToken)
    {
        return connection.Transport.Input.CopyToAsync(connection.Transport.Output, cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return host.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return host.StopAsync(cancellationToken);
    }
}
