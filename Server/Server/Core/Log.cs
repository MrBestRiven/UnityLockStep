namespace LockStepServer;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}

/// <summary>
/// 日志。
/// 设计要点：战斗帧是 30Hz 的高频路径，如果每帧每包都落盘，磁盘 I/O 会直接吃掉性能
/// （旧项目就是这么做的）。所以高频日志走 <see cref="Frame"/>，默认关闭。
/// </summary>
public static class Log
{
    private static readonly object Gate = new();

    public static LogLevel MinLevel { get; set; } = LogLevel.Debug;

    /// <summary>是否输出战斗帧级别的高频日志</summary>
    public static bool EnableFrameLog { get; set; }

    public static bool EnableTimestamp { get; set; } = true;

    public static void D(string msg) => Write(LogLevel.Debug, msg);

    public static void I(string msg) => Write(LogLevel.Info, msg);

    public static void W(string msg) => Write(LogLevel.Warn, msg);

    public static void E(string msg, Exception? ex = null) =>
        Write(LogLevel.Error, ex is null ? msg : $"{msg}\n{ex}");

    /// <summary>战斗帧高频日志。只在 EnableFrameLog 打开时输出</summary>
    public static void F(string msg)
    {
        if (EnableFrameLog) Write(LogLevel.Debug, msg);
    }

    private static void Write(LogLevel level, string msg)
    {
        if (level < MinLevel) return;

        var line = EnableTimestamp
            ? $"[{DateTime.Now:HH:mm:ss.fff}][{level}] {msg}"
            : $"[{level}] {msg}";

        lock (Gate)
        {
            Console.WriteLine(line);
        }
    }
}
