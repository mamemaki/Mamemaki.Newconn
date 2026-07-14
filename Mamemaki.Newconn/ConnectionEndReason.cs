namespace Mamemaki.Newconn;

/// <summary>
/// Represents connection end reason.
/// </summary>
public class ConnectionEndReason
{
    /// <summary>
    /// Gets or sets the connection end reason code
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets the connection end reason display name
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConnectionEndReason"/>.
    /// </summary>
    /// <param name="code">The connection end reason code.</param>
    /// <param name="displayName">The connection end reason display name.</param>
    public ConnectionEndReason(int code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public override string? ToString()
    {
        return DisplayName;
    }


    /// <summary>
    /// The connection is closed for an unknown reason.
    /// </summary>
    public static readonly ConnectionEndReason Unknown = new ConnectionEndReason(0, nameof(Unknown));

    /// <summary>
    /// The connection is closed due to server shutdown.
    /// </summary>
    public static readonly ConnectionEndReason ServerShutdown = new ConnectionEndReason(1, nameof(ServerShutdown));

    /// <summary>
    /// The connection is closed by the remote endpoint.
    /// </summary>
    public static readonly ConnectionEndReason RemoteClosing = new ConnectionEndReason(2, nameof(RemoteClosing));

    /// <summary>
    /// The connection is closed by the local endpoint.
    /// </summary>
    public static readonly ConnectionEndReason LocalClosing = new ConnectionEndReason(3, nameof(LocalClosing));

    /// <summary>
    /// The connection is closed due to an application error.
    /// </summary>
    public static readonly ConnectionEndReason ApplicationError = new ConnectionEndReason(4, nameof(ApplicationError));

    /// <summary>
    /// The connection is closed due to a protocol error.
    /// </summary>
    public static readonly ConnectionEndReason ProtocolError = new ConnectionEndReason(5, nameof(ProtocolError));

    /// <summary>
    /// The connection is closed due to a transport(socket) error.
    /// </summary>
    public static readonly ConnectionEndReason TransportError = new ConnectionEndReason(6, nameof(TransportError));

    /// <summary>
    /// The connection is closed because it was rejected.
    /// </summary>
    public static readonly ConnectionEndReason Rejected = new ConnectionEndReason(10, nameof(Rejected));

    /// <summary>
    /// The connection is closed by the server due to a idle timeout.
    /// </summary>
    public static readonly ConnectionEndReason IdleTimeOut = new ConnectionEndReason(11, nameof(IdleTimeOut));

    /// <summary>
    /// The connection is closed due to TLS handshake error.
    /// </summary>
    public static readonly ConnectionEndReason TlsHandshakeError = new ConnectionEndReason(20, nameof(TlsHandshakeError));
}
