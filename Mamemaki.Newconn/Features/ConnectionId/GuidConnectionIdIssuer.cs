namespace Mamemaki.Newconn.Features.ConnectionId;

public class GuidConnectionIdIssuer : IConnectionIdIssuer<string>
{
    public string IssueNewId()
    {
        return Guid.NewGuid().ToString();
    }
}
