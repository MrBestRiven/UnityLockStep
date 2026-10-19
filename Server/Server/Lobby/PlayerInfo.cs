namespace LockStepServer;

/// <summary>
/// 玩家数据。
/// 大厅态（TCP）和战斗态（KCP）是两条独立连接，分别持有。
/// </summary>
public sealed class PlayerInfo
{
    public required uint Id { get; init; }

    public required string Account { get; init; }

    /// <summary>大厅连接（TCP）。可能为 null —— 玩家还没登录或已断开</summary>
    public IPeer? LobbyPeer { get; set; }

    /// <summary>战斗连接（KCP）。进入战斗后才建立</summary>
    public IPeer? BattlePeer { get; set; }

    /// <summary>所在房间 ID，0 表示不在房间内</summary>
    public uint RoomId { get; set; }
}
