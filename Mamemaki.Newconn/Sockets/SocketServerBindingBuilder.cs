using Mamemaki.Newconn.Servers;

namespace Mamemaki.Newconn.Sockets;

public class SocketServerBindingBuilder<TConnection> : ServerBindingBuilder<TConnection>
    where TConnection : Connection, new()
{
    public SocketConnectionFactoryOptions TransportOptions { get => ((SocketConnectionFactory<TConnection>)ConnectionFactory).Options; }

    public SocketServerBindingBuilder(
        SocketConnectionFactory<TConnection> connectionFactory,
        IServiceProvider? serviceProvider = null)
        : base(connectionFactory, serviceProvider)
    {
    }
}
