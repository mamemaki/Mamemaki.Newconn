using Mamemaki.Newconn.Features;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace Mamemaki.Newconn.Sockets;

public class SocketConnectionFactory<TConnection> : ConnectionFactory<TConnection>
    where TConnection : Connection, new()
{
    private readonly SocketConnectionFactoryOptions options;
    private readonly ILogger logger;
    private readonly MemoryPool<byte> memoryPool;
    private readonly PipeOptions inputPipeOptions;
    private readonly PipeOptions outputPipeOptions;
    private readonly SocketSenderPool socketSenderPool;

    public SocketConnectionFactoryOptions Options { get => options; }

    public SocketConnectionFactory(ILogger logger, SocketConnectionFactoryOptions? options_in = null)
    {
        this.logger = logger;
        this.options = options_in ?? new();
        memoryPool = options.MemoryPoolFactory.Create();

        var maxReadBufferSize = options.MaxReadBufferSize ?? 0;
        var maxWriteBufferSize = options.MaxWriteBufferSize ?? 0;

        // These are the same, it's either the thread pool or inline
        var applicationScheduler = options.UnsafePreferInlineScheduling ? PipeScheduler.Inline : PipeScheduler.ThreadPool;
        var transportScheduler = applicationScheduler;
        // https://github.com/aspnet/KestrelHttpServer/issues/2573
        var awaiterScheduler = OperatingSystem.IsWindows() ? transportScheduler : PipeScheduler.Inline;

        inputPipeOptions = new PipeOptions(memoryPool, applicationScheduler, transportScheduler,
            maxReadBufferSize, maxReadBufferSize / 2, minimumSegmentSize: 4 * 1024, useSynchronizationContext: false);
        outputPipeOptions = new PipeOptions(memoryPool, transportScheduler, applicationScheduler,
            maxWriteBufferSize, maxWriteBufferSize / 2, minimumSegmentSize: 4 * 1024, useSynchronizationContext: false);
        socketSenderPool = new SocketSenderPool(awaiterScheduler);
    }

    public override async ValueTask<TConnection> ConnectAsync(EndPoint? endpoint,
        IConnectionProperties? properties = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint, nameof(endpoint));

        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = options.NoDelay,
        };

        await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);

        TConnection? connection = null;
        try
        {
            var connNo = Options.ConnectionIdIssuer.IssueNewId();
            connection = CreateConnection(connNo, socket, logger, properties,
                outputPipeOptions, inputPipeOptions, inputPipeOptions.ReaderScheduler,
                memoryPool, socketSenderPool,
                options.WaitForDataBeforeAllocatingBuffer, options.MinAllocBufferSize);
            if (!await connection.FireOnConnectedAsync(Middlewares, cancellationToken))
            {
                var ex = new ConnectionAbortedException("Rejected");
                connection.Abort(ex);
                throw ex;
            }
            return connection;
        }
        catch (Exception)
        {
            if (connection != null)
                await connection.DisposeAsync();
            throw;
        }
    }

    public override ValueTask<ConnectionListener<TConnection>> BindAsync(EndPoint? endpoint, 
        IConnectionProperties? properties = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint, nameof(endpoint));

        Socket socket;
        try
        {
            socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (endpoint is IPEndPoint ip)
            {
                if (ip.Address.Equals(IPAddress.IPv6Any))
                {
                    socket.DualMode = true;
                }
            }
            socket.Bind(endpoint);
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            throw new Exception("AddressInUseException", e);
        }

        var listener = new SocketConnectionListener<TConnection>(logger, socket, properties, options, Middlewares);

        socket.Listen(options.Backlog);

        return ValueTask.FromResult<ConnectionListener<TConnection>>(listener);
    }

    internal static TConnection CreateConnection(long connNo, Socket socket, ILogger logger,
        IConnectionProperties? defaultProperties, PipeOptions sendPipeOptions, PipeOptions receivePipeOptions,
        PipeScheduler scheduler, MemoryPool<byte> memoryPool, SocketSenderPool socketSenderPool,
        bool waitForData = true, int minAllocBufferSize = 0)
    {
        var connection = new TConnection();

        var propertiesNew = new ConnectionProperties(defaultProperties);
        propertiesNew.Set(socket);

        var socketConnection = new SocketConnection(socket, connNo, 
            memoryPool, scheduler, logger, socketSenderPool,
            receivePipeOptions, sendPipeOptions, waitForData, minAllocBufferSize);
        socketConnection.Start();
        propertiesNew.Set<IConnectionLifetimeFeature>(socketConnection);
        propertiesNew.Set<IMemoryPoolFeature>(socketConnection);

        Connection.SetupConnection(connection,
            socketConnection,
            propertiesNew,
            socket.LocalEndPoint,
            socket.RemoteEndPoint,
            logger, socketConnection);

        return connection;
    }
}
