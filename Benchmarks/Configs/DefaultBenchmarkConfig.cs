using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;
using System.Diagnostics.Tracing;

namespace Benchmarks.Configs;

internal class DefaultBenchmarkConfig : ManualConfig
{
    public DefaultBenchmarkConfig()
    {
        var isProduction = false;

        WithOptions(ConfigOptions.DisableLogFile);

        var job = Job.Default
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithGcForce(true)
            .AsDefault();
        if (!isProduction)
        {
            //job = job.WithLaunchCount(1)
            //    .WithWarmupCount(1)
            //    .WithIterationCount(3)
            //    //.WithUnrollFactor(16)
            //    //.WithInvocationCount(8)
            //    .WithEvaluateOverhead(false);
        }
        AddJob(job);

        if (isProduction)
        {
            AddExporter(CsvMeasurementsExporter.Default);
            AddExporter(RPlotExporter.Default);
        }

        AddColumnProvider(DefaultColumnProviders.Instance);
        AddLogger(ConsoleLogger.Default);

        var providers = new[]
        {
            new EventPipeProvider(ClrTraceEventParser.ProviderName, EventLevel.Verbose,
                (long) ClrTraceEventParser.Keywords.Default |
                (long) ClrTraceEventParser.Keywords.GC |
                (long) ClrTraceEventParser.Keywords.GCHandle |
                (long) ClrTraceEventParser.Keywords.Threading |
                (long) ClrTraceEventParser.Keywords.WaitHandle |
                (long) ClrTraceEventParser.Keywords.Exception
            ),
        };

        var diagnosers = new List<IDiagnoser>()
        {
            new MemoryDiagnoser(new MemoryDiagnoserConfig(displayGenColumns: false)),
        };
        if (isProduction)
        {
            diagnosers.Add(new EventPipeProfiler(providers: providers, performExtraBenchmarksRun: false));
        }
        AddDiagnoser(diagnosers.ToArray());
    }
}
