using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 苹果仙子的**战斗状态机**。
///
/// 之前是 `step % 3` 死循环 1→2→3，玩家打两轮就能背下来，而且没有"停止 / 收招 / 待机"这些概念。
/// 这里改成显式状态机：
///
///   Npc ──开战──► Reposition ──► Idle ──抽技能──► Dive / Stems / Water ──► Recover ──┐
///     ▲              ▲                                                                    │
///     └──中止/战败────┴────────────────────────────────────────────────────────────────────┘
///
/// 技能用**加权随机**抽取，并且带"同一个技能不能连着放"的冷却，所以：
///   · 每轮顺序都不一样，玩家背不下来；
///   · 但也不会连着三次都是藤鞭这种手感很差的连发。
///
/// 权重和冷却都在 AppleFairy 的「⑦ 状态机 / 技能编排」里，可以按手感调。
/// </summary>
public partial class AppleFairy
{
    /// <summary>仙子当前状态。调试时看这个就知道她在干嘛。</summary>
    public enum FairyState
    {
        Npc,         // 未开战，站桩等玩家搭话
        Reposition,  // 飞到一个新的悬浮点（换位）
        Idle,        // 悬浮待机，等着出手
        Dive,        // 技能 1：俯冲撒尘
        Stems,       // 技能 2：藤鞭
        Water,       // 技能 3：水流冲击波
        Recover,     // 收招缓冲（不移动、不攻击）
        Defeated,    // 被打败，走战败演出
        Carrying,    // 抓着玩家往上飞（第四章过场）
    }

    /// <summary>三个技能。</summary>
    public enum FairySkill { Dive, Stems, Water }

    private static readonly FairySkill[] AllSkills =
        { FairySkill.Dive, FairySkill.Stems, FairySkill.Water };

    public FairyState CurrentState => _state;
    public FairySkill LastSkill => _lastSkill;

    private FairyState _state = FairyState.Npc;
    private FairySkill _lastSkill = FairySkill.Dive;

    // 每个技能"已经隔了几次没被抽到"，用来做冷却
    private readonly int[] _sincePick = new int[3];
    private readonly RandomNumberGenerator _rng = new();

    /// <summary>初始化状态机（在 _Ready 里调一次）。</summary>
    private void InitStateMachine()
    {
        _rng.Randomize();
        for (int i = 0; i < _sincePick.Length; i++) _sincePick[i] = 99;   // 开局三个都能抽
        _state = FairyState.Npc;
    }

    // ─────────────────────────────────────── 状态切换

    /// <summary>切状态。所有状态副作用（清标志、切动画）都收在这里，别散到各个技能里。</summary>
    private void SetState(FairyState next)
    {
        if (_state == next) return;

        var prev = _state;
        OnExitState(prev);
        _state = next;
        OnEnterState(next);

        if (LogStates) GD.Print($"[Fairy] {prev} → {next}");
    }

    private void OnExitState(FairyState s)
    {
        // 离开技能状态时，把"正在俯冲 / 正在施法"的标记收掉，别让下一次待机还在漂
        if (s == FairyState.Dive) _diving = false;
        if (s == FairyState.Water) _casting = false;
    }

    private void OnEnterState(FairyState s)
    {
        switch (s)
        {
            case FairyState.Idle:
                _diving = false;
                _casting = false;
                if (_battleOn && !_defeated)
                    SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);
                break;

            case FairyState.Dive:
                _diving = true;      // 俯冲期间放开"离玩家最远"的限制
                _casting = false;
                break;

            case FairyState.Stems:
                _diving = false;
                _casting = false;
                break;

            case FairyState.Water:
                _diving = false;
                _casting = true;     // 施法期间冻住悬浮浮动，她原地不动
                break;

            case FairyState.Defeated:
                _pantT = 0f;
                _diving = false;
                _casting = false;
                break;

            case FairyState.Recover:
            case FairyState.Npc:
                _diving = false;
                _casting = false;
                break;
        }
    }

    /// <summary>每物理帧按状态做表现（目前只有待机时的上下浮动）。</summary>
    private void UpdateState(float dt)
    {
        if (_carrying) return;

        // 战败喘气：最需要"夸张"的状态，除了 4 帧贴图再叠一层程序化起伏
        if (_state == FairyState.Defeated)
        {
            UpdatePanting(dt);
            return;
        }

        if (_defeated) return;

        switch (_state)
        {
            case FairyState.Idle:
            case FairyState.Reposition:
                _bobT += dt;
                GlobalPosition += new Vector2(0f, Mathf.Sin(_bobT * 2.4f) * HoverBobAmp * dt);
                GlobalPosition = ClampToArena(GlobalPosition);
                break;
        }
    }

    // ─────────────────────────────────────── 主循环（状态驱动）

    /// <summary>
    /// 战斗主循环。每一轮都是：换位 → 待机 → 抽一个技能 → 收招，然后再来一轮。
    /// 技能顺序由 <see cref="PickNextSkill"/> 加权随机决定，不再是固定 1→2→3。
    /// </summary>
    private async void BattleLoop()
    {
        while (IsInstanceValid(this) && _battleOn && !_defeated)
        {
            // ① 换个位置，避免一直杵在同一个点
            SetState(FairyState.Reposition);
            await RepositionToHoverPoint();
            if (!_battleOn || _defeated) break;

            // ② 悬浮待机，给玩家喘口气 / 看清她在哪
            SetState(FairyState.Idle);
            await Wait(IdleTime);
            if (!_battleOn || _defeated) break;

            // ③ 随机挑技能
            var skill = PickNextSkill();
            switch (skill)
            {
                case FairySkill.Dive:
                    SetState(FairyState.Dive);
                    await SkillDive();
                    break;

                case FairySkill.Stems:
                    SetState(FairyState.Stems);
                    await SkillStems();
                    break;

                default:
                    SetState(FairyState.Water);
                    await SkillWater();
                    break;
            }

            if (!_battleOn || _defeated) break;

            // ④ 收招
            SetState(FairyState.Recover);
            await Wait(RecoverTime);
        }

        if (!_defeated && !_battleOn) SetState(FairyState.Npc);
    }

    /// <summary>飞到玩家附近的一个随机悬浮点（左右随机、高度随机），并转向玩家。</summary>
    private async Task RepositionToHoverPoint()
    {
        if (!PlayerAlive()) return;

        float side = _rng.Randf() < 0.5f ? -1f : 1f;
        float x = _player.GlobalPosition.X + side * _rng.RandfRange(240f, 400f);
        x = Mathf.Clamp(x, ArenaRect.Position.X + 80f, ArenaRect.Position.X + ArenaRect.Size.X - 80f);

        // 高度不能随便取：她身体是以节点为中心、上下各 70px，悬浮太高就整只都在
        // 玩家跳起来的拳头上方，表现成"永远砍不到"。这里限制在一个够得着的区间里。
        float lo = Mathf.Min(HoverLowY, HoverHighY);
        float hi = Mathf.Max(HoverLowY, HoverHighY);
        float y = _rng.RandfRange(lo, hi);
        y = Mathf.Clamp(y, ArenaRect.Position.Y, ArenaRect.Position.Y + ArenaRect.Size.Y);

        // 有分身时，别挑到和它几乎同一个点，否则两个建模会叠在一起（下面的推开逻辑只是兜底）
        float ox = OtherX();
        if (!float.IsNaN(ox) && Mathf.Abs(x - ox) < CloneMinSeparation)
        {
            float away = Mathf.Abs(x - ox) < 1f ? side : Mathf.Sign(x - ox);
            x = ox + away * (CloneMinSeparation + 40f);
            x = Mathf.Clamp(x, ArenaRect.Position.X + 80f, ArenaRect.Position.X + ArenaRect.Size.X - 80f);
        }

        await FlyTo(new Vector2(x, y), FloatSpeed);
        if (!PlayerAlive()) return;
        FaceTo(_player.GlobalPosition.X);
    }

    // ─────────────────────────────────────── 技能抽取

    /// <summary>
    /// 加权随机抽下一个技能。
    ///
    /// 规则：
    ///   · 权重 <= 0 的技能直接不参与（想禁用某个技能把权重调 0 就行）；
    ///   · 刚放过的技能要隔 <see cref="SkillCooldown"/> 轮才能再抽到（默认 1 = 不连着放）；
    ///   · 如果全被冷却挡掉（只可能出现在权重全设 0 的极端情况），退回上一次的技能。
    /// </summary>
    private FairySkill PickNextSkill()
    {
        var candidates = new List<FairySkill>(3);
        float total = 0f;

        foreach (var s in AllSkills)
        {
            float w = WeightOf(s);
            if (w <= 0f) continue;
            if (SkillCooldown > 0 && _sincePick[(int)s] < SkillCooldown) continue;
            candidates.Add(s);
            total += w;
        }

        FairySkill pick;
        if (candidates.Count == 0)
        {
            pick = _lastSkill;                 // 兜底：别死循环
        }
        else
        {
            float roll = _rng.RandfRange(0f, total);
            pick = candidates[candidates.Count - 1];
            float acc = 0f;
            foreach (var s in candidates)
            {
                acc += WeightOf(s);
                if (roll <= acc) { pick = s; break; }
            }
        }

        // 更新冷却计数：所有技能 +1，被抽中的归零
        for (int i = 0; i < _sincePick.Length; i++) _sincePick[i]++;
        _sincePick[(int)pick] = 0;
        _lastSkill = pick;

        return pick;
    }

    private float WeightOf(FairySkill s)
    {
        switch (s)
        {
            case FairySkill.Dive: return DiveWeight;
            case FairySkill.Stems: return StemWeight;
            default: return WaterWeight;
        }
    }
}
