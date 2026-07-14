using Microsoft.Extensions.Logging;
using System.Net;
using System.Runtime.CompilerServices;

namespace Mamemaki.Newconn.Servers;

public class EndPointBinding<TConnection>(EndPoint? endPoint,
    ServerBindingOptions options,
    ConnectionFactory<TConnection> connectionFactory, 
    ConnectionDelegate<TConnection> onConnection) : ServerBinding
    where TConnection : Connection, new()
{
    public override async IAsyncEnumerable<INewconnListener> BindAsync(
        IServerContext serverContext,
        IConnectionProperties? properties = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return await BindAsync(endPoint, properties, serverContext, options, 
            connectionFactory, onConnection, cancellationToken);
    }

    internal static async ValueTask<INewconnListener> BindAsync(EndPoint? endPoint,
        IConnectionProperties? properties, IServerContext serverContext, ServerBindingOptions options,
        ConnectionFactory<TConnection> connectionFactory,
        ConnectionDelegate<TConnection> onConnection, CancellationToken cancellationToken)
    {
        var listener = await connectionFactory.BindAsync(endPoint, properties, cancellationToken);
        var logger = serverContext.LoggerFactory.CreateLogger<TConnection>();
        return new NewconnListener<TConnection>(logger, options, listener, onConnection);
    }

    public override string ToString()
    {
        return endPoint?.ToString()!;
    }
}
