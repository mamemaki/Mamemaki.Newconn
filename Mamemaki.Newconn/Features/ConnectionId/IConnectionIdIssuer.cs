namespace Mamemaki.Newconn.Features.ConnectionId;

/// <summary>
/// Represents a connection id issuer.
/// </summary>
/// <typeparam name="TId"></typeparam>
public interface IConnectionIdIssuer<TId>
{
    /// <summary>
    /// Issue a new identifier of the connection.
    /// </summary>
    /// <returns></returns>
    TId IssueNewId();
}
