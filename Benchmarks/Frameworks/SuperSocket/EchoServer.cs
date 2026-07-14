using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Benchmarks.Frameworks.SuperSocket;

internal class EchoServer : IEchoServer
{
    private readonly IHost host;

    public EchoServer(BenchmarkConfiguration config)
        : base()
    {
        host = SuperSocketHostBuilder.Create<TextPackageInfo, LinePipelineFilter>()
            .ConfigureSuperSocket(options =>
            {
                options.Name = "Echo Server";
                options.AddListener(new ListenOptions()
                {
                    Ip = "Any",
                    Port = config.Port
                });
            })
            .UsePackageHandler(OnPackageAsync)
            .Build();
    }

    public ValueTask OnPackageAsync(IAppSession session, TextPackageInfo package)
    {
        return session.SendAsync(Encoding.UTF8.GetBytes(package.Text + "\r\n"));
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
