using Mamemaki.Newconn.Internal;
using Microsoft.Extensions.Logging;

namespace Mamemaki.Newconn.Features.ConnectionLimits;

internal sealed class ConnectionLimitMiddleware : IDisposable
{
    private readonly ResourceCounter concurrentConnectionCounter;
    private readonly ILogger logger;

    private ConnectionReleasor? releasor;

    public ConnectionLimitMiddleware(ResourceCounter concurrentConnectionCounter, ILogger logger)
    {
        this.concurrentConnectionCounter = concurrentConnectionCounter;
        this.logger = logger;
    }

    public void Dispose()
    {
        releasor?.ReleaseConnection();
        releasor = null;
    }

    public Task<bool> OnConnectionAsync(Connection connection, CancellationToken _)
    {
        if (!concurrentConnectionCounter.TryLockOne())
        {
            NewconnLog.ConnectionRejected(logger, connection.ConnectionNo);
            return Task.FromResult(false);
        }

        releasor = new ConnectionReleasor(concurrentConnectionCounter);
        connection.Properties.Set<IDecrementConcurrentConnectionCountFeature>(releasor);
        return Task.FromResult(true);
    }

    private sealed class ConnectionReleasor : IDecrementConcurrentConnectionCountFeature
    {
        private readonly ResourceCounter concurrentConnectionCounter;
        private bool connectionReleased;

        public ConnectionReleasor(ResourceCounter normalConnectionCounter)
        {
            concurrentConnectionCounter = normalConnectionCounter;
        }

        public void ReleaseConnection()
        {
            if (!connectionReleased)
            {
                connectionReleased = true;
                concurrentConnectionCounter.ReleaseOne();
            }
        }
    }
}
