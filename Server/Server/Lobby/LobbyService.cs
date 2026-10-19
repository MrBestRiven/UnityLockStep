namespace LockStepServer;

/// <summary>
/// 大厅服务：登录 / 创建房间 / 加入房间。
///
/// 【最重要的契约】
/// 本类运行在 TCP 接收线程上，**绝对不能直接修改 Room 的内部状态**。
/// 需要影响房间时，只能投递命令：
///
///     room.TryPost(new RoomCommand { Type = RoomCommandType.PlayerJoin, PlayerId = id });
///
/// 然后立刻返回。真正的加入动作由房间自己的线程在下一帧执行。
/// 这条边界守住之后，房间状态就是事实上的单线程，不需要任何锁。
/// </summary>
public sealed class LobbyService
{
    private readonly PlayerRegistry players;
    private readonly RoomRegistry rooms;

    public LobbyService(PlayerRegistry players, RoomRegistry rooms)
    {
        this.players = players;
        this.rooms = rooms;
    }

    /// <summary>
    /// 处理一条大厅消息。
    /// TODO: body 是 protobuf 序列化后的字节，用 Parser.ParseFrom(body.Span) 解码。
    /// TODO: 定义 LogicMsgID 枚举（Login / CreateRoom / JoinRoom ...）
    /// </summary>
    public void HandleMessage(IPeer peer, byte cmd, ReadOnlyMemory<byte> body)
    {
        // 注意：本方法可能被多个 TCP 线程并发调用。
        // 所有共享状态（players / rooms）都已是并发安全的，但单个 PlayerInfo 的
        // 字段读写仍需你自己保证顺序语义（比如同一账号重复登录）。

        switch (cmd)
        {
            // case (byte)LogicMsgID.Login:
            //     OnLogin(peer, body);
            //     break;
            // case (byte)LogicMsgID.CreateRoom:
            //     OnCreateRoom(peer, body);
            //     break;
            // case (byte)LogicMsgID.JoinRoom:
            //     OnJoinRoom(peer, body);
            //     break;
            default:
                Log.W($"Unknown lobby cmd:{cmd} from peer:{peer.Id}");
                break;
        }
    }

    // TODO: 登录 —— 查/建玩家，绑定 LobbyPeer，回包 playerId
    // private void OnLogin(IPeer peer, ReadOnlyMemory<byte> body) { ... }

    // TODO: 创建房间 —— rooms.Create(...)，回包 roomId
    // private void OnCreateRoom(IPeer peer, ReadOnlyMemory<byte> body) { ... }

    // TODO: 加入房间 —— 设置 player.RoomId，然后
    //       rooms.Get(roomId)?.TryPost(new RoomCommand { Type = PlayerJoin, PlayerId = player.Id });
    //       不要在这里直接改 Room 的 players 集合！
    // private void OnJoinRoom(IPeer peer, ReadOnlyMemory<byte> body) { ... }
}
