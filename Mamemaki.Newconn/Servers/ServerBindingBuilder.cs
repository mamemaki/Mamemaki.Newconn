using Mamemaki.Newconn.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Mamemaki.Newconn.Servers;

/// <summary>
/// Represents the server binding builder.
/// </summary>
/// <typeparam name="TConnection">The type of connection.</typeparam>
public class ServerBindingBuilder<TConnection> : IServerMiddlewareBuilder
    where TConnection : Connection, new()
{
    public ServerBindingOptions Options { get; private set; } = new();

    public ConnectionFactory<TConnection> ConnectionFactory { get; }

    /// <summary>
    /// Gets or sets a connection handler.
    /// </summary>
    public ConnectionDelegate<TConnection>? OnConnection { get; set; }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/>.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Create an initialized <see cref="ServerBindingBuilder{TConnection}"/>.
    /// </summary>
    /// <param name="serviceProvider">A service provider.</param>
    public ServerBindingBuilder(ConnectionFactory<TConnection> connectionFactory, 
        IServiceProvider? serviceProvider = null)
    {
        ConnectionFactory = connectionFactory;
        ServiceProvider = serviceProvider ?? EmptyServiceProvider.Instance;
    }

    /// <summary>
    /// Register a connection middleware.
    /// </summary>
    /// <param name="middleware">The middleware to use.</param>
    /// <returns>The same instance of the <see cref="ServerBindingBuilder{TConnection}"/> for chaining.</returns>
    public ServerBindingBuilder<TConnection> Use(ConnectionMiddlewareDelegate middleware)
    {
        ConnectionFactory.Middlewares.Add(middleware);
        return this;
    }

    IMiddlewareBuilder IMiddlewareBuilder.Use(ConnectionMiddlewareDelegate middleware)
    {
        return Use(middleware);
    }

    /// <summary>
    /// Register a connection handler.
    /// </summary>
    /// <param name="onConnection">The connection handler to use.</param>
    /// <returns>The same instance of the <see cref="ServerBindingBuilder{TConnection}"/> for chaining.</returns>
    public ServerBindingBuilder<TConnection> Run(ConnectionDelegate<TConnection> onConnection)
    {
        OnConnection = onConnection;
        return this;
    }

    /// <summary>
    /// Register a connection handler.
    /// </summary>
    /// <typeparam name="TServer">The type of connection handler.</typeparam>
    /// <returns>The same instance of the <see cref="ServerBindingBuilder{TConnection}"/> for chaining.</returns>
    public ServerBindingBuilder<TConnection> Run<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TServer>()
        where TServer : ConnectionHandler<TConnection>
    {
        var handler = ActivatorUtilities.GetServiceOrCreateInstance<TServer>(ServiceProvider);

        OnConnection = handler.OnConnectedAsync;
        return this;
    }

    private const string NoOnConnectionErrorMessage = "No OnConnection configured. Set the OnConnection property.";

    public EndPointBinding<TConnection> Build(EndPoint? endPoint)
    {
        if (OnConnection == null)
            throw new InvalidOperationException(NoOnConnectionErrorMessage);

        var binding = new EndPointBinding<TConnection>(endPoint, Options, ConnectionFactory, OnConnection);
        return binding;
    }

    public LocalhostBinding<TConnection> BuildLocalhost(int port)
    {
        if (OnConnection == null)
            throw new InvalidOperationException(NoOnConnectionErrorMessage);

        var binding = new LocalhostBinding<TConnection>(port, Options, ConnectionFactory, OnConnection);
        return binding;
    }
}
