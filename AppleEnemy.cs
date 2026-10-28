using Godot;

/// <summary>
/// 会追踪玩家的苹果怪。
///
/// 贴图 16 帧，按用途分段（0 起算）：
///   0~3   向左走
///   4~7   向右走
///   8~10  站立呼吸
///   11~15 死后爆炸
///
/// 继承 Enemy，只重写两件事：
///   1. ComputeHorizontalVelocity —— 怎么走（追踪 / 悬崖前停下）
///   2. UpdateAnimation           —— 播哪一段帧
/// 死亡、受击、击退、掉血、血条全部沿用父类。
/// </summary>
public partial class AppleEnemy : Enemy
{
    [Export] public float DetectRange = 600f;   // 多远开始追（像素）
    // 追踪时可以比 PatrolRange 多跑这么远；超过就「脱战」，一路走回巡逻区。
    // 脱战期间无视玩家，回到 PatrolRange 内再冷静 ReturnCooldown 秒才重新追，
    // 这样边界处不会出现「想追又被拉回」的每帧抖动。
    [Export] public float ChaseOverrun = 100f;
    [Export] public float ReturnCooldown = 1.5f;   // 脱战回位后冷静多久才重新追

    // 帧段（0 起算）
    // 帧段划分（不同贴图切法不同，所以做成可配）
    [Export] public int WalkRightStart = 0;   // 往右走：Start ~ Start+3
    [Export] public int WalkLeftStart = 4;    // 往左走：Start ~ Start+3
    [Export] public int IdleStart = 8;        // 待机/呼吸：Start ~ Start+2
    [Export] public int DeathStart = 11;      // 死亡碎裂：Start ~ DeathEnd
    [Export] public int DeathEnd = 15;
    [Export] public float DeathAnimInterval = 0.05f;   // 碎裂帧率间隔（越小越快；活着用 AnimInterval）

    private Box _player;
    private bool _chasing;
    private bool _lastChasing;      // 上一帧是否在追（检测状态切换）
    private int _lastDir = 1;       // 上一帧朝向
    private bool _returning;      // 脱战中：正在走回巡逻区
    private float _returnCooldown;   // 这一帧有没有在追玩家

    public override void _Ready()
    {
        base._Ready();

        _player = GetTree().Root.FindChild("box", true, false) as Box;
        if (_player == null)
            GD.PushWarning("[AppleEnemy] 没找到玩家 box，会退化成普通巡逻");

        // 开局先站在待机段，否则第一帧会闪一下走路姿势
        if (_sprite != null) _sprite.Frame = IdleStart;
    }

    // ---------------------------------------------------------- 移动

    protected override float ComputeHorizontalVelocity(float dt)
    {
        _chasing = false;
        _returnCooldown = Mathf.Max(0f, _returnCooldown - dt);

        float offset = GlobalPosition.X - _spawnPos.X;
        float absOffset = Mathf.Abs(offset);
        float maxChase = PatrolRange + ChaseOverrun;

        // ① 超过追击上限 → 进入脱战
        if (!_returning && absOffset > maxChase)
            _returning = true;

        // ② 脱战中：无视玩家，一路走回巡逻区
        if (_returning)
        {
            if (absOffset <= PatrolRange)
            {
                _returning = false;
                _returnCooldown = ReturnCooldown;   // 到家了，冷静一会儿
            }
            else
            {
                float back = offset > 0f ? -1f : 1f;
                if (!CanWalkToward(back)) return 0f;
                _chasing = true;
                Direction = back > 0f ? 1 : -1;
                return back * Speed;
            }
        }

        // ③ 脱战冷却中 → 只巡逻，不追
        if (_returnCooldown > 0f)
            return base.ComputeHorizontalVelocity(dt);

        // ④ 没有玩家 → 正常巡逻
        if (_player == null || !IsInstanceValid(_player))
            return base.ComputeHorizontalVelocity(dt);

        float dx = _player.GlobalPosition.X - GlobalPosition.X;

        // 玩家太远 → 站着呼吸
        if (Mathf.Abs(dx) > DetectRange) return 0f;
        if (Mathf.Abs(dx) < 2f) return 0f;

        float want = Mathf.Sign(dx);

        // 前方是悬崖或墙 → 停在边缘等着
        if (!CanWalkToward(want)) return 0f;

        _chasing = true;
        Direction = want > 0f ? 1 : -1;
        return want * Speed;
    }

    // ---------------------------------------------------------- 死亡

    /// <summary>
    /// 死亡瞬间就关掉本体碰撞。
    /// 父类只在「动画播完」的 OnDeadFinished 里关（Enemy.cs:303），
    /// 而这里 UpdateDead 被重写、碎裂动画要播 DeadTime 那么久，
    /// 期间碰撞体一直开着 → 子弹和武器会打在这具还在碎的尸体上白白消失。
    /// </summary>
    protected override void OnDeath()
    {
        base.OnDeath();

        // 用 SetDeferred：死亡是被物理回调触发的，物理帧内不能直接改碰撞状态
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null) body.SetDeferred("disabled", true);
    }

    /// <summary>
    /// 重写父类的死亡表现：不放大压扁、不淡出，保持原样停在那，
    /// 时间到直接消失（父类会一边缩放一边变透明）。
    /// </summary>
    protected override void UpdateDead(float dt)
    {
        _deadTimer -= dt;
        if (_deadTimer <= 0f) OnDeadFinished();   // 隐藏等重生，不销毁
    }

    public override void Respawn()
    {
        base.Respawn();
        _chasing = false;   // 重置追踪状态
    }

    // ---------------------------------------------------------- 动画

    protected override void UpdateAnimation(float dt)
    {
        // 死亡优先：立刻跳到碎裂首帧，不受 AnimInterval 节流限制
        if (_dead)
        {
            if (_sprite.Frame < DeathStart)
            {
                _sprite.Frame = DeathStart;
                _animTimer = 0f;
                return;
            }

            _animTimer += dt;
            if (_animTimer < DeathAnimInterval) return;
            _animTimer = 0f;

            if (_sprite.Frame < DeathEnd) _sprite.Frame++;   // 播到末帧就停住，不循环
            return;
        }

        // 状态/朝向一变就立刻跳到对应帧段的第一帧，
        // 否则要等 AnimInterval（0.12 秒）才更新，会在切换瞬间残留上一段的帧。
        bool switched = _chasing != _lastChasing
                     || (_chasing && Direction != _lastDir);
        if (switched)
        {
            _lastChasing = _chasing;
            _lastDir = Direction;
            _sprite.Frame = _chasing
                ? (Direction > 0 ? WalkRightStart : WalkLeftStart)
                : IdleStart;
            _animTimer = 0f;
            return;
        }

        _animTimer += dt;
        if (_animTimer < AnimInterval) return;
        _animTimer = 0f;

        // 活着：按状态选帧段
        int start;
        int count;

        if (_chasing)
        {
            start = Direction > 0 ? WalkRightStart : WalkLeftStart;
            count = 4;
        }
        else
        {
            start = IdleStart;
            count = 3;
        }

        // 当前帧不在这一段里就跳回段首，否则在段内循环
        int f = _sprite.Frame;
        if (f < start || f >= start + count)
            f = start;
        else
            f = start + (f - start + 1) % count;

        _sprite.Frame = f;
    }
}
