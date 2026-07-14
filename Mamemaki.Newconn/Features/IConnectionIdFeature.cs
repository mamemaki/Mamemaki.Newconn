namespace Mamemaki.Newconn.Features;

/// <summary>
/// Represents the unique number of the connection.
/// </summary>
public interface IConnectionIdFeature
{
    /// <summary>
    /// Gets the connection unique number.
    /// </summary>
    long ConnectionNo { get; }
}
