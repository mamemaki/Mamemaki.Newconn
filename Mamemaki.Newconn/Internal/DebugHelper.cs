using System.Runtime.CompilerServices;

#pragma warning disable IDE0130
namespace System.Diagnostics;
#pragma warning restore IDE0130

public static class DebugHelper
{
    [Conditional("VERBOSE")]
    public static void Log(long connectionNo, string message, [CallerMemberName] string? caller = null)
    {
        var thread = System.Threading.Thread.CurrentThread;
        var s = $"[#{connectionNo} {caller}:{thread.ManagedThreadId}]: {message}";
        Debug.WriteLine(s);
    }
}
