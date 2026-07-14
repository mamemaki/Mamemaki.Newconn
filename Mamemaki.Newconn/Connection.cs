using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipelines;
using System.Net;

namespace Mamemaki.Newconn;

/// <summary>
/// The Connection middleware delegate
/// </summary>
/// <param name="connection"></param>
/// <param name="cancellationToken"></param>
/// <returns>Return false means that the connection to be rejected. otherwise true.</returns>
public delegate Task<bool> ConnectionMiddlewareDelegate(Connection connection, CancellationToken cancellationToken);

/// <summary>
/// Represents a connection.
/// 
/// WARNING: All members becomes available after OnConnectedAsync is called.
/// </summary>
public class Connection : 
    IConnectionStateFeature, 
    IConnectionIdFeature, 
    IConnectionHeartbeatFeature, 
    IAsyncDisposable, 
    IDisposable
{
    /// <summary>
    /// Gets the connection unique number.
    /// </summary>
    public long ConnectionNo => TransportConnection.ConnectionNo;

    /// <summary>
    /// Gets the connection identifier.
    /// The inherited class can override this. Otherwise, it returns <see cref="ConnectionNo"/> as a string.
    /// </summary>
    public string ConnectionId { get; protected set; } = default!;

    /// <summary>
    /// Gets the collection of property provided by the server, client or middleware available on this connection.
    /// The properties may include connection features or options.
    /// </summary>
    public IConnectionProperties Properties { get; private set; } = ThrowConnectionProperties.Instance;

    /// <summary>
    /// Gets or sets the local endpoint for this connection.
    /// </summary>
    public virtual EndPoint? LocalEndPoint { get; set; }

    /// <summary>
    /// Gets or sets the remote endpoint for this connection.
    /// </summary>
    public virtual EndPoint? RemoteEndPoint { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IDuplexPipe"/> that can be used to read or write data on this connection.
    /// </summary>
    public IDuplexPipe Transport { get; set; } = ThrowDuplexPipe.Instance;

    internal Task? ExecutionTask;
    internal TransportConnection TransportConnection = default!;

    protected ILogger logger = NullLogger.Instance;

    private readonly List<object> disposalObjects = [];

    /// <summary>
    /// Register an object to the disposal objects
    /// The disposal objects will be disposed when connection close.
    /// Dispositions are performed in the reverse order they are registered.
    /// </summary>
    /// <param name="obj">An object to be dispose.</param>
    public void RegisterDisposalObject(IAsyncDisposable obj)
    {
        disposalObjects.Add(obj);
    }

    /// <summary>
    /// Register an object to the disposal objects
    /// The disposal objects will be disposed when connection close.
    /// Dispositions are performed in the reverse order they are registered.
    /// </summary>
    /// <param name="obj">An object to be dispose.</param>
    public void RegisterDisposalObject(IDisposable obj)
    {
        disposalObjects.Add(obj);
    }

    private List<(Action<object> handler, object state)>? heartbeatHandlers;
    private readonly object heartbeatLock = new object();

    private bool closed;

    /// <summary>
    /// Gets whether the connection is closed.
    /// </summary>
    public bool IsClosed => closed || TransportConnection.IsShutdown;

    /// <summary>
    /// This is called when a new connection is established.
    /// You can override this and initialize the connection in this method.
    /// Remember, the connection is not initialized at the constructor, it is initialized when this method is called.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    protected virtual ValueTask OnConnectedAsync()
    {
        logger.LogInformation("Connection({ConnectionId}, {RemoteEndPoint}) connected", ConnectionId, RemoteEndPoint);
        NewconnLog.ConnectionConnected(logger, ConnectionNo);
        return default;
    }

    /// <summary>
    /// This is called when the connection is disconnected.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    internal protected virtual ValueTask OnDisconnectedAsync()
    {
        return default;
    }

    /// <summary>
    /// Aborts the connection.
    /// </summary>
    /// <param name="abortReason">An abort reason exception that will be pass to the <see cref="Transport.Input.Complete"/>, if any.</param>
    public void Abort(ConnectionAbortedException? abortReason = null)
    {
        abortReason ??= new ConnectionAbortedException("The connection was aborted.");

        TransportConnection.Abort(abortReason);
    }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    /// <param name="connectionEndReason">A connection end reason, if any.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    public async ValueTask CloseAsync(ConnectionEndReason? connectionEndReason = null,
        CancellationToken cancellationToken = default)
    {
        if (!closed)
        {
            closed = true;
            await CloseAsyncCore(connectionEndReason, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    /// <param name="connectionEndReason">A connection end reason, if any.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    protected virtual async ValueTask<ConnectionEndReason> CloseAsyncCore(
        ConnectionEndReason? connectionEndReason, CancellationToken cancellationToken)
    {
        await DisposeDisposalObjectsAsync();

        var closedReason = connectionEndReason ?? DetermineEndReason();
        logger.LogInformation("Connection({ConnectionId}) closed due to {closedReason}", ConnectionId, closedReason);
        NewconnLog.ConnectionClosed(logger, ConnectionNo, closedReason);
        return closedReason;
    }

    private ConnectionEndReason DetermineEndReason()
    {
        return TransportConnection.ShutdownKind switch
        {
            PipeShutdownKind.None => ConnectionEndReason.Unknown,
            PipeShutdownKind.PipeDisposed => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.ReadEndOfStream => ConnectionEndReason.RemoteClosing,
            PipeShutdownKind.ReadDisposed => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.ReadIOException => ConnectionEndReason.TransportError,
            PipeShutdownKind.ReadException => ConnectionEndReason.TransportError,
            PipeShutdownKind.ReadSocketError => ConnectionEndReason.TransportError,
            PipeShutdownKind.ReadSocketReset => ConnectionEndReason.RemoteClosing,
            PipeShutdownKind.ReadSocketAborted => ConnectionEndReason.RemoteClosing,
            PipeShutdownKind.ReadFlushCompleted => ConnectionEndReason.RemoteClosing,
            PipeShutdownKind.ReadFlushCanceled => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.WriteEndOfStream => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.WriteDisposed => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.WriteIOException => ConnectionEndReason.TransportError,
            PipeShutdownKind.WriteException => ConnectionEndReason.TransportError,
            PipeShutdownKind.WriteSocketError => ConnectionEndReason.TransportError,
            PipeShutdownKind.WriteSocketReset => ConnectionEndReason.RemoteClosing,
            PipeShutdownKind.WriteSocketAborted => ConnectionEndReason.RemoteClosing,
            PipeShutdownKind.InputReaderCompleted => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.InputWriterCompleted => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.OutputReaderCompleted => ConnectionEndReason.LocalClosing,
            PipeShutdownKind.OutputWriterCompleted => ConnectionEndReason.LocalClosing,
            _ => ConnectionEndReason.Unknown,
        };
    }

    /// <summary>
    /// Disposes disposal objects
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    protected virtual async ValueTask DisposeDisposalObjectsAsync()
    {
        foreach (var obj in disposalObjects.Reverse<object>())
        {
            try
            {
                if (obj is IAsyncDisposable objAsyncDisposable)
                {
                    await objAsyncDisposable.DisposeAsync();
                }
                else if (obj is IDisposable objDisposable)
                {
                    objDisposable.Dispose();
                }
                else
                {
                    logger.LogError("The disposable object({obj}) is not disposable.", obj);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispose object({obj})", obj);
            }
        }
    }

    /// <summary>
    /// Disposes of the connection.
    /// </summary>
    /// <remarks>
    /// This is equivalent to calling <see cref="CloseAsync()"/>, and calling GetAwaiter().GetResult() on the resulting task.
    /// </remarks>
    public void Dispose()
    {
        var t = CloseAsync();

        if (t.IsCompleted) t.GetAwaiter().GetResult();
        else t.AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the connection.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    /// <remarks>This is equivalent to calling <see cref="CloseAsync()"/>.</remarks>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    internal void TickHeartbeat()
    {
        if (heartbeatHandlers == null)
            return;

        lock (heartbeatLock)
        {
            foreach (var (handler, state) in heartbeatHandlers)
            {
                handler(state);
            }
        }
    }

    void IConnectionHeartbeatFeature.OnHeartbeat(Action<object> action, object state)
    {
        lock (heartbeatLock)
        {
            if (heartbeatHandlers == null)
            {
                heartbeatHandlers = new List<(Action<object> handler, object state)>();
            }

            heartbeatHandlers.Add((action, state));
        }
    }

    internal async Task<bool> FireOnConnectedAsync(IList<ConnectionMiddlewareDelegate>? middlewares,
        CancellationToken cancellationToken)
    {
        if (middlewares != null)
        {
            foreach (var middleware in middlewares)
            {
                if (!await middleware(this, cancellationToken))
                {
                    return false;
                }
            }
        }

        await OnConnectedAsync();
        return true;
    }

    /// <summary>
    /// Setup the connection.
    /// </summary>
    /// <param name="connection">A connection to setup.</param>
    /// <param name="transportConnection">A transport connection.</param>
    /// <param name="properties">Properties for the connection.
    /// Do not share it with other connections. Otherwise, inconsistencies will occur.</param>
    /// <param name="localEndPoint">Local endpoint for the connection if any.</param>
    /// <param name="remoteEndPoint">Remote endpoint for the connection if any.</param>
    /// <param name="logger">A logger for the connection.</param>
    /// <param name="disposableObjects">Disposal objects for the connection.</param>
    public static void SetupConnection(Connection connection, 
        TransportConnection transportConnection,
        IConnectionProperties? properties = null,
        EndPoint? localEndPoint = null,
        EndPoint? remoteEndPoint = null,
        ILogger? logger = null,
        params object[] disposableObjects)
    {
        connection.ConnectionId = transportConnection.ConnectionNo.ToString();
        connection.TransportConnection = transportConnection;
        connection.Transport = transportConnection.Transport;
        connection.Properties = properties ?? new ConnectionProperties();
        if (localEndPoint != null)
            connection.LocalEndPoint = localEndPoint;
        if (remoteEndPoint != null)
            connection.RemoteEndPoint = remoteEndPoint;
        if (disposableObjects  != null)
            connection.disposalObjects.AddRange(disposableObjects);
        if (logger != null)
            connection.logger = logger;
    }
}
