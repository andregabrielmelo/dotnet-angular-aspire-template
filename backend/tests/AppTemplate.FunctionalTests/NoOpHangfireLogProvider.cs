using Hangfire.Logging;

namespace AppTemplate.FunctionalTests;

/// <summary>Discards Hangfire's internal logging in tests (see AppTemplateWebApplicationFactory).</summary>
public sealed class NoOpHangfireLogProvider : ILogProvider
{
    public static readonly NoOpHangfireLogProvider Instance = new();

    public ILog GetLogger(string name) => NoOpLog.Instance;

    private sealed class NoOpLog : ILog
    {
        public static readonly NoOpLog Instance = new();

        public bool Log(
            LogLevel logLevel,
            Func<string>? messageFunc,
            Exception? exception = null
        ) => false;
    }
}
