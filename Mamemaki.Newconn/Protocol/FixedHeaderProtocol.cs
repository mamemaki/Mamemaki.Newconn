using System.Buffers;

namespace Mamemaki.Newconn.Protocol;

/// <summary>
/// A base class for a protocol class that uses a fixed header protocol.<br/>
/// The fixed header protocol is defined as follows:<br/>
/// - The message consists of a fixed-length header and a body.<br/>
/// - The header includes a length of the body part.<br/>
/// - The body part is optional.<br/>
/// </summary>
/// <typeparam name="TMessage">The type of message.</typeparam>
public abstract class FixedHeaderProtocol<TMessage> : Protocol<TMessage>
{
    protected readonly int headerSize;

    public FixedHeaderProtocol(int headerSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(headerSize, 1, nameof(headerSize));
        this.headerSize = headerSize;
    }

    /// <summary>
    /// Gets body length from header payload
    /// </summary>
    /// <param name="buffer">The payload that has at least <see cref="headerSize"/> bytes.</param>
    /// <returns></returns>
    protected abstract int ReadBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer);

    /// <summary>
    /// Decode payload to a message
    /// </summary>
    /// <param name="payload">The payload that includes header and body. It has at least <see cref="headerSize"/> bytes.</param>
    /// <returns></returns>
    protected abstract TMessage DecodeMessage(ref ReadOnlySequence<byte> payload);

    public override bool TryRead(ref SequenceReader<byte> reader, out TMessage? message)
    {
        if (reader.Remaining < headerSize)
        {
            message = default;
            return false;
        }

        var header = reader.Sequence.Slice(0, headerSize);
        var bodySize = ReadBodyLengthFromHeader(ref header);
        if (bodySize < 0)
        {
            throw new ProtocolException("Failed to get body length from the header.");
        }
        else if (bodySize == 0)
        {
            message = DecodeMessage(ref header);
            reader.Advance(headerSize);
            return true;
        }

        var totalSize = headerSize + bodySize;
        if (reader.Remaining < totalSize)
        {
            message = default;
            return false;
        }

        var payload = reader.Sequence.Slice(0, totalSize);
        message = DecodeMessage(ref payload);
        reader.Advance(totalSize);
        return true;
    }
}
