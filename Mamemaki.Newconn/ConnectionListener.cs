using Mamemaki.Newconn.Internal;
using System.Net;
using System.Runtime.CompilerServices;

namespace Mamemaki.Newconn;

/// <summary>
/// Represents a connection listener that accepts connections.
/// </summary>
/// <typeparam name="TConnection">The type of connection.</typeparam>
public abstract class ConnectionListener<TConnection> : IAsyncDisposable
    where TConnection : Connection
{
    /// <summary>
    /// Gets the collection of property that used for the connections.
    /// The properties may include connection features or options.
    /// </summary>
    public IConnectionProperties Properties { get; protected set; } = ThrowConnectionProperties.Instance;

    /// <summary>
    /// Gets or sets the middlewares that used for the connections.
    /// </summary>
    public IList<ConnectionMiddlewareDelegate> Middlewares { get; set; } = [];

    private bool disposed;

    protected ConnectionListener()
    {
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose resouce objects related this instance.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    protected abstract ValueTask DisposeAsyncCore();

    /// <summary>
    /// Gets or sets the local endpoint for this connection.
    /// </summary>
    public abstract EndPoint? LocalEndPoint { get; }

    /// <summary>
    /// Accepts a new connection.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask<TConnection>"/> for the <see cref="Connection"/>.</returns>
    public abstract ValueTask<TConnection?> AcceptAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts new connections.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="IAsyncEnumerable<TConnection>"/> for the <see cref="Connection"/>.</returns>
    public virtual async IAsyncEnumerable<TConnection> AcceptManyAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            var connection = await AcceptAsync(cancellationToken).ConfigureAwait(false);
            if (connection == null)
            {
                // Null means we don't have anymore connections
                yield break;
            }

            yield return connection;
        }
    }
}
