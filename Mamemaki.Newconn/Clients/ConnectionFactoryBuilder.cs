using Mamemaki.Newconn.Hosting;

namespace Mamemaki.Newconn.Clients;

/// <summary>
/// Represents the connection factory builder.
/// </summary>
/// <typeparam name="TConnection">The type of connection.</typeparam>
public class ConnectionFactoryBuilder<TConnection> : IClientMiddlewareBuilder
    where TConnection : Connection, new()
{
    protected ConnectionFactory<TConnection>? ConnectionFactory;

    protected readonly IList<ConnectionMiddlewareDelegate> Middlewares = new List<ConnectionMiddlewareDelegate>();

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/>.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Create an initialized <see cref="ConnectionFactoryBuilder{TConnection}"/>.
    /// </summary>
    /// <param name="serviceProvider">A service provider.</param>
    public ConnectionFactoryBuilder(IServiceProvider? serviceProvider = null)
    {
        ServiceProvider = serviceProvider ?? EmptyServiceProvider.Instance;
    }

    /// <summary>
    /// Register a connection factory.
    /// This method used when using a custom connection factory.
    /// If this method is not called, the default connection factory(<see cref="SocketConnectionFactory{TConnection}"/>) will be used.
    /// </summary>
    /// <param name="connectionFactory">A connection factory.</param>
    /// <returns>The same instance of the <see cref="ConnectionFactoryBuilder{TConnection}"/> for chaining.</returns>
    public ConnectionFactoryBuilder<TConnection> UseConnectionFactory(ConnectionFactory<TConnection> connectionFactory)
    {
        ConnectionFactory = connectionFactory;
        return this;
    }

    /// <summary>
    /// Register a connection middleware.
    /// </summary>
    /// <param name="middleware">The middleware to use.</param>
    /// <returns>The same instance of the <see cref="ConnectionFactoryBuilder{TConnection}"/> for chaining.</returns>
    public ConnectionFactoryBuilder<TConnection> Use(ConnectionMiddlewareDelegate middleware)
    {
        Middlewares.Add(middleware);
        return this;
    }

    IMiddlewareBuilder IMiddlewareBuilder.Use(ConnectionMiddlewareDelegate middleware)
    {
        return Use(middleware);
    }

    /// <summary>
    /// Run the given actions to initialize the connection factory.
    /// </summary>
    /// <returns>An initialized <see cref="ConnectionFactoryBuilder{TConnection}"/>.</returns>
    public ConnectionFactory<TConnection> Build()
    {
        if (ConnectionFactory == null)
            this.UseSockets();
        ConnectionFactory!.Middlewares = Middlewares;
        return ConnectionFactory;
    }
}
