using Mamemaki.Newconn.Servers;

namespace Mamemaki.Newconn.Features;

/// <summary>
/// Represents the metrics of the connection.
/// </summary>
public interface IConnectionMetricsFeature
{
    NewconnMetrics Metrics { get; }
}
