namespace LockStepServer;

/// <summary>
/// 房间内的玩家。
/// 注意 <see cref="Pos"/> 是**房间内**的槽位号，只在自己房间有意义。
///
/// 旧项目把 pos -> player 的映射放在了全局字典里，于是两个房间的 pos 0 会互相覆盖，
/// 多开一局必炸。这里从类型设计上就杜绝了这种可能。
/// </summary>
public sealed class RoomPlayer
{
    public required uint PlayerId { get; init; }

    /// <summary>房间内槽位，0 .. MaxPlayersPerRoom-1</summary>
    public int Pos { get; init; }

    public bool IsReady { get; set; }

    /// <summary>战斗连接（KCP）。未建立时为 null</summary>
    public IPeer? Peer { get; set; }

    public bool IsConnected => Peer?.IsConnected ?? false;
}
