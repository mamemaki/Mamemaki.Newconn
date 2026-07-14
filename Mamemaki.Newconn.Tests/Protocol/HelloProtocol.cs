using Mamemaki.Newconn.Protocol;
using System.Buffers;
using System.Text;

namespace Mamemaki.Newconn.Tests.Protocol;

public class HelloProtocol : FixedSizeProtocol<string>
{
    public static readonly string Data = "Hello World";
    public static readonly byte[] DataBytes = "Hello World"u8.ToArray();
    public static readonly Encoding Encoding = new UTF8Encoding(false);

    public HelloProtocol()
        : base(Data.Length)
    {
    }

    protected override string DecodeMessage(ref ReadOnlySequence<byte> payload)
    {
        var text = Encoding.GetString(payload);
        return text;
    }

    public override void Write(string message, IBufferWriter<byte> writer)
    {
        writer.Write(Encoding.GetBytes(message));
    }
}
