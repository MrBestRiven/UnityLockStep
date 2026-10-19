using System.Collections.Concurrent;

namespace LockStepServer;

/// <summary>
/// 房间表 + 房间生命周期管理。
/// 创建房间的同时就把房间线程和它的 CancellationToken 一起准备好 —— 退出机制从第一天就存在。
/// </summary>
public sealed class RoomRegistry
{
    private sealed class RoomHandle
    {
        public required Room Room { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public Task? Task { get; set; }
    }

    private readonly ConcurrentDictionary<uint, RoomHandle> rooms = new();
    private uint nextRoomId;

    public Room? Get(uint roomId) => rooms.TryGetValue(roomId, out var h) ? h.Room : null;

    /// <summary>创建并启动一个房间</summary>
    public Room Create(int playerCount = BattleConfig.MaxPlayersPerRoom)
    {
        var roomId = Interlocked.Increment(ref nextRoomId);
        var room = new Room((uint)roomId, playerCount);
        var cts = new CancellationTokenSource();

        var handle = new RoomHandle { Room = room, Cts = cts };

        // 房间线程：从这一刻起，房间内部状态归它独有
        handle.Task = Task.Run(() => room.RunAsync(cts.Token), cts.Token);

        rooms[roomId] = handle;
        Log.I($"Room created id:{roomId} players:{playerCount}");
        return room;
    }

    /// <summary>停止并移除一个房间</summary>
    public void Stop(uint roomId)
    {
        if (!rooms.TryRemove(roomId, out var handle)) return;

        handle.Cts.Cancel();

        try
        {
            handle.Task?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // 房间内部已经自行 catch，这里忽略
        }

        handle.Cts.Dispose();
    }

    public void StopAll()
    {
        foreach (var roomId in rooms.Keys) Stop(roomId);
    }

    public IEnumerable<Room> All => rooms.Values.Select(h => h.Room);
}
