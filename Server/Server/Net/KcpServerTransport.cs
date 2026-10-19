using System.Collections.Concurrent;

namespace LockStepServer;

// 接入 kcp2k 后这两个事件会在 OnKcpConnected / OnKcpData 中触发，届时删掉本行抑制
#pragma warning disable CS0067 // 事件从未使用

/// <summary>
/// KCP 服务器（战斗）。
///
/// 线程模型：
///   房间线程 --EnqueueSend--> 发送队列 --(Tick 线程排空)--> KCP --> 网络
///   网络 -----Tick--> OnData 回调 --(投递)--> 房间 inbox
///
/// 依赖 kcp2k：
///   - 从旧项目拷贝：ThirtyParty/ERIUnityLockstepServer/NetCore/kcp2k
///   - 或 NuGet 引入 kcp2k
/// 接入前本类的 KCP 调用点留了 TODO，其余结构已经就位。
/// </summary>
public sealed class KcpServerTransport : IServerTransport
{
    private readonly int port;

    private readonly ConcurrentQueue<(int ConnectionId, byte[] Packet)> sendQueue = new();
    private readonly ConcurrentDictionary<int, KcpPeer> peers = new();

    private CancellationTokenSource? cts;

    /// <summary>KCP 的 update 间隔，默认 10ms。帧同步下这个值直接决定广播延迟</summary>
    public int TickIntervalMs { get; set; } = 10;

    public KcpServerTransport(int port)
    {
        this.port = port;
    }

    public bool IsRunning => cts is not null && !cts.IsCancellationRequested;

    public event Action<IPeer>? PeerConnected;
    public event Action<IPeer, byte, ReadOnlyMemory<byte>>? MessageReceived;
    public event Action<IPeer>? PeerDisconnected;

    public void Start()
    {
        if (IsRunning) return;

        cts = new CancellationTokenSource();

        // TODO: 接入 kcp2k
        // kcpServer = new KcpServer(
        //     OnKcpConnected, OnKcpData, OnKcpDisconnected, OnKcpError, config);
        // kcpServer.Start(port);

        _ = Task.Run(() => TickLoopAsync(cts.Token), cts.Token);
        Log.I($"KcpServerTransport started on port {port}");
    }

    public void Stop()
    {
        cts?.Cancel();

        // TODO: kcpServer?.Stop();
        peers.Clear();
        cts = null;
        Log.I("KcpServerTransport stopped");
    }

    /// <summary>房间线程调用：把包丢进队列立刻返回，不阻塞帧循环</summary>
    public void EnqueueSend(int connectionId, byte cmd, ReadOnlySpan<byte> body)
    {
        sendQueue.Enqueue((connectionId, PacketCodec.Pack(cmd, body)));
    }

    public void Disconnect(int connectionId)
    {
        // TODO: kcpServer?.Disconnect(connectionId);
        if (peers.TryRemove(connectionId, out var peer))
        {
            peer.MarkDisconnected();
            PeerDisconnected?.Invoke(peer);
        }
    }

    private async Task TickLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                DrainSendQueue();
                // TODO: kcpServer?.Tick();
            }
            catch (Exception ex)
            {
                Log.E("Kcp tick exception", ex);
            }

            await Task.Delay(TickIntervalMs, token);
        }
    }

    /// <summary>
    /// 排空发送队列。
    /// 注意这里是 while 而不是 if —— 旧项目一次 Tick 只发一个包，
    /// 4 人房一帧广播 4 个包要 40ms 才发完，直接吃掉了一整个帧间隔。
    /// </summary>
    private void DrainSendQueue()
    {
        while (sendQueue.TryDequeue(out var item))
        {
            // TODO: kcpServer?.Send(item.ConnectionId,
            //           new ArraySegment<byte>(item.Packet),
            //           KcpChannel.Unreliable);

            Log.F($"KcpSend -> conn:{item.ConnectionId} len:{item.Packet.Length}");
        }
    }

    // TODO: kcp2k 接入后实现这三个回调（并把对应的私有字段声明补上）
    // private void OnKcpConnected(int connectionId)
    // {
    //     var peer = new KcpPeer(connectionId, this);
    //     peers[connectionId] = peer;
    //     PeerConnected?.Invoke(peer);
    // }

    // private void OnKcpData(int connectionId, ArraySegment<byte> data, KcpChannel channel)
    // {
    //     if (!peers.TryGetValue(connectionId, out var peer)) return;
    //
    //     // 这里不要再 ToArray() 拷贝一份（旧项目在高频收包路径上做了全量拷贝）。
    //     // 直接把 data 交给上层，上层必须同步消费完。
    //     var (cmd, body) = 解包(data);
    //     MessageReceived?.Invoke(peer, cmd, body);
    // }

    // private void OnKcpDisconnected(int connectionId) => Disconnect(connectionId);
}
