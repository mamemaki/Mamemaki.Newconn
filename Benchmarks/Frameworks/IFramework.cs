using Benchmarks.Frameworks.Kestrel;
using Benchmarks.Frameworks.Newconn;
using Benchmarks.Frameworks.SuperSocket;
using System.Net;

namespace Benchmarks.Frameworks;

internal interface IFramework
{
    IEchoServer CreateEchoServer();

    Task<IEchoClient> ConnectEchoClientAsync(int id, IPEndPoint ep, CancellationToken cancellationToken);

    public static IFramework Create(FrameworkType type, BenchmarkConfiguration config)
    {
        return type switch
        {
            FrameworkType.Newconn => new NewconnFramework(config),
            FrameworkType.Kestrel => new KestrelFramework(config),
            FrameworkType.SuperSocket => new SuperSocketFramework(config),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}
