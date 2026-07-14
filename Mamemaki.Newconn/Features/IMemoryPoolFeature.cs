using System.Buffers;

namespace Mamemaki.Newconn.Features;

/// <summary>
/// The <see cref="MemoryPool{byte}"/> used by the connection.
/// </summary>
public interface IMemoryPoolFeature
{
    /// <summary>
    /// Gets the <see cref="MemoryPool{byte}"/> used by the connection.
    /// </summary>
    MemoryPool<byte> MemoryPool { get; }
}
