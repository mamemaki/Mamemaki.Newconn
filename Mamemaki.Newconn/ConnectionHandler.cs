namespace Mamemaki.Newconn;

/// <summary>
/// A function that can process a connection.
/// </summary>
/// <param name="connection">A <see cref="Connection" /> representing the connection.</param>
/// <returns>A <see cref="Task"/> that represents the connection lifetime. When the task completes, the connection will be closed.</returns>
public delegate Task ConnectionDelegate<TConnection>(TConnection connection, CancellationToken cancellationToken)
    where TConnection : Connection;

/// <summary>
/// Represents an endpoint that multiple connections connect to.
/// </summary>
public abstract class ConnectionHandler<TConnection>
    where TConnection : Connection
{
    /// <summary>
    /// Called when a new connection is accepted to the endpoint.
    /// </summary>
    /// <param name="connection">The new <see cref="Connection"/></param>
    /// <returns>A <see cref="Task"/> that represents the connection lifetime. When the task completes, the connection is complete.</returns>
    public abstract Task OnConnectedAsync(TConnection connection, CancellationToken cancellationToken);
}
