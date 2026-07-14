using System.Buffers;

namespace Mamemaki.Newconn.Protocol;

/// <summary>
/// A base class for a protocol class that uses a fixed size protocol.<br/>
/// The fixed size protocol is defined as follows:<br/>
/// - The message is fixed-length.<br/>
/// </summary>
/// <typeparam name="TMessage">The type of message.</typeparam>
public abstract class FixedSizeProtocol<TMessage> : Protocol<TMessage>
{
    protected readonly int messageSize;

    public FixedSizeProtocol(int messageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(messageSize, 1, nameof(messageSize));
        this.messageSize = messageSize;
    }

    protected abstract TMessage DecodeMessage(ref ReadOnlySequence<byte> payload);

    public override bool TryRead(ref SequenceReader<byte> reader, out TMessage? message)
    {
        if (reader.Remaining < messageSize)
        {
            message = default;
            return false;
        }

        var payload = reader.Sequence.Slice(0, messageSize);

        message = DecodeMessage(ref payload);
        reader.Advance(messageSize);
        return true;
    }
}
