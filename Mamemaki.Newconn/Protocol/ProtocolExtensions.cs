namespace Mamemaki.Newconn.Protocol;

public static class ProtocolExtensions
{
    public static ProtocolReaderWriter<TMessage> CreateProtocolReaderWriter<TMessage>(
        this Connection connection, Protocol<TMessage> protocol)
        => new ProtocolReaderWriter<TMessage>(connection, protocol);
}
