using Mamemaki.Newconn.Protocol;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Mamemaki.Newconn.Tests.Protocol;

public class FixedHeaderProtocolTests
{
    public class FixedHeaderUtf8Protocol : FixedHeaderProtocol<string>
    {
        public static readonly Encoding Encoding = new UTF8Encoding(false);
        private readonly bool throwGetBodyLength;
        private readonly bool throwInDecode;
        private readonly bool throwInEncode;

        public FixedHeaderUtf8Protocol(int headerSize = 4, 
            bool throwGetBodyLength = false, bool throwInDecode = false, bool throwInEncode = false)
            : base(headerSize)
        {
            this.throwGetBodyLength = throwGetBodyLength;
            this.throwInDecode = throwInDecode;
            this.throwInEncode = throwInEncode;
        }

        protected override int ReadBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
        {
            if (throwGetBodyLength)
                throw new Exception("failed.");
            return BinaryPrimitives.ReadInt32BigEndian(buffer.FirstSpan);
        }

        protected override string DecodeMessage(ref ReadOnlySequence<byte> payload)
        {
            if (throwInDecode)
                throw new Exception("failed.");
            var reader = new SequenceReader<byte>(payload);
            reader.Advance(headerSize);
            if (reader.Remaining > 0)
            {
                var textPayload = reader.Sequence.Slice(headerSize, reader.Remaining);
                var text = Encoding.GetString(textPayload);
                return text;
            }
            return "";
        }

        public override void Write(string message, IBufferWriter<byte> writer)
        {
            if (throwInEncode)
                throw new Exception("failed.");
            var bytes = Encoding.GetBytes(message);
            BinaryPrimitives.WriteInt32BigEndian(writer.GetSpan(headerSize), bytes.Length);
            writer.Advance(headerSize);
            writer.Write(bytes);
        }

        public byte[] CreateMessageBytes(string text)
        {
            var writer = new ArrayBufferWriter<byte>();
            Write(text, writer);
            return writer.WrittenMemory.ToArray();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidFixedSize(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedHeaderUtf8Protocol(size));
    }

    [Fact]
    public void TryRead()
    {
        var protocol = new FixedHeaderUtf8Protocol();
        var buffer = protocol.CreateMessageBytes("Hello World");
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer));
        Assert.True(protocol.TryRead(ref reader, out var message));
        Assert.Equal("Hello World", message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(13)]
    [InlineData(14)]
    public void TryRead_Insufficient(int trimPos)
    {
        var protocol = new FixedHeaderUtf8Protocol();
        var buffer = protocol.CreateMessageBytes("Hello World");
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer).Slice(0, trimPos));
        Assert.False(protocol.TryRead(ref reader, out var _));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(21)]
    [InlineData(28)]
    [InlineData(29)]
    public void TryRead_InsufficientAt2ndSegment(int trimPos)
    {
        var protocol = new FixedHeaderUtf8Protocol();
        byte[] buffer = [..protocol.CreateMessageBytes("Hello World"), ..protocol.CreateMessageBytes("Hello World")];
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer).Slice(0, trimPos));
        Assert.True(protocol.TryRead(ref reader, out var message));
        Assert.Equal("Hello World", message);
        Assert.False(protocol.TryRead(ref reader, out var _));
    }

    [Fact]
    public void TryRead_ReadBodyLengthFromHeader_ReturnNegative()
    {
        var protocol = new FixedHeaderUtf8Protocol();
        var buffer = protocol.CreateMessageBytes("Hello World");
        BinaryPrimitives.WriteInt32BigEndian(buffer, -1);
        var ex = Assert.Throws<ProtocolException>(() =>
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
        Assert.Equal("Failed to get body length from the header.", ex.Message);
    }

    [Fact]
    public void TryRead_ReadBodyLengthFromHeader_Threw()
    {
        var protocol = new FixedHeaderUtf8Protocol(throwGetBodyLength: true);
        var buffer = protocol.CreateMessageBytes("Hello World");
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
    public void TryRead_ReadBodyLengthFromHeader_NoBody()
    {
        var protocol = new FixedHeaderUtf8Protocol();
        var buffer = protocol.CreateMessageBytes("");
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(buffer));
        protocol.TryRead(ref reader, out var message);
        Assert.Equal("", message);
    }

    [Fact]
    public void TryRead_DecodeMessageThrew()
    {
        var protocol = new FixedHeaderUtf8Protocol(throwInDecode: true);
        var buffer = protocol.CreateMessageBytes("Hello World");
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
        var protocol = new FixedHeaderUtf8Protocol();
        var writer = new ArrayBufferWriter<byte>();
        protocol.Write("Hello World", writer);
        var buffer = protocol.CreateMessageBytes("Hello World");
        Assert.True(writer.WrittenSpan.SequenceEqual(buffer));
    }

    [Fact]
    public void Write_EncodeMessageThrew()
    {
        var protocol = new FixedHeaderUtf8Protocol(throwInEncode: true);
        var ex = Assert.Throws<Exception>(() =>
        {
            var writer = new ArrayBufferWriter<byte>();
            protocol.Write("Hello World", writer);
        });
        Assert.Equal("failed.", ex.Message);
    }
}
