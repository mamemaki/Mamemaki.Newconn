using Mamemaki.Newconn.Protocol;
using System.Buffers;
using System.Text;

namespace Mamemaki.Newconn.Tests.Protocol;

public class FixedSizeProtocolTests
{
    public class FixedUtf8Protocol : FixedSizeProtocol<string>
    {
        public static readonly Encoding Encoding = new UTF8Encoding(false);
        private readonly bool throwInDecode;

        public FixedUtf8Protocol(int messageSize, bool throwInDecode = false)
            : base(messageSize)
        {
            this.throwInDecode = throwInDecode;
        }

        protected override string DecodeMessage(ref ReadOnlySequence<byte> payload)
        {
            if (throwInDecode)
                throw new Exception("failed.");
            var text = Encoding.GetString(payload);
            return text;
        }

        public override void Write(string message, IBufferWriter<byte> writer)
        {
            var bytes = Encoding.GetBytes(message);
            if (bytes.Length != messageSize)
                throw new ArgumentException($"message size must be {messageSize} instead of {bytes.Length}", nameof(message));
            writer.Write(bytes);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidFixedSize(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedUtf8Protocol(size));
    }

    [Fact]
    public void TryRead()
    {
        var buffer = "Hello World"u8.ToArray();
        var protocol = new FixedUtf8Protocol(buffer.Length);
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer));
        Assert.True(protocol.TryRead(ref reader, out var message));
        Assert.Equal("Hello World", message);
    }

    [Fact]
    public void TryRead_Insufficient()
    {
        var buffer = "Hello World"u8.ToArray();
        var protocol = new FixedUtf8Protocol(buffer.Length);
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer).Slice(0, 5));
        Assert.False(protocol.TryRead(ref reader, out var _));
    }

    [Fact]
    public void TryRead_InsufficientAt2ndSegment()
    {
        var buffer = ("Hello World"u8 + "Hello World"u8).ToArray();
        var protocol = new FixedUtf8Protocol(buffer.Length/2);
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer).Slice(0, 20));
        Assert.True(protocol.TryRead(ref reader, out var message));
        Assert.Equal("Hello World", message);
        Assert.False(protocol.TryRead(ref reader, out var _));
    }

    [Fact]
    public void TryRead_DecodeMessage_Threw()
    {
        var buffer = "Hello World"u8.ToArray();
        var protocol = new FixedUtf8Protocol(buffer.Length, throwInDecode: true);
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
}
