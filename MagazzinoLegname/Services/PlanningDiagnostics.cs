using System.Diagnostics;
using System.IO;
namespace MagazzinoLegname.Services;

// Opt-in Debug diagnostics; no handlers mark exceptions as handled.
public static class PlanningDiagnostics
{
    private static readonly object Sync = new();
    private static long _sequence;
    [Conditional("DEBUG")]
    public static void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:O} #{Interlocked.Increment(ref _sequence)} PID={Environment.ProcessId} T={Environment.CurrentManagedThreadId} {message}";
        Debug.WriteLine(line);
        var path = Environment.GetEnvironmentVariable("MAGAZZINO_PLANNING_TRACE");
        if (string.IsNullOrWhiteSpace(path)) return;
        try { lock (Sync) File.AppendAllText(path, line + Environment.NewLine); }
        catch (IOException error) { Debug.WriteLine($"Planning trace I/O error: {error}"); }
        catch (UnauthorizedAccessException error) { Debug.WriteLine($"Planning trace access error: {error}"); }
    }
    [Conditional("DEBUG")]
    public static void Attach(System.Windows.Application app)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MAGAZZINO_PLANNING_TRACE"))) return;
        app.DispatcherUnhandledException += (_, e) => Log($"DispatcherUnhandledException Handled={e.Handled}\n{e.Exception}\nInnerException: {e.Exception.InnerException?.ToString() ?? "<null>"}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log($"AppDomain.UnhandledException IsTerminating={e.IsTerminating}\n{e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => Log($"TaskScheduler.UnobservedTaskException Observed={e.Observed}\n{e.Exception}");
        Log("Debug exception observers attached");
    }
}
