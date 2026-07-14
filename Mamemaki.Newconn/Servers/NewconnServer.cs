using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Features.Heartbeats;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Mamemaki.Newconn.Servers;

public class NewconnServer : IServerContext, IAsyncDisposable
{
    public NewconnServerOptions Options { get; private set; }
    public ILoggerFactory LoggerFactory { get; private set; }
    IHeartbeat? IServerContext.Heartbeat => Heartbeat;
    NewconnMetrics IConnectionMetricsFeature.Metrics => Options.Metrics;

    internal Heartbeat? Heartbeat { get; private set; }
    private readonly ILogger logger;
    private readonly List<INewconnListener> listeners = [];

    private bool started;
    private bool stopped;

    public NewconnServer(NewconnServerOptions options)
    {
        this.logger = options.LoggerFactory.CreateLogger<NewconnServer>();
        this.Options = options;
        this.LoggerFactory = options.LoggerFactory;
        if (options.UseHeartbeat)
        {
            Heartbeat = new Heartbeat(
                Options.TimeProvider,
                logger,
                Options.HeartbeatInterval);
            Heartbeat.Callbacks.AddRange(options.HeartbeatHandlers);
        }
    }

    public IEnumerable<EndPoint?> EndPoints
    {
        get
        {
            foreach (var listener in listeners)
            {
                yield return listener.LocalEndPoint;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (started) return;
        started = true;

        try
        {
            foreach (var binding in Options.Bindings)
            {
                var propertiesNew = new ConnectionProperties();
                propertiesNew.Set<IServerContext>(this);
                await foreach (var listener in binding.BindAsync(
                    this, propertiesNew, cancellationToken).ConfigureAwait(false))
                {
                    listener.Start();
                    listeners.Add(listener);

                    Heartbeat?.Callbacks.Add(listener);
                }
            }
        }
        catch
        {
            await StopAsync(cancellationToken).ConfigureAwait(false);

            throw;
        }

        Heartbeat?.Start();
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (stopped) return;
        stopped = true;

        if (Heartbeat != null)
        {
            Heartbeat.Dispose();
            Heartbeat = null;
        }

        var tasks = new Task[listeners.Count];
        for (var i = 0; i < listeners.Count; i++)
        {
            var listener = listeners[i];
            tasks[i] = listener.StopAsync(Options.ShutdownTimeout, cancellationToken).AsTask();
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
        listeners.Clear();
    }
}
