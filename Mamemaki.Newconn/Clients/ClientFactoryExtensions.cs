using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace Mamemaki.Newconn.Clients;

public static class ClientFactoryExtensions
{
    /// <summary>
    /// Add the <see cref="ClientFactory{TClient, TConnection}"/> to the DI services.
    /// </summary>
    /// <typeparam name="TClient">The type of client.</typeparam>
    /// <typeparam name="TConnection">The type of connection.</typeparam>
    /// <param name="services">A DI service collection.</param>
    /// <param name="configure">A configure action for the <see cref="ConnectionFactoryBuilder{TConnection}"/>.</param>
    /// <param name="createClient">A delegate for instantiating a client.</param>
    /// <returns>The same instance of the <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddClientFactory<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TClient,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConnection>
        (this IServiceCollection services,
        Action<ConnectionFactoryBuilder<TConnection>>? configure = null,
        CreateClientDelegate<TClient>? createClient = null)
        where TClient : Client
        where TConnection : Connection, new()
    {
        services.AddConnectionFactory(configure);

        services.TryAddSingleton(services =>
        {
            if (createClient == null)
            {
                createClient = (connection, args) => ActivatorUtilities.CreateInstance<TClient>(services, [connection, ..args]);
            }

            var connectionFactory = services.GetRequiredService<ConnectionFactory<TConnection>>();
            var clientFactory = new ClientFactory<TClient, TConnection>(connectionFactory, createClient);
            return clientFactory;
        });
        return services;
    }
}
