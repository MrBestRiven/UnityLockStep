namespace LockStepServer;

/// <summary>
/// 战斗常量
/// </summary>
public static class BattleConfig
{
    /// <summary>33ms 一帧（约 30Hz）</summary>
    public const int FrameIntervalMs = 33;

    /// <summary>一局最多多少帧。达到后房间必须自己停下来，不能像旧项目那样抛异常后继续跑</summary>
    public const int MaxFrameCount = 10000;

    /// <summary>
    /// 输入可写窗口的帧数。
    /// 客户端只能写 [当前帧, 当前帧 + InputWindowFrames) 区间内的输入，
    /// 超出范围一律拒绝 —— 旧项目直接拿客户端上报的帧号索引数组，越界就崩。
    /// </summary>
    public const int InputWindowFrames = 64;

    public const int MaxPlayersPerRoom = 2;
}
