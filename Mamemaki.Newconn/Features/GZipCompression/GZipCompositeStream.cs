using System.IO.Compression;

namespace Mamemaki.Newconn.Features.GZipCompression;

internal sealed class GZipCompositeStream : Stream
{
    /// <summary>
    /// Gets the base stream
    /// </summary>
    public Stream BaseStream { get; }

    private readonly GZipStream readStream;
    private readonly GZipStream writeStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="GZipCompositeStream"/> class with the base stream.
    /// </summary>
    /// <param name="stream">The base stream</param>
    /// <param name="compressionLevel">The CompressionLevel. Defaults is Optimal.</param>
    public GZipCompositeStream(Stream stream, CompressionLevel compressionLevel = default)
    {
        this.readStream = new GZipStream(stream, CompressionMode.Decompress);
        this.writeStream = new GZipStream(stream, compressionLevel);
        this.BaseStream = stream;
    }

    public override async ValueTask DisposeAsync()
    {
        await readStream.DisposeAsync();
        await writeStream.DisposeAsync();
    }

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush()
    {
        writeStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return readStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        writeStream.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        writeStream.Write(buffer);
    }

    public override int Read(Span<byte> buffer)
    {
        return readStream.Read(buffer);
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return readStream.BeginRead(buffer, offset, count, callback, state);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return writeStream.BeginWrite(buffer, offset, count, callback, state);
    }

    public override void Close()
    {
        BaseStream.Close();
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        readStream.CopyTo(destination, bufferSize);
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        return readStream.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return readStream.EndRead(asyncResult);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        writeStream.EndWrite(asyncResult);
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return writeStream.FlushAsync(cancellationToken);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return readStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return readStream.ReadAsync(buffer, cancellationToken);
    }

    public override int ReadByte()
    {
        return readStream.ReadByte();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return writeStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return writeStream.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        writeStream.WriteByte(value);
    }

    public override bool CanTimeout => BaseStream.CanTimeout;

    public override int ReadTimeout { get => BaseStream.ReadTimeout; set => BaseStream.ReadTimeout = value; }

    public override int WriteTimeout { get => BaseStream.WriteTimeout; set => BaseStream.WriteTimeout = value; }
}
