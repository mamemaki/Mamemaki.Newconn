using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Benchmarks.Configs;

namespace Benchmarks;

internal class Program
{
    static void Main(string[] args)
    {
#if DEBUG
        var config = new DebugInProcessConfig();
        var _ = BenchmarkRunner.Run<EchoServerBenchmark>(config);
#else
        IConfig config = new DefaultBenchmarkConfig();
        var _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
#endif
    }
}
