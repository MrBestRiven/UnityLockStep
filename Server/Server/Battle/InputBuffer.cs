namespace LockStepServer;

/// <summary>
/// 房间内的输入缓冲（按帧 × 玩家槽位组织的环形数组）。
///
/// 【这是整个服务器最核心的一个类】
/// 帧同步的前提是：每一帧、每个玩家都必须有一个确定的输入值。
/// 但网络会丢包会抖动，客户端某一帧的输入可能根本没到。
///
/// 旧项目的做法是"没收到就是 0"，于是远端玩家会周期性抖回初始朝向 —— 这是错的。
/// 正确做法是补帧兜底：没收到就沿用该玩家上一次的有效输入。
/// 这段逻辑就封装在 <see cref="GetInput"/> 里。
///
/// 本类只被房间自己的线程访问，所以不需要任何锁。
/// </summary>
public sealed class InputBuffer
{
    private readonly int playerCount;
    private readonly int capacity;

    /// <summary>环形输入数组，布局 [frame % capacity, pos]</summary>
    private readonly byte[] inputs;

    /// <summary>该帧该玩家是否真的上报过</summary>
    private readonly bool[] hasInput;

    /// <summary>每个玩家最近一次的有效输入，用于补帧兜底</summary>
    private readonly byte[] lastInput;

    /// <summary>可写窗口起点（已提交帧 + 1）</summary>
    private int windowStart;

    public InputBuffer(int playerCount, int capacity = BattleConfig.InputWindowFrames)
    {
        if (playerCount <= 0) throw new ArgumentOutOfRangeException(nameof(playerCount));

        this.playerCount = playerCount;
        this.capacity = Math.Max(2, capacity);
        inputs = new byte[this.capacity * this.playerCount];
        hasInput = new bool[this.capacity * this.playerCount];
        lastInput = new byte[this.playerCount];
        windowStart = 0;
    }

    public int PlayerCount => playerCount;

    public int WindowStart => windowStart;

    public int WindowEnd => windowStart + capacity; // 不含

    /// <summary>帧号是否落在可写窗口内。所有外部输入的入口都必须先过这一关</summary>
    public bool IsInWindow(int frame) => frame >= windowStart && frame < windowStart + capacity;

    /// <summary>
    /// 写入某帧某玩家的输入。
    /// 返回 false 表示帧号越界（客户端造假或严重落后），调用方应记日志而不是让异常冒泡。
    /// </summary>
    public bool TrySet(int frame, int pos, byte input)
    {
        if (pos < 0 || pos >= playerCount) return false;

        if (!IsInWindow(frame))
        {
            // 旧项目在这里直接索引数组导致崩溃。这里静默拒绝 + 日志。
            Log.W($"InputBuffer.TrySet out of window frame:{frame} pos:{pos} window:[{windowStart},{WindowEnd})");
            return false;
        }

        var index = (frame % capacity) * playerCount + pos;
        inputs[index] = input;
        hasInput[index] = true;
        return true;
    }

    /// <summary>
    /// 取某帧某玩家的输入。
    /// 没上报过 -> 返回该玩家上一次的有效输入（补帧兜底），这就是"抖动消失"的关键。
    /// </summary>
    public byte GetInput(int frame, int pos)
    {
        if (pos < 0 || pos >= playerCount) return 0;
        if (!IsInWindow(frame)) return lastInput[pos];

        var index = (frame % capacity) * playerCount + pos;
        return hasInput[index] ? inputs[index] : lastInput[pos];
    }

    /// <summary>该帧该玩家是否真的上报过（用于统计延迟/掉帧，广播时一般不需要）</summary>
    public bool HasInput(int frame, int pos)
    {
        if (pos < 0 || pos >= playerCount || !IsInWindow(frame)) return false;
        return hasInput[(frame % capacity) * playerCount + pos];
    }

    /// <summary>
    /// 帧推进后调用：把该帧的有效输入固化进 lastInput，并清空槽位供将来复用。
    /// 必须在广播完该帧之后调用。
    /// </summary>
    public void Commit(int frame)
    {
        var index = (frame % capacity) * playerCount;

        for (var pos = 0; pos < playerCount; pos++)
        {
            if (hasInput[index + pos]) lastInput[pos] = inputs[index + pos];
        }

        // 清掉这个槽位，capacity 帧之后它会作为新帧被重新使用
        Array.Clear(hasInput, index, playerCount);
        Array.Clear(inputs, index, playerCount);

        windowStart = frame + 1;
    }

    /// <summary>把窗口对齐到指定帧（开局/重连时用）</summary>
    public void Reset(int frame)
    {
        Array.Clear(inputs);
        Array.Clear(hasInput);
        Array.Clear(lastInput);
        windowStart = frame;
    }
}
