namespace LockStepServer;

/// <summary>
/// 服务器全局配置
/// TODO: 后续可以改成从 json / 命令行 / 环境变量读取
/// </summary>
public static class ServerConfig
{
    public const string ListenAddress = "127.0.0.1";

    /// <summary>大厅端口（TCP）</summary>
    public const int TcpPort = 10085;

    /// <summary>战斗端口（KCP）</summary>
    public const int KcpPort = 10086;

    /// <summary>单个消息体最大长度，超过直接判定为非法包并断开</summary>
    public const int MaxBodyLength = 1024 * 1024;

    /// <summary>TCP 单次接收缓冲大小</summary>
    public const int TcpReceiveBufferSize = 4096;
}
