using System.Net;
using System.Runtime.CompilerServices;

namespace Mamemaki.Newconn.Servers;

public class LocalhostBinding<TConnection>(int port,
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
        var exceptions = new List<Exception>();

        INewconnListener? ipv6Server = null;
        INewconnListener? ipv4Server = null;

        try
        {
            ipv6Server = await EndPointBinding<TConnection>.BindAsync(new IPEndPoint(IPAddress.IPv6Loopback, port),
                properties, serverContext, options, connectionFactory, onConnection, cancellationToken);
        }
        catch (Exception ex) when (ex is not IOException)
        {
            exceptions.Add(ex);
        }

        if (ipv6Server != null)
        {
            yield return ipv6Server;
        }

        try
        {
            ipv4Server = await EndPointBinding<TConnection>.BindAsync(new IPEndPoint(IPAddress.Loopback, port),
                properties, serverContext, options, connectionFactory, onConnection, cancellationToken);
        }
        catch (Exception ex) when (ex is not IOException)
        {
            exceptions.Add(ex);
        }

        if (exceptions.Count == 2)
        {
            throw new IOException($"Failed to bind to {this}", new AggregateException(exceptions));
        }

        if (ipv4Server != null)
        {
            yield return ipv4Server;
        }
    }

    public override string ToString()
    {
        return $"localhost:{port}";
    }
}
