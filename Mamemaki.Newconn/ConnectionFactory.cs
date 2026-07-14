using System.Net;

namespace Mamemaki.Newconn;

/// <summary>
/// Represents a connection factory that opens a new connection or creates a connection listener.
/// </summary>
/// <typeparam name="TConnection">The type of connection.</typeparam>
public abstract class ConnectionFactory<TConnection>
    where TConnection : Connection, new()
{
    /// <summary>
    /// Gets or sets the middlewares that used for the connections.
    /// </summary>
    public IList<ConnectionMiddlewareDelegate> Middlewares { get; set; } = [];

    /// <summary>
    /// Opens a new <see cref="Connection"/>.
    /// </summary>
    /// <param name="endPoint">The <see cref="EndPoint"/> to connect to, if any.</param>
    /// <param name="properties">Options used to create the connection, if any.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask<TConnection>"/> for the <see cref="Connection"/>.</returns>
    public abstract ValueTask<TConnection> ConnectAsync(EndPoint? endpoint, 
        IConnectionProperties? properties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a <see cref="ConnectionListener<TConnection>"/> bound to the specified <see cref="EndPoint"/>.
    /// </summary>
    /// <param name="endPoint">The <see cref="EndPoint"/> to bind to, if any.</param>
    /// <param name="properties">Options used to create the connection listener, if any.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask<TConnection>"/> for the <see cref="Connection"/>.</returns>
    public abstract ValueTask<ConnectionListener<TConnection>> BindAsync(EndPoint? endpoint, 
        IConnectionProperties? properties = null, CancellationToken cancellationToken = default);
}
