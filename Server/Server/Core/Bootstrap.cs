namespace LockStepServer;

/// <summary>
/// 启动装配。
/// 这里是唯一知道所有模块存在的地方：装配依赖、接线、决定生命周期。
/// </summary>
public static class Bootstrap
{
    public static async Task RunAsync(CancellationToken token)
    {
        Log.I("Server starting...");

        var players = new PlayerRegistry();
        var rooms = new RoomRegistry();
        var lobby = new LobbyService(players, rooms);

        var tcp = new TcpServerTransport(ServerConfig.ListenAddress, ServerConfig.TcpPort);
        var kcp = new KcpServerTransport(ServerConfig.KcpPort);

        // 大厅：TCP 消息 -> LobbyService
        tcp.MessageReceived += (peer, cmd, body) => lobby.HandleMessage(peer, cmd, body);

        // 战斗：KCP 消息 -> 找到玩家所属房间 -> 投递进房间的 inbox（不直接改房间状态）
        kcp.MessageReceived += (peer, cmd, body) =>
        {
            // TODO: peer 连上 KCP 后需要先上报 playerId 完成绑定，
            //       绑定前无法确定该消息属于哪个房间，直接丢弃并记日志。
            //       var player = players.GetByBattlePeer(peer);
            //       var room = rooms.Get(player.RoomId);
            //       room?.TryPost(new RoomCommand { Type = ..., PlayerId = ..., Payload = body });
        };

        tcp.Start();
        // kcp.Start();   // TODO: 接入 kcp2k 之后打开

        Log.I($"Server started. tcp:{ServerConfig.TcpPort} kcp:{ServerConfig.KcpPort}");

        try
        {
            // 一直跑到收到退出信号
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        finally
        {
            Log.I("Server stopping...");
            kcp.Stop();
            tcp.Stop();
            rooms.StopAll();
            Log.I("Server stopped.");
        }
    }
}
