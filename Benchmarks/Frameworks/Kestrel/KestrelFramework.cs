using System.Net;

namespace Benchmarks.Frameworks.Kestrel;

internal class KestrelFramework : IFramework
{
    private readonly BenchmarkConfiguration config;

    public KestrelFramework(BenchmarkConfiguration config)
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
