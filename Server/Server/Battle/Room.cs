using System.Collections.Concurrent;

namespace LockStepServer;

public enum RoomCommandType
{
    PlayerJoin,
    PlayerLeave,
    PlayerReady,
    PlayerInput,
    Stop,
}

/// <summary>
/// 投递给房间的命令。
/// 外部线程（TCP 大厅线程、KCP 网络线程）只能通过投递这个结构体来影响房间，
/// 不允许直接读写房间的任何字段。
/// </summary>
public struct RoomCommand
{
    public RoomCommandType Type;
    public uint PlayerId;

    /// <summary>PlayerInput 用：客户端上报的帧号</summary>
    public int Frame;

    /// <summary>PlayerInput 用：客户端上报的输入字节</summary>
    public byte Input;

    /// <summary>原始消息体，用于需要 protobuf 解码的命令</summary>
    public ReadOnlyMemory<byte> Payload;
}

/// <summary>
/// 一个战斗房间。
///
/// 【线程模型 —— 本骨架的核心约定】
///   外部线程  --TryPost(cmd)-->  inbox  .........>  （边界）
///   房间线程  <--DrainCommands--  inbox            Tick() 读写所有状态
///
/// 房间一旦启动，它的一切状态（玩家列表、帧号、输入缓冲）**只由 RunAsync 这个循环读写**。
/// 因此房间内部完全不需要锁，也不会出现旧项目那种"字典被三个线程同时改"的问题。
///
/// 生命周期：CancellationToken 从创建就有。旧项目的帧循环是 while(true)，
/// 打满 MaxFrameCount 后抛异常被自己 catch 掉继续跑，每帧刷异常日志把磁盘写满。
/// </summary>
public sealed class Room
{
    private readonly ConcurrentQueue<RoomCommand> inbox = new();
    private readonly List<RoomPlayer> players = new();
    private readonly InputBuffer inputs;

    public Room(uint roomId, int playerCount)
    {
        RoomId = roomId;
        PlayerCount = playerCount;
        inputs = new InputBuffer(playerCount);
    }

    public uint RoomId { get; }

    public int PlayerCount { get; }

    /// <summary>服务器权威帧号，从 -1 开始（还没开始推进）</summary>
    public int AuthoritativeFrame { get; private set; } = -1;

    public bool IsRunning { get; private set; }

    public bool IsFull => players.Count >= PlayerCount;

    /// <summary>
    /// 外部线程唯一的入口：投递一条命令后立即返回。
    /// </summary>
    public bool TryPost(RoomCommand cmd)
    {
        if (!IsRunning && cmd.Type != RoomCommandType.Stop) return false;

        inbox.Enqueue(cmd);
        return true;
    }

    /// <summary>
    /// 房间自己的主循环。由 <see cref="RoomRegistry"/> 在创建时启动。
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        IsRunning = true;
        Log.I($"Room {RoomId} started");

        try
        {
            while (!token.IsCancellationRequested)
            {
                DrainCommands();

                if (!TryTick())
                {
                    // 正常结束（人数不够 / 打满帧数），退出循环而不是抛异常
                    break;
                }

                // TODO: 这里用 Task.Delay 会有累积漂移。
                //       更精确的做法是记录下一帧的目标时间戳，用 Stopwatch 算出真实需要等待的时间。
                await Task.Delay(BattleConfig.FrameIntervalMs, token);
            }
        }
        catch (OperationCanceledException)
        {
            // 被外部取消，正常
        }
        catch (Exception ex)
        {
            Log.E($"Room {RoomId} crashed", ex);
        }
        finally
        {
            IsRunning = false;
            Log.I($"Room {RoomId} stopped at frame {AuthoritativeFrame}");
        }
    }

    /// <summary>消费所有投递进来的命令。只有房间线程会调</summary>
    private void DrainCommands()
    {
        while (inbox.TryDequeue(out var cmd))
        {
            switch (cmd.Type)
            {
                case RoomCommandType.PlayerJoin:
                    // TODO: 加入玩家、分配 Pos（Pos 是本房间内递增，不是全局）
                    //       把 PlayerId -> RoomPlayer 的映射存在本房间的字典里
                    break;

                case RoomCommandType.PlayerLeave:
                    // TODO: 移除玩家。人数不足时要决定是否结束本局
                    break;

                case RoomCommandType.PlayerReady:
                    // TODO: 标记 IsReady；所有人都 ready 后切到战斗态（可以广播 BattleStart）
                    break;

                case RoomCommandType.PlayerInput:
                    // TODO: 先按 PlayerId 找到本房间的 RoomPlayer 拿到 Pos，
                    //       再 inputs.TrySet(cmd.Frame, pos, cmd.Input);
                    //       注意：一定要检查 TrySet 的返回值（越界会被拒绝）
                    break;

                case RoomCommandType.Stop:
                    return;
            }
        }
    }

    /// <summary>
    /// 推进一帧。返回 false 表示房间应该结束。
    /// TODO: 由你实现，大致步骤：
    ///   1. AuthoritativeFrame++
    ///   2. 判断是否所有人都 ready，没齐就返回 true 空转（或直接返回 false 结束）
    ///   3. 组装本帧输入：payload[pos] = inputs.GetInput(frame, pos)   ← 补帧兜底在这里生效
    ///   4. 广播给所有 ready 且已连接的玩家：player.Peer!.Send(BattleMsgFrame, payload)
    ///   5. inputs.Commit(frame)
    ///   6. if (frame >= BattleConfig.MaxFrameCount) return false;      ← 到点正常结束，不要抛异常
    /// </summary>
    private bool TryTick()
    {
        // TODO
        return false;
    }
}
