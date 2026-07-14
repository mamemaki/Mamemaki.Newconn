namespace Benchmarks.Frameworks;

internal interface IEchoClient : IDisposable
{
    int? Id { get; }

    ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken);
}
