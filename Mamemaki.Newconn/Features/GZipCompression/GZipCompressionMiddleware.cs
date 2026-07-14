using Mamemaki.Newconn.Internal;
using System.IO.Compression;
using System.IO.Pipelines;

namespace Mamemaki.Newconn.Features.GZipCompression;

internal class GZipCompressionMiddleware(CompressionLevel compressionLevel = default) : IAsyncDisposable
{
    private CompressionDuplexPipe? compressionDuplexPipe;
    private Stream? stream;

    public Task<bool> OnConnectionAsync(Connection connection, CancellationToken _)
    {
        compressionDuplexPipe = new CompressionDuplexPipe(connection, connection.Transport, compressionLevel);
        stream = compressionDuplexPipe.Stream;

        connection.Transport = compressionDuplexPipe;

        return Task.FromResult(true);
    }

    public async ValueTask DisposeAsync()
    {
        if (compressionDuplexPipe != null)
        {
            await compressionDuplexPipe.DisposeAsync();
            compressionDuplexPipe = null;
        }
        if (stream != null)
        {
            await stream.DisposeAsync();
            stream = null;
        }
        GC.SuppressFinalize(this);
    }

    private class CompressionDuplexPipe(IConnectionStateFeature connection, 
        IDuplexPipe transport, CompressionLevel compressionLevel = default) :
        DuplexPipeStreamAdapter<GZipCompositeStream>(connection, transport, stream 
            => new GZipCompositeStream(stream, compressionLevel))
    {
    }
}
