using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace LockStepServer;

/// <summary>
/// TCP 服务器（大厅）。
/// 线程模型：一个 accept 循环 + 每个连接一个接收循环。
/// 接收循环只做一件事：拆包 -> 抛出事件 -> 继续收。不碰任何游戏状态。
/// </summary>
public sealed class TcpServerTransport : IServerTransport
{
    private readonly string address;
    private readonly int port;

    private readonly ConcurrentDictionary<int, TcpPeer> peers = new();

    private TcpListener? listener;
    private CancellationTokenSource? cts;
    private int nextId;

    public TcpServerTransport(string address, int port)
    {
        this.address = address;
        this.port = port;
    }

    public bool IsRunning => listener?.Server.IsBound ?? false;

    public event Action<IPeer>? PeerConnected;
    public event Action<IPeer, byte, ReadOnlyMemory<byte>>? MessageReceived;
    public event Action<IPeer>? PeerDisconnected;

    public void Start()
    {
        if (listener is not null) return;

        cts = new CancellationTokenSource();
        listener = new TcpListener(IPAddress.Parse(address), port);
        listener.Start();
        Log.I($"TcpServerTransport listening {address}:{port}");

        _ = Task.Run(() => AcceptLoopAsync(cts.Token), cts.Token);
    }

    public void Stop()
    {
        cts?.Cancel();

        foreach (var peer in peers.Values) peer.Close();
        peers.Clear();

        listener?.Stop();
        listener = null;
        Log.I("TcpServerTransport stopped");
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await listener!.AcceptTcpClientAsync(token);
                var peer = new TcpPeer(Interlocked.Increment(ref nextId), client);
                peers[peer.Id] = peer;

                Log.I($"Tcp accepted id:{peer.Id} remote:{client.Client.RemoteEndPoint}");
                PeerConnected?.Invoke(peer);

                // 每个连接一个接收循环，framer 在循环内部创建（连接私有）
                _ = Task.Run(() => ReceiveLoopAsync(peer, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.E("AcceptLoop exception", ex);
            }
        }
    }

    private async Task ReceiveLoopAsync(TcpPeer peer, CancellationToken token)
    {
        // 关键：framer 是连接私有的，绝不能像旧项目那样所有连接共享一个 MemoryStream
        var framer = new TcpPacketFramer();
        var buffer = new byte[ServerConfig.TcpReceiveBufferSize];

        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await peer.Stream!.ReadAsync(buffer, token);
                if (read == 0) break; // 对端关闭

                framer.Append(buffer, 0, read);

                while (true)
                {
                    var result = framer.TryRead(out var cmd, out var body);

                    if (result == FramerResult.NeedMoreData) break;

                    if (result == FramerResult.Corrupted)
                    {
                        Log.W($"Corrupted packet from id:{peer.Id}, closing connection");
                        peer.Close();
                        return;
                    }

                    // 大厅是低频路径，这里拷贝一份换取"上层不用关心 buffer 生命周期"的安全性。
                    // 战斗（KCP）高频路径不需要这层拷贝，直接传引用即可。
                    var payload = new ReadOnlyMemory<byte>(body.Array!, body.Offset, body.Count).ToArray();
                    MessageReceived?.Invoke(peer, cmd, payload);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 服务器关闭
        }
        catch (Exception ex)
        {
            Log.E($"ReceiveLoop exception id:{peer.Id}", ex);
        }
        finally
        {
            peers.TryRemove(peer.Id, out _);
            PeerDisconnected?.Invoke(peer);
            peer.Close();
            Log.I($"Tcp disconnected id:{peer.Id}");
        }
    }
}
