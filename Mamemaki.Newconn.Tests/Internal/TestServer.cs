using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Features.Heartbeats;
using Mamemaki.Newconn.Internal;
using Mamemaki.Newconn.Servers;
using System.IO.Pipelines;

namespace Mamemaki.Newconn.Tests.Internal;

class TestServer : NewconnServer, IHeartbeatHandler
{
    public new TestServerOptions Options { get; private set; }

    public List<TestSession> Sessions { get; private set; } = [];

    public TestServer(TestServerOptions options)
        : base(options)
    {
        Options = options;
        Heartbeat?.Callbacks.Add(this);
        Heartbeat?.Start();
    }

    public void OnHeartbeat()
    {
        foreach (var session in Sessions)
        {
            session.ServerConnection.TickHeartbeat();
            session.ClientConnection.TickHeartbeat();
        }
    }

    public async Task<TestSession> CreateSessionAsnc(
        int minimumSegmentSize = -1,
        CancellationToken cancellationToken = default)
    {
        var options = new PipeOptions(
            minimumSegmentSize: minimumSegmentSize,
            useSynchronizationContext: false);
        var pair = DuplexPipe.CreateConnectionPair(options, options);

        var connServer = new InMemoryConnection(pair, true);
        connServer.Properties.Set<IServerContext>(this);
        connServer.Properties.Set<IConnectionHeartbeatFeature>(connServer);

        var connClient = new InMemoryConnection(pair, false);
        connClient.Properties.Set<IServerContext>(this);
        connClient.Properties.Set<IConnectionHeartbeatFeature>(connClient);

        var tasks = new List<Task<bool>>();
        tasks.Add(connServer.FireOnConnectedAsync(Options.MiddlewaresOnServer.Middlewares, cancellationToken));
        tasks.Add(connClient.FireOnConnectedAsync(Options.MiddlewaresOnClient.Middlewares, cancellationToken));
        if ((await Task.WhenAll(tasks)).Any(b => b == false))
        {
            var ex = new ConnectionAbortedException("Rejected");
            connServer.Abort(ex);
            connClient.Abort(ex);
            throw ex;
        }

        var session = new TestSession(connServer, connClient);
        Sessions.Add(session);
        return session;
    }
}
