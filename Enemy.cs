using Godot;

/// <summary>
/// 左右巡逻的苹果怪：走到平台边缘或撞到墙就掉头。
/// 边缘靠两根 RayCast2D 探针往下探，前面没地面就转身。
/// </summary>
public partial class Enemy : CharacterBody2D
{
    // ── 扩展点 ──────────────────────────────────────────────
    // 死亡瞬间发出这个信号，独立组件（掉落、爆炸…）可以监听，不用改这个脚本。
    [Signal] public delegate void DiedEventHandler();

    /// <summary>死亡瞬间的钩子，子类重写它做特殊处理（比信号更适合「改行为」）。</summary>
    protected virtual void OnDeath()
    {
        // 有循环帧限制时，死亡瞬间跳到「碎裂」段的第一帧
        if (_sprite == null) return;

        int start = DeathFrameStart >= 0 ? DeathFrameStart
                  : (LoopFrames > 0 && LoopFrames < _frameCount ? LoopFrames : -1);
        if (start >= 0) _sprite.Frame = start;
    }

    // ────────────────────────────────────────────────────────

    [Export] public float Speed = 70f;          // 巡逻速度
    [Export] public float Gravity = 1200f;      // 和玩家保持一致
    [Export] public int Direction = -1;         // 1 = 往右，-1 = 往左

    [ExportGroup("巡逻区间")]
    [Export] public float PatrolRange = 150f;    // 离出生点多远就折返（像素，左右各这么多）
    [Export] public float AnimInterval = 0.12f; // 每帧动画间隔
    [Export] public int LoopFrames = 0;         // 平时循环的帧数（0=全部）
    [Export] public int DeathFrameStart = -1;   // 死亡动画从哪帧开始（-1 = 用 LoopFrames）

    // ── 战斗数值：每只怪单独调，不用改代码 ──────────────────
    [ExportGroup("战斗数值")]
    [Export] public int Damage = 10;            // 咬到 / 碰到玩家扣多少血
    [Export] public int MaxHp = 50;             // 血量上限（木棒 20 伤害 → 三下打死）
    [Export] public float KnockbackUpRatio = 0.32f; // 击退的垂直分量比例，越大越往上飞
    [Export] public bool ImmuneToKnockback = false; // 勾上后受击不会被推开（Boss 用）

    [ExportGroup("接触判定")]
    // 玩家离开判定区后，还继续判定多久（秒）。
    // 高速穿过、或者被子弹击退的瞬间，"进入"和"离开"可能落在同一帧，
    // 只靠 _touchingPlayer 会漏掉这次接触。
    [Export] public float TouchGraceTime = 0.15f;
    // 超过这个距离才算"真的离开了"（判定距离 60 的两倍，留足余量）
    [Export] public float LeaveDistance = 120f;

    /// <summary>当前血量（只读，给 UI 用）。</summary>
    public int Hp => _hp;
    protected Sprite2D _sprite;
    protected RayCast2D _rayLeft;
    protected RayCast2D _rayRight;
    protected float _animTimer;
    private int _frameCount = 4;   // 从贴图读，做新怪物时不用改代码

    private Area2D _hurtArea;
    private Box _touchingPlayer;   // 当前压在 HurtArea 里的玩家，用事件维护，避免每帧分配数组
    private float _touchGrace;     // 离开后的宽限时间：防止"刚碰到就被推开"导致漏判
    private bool _leftSinceEnter;  // 是否已经"真的离开"过（超过宽限时间）

    private int _hp;
    private float _hitInvuln;
    private Vector2 _knockVel;
    private float _knockTimer;
    private float _knockRecover;   // 击退刚结束的恢复期，期间不判断掉头
    private TextureProgressBar _bar;
    protected bool _dead;
    protected Vector2 _spawnPos;   // 出生位置，重生时回去（子类要用它算巡逻边界）
    protected float _deadTimer;
    // 死亡动画时长，同时也是尸体停留时间。
    // 想让「死后 N 秒才发生的事」看起来连贯，就把它设成 N。
    [Export] public float DeadTime = 0.35f;


    public override void _Ready()
    {
        _spawnPos = GlobalPosition;
        _sprite = GetNode<Sprite2D>("Sprite2D");
        // 动画帧数从贴图读，新怪物贴图多少帧都行
        if (_sprite.Hframes > 0) _frameCount = _sprite.Hframes;
        _rayLeft = GetNode<RayCast2D>("RayLeft");
        _rayRight = GetNode<RayCast2D>("RayRight");
        _hurtArea = GetNode<Area2D>("HurtArea");
        _hurtArea.BodyEntered += OnHurtAreaEntered;
        _hurtArea.BodyExited += OnHurtAreaExited;
        _hp = MaxHp;
        _bar = GetNodeOrNull<TextureProgressBar>("HpBar");
        UpdateBar();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_dead)
        {
            UpdateAnimation(dt);   // 子类可以在这里播死亡帧
            UpdateDead(dt);
            return;
        }

        // 受击闪红 + 短暂无敌，避免同一次挥击连续扣血
        _hitInvuln = Mathf.Max(0f, _hitInvuln - dt);
        _sprite.Modulate = _hitInvuln > 0f ? new Color(1f, 0.45f, 0.45f) : Colors.White;

        // 0) 被击退中：暂时不巡逻，只吃击退速度
        if (_knockTimer > 0f)
        {
            _knockTimer -= dt;
            Velocity = _knockVel;
            _knockVel = _knockVel.MoveToward(Vector2.Zero, 1000f * dt);
            MoveAndSlide();
            return;
        }

        // 0.5) 击退刚结束：沿原方向继续走，不判断掉头
        if (_knockRecover > 0f)
        {
            _knockRecover -= dt;
            if (!IsOnFloor()) Velocity += new Vector2(0f, Gravity * dt);
            Velocity = new Vector2(Direction * Speed, Velocity.Y);
            MoveAndSlide();

            UpdateAnimation(dt);
            return;
        }

        // 1) 重力：落地后就不再加
        if (!IsOnFloor())
        {
            Velocity += new Vector2(0, Gravity * dt);
        }

        // 2) 水平速度交给虚方法算（子类可重写做追踪）
        Velocity = new Vector2(ComputeHorizontalVelocity(dt), Velocity.Y);

        // 4) 真正移动
        MoveAndSlide();

        // 5) 4 帧弹跳动画
        UpdateAnimation(dt);
        // 玩家碰到就扣血。用事件维护的引用，不每帧查重叠列表（那是 GC 卡顿的来源）
        _touchGrace = Mathf.Max(0f, _touchGrace - dt);
        if (_touchGrace > 0f && _touchingPlayer != null && IsInstanceValid(_touchingPlayer))
        {
            _touchingPlayer.TakeDamage(Damage, GlobalPosition);
        }
        else if (_touchGrace <= 0f && _touchingPlayer != null)
        {
            // 宽限耗尽，用「距离」判断是不是真的离开了。
            // 不能用 HasOverlappingBodies()：它在物理帧里返回的是上一帧状态，
            // 玩家明明还贴着却返回 false，会误判成"离开"并清掉免疫。
            float dist = GlobalPosition.DistanceTo(_touchingPlayer.GlobalPosition);
            if (dist > LeaveDistance)
            {
                _touchingPlayer = null;
                _leftSinceEnter = true;         // 真的离开了
            }
            else
            {
                _touchGrace = TouchGraceTime;   // 还在附近，续上宽限
            }
        }
    }

    /// <summary>
    /// 推进动画帧。默认是「整张贴图所有帧循环播放」。
    /// 子类重写它可以按状态播不同的帧段（比如走/站/死）。
    /// </summary>
    protected virtual void UpdateAnimation(float dt)
    {
        _animTimer += dt;
        if (_animTimer < AnimInterval) return;

        _animTimer = 0f;

        if (_dead)
        {
            // 死亡：往后播到最后一帧（碎裂），不循环
            if (_sprite.Frame < _frameCount - 1) _sprite.Frame++;
            return;
        }

        int loop = LoopFrames > 0 ? LoopFrames : _frameCount;
        _sprite.Frame = (_sprite.Frame + 1) % loop;
    }

    /// <summary>
    /// 计算这一帧的水平速度。默认是「按 Direction 巡逻，撞墙或到悬崖边掉头」。
    /// 子类重写它就能改成追踪、逃跑、原地不动等行为，不用碰其余逻辑。
    /// </summary>
    protected virtual float ComputeHorizontalVelocity(float dt)
    {
        // 前方探针：往右走用右探针，往左走用左探针
        RayCast2D ray = Direction > 0 ? _rayRight : _rayLeft;

        // 撞墙 或者 探不到地面 → 掉头
        if (IsOnWall() || !ray.IsColliding())
        {
            Direction = -Direction;
        }

        // 超出出生点左右 PatrolRange → 强制往回走
        float offset = GlobalPosition.X - _spawnPos.X;
        if (offset > PatrolRange) Direction = -1;
        else if (offset < -PatrolRange) Direction = 1;

        return Direction * Speed;
    }

    /// <summary>前方能不能走（不是墙、也不是悬崖）。子类追踪时用它避免跳崖。</summary>
    protected bool CanWalkToward(float dir)
    {
        if (dir == 0f) return false;
        RayCast2D ray = dir > 0 ? _rayRight : _rayLeft;
        return !IsOnWall() && ray.IsColliding();
    }

    private void OnHurtAreaEntered(Node2D body)
    {
        if (body is Box b)
        {
            _touchingPlayer = b;
            _touchGrace = TouchGraceTime;

            // 只有"真的离开过"（超过 TouchGraceTime 才回来）才算新接触。
            // 贴着边缘抖动时 _touchGrace 一直在续，_leftSinceEnter 保持 false，
            // 免疫照常生效，不会变成连续掉血。
            if (_leftSinceEnter)
            {
                b.ResetHurtCooldown();
                _leftSinceEnter = false;
            }
        }
    }

    private void OnHurtAreaExited(Node2D body)
    {
        // 不清引用，只续一段宽限。否则"刚碰到就被子弹推开"的那次接触会整个丢掉。
        if (body is Box b && _touchingPlayer == b) _touchGrace = TouchGraceTime;
    }

    /// <summary>被武器打中。返回 true 表示这次真的扣到血了。</summary>
    public bool TryTakeHit(int dmg, Vector2 from, float knockback, float upRatio = -1f, bool ignoreInvuln = false)
    {
        if (_dead) return false;
        // 受击无敌用来防止「一次挥击打多段」。
        // 空手出拳是单帧判定，天然只命中一次，所以可以传 ignoreInvuln = true 让连击打得出来。
        if (!ignoreInvuln && _hitInvuln > 0f) return false;

        _hp = Mathf.Max(0, _hp - dmg);
        _hitInvuln = 0.25f;
        UpdateBar();

        if (_hp <= 0)
        {
            Die();      // 死亡音在 Die() 里放，避免和受击音叠在一起
            return true;
        }

        AudioManager.Instance?.PlaySfx("enemy_hurt", 0.8f, 1f + GD.Randf() * 0.1f);

        // 被打退：从受击方向反着弹开（Boss 免疫击退，否则会被硬控到死）
        if (!ImmuneToKnockback)
        {
            float dirX = GlobalPosition.X < from.X ? -1f : 1f;
            float up = upRatio >= 0f ? upRatio : KnockbackUpRatio;
            _knockVel = new Vector2(dirX * knockback, -knockback * up);
            _knockTimer = 0.18f;
            _knockRecover = 0.45f;   // 击退结束后 0.45 秒内保持原方向，不被推歪就掉头
        }

        return true;
    }

    private void UpdateBar()
    {
        if (_bar == null) return;
        _bar.MaxValue = MaxHp;
        _bar.Value = _hp;
    }

    /// <summary>被击倒后：一边压扁一边淡出，播完销毁自己。</summary>
    protected virtual void UpdateDead(float dt)
    {
        _deadTimer -= dt;
        float k = 1f - Mathf.Max(0f, _deadTimer) / DeadTime;
        Scale = new Vector2(1f + 0.45f * k, 1f - 0.65f * k);
        Modulate = new Color(1f, 1f, 1f, 1f - k);

        if (_deadTimer <= 0f) OnDeadFinished();
    }

    /// <summary>死亡动画播完时调用。默认隐藏起来等重生（不销毁，否则没法复活）。</summary>
    protected virtual void OnDeadFinished()
    {
        Visible = false;
        if (_hurtArea != null) _hurtArea.Monitoring = false;
        if (_bar != null) _bar.Visible = false;

        // 本体碰撞体也要关掉：否则子弹/武器会打到这具"隐形的尸体"上白白消失
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null) body.SetDeferred("disabled", true);

        SetPhysicsProcess(false);
    }

    /// <summary>玩家复活时把这只怪恢复原状：回到出生点、满血、重新显示。</summary>
    public virtual void Respawn()
    {
        if (!IsInstanceValid(this)) return;

        GlobalPosition = _spawnPos;
        Velocity = Vector2.Zero;
        _hp = MaxHp;
        _dead = false;
        _deadTimer = 0f;
        _hitInvuln = 0f;
        _knockTimer = 0f;
        _knockRecover = 0f;
        _animTimer = 0f;

        Scale = Vector2.One;
        Modulate = Colors.White;
        Visible = true;

        if (_sprite != null)
        {
            _sprite.Visible = true;
            _sprite.Frame = 0;
            _sprite.Modulate = Colors.White;
        }
        if (_bar != null) _bar.Visible = true;
        if (_hurtArea != null) _hurtArea.Monitoring = true;

        // 恢复本体碰撞体
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null) body.SetDeferred("disabled", false);

        SetPhysicsProcess(true);
        UpdateBar();
    }
    /// <summary>立刻杀死（掉坑、剧情杀等外部调用）。</summary>
    public void Kill()
    {
        Die();
    }

    /// <summary>统一的死亡入口：切状态、调钩子、发信号，只走这一条路。</summary>
    private void Die()
    {
        if (_dead) return;              // 已经在死，别重复触发

        _hp = 0;
        _dead = true;
        AudioManager.Instance?.PlaySfx("enemy_hurt", 0.9f, 0.95f + GD.Randf() * 0.1f);
        _deadTimer = DeadTime;
        if (_bar != null) _bar.Visible = false;
        UpdateBar();

        OnDeath();                       // 给子类重写
        EmitSignal(SignalName.Died);     // 给独立组件监听
    }
}
