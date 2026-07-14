using System.Buffers;

namespace Mamemaki.Newconn.Internal;

public interface IMemoryPoolFactory<T>
{
    /// <summary>
    /// Creates a new instance of a memory pool.
    /// </summary>
    /// <param name="options">Options for configuring the memory pool.</param>
    /// <returns>A new memory pool instance.</returns>
    MemoryPool<T> Create();
}
