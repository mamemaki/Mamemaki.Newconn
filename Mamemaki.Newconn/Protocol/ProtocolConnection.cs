namespace Mamemaki.Newconn.Protocol;

/// <summary>
/// Represents a connection using the Protocol.
/// </summary>
/// <typeparam name="TProtocol">The type of the protocol.</typeparam>
/// <typeparam name="TMessage">The type of the message.</typeparam>
public class ProtocolConnection<TProtocol, TMessage> : Connection
    where TProtocol : Protocol<TMessage>, new()
{
    public static readonly TProtocol Protocol = new();

    protected ProtocolReaderWriter<TMessage> protocolReaderWriter = default!;

    protected override async ValueTask OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        protocolReaderWriter = this.CreateProtocolReaderWriter(Protocol);
    }

    /// <summary>
    /// Send a message defined by the protocol.
    /// </summary>
    /// <param name="message">A message object to send.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    public virtual ValueTask SendAsync(TMessage message, CancellationToken cancellationToken)
    {
        return protocolReaderWriter.WriteAsync(message, cancellationToken);
    }

    /// <summary>
    /// Receives a message defined by the protocol.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A received <see cref="ValueTask<TMessage>"/> object.</returns>
    public virtual ValueTask<TMessage> ReceiveAsync(CancellationToken cancellationToken)
    {
        return protocolReaderWriter.ReadOneAsync(cancellationToken);
    }

    /// <summary>
    /// Receives a message defined by the protocol as specified message type.
    /// </summary>
    /// <typeparam name="TReceiveMessage">The type of the message.</typeparam>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A received <see cref="ValueTask<TMessage>"/> object.</returns>
    /// <exception cref="Exception">If the received message type does not match to the <see cref="{TReceiveMessage}"/> type.</exception>
    public async virtual ValueTask<TReceiveMessage> ReceiveAsAsync<TReceiveMessage>(CancellationToken cancellationToken)
    {
        var message = await ReceiveAsync(cancellationToken);
        if (message is TReceiveMessage typedMessage)
            return typedMessage;
        throw new Exception($"Message({message?.GetType().Name}) is not {typeof(TReceiveMessage).Name}");
    }

    /// <summary>
    /// Sends and receives a message defined by the protocol as specified message type.
    /// </summary>
    /// <typeparam name="TReceiveMessage">The type of the message.</typeparam>
    /// <param name="message">A message object to send.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A received <see cref="ValueTask<TMessage>"/> object.</returns>
    public async virtual ValueTask<TReceiveMessage> SendAndReceiveAsync<TReceiveMessage>(
        TMessage message, CancellationToken cancellationToken)
        where TReceiveMessage : TMessage
    {
        await protocolReaderWriter.WriteAsync(message, cancellationToken);
        return await ReceiveAsAsync<TReceiveMessage>(cancellationToken);
    }

    /// <summary>
    /// Receives messages that are defined by the protocol as continually.
    /// </summary>
    /// <param name="messageHandler">A message handler that is called when a message is received.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> for the asynchronous operation.</returns>
    public async virtual Task RunReceiveLoopAsync(Func<TMessage, CancellationToken, ValueTask> messageHandler,
        CancellationToken cancellationToken)
    {
        await foreach (var message in protocolReaderWriter.RunReadLoopAsync(cancellationToken))
        {
            await messageHandler(message, cancellationToken);
        }
    }
}
