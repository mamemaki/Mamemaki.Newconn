using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Features.Heartbeats;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace Mamemaki.Newconn.Servers;

internal class NewconnListener<TConnection> : INewconnListener, IHeartbeatHandler
    where TConnection : Connection
{
    private readonly ILogger logger;
    private readonly ServerBindingOptions options;
    private readonly ConnectionListener<TConnection> connectionListener;
    private readonly ConnectionDelegate<TConnection> connectionHandler;

    private bool stopped;
    private Task? listeningTask;
    private readonly CancellationTokenSource listeningCts = new();
    internal readonly ConnectionManager ConnectionManager;

    public virtual EndPoint? LocalEndPoint { get => connectionListener.LocalEndPoint; }

    public NewconnListener(ILogger logger,
        ServerBindingOptions options,
        ConnectionListener<TConnection> connectionListener,
        ConnectionDelegate<TConnection> connectionHandler)
    {
        this.logger = logger;
        this.options = options;
        this.connectionListener = connectionListener;
        this.connectionHandler = connectionHandler;

        ConnectionManager = options.ConnectionManager ?? new ConnectionManager(logger);
    }

    public void OnHeartbeat()
    {
        ConnectionManager.OnHeartbeat();
    }

    public void Start()
    {
        if (listeningTask != null)
            throw new Exception("already listening");
        listeningTask = ListeningAsync(listeningCts.Token);
    }

    protected virtual async Task ListeningAsync(CancellationToken cancellationToken)
    {
        try
        {
            var serverContext = connectionListener.Properties.Get<IServerContext>();
            await foreach (var connection in connectionListener.AcceptManyAsync(cancellationToken))
            {
                if (serverContext.Heartbeat != null)
                    connection.Properties.Set<IHeartbeat>(serverContext.Heartbeat);
                connection.Properties.Set<IConnectionMetricsFeature>(serverContext);

                ConnectionManager.AddConnection(connection);
                serverContext.Metrics.ConnectionQueuedStart(connection);

                ThreadPool.UnsafeQueueUserWorkItem(connection =>
                {
                    connection.ExecutionTask = OnConnectionAcceptedAsync(connection, serverContext, cancellationToken);
                }, connection, preferLocal: false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Stopped accepting connections on {endpoint}", connectionListener.LocalEndPoint);
            return;
        }
    }

    protected virtual IDisposable? BeginConnectionScope(Connection connection)
    {
        if (logger.IsEnabled(LogLevel.Critical))
        {
            return logger.BeginScope(connection);
        }

        return null;
    }

    protected virtual async Task OnConnectionAcceptedAsync(TConnection connection,
        IServerContext serverContext, CancellationToken cancellationToken)
    {
        var startTimestamp = 0L;
        ConnectionMetricsTagsFeature? metricsTagsFeature = null;
        Exception? unhandledException = null;

        if (serverContext.Metrics.ConnectionDurationEnabled)
        {
            metricsTagsFeature = new ConnectionMetricsTagsFeature();
            connection.Properties.Set<IConnectionMetricsTagsFeature>(metricsTagsFeature);

            startTimestamp = Stopwatch.GetTimestamp();
        }

        if (serverContext.Heartbeat != null)
            connection.Properties.Set<IConnectionHeartbeatFeature>(connection);

        try
        {
            serverContext.Metrics.ConnectionQueuedStop(connection);
            serverContext.Metrics.ConnectionStart(connection);

            using var scope = BeginConnectionScope(connection);

            if (!await connection.FireOnConnectedAsync(connectionListener.Middlewares, cancellationToken))
            {
                serverContext.Metrics.ConnectionRejected(connection);
                connection.Abort(new ConnectionAbortedException("Rejected"));
                return;
            }

            try
            {
                await connectionHandler(connection, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await connection.OnDisconnectedAsync().ConfigureAwait(false);
            }
        }
        catch (ConnectionAbortedException)
        {
            // Don't let connection aborted exceptions out
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred at connection({ConnectionId})",
                connection.ConnectionId);
            unhandledException = ex;
        }
        finally
        {
            var currentTimestamp = 0L;
            if (serverContext.Metrics.ConnectionDurationEnabled)
                currentTimestamp = Stopwatch.GetTimestamp();
            serverContext.Metrics.ConnectionStop(connection, unhandledException,
                metricsTagsFeature?.TagsList, startTimestamp, currentTimestamp);

            await connection.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            // Remove the connection from tracking
            ConnectionManager.RemoveConnection(connection.ConnectionNo);
        }
    }

    public virtual async ValueTask StopAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (stopped) return;
        stopped = true;

        listeningCts.Cancel();
        await connectionListener.DisposeAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);

        if (listeningTask == null)
            return;

        // Give connections a chance to close gracefully
        var tasks = new List<Task>(ConnectionManager.Count);
        tasks.Add(listeningTask);
        ConnectionManager.Walk(connection =>
        {
            tasks.Add(connection.CloseAsync(ConnectionEndReason.ServerShutdown, cancellationToken).AsTask());
            tasks.Add(connection.ExecutionTask!);
        });

        if (!await Task.WhenAll(tasks).WaitAsync(cancellationToken).TimeoutAfter(timeout).ConfigureAwait(false))
        {
            // Abort all connections still in flight
            tasks.Clear();
            tasks.Add(listeningTask);
            ConnectionManager.Walk(connection =>
            {
                tasks.Add(connection.CloseAsync(ConnectionEndReason.ServerShutdown, cancellationToken).AsTask());
                tasks.Add(connection.ExecutionTask!);
            });
            await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class ConnectionMetricsTagsFeature : IConnectionMetricsTagsFeature
    {
        ICollection<KeyValuePair<string, object?>> IConnectionMetricsTagsFeature.Tags => TagsList;

        public List<KeyValuePair<string, object?>> TagsList { get; } = new List<KeyValuePair<string, object?>>();
    }
}
