using Mamemaki.Newconn.Internal;
using Microsoft.Extensions.Logging;

namespace Mamemaki.Newconn.Features.Heartbeats;

internal sealed class Heartbeat : IDisposable, IHeartbeat
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1);

    public readonly List<IHeartbeatHandler> Callbacks = [];
    private readonly TimeProvider timeProvider;
    private readonly ILogger logger;
    private readonly TimeSpan interval;
    private readonly Thread timerThread;
    private readonly ManualResetEventSlim stopEvent;

    public TimeSpan Interval { get => interval; }

    public Heartbeat(TimeProvider timeProvider, ILogger logger, TimeSpan interval)
    {
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.interval = interval;
        // Wait time is long, so don't try to spin to exit early. Spinning would waste CPU time.
        this.stopEvent = new ManualResetEventSlim(false, spinCount: 0);
        timerThread = new Thread(state => ((Heartbeat)state!).TimerLoop())
        {
            Name = "Newconn Timer",
            IsBackground = true
        };
    }

    public void Start()
    {
        OnHeartbeat();
        timerThread.Start(this);
    }

    internal void OnHeartbeat()
    {
        var now = timeProvider.GetTimestamp();

        try
        {
            foreach (var callback in Callbacks)
            {
                callback.OnHeartbeat();
            }

            var duration = timeProvider.GetElapsedTime(now);

            if (duration > interval)
            {
                NewconnLog.HeartbeatSlow(logger, timeProvider.GetUtcNow(), duration, interval);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(0, ex, $"{nameof(Heartbeat)}.{nameof(OnHeartbeat)}");
        }
    }

    private void TimerLoop()
    {
        // Starting the heartbeat immediately triggers OnHeartbeat.
        // Initial delay to avoid running heartbeat again from timer thread.
        while (!stopEvent.Wait(interval))
        {
            OnHeartbeat();
        }
    }

    public void Dispose()
    {
        // Stop heart beat and immediately exit wait interval.
        stopEvent.Set();

        // Wait for heartbeat thread to finish.
        // Should either be immediate or a short delay while heartbeat callbacks complete.
        if (timerThread.IsAlive)
        {
            timerThread.Join();
        }

        stopEvent.Dispose();
    }
}
