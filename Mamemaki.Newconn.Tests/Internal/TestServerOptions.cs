using Mamemaki.Newconn.Hosting;
using Mamemaki.Newconn.Servers;

namespace Mamemaki.Newconn.Tests.Internal;

internal class TestServerOptions : NewconnServerOptions
{
    public TestServerMiddlewareBuilder MiddlewaresOnServer { get; set; }
    public TestClientMiddlewareBuilder MiddlewaresOnClient { get; set; }

    public TestServerOptions(IServiceProvider? serviceProvider = null)
    {
        ServiceProvider = serviceProvider ?? EmptyServiceProvider.Instance;
        MiddlewaresOnServer = new(serviceProvider);
        MiddlewaresOnClient = new(serviceProvider);
    }
}

internal class TestMiddlewareBuilder(IServiceProvider? serviceProvider = null) : IMiddlewareBuilder
{
    public IServiceProvider ServiceProvider { get; set; } = serviceProvider ?? EmptyServiceProvider.Instance;

    public IList<ConnectionMiddlewareDelegate> Middlewares { get; set; } = [];

    public IMiddlewareBuilder Use(ConnectionMiddlewareDelegate middleware)
    {
        Middlewares.Add(middleware);
        return this;
    }
}

internal class TestServerMiddlewareBuilder(IServiceProvider? serviceProvider = null) 
    : TestMiddlewareBuilder(serviceProvider), IServerMiddlewareBuilder
{
}

internal class TestClientMiddlewareBuilder(IServiceProvider? serviceProvider = null)
    : TestMiddlewareBuilder(serviceProvider), IClientMiddlewareBuilder
{
}
