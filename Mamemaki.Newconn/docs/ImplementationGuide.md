# Implementation Guide

## Implementation simple message protocol
We will explain how to build a TCP server and client using a simple message protocol example.

The complete source code can be found at [SimpleCommandProtocolTests](../../Mamemaki.Newconn.Tests/Protocol/SimpleCommandProtocolTests.cs).

### Protocol definition
The Simple Message Protocol is defined as follows:
- Messages terminate with CRLF
- Messages are UTF-8 strings
- Messages are split into two parts by the first occurrence of whitespace: the first part is the command name, the second part is the body
- The body part is optional. No whitespace means no body.

### Implementation steps
To build server and client, you need implement these components. Please refer to [Key components](../README.md#key-components) for the role of each component.

- Protocol
- Connection
- ConnectionHandler
- Client(Optional)

We will write it as a unit test. That way, you won't need to switch between server and client projects. Of course, you can get test code, and it can also be used as a playground.

### Implement Protocol

```cs
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

    public override void EncodeMessage(SimpleCommandMessage message, IBufferWriter<byte> writer)
    {
        encoding.GetBytes(message.Command, writer);
        if (!string.IsNullOrEmpty(message.Body))
        {
            encoding.GetBytes(" ", writer);
            encoding.GetBytes(message.Body, writer);
        }
    }
}
```

Firstly, you would define a message class that sent and received by the protocol.

The next step is to implement the protocol class with the message class. The protocol implementation above is based on [TerminatorProtocol](../Protocol/TerminatorProtocol.cs). It can be used for protocols where messages terminate at a specific byte sequence. There are the following protocol base classes.

- **FixedSizeProtocol**: Protocol for fixed-size messages.
- **TerminatorProtocol**: Protocol where the messages terminate at a specific byte sequence.
- **FixedHeaderProtocol**: Protocol where the message length is contained within a fixed-size header.

If you inherit from TerminatorProtocol, you need to implement the `DecodeMessage` and `EncodeMessage` methods. In `DecodeMessage`, you write the process to decode a byte sequence into a message object. In `EncodeMessage`, you write the process to encode a message object into a byte sequence. Since TerminatorProtocol handles it, the byte sequence does not include a terminator sequence.

### Implement Connection

```cs
class SimpleCommandConnection : ProtocolConnection<SimpleCommandProtocol, SimpleCommandMessage>
{
    public async ValueTask<SimpleCommandMessage> SendAndReceiveAsync(string messageStr, CancellationToken cancellationToken)
    {
        var message = Protocol.DecodeMessage(messageStr);
        return await base.SendAndReceiveAsync<SimpleCommandMessage>(message, cancellationToken);
    }
}
```

The connection class is defined by inheriting from the `ProtocolConnection` class along with the protocol class and message class.

In this example, we added the `SendAndReceiveAsync` utility method, which sends messages by string.

### Implement ConnectionHandler
```cs
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
```
The connection handler class is defined by inheriting the `ConnectionHandler` class along with the connection class. The connection handlers can also be implemented as a method, but here we are using the class pattern.

In the connection handler class, you need to implement the `OnConnectedAsync` method. In it, you write the process for how to handle the accepted connection. Exiting this method will close the connection.

In this example, it first receives and processes an authentication message. After authentication is complete successfully, then it receives command messages continuously until the client closes the connection.

### Implement Client

```cs
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
```
The client class is defined by inheriting the `Client` class. Its holds the connection. Disposing this instance will close the connection.

You would override the Connection property as shown above to make it your own connection class type.

In this example, the constructor takes a connection object and an credentials store object, and defines two business layer methods.

You're all set. Let's write the code to actually communicate in the next section.

## Using simple message protocol server and client without DI
```cs
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
```

Here, we first configure and start the server, then configure the client and connect it to the server. Finally, we perform authentication and query the weather from the client.

To configure the server, create a `NewconnServer` instance along with `NewconnServerOptions`. Then call `NewconnServer.StartAsync` to start the server. We use `await using` to ensure the `NewconnServer` instance is disposed when the test ends.

To configure the listener, we are calling the extension method `SocketListenAnyIP` on `NewconnServerOptions`. The following extension methods are available.

- **SocketListen(EndPoint)**: Configure the listener bound to an EndPoint.
- **SocketListen(IPAddress, int)**: Configure the listener bound to a IP address and port.
- **SocketListenAnyIP(int)**: Configure the listener bound to all network interfaces.
- **SocketListenLocalhost(int)**: Configure the listener bound to loopback address.

To configure the client, create a `ConnectionFactory` using `ConnectionFactoryBuilder` and create a `ClientFactory` with it. Then call `ClientFactory.ConnectAsync` to connect to the server.

## Using simple message protocol server and client with DI
Here's an example using DI version.

```cs
var cancellationToken = CancellationToken.None;
int port = 1235;

// Server
var builder = Host.CreateApplicationBuilder();
builder.Services.AddSingleton<DbContext>();
builder.Services.UseNewconnServer(server =>
{
    server.SocketListenAnyIP<SimpleCommandConnection>(port, builder =>
    {
        builder.UseConnectionHandler<SimpleCommandConnectionHandler>();
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
```

It performs the same actions as the version without DI.

`SimpleCommandConnectionHandler` and `SimpleCommandClient` are instantiated by [ActivatorUtilities.CreateInstance](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities.createinstance).
