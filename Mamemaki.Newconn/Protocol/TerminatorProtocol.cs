using System.Buffers;

namespace Mamemaki.Newconn.Protocol;

/// <summary>
/// A base class for a protocol class that uses a terminator protocol.<br/>
/// The terminator protocol is defined as follows:<br/>
/// - The message ends with a specific sequence.<br/>
/// </summary>
/// <typeparam name="TMessage">The type of message.</typeparam>
public abstract class TerminatorProtocol<TMessage> : Protocol<TMessage>
{
    /// <summary>
    /// The termination sequence for the protocol
    /// </summary>
    private readonly byte[] terminator;

    /// <summary>
    /// Creates an instance of <see cref="TerminatorProtocol{TMessage}"/> with a termination sequence
    /// </summary>
    /// <param name="terminator">The termination sequence</param>
    public TerminatorProtocol(byte[] terminator)
    {
        if (terminator.Length == 0) throw new ArgumentException("terminator must not be empty", nameof(terminator));
        this.terminator = terminator;
    }

    /// <summary>
    /// Decode a byte sequence into a message object
    /// The byte sequence does not contain the termination sequence.
    /// </summary>
    /// <param name="payload">The byte sequence</param>
    /// <returns>Decoded message object</returns>
    protected abstract TMessage DecodeMessage(ref ReadOnlySequence<byte> payload);

    /// <summary>
    /// Encode a message object into a byte sequence
    /// The byte sequence does not contain the termination sequence.
    /// </summary>
    /// <param name="message">The message object to encode</param>
    /// <param name="writer">A buffer writer for the byte sequence</param>
    protected abstract void EncodeMessage(TMessage message, IBufferWriter<byte> writer);

    public override bool TryRead(ref SequenceReader<byte> reader, out TMessage? message)
    {
        if (!reader.TryReadTo(out ReadOnlySequence<byte> payload, terminator))
        {
            message = default;
            return false;
        }

        try
        {
            message = DecodeMessage(ref payload);
            return true;
        }
        catch (Exception)
        {
            reader.Rewind(payload.Length + terminator.Length);
            throw;
        }
    }

    public override void Write(TMessage message, IBufferWriter<byte> writer)
    {
        EncodeMessage(message, writer);
        writer.Write(terminator);
    }
}
