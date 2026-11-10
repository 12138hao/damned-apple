using Godot;

/// <summary>
/// 苹果仙子的**半血分身**机制。
///
/// 规则（老大定的）：
///   · 血量掉到一半 → 分裂出第二个个体；
///   · 两个个体**共用同一条血条**：打谁都是在扣同一个血池；
///   · 两个建模**不能重合**，会自动互相推开；
///   · 各自跑自己的状态机，技能独立随机。
///
/// 实现思路（为什么要这么绕）：
///   玩家出拳的判定写死了 `if (collider is Enemy)`，所以分身必须也是 Enemy，才打得到。
///   但 `Enemy.TryTakeHit` 不是虚方法，没法在子类里拦下伤害。
///   于是让分身自己先吃这一下，然后在下一物理帧把「掉了多少血」转给本体，
///   再把自己补满 —— 分身血量只是一个**代理值**，永远不会死；真正的血只有本体那一份。
///
/// 于是：
///   · 打分身 → 转伤给本体 → 血条掉的是本体，天然共血条；
///   · 本体血量归零才走战败，分身只是被清掉。
/// </summary>
public partial class AppleFairy
{
    // ══════════════════════════ 半血分身

    [ExportGroup("⑧ 半血分身")]
    [Export] public bool EnableClone = true;          // 是否启用"半血分裂"
    [Export] public float CloneAtHpPercent = 0.5f;    // 血量降到上限的这个比例时触发（0.5 = 半血）
    [Export] public float CloneSpawnOffsetX = 220f;   // 分身从本体旁边多远冒出来
    [Export] public float CloneMinSeparation = 150f;  // 两个个体至少隔多远（防止建模叠在一起）
    [Export] public Color CloneTint = new Color(0.90f, 0.96f, 1f, 0.92f); // 分身染色（略偏冷 + 微透明，好区分）

    [ExportGroup("⑨ 分身召唤演出")]
    [Export] public float CloneSummonTime = 0.6f;     // 魔法阵展开多久之后，分身才从阵心出现
    [Export] public float CloneSummonCircleSize = 210f; // 法阵直径（世界像素）
    [Export] public Color CloneSummonTint = new Color(0.62f, 0.88f, 1f, 1f); // 法阵颜色（蓝色）

    /// <summary>分身用的"虚拟血量"上限。够大就行，只要一拳打不死，就不会走死亡流程。</summary>
    private const int CloneVirtualHp = 1_000_000;

    private bool _isClone;          // 这个实例是不是分身
    private AppleFairy _master;     // 分身指向本体
    private AppleFairy _clone;      // 本体指向分身
    private bool _cloneSpawned;
    private bool _cloneSummoning;   // 正在播召唤演出
    private Sprite2D _cloneCircle;  // 场上那个召唤法阵

    public bool IsClone => _isClone;

    // ─────────────────────────────────────── 触发 / 生成 / 清除

    /// <summary>本体每物理帧调一次：血量跌破阈值就分裂。</summary>
    private void CheckHalfHpClone()
    {
        if (!EnableClone || _isClone || _cloneSpawned || _defeated || !_battleOn) return;

        float pct = Mathf.Clamp(CloneAtHpPercent, 0.05f, 0.95f);
        if (Hp > MaxHp * pct) return;

        SpawnClone();
    }

    private void SpawnClone()
    {
        if (_cloneSpawned) return;
        _cloneSpawned = true;          // 立刻占位，别让演出期间重复触发

        float dir = _rng.Randf() < 0.5f ? -1f : 1f;
        Vector2 spawn = ClampToArena(GlobalPosition + new Vector2(dir * CloneSpawnOffsetX, 0f));

        SummonClone(spawn);
    }

    /// <summary>
    /// 召唤演出：先在落点展开一个**蓝色魔法阵**（旋转 + 由小变大 + 淡入），
    /// 阵成形之后分身才从阵心淡入，法阵随即炸开淡出。
    ///
    /// 不直接凭空冒出一个人，是为了让玩家一眼看清"她分了一个出来"，而不是莫名其妙多了一只。
    /// </summary>
    private async void SummonClone(Vector2 spawn)
    {
        _cloneSummoning = true;

        var circle = new Sprite2D
        {
            Name = "CloneSummonCircle",
            Texture = _texCircle,
            ZIndex = 3,
            Modulate = new Color(CloneSummonTint.R, CloneSummonTint.G, CloneSummonTint.B, 0f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        GetParent().AddChild(circle);
        circle.GlobalPosition = spawn;
        _cloneCircle = circle;

        float baseSize = CloneSummonCircleSize / Mathf.Max(1f, _texCircle.GetHeight());

        // 一阶段：法阵展开（转起来、由小变大、淡入）
        float t = 0f;
        while (t < CloneSummonTime && IsInstanceValid(this) && IsInstanceValid(circle) && _battleOn)
        {
            float dt = (float)GetPhysicsProcessDeltaTime();
            t += dt;
            float k = Mathf.Clamp(t / Mathf.Max(0.01f, CloneSummonTime), 0f, 1f);

            circle.Rotation += 2.4f * dt;
            circle.Modulate = new Color(CloneSummonTint.R, CloneSummonTint.G, CloneSummonTint.B, k * 0.95f);
            circle.Scale = Vector2.One * baseSize * (0.25f + 0.75f * k) * (1f + 0.05f * Mathf.Sin(t * 26f));
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        if (!IsInstanceValid(circle)) { _cloneSummoning = false; return; }
        if (_defeated || !_battleOn || !IsInstanceValid(this))
        {
            circle.QueueFree();
            _cloneCircle = null;
            _cloneSummoning = false;
            return;
        }

        // 二阶段：阵心起爆，分身出现
        CreateClone(spawn);

        var tw = circle.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(circle, "scale", Vector2.One * baseSize * 1.9f, 0.32f);
        tw.TweenProperty(circle, "modulate:a", 0f, 0.32f);

        // 用计时器等，而不是等 Tween 信号：万一节点被清掉，Tween 永远不会 finished，会卡住协程
        await Wait(0.34f);
        if (IsInstanceValid(circle)) circle.QueueFree();
        if (_cloneCircle == circle) _cloneCircle = null;

        _cloneSummoning = false;
    }

    /// <summary>真正把分身节点造出来（带淡入）。</summary>
    private void CreateClone(Vector2 spawn)
    {
        var c = new AppleFairy
        {
            Name = "AppleFairyClone",
            _isClone = true,
            _master = this,
            HomePos = spawn,          // 必须在 AddChild 之前给，_Ready 会用它定位
            MaxHp = CloneVirtualHp,   // 虚拟血量，一拳打不死就好
        };

        GetParent().AddChild(c);      // 这行会触发它的 _Ready（里面会 CallDeferred 开战）
        _clone = c;

        // 出场：从透明淡入到 CloneTint，顺便稍微放大一点
        c.Modulate = new Color(CloneTint.R, CloneTint.G, CloneTint.B, 0f);
        var tw = c.CreateTween();
        tw.TweenProperty(c, "modulate:a", CloneTint.A, 0.28f);
    }

    /// <summary>把分身清掉（本体战败 / 玩家挑战失败 / 重开时都要调）。</summary>
    private void DespawnClone()
    {
        if (_clone != null && IsInstanceValid(_clone))
        {
            _clone._battleOn = false;
            _clone._defeated = true;
            _clone.QueueFree();
        }
        _clone = null;

        // 召唤演出还没播完就被打断：把法阵一起收掉，否则它会孤零零留在场上
        if (_cloneCircle != null && IsInstanceValid(_cloneCircle)) _cloneCircle.QueueFree();
        _cloneCircle = null;
        _cloneSummoning = false;

        _cloneSpawned = false;
    }

    /// <summary>分身自己的开战入口：不走对话、不发 BattleStarted（那是本体的活）。</summary>
    private void BeginCloneBattle()
    {
        if (!IsInstanceValid(this)) return;

        _battleOn = true;
        _defeated = false;
        Invulnerable = false;
        Damage = TouchDamage;

        _lastHp = Hp;
        SetState(FairyState.Idle);
        SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);
        BattleLoop();
    }

    // ─────────────────────────────────────── 共血条

    /// <summary>分身每物理帧调一次：把这一帧掉的血转给本体，然后把自己补满。</summary>
    private void SyncCloneHp()
    {
        if (_lastHp < 0) _lastHp = Hp;

        int lost = _lastHp - Hp;
        if (lost > 0)
        {
            _flash = 0.12f;
            if (_master != null && IsInstanceValid(_master))
                _master.DrainSharedHp(lost);

            if (Hp < MaxHp) SetHp(MaxHp);   // 补满，保证分身永远打不死
        }

        _lastHp = Hp;
    }

    /// <summary>本体收伤害的入口（由分身转交）。走的是"直接扣血"，绕开受击无敌。</summary>
    public void DrainSharedHp(int dmg)
    {
        if (_defeated || dmg <= 0) return;

        _flash = 0.12f;
        DrainHp(dmg);
        _lastHp = Hp;
    }

    // ─────────────────────────────────────── 防重合

    /// <summary>另一个个体的 X 坐标；没有就返回 NaN。</summary>
    private float OtherX()
    {
        var o = _isClone ? _master : _clone;
        if (o == null || !IsInstanceValid(o)) return float.NaN;
        return o.GlobalPosition.X;
    }

    /// <summary>
    /// 两个个体靠太近时互相推开。
    ///
    /// 只在"贴得太近"时生效，而且是按帧一点点推（不是瞬间弹开），
    /// 所以不会和飞行 / 俯冲打架，看起来像两个人自然地互相避让。
    /// </summary>
    private void KeepCloneSeparated(float dt)
    {
        if (_isClone || _clone == null || !IsInstanceValid(_clone)) return;

        float dx = _clone.GlobalPosition.X - GlobalPosition.X;
        float sep = Mathf.Max(1f, CloneMinSeparation);
        if (Mathf.Abs(dx) >= sep) return;

        float dir = Mathf.Abs(dx) < 0.5f
            ? (_rng.Randf() < 0.5f ? -1f : 1f)     // 完全重叠时随便挑一边
            : Mathf.Sign(dx);

        float need = (sep - Mathf.Abs(dx)) * 0.5f;
        float step = Mathf.Min(need, 300f * dt);

        GlobalPosition = ClampToArena(GlobalPosition - new Vector2(dir * step, 0f));
        _clone.GlobalPosition = _clone.ClampToArena(_clone.GlobalPosition + new Vector2(dir * step, 0f));
    }
}
