using System.Buffers;

namespace Mamemaki.Newconn.Internal;

internal sealed class DefaultSimpleMemoryPoolFactory : IMemoryPoolFactory<byte>
{
    public static DefaultSimpleMemoryPoolFactory Instance { get; } = new DefaultSimpleMemoryPoolFactory();

    public MemoryPool<byte> Create()
    {
        return MemoryPool<byte>.Shared;
    }
}
