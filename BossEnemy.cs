using Godot;

/// <summary>
/// Boss 首领：站桩，循环放技能（跳跃重砸 / 喷毒液）。
///
/// 死亡后**不立即消失**——覆盖 OnDeadFinished 保持可见，
/// 等 BossArena 把胜利对话播完再 QueueFree。
/// </summary>
public partial class BossEnemy : Enemy
{
    [ExportGroup("状态")]
    [Export] public bool Dormant = true;              // 待机：玩家进区域前不放技能

    [ExportGroup("节奏")]
    [Export] public float IdleTime = 1.2f;            // 两次技能之间歇多久（秒）

    [ExportGroup("阶段")]
    [Export] public Texture2D Phase1Texture;          // boss_jump.png（32 帧跳跃动画）
    [Export] public int Phase1Hframes = 32;
    [Export] public float Phase1Scale = 1.2f;         // 贴图 128×160，放大到 154×192
    [Export] public Texture2D Phase2Texture;          // boss_phase2.png（单帧大脸）
    [Export] public float Phase2Scale = 0.42f;        // 512×512 → 约 215×215
    [Export] public int PhaseChangeHpPercent = 50;    // 血量降到这个百分比切二阶段

    [ExportGroup("跳跃重砸")]
    [Export] public float JumpForce = 780f;           // 起跳初速度
    [Export] public float JumpHorizontalSpeed = 230f; // 朝玩家跳的水平速度
    [Export] public float HangTime = 2f;              // 滞空时长（秒）
    [Export] public float SlamFallSpeed = 1700f;      // 砸下来的下落速度
    [Export] public float AirFrameTime = 0.055f;      // 空中动画每帧间隔
    [Export] public float SlamRadius = 150f;          // 落地砸击的水平半径
    [Export] public float SlamHeight = 130f;          // 落地砸击的垂直容差（从脚底算）
    [Export] public int SlamDamage = 25;              // 砸击伤害

    [ExportGroup("表现")]
    [Export] public float SquashAmount = 0.16f;   // 起跳/落地时拉伸幅度
    [Export] public float SlamShake = 12f;        // 落地震屏强度
    [Export] public float SlamShakeTime = 0.28f;
    [Export] public float SpitWarnTime = 0.45f;   // 喷毒前的预警闪烁时长

    [ExportGroup("毒液")]
    [Export] public int SpitCount = 3;                // 一次喷几滩
    [Export] public float SpitSpread = 110f;          // 每滩之间的水平间隔
    [Export] public float SpitDelay = 0.35f;          // 每滩之间的延迟
    [Export] public float SpitDropY = 40f;            // 毒液生成在脚下多低
    [Export] public float PoisonRadius = 140f;        // 每滩毒液半径（图是 464 宽扁的，要大一点）
    [Export] public float PoisonDuration = 4f;        // 存在多久
    [Export] public int PoisonDamage = 5;             // 每跳伤害
    [Export] public float PoisonTickInterval = 0.5f;  // 每跳间隔
    [Export] public Texture2D PoisonTexture;          // 毒圈贴图（留空则用 apple-juice）
    [Export] public float PoisonTexHalfWidth = 232f;  // 贴图内容半宽（464 宽的一半）
    [Export] public float PoisonTexBottomOffset = 45f;// 贴图底部相对中心的距离
    [Export] public float PoisonSquashY = 1f;         // 压扁程度（这张是液体图，不用压）

    [ExportGroup("概率")]
    [Export] public float JumpChance = 0.55f;  // 跳砸概率，剩下是喷毒

    private enum Phase { Idle, Airborne, SpitCharge, Spitting }

    private Phase _phase = Phase.Idle;
    private float _timer;
    private bool _wasOnFloor = true;
    private int _spitLeft;
    private float _spitTimer;

    private Sprite2D _sprite2;
    private DialogueUI _bubble;
    private CollisionShape2D _bodyShape;
    private Area2D _hurtArea2;
    private Vector2 _baseScale = Vector2.One;
    private Vector2 _originalScale = Vector2.One;   // 场景里配的原始 Scale，换图时永远基于它算

    private bool _phase2;        // 是否已进入第二阶段
    private float _hangTimer;    // 滞空倒计时
    private bool _slamming;      // 是否已经开始砸下
    private float _jumpDir;      // 这一跳的水平方向（-1 左 / +1 右 / 0 原地）
    private bool _reachedApex;   // 是否已经升到最高点（到了才开始滞空计时）

    // 死亡演出
    private enum DeathPhase { None, Jumping, Slamming, Done }
    private DeathPhase _deathPhase = DeathPhase.None;
    private Vector2 _deathTarget;
    private float _deathTimer;
    private float _animT;        // 空中动画计时

    public override void _Ready()
    {
        base._Ready();
        _sprite2 = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite2 == null) return;

        // 一阶段：换成跳跃动画表。注意不要覆盖 Scale——
        // 用户在场景里调过大小，代码强行设会把它改回去。
        if (Phase1Texture != null)
        {
            _sprite2.Texture = Phase1Texture;
            _sprite2.Hframes = Mathf.Max(1, Phase1Hframes);
            _sprite2.Frame = 0;
        }
        _baseScale = _sprite2.Scale;
        _originalScale = _sprite2.Scale;   // 记住场景里的原始 Scale，换图永远基于它

        _bubble = GetNodeOrNull<DialogueUI>("DialogueUI");
        _bodyShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _hurtArea2 = GetNodeOrNull<Area2D>("HurtArea");
        ImmuneToKnockback = true;      // Boss 不吃击退
        SyncBodyToSprite(false);
    }

    // 各阶段贴图的内容范围（实测，用来让碰撞体和画面严丝合缝）
    private const float P1ContentW = 80f, P1ContentH = 144f, P1Bottom = 71f;    // 128×160 帧里
    private const float P2ContentW = 472f, P2ContentH = 444f, P2Bottom = 219f;  // 512×512 图里

    /// <summary>按当前贴图内容 + Scale 重算碰撞体，并让贴图底部贴住碰撞体底部。</summary>
    private void SyncBodyToSprite(bool phase2)
    {
        if (_sprite2 == null || _bodyShape == null) return;

        float cw = phase2 ? P2ContentW : P1ContentW;
        float ch = phase2 ? P2ContentH : P1ContentH;
        float bottom = phase2 ? P2Bottom : P1Bottom;

        var sc = _sprite2.Scale;
        _bodyShape.Shape = new RectangleShape2D
        {
            Size = new Vector2(cw * sc.X, ch * sc.Y)
        };

        // 贴图位置统一归零：位置只由 offset 控制。
        // 否则场景里手动调过的 position 会叠加进来（比如 position.y=-98 就会整体上浮 98）。
        _sprite2.Position = Vector2.Zero;

        // 碰撞体底部（相对节点）= ch*sc.Y/2
        // 贴图内容底部（相对节点）= (bottom + offset.y) * sc.Y
        // 两者相等 → offset.y = ch/2 - bottom（和 Scale 无关）
        _sprite2.Offset = new Vector2(0f, ch * 0.5f - bottom);

        // 受击判定区也要挪到脚底！
        // Boss 本体很高（374 像素），判定区如果留在身体中心，
        // 玩家站在脚下时垂直差 160+ 像素，判定半径 100 根本够不到 → 打不到人。
        if (_hurtArea2 != null)
            _hurtArea2.Position = new Vector2(0f, ch * sc.Y * 0.5f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead)
        {
            UpdateDeath((float)delta);   // 死亡演出自己处理，不能走父类的死亡分支
            return;
        }

        base._PhysicsProcess(delta);   // 父类负责重力、移动、接触伤害

        // 第一次受击才苏醒（在此之前一直待机）
        if (Dormant)
        {
            if (Hp < MaxHp) Dormant = false;
            else return;
        }

        CheckPhase();

        float dt = (float)delta;
        bool onFloor = IsOnFloor();

        // 空中：先自然升到最高点 → 悬停 HangTime 秒 → 高速砸下
        if (_phase == Phase.Airborne)
        {
            if (!_reachedApex)
            {
                // 上升阶段：不干预，让重力把初速度吃干净
                if (Velocity.Y >= 0f)
                {
                    // 到顶了，开始滞空计时
                    _reachedApex = true;
                    _hangTimer = HangTime;
                    Velocity = new Vector2(Velocity.X, 0f);
                }
            }
            else if (_hangTimer > 0f)
            {
                _hangTimer -= dt;
                Velocity = new Vector2(Velocity.X, 0f);   // 悬停
            }
            else
            {
                if (!_slamming)
                {
                    _slamming = true;
                    Velocity = new Vector2(0f, 0f);
                }
                Velocity = new Vector2(0f, SlamFallSpeed);
            }

            if (_slamming && onFloor)
            {
                DoSlam();
                EnterIdle();
            }
            _wasOnFloor = onFloor;
            return;
        }

        _wasOnFloor = onFloor;

        // 落地/待机时把拉伸压扁平滑收回基准尺寸（比 Tween 可靠）
        if (_sprite2 != null && _phase != Phase.Airborne)
            _sprite2.Scale = _sprite2.Scale.Lerp(_baseScale, Mathf.Min(1f, dt * 14f));

        switch (_phase)
        {
            case Phase.Idle:
                _timer -= dt;
                if (_timer <= 0f) ChooseSkill();
                break;

            case Phase.SpitCharge:
                _spitTimer -= dt;
                // 喷毒前闪红预警，给玩家反应时间
                if (_sprite2 != null)
                    _sprite2.Modulate = Mathf.Sin(_spitTimer * 45f) > 0f
                        ? new Color(1f, 0.45f, 0.45f)
                        : Colors.White;
                if (_spitTimer <= 0f)
                {
                    if (_sprite2 != null) _sprite2.Modulate = Colors.White;
                    _phase = Phase.Spitting;
                    _spitLeft = SpitCount;
                    _spitTimer = 0.2f;
                }
                break;

            case Phase.Spitting:
                _spitTimer -= dt;
                if (_spitTimer <= 0f)
                {
                    SpawnPoison();
                    _spitLeft--;
                    if (_spitLeft <= 0) EnterIdle();
                    else _spitTimer = SpitDelay;
                }
                break;
        }
    }

    /// <summary>
    /// 父类每帧用这个覆盖 Velocity.X，所以跳跃的水平速度必须在这里给，
    /// 在 DoJump 里设 Velocity.X 会被冲掉。
    /// 滞空期间朝玩家方向漂移，砸下时水平停住。
    /// </summary>
    protected override float ComputeHorizontalVelocity(float dt)
    {
        // 上升 + 滞空期间都朝玩家方向漂移，砸下时水平停住
        if (_phase == Phase.Airborne && !_slamming)
            return _jumpDir * JumpHorizontalSpeed;
        return 0f;
    }

    /// <summary>空中播放跳跃动画，落地/待机用第 0 帧。</summary>
    protected override void UpdateAnimation(float dt)
    {
        if (_sprite2 == null) return;

        if (_dead && _sprite2.Hframes > 1)
        {
            // 死亡演出期间继续播哭泣动画
            _animT += dt;
            if (_animT >= AirFrameTime * 1.6f)
            {
                _animT = 0f;
                _sprite2.Frame = (_sprite2.Frame + 1) % _sprite2.Hframes;
            }
        }
        else if (_phase == Phase.Airborne && _sprite2.Hframes > 1)
        {
            _animT += dt;
            if (_animT >= AirFrameTime)
            {
                _animT = 0f;
                _sprite2.Frame = (_sprite2.Frame + 1) % _sprite2.Hframes;
            }
        }
        else
        {
            _sprite2.Frame = 0;
            _animT = 0f;
        }
    }

    private void EnterIdle()
    {
        _phase = Phase.Idle;
        _timer = IdleTime;
        Velocity = new Vector2(0f, Velocity.Y);
    }

    private void ChooseSkill()
    {
        // 第一阶段只会跳；第二阶段才开始喷毒
        if (!_phase2)
        {
            DoJump();
            return;
        }

        if (GD.Randf() < JumpChance) DoJump();
        else StartSpit();
    }

    /// <summary>血量过半 → 换第二阶段贴图，解锁喷毒。</summary>
    private void CheckPhase()
    {
        if (_phase2 || _sprite2 == null) return;

        float percent = Hp * 100f / Mathf.Max(1, MaxHp);
        if (percent > PhaseChangeHpPercent) return;

        _phase2 = true;
        if (Phase2Texture != null)
        {
            _sprite2.Texture = Phase2Texture;
            _sprite2.Hframes = 1;
            _sprite2.Frame = 0;
            _sprite2.Scale = Vector2.One * Phase2Scale;
            _baseScale = _sprite2.Scale;
        }
        SyncBodyToSprite(true);
        (GetParent() as BossArena)?.Shake(16f, 0.45f);
        GD.Print("[Boss] 进入第二阶段");
    }

    private void DoJump()
    {
        // 朝玩家所在方向跳
        float dir = 0f;
        var player = GetTree().Root.FindChild("box", true, false) as Box;
        if (player != null && IsInstanceValid(player))
        {
            float dx = player.GlobalPosition.X - GlobalPosition.X;
            if (Mathf.Abs(dx) > 8f) dir = Mathf.Sign(dx);
        }

        _phase = Phase.Airborne;
        _wasOnFloor = false;
        _reachedApex = false;    // 先升到顶才开始滞空
        _hangTimer = 0f;
        _slamming = false;
        _animT = 0f;
        _jumpDir = dir;
        Velocity = new Vector2(dir * JumpHorizontalSpeed, -JumpForce);

        // 起跳：纵向拉长
        if (_sprite2 != null)
            _sprite2.Scale = _baseScale * new Vector2(1f - SquashAmount, 1f + SquashAmount);
    }

    private void DoSlam(bool dealDamage = true)
    {
        // 落地：横向压扁。恢复交给 _PhysicsProcess 里的每帧插值，
        // 不用 Tween —— Tween 在这里不可靠，会导致压扁状态一直留着，
        // 贴图比碰撞体矮一截，看起来像浮在天上。
        if (_sprite2 != null)
            _sprite2.Scale = _baseScale * new Vector2(1f + SquashAmount, 1f - SquashAmount);

        // 震屏
        (GetParent() as BossArena)?.Shake(SlamShake, SlamShakeTime);
        AudioManager.Instance?.PlaySfx("slam", 1f);

        if (!dealDamage) return;   // 死亡演出那一下只震不伤

        var player = GetTree().Root.FindChild("box", true, false) as Box;
        if (player == null || !IsInstanceValid(player)) return;

        // 水平按半径判、垂直按"离脚底多远"判。
        // 不能直接算 Boss 中心到玩家的距离：Boss 中心离地面 187 像素，
        // 玩家站在脚下时距离就有 187，用半径 150 判永远砸不到。
        float dx = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
        float footY = GlobalPosition.Y;
        if (_bodyShape?.Shape is RectangleShape2D rect) footY += rect.Size.Y * 0.5f;
        float dy = Mathf.Abs(player.GlobalPosition.Y - footY);

        if (dx > SlamRadius || dy > SlamHeight) return;

        player.TakeDamage(SlamDamage, GlobalPosition);
    }

    private void StartSpit()
    {
        _phase = Phase.SpitCharge;
        _spitTimer = SpitWarnTime;
    }

    private void SpawnPoison()
    {
        // 代码创建，不依赖 PoisonZone.tscn（和 DeathExplosion 的做法一致）
        if (PoisonTexture == null)
            PoisonTexture = GD.Load<Texture2D>("res://assets/boss/poison_juice.png");

        var zone = new PoisonZone
        {
            Radius = PoisonRadius,
            Duration = PoisonDuration,
            DamagePerTick = PoisonDamage,
            TickInterval = PoisonTickInterval,
            ZoneTexture = PoisonTexture,
            TextureHalfWidth = PoisonTexHalfWidth,
            TextureBottomOffset = PoisonTexBottomOffset,
            SquashY = PoisonSquashY,
        };
        GetParent().AddChild(zone);

        int idx = SpitCount - _spitLeft;                 // 0, 1, 2...
        float offsetX = (idx - (SpitCount - 1) * 0.5f) * SpitSpread;
        float spawnX = GlobalPosition.X + offsetX;

        // 毒液必须落在地面上。喷毒瞬间 Boss 可能还在空中，
        // 直接用 Boss 的 y 会让毒滩悬在半空，所以向下打一条射线找地面。
        zone.GlobalPosition = new Vector2(spawnX, FindGroundY(spawnX));
    }

    /// <summary>在 Boss 头顶的气泡里显示一句台词。</summary>
    public void ShowLine(string text)
    {
        if (_bubble == null) _bubble = GetNodeOrNull<DialogueUI>("DialogueUI");
        _bubble?.ShowLine(text);
    }

    /// <summary>收起气泡。</summary>
    public void HideBubble()
    {
        if (_bubble == null) _bubble = GetNodeOrNull<DialogueUI>("DialogueUI");
        _bubble?.HideBubble();
    }

    /// <summary>对话开始时换成哭泣贴图。视觉大小/脚底位置和跳跃贴图保持一致。</summary>
    public void ShowCryTexture()
    {
        if (_sprite2 == null) return;

        var tex = GD.Load<Texture2D>("res://assets/boss/boss_cry.png");
        if (tex == null) { GD.PushWarning("[Boss] 找不到 boss_cry.png"); return; }

        // 哭泣图是 16 帧 × 512×511（8192 / 16 = 512），内容占满整帧。
        // 原跳跃图内容 80×144。
        // 按「内容宽度」对齐：哭泣苹果本体宽度和跳跃状态一致（80×2.6 = 208）。
        // 不能按高度对齐 —— 哭泣图比例接近 1:1，按高度会让宽度变成 375（1.8 倍大）。
        const int CryFrames = 16;
        const float CryContentW = 512f;
        const float CryBottom = 255f;
        const float OriginalContentW = 80f;
        float ratio = OriginalContentW / CryContentW;   // 80/512 = 0.15625

        _sprite2.Texture = tex;
        _sprite2.Hframes = CryFrames;
        _sprite2.Frame = 0;
        // 必须基于 _originalScale 算，不能用 _baseScale：
        // 否则第二次换图会在上一次结果上再乘一遍 ratio，越换越小。
        _sprite2.Scale = _originalScale * ratio;
        _baseScale = _sprite2.Scale;   // 后续拉伸基于当前尺寸

        // Offset 要让「贴图脚底」对齐「碰撞体底部」，不能只按内容底部算：
        // Scale 变小后贴图整体缩小，脚底会浮空。
        // 目标：GlobalY + (Offset.y + CryBottom) * Scale = GlobalY + 碰撞体半高
        // → Offset.y = 碰撞体半高 / Scale - CryBottom
        float bodyHalfH = 187.2f;   // 碰撞体 150×192 的一半（场景里配的）
        if (_bodyShape?.Shape is RectangleShape2D rect) bodyHalfH = rect.Size.Y * 0.5f;
        _sprite2.Offset = new Vector2(0f, bodyHalfH / _sprite2.Scale.Y - CryBottom);
    }

    /// <summary>对话结束后调用：跳向战场中央砸一下，然后消失。</summary>
    public void StartDeathSequence(Vector2 center)
    {
        if (_deathPhase != DeathPhase.None) return;

        _deathTarget = center;
        _deathPhase = DeathPhase.Jumping;
        _deathTimer = 0f;

        float dx = center.X - GlobalPosition.X;
        float dir = Mathf.Abs(dx) < 4f ? 0f : Mathf.Sign(dx);
        Velocity = new Vector2(dir * JumpHorizontalSpeed, -JumpForce);

        if (_sprite2 != null)
            _sprite2.Scale = _baseScale * new Vector2(1f - SquashAmount, 1f + SquashAmount);
    }

    private void UpdateDeath(float dt)
    {
        UpdateAnimation(dt);   // 死亡演出期间也要推进动画（哭泣帧）

        switch (_deathPhase)
        {
            case DeathPhase.None:
                // 对话期间：如果死的时候正好在空中，自然落回地面站好
                if (!IsOnFloor()) Velocity += new Vector2(0f, Gravity * dt);
                else Velocity = new Vector2(0f, Velocity.Y);
                if (_sprite2 != null && IsOnFloor())
                    _sprite2.Scale = _sprite2.Scale.Lerp(_baseScale, Mathf.Min(1f, dt * 10f));
                MoveAndSlide();
                break;

            case DeathPhase.Jumping:
                if (!IsOnFloor()) Velocity += new Vector2(0f, Gravity * dt);
                else if (Velocity.Y >= 0f)
                {
                    DoSlam(false);                       // 只震不伤
                    (GetParent() as BossArena)?.Shake(22f, 0.55f);
                    if (_sprite2 != null)
                        _sprite2.Scale = _baseScale * new Vector2(1f + SquashAmount, 1f - SquashAmount);
                    _deathPhase = DeathPhase.Slamming;
                    _deathTimer = 0.15f;   // 砸完几乎立刻消失，让坍塌紧接上来
                }
                MoveAndSlide();
                break;

            case DeathPhase.Slamming:
                if (!IsOnFloor()) Velocity += new Vector2(0f, Gravity * dt);
                else Velocity = new Vector2(0f, Velocity.Y);
                if (_sprite2 != null)
                    _sprite2.Scale = _sprite2.Scale.Lerp(_baseScale, Mathf.Min(1f, dt * 10f));
                MoveAndSlide();
                _deathTimer -= dt;
                if (_deathTimer <= 0f)
                {
                    _deathPhase = DeathPhase.Done;
                    QueueFree();
                }
                break;
        }
    }

    /// <summary>从 Boss 当前位置向下找地面，找不到就用兜底值。</summary>
    private float FindGroundY(float x)
    {
        var space = GetWorld2D().DirectSpaceState;
        var from = new Vector2(x, GlobalPosition.Y);
        var to = new Vector2(x, GlobalPosition.Y + 900f);
        var query = PhysicsRayQueryParameters2D.Create(from, to, 1);   // layer 1 = 地形
        var hit = space.IntersectRay(query);

        if (hit.Count > 0) return ((Vector2)hit["position"]).Y;
        return GlobalPosition.Y + SpitDropY;   // 兜底
    }

    protected override void OnDeath()
    {
        // 停下，不播死亡动画（等对话）
        Velocity = Vector2.Zero;
        _phase = Phase.Idle;
    }

    protected override void OnDeadFinished()
    {
        // 覆盖父类：不隐藏、不关碰撞、不停物理。
        // 由 BossArena 在对话结束后负责 QueueFree。
        Velocity = Vector2.Zero;
    }
}
