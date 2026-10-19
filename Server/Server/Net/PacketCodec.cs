using System.Buffers.Binary;

namespace LockStepServer;

/// <summary>
/// 消息编解码。
/// 设计要点：所有方法都是纯函数式，输入输出都走 Span，
/// 不存在"共享的 MemoryStream"这种跨线程可变状态（旧项目的 P0 问题就出在这）。
/// </summary>
public static class PacketCodec
{
    public static void WriteHead(Span<byte> dest, byte cmd, int bodyLength)
    {
        dest[0] = cmd;
        BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(1, 4), bodyLength);
    }

    /// <summary>
    /// 读包头。长度非法（负数或超过 MaxBodyLength）时返回 false —— 调用方应断开连接。
    /// </summary>
    public static bool TryReadHead(ReadOnlySpan<byte> src, out byte cmd, out int bodyLength)
    {
        cmd = 0;
        bodyLength = 0;

        if (src.Length < Head.Size) return false;

        cmd = src[0];
        bodyLength = BinaryPrimitives.ReadInt32LittleEndian(src.Slice(1, 4));

        return bodyLength >= 0 && bodyLength <= ServerConfig.MaxBodyLength;
    }

    /// <summary>组装一个完整包（头 + 体）。高频路径后续可换成从 BufferPool 取数组</summary>
    public static byte[] Pack(byte cmd, ReadOnlySpan<byte> body)
    {
        var packet = new byte[Head.Size + body.Length];
        WriteHead(packet, cmd, body.Length);
        body.CopyTo(packet.AsSpan(Head.Size));
        return packet;
    }
}
