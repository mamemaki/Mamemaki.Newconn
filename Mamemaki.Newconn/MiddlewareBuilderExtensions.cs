using Mamemaki.Newconn.Features.DataLogging;
using Mamemaki.Newconn.Features.GZipCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mamemaki.Newconn;

public delegate void LoggingFormatter(ILogger logger, string method, ReadOnlySpan<byte> buffer);

public static class MiddlewareBuilderExtensions
{
    /// <summary>
    /// Emits verbose logs for bytes read from and written to the connection.
    /// </summary>
    /// <param name="builder">The <see cref="IMiddlewareBuilder"/>.</param>
    /// <param name="loggingFormatter">A logger formatter.</param>
    /// <returns>The <see cref="IMiddlewareBuilder"/>.</returns>
    public static IMiddlewareBuilder UseDataLogging(this IMiddlewareBuilder builder,
        LoggingFormatter? loggingFormatter = null)
    {
        var loggerFactory = builder.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<DataLoggingMiddleware>();

        builder.Use(async (connection, cancellationToken) =>
        {
            var middleware = new DataLoggingMiddleware(logger, loggingFormatter);
            connection.RegisterDisposalObject(middleware);
            return await middleware.OnConnectionAsync(connection, cancellationToken);
        });

        return builder;
    }

    /// <summary>
    /// Compresses sent and received data.
    /// </summary>
    /// <param name="builder">The <see cref="IMiddlewareBuilder"/>.</param>
    /// <returns>The <see cref="IMiddlewareBuilder"/>.</returns>
    public static IMiddlewareBuilder UseGZipCompression(this IMiddlewareBuilder builder)
    {
        builder.Use(async (connection, cancellationToken) =>
        {
            var middleware = new GZipCompressionMiddleware();
            connection.RegisterDisposalObject(middleware);
            return await middleware.OnConnectionAsync(connection, cancellationToken);
        });

        return builder;
    }
}
