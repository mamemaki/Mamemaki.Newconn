using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Internal;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;

namespace Mamemaki.Newconn.Servers;

public class NewconnMetrics
{
    public const string MeterName = "Mamemaki.Newconn.Servers.Newconn";
    public const string ErrorTypeAttributeName = "error.type";

    private readonly Meter meter;
    private readonly UpDownCounter<long> activeConnectionsCounter;
    private readonly Histogram<double> connectionDuration;
    private readonly Counter<long> rejectedConnectionsCounter;
    private readonly Counter<long> timedoutConnectionsCounter;
    private readonly UpDownCounter<long> queuedConnectionsCounter;
    private readonly Histogram<double> tlsHandshakeDuration;
    private readonly UpDownCounter<long> activeTlsHandshakesCounter;

    public NewconnMetrics(IMeterFactory meterFactory)
    {
        meter = meterFactory.Create(MeterName);

        activeConnectionsCounter = meter.CreateUpDownCounter<long>(
            "newconn.active_connections",
            unit: "{connection}",
            description: "Number of connections that are currently active on the server.");

        connectionDuration = meter.CreateHistogram<double>(
            "newconn.connection.duration",
            unit: "s",
            description: "The duration of connections on the server.");

        rejectedConnectionsCounter = meter.CreateCounter<long>(
           "newconn.rejected_connections",
            unit: "{connection}",
            description: "Number of connections rejected by the server. Connections are rejected when the currently active count exceeds the value configured with MaxConcurrentConnections.");

        timedoutConnectionsCounter = meter.CreateCounter<long>(
           "newconn.timedout_connections",
            unit: "{connection}",
            description: "Number of connections timedout by the server.");

        queuedConnectionsCounter = meter.CreateUpDownCounter<long>(
           "newconn.queued_connections",
            unit: "{connection}",
            description: "Number of connections that are currently queued and are waiting to start.");

        tlsHandshakeDuration = meter.CreateHistogram<double>(
            "newconn.tls_handshake.duration",
            unit: "s",
            description: "The duration of TLS handshakes on the server.");

        activeTlsHandshakesCounter = meter.CreateUpDownCounter<long>(
           "newconn.active_tls_handshakes",
            unit: "{handshake}",
            description: "Number of TLS handshakes that are currently in progress on the server.");
    }

    public bool ConnectionDurationEnabled { get => activeConnectionsCounter.Enabled; }

    public void ConnectionStart(Connection connection)
    {
        if (activeConnectionsCounter.Enabled)
        {
            ConnectionStartCore(connection);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ConnectionStartCore(Connection connection)
    {
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);
        activeConnectionsCounter.Add(1, tags);
    }

    public void ConnectionStop(Connection connection, Exception? exception, List<KeyValuePair<string, object?>>? customTags, long startTimestamp, long currentTimestamp)
    {
        if (activeConnectionsCounter.Enabled || connectionDuration.Enabled)
        {
            ConnectionStopCore(connection, exception, customTags, startTimestamp, currentTimestamp);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ConnectionStopCore(Connection connection, Exception? exception, 
        List<KeyValuePair<string, object?>>? customTags, long startTimestamp, long currentTimestamp)
    {
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);

        if (activeConnectionsCounter.Enabled)
        {
            // Decrease in connections counter must match tags from increase. No custom tags.
            activeConnectionsCounter.Add(-1, tags);
        }

        if (connectionDuration.Enabled)
        {
            // Add custom tags for duration.
            if (customTags != null)
            {
                for (var i = 0; i < customTags.Count; i++)
                {
                    tags.Add(customTags[i]);
                }
            }

            if (exception != null)
            {
                tags.TryAddTag(ErrorTypeAttributeName, exception.GetType().FullName);
            }

            var duration = Stopwatch.GetElapsedTime(startTimestamp, currentTimestamp);
            connectionDuration.Record(duration.TotalSeconds, tags);
        }
    }

    public void ConnectionRejected(Connection connection)
    {
        //AddConnectionEndReason(connection, ConnectionEndReason.MaxConcurrentConnectionsExceeded);

        // Check live rather than cached state because this is just a counter, it's not a start/stop event like the other metrics.
        if (rejectedConnectionsCounter.Enabled)
        {
            ConnectionRejectedCore(connection);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ConnectionRejectedCore(Connection connection)
    {
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);
        rejectedConnectionsCounter.Add(1, tags);
    }

    public void ConnectionTimedout(Connection connection)
    {
        // Check live rather than cached state because this is just a counter, it's not a start/stop event like the other metrics.
        if (timedoutConnectionsCounter.Enabled)
        {
            ConnectionTimeoutCore(connection);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ConnectionTimeoutCore(Connection connection)
    {
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);
        timedoutConnectionsCounter.Add(1, tags);
    }

    public void ConnectionQueuedStart(Connection connection)
    {
        if (queuedConnectionsCounter.Enabled)
        {
            ConnectionQueuedStartCore(connection);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ConnectionQueuedStartCore(Connection connection)
    {
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);
        queuedConnectionsCounter.Add(1, tags);
    }

    public void ConnectionQueuedStop(Connection connection)
    {
        if (queuedConnectionsCounter.Enabled)
        {
            ConnectionQueuedStopCore(connection);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ConnectionQueuedStopCore(Connection connection)
    {
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);
        queuedConnectionsCounter.Add(-1, tags);
    }

    public void TlsHandshakeStart(Connection connection)
    {
        if (activeTlsHandshakesCounter.Enabled)
        {
            TlsHandshakeStartCore(connection);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TlsHandshakeStartCore(Connection connection)
    {
        // Tags must match TLS handshake end.
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);
        activeTlsHandshakesCounter.Add(1, tags);
    }

    public void TlsHandshakeStop(Connection connection, long startTimestamp, long currentTimestamp, 
        SslProtocols? protocol = null, Exception? exception = null)
    {
        if (activeTlsHandshakesCounter.Enabled || tlsHandshakeDuration.Enabled)
        {
            TlsHandshakeStopCore(connection, startTimestamp, currentTimestamp, protocol, exception);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TlsHandshakeStopCore(Connection connection, long startTimestamp, long currentTimestamp, 
        SslProtocols? protocol = null, Exception? exception = null)
    {
        var tags = new TagList();
        InitializeConnectionTags(ref tags, connection);

        if (activeTlsHandshakesCounter.Enabled)
        {
            // Tags must match TLS handshake start.
            activeTlsHandshakesCounter.Add(-1, tags);
        }

        if (protocol != null && TryGetHandshakeProtocol(protocol.Value, out var protocolName, out var protocolVersion))
        {
            // Protocol name should always be TLS. Have logic to a tls.protocol.name tag if not TLS just in case.
            if (protocolName != "tls")
            {
                tags.Add("tls.protocol.name", protocolName);
            }
            tags.Add("tls.protocol.version", protocolVersion);
        }
        if (exception != null)
        {
            // Set exception name as error.type if there isn't already a value.
            tags.TryAddTag(ErrorTypeAttributeName, exception.GetType().FullName);
        }

        var duration = Stopwatch.GetElapsedTime(startTimestamp, currentTimestamp);
        tlsHandshakeDuration.Record(duration.TotalSeconds, tags);
    }

    private static void InitializeConnectionTags(ref TagList tags, in Connection connection)
    {
        var localEndpoint = connection.LocalEndPoint;
        if (localEndpoint is IPEndPoint localIPEndPoint)
        {
            tags.Add("server.address", localIPEndPoint.Address.ToString());
            tags.Add("server.port", localIPEndPoint.Port);

            switch (localIPEndPoint.Address.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    tags.Add("network.type", "ipv4");
                    break;
                case AddressFamily.InterNetworkV6:
                    tags.Add("network.type", "ipv6");
                    break;
            }
        }
        else if (localEndpoint is UnixDomainSocketEndPoint udsEndPoint)
        {
            tags.Add("server.address", udsEndPoint.ToString());
            tags.Add("network.transport", "unix");
        }
        else if (localEndpoint != null)
        {
            tags.Add("server.address", localEndpoint.ToString());
            tags.Add("network.transport", localEndpoint.AddressFamily.ToString());
        }
    }

    public static bool TryGetHandshakeProtocol(SslProtocols protocols, [NotNullWhen(true)] out string? name, [NotNullWhen(true)] out string? version)
    {
        // Protocol should be either TLS 1.2 or 1.3. Many older SslProtocols are no longer supported.
        // Logic for resolving older known values is still here out of an abundence of caution.

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable SYSLIB0039 // Type or member is obsolete
        switch (protocols)
        {
            case SslProtocols.Ssl2:
                name = "ssl";
                version = "2.0";
                return true;
            case SslProtocols.Ssl3:
                name = "ssl";
                version = "3.0";
                return true;
            case SslProtocols.Tls:
                name = "tls";
                version = "1.0";
                return true;
            case SslProtocols.Tls11:
                name = "tls";
                version = "1.1";
                return true;
            case SslProtocols.Tls12:
                name = "tls";
                version = "1.2";
                return true;
            case SslProtocols.Tls13:
                name = "tls";
                version = "1.3";
                return true;
        }
#pragma warning restore SYSLIB0039 // Type or member is obsolete
#pragma warning restore CS0618 // Type or member is obsolete

        name = null;
        version = null;
        return false;
    }

    public void AddConnectionEndReason(IConnectionMetricsTagsFeature? feature, ConnectionEndReason reason)
    {
        if (feature != null)
        {
            if (TryGetErrorType(reason, out var errorTypeValue))
            {
                feature.TryAddTag(ErrorTypeAttributeName, errorTypeValue);
            }
        }
    }

    protected virtual string? GetErrorType(ConnectionEndReason reason)
    {
        TryGetErrorType(reason, out var errorTypeValue);
        return errorTypeValue;
    }

    protected virtual bool TryGetErrorType(ConnectionEndReason reason, [NotNullWhen(true)] out string? errorTypeValue)
    {
        if (reason == ConnectionEndReason.Unknown)
            errorTypeValue = null;
        else
            errorTypeValue = reason.DisplayName;

        return errorTypeValue != null;
    }
}
