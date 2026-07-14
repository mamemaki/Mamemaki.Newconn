using Mamemaki.Newconn.Features.Heartbeats;
using System.Net;

namespace Mamemaki.Newconn.Servers;

public interface INewconnListener : IHeartbeatHandler
{
    EndPoint? LocalEndPoint { get; }

    void Start();

    ValueTask StopAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
