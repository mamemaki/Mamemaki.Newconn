using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mamemaki.Newconn.Tests.Internal;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddInMemory(this ILoggingBuilder builder)
    {
        var logger = new InMemoryLogger();
        builder.Services.AddSingleton(logger);
        return builder.AddProvider(new InMemLoggerProvider(logger));
    }
}

public sealed class InMemLoggerProvider : ILoggerProvider
{
    private readonly InMemoryLogger logger;

    public InMemLoggerProvider(InMemoryLogger logger) => this.logger = logger;

    public ILogger CreateLogger(string categoryName) => logger;

    public void Dispose() { }
}

public sealed class InMemoryLogger : ILogger
{
    private readonly List<(LogLevel, Exception?, string)> logLines = new List<(LogLevel, Exception?, string)>();

    public IEnumerable<(LogLevel Level, Exception? Exception, string Message)> RecordedLogs => logLines.AsReadOnly();
    public IEnumerable<(LogLevel Level, Exception? Exception, string Message)> RecordedTraceLogs => logLines.Where(l => l.Item1 == LogLevel.Trace);
    public IEnumerable<(LogLevel Level, Exception? Exception, string Message)> RecordedDebugLogs => logLines.Where(l => l.Item1 == LogLevel.Debug);
    public IEnumerable<(LogLevel Level, Exception? Exception, string Message)> RecordedInformationLogs => logLines.Where(l => l.Item1 == LogLevel.Information);
    public IEnumerable<(LogLevel Level, Exception? Exception, string Message)> RecordedWarningLogs => logLines.Where(l => l.Item1 == LogLevel.Warning);
    public IEnumerable<(LogLevel Level, Exception? Exception, string Message)> RecordedErrorLogs => logLines.Where(l => l.Item1 == LogLevel.Error);
    public IEnumerable<(LogLevel Level, Exception? Exception, string Message)> RecordedCriticalLogs => logLines.Where(l => l.Item1 == LogLevel.Critical);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        logLines.Add((logLevel, exception, formatter(state, exception)));
    }
}
