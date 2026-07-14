namespace Mamemaki.Newconn.Features;

/// <summary>
/// Represents the connection state.
/// </summary>
public interface IConnectionStateFeature : IConnectionIdFeature
{
    /// <summary>
    /// Gets whether the connection is closed.
    /// </summary>
    bool IsClosed { get; }
}
