using System.Net;

namespace Mamemaki.Newconn.Clients;

/// <summary>
/// A delegate for instantiating a client.
/// </summary>
/// <typeparam name="TClient">The type of client.</typeparam>
/// <param name="connection">The connection for the client.</param>
/// <param name="args">Arguments for the instantiating the client.</param>
/// <returns>A instantiated <see cref="{TClient}"/> object.</returns>
public delegate TClient CreateClientDelegate<TClient>(Connection connection, params object[] args)
    where TClient : Client;

/// <summary>
/// Represents a client factory that opens a new connection.
/// </summary>
/// <typeparam name="TClient">The type of client.</typeparam>
/// <typeparam name="TConnection">The type of connection.</typeparam>
public class ClientFactory<TClient, TConnection>
    where TClient : Client
    where TConnection : Connection, new()
{
    private readonly ConnectionFactory<TConnection> connectionFactory;
    private readonly CreateClientDelegate<TClient> createClient;

    public ClientFactory(
        ConnectionFactory<TConnection> connectionFactory,
        CreateClientDelegate<TClient> createClient)
    {
        this.connectionFactory = connectionFactory;
        this.createClient = createClient;
    }

    /// <summary>
    /// Opens a new <see cref="Connection"/>.
    /// </summary>
    /// <param name="endPoint">The <see cref="EndPoint"/> to connect to, if any.</param>
    /// <param name="properties">Options used to create the connection, if any.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <param name="args">Arguments for the instantiating the client.</param>
    /// <returns>A <see cref="ValueTask<TClient>"/> for the <see cref="{TClient}"/>.</returns>
    public virtual async ValueTask<TClient> ConnectAsync(EndPoint? endpoint,
        IConnectionProperties? properties = null, CancellationToken cancellationToken = default, 
        params object[] args)
    {
        var connection = await connectionFactory.ConnectAsync(endpoint, properties, cancellationToken);

        var client = createClient(connection, args);

        return client;
    }
}
