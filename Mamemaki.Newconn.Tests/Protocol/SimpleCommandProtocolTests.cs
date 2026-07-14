using Mamemaki.Newconn.Clients;
using Mamemaki.Newconn.Hosting;
using Mamemaki.Newconn.Protocol;
using Mamemaki.Newconn.Servers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Buffers;
using System.Net;
using System.Text;

namespace Mamemaki.Newconn.Tests.Protocol;

public class CredentialStore
{
    public string Password { get; set; } = "mysecret";
}

public class DbContext
{
    public string Password { get; set; } = "mysecret";
}

public class SimpleCommandMessage
{
    public required string Command { get; set; }
    public string? Body { get; set; }
}

public class SimpleCommandProtocol : TerminatorProtocol<SimpleCommandMessage>
{
    private static readonly byte[] terminator = new[] { (byte)'\r', (byte)'\n' };
    private static readonly Encoding encoding = new UTF8Encoding(false);

    public SimpleCommandProtocol()
        : base(terminator)
    {
    }

    public SimpleCommandMessage DecodeMessage(string text)
    {
        try
        {
            var parts = text.Split(' ', 2);
            var command = parts[0];
            var body = parts.Length > 1 ? parts[1] : null;
            return new SimpleCommandMessage()
            {
                Command = command,
                Body = body,
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse message({text})", ex);
        }
    }

    protected override SimpleCommandMessage DecodeMessage(ref ReadOnlySequence<byte> payload)
    {
        var text = encoding.GetString(payload);
        return DecodeMessage(text);
    }

    protected override void EncodeMessage(SimpleCommandMessage message, IBufferWriter<byte> writer)
    {
        encoding.GetBytes(message.Command, writer);
        if (!string.IsNullOrEmpty(message.Body))
        {
            encoding.GetBytes(" ", writer);
            encoding.GetBytes(message.Body, writer);
        }
    }
}

class SimpleCommandConnection : ProtocolConnection<SimpleCommandProtocol, SimpleCommandMessage>
{
    public async ValueTask<SimpleCommandMessage> SendAndReceiveAsync(string messageStr, CancellationToken cancellationToken)
    {
        var message = Protocol.DecodeMessage(messageStr);
        return await base.SendAndReceiveAsync<SimpleCommandMessage>(message, cancellationToken);
    }
}

class SimpleCommandConnectionHandler(DbContext dbContext) : ConnectionHandler<SimpleCommandConnection>
{
    public override async Task OnConnectedAsync(SimpleCommandConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            // Receive an authenticate command
            var authMsg = await connection.ReceiveAsync(cancellationToken);
            var isAuthOK = authMsg.Command == "AUTH" && authMsg.Body == dbContext.Password;
            if (!isAuthOK)
            {
                await connection.SendAsync(new SimpleCommandMessage() { Command = "NG", Body = "Forbidden" }, cancellationToken);
                return;
            }
            await connection.SendAsync(new SimpleCommandMessage() { Command = "OK" }, cancellationToken);

            await connection.RunReceiveLoopAsync(async (message, cancellationToken) =>
            {
                var responseBody = message.Command switch
                {
                    "GET.WEATHER" => message.Body == "london" ? "always drizzle" : "sunny",
                    _ => throw new Exception($"Unknown command({message.Command})"),
                };
                await connection.SendAsync(new SimpleCommandMessage() { Command = "OK", Body = responseBody }, cancellationToken);
            }, cancellationToken);
        }
        catch (ConnectionClosedByRemoteException)
        {
            // Connection closed by remote host. We will finish the process gracefully.
        }
        catch (OperationCanceledException)
        {
            // Application shutdown, cancelled by user or connection aborted by local.
            // Anyway, we will finish the process gracefully.
        }
        catch (Exception)
        {
            // Unexpected error occurred. we will close the connection immediately.
        }
    }
}

class SimpleCommandClient(SimpleCommandConnection connection, CredentialStore credentialStore) : Client
{
    protected override SimpleCommandConnection Connection { get; } = connection;

    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        var res = await Connection.SendAndReceiveAsync($"AUTH {credentialStore.Password}", cancellationToken);
        if (res.Command != "OK")
            throw new Exception($"Authentication failed({res.Body})");
    }

    public async Task<string?> GetWeatherAsync(string location, CancellationToken cancellationToken)
    {
        var res = await Connection.SendAndReceiveAsync($"GET.WEATHER {location}", cancellationToken);
        if (res.Command != "OK")
            throw new Exception($"Failed to get weather({res.Body})");
        return res.Body;
    }
}

public class SimpleCommandProtocolTests
{
    [Fact]
    public async Task WithoutDI()
    {
        var cancellationToken = CancellationToken.None;
        int port = 1234;

        // Server
        var options = new NewconnServerOptions();
        var connectionHandler = new SimpleCommandConnectionHandler(new DbContext());
        options.SocketListenAnyIP<SimpleCommandConnection>(port,
            builder => builder.Run(connectionHandler.OnConnectedAsync));
        await using var server = new NewconnServer(options);
        await server.StartAsync(cancellationToken);

        // Client
        var connectionFactory = new ConnectionFactoryBuilder<SimpleCommandConnection>()
            .Build();
        var clientFactory = new ClientFactory<SimpleCommandClient, SimpleCommandConnection>(connectionFactory, 
            (connection, args) => new SimpleCommandClient((SimpleCommandConnection)connection, new CredentialStore()));
        await using var client = await clientFactory.ConnectAsync(
            IPEndPoint.Parse($"127.0.0.1:{port}"), null, cancellationToken);

        // Authenticate
        await client.AuthenticateAsync(cancellationToken);

        // Query weather
        var weather = await client.GetWeatherAsync("london", cancellationToken);
        Assert.Equal("always drizzle", weather);
    }

    [Fact]
    public async Task WithDI()
    {
        var cancellationToken = CancellationToken.None;
        int port = 1235;

        // Server
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<DbContext>();
        builder.Services.UseNewconnServer(server =>
        {
            server.SocketListenAnyIP<SimpleCommandConnection>(port, builder =>
            {
                builder.Run<SimpleCommandConnectionHandler>();
            });
        });
        using var host = builder.Build();
        host.Start();

        // Client
        var services = new ServiceCollection();
        services.AddSingleton<CredentialStore>();
        services.AddClientFactory<SimpleCommandClient, SimpleCommandConnection>();
        var serviceProvider = services.BuildServiceProvider();
        var clientFactory = serviceProvider.GetRequiredService<ClientFactory<SimpleCommandClient, SimpleCommandConnection>>();
        await using var client = await clientFactory.ConnectAsync(
            IPEndPoint.Parse($"127.0.0.1:{port}"), null, cancellationToken);

        // Authenticate
        await client.AuthenticateAsync(cancellationToken);

        // Query weather
        var weather = await client.GetWeatherAsync("london", cancellationToken);
        Assert.Equal("always drizzle", weather);
    }
}
