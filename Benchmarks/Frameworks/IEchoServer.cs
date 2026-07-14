namespace Benchmarks.Frameworks;

internal interface IEchoServer
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
