using Mamemaki.Newconn.Features.Heartbeats;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Mamemaki.Newconn.Servers;

/// <summary>
/// Represents a connection manager that handles the lifetime of connections.
/// </summary>
public class ConnectionManager : IHeartbeatHandler
{
    protected readonly ConcurrentDictionary<long, Connection> connections = new();
    protected readonly ILogger logger;

    public ConnectionManager(ILogger logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Gets the active connections count.
    /// </summary>
    public int Count { get => connections.Count; }

    public virtual void OnHeartbeat()
    {
        Walk(connection => connection.TickHeartbeat());
    }

    public virtual void AddConnection(Connection connection)
    {
        Debug.Assert(connection.ConnectionNo > 0);

        if (!connections.TryAdd(connection.ConnectionNo, connection))
        {
            throw new InvalidOperationException($"Unable to add connection({connection.ConnectionNo}).");
        }
    }

    public virtual void RemoveConnection(long connNo)
    {
        if (!connections.TryRemove(connNo, out var connection))
        {
            throw new InvalidOperationException($"Unable to remove connection({connNo}).");
        }

        Debug.Assert(connection.IsClosed);
    }

    public void Walk(Action<Connection> callback)
    {
        foreach (var kvp in connections)
        {
            var connection = kvp.Value;
            callback(connection);
        }
    }

    public async ValueTask WalkAsync(Func<Connection, ValueTask> callback)
    {
        foreach (var kvp in connections)
        {
            var connection = kvp.Value;
            await callback(connection);
        }
    }
}
