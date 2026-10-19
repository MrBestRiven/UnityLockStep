using System.Collections.Concurrent;

namespace LockStepServer;

/// <summary>
/// 玩家表。
/// 并发安全是必须的：TCP 大厅每个连接一个线程，都会读写这张表
/// （旧项目用普通 Dictionary 裸奔，并发写会直接损坏内部结构）。
/// </summary>
public sealed class PlayerRegistry
{
    private readonly ConcurrentDictionary<uint, PlayerInfo> byId = new();
    private readonly ConcurrentDictionary<string, PlayerInfo> byAccount = new();

    private uint nextId;

    public PlayerInfo? Get(uint id) => byId.TryGetValue(id, out var p) ? p : null;

    public PlayerInfo? GetByAccount(string account) =>
        byAccount.TryGetValue(account, out var p) ? p : null;

    /// <summary>新增玩家。account 已存在时返回 null</summary>
    public PlayerInfo? TryAdd(string account)
    {
        var id = Interlocked.Increment(ref nextId);
        var player = new PlayerInfo { Id = id, Account = account };

        if (!byAccount.TryAdd(account, player)) return null;

        byId[id] = player;
        return player;
    }

    public void Remove(uint id)
    {
        if (!byId.TryRemove(id, out var player)) return;
        byAccount.TryRemove(player.Account, out _);
    }

    public IEnumerable<PlayerInfo> All => byId.Values;
}
