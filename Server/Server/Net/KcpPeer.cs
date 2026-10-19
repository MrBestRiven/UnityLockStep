namespace LockStepServer;

/// <summary>
/// KCP 连接（战斗用）。
/// 注意：KCP 的 Send 不是立即发送，而是投递到 transport 的发送队列，
/// 由 transport 的 Tick 线程统一排空后交给 KCP。
/// 这样房间线程（33ms）和网络线程（10ms）就解耦了。
/// </summary>
public sealed class KcpPeer : IPeer
{
    private readonly KcpServerTransport transport;

    public int Id { get; }

    public bool IsConnected { get; private set; } = true;

    public KcpPeer(int id, KcpServerTransport transport)
    {
        Id = id;
        this.transport = transport;
    }

    public void Send(byte cmd, ReadOnlySpan<byte> body) => transport.EnqueueSend(Id, cmd, body);

    public void Close() => transport.Disconnect(Id);

    internal void MarkDisconnected() => IsConnected = false;
}
