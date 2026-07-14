namespace Mamemaki.Newconn.Hosting;

internal class EmptyServiceProvider : IServiceProvider
{
    public static IServiceProvider Instance { get; } = new EmptyServiceProvider();

    public object? GetService(Type serviceType) => null;
}
