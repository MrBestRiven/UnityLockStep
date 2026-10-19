namespace LockStepServer;

public enum FramerResult
{
    /// <summary>数据还不够一个完整包，继续收</summary>
    NeedMoreData,

    /// <summary>取出了一个完整包</summary>
    PacketReady,

    /// <summary>包头长度非法（可能是攻击或客户端 bug），调用方应断开连接</summary>
    Corrupted,
}

/// <summary>
/// TCP 粘包/半包拆包器。
/// 设计要点：**每个连接独立持有一个实例**。
/// 旧项目用一个全局共享的 MemoryStream 被所有客户端线程复用，并发下会解析出混合数据 —— 这是必须避免的。
/// </summary>
public sealed class TcpPacketFramer
{
    private byte[] buffer;
    private int readPos;
    private int writePos;

    public TcpPacketFramer(int capacity = ServerConfig.TcpReceiveBufferSize)
    {
        buffer = new byte[capacity];
    }

    public int BufferedBytes => writePos - readPos;

    /// <summary>
    /// 追加本次收到的字节。
    /// 用 (byte[], offset, count) 而不是 Span，是因为 TCP 的接收循环是 async 方法，
    /// C# 12 不允许在 async 方法里出现 ref struct 局部变量。
    /// </summary>
    public void Append(byte[] data, int offset, int count)
    {
        Compact();
        EnsureCapacity(count);
        data.AsSpan(offset, count).CopyTo(buffer.AsSpan(writePos));
        writePos += count;
    }

    /// <summary>
    /// 尝试取出一个完整包。
    /// 注意：body 指向内部缓冲，必须在下一次 <see cref="Append"/> 之前消费完（或自行拷贝）。
    /// </summary>
    public FramerResult TryRead(out byte cmd, out ArraySegment<byte> body)
    {
        cmd = 0;
        body = default;

        var available = writePos - readPos;
        if (available < Head.Size) return FramerResult.NeedMoreData;

        if (!PacketCodec.TryReadHead(buffer.AsSpan(readPos, available), out cmd, out var len))
            return FramerResult.Corrupted;

        if (available - Head.Size < len) return FramerResult.NeedMoreData;

        body = new ArraySegment<byte>(buffer, readPos + Head.Size, len);
        readPos += Head.Size + len;
        return FramerResult.PacketReady;
    }

    /// <summary>把未消费的数据搬到缓冲头部，腾出尾部空间</summary>
    private void Compact()
    {
        var len = writePos - readPos;
        if (len == 0)
        {
            readPos = 0;
            writePos = 0;
            return;
        }

        if (readPos == 0) return;

        Array.Copy(buffer, readPos, buffer, 0, len);
        readPos = 0;
        writePos = len;
    }

    private void EnsureCapacity(int additional)
    {
        var required = (writePos - readPos) + additional;
        if (required <= buffer.Length) return;

        var size = buffer.Length;
        while (size < required) size <<= 1;
        Array.Resize(ref buffer, size);
    }
}
