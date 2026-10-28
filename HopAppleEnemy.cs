using Godot;

/// <summary>
/// 地下洞穴里的跳跃苹果怪。
///
/// 和第一关苹果怪（Enemy / AppleEnemy）的区别：
///   · **不会走**，只会一下一下地蹦（没有走路帧）
///   · 发现玩家先蹲下蓄力（有预警），再朝玩家扑出去咬一口
///   · 飞扑用**左右侧视图**：往右扑就用朝右的图，往左就用朝左的
///   · **死亡直接炸开**，不爆炸、不留毒圈
///
/// 帧表（assets/hop_apple_sheet.png，32 帧，每帧 160×160）：
///   0~3    呼吸（朝右）      4~7    呼吸（朝左）
///   8~12   蹦跳（朝右）      13~17  蹦跳（朝左）
///   18~21  飞扑（朝右）      22~25  飞扑（朝左）
///   26~31  死亡炸开
///
/// 挂法：CharacterBody2D（layer=4）+ Sprite2D（hframes=32）+ HurtArea
///      + RayLeft / RayRight（基类硬取，缺了会抛异常）。
/// </summary>
public partial class HopAppleEnemy : Enemy
{
    // ─────────────────────────── 帧表
    [ExportGroup("帧表")]
    [Export] public int IdleRightStart = 0;      // 呼吸朝右：0~3
    [Export] public int IdleLeftStart = 4;       // 呼吸朝左：4~7
    [Export] public int HopRightStart = 8;       // 蹦跳朝右：8~12
    [Export] public int HopLeftStart = 13;       // 蹦跳朝左：13~17
    [Export] public int LungeRightStart = 18;    // 飞扑朝右：18~21
    [Export] public int LungeLeftStart = 22;     // 飞扑朝左：22~25
    [Export] public int DeathStart = 26;         // 死亡炸开：26~31
    [Export] public int DeathEnd = 31;
    [Export] public float IdleAnimInterval = 0.18f;
    [Export] public float DeathAnimInterval = 0.07f;

    // ─────────────────────────── 蹦跳
    [ExportGroup("蹦跳")]
    [Export] public float HopInterval = 0.55f;   // 落地后停多久再跳下一次
    [Export] public float HopSpeed = 190f;       // 跳跃时的水平速度
    [Export] public float HopImpulse = 560f;     // 起跳的向上初速度（越大跳越高）
    [Export] public float LandHoldTime = 0.12f;  // 落地压扁保持多久

    // ─────────────────────────── 侦测与飞扑
    [ExportGroup("侦测与飞扑")]
    [Export] public float DetectRangeX = 300f;   // 水平多近能发现玩家
    [Export] public float DetectRangeY = 140f;   // 垂直高度差容忍
    [Export] public float LungeChargeTime = 0.38f; // 蓄力预警时长——玩家的反应窗口
    [Export] public float LungeSpeed = 560f;     // 扑出去的速度（玩家 300）
    [Export] public float LungeTime = 0.22f;     // 冲出去多久
    [Export] public float RecoverTime = 0.5f;    // 扑完喘气
    [Export] public float LungeCooldown = 1.1f;  // 两次飞扑之间至少隔多久

    private enum St { Idle, Hop, Charge, Lunge, Recover }
    private St _st = St.Idle;

    private float _timer;
    private float _lungeCd;
    private float _landHold;
    private Box _player;
    private Vector2 _lungeDir = Vector2.Right;
    private bool _landedOnce;

    public override void _Ready()
    {
        base._Ready();
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _timer = HopInterval;
        if (_sprite != null)
        {
            _sprite.Hframes = 32;
            _sprite.Frame = IdleRightStart;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_dead)
        {
            base._PhysicsProcess(delta);
            return;
        }

        base._PhysicsProcess(delta);   // 重力、移动、接触伤害都交给父类

        _timer -= dt;
        _lungeCd = Mathf.Max(0f, _lungeCd - dt);
        _landHold = Mathf.Max(0f, _landHold - dt);

        bool onFloor = IsOnFloor();

        switch (_st)
        {
            case St.Idle:
                if (TryStartLunge()) break;
                if (_timer <= 0f) StartHop();
                break;

            case St.Hop:
                if (!onFloor) _landedOnce = true;
                else if (_landedOnce)
                {
                    _landedOnce = false;
                    _st = St.Idle;
                    _timer = HopInterval;
                    _landHold = LandHoldTime;
                    Velocity = new Vector2(0f, Velocity.Y);
                }
                break;

            case St.Charge:
                if (_timer <= 0f)
                {
                    _st = St.Lunge;
                    _timer = LungeTime;
                }
                break;

            case St.Lunge:
                if (_timer <= 0f)
                {
                    _st = St.Recover;
                    _timer = RecoverTime;
                    Velocity = new Vector2(Velocity.X * 0.3f, Velocity.Y);
                }
                break;

            case St.Recover:
                if (_timer <= 0f)
                {
                    _st = St.Idle;
                    _timer = HopInterval;
                }
                break;
        }
    }

    /// <summary>父类每帧用这个覆盖 Velocity.X。</summary>
    protected override float ComputeHorizontalVelocity(float dt)
    {
        switch (_st)
        {
            case St.Hop:   return Direction * HopSpeed;
            case St.Lunge: return _lungeDir.X * LungeSpeed;
            default:       return 0f;      // 蓄力时站定，给玩家看清信号
        }
    }

    private bool TryStartLunge()
    {
        if (_lungeCd > 0f) return false;
        if (_player == null || !IsInstanceValid(_player)) return false;

        float dx = _player.GlobalPosition.X - GlobalPosition.X;
        float dy = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
        if (Mathf.Abs(dx) > DetectRangeX) return false;
        if (dy > DetectRangeY) return false;

        Direction = dx >= 0f ? 1 : -1;
        _lungeDir = new Vector2(Direction, 0f);

        _st = St.Charge;
        _timer = LungeChargeTime;
        _lungeCd = LungeChargeTime + LungeTime + RecoverTime + LungeCooldown;
        Velocity = new Vector2(0f, Velocity.Y);
        return true;
    }

    private void StartHop()
    {
        if (!IsOnFloor())
        {
            _timer = 0.1f;
            return;
        }
        _st = St.Hop;
        _landedOnce = false;
        _landHold = 0f;
        Velocity = new Vector2(Direction * HopSpeed, -HopImpulse);
    }

    // ─────────────────────────── 动画
    protected override void UpdateAnimation(float dt)
    {
        if (_sprite == null) return;

        if (_dead)
        {
            if (_sprite.Frame < DeathStart) { _sprite.Frame = DeathStart; _animTimer = 0f; return; }
            _animTimer += dt;
            if (_animTimer < DeathAnimInterval) return;
            _animTimer = 0f;
            if (_sprite.Frame < DeathEnd) _sprite.Frame++;
            return;
        }

        _sprite.FlipH = false;   // 贴图自带左右两套，不需要镜像

        switch (_st)
        {
            case St.Hop:
                UpdateHopFrame();
                break;

            case St.Charge:
                // 蓄力：前半段后仰，后半段压紧，给玩家一个「要来了」的读条感
                _sprite.Frame = LungeStartFor(0) + (_timer < LungeChargeTime * 0.5f ? 1 : 0);
                break;

            case St.Lunge:
                _sprite.Frame = LungeStartFor(2);   // 拉最长、嘴张最大的那一帧
                break;

            case St.Recover:
                _sprite.Frame = LungeStartFor(3);
                break;

            default:
                if (_landHold > 0f)
                    _sprite.Frame = HopStartFor() + 4;   // 落地压扁
                else
                    PlayIdleBreath(dt);
                break;
        }
    }

    /// <summary>蹦跳帧按物理状态选，不循环播——循环播放会像在扑腾翅膀。</summary>
    private void UpdateHopFrame()
    {
        int start = HopStartFor();
        if (Velocity.Y < -220f)      _sprite.Frame = start + 1;   // 刚起跳：拉长
        else if (Velocity.Y < 0f)    _sprite.Frame = start + 2;   // 接近顶点
        else                         _sprite.Frame = start + 3;   // 下落
    }

    private void PlayIdleBreath(float dt)
    {
        int start = Direction > 0 ? IdleRightStart : IdleLeftStart;
        _animTimer += dt;
        if (_animTimer < IdleAnimInterval) return;
        _animTimer = 0f;

        int f = _sprite.Frame;
        if (f < start || f >= start + 4) f = start;
        else f = start + (f - start + 1) % 4;
        _sprite.Frame = f;
    }

    private int HopStartFor() => Direction > 0 ? HopRightStart : HopLeftStart;

    /// <summary>飞扑帧段按「往哪扑」选：往右用朝右的图，往左用朝左的图。</summary>
    private int LungeStartFor(int offset)
    {
        int start = _lungeDir.X >= 0f ? LungeRightStart : LungeLeftStart;
        return start + offset;
    }

    // ─────────────────────────── 死亡
    protected override void OnDeath()
    {
        base.OnDeath();
        _st = St.Idle;
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null) body.SetDeferred("disabled", true);
        if (_sprite != null) _sprite.Frame = DeathStart;
    }

    /// <summary>重写父类：不压扁不淡出，直接播炸开帧。</summary>
    protected override void UpdateDead(float dt)
    {
        _deadTimer -= dt;
        if (_deadTimer <= 0f) OnDeadFinished();
    }

    public override void Respawn()
    {
        base.Respawn();
        _st = St.Idle;
        _timer = HopInterval;
        _lungeCd = 0f;
        _landedOnce = false;
        _landHold = 0f;
        _animTimer = 0f;
        if (_sprite != null) _sprite.Frame = IdleRightStart;
    }
}
