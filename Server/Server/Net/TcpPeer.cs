using System.Net.Sockets;

namespace LockStepServer;

/// <summary>
/// TCP 连接（大厅用）。
/// 大厅消息低频，Send 用同步写 + 锁串行化就够了：
/// 旧项目用 async void 发送，异常被吞且并发写同一个 stream 会字节交织。
/// </summary>
public sealed class TcpPeer : IPeer
{
    private readonly TcpClient client;
    private readonly object sendGate = new();

    public int Id { get; }

    public NetworkStream? Stream { get; private set; }

    public bool IsConnected => client.Connected;

    public TcpPeer(int id, TcpClient client)
    {
        Id = id;
        this.client = client;
        Stream = client.GetStream();
    }

    public void Send(byte cmd, ReadOnlySpan<byte> body)
    {
        if (!IsConnected || Stream is null) return;

        var packet = PacketCodec.Pack(cmd, body);

        lock (sendGate)
        {
            try
            {
                Stream.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                Log.E($"TcpPeer.Send failed id:{Id}", ex);
                Close();
            }
        }
    }

    public void Close()
    {
        try
        {
            Stream?.Dispose();
            Stream = null;
            client.Close();
        }
        catch (Exception ex)
        {
            Log.W($"TcpPeer.Close exception id:{Id} {ex.Message}");
        }
    }
}
