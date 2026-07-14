namespace Mamemaki.Newconn.Servers;

public abstract class ServerBinding
{
    public abstract IAsyncEnumerable<INewconnListener> BindAsync(
        IServerContext serverContext, IConnectionProperties? properties = null, 
        CancellationToken cancellationToken = default);
}
