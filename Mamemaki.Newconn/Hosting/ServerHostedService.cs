using Mamemaki.Newconn.Servers;
using Microsoft.Extensions.Hosting;

namespace Mamemaki.Newconn.Hosting;

/// <summary>
/// Represents a server hosted service.
/// </summary>
/// <param name="options"></param>
public class ServerHostedService(NewconnServerOptions options) : IHostedService
{
    private readonly NewconnServer server = new NewconnServer(options);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return server.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return server.StopAsync(cancellationToken);
    }
}
