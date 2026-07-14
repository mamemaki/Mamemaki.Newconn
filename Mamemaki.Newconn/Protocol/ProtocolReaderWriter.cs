//#define VERBOSE
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace Mamemaki.Newconn.Protocol;

/// <summary>
/// Represents a protocol reader writer.
/// </summary>
/// <typeparam name="TMessage">The type of message.</typeparam>
public class ProtocolReaderWriter<TMessage>
{
    [Conditional("VERBOSE")]
    private void DebugLog(string message, [CallerMemberName] string? caller = null) =>
        DebugHelper.Log(connection.ConnectionNo, message, $"{caller}");

    private readonly Connection connection;
    private readonly Protocol<TMessage> protocol;
    private readonly PipeReader pipeReader;
    private readonly PipeWriter pipeWriter;

    /// <summary>
    /// Maximum byte size for a message. No limit if set null. Defaults is 1MiB.
    /// </summary>
    public int? MaxMessageSizeInBytes { get; private set; }

    /// <summary>
    /// Throw when there are incomplete data. Defaults is true.
    /// </summary>
    public bool ThrowWhenIncompleteData { get; private set; }

    public const int DefaultMaximumMessageBytes = 1 * 1024 * 1024;   // 1MiB

    /// <summary>
    /// Create an initialized <see cref="ProtocolReaderWriter{TMessage}"/>.
    /// </summary>
    /// <param name="connection">A connection.</param>
    /// <param name="protocol">A protocol.</param>
    /// <param name="maximumMessageBytes">A maximum byte size for a message. No limit if set null. Defaults is 1MiB.</param>
    /// <param name="throwWhenIncompleteData">If true, throw when there are incomplete data. Defaults is true.</param>
    public ProtocolReaderWriter(Connection connection, Protocol<TMessage> protocol,
        int? maximumMessageBytes = DefaultMaximumMessageBytes,
        bool throwWhenIncompleteData = true)
    {
        this.connection = connection;
        this.protocol = protocol;
        this.pipeReader = connection.Transport.Input;
        this.pipeWriter = connection.Transport.Output;
        this.MaxMessageSizeInBytes = maximumMessageBytes;
        this.ThrowWhenIncompleteData = throwWhenIncompleteData;
    }

    public async IAsyncEnumerable<TMessage> RunReadLoopAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DebugLog("starting read loop");

        try
        {
            while (true)
            {
                ReadResult result;
                try
                {
                    DebugLog($"reading next data..");
                    result = await pipeReader.ReadAsync(cancellationToken);
                    if (result.IsCanceled)
                    {
                        DebugLog("canceled");
                        break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (IsConnectionClosed())
                        break;
                    DebugLog($"exit: unexpected error({ex.Message})");
                    throw;
                }

                var buffer = result.Buffer;
                var overLength = false;
                if (MaxMessageSizeInBytes.HasValue && buffer.Length > MaxMessageSizeInBytes)
                {
                    buffer = buffer.Slice(buffer.Start, MaxMessageSizeInBytes.Value);
                    overLength = true;
                }

                DebugLog($"data({buffer.Length} bytes) is arrived. trying decode.. ({result.IsCompleted}, {overLength})");
                long consumedTotal = 0;
                while (TryRead(ref buffer, out var message, consumedTotal, out var consumed))
                {
                    consumedTotal += consumed;

                    yield return message;
                    if (consumedTotal >= buffer.Length)
                        break;
                }

                if (overLength)
                {
                    throw new InvalidDataException($"The maximum message size of {MaxMessageSizeInBytes}B was exceeded.");
                }

                if (connection.IsClosed)
                {
                    DebugLog("connection closed");
                    break;
                }

                try
                {
                    DebugLog($"advancing to {consumedTotal}");
                    pipeReader.AdvanceTo(buffer.GetPosition(consumedTotal), buffer.End);
                }
                catch (Exception ex)
                {
                    if (IsConnectionClosed())
                        break;
                    DebugLog($"exit: unexpected error({ex.Message})");
                    throw;
                }

                if (result.IsCompleted)
                {
                    DebugLog("completed");
                    if (ThrowWhenIncompleteData && buffer.Length - consumedTotal > 0)
                    {
                        throw new InvalidDataException("There are incomplete data.");
                    }
                    break;
                }
            }
        }
        finally
        {
            DebugLog("marking reader as completed");
            await pipeReader.CompleteAsync();
        }

        yield break;
    }

    private bool IsConnectionClosed()
    {
        if (connection.IsClosed)
        {
            DebugLog("exit: connection closed");
            return true;
        }
        return false;
    }

    public async ValueTask<TMessage> ReadOneAsync(CancellationToken cancellationToken = default)
    {
        var message = await TryReadOneAsync(cancellationToken);
        if (message == null)
            throw new ConnectionClosedByRemoteException();

        return message;
    }

    public async ValueTask<TMessage?> TryReadOneAsync(CancellationToken cancellationToken = default)
    {
        DebugLog("starting read one");

        try
        {
            while (true)
            {
                DebugLog($"reading next data..");
                var result = await pipeReader.ReadAsync(cancellationToken);
                if (result.IsCanceled)
                    throw new OperationCanceledException();

                var buffer = result.Buffer;
                var overLength = false;
                if (MaxMessageSizeInBytes.HasValue && buffer.Length > MaxMessageSizeInBytes)
                {
                    buffer = buffer.Slice(buffer.Start, MaxMessageSizeInBytes.Value);
                    overLength = true;
                }

                DebugLog($"data({buffer.Length} bytes) is arrived. trying decode.. ({result.IsCompleted}, {overLength})");
                if (TryRead(ref buffer, out var message, 0, out var consumed))
                {
                    pipeReader.AdvanceTo(buffer.GetPosition(consumed));
                    return message;
                }
                else if (overLength)
                {
                    throw new InvalidDataException($"The maximum message size of {MaxMessageSizeInBytes}B was exceeded.");
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    DebugLog("completed");
                    break;
                }
            }

            return default;
        }
        catch (OperationCanceledException)
        {
            DebugLog("canceled");
            throw;
        }
        catch (Exception ex)
        {
            if (IsConnectionClosed())
                return default;
            DebugLog($"exit: unexpected error({ex.Message})");
            throw;
        }
    }

    private bool TryRead(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out TMessage? message, 
        long offset, out long consumed)
    {
        var reader = new SequenceReader<byte>(offset == 0 ? buffer : buffer.Slice(offset));
        var ret = protocol.TryRead(ref reader, out message);
        consumed = reader.Consumed;
        return ret;
    }

    public async ValueTask WriteAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        protocol.Write(message, pipeWriter);

        await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
