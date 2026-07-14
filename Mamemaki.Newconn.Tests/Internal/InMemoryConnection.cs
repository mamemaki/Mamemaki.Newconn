using Mamemaki.Newconn.Features;
using Mamemaki.Newconn.Internal;

namespace Mamemaki.Newconn.Tests.Internal;

class InMemoryConnection : Connection
{
    public InMemoryConnection(DuplexPipe.DuplexPipePair pair, bool isServer)
    {
        var transportConnection = new InMemoryTransportConnection(pair, isServer);
        var propertiesNew = new ConnectionProperties();
        propertiesNew.Set<IMemoryPoolFeature>(transportConnection);
        SetupConnection(this, 
            transportConnection, propertiesNew,
            disposableObjects: transportConnection);
    }
}
