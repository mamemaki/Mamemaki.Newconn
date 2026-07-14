using Mamemaki.Newconn.Features.ConnectionLimits;
using Mamemaki.Newconn.Features.ConnectionTimeout;
using Mamemaki.Newconn.Features.Tls;
using Mamemaki.Newconn.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mamemaki.Newconn.Servers;

public static class ServerBindingBuilderExtensions
{
    /// <summary>
    /// Use TLS at the connection.
    /// </summary>
    /// <param name="builder">A middleware builder.</param>
    /// <param name="options">A TLS options.</param>
    /// <returns>The same instance of the <see cref="IServerMiddlewareBuilder"/> for chaining.</returns>
    public static IServerMiddlewareBuilder UseServerTls(
        this IServerMiddlewareBuilder builder, TlsOptions options)
    {
        var loggerFactory = builder.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<TlsServerConnectionMiddleware>();
        var metrics = builder.ServiceProvider.GetRequiredService<NewconnMetrics>();

        builder.Use(async (connection, cancellationToken) =>
        {
            var tlsServerMiddleware = new TlsServerConnectionMiddleware(options, logger, metrics);
            connection.RegisterDisposalObject(tlsServerMiddleware);
            return await tlsServerMiddleware.OnConnectionAsync(connection, cancellationToken);
        });

        return builder;
    }

    /// <summary>
    /// Limits concurrent connections.
    /// </summary>
    /// <param name="builder">A middleware builder.</param>
    /// <param name="maxConcurrentConnections">The maximum concurrent connection count.</param>
    /// <returns>The same instance of the <see cref="IServerMiddlewareBuilder"/> for chaining.</returns>
    public static IServerMiddlewareBuilder UseConnectionLimit(
        this IServerMiddlewareBuilder builder, long? maxConcurrentConnections)
    {
        var loggerFactory = builder.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<ConnectionLimitMiddleware>();
        var concurrentConnectionCounter = maxConcurrentConnections != null ? 
            ResourceCounter.Quota(maxConcurrentConnections.Value) :
            ResourceCounter.Unlimited;

        builder.Use(async (connection, cancellationToken) =>
        {
            var middleware = new ConnectionLimitMiddleware(concurrentConnectionCounter, logger);
            connection.RegisterDisposalObject(middleware);
            return await middleware.OnConnectionAsync(connection, cancellationToken);
        });

        return builder;
    }

    /// <summary>
    /// Time out connection.
    /// Timeout settings are configured through <see cref="IConnectionTimeoutFeature"/>.
    /// </summary>
    /// <param name="builder">A middleware builder.</param>
    /// <returns>The same instance of the <see cref="IServerMiddlewareBuilder"/> for chaining.</returns>
    public static IServerMiddlewareBuilder UseConnectionTimeout(this IServerMiddlewareBuilder builder)
    {
        var loggerFactory = builder.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<ConnectionTimeoutMiddleware>();
        var metrics = builder.ServiceProvider.GetRequiredService<NewconnMetrics>();

        builder.Use(async (connection, cancellationToken) =>
        {
            var serverContext = connection.Properties.Get<IServerContext>();
            var middleware = new ConnectionTimeoutMiddleware(logger, metrics, serverContext.Options.TimeProvider);
            return await middleware.OnConnectionAsync(connection, cancellationToken);
        });

        return builder;
    }
}
