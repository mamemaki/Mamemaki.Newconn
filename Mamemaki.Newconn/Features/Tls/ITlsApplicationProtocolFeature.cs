namespace Mamemaki.Newconn.Features.Tls;

public interface ITlsApplicationProtocolFeature
{
    ReadOnlyMemory<byte> ApplicationProtocol { get; }
}
