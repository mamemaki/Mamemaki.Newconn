using Mamemaki.Newconn.Features;
using System.Buffers;
using System.IO.Pipelines;

namespace Mamemaki.Newconn;

/// <summary>
/// Represents a transport connection.
/// </summary>
public abstract class TransportConnection : IConnectionLifetimeFeature, IMemoryPoolFeature
{
    /// <summary>
    /// Gets the connection unique number.
    /// </summary>
    public long ConnectionNo { get; protected set; }

    /// <summary>
    /// Triggered when the client connection is closed.
    /// </summary>
    public CancellationToken ConnectionClosed { get; set; }

    public virtual MemoryPool<byte> MemoryPool { get; protected set; } = default!;

    public IDuplexPipe Transport { get; protected set; } = default!;

    public IDuplexPipe Application { get; set; } = default!;

    public bool IsShutdown => ShutdownKind != PipeShutdownKind.None;

    /// <summary>
    /// When possible, determines how the pipe first reached a close state
    /// </summary>
    public virtual PipeShutdownKind ShutdownKind { get; }

    public virtual int ShutdownErrorCode { get; }

    public void Abort() =>
        Abort(new ConnectionAbortedException("The connection was aborted by the application via Abort()."));

    public virtual void Abort(ConnectionAbortedException abortReason)
    {
        throw new NotImplementedException();
    }
}
