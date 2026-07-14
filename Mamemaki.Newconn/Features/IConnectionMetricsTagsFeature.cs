namespace Mamemaki.Newconn.Features;

/// <summary>
/// Represents the metrics tags of the connection.
/// </summary>
public interface IConnectionMetricsTagsFeature
{
    /// <summary>
    /// Gets the tag collection.
    /// </summary>
    ICollection<KeyValuePair<string, object?>> Tags { get; }
}
