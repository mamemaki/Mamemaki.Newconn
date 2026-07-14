using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Features.Heartbeats;
using Microsoft.Extensions.Logging;

namespace Mamemaki.Newconn.Servers;

/// <summary>
/// Represents a server context.
/// </summary>
public interface IServerContext : IConnectionMetricsFeature
{
    /// <summary>
    /// Gets a server options.
    /// </summary>
    NewconnServerOptions Options { get; }

    /// <summary>
    /// Gets a logger factory.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets a heartbeat.
    /// </summary>
    IHeartbeat? Heartbeat { get; }
}
