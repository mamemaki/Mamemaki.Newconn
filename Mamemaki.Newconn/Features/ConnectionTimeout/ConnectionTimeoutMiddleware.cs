using Mamemaki.Newconn.Internal;
using Mamemaki.Newconn.Servers;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Mamemaki.Newconn.Features.ConnectionTimeout;

internal sealed class ConnectionTimeoutMiddleware : IConnectionTimeoutFeature
{
    private readonly ILogger logger;
    private readonly NewconnMetrics metrics;
    private readonly TimeProvider timeProvider;

    private long heartbeatIntervalTicks;
    private long lastTimestamp;
    private long timeoutTimestamp = long.MaxValue;
    public Action<Connection>? OnTimeout;

    public ConnectionTimeoutMiddleware(
        ILogger logger, 
        NewconnMetrics metrics,
        TimeProvider timeProvider)
    {
        this.logger = logger;
        this.metrics = metrics;
        this.timeProvider = timeProvider;
    }

    public Task<bool> OnConnectionAsync(Connection connection, CancellationToken _)
    {
        var serverContext = connection.Properties.Get<IServerContext>();
        if (serverContext.Heartbeat == null)
            throw new InvalidOperationException("No Heartbeat configured");
        heartbeatIntervalTicks = serverContext.Heartbeat.Interval.ToTicks(timeProvider);
        Interlocked.Exchange(ref lastTimestamp, timeProvider.GetTimestamp());

        var connectionHeartbeat = connection.Properties.Get<IConnectionHeartbeatFeature>();
        connectionHeartbeat.OnHeartbeat(connection =>
        {
            var timestamp = timeProvider.GetTimestamp();
            Tick((Connection)connection, timestamp);
        }, connection);

        connection.Properties.Set<IConnectionTimeoutFeature>(this);

        return Task.FromResult(true);
    }

    public bool SetTimeout(TimeSpan timeout, Action<Connection>? onTimeout = null)
    {
        if (timeoutTimestamp != long.MaxValue)
            return false;

        OnTimeout = onTimeout ?? (connection =>
        {
            NewconnLog.ConnectionTimedout(logger, connection.ConnectionNo);
            metrics.ConnectionTimedout(connection);
            connection.Abort(new ConnectionAbortedException("Timed out"));
        });

        // Add Heartbeat.Interval since this can be called right before the next heartbeat.
        Debug.Assert(lastTimestamp != 0);
        var timeoutTicks = timeout.ToTicks(timeProvider);
        Interlocked.Exchange(ref timeoutTimestamp, Interlocked.Read(ref lastTimestamp) + timeoutTicks + heartbeatIntervalTicks);
        return true;
    }

    public void CancelTimeout()
    {
        Interlocked.Exchange(ref timeoutTimestamp, long.MaxValue);

        OnTimeout = null;
    }

    public void Tick(Connection connection, long timestamp)
    {
        if (!Debugger.IsAttached)
        {
            if (timestamp > Interlocked.Read(ref timeoutTimestamp))
            {
                var onTimeout = OnTimeout;
                CancelTimeout();

                if (onTimeout == null)
                    throw new Exception("No OnTimeout");
                onTimeout(connection);
            }
        }
    }
}
