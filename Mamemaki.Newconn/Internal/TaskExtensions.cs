#pragma warning disable IDE0130
namespace System.Threading.Tasks;
#pragma warning restore IDE0130

internal static class TaskExtensions
{
    public static async Task<bool> TimeoutAfter(this Task task, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await task.WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
