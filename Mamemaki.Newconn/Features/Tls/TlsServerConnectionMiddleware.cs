using Mamemaki.Newconn.Servers;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Mamemaki.Newconn.Features.Tls;

public class TlsServerConnectionMiddleware : IAsyncDisposable
{
    private readonly TlsOptions options;
    private readonly ILogger logger;
    private readonly NewconnMetrics metrics;
    private readonly X509Certificate2? certificate;
    private readonly Func<Connection, string?, X509Certificate2>? certificateSelector;

    private SslStream? sslStream;
    private SslDuplexPipe? sslDuplexPipe;

    public TlsServerConnectionMiddleware(TlsOptions options, ILogger logger, NewconnMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        // capture the certificate now so it can't be switched after validation
        certificate = options.LocalCertificate;
        certificateSelector = options.LocalServerCertificateSelector;
        if (certificate == null && certificateSelector == null)
        {
            throw new ArgumentException("Server certificate is required", nameof(options));
        }

        // If a selector is provided then ignore the cert, it may be a default cert.
        if (certificateSelector != null)
        {
            // SslStream doesn't allow both.
            certificate = null;
        }
        else
        {
            if (certificate == null)
            {
                throw new ArgumentException("Server certificate is required", nameof(options));
            }
            EnsureCertificateIsAllowedForServerAuth(certificate);
        }

        this.options = options;
        this.logger = logger;
        this.metrics = metrics;
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

    public Task<bool> OnConnectionAsync(Connection connection, CancellationToken cancellationToken)
    {
        return Task.Run(() => InnerOnConnectionAsync(connection, cancellationToken), cancellationToken);
    }

    private async Task<bool> InnerOnConnectionAsync(Connection connection, CancellationToken _)
    {
        bool certificateRequired;
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
            certificateRequired = false;
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

            certificateRequired = true;
        }

        sslStream = sslDuplexPipe.Stream;

        var metricsTagsFeature = connection.Properties.TryGet<IConnectionMetricsTagsFeature>();
        var startTimestamp = Stopwatch.GetTimestamp();
        using (var cancellationTokeSource = new CancellationTokenSource(Debugger.IsAttached ? Timeout.InfiniteTimeSpan : options.HandshakeTimeout))
        {
            try
            {
                // Adapt to the SslStream signature
                ServerCertificateSelectionCallback? selector = null;
                if (certificateSelector != null)
                {
                    selector = (sender, name) =>
                    {
                        connection.Properties.Set(sslStream);
                        var cert = certificateSelector(connection, name);
                        EnsureCertificateIsAllowedForServerAuth(cert);
                        return cert;
                    };
                }

                var sslOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    ServerCertificateSelectionCallback = selector,
                    ClientCertificateRequired = certificateRequired,
                    EnabledSslProtocols = options.SslProtocols,
                    CertificateRevocationCheckMode = options.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                    ApplicationProtocols = new List<SslApplicationProtocol>(),
                    CipherSuitesPolicy = options.CipherSuitesPolicy
                };

                options.OnAuthenticateAsServer?.Invoke(connection, sslOptions);

                metrics.TlsHandshakeStart(connection);

                await sslStream.AuthenticateAsServerAsync(sslOptions, cancellationTokeSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                RecordHandshakeFailed(connection, metrics, startTimestamp, Stopwatch.GetTimestamp(), metricsTagsFeature, ex);

                logger?.LogDebug(2, "Authentication timed out");
                await sslStream.DisposeAsync().ConfigureAwait(false);
                sslStream = null;
                throw new TlsException("Authentication timed out.", ex);
            }
            catch (Exception ex) when (ex is IOException || ex is AuthenticationException)
            {
                RecordHandshakeFailed(connection, metrics, startTimestamp, Stopwatch.GetTimestamp(), metricsTagsFeature, ex);

                logger?.LogDebug(1, ex, "Authentication failed");
                await sslStream.DisposeAsync().ConfigureAwait(false);
                sslStream = null;
                throw new TlsException("Authentication failed.", ex);
            }
        }

        metrics.TlsHandshakeStop(connection, startTimestamp, Stopwatch.GetTimestamp(), protocol: sslStream.SslProtocol);

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

        static void RecordHandshakeFailed(Connection connection, NewconnMetrics metrics, 
            long startTimestamp, long currentTimestamp, 
            IConnectionMetricsTagsFeature? metricsTagsFeature, Exception ex)
        {
            metrics.AddConnectionEndReason(metricsTagsFeature, ConnectionEndReason.TlsHandshakeError);
            metrics.TlsHandshakeStop(connection, startTimestamp, currentTimestamp, exception: ex);
        }
    }

    protected static void EnsureCertificateIsAllowedForServerAuth(X509Certificate2 certificate)
    {
        if (!CertificateLoader.IsCertificateAllowedForServerAuth(certificate))
        {
            throw new InvalidOperationException($"Invalid server certificate for server authentication: {certificate.Thumbprint}");
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
