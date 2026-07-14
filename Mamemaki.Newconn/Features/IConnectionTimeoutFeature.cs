namespace Mamemaki.Newconn.Features;

/// <summary>
/// Feature for handling connection timeouts.
/// </summary>
public interface IConnectionTimeoutFeature
{
    /// <summary>
    /// Close the connection after the specified positive finite <see cref="TimeSpan"/>
    /// unless the timeout is canceled or reset.
    /// </summary>
    /// <param name="timeout">Length of timeout.</param>
    /// <param name="onTimeout">Timeout handler.</param>
    /// <returns>true if set timeout successfully, otherwise false.</returns>
    bool SetTimeout(TimeSpan timeout, Action<Connection>? onTimeout = null);

    /// <summary>
    /// Prevent the connection from closing after a timeout specified by <see cref="SetTimeout(TimeSpan)"/>.
    /// </summary>
    void CancelTimeout();
}
