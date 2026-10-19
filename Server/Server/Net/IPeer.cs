namespace LockStepServer;

/// <summary>
/// 一条连接的统一抽象。
/// 上层（Lobby / Room）只认 IPeer，不关心底层是 TCP 还是 KCP —— 广播时不用 if 判断。
/// </summary>
public interface IPeer
{
    /// <summary>连接 ID，在同一个 transport 内唯一</summary>
    int Id { get; }

    bool IsConnected { get; }

    /// <summary>
    /// 发送一条消息。实现必须保证线程安全（可能被房间线程、大厅线程同时调用）。
    /// </summary>
    void Send(byte cmd, ReadOnlySpan<byte> body);

    void Close();
}
