namespace Mamemaki.Newconn.Features.Tls;

public class TlsException : Exception
{
    public TlsException()
    {
    }

    public TlsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
