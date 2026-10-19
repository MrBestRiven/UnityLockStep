namespace LockStepServer;

/// <summary>
/// 传输层抽象。TCP 和 KCP 各实现一份，对外暴露相同的事件。
/// </summary>
public interface IServerTransport
{
    bool IsRunning { get; }

    event Action<IPeer>? PeerConnected;

    /// <summary>
    /// 收到一条完整消息。
    /// 契约：payload **只在本次回调内有效**（底层可能复用缓冲）。
    /// 需要跨帧留存请自行拷贝。
    /// </summary>
    event Action<IPeer, byte, ReadOnlyMemory<byte>>? MessageReceived;

    event Action<IPeer>? PeerDisconnected;

    void Start();

    void Stop();
}
