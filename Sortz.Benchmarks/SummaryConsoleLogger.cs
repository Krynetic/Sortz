using BenchmarkDotNet.Loggers;

namespace Sortz.Benchmarks;

// Replace the default console logger by ID/priority; the full log file and
// exported benchmark reports remain available.
internal sealed class SummaryConsoleLogger : ILogger
{
    private static ILogger Console => ConsoleLogger.Default;
    public string Id => Console.Id;
    public int Priority => Console.Priority + 1;

    private static bool Visible(string text)
    {
        ReadOnlySpan<char> line = text.AsSpan().TrimStart();
        return !line.StartsWith("Workload", StringComparison.Ordinal) &&
               !line.StartsWith("Overhead", StringComparison.Ordinal);
    }

    public void Write(LogKind logKind, string text)
    {
        if (Visible(text))
            Console.Write(logKind, text);
    }

    public void WriteLine(LogKind logKind, string text)
    {
        if (Visible(text))
            Console.WriteLine(logKind, text);
    }

    public void WriteLine() => Console.WriteLine();
    public void Flush() => Console.Flush();
}
