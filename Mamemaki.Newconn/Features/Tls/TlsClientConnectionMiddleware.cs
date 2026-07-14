using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Mamemaki.Newconn.Features.Tls;

public class TlsClientConnectionMiddleware : IAsyncDisposable
{
    private readonly TlsOptions options;
    private readonly ILogger logger;
    private readonly X509Certificate2? certificate;

    private SslStream? sslStream;
    private SslDuplexPipe? sslDuplexPipe;

    public TlsClientConnectionMiddleware(TlsOptions options, ILogger<TlsClientConnectionMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        // capture the certificate now so it can't be switched after validation
        certificate = options.LocalCertificate;

        if (certificate != null)
        {
            EnsureCertificateIsAllowedForClientAuth(certificate);
        }

        this.options = options;
        this.logger = logger;
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose the inner stream (SslDuplexPipe) before disposing the SslStream
        // as the duplex pipe can hit an ODE as it still may be writing.
        if (sslDuplexPipe != null)
        {
            await sslDuplexPipe.DisposeAsync();
            sslDuplexPipe = null;
        }
        if (sslStream != null)
        {
            await sslStream.DisposeAsync();
            sslStream = null;
        }
        GC.SuppressFinalize(this);
    }

    public async Task<bool> OnConnectionAsync(Connection connection, CancellationToken _)
    {
        var feature = new TlsConnectionFeature();
        connection.Properties.Set<ITlsConnectionFeature>(feature);
        connection.Properties.Set<ITlsHandshakeFeature>(feature);

        var memoryPool = connection.Properties.Get<IMemoryPoolFeature>().MemoryPool;
        
        var inputPipeOptions = new StreamPipeReaderOptions
        (
            pool: memoryPool,
            bufferSize: memoryPool.GetMinimumSegmentSize(),
            minimumReadSize: memoryPool.GetMinimumAllocSize(),
            leaveOpen: true
        );

        var outputPipeOptions = new StreamPipeWriterOptions
        (
            pool: memoryPool,
            leaveOpen: true
        );

        if (options.RemoteCertificateMode == RemoteCertificateMode.NoCertificate)
        {
            sslDuplexPipe = new SslDuplexPipe(connection, connection.Transport, inputPipeOptions, outputPipeOptions);
        }
        else
        {
            sslDuplexPipe = new SslDuplexPipe(connection, connection.Transport, inputPipeOptions, outputPipeOptions, s => new SslStream(
                s,
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (certificate == null)
                    {
                        return options.RemoteCertificateMode != RemoteCertificateMode.RequireCertificate;
                    }

                    if (options.RemoteCertificateValidation == null)
                    {
                        if (sslPolicyErrors != SslPolicyErrors.None)
                        {
                            return false;
                        }
                    }

                    var certificate2 = ConvertToX509Certificate2(certificate);
                    if (certificate2 == null)
                    {
                        return false;
                    }

                    if (options.RemoteCertificateValidation != null)
                    {
                        if (!options.RemoteCertificateValidation(certificate2, chain, sslPolicyErrors))
                        {
                            return false;
                        }
                    }

                    return true;
                }));
        }

        sslStream = sslDuplexPipe.Stream;

        using (var cancellationTokeSource = new CancellationTokenSource(options.HandshakeTimeout))
        {
            try
            {
                var sslOptions = new SslClientAuthenticationOptions
                {
                    ClientCertificates = new X509CertificateCollection(new[] { certificate }.OfType<X509Certificate2>().ToArray()),
                    EnabledSslProtocols = options.SslProtocols,
                    CertificateRevocationCheckMode = options.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                    ApplicationProtocols = new List<SslApplicationProtocol>(),
                };

                options.OnAuthenticateAsClient?.Invoke(connection, sslOptions);

                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationTokeSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                logger?.LogDebug(2, "Authentication timed out");
                await sslStream.DisposeAsync().ConfigureAwait(false);
                sslStream = null;
                throw new TlsException("Authentication timed out.", ex);
            }
            catch (Exception ex) when (ex is IOException || ex is AuthenticationException)
            {
                logger?.LogDebug(1, ex, "Authentication failed");
                await sslStream.DisposeAsync().ConfigureAwait(false);
                sslStream = null;
                throw new TlsException("Authentication failed.", ex);
            }
        }

        feature.ApplicationProtocol = sslStream.NegotiatedApplicationProtocol.Protocol;
        connection.Properties.Set<ITlsApplicationProtocolFeature>(feature);
        feature.LocalCertificate = ConvertToX509Certificate2(sslStream.LocalCertificate);
        feature.RemoteCertificate = ConvertToX509Certificate2(sslStream.RemoteCertificate);
        feature.CipherAlgorithm = sslStream.CipherAlgorithm;
        feature.CipherStrength = sslStream.CipherStrength;
        feature.HashAlgorithm = sslStream.HashAlgorithm;
        feature.HashStrength = sslStream.HashStrength;
        feature.KeyExchangeAlgorithm = sslStream.KeyExchangeAlgorithm;
        feature.KeyExchangeStrength = sslStream.KeyExchangeStrength;
        feature.Protocol = sslStream.SslProtocol;

        var originalTransport = connection.Transport;

        connection.Transport = sslDuplexPipe;

        return true;
    }

    protected static void EnsureCertificateIsAllowedForClientAuth(X509Certificate2 certificate)
    {
        if (!CertificateLoader.IsCertificateAllowedForClientAuth(certificate))
        {
            throw new InvalidOperationException($"Invalid client certificate for client authentication: {certificate.Thumbprint}");
        }
    }

    private static X509Certificate2? ConvertToX509Certificate2(X509Certificate? certificate)
    {
        if (certificate is null)
        {
            return null;
        }

        return certificate as X509Certificate2 ?? new X509Certificate2(certificate);
    }
}
