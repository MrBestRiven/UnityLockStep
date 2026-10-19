using System.Runtime.InteropServices;

namespace LockStepServer;

/// <summary>
/// 消息头：1 字节 cmd + 4 字节 body 长度（小端）
/// Pack = 1 保证没有对齐填充，sizeof(Head) 一定是 5
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Head
{
    public byte Cmd;
    public int Length;

    /// <summary>头长度，和 <see cref="ServerConfig.MaxBodyLength"/> 一起用来做合法性校验</summary>
    public const int Size = 5;

    public Head(byte cmd, int length)
    {
        Cmd = cmd;
        Length = length;
    }
}
