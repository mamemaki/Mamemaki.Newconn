using Mamemaki.Newconn.Tests.Internal;
using System.Buffers;
using System.IO.Compression;
using System.Text;

namespace Mamemaki.Newconn.Tests.Features;

public class GZipCompressionTests
{
    TestServer CreateServer(TestServerOptions options)
    {
        return new TestServer(options);
    }

    [Fact]
    public async Task Hello()
    {
        var options = new TestServerOptions();
        options.MiddlewaresOnServer.UseGZipCompression();
        options.MiddlewaresOnClient.UseGZipCompression();
        await using (var server = CreateServer(options))
        {
            await using (var session = await server.CreateSessionAsnc())
            {
                await session.ServerSendAsync("Hello"u8.ToArray());

                var buffer = await session.ClientReceiveAsync();
                var text = Encoding.UTF8.GetString(buffer);
                Assert.Equal("Hello", text);
            }
        }
    }

    [Fact]
    public async Task Hello_ManuallyDecompress()
    {
        var options = new TestServerOptions();
        options.MiddlewaresOnServer.UseGZipCompression();
        await using (var server = CreateServer(options))
        {
            await using (var session = await server.CreateSessionAsnc())
            {
                await session.ServerSendAsync("Hello"u8.ToArray());

                var buffer = await session.ClientReceiveAsync();
                var decompressed = Decompress(buffer.ToArray());
                var text = Encoding.UTF8.GetString(decompressed);
                Assert.Equal("Hello", text);
            }
        }
    }

    private byte[] Decompress(byte[] input)
    {
        using (var compressedStream = new MemoryStream(input))
        using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            zipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }
}
