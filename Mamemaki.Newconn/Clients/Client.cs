namespace Mamemaki.Newconn.Clients;

/// <summary>
/// Represents a client that holds the connection.
/// The client is designed to be instantiated by the <see cref="ClientFactory{TClient, TConnection}"/> registered as DI service.
/// </summary>
public abstract class Client : IAsyncDisposable
{
    /// <summary>
    /// Gets the connection.
    /// </summary>
    protected abstract Connection Connection { get; }

    private bool disposed;

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
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Connection is not null)
        {
            await Connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
