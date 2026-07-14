using Mamemaki.Newconn.Internal;
using System.IO.Pipelines;
using System.Net.Security;

namespace Mamemaki.Newconn.Features.Tls;

internal class SslDuplexPipe : DuplexPipeStreamAdapter<SslStream>
{
    public SslDuplexPipe(IConnectionStateFeature connection, 
        IDuplexPipe transport, StreamPipeReaderOptions readerOptions, StreamPipeWriterOptions writerOptions)
        : this(connection, transport, readerOptions, writerOptions, s => new SslStream(s))
    {
    }

    public SslDuplexPipe(IConnectionStateFeature connection, 
        IDuplexPipe transport, StreamPipeReaderOptions readerOptions, StreamPipeWriterOptions writerOptions, Func<Stream, SslStream> factory) :
        base(connection, transport, readerOptions, writerOptions, factory)
    {
    }
}
