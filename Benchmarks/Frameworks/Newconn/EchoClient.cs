using Mamemaki.Newconn;
using System.Buffers;

namespace Benchmarks.Frameworks.Newconn;

internal class EchoClient : Connection, IEchoClient
{
    public int? Id { get; set; }

    protected override ValueTask OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    public async ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        Transport.Output.Write(message);
        await Transport.Output.FlushAsync(cancellationToken);

        await ReadResponseAsync(message.Length, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ReadResponseAsync(int readLen, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await Transport.Input.ReadAsync(cancellationToken);

            if (result.Buffer.Length >= readLen)
            {
                Transport.Input.AdvanceTo(result.Buffer.GetPosition(readLen));
                return;
            }

            Transport.Input.AdvanceTo(result.Buffer.Start, result.Buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }
    }
}
