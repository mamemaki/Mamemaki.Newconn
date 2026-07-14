// This code is essentially based on Kestrel.
// https://github.com/dotnet/aspnetcore/blob/1b7269e1ec8b0b1103a3d2dffaa400aea347bf39/src/Servers/Kestrel/Transport.Sockets/src/Internal/SocketConnection.cs
//
// We have changed the following points.
// - Managing closed reason(ShutdownKind) - ported from Pipelines.Sockets.Unofficial
// - Debug logging - ported from Pipelines.Sockets.Unofficial
// - Not throw ConnectionAbortedException when graceful close
// - Not throw ConnectionResetException when connection reset
//#define VERBOSE
using Mamemaki.Newconn.Internal;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Mamemaki.Newconn.Sockets;

/// <summary>
/// Represents Socket connection
/// </summary>
internal sealed class SocketConnection : TransportConnection, IAsyncDisposable
{
    public PipeWriter Input => Application.Output;

    public PipeReader Output => Application.Input;


    private int _socketShutdownKind;
    /// <summary>
    /// When possible, determines how the pipe first reached a close state
    /// </summary>
    public override PipeShutdownKind ShutdownKind => (PipeShutdownKind)Volatile.Read(ref _socketShutdownKind);
    /// <summary>
    /// When the ShutdownKind relates to a socket error, may contain the socket error code
    /// </summary>
    public SocketError SocketError { get; private set; }
    public override int ShutdownErrorCode => (int)SocketError;

    private bool TrySetShutdown(PipeShutdownKind kind) => kind != PipeShutdownKind.None
        && Interlocked.CompareExchange(ref _socketShutdownKind, (int)kind, 0) == 0;
    private bool TrySetShutdown(PipeShutdownKind kind, Exception? ex) => ex is SocketException se ?
        TrySetShutdown(kind, se.SocketErrorCode) : TrySetShutdown(kind);
    private bool TrySetShutdown(PipeShutdownKind kind, SocketError socketError)
    {
        bool win = TrySetShutdown(kind);
        if (win) SocketError = socketError;
        return win;
    }

    [Conditional("VERBOSE")]
    private void DebugLog(string message, [CallerMemberName] string? caller = null) => 
        DebugHelper.Log(ConnectionNo, message, caller);

    private readonly Socket socket;
    private readonly ILogger logger;
    private readonly SocketReceiver receiver;
    private SocketSender? sender;
    private readonly SocketSenderPool socketSenderPool;
    private readonly CancellationTokenSource connectionClosedTokenSource = new CancellationTokenSource();

    private readonly object shutdownLock = new();
    private volatile bool shutdown;
    private volatile Exception? shutdownReason;
    private Task? sendingTask;
    private Task? receivingTask;
    private readonly TaskCompletionSource waitForConnectionClosedTcs = new TaskCompletionSource();
    private bool connectionClosed;
    private readonly bool waitForData;
    private readonly int minAllocBufferSize;

    internal SocketConnection(Socket socket,
        long connNo,
        MemoryPool<byte> memoryPool,
        PipeScheduler socketScheduler,
        ILogger logger,
        SocketSenderPool socketSenderPool,
        PipeOptions inputOptions,
        PipeOptions outputOptions,
        bool waitForData = true,
        int minAllocBufferSize = 0)
    {
        Debug.Assert(socket != null);
        Debug.Assert(memoryPool != null);
        Debug.Assert(logger != null);

        ConnectionNo = connNo;
        MemoryPool = memoryPool;
        this.socket = socket;
        this.logger = logger;
        this.waitForData = waitForData;
        this.minAllocBufferSize = minAllocBufferSize;
        this.socketSenderPool = socketSenderPool;

        ConnectionClosed = connectionClosedTokenSource.Token;

        receiver = new SocketReceiver(socketScheduler);

        var pair = DuplexPipe.CreateConnectionPair(inputOptions, outputOptions);

        // Set the transport and connection id
        Transport = pair.Transport;
        Application = pair.Application;
    }

    public void Start()
    {
        try
        {
            // Spawn send and receive logic
            receivingTask = DoReceive();
            sendingTask = DoSend();
        }
        catch (Exception ex)
        {
            logger.LogError(0, ex, $"Unexpected exception in {nameof(SocketConnection)}.{nameof(Start)}.");
        }
    }

    public override void Abort(ConnectionAbortedException abortReason)
    {
        // Try to gracefully close the socket to match libuv behavior.
        Shutdown(abortReason);

        // Cancel ProcessSends loop after calling shutdown to ensure the correct shutdown gets set.
        Output.CancelPendingRead();
    }

    public async ValueTask DisposeAsync()
    {
        TrySetShutdown(PipeShutdownKind.PipeDisposed);

        Transport.Input.Complete();
        //TrySetShutdown(PipeShutdownKind.OutputReaderCompleted);
        Transport.Output.Complete();
        //TrySetShutdown(PipeShutdownKind.OutputWriterCompleted);

        try
        {
            // Now wait for both to complete
            if (receivingTask != null)
            {
                await receivingTask;
            }

            if (sendingTask != null)
            {
                await sendingTask;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(0, ex, $"Unexpected exception in {nameof(SocketConnection)}.{nameof(Start)}.");
        }
        finally
        {
            receiver.Dispose();
            sender?.Dispose();
        }

        connectionClosedTokenSource.Dispose();
    }

    private async Task DoReceive()
    {
        Exception? error = null;
        DebugLog("starting receive loop");

        try
        {
            while (!shutdown)
            {
                if (waitForData)
                {
                    DebugLog($"wait for data before allocating a buffer..");
                    var waitForDataResult = await receiver.WaitForDataAsync(socket);

                    if (!IsNormalCompletion(waitForDataResult))
                    {
                        break;
                    }

                    DebugLog($"data is arrived. {waitForDataResult.BytesTransferred} bytes available");
                }

                // Ensure we have some reasonable amount of buffer space
                var buffer = Input.GetMemory(minAllocBufferSize);
                DebugLog($"leased {buffer.Length} bytes from pipe");

                var receiveResult = await receiver.ReceiveAsync(socket, buffer);
                if (!IsNormalCompletion(receiveResult))
                {
                    break;
                }

                var bytesReceived = receiveResult.BytesTransferred;
                DebugLog($"received {bytesReceived} bytes");

                if (bytesReceived == 0)
                {
                    // FIN
                    NewconnLog.ConnectionReadFin(logger, ConnectionNo);
                    TrySetShutdown(PipeShutdownKind.ReadEndOfStream);
                    break;
                }

                Input.Advance(bytesReceived);

                DebugLog("flushing pipe");
                var flushTask = Input.FlushAsync();

                var paused = !flushTask.IsCompleted;

                if (paused)
                {
                    NewconnLog.ConnectionPause(logger, ConnectionNo);
                }

                var result = await flushTask;

                if (paused)
                {
                    NewconnLog.ConnectionResume(logger, ConnectionNo);
                }

                DebugLog($"pipe flushed ({result.IsCompleted}, {result.IsCanceled})");
                if (result.IsCompleted)
                {
                    TrySetShutdown(PipeShutdownKind.ReadFlushCompleted);
                    break;
                }
                if (result.IsCanceled)
                {
                    TrySetShutdown(PipeShutdownKind.ReadFlushCanceled);
                    break;
                }

                bool IsNormalCompletion(SocketOperationResult result)
                {
                    // There's still a small chance that both DoReceive() and DoSend() can log the same connection reset.
                    // Both logs will have the same ConnectionId. I don't think it's worthwhile to lock just to avoid this.
                    // When shutdown is set, error is ignored, so it does not need to be initialized.
                    if (shutdown)
                    {
                        DebugLog("exit: already shutdown");
                        return false;
                    }

                    if (!result.HasError)
                    {
                        return true;
                    }

                    if (IsConnectionResetError(result.SocketError.SocketErrorCode))
                    {
                        var ex = result.SocketError;
                        TrySetShutdown(PipeShutdownKind.ReadSocketReset, result.SocketError.SocketErrorCode);

                        NewconnLog.ConnectionReset(logger, ConnectionNo);
                        DebugLog("exit: reset");
                        return false;
                    }

                    if (IsConnectionAbortError(result.SocketError.SocketErrorCode))
                    {
                        error = result.SocketError;

                        // This is unexpected if the socket hasn't been disposed yet.
                        TrySetShutdown(PipeShutdownKind.ReadSocketAborted, result.SocketError.SocketErrorCode);
                        NewconnLog.ConnectionError(logger, ConnectionNo, error);
                        DebugLog("exit: aborted");
                        return false;
                    }

                    // This is unexpected.
                    error = result.SocketError;
                    TrySetShutdown(PipeShutdownKind.ReadSocketError, result.SocketError.SocketErrorCode);
                    NewconnLog.ConnectionError(logger, ConnectionNo, error);
                    DebugLog($"exit: socket error({error?.Message})");
                    return false;
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            // This exception should always be ignored because shutdownReason should be set.
            if (!shutdown)
            {
                // This is unexpected if the socket hasn't been disposed yet.
                error = ex;
                TrySetShutdown(PipeShutdownKind.ReadDisposed);
                NewconnLog.ConnectionError(logger, ConnectionNo, error);
            }
            DebugLog("exit: disposed");
        }
        catch (IOException ex)
        {
            error = ex;
            TrySetShutdown(PipeShutdownKind.ReadIOException);
            NewconnLog.ConnectionError(logger, ConnectionNo, error);
            DebugLog($"exit: io({error.Message})");
        }
        catch (Exception ex)
        {
            // This is unexpected.
            error = ex;
            TrySetShutdown(PipeShutdownKind.ReadException, error);
            NewconnLog.ConnectionError(logger, ConnectionNo, error);
            DebugLog($"exit: unexpected error({error.Message})");
        }
        finally
        {
            // If Shutdown() has already been called, assume that was the reason ProcessReceives() exited.
            DebugLog($"marking input pipe as complete");
            Input.Complete(shutdownReason ?? error);

            TrySetShutdown(PipeShutdownKind.InputWriterCompleted, error);

            DebugLog($"fire and wait connection closed event");
            FireConnectionClosed();
            await waitForConnectionClosedTcs.Task;

            DebugLog(error is null ? "exiting receive loop with success" : 
                $"exiting receive loop with failure: {error.Message}");
        }
    }

    private async Task DoSend()
    {
        Exception? shutdownReason = null;
        Exception? unexpectedError = null;
        DebugLog("starting send loop");

        try
        {
            while (true)
            {
                DebugLog("awaiting data from pipe..");
                var result = await Output.ReadAsync();

                if (result.IsCanceled)
                {
                    DebugLog("cancelled");
                    break;
                }
                var buffer = result.Buffer;

                if (!buffer.IsEmpty)
                {
                    DebugLog($"sending {buffer.Length} bytes over socket..");
                    sender = socketSenderPool.Rent();
                    var transferResult = await sender.SendAsync(socket, buffer);

                    if (transferResult.HasError)
                    {
                        if (IsConnectionResetError(transferResult.SocketError.SocketErrorCode))
                        {
                            var ex = transferResult.SocketError;
                            TrySetShutdown(PipeShutdownKind.WriteSocketReset, transferResult.SocketError.SocketErrorCode);
                            NewconnLog.ConnectionReset(logger, ConnectionNo);
                            DebugLog("exit: reset");
                            break;
                        }

                        if (IsConnectionAbortError(transferResult.SocketError.SocketErrorCode))
                        {
                            TrySetShutdown(PipeShutdownKind.WriteSocketAborted, transferResult.SocketError.SocketErrorCode);
                            shutdownReason = transferResult.SocketError;
                            DebugLog("exit: aborted");
                            break;
                        }

                        unexpectedError = shutdownReason = transferResult.SocketError;
                        TrySetShutdown(PipeShutdownKind.WriteSocketError, transferResult.SocketError.SocketErrorCode);
                        DebugLog($"exit: socket error({unexpectedError?.Message})");
                    }

                    // We don't return to the pool if there was an exception, and
                    // we keep the sender assigned so that we can dispose it in StartAsync.
                    socketSenderPool.Return(sender);
                    sender = null;
                }

                DebugLog("advancing");
                Output.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    DebugLog("completed");
                    break;
                }
            }
            TrySetShutdown(PipeShutdownKind.WriteEndOfStream);
        }
        catch (ObjectDisposedException ex)
        {
            // This should always be ignored since Shutdown() must have already been called by Abort().
            shutdownReason = ex;
            TrySetShutdown(PipeShutdownKind.WriteDisposed);
            DebugLog("exit: disposed");
        }
        catch (IOException ex)
        {
            shutdownReason = ex;
            unexpectedError = ex;
            TrySetShutdown(PipeShutdownKind.WriteIOException);
            NewconnLog.ConnectionError(logger, ConnectionNo, unexpectedError);
            DebugLog($"exit: io({ex.Message})");
        }
        catch (Exception ex)
        {
            shutdownReason = ex;
            unexpectedError = ex;
            TrySetShutdown(PipeShutdownKind.WriteException, ex);
            NewconnLog.ConnectionError(logger, ConnectionNo, unexpectedError);
            DebugLog($"exit: unexpected error({ex.Message})");
        }
        finally
        {
            DebugLog("shutting down socket");
            Shutdown(shutdownReason);

            // Complete the output after disposing the socket
            DebugLog($"marking output pipe as complete");
            Output.Complete(unexpectedError);
            TrySetShutdown(PipeShutdownKind.InputReaderCompleted, unexpectedError);

            // Cancel any pending flushes so that the input loop is un-paused
            Input.CancelPendingFlush();

            DebugLog(unexpectedError is null ? "exiting send loop with success" : 
                $"exiting send loop with failure: {unexpectedError.Message}");
        }
    }

    private void FireConnectionClosed()
    {
        // Guard against scheduling this multiple times
        if (connectionClosed)
        {
            return;
        }

        connectionClosed = true;

        ThreadPool.UnsafeQueueUserWorkItem(state =>
        {
            state.CancelConnectionClosedToken();

            state.waitForConnectionClosedTcs.TrySetResult();
        },
        this,
        preferLocal: false);
    }

    private void Shutdown(Exception? shutdownReason)
    {
        lock (shutdownLock)
        {
            if (shutdown)
            {
                return;
            }

            // Make sure to dispose the socket after the volatile shutdown is set.
            // Without this, the RequestsCanBeAbortedMidRead test will sometimes fail when
            // a BadHttpRequestException is thrown instead of a TaskCanceledException.
            shutdown = true;
            this.shutdownReason = shutdownReason;

            // NB: not shutdownReason since we don't want to do this on graceful completion
            if (shutdownReason is not null)
            {
                NewconnLog.ConnectionWriteRst(logger, ConnectionNo, shutdownReason.Message);

                // This forces an abortive close with linger time 0 (and implies Dispose)
                socket.Close(timeout: 0);
                return;
            }

            NewconnLog.ConnectionWriteFin(logger, ConnectionNo,
                this.shutdownReason?.Message ?? "send loop completed gracefully");

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Ignore any errors from Socket.Shutdown() since we're tearing down the connection anyway.
            }

            socket.Dispose();
        }
    }

    private void CancelConnectionClosedToken()
    {
        try
        {
            connectionClosedTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            logger.LogError(0, ex, $"Unexpected exception in {nameof(SocketConnection)}.{nameof(CancelConnectionClosedToken)}.");
        }
    }

    private static bool IsConnectionResetError(SocketError errorCode)
    {
        return errorCode == SocketError.ConnectionReset ||
               errorCode == SocketError.Shutdown ||
               errorCode == SocketError.ConnectionAborted && OperatingSystem.IsWindows();
    }

    private static bool IsConnectionAbortError(SocketError errorCode)
    {
        // Calling Dispose after ReceiveAsync can cause an "InvalidArgument" error on *nix.
        return errorCode == SocketError.OperationAborted ||
               errorCode == SocketError.Interrupted ||
               errorCode == SocketError.InvalidArgument && !OperatingSystem.IsWindows();
    }
}
