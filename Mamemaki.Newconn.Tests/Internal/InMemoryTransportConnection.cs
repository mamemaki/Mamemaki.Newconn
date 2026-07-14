using Mamemaki.Newconn.Internal;
using System.Buffers;

namespace Mamemaki.Newconn.Tests.Internal;

class InMemoryTransportConnection : TransportConnection, IAsyncDisposable
{
    public InMemoryTransportConnection(DuplexPipe.DuplexPipePair pair, bool isServer)
    {
        MemoryPool = MemoryPool<byte>.Shared;
        Transport = isServer ? pair.Transport : pair.Application;
        Application = isServer ? pair.Application : pair.Transport;
    }

    private PipeShutdownKind shutdownKind;
    public override PipeShutdownKind ShutdownKind => shutdownKind;

    public override void Abort(ConnectionAbortedException abortReason)
    {
        Transport.Input.CancelPendingRead();
        shutdownKind = PipeShutdownKind.ReadSocketAborted;
    }

    public ValueTask DisposeAsync()
    {
        Transport.Input.Complete();
        Transport.Output.Complete();
        Application.Output.Complete();
        Application.Input.Complete();
        return default;
    }
}
