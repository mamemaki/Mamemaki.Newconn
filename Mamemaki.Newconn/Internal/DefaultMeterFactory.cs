using System.Diagnostics.Metrics;

namespace Mamemaki.Newconn.Internal;

internal sealed class DefaultMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new Meter(options);

    public void Dispose() { }
}
