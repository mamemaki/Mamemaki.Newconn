using System.Buffers;

namespace Mamemaki.Newconn.Protocol;

/// <summary>
/// Represents a protocol that encodes and decodes transport stream data into protocol messages
/// The protocol is stateless.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public abstract class Protocol<TMessage>
{
    public abstract bool TryRead(ref SequenceReader<byte> reader, out TMessage? message);

    public abstract void Write(TMessage message, IBufferWriter<byte> writer);
}
