using Mamemaki.Newconn.Features.Tls;
using Mamemaki.Newconn.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace Mamemaki.Newconn.Clients;

public static class ConnectionFactoryExtensions
{
    /// <summary>
    /// Add the <see cref="ConnectionFactory{TConnection}"/> to the DI services.
    /// </summary>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="services">A DI service collection.</param>
    /// <param name="configure">A configure action for the <see cref="ConnectionFactoryBuilder{TConnection}"/>.</param>
    /// <returns>The same instance of the <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddConnectionFactory<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConnection>(
        this IServiceCollection services,
        Action<ConnectionFactoryBuilder<TConnection>>? configure = null)
        where TConnection : Connection, new()
    {
        services.TryAddSingleton<ConnectionFactory<TConnection>>(services =>
        {
            var builder = new ConnectionFactoryBuilder<TConnection>(services);
            configure?.Invoke(builder);
            return builder.Build();
        });
        return services;
    }

    /// <summary>
    /// Use the socket connection factory at the builder.
    /// </summary>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="builder">A connection factory builder.</param>
    /// <param name="configure">A configure action for the <see cref="SocketConnectionFactoryOptions"/>.</param>
    /// <returns>The same instance of the <see cref="ConnectionFactoryBuilder{TConnection}"/> for chaining.</returns>
    public static ConnectionFactoryBuilder<TConnection> UseSockets<TConnection>(
        this ConnectionFactoryBuilder<TConnection> builder, Action<SocketConnectionFactoryOptions>? configure = null)
        where TConnection : Connection, new()
    {
        var factoryOptions = new SocketConnectionFactoryOptions();
        configure?.Invoke(factoryOptions);

        var loggerFactory = builder.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<TConnection>();
        return builder.UseConnectionFactory(new SocketConnectionFactory<TConnection>(logger, factoryOptions));
    }

    /// <summary>
    /// Use TLS at the connection.
    /// </summary>
    /// <param name="builder">A connection factory builder.</param>
    /// <param name="options">A TLS options.</param>
    /// <returns>The same instance of the <see cref="IClientMiddlewareBuilder"/> for chaining.</returns>
    public static IClientMiddlewareBuilder UseClientTls(
        this IClientMiddlewareBuilder builder, TlsOptions options)
    {
        var loggerFactory = builder.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<TlsClientConnectionMiddleware>();

        builder.Use(async (connection, cancellationToken) =>
        {
            var tlsClientMiddleware = new TlsClientConnectionMiddleware(options, logger);
            connection.RegisterDisposalObject(tlsClientMiddleware);
            return await tlsClientMiddleware.OnConnectionAsync(connection, cancellationToken);
        });

        return builder;
    }
}
