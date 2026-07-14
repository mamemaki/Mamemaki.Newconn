namespace Mamemaki.Newconn;

/// <summary>
/// Represents the middleware builder
/// </summary>
public interface IMiddlewareBuilder
{
    /// <summary>
    /// Gets the <see cref="IServiceProvider"/>.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Register a connection middleware.
    /// Executions are performed in the order they are registered.
    /// </summary>
    /// <param name="middleware">The middleware to use.</param>
    /// <returns>The same instance of the <see cref="IMiddlewareBuilder"/> for chaining.</returns>
    IMiddlewareBuilder Use(ConnectionMiddlewareDelegate middleware);
}

/// <summary>
/// Represents the middleware builder for the server
/// </summary>
public interface IServerMiddlewareBuilder : IMiddlewareBuilder
{
}

/// <summary>
/// Represents the middleware builder for the client
/// </summary>
public interface IClientMiddlewareBuilder : IMiddlewareBuilder
{
}
