using Microsoft.Extensions.Logging;

namespace Mamemaki.Newconn.Internal;

internal static partial class NewconnLog
{
    [LoggerMessage(1, LogLevel.Debug, @"Connection ""{ConnectionNo}"" connected.", EventName = "ConnectionConnected")]
    public static partial void ConnectionConnected(ILogger logger, long connectionNo);

    [LoggerMessage(2, LogLevel.Debug, @"Connection ""{ConnectionNo}"" closed as {ConnectionEndReason}.", EventName = "ConnectionClosed")]
    public static partial void ConnectionClosed(ILogger logger, long connectionNo, ConnectionEndReason connectionEndReason);

    [LoggerMessage(4, LogLevel.Debug, @"Connection ""{ConnectionNo}"" paused.", EventName = "ConnectionPause")]
    public static partial void ConnectionPause(ILogger logger, long connectionNo);

    [LoggerMessage(5, LogLevel.Debug, @"Connection ""{ConnectionNo}"" resumed.", EventName = "ConnectionResume")]
    public static partial void ConnectionResume(ILogger logger, long connectionNo);

    [LoggerMessage(6, LogLevel.Debug, @"Connection ""{ConnectionNo}"" received FIN.", EventName = "ConnectionReadFin", SkipEnabledCheck = true)]
    public static partial void ConnectionReadFin(ILogger logger, long connectionNo);

    [LoggerMessage(7, LogLevel.Debug, @"Connection ""{ConnectionNo}"" sending FIN because: ""{Reason}""", EventName = "ConnectionWriteFin", SkipEnabledCheck = true)]
    public static partial void ConnectionWriteFin(ILogger logger, long connectionNo, string reason);

    [LoggerMessage(8, LogLevel.Debug, @"Connection ""{ConnectionNo}"" sending RST because: ""{Reason}""", EventName = "ConnectionWriteRst", SkipEnabledCheck = true)]
    public static partial void ConnectionWriteRst(ILogger logger, long connectionNo, string reason);

    //[LoggerMessage(9, LogLevel.Debug, @"Connection ""{ConnectionNo}"" completed keep alive response.", EventName = "ConnectionKeepAlive")]
    //public static partial void ConnectionKeepAlive(ILogger logger, long connectionNo);

    //[LoggerMessage(10, LogLevel.Debug, @"Connection ""{ConnectionNo}"" disconnecting.", EventName = "ConnectionDisconnect")]
    //public static partial void ConnectionDisconnect(ILogger logger, long connectionNo);

    [LoggerMessage(14, LogLevel.Debug, @"Connection ""{ConnectionNo}"" communication error.", EventName = "ConnectionError", SkipEnabledCheck = true)]
    public static partial void ConnectionError(ILogger logger, long connectionNo, Exception ex);

    //[LoggerMessage(16, LogLevel.Debug, "Some connections failed to close gracefully during server shutdown.", EventName = "NotAllConnectionsClosedGracefully")]
    //public static partial void NotAllConnectionsClosedGracefully(ILogger logger);

    [LoggerMessage(18, LogLevel.Debug, @"Connection reset while in backlog.", EventName = "ConnectionResetWhileInBacklog", SkipEnabledCheck = true)]
    public static partial void ConnectionResetWhileInBacklog(ILogger logger);

    [LoggerMessage(19, LogLevel.Debug, @"Connection ""{ConnectionNo}"" reset.", EventName = "ConnectionReset", SkipEnabledCheck = true)]
    public static partial void ConnectionReset(ILogger logger, long connectionNo);

    //[LoggerMessage(21, LogLevel.Debug, "Some connections failed to abort during server shutdown.", EventName = "NotAllConnectionsAborted")]
    //public static partial void NotAllConnectionsAborted(ILogger logger);

    [LoggerMessage(22, LogLevel.Warning, @"As of ""{now}"", the heartbeat has been running for ""{heartbeatDuration}"" which is longer than ""{interval}"". This could be caused by thread pool starvation.", EventName = "HeartbeatSlow")]
    public static partial void HeartbeatSlow(ILogger logger, DateTimeOffset now, TimeSpan heartbeatDuration, TimeSpan interval);

    [LoggerMessage(24, LogLevel.Warning, @"Connection ""{ConnectionNo}"" rejected because the maximum number of concurrent connections has been reached.", EventName = "ConnectionRejected")]
    public static partial void ConnectionRejected(ILogger logger, long connectionNo);

    [LoggerMessage(25, LogLevel.Warning, @"Connection ""{ConnectionNo}"" timed out.", EventName = "ConnectionTimedout")]
    public static partial void ConnectionTimedout(ILogger logger, long connectionNo);

    [LoggerMessage(34, LogLevel.Information, @"Connection ""{ConnectionNo}"", Request id ""{TraceIdentifier}"": the application aborted the connection.", EventName = "ApplicationAbortedConnection")]
    public static partial void ApplicationAbortedConnection(ILogger logger, long connectionNo, string traceIdentifier);

    [LoggerMessage(39, LogLevel.Debug, @"Connection ""{ConnectionNo}"" accepted.", EventName = "ConnectionAccepted")]
    public static partial void ConnectionAccepted(ILogger logger, long connectionNo);
}
