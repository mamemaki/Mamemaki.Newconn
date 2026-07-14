using BenchmarkDotNet.Attributes;
using Benchmarks.Frameworks;
using System.Net;
using System.Threading.Tasks.Dataflow;

namespace Benchmarks;

public class EchoServerBenchmark
{
    [Params(FrameworkType.Kestrel, FrameworkType.Newconn, FrameworkType.SuperSocket)]
    public FrameworkType Framework { get; set; }

    private IFramework? frameworkImpl;
    private readonly BenchmarkConfiguration config = new();
    private IEchoServer? server;
    private readonly List<IEchoClient> clients = new List<IEchoClient>();

    [GlobalSetup]
    public async ValueTask GlobalSetup()
    {
        var cancellationToken = CancellationToken.None;
        frameworkImpl = IFramework.Create(Framework, config);
        var frameworkImplBase = IFramework.Create(FrameworkType.Newconn, config);

        server = frameworkImpl.CreateEchoServer();

        Console.WriteLine("Start server..");
        await server.StartAsync(cancellationToken);

        Console.WriteLine("Start clients..");
        var clientEp = IPEndPoint.Parse($"{config.Address}:{config.Port}");
        var clientConnectionTasks = new List<Task<IEchoClient>>();
        for (var i = 0; i < config.Clients; i++)
        {
            clientConnectionTasks.Add(frameworkImplBase.ConnectEchoClientAsync(i + 1, clientEp, cancellationToken));
        }
        await Task.WhenAll(clientConnectionTasks).WaitAsync(cancellationToken);
        foreach (var clientConnectionTask in clientConnectionTasks)
        {
            clients.Add(clientConnectionTask.Result);
        }
        Console.WriteLine("Setup done!");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        ArgumentNullException.ThrowIfNull(server);
        var cancellationToken = CancellationToken.None;

        foreach (var client in clients)
        {
            client.Dispose();
        }
        clients.Clear();

        server.StopAsync(cancellationToken).Wait();
        server = null;
    }

    [Benchmark]
    public void Run()
    {
        Task.Run(RunAsync).Wait();
    }

    private async Task RunAsync()
    {
        ArgumentNullException.ThrowIfNull(server);
        var cancellationToken = CancellationToken.None;

        var block = new ActionBlock<IEchoClient>(async client =>
        {
            try
            {
                for (var i = 0; i < config.MessageCount; i++)
                {
                    await client.SendMessageAsync(config.Message, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("OperationCanceledException");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{client.Id}] {ex}");
                throw;
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 10,
            CancellationToken = cancellationToken,
        });

        foreach (var client in clients)
        {
            block.Post(client);
        }
        block.Complete();
        await block.Completion.ConfigureAwait(false);
    }
}
