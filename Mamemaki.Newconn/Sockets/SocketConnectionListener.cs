using Mamemaki.Newconn.Internal;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace Mamemaki.Newconn.Sockets;

/// <summary>
/// Represents a socket connection listener that accepts connections.
/// </summary>
/// <typeparam name="TConnection">The type of connection.</typeparam>
public class SocketConnectionListener<TConnection> : ConnectionListener<TConnection>
    where TConnection : Connection, new()
{
    public override EndPoint? LocalEndPoint { get; }

    private sealed class QueueSettings
    {
        public PipeScheduler Scheduler { get; init; } = default!;
        public PipeOptions InputOptions { get; init; } = default!;
        public PipeOptions OutputOptions { get; init; } = default!;
        public SocketSenderPool SocketSenderPool { get; init; } = default!;
        public MemoryPool<byte> MemoryPool { get; init; } = default!;
    }

    private readonly ILogger logger;
    private readonly SocketConnectionFactoryOptions options;

    private readonly Socket socket;
    private readonly int settingsCount;
    private readonly QueueSettings[] settings;
    private long settingsIndex;    // long to prevent overflow

    public SocketConnectionListener(ILogger logger, Socket socket, IConnectionProperties? properties,
        SocketConnectionFactoryOptions options, IList<ConnectionMiddlewareDelegate> middlewares)
    {
        this.logger = logger;
        this.socket = socket;
        this.options = options;
        this.Properties = properties ?? new ConnectionProperties();
        this.Middlewares = middlewares;

        if (socket.LocalEndPoint == null)
            throw new Exception("socket.LocalEndPoint is null");
        LocalEndPoint = socket.LocalEndPoint;

        var maxReadBufferSize = options.MaxReadBufferSize ?? 0;
        var maxWriteBufferSize = options.MaxWriteBufferSize ?? 0;
        var applicationScheduler = options.UnsafePreferInlineScheduling ?
            PipeScheduler.Inline : PipeScheduler.ThreadPool;

        this.settingsCount = options.IOQueueCount;
        if (settingsCount > 0)
        {
            settings = new QueueSettings[settingsCount];

            for (var i = 0; i < settingsCount; i++)
            {
                var memoryPool = options.MemoryPoolFactory.Create();
                var transportScheduler = options.UnsafePreferInlineScheduling ?
                    PipeScheduler.Inline : new IOQueue();

                settings[i] = new QueueSettings()
                {
                    Scheduler = transportScheduler,
                    InputOptions = new PipeOptions(memoryPool, applicationScheduler, transportScheduler,
                        maxReadBufferSize, maxReadBufferSize / 2, minimumSegmentSize: 4 * 1024, useSynchronizationContext: false),
                    OutputOptions = new PipeOptions(memoryPool, transportScheduler, applicationScheduler,
                        maxWriteBufferSize, maxWriteBufferSize / 2, minimumSegmentSize: 4 * 1024, useSynchronizationContext: false),
                    SocketSenderPool = new SocketSenderPool(PipeScheduler.Inline),
                    MemoryPool = memoryPool,
                };
            }
        }
        else
        {
            var memoryPool = options.MemoryPoolFactory.Create();
            var transportScheduler = options.UnsafePreferInlineScheduling ? PipeScheduler.Inline : PipeScheduler.ThreadPool;

            settings =
            [
                new QueueSettings()
                {
                    Scheduler = transportScheduler,
                    InputOptions = new PipeOptions(memoryPool, applicationScheduler, transportScheduler,
                        maxReadBufferSize, maxReadBufferSize / 2, minimumSegmentSize: 4 * 1024, useSynchronizationContext: false),
                    OutputOptions = new PipeOptions(memoryPool, transportScheduler, applicationScheduler,
                        maxWriteBufferSize, maxWriteBufferSize / 2, minimumSegmentSize: 4 * 1024, useSynchronizationContext: false),
                    SocketSenderPool = new SocketSenderPool(PipeScheduler.Inline),
                    MemoryPool = memoryPool,
                }
            ];
            settingsCount = 1;
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        socket.Dispose();

        foreach (var setting in settings)
        {
            setting.SocketSenderPool.Dispose();
            setting.MemoryPool.Dispose();
        }

        return default;
    }

    public override async ValueTask<TConnection?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Socket acceptSocket;
            TConnection? connection = null;
            try
            {
                acceptSocket = await socket.AcceptAsync(cancellationToken);

                // Only apply no delay to Tcp based endpoints
                if (acceptSocket.LocalEndPoint is IPEndPoint)
                {
                    acceptSocket.NoDelay = options.NoDelay;
                }

                var setting = settings[Interlocked.Increment(ref settingsIndex) % settingsCount];
                var connNo = options.ConnectionIdIssuer.IssueNewId();
                connection = SocketConnectionFactory<TConnection>.CreateConnection(
                    connNo, acceptSocket, logger, Properties,
                    setting.OutputOptions, setting.InputOptions,
                    setting.SocketSenderPool.Scheduler, setting.MemoryPool, setting.SocketSenderPool,
                    options.WaitForDataBeforeAllocatingBuffer, options.MinAllocBufferSize);

                return connection;
            }
            catch (ObjectDisposedException)
            {
                // A call was made to UnbindAsync/DisposeAsync just return null which signals we're done
                if (connection != null)
                    await connection.DisposeAsync();
                return null;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.OperationAborted)
            {
                // A call was made to UnbindAsync/DisposeAsync just return null which signals we're done
                if (connection != null)
                    await connection.DisposeAsync();
                return null;
            }
            catch (SocketException)
            {
                // The connection got reset while it was in the backlog, so we try again.
                NewconnLog.ConnectionResetWhileInBacklog(logger);
                if (connection != null)
                    await connection.DisposeAsync();
            }
            catch (Exception)
            {
                if (connection != null)
                    await connection.DisposeAsync();
                throw;
            }
        }
    }
}
