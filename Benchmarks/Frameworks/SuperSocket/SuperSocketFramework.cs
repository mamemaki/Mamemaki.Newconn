using System.Net;

namespace Benchmarks.Frameworks.SuperSocket;

internal class SuperSocketFramework : IFramework
{
    private readonly BenchmarkConfiguration config;

    public SuperSocketFramework(BenchmarkConfiguration config)
    {
        this.config = config;
    }

    public IEchoServer CreateEchoServer()
    {
        return new EchoServer(config);
    }

    public Task<IEchoClient> ConnectEchoClientAsync(int id, IPEndPoint ep, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
