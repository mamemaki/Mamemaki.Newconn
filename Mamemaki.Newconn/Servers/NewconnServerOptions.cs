using Mamemaki.Newconn.Features.Heartbeats;
using Mamemaki.Newconn.Hosting;
using Mamemaki.Newconn.Internal;
using Mamemaki.Newconn.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace Mamemaki.Newconn.Servers;

/// <summary>
/// Represents a server options.
/// </summary>
public class NewconnServerOptions
{
    /// <summary>
    /// Gets server bindings to the server.
    /// </summary>
    public IList<ServerBinding> Bindings { get; } = [];

    /// <summary>
    /// Gets or sets a server shutdown timeout. Defaults is 5 seconds.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets a logger factory.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

    /// <summary>
    /// Gets or sets a service provider.
    /// </summary>
    public IServiceProvider ServiceProvider { get; set; } = EmptyServiceProvider.Instance;

    /// <summary>
    /// Gets or sets a time provider.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets or sets whether the heartbeat is used. Defaults is false.
    /// </summary>
    public bool UseHeartbeat { get; set; } = false;

    /// <summary>
    /// Gets or sets the heartbeat interval. Defaults is 1 second.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = Heartbeat.DefaultInterval;

    /// <summary>
    /// Gets the heartbeat handlers.
    /// </summary>
    public IEnumerable<IHeartbeatHandler> HeartbeatHandlers { get; } = [];

    /// <summary>
    /// Gets the metrics.
    /// </summary>
    public NewconnMetrics Metrics { get; }

    /// <summary>
    /// Create an initialized <see cref="NewconnServerOptions"/>.
    /// </summary>
    /// <param name="metrics">A metrics, if any.</param>
    public NewconnServerOptions(NewconnMetrics? metrics = null)
    {
        Metrics = metrics ?? new NewconnMetrics(new DefaultMeterFactory());
    }

    /// <summary>
    /// Listen to a socket at specified endpoint.
    /// </summary>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="endPoint">An endpoint to listen.</param>
    /// <param name="configure">A configure action for the <see cref="SocketServerBindingBuilder{TConnection}"/>.</param>
    public void SocketListen<TConnection>(EndPoint endPoint, 
        Action<SocketServerBindingBuilder<TConnection>>? configure = null)
        where TConnection : Connection, new()
    {
        var logger = LoggerFactory.CreateLogger<TConnection>();
        var connectionFactory = new SocketConnectionFactory<TConnection>(logger);
        var builder = new SocketServerBindingBuilder<TConnection>(connectionFactory, ServiceProvider);
        configure?.Invoke(builder);
        var binding = builder.Build(endPoint);
        Bindings.Add(binding);
    }

    /// <summary>
    /// Listen to a socket at specified address and port.
    /// </summary>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="address">An address to listen.</param>
    /// <param name="port">A port number to listen.</param>
    /// <param name="configure">A configure action for the <see cref="SocketServerBindingBuilder{TConnection}"/>.</param>
    public void SocketListen<TConnection>(IPAddress address, int port, 
        Action<SocketServerBindingBuilder<TConnection>>? configure = null)
        where TConnection : Connection, new()
    {
        var endPoint = new IPEndPoint(address, port);
        SocketListen(endPoint, configure);
    }

    /// <summary>
    /// Listen to a socket at any IP address and specified port.
    /// </summary>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="port">A port number to listen.</param>
    /// <param name="configure">A configure action for the <see cref="SocketServerBindingBuilder{TConnection}"/>.</param>
    public void SocketListenAnyIP<TConnection>(int port, 
        Action<SocketServerBindingBuilder<TConnection>>? configure = null)
        where TConnection : Connection, new()
    {
        var endPoint = new IPEndPoint(Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, port);
        SocketListen(endPoint, configure);
    }

    /// <summary>
    /// Listen to a socket at localhost and specified port.
    /// </summary>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="port">A port number to listen.</param>
    /// <param name="configure">A configure action for the <see cref="SocketServerBindingBuilder{TConnection}"/>.</param>
    public void SocketListenLocalhost<TConnection>(int port,
        Action<SocketServerBindingBuilder<TConnection>>? configure = null)
        where TConnection : Connection, new()
    {
        var logger = LoggerFactory.CreateLogger<TConnection>();
        var connectionFactory = new SocketConnectionFactory<TConnection>(logger);
        var builder = new SocketServerBindingBuilder<TConnection>(connectionFactory, ServiceProvider);
        configure?.Invoke(builder);
        var binding = builder.BuildLocalhost(port);
        Bindings.Add(binding);
    }

    /// <summary>
    /// Listen to a socket at specified unix socket path.
    /// </summary>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="socketPath">A unix socket path to listen.</param>
    /// <param name="configure">A configure action for the <see cref="SocketServerBindingBuilder{TConnection}"/>.</param>
    public void SocketListenUnixSocket<TConnection>(string socketPath,
        Action<SocketServerBindingBuilder<TConnection>>? configure = null)
        where TConnection : Connection, new()
    {
        var endPoint = new UnixDomainSocketEndPoint(socketPath);
        SocketListen(endPoint, configure);
    }
}
