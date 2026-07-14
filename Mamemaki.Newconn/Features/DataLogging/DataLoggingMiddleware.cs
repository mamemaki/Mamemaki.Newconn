using Mamemaki.Newconn.Internal;
using Microsoft.Extensions.Logging;
using System.IO.Pipelines;

namespace Mamemaki.Newconn.Features.DataLogging;

internal class DataLoggingMiddleware(ILogger logger, LoggingFormatter? loggingFormatter = null) : IAsyncDisposable
{
    private LoggingDuplexPipe? loggingDuplexPipe;

    public Task<bool> OnConnectionAsync(Connection connection, CancellationToken _)
    {
        loggingDuplexPipe = new LoggingDuplexPipe(connection, connection.Transport, logger, loggingFormatter);

        connection.Transport = loggingDuplexPipe;

        return Task.FromResult(true);
    }

    public async ValueTask DisposeAsync()
    {
        if (loggingDuplexPipe != null)
        {
            await loggingDuplexPipe.DisposeAsync();
            loggingDuplexPipe = null;
        }
        GC.SuppressFinalize(this);
    }

    private class LoggingDuplexPipe(IConnectionStateFeature connection,
        IDuplexPipe transport, ILogger logger, LoggingFormatter? loggingFormatter = null) :
        DuplexPipeStreamAdapter<LoggingStream>(connection, transport, stream => new LoggingStream(stream, logger, loggingFormatter))
    {
    }
}
