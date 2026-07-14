using Mamemaki.Newconn.Servers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mamemaki.Newconn.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add the <see cref="ServerHostedService"/> to the DI services.
    /// </summary>
    /// <param name="services">A DI service collection.</param>
    /// <param name="configure">A configure action for the <see cref="NewconnServerOptions"/>.</param>
    /// <returns>The same instance of the <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseNewconnServer(this IServiceCollection services, 
        Action<NewconnServerOptions> configure)
    {
        services.TryAddSingleton<NewconnMetrics>();

        services.TryAddSingleton(services =>
        {
            var metrics = services.GetRequiredService<NewconnMetrics>();
            var options = new NewconnServerOptions(metrics)
            {
                ServiceProvider = services,
                LoggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance,
            };
            configure(options);
            return options;
        });

        services.AddHostedService<ServerHostedService>();
        return services;
    }
}
