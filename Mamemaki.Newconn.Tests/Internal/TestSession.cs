using System.Buffers;

namespace Mamemaki.Newconn.Tests.Internal;

class TestSession(InMemoryConnection serverConnection, InMemoryConnection clientConnection) : IAsyncDisposable
{
    public InMemoryConnection ServerConnection { get; private set; } = serverConnection;
    public InMemoryConnection ClientConnection { get; private set; } = clientConnection;

    public async ValueTask DisposeAsync()
    {
        await ServerConnection.DisposeAsync();
        await ClientConnection.DisposeAsync();
    }

    public async ValueTask ServerSendAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        await ServerConnection.Transport.Output.WriteAsync(source, cancellationToken);
        ServerConnection.Transport.Output.Complete();
    }

    public async ValueTask<ReadOnlySequence<byte>> ServerReceiveAsync(CancellationToken cancellationToken = default)
    {
        var result = await ServerConnection.Transport.Input.ReadAsync(cancellationToken);
        return result.Buffer;
    }

    public async ValueTask ClientSendAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        await ClientConnection.Transport.Output.WriteAsync(source, cancellationToken);
        ClientConnection.Transport.Output.Complete();
    }

    public async ValueTask<ReadOnlySequence<byte>> ClientReceiveAsync(CancellationToken cancellationToken = default)
    {
        var result = await ClientConnection.Transport.Input.ReadAsync(cancellationToken);
        return result.Buffer;
    }
}
