using Mamemaki.Newconn.Features.Tls;
using Mamemaki.Newconn.Internal;
using Mamemaki.Newconn.Clients;
using Mamemaki.Newconn.Servers;
using Mamemaki.Newconn.Tests.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.Metrics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Mamemaki.Newconn.Tests.Features;

public class TlsTests
{
    static readonly X509Certificate2 TestCert = X509CertificateLoader.LoadPkcs12FromFile("Features/testcert.pfx", "testcert");

    TestServer CreateServer(Action<TestServerOptions> configure)
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<IMeterFactory>(new DefaultMeterFactory());
        services.TryAddSingleton<NewconnMetrics>();
        var serviceProvider = services.BuildServiceProvider();

        var options = new TestServerOptions(serviceProvider);
        configure.Invoke(options);
        return new TestServer(options);
    }

    bool VerifyTestCert(X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors policyErrors)
    {
        if (certificate == null)
            return false;
        if (certificate.Subject == "CN=test.local" && certificate.Thumbprint == "F9FA56E896BA208CEFE2E04F54B13EDC2AF09C89")
            return true;
        return false;
    }

    [Fact]
    public async Task WithServerCertificate()
    {
        var tlsOptionsForServer = new TlsOptions();
        tlsOptionsForServer.RemoteCertificateMode = RemoteCertificateMode.NoCertificate;
        tlsOptionsForServer.LocalCertificate = TestCert;

        var tlsOptionsForClient = new TlsOptions();
        tlsOptionsForClient.RemoteCertificateValidation = VerifyTestCert;

        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseServerTls(tlsOptionsForServer);
            options.MiddlewaresOnClient.UseClientTls(tlsOptionsForClient);
        }))
        {
            await using (var session = await server.CreateSessionAsnc())
            {
                await session.ServerSendAsync("Hello"u8.ToArray());

                var buffer = await session.ClientReceiveAsync();
                var text = Encoding.UTF8.GetString(buffer);
                Assert.Equal("Hello", text);
            }
        }
    }

    [Fact]
    public async Task WithServerAndClientCertificate()
    {
        var tlsOptionsForServer = new TlsOptions();
        tlsOptionsForServer.LocalCertificate = TestCert;
        tlsOptionsForServer.RemoteCertificateValidation = VerifyTestCert;

        var tlsOptionsForClient = new TlsOptions();
        tlsOptionsForClient.LocalCertificate = TestCert;
        tlsOptionsForClient.RemoteCertificateValidation = VerifyTestCert;

        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseServerTls(tlsOptionsForServer);
            options.MiddlewaresOnClient.UseClientTls(tlsOptionsForClient);
        }))
        {
            await using (var session = await server.CreateSessionAsnc())
            {
                await session.ServerSendAsync("Hello"u8.ToArray());

                var buffer = await session.ClientReceiveAsync();
                var text = Encoding.UTF8.GetString(buffer);
                Assert.Equal("Hello", text);
            }
        }
    }

    [Fact]
    public async Task Handshake_Timeout()
    {
        var tlsOptionsForServer = new TlsOptions();
        tlsOptionsForServer.HandshakeTimeout = TimeSpan.FromMilliseconds(100);
        tlsOptionsForServer.RemoteCertificateMode = RemoteCertificateMode.NoCertificate;
        tlsOptionsForServer.LocalCertificate = TestCert;

        await using (var server = CreateServer(options =>
        {
            options.MiddlewaresOnServer.UseServerTls(tlsOptionsForServer);
        }))
        {
            var ex = await Assert.ThrowsAsync<TlsException>(async () => await server.CreateSessionAsnc());
            Assert.Equal("Authentication timed out.", ex.Message);
        }
    }
}
