namespace Mamemaki.Newconn.Features;

/// <summary>
/// A connection feature allowing middleware to stop counting connections towards <see cref="ConnectionLimitMiddleware.ConnectionLimit"/>.
/// </summary>
public interface IDecrementConcurrentConnectionCountFeature
{
    /// <summary>
    /// Idempotent method to stop counting a connection towards <see cref="ConnectionLimitMiddleware.ConnectionLimit"/>.
    /// </summary>
    void ReleaseConnection();
}
