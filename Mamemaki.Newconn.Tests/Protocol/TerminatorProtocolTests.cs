using Mamemaki.Newconn.Protocol;
using System.Buffers;
using System.Text;

namespace Mamemaki.Newconn.Tests.Protocol;

public class TerminatorProtocolTests
{
    public class TerminatorUtf8Protocol : TerminatorProtocol<string>
    {
        private static readonly byte[] Terminator = new[] { (byte)'\r', (byte)'\n' };
        public static readonly Encoding Encoding = new UTF8Encoding(false);
        private readonly bool throwInDecode;
        private readonly bool throwInEncode;

        public TerminatorUtf8Protocol(byte[]? terminator = null, 
            bool throwInDecode = false, bool throwInEncode = false)
            : base(terminator ?? Terminator)
        {
            this.throwInDecode = throwInDecode;
            this.throwInEncode = throwInEncode;
        }

        protected override string DecodeMessage(ref ReadOnlySequence<byte> payload)
        {
            if (throwInDecode)
                throw new Exception("failed.");
            var text = Encoding.GetString(payload);
            return text;
        }

        protected override void EncodeMessage(string message, IBufferWriter<byte> writer)
        {
            if (throwInEncode)
                throw new Exception("failed.");
            writer.Write(Encoding.GetBytes(message));
        }
    }

    [Theory]
    [InlineData(new byte[] { })]
    public void InvalidTerminator(byte[] terminator)
    {
        Assert.Throws<ArgumentException>(() => new TerminatorUtf8Protocol(terminator));
    }

    [Fact]
    public void TryRead()
    {
        var buffer = "Hello World\r\n"u8.ToArray();
        var protocol = new TerminatorUtf8Protocol();
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer));
        Assert.True(protocol.TryRead(ref reader, out var message));
        Assert.Equal("Hello World", message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(12)]
    public void TryRead_Insufficient(int trimPos)
    {
        var buffer = "Hello World\r\n"u8.ToArray();
        var protocol = new TerminatorUtf8Protocol();
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer).Slice(0, trimPos));
        Assert.False(protocol.TryRead(ref reader, out var _));
    }

    [Theory]
    [InlineData(13)]
    [InlineData(20)]
    [InlineData(25)]
    public void TryRead_InsufficientAt2ndSegment(int trimPos)
    {
        var buffer = ("Hello World\r\n"u8 + "Hello World\r\n"u8).ToArray();
        var protocol = new TerminatorUtf8Protocol();
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer).Slice(0, trimPos));
        Assert.True(protocol.TryRead(ref reader, out var message));
        Assert.Equal("Hello World", message);
        Assert.False(protocol.TryRead(ref reader, out var _));
    }

    [Fact]
    public void TryRead_NoTerminator()
    {
        var buffer = "Hello World"u8.ToArray();
        var protocol = new TerminatorUtf8Protocol();
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer));
        Assert.False(protocol.TryRead(ref reader, out var _));
    }

    [Fact]
    public void TryRead_DecodeMessage_Threw()
    {
        var buffer = "Hello World\r\n"u8.ToArray();
        var protocol = new TerminatorUtf8Protocol(throwInDecode: true);
        var ex = Assert.Throws<Exception>(() =>
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer));
            try
            {
                protocol.TryRead(ref reader, out var message);
            }
            finally
            {
                Assert.Equal(buffer.Length, reader.Remaining);
            }
        });
        Assert.Equal("failed.", ex.Message);
    }

    [Fact]
    public void Write()
    {
        var protocol = new TerminatorUtf8Protocol();
        var writer = new ArrayBufferWriter<byte>();
        protocol.Write("Hello World", writer);
        Assert.True(writer.WrittenSpan.SequenceEqual("Hello World\r\n"u8));
    }

    [Fact]
    public void Write_EncodeMessage_Threw()
    {
        var protocol = new TerminatorUtf8Protocol(throwInEncode: true);
        var ex = Assert.Throws<Exception>(() =>
        {
            var writer = new ArrayBufferWriter<byte>();
            protocol.Write("Hello World", writer);
        });
        Assert.Equal("failed.", ex.Message);
    }
}
