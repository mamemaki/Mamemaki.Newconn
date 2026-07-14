namespace Mamemaki.Newconn.Features.ConnectionId;

public class CounterConnectionIdIssuer : IConnectionIdIssuer<long>
{
    public static readonly CounterConnectionIdIssuer Instance = new();

    private long counter = 0;

    public long IssueNewId()
    {
        return Interlocked.Increment(ref counter);
    }
}
