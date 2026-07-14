using Mamemaki.Newconn;
using Mamemaki.Newconn.Clients;
using System.Net;

namespace Benchmarks.Frameworks.Newconn;

internal class NewconnFramework : IFramework
{
    private readonly BenchmarkConfiguration config;
    private readonly ConnectionFactory<EchoClient> connectionFactory;

    public NewconnFramework(BenchmarkConfiguration config)
    {
        this.config = config;
        connectionFactory = new ConnectionFactoryBuilder<EchoClient>()
            .Build();
    }

    public IEchoServer CreateEchoServer()
    {
        return new EchoServer(config);
    }

    public async Task<IEchoClient> ConnectEchoClientAsync(int id, IPEndPoint ep, CancellationToken cancellationToken)
    {
        var client = await connectionFactory.ConnectAsync(ep, null, cancellationToken);
        client.Id = id;

        Console.WriteLine($"Client[{id}] connected");
        return client;
    }
}
