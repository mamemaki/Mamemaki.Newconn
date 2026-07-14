using System.Security.Authentication;

namespace Mamemaki.Newconn.Features.Tls;

public interface ITlsHandshakeFeature
{
    SslProtocols Protocol { get; }

    CipherAlgorithmType CipherAlgorithm { get; }

    int CipherStrength { get; }

    HashAlgorithmType HashAlgorithm { get; }

    int HashStrength { get; }

    ExchangeAlgorithmType KeyExchangeAlgorithm { get; }

    int KeyExchangeStrength { get; }
}
