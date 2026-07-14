using Mamemaki.Newconn.Internal;
using Mamemaki.Newconn.Protocol;
using System.Buffers;
using System.IO.Pipelines;

namespace Mamemaki.Newconn.Tests.Protocol;

public class ProtocolReaderWriterTests
{
    readonly HelloProtocol protocol = new();

    class TestTransportConnection : TransportConnection, IAsyncDisposable
    {
        public TestTransportConnection(DuplexPipe.DuplexPipePair pair)
        {
            Transport = pair.Transport;
            Application = pair.Application;
        }

        private PipeShutdownKind shutdownKind;
        public override PipeShutdownKind ShutdownKind => shutdownKind;

        public override void Abort(ConnectionAbortedException abortReason)
        {
            Transport.Input.CancelPendingRead();
            shutdownKind = PipeShutdownKind.ReadSocketAborted;
        }

        public ValueTask DisposeAsync()
        {
            Application.Output.Complete();
            //Application.Input.CancelPendingRead();
            Transport.Output.Complete();
            //Transport.Input.CancelPendingRead();
            return default;
        }
    }

    class TestConnection : Connection
    {
        public IDuplexPipe Application { get; set; } = default!;

        public TestConnection(DuplexPipe.DuplexPipePair pair)
        {
            var transportConnection = new TestTransportConnection(pair);
            TransportConnection = transportConnection;
            Transport = TransportConnection.Transport;
            Application = TransportConnection.Application;
            RegisterDisposalObject(transportConnection);
        }
    }

    private TestConnection CreateTestConnection()
    {
        var options = new PipeOptions(useSynchronizationContext: false);
        return new TestConnection(DuplexPipe.CreateConnectionPair(options, options));
    }

    [Fact]
    public async Task ReadOneAsync()
    {
        var connection = CreateTestConnection();
        for (int i = 0; i < 3; i++)
        {
            await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
        }
        connection.Application.Output.Complete();

        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);
        var count = 0;
        for (int i = 0; i < 3; i++)
        {
            var message = await protocolReaderWriter.ReadOneAsync();

            count++;
            Assert.Equal(HelloProtocol.Data, message);
        }
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ReadOneAsync_Chunked()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);

        var readTask = protocolReaderWriter.ReadOneAsync();

        // Write byte by byte
        var dataBytes = HelloProtocol.DataBytes;
        for (int i = 0; i < dataBytes.Length; i++)
        {
            await connection.Application.Output.WriteAsync(dataBytes.AsMemory(i, 1));
        }

        var message = await readTask;
        Assert.Equal(HelloProtocol.Data, message);
    }

    [Fact]
    public async Task ReadOneAsync_Chunked2()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);

        var readTask = protocolReaderWriter.ReadOneAsync();

        connection.Application.Output.Write(HelloProtocol.DataBytes);
        connection.Application.Output.Write(HelloProtocol.DataBytes.AsSpan(0, 5));
        await connection.Application.Output.FlushAsync();

        var message = await readTask;
        Assert.Equal(HelloProtocol.Data, message);

        var readTask2 = protocolReaderWriter.ReadOneAsync();
        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes.AsMemory(5));
        message = await readTask2;
        Assert.Equal(HelloProtocol.Data, message);
    }

    [Fact]
    public async Task ReadOneAsync_MaxMessageSizeOver()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol, 
            maximumMessageBytes: 10);

        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            async () => await protocolReaderWriter.ReadOneAsync());
        Assert.EndsWith("B was exceeded.", ex.Message);
    }

    [Fact]
    public async Task ReadOneAsync_CancelPendingRead()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);

        var readTask = protocolReaderWriter.ReadOneAsync();
        connection.Transport.Input.CancelPendingRead();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await readTask);

        // Read once more
        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
        var message = await protocolReaderWriter.ReadOneAsync();
        Assert.Equal(HelloProtocol.Data, message);
    }

    [Fact]
    public async Task ReadOneAsync_CancelPendingRead2()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);

        connection.Transport.Input.CancelPendingRead();
        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await protocolReaderWriter.ReadOneAsync());

        // Read once more
        var message = await protocolReaderWriter.ReadOneAsync();
        Assert.Equal(HelloProtocol.Data, message);
    }

    [Fact]
    public async Task ReadOneAsync_Cancel()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);

        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);

        var message = await protocolReaderWriter.ReadOneAsync();
        Assert.Equal(HelloProtocol.Data, message);

        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await protocolReaderWriter.ReadOneAsync(cts.Token));

        // After cancel
        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);

        message = await protocolReaderWriter.ReadOneAsync();
        Assert.Equal(HelloProtocol.Data, message);
    }

    [Fact]
    public async Task ReadOneAsync_Complete()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);

        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
        await connection.Application.Output.CompleteAsync();

        var message = await protocolReaderWriter.TryReadOneAsync();
        Assert.Equal(HelloProtocol.Data, message);

        message = await protocolReaderWriter.TryReadOneAsync();
        Assert.Null(message);

        await Assert.ThrowsAsync<ConnectionClosedByRemoteException>(async () => await protocolReaderWriter.ReadOneAsync());
    }

    [Fact]
    public async Task RunReadLoopAsync()
    {
        var connection = CreateTestConnection();
        for (int i = 0; i < 3; i++)
        {
            await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
        }
        connection.Application.Output.Complete();

        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);
        var count = 0;
        await foreach (var message in protocolReaderWriter.RunReadLoopAsync())
        {
            count++;
            Assert.Equal(HelloProtocol.Data, message);
        }
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task RunReadLoopAsync_IncompleteData()
    {
        var connection = CreateTestConnection();

        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes[0..3].AsMemory());
        connection.Application.Output.Complete();

        var ex = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);
            await foreach (var message in protocolReaderWriter.RunReadLoopAsync())
            {
                Assert.Equal(HelloProtocol.Data, message);
            }
        });
        Assert.Equal("There are incomplete data.", ex.Message);
    }

    [Fact]
    public async Task RunReadLoopAsync_MaxMessageSizeOver()
    {
        var connection = CreateTestConnection();
        var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol,
            maximumMessageBytes: 10);

        await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
        connection.Application.Output.Complete();

        var ex = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var message in protocolReaderWriter.RunReadLoopAsync())
            {
                ;
            }
        });
        Assert.EndsWith("B was exceeded.", ex.Message);
    }

    [Fact]
    public async Task RunReadLoopAsync_Asynchronous()
    {
        var connection = CreateTestConnection();

        async Task WritingTask()
        {
            for (int i = 0; i < 3; i++)
            {
                await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
            }
            connection.Application.Output.Complete();
        }

        async Task ReadingTask()
        {
            var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);
            var count = 0;
            await foreach (var message in protocolReaderWriter.RunReadLoopAsync())
            {
                count++;
                Assert.Equal(HelloProtocol.Data, message);
            }
            Assert.Equal(3, count);
        }

        var readingTask = ReadingTask();
        var writingTask = WritingTask();

        await writingTask;
        await readingTask;
    }

    [Fact]
    public async Task RunReadLoopAsync_CloseWhileReading()
    {
        var connection = CreateTestConnection();

        async Task WritingTask()
        {
            for (int i = 0; i < 3; i++)
            {
                await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
            }
            await connection.CloseAsync();
        }

        async Task ReadingTask()
        {
            var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);
            var count = 0;
            await foreach (var message in protocolReaderWriter.RunReadLoopAsync())
            {
                count++;
                Assert.Equal(HelloProtocol.Data, message);
            }
            Assert.True(connection.IsClosed);
        }

        var readingTask = ReadingTask();
        var writingTask = WritingTask();

        await writingTask;
        await readingTask;
    }

    [Fact]
    public async Task RunReadLoopAsync_AbortWhileReading()
    {
        var connection = CreateTestConnection();

        async Task WritingTask()
        {
            for (int i = 0; i < 3; i++)
            {
                await connection.Application.Output.WriteAsync(HelloProtocol.DataBytes);
            }
            connection.Abort();
        }

        async Task ReadingTask()
        {
            var protocolReaderWriter = new ProtocolReaderWriter<string>(connection, protocol);
            var count = 0;
            await foreach (var message in protocolReaderWriter.RunReadLoopAsync())
            {
                count++;
                Assert.Equal(HelloProtocol.Data, message);
            }
            Assert.True(connection.IsClosed);
        }

        var readingTask = ReadingTask();
        var writingTask = WritingTask();

        await writingTask;
        await readingTask;
    }
}
