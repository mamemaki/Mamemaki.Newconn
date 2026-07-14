using System.IO.Pipelines;

namespace Mamemaki.Newconn.Internal;

internal class ThrowDuplexPipe : IDuplexPipe
{
    public static ThrowDuplexPipe Instance { get; } = new ThrowDuplexPipe();

    private const string ErrorMessage = "Transport is unavailable until it connects.";

    public PipeReader Input => throw new InvalidOperationException(ErrorMessage);
    public PipeWriter Output => throw new InvalidOperationException(ErrorMessage);
}
