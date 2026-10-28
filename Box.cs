using Godot;

// 第 1 关：让方块受重力掉下来，砸在地面上停住。
// 这个文件现在是空的，答案要你自己写。
public partial class Box : CharacterBody2D
{
    // TODO 0：声明一个重力常量
    //   参考值 1200f
    //   建议前面加 [Export]，这样它会出现在右边的检查器面板里，
    //   运行时能直接拖动数值，立刻看到下落快慢的变化。
    //   写法提示：[Export] public float Gravity = 1200f;
    [Export] public float Speed = 700f;
    [Export] public float Gravity = 1200f;
    [Export] public float Acceleration = 2400f;   // 起步加速：300/2400 ≈ 0.125 秒到全速
    [Export] public float Friction = 1800f;       // 松手刹车：300/1800 ≈ 0.167 秒停住
    [Export] public float jumpVelocity = 1500f;
    [Export] public float CoyoteTime = 0.12f;
    [Export] public float JumpBufferTime = 0.12f;
    [Export] public int MaxJumps = 2;
    [Export] public int MaxHp = 100;
    [Export] public float HurtCooldown = 1.0f;
    [Export] public float KnockTime = 0.2f;
    [Export] public float KnockBack = 400f;
    [Export] public float KnockUp = 300f;
    [Export] public float DeathTime = 2.0f;      // 躺平展示多久
    [ExportGroup("相机")]
    [Export] public bool LockCameraY = false;      // 勾上：镜头只跟 X，Y 固定在 CameraFixedY
    [Export] public float CameraFixedY = 420f;     // 锁定时镜头停在这个世界 Y

    [ExportGroup("载具")]
    [Export] public float VehicleSpeedMult = 1.5f;      // 乘坐后移动速度倍率
    [Export] public int VehicleBulletDamage = 3;        // 子弹伤害
    [Export] public float VehicleBulletSpeed = 420f;    // 子弹速度
    [Export] public int VehicleBulletLifeFrames = 100;  // 子弹飞多少物理帧后消失
    // 炮口在贴图里的位置（相对贴图中心）。vehicle_sheet.png 128×128，炮口在 (119,66) 和 (8,66)
    // → 相对中心 ±55.5, +2。换贴图时改这两个值。
    [Export] public float VehicleMuzzleIconX = 55.5f;
    [Export] public float VehicleMuzzleIconY = 2f;
    [Export] public int VehicleBulletPerShot = 1;       // 点一次鼠标发几发
    [Export] public float VehicleBulletSpreadY = 10f;   // 多发时每发纵向间隔
    [Export] public float VehicleBulletKnockback = 120f; // 子弹把敌人推多远
    [Export] public float VehicleFootTweakY = 0f;       // 车轮对齐后如果还想微调，正数往下
    // 乘骑时把碰撞体放大到接近载具视觉尺寸。
    // 否则视觉上载具（128×0.7=90 宽）已经压住敌人了，但 32×32 的碰撞体还没接触，
    // 会出现「明明碾过去了却不掉血」的情况。
    [Export] public float VehicleBodyWidth = 56f;
    [Export] public float VehicleBodyHeight = 48f;
    private float _vehicleSpriteScale = 0.7f;           // 载具贴图缩放（由 VehiclePickup 传进来）

    [Export] public float ReviveTime = 2.0f;     // 复活动画总时长
    [Export] public float ReviveWingTime = 1.0f; // 前 1 秒稳定展示翅膀光环，之后闪烁

    // ---- 第 9 关：近战武器 ----
    // ── 武器系统：4 个槽，每个槽放一个 WeaponData 资源 ──
    // 伤害 / 击退 / 挥击角度全部由武器自己决定，改武器不用改代码
    [Export] public WeaponData[] Weapons = new WeaponData[4];

    // 武器用独立贴图显示（角色帧里那根太细，看起来像没有）
    [Export] public bool ShowWeaponSprite = true;

    // 武器贴图自带的斜角：weapon.png 的棒子是从左下握把指向右上棒头，本身斜了约 45°。
    // 不补偿的话，武器会始终偏离鼠标方向 45°。
    [Export] public float WeaponAngleOffset = 45f;

    [ExportGroup("攻击角度限制")]
    // 屏幕坐标：0°=右，90°=下，180°=左，-90°=上。
    // 这段区间（脚下左右各 60°）打不到，鼠标指到这里时攻击方向会贴到边界。
    [Export] public float AimBlockFromDeg = 25f;    // 脚下禁区起点（右下）
    [Export] public float AimBlockToDeg = 155f;     // 脚下禁区终点（左下）

    [ExportGroup("空手出拳")]
    [Export] public int FistDamage = 8;            // 第一拳伤害
    [Export] public float FistReach = 26f;         // 拳头伸出距离
    [Export] public float FistTime = 0.14f;        // 单拳动作时长
    [Export] public float FistCooldown = 0.10f;    // 两拳之间最短间隔
    [Export] public float ComboWindow = 0.45f;     // 连击窗口：这么久内再点算连击
    [Export] public int MaxCombo = 4;              // 打满几拳（对应 4 个出拳帧）
    [Export] public float FistRadius = 10f;        // 命中判定半径
    
    [Export] public float AttackCooldown = 0.10f; // 挥完再等多久能再挥
    [Export] public float ChargeMaxTime = 0.8f;   // 按住多久蓄满（秒）
    [Export] public float LookDistance = 72f;     // 上下看时镜头移动距离（约屏幕 1/5）
    [Export] public float LookSpeed = 420f;       // 镜头移动速度
    [Export] public float ChargeShowThreshold = 0.15f;  // 按住超过这么久才显示蓄力条
    [Export] public float BreathInterval = 0.7f;  // 站立呼吸动画的切换间隔（秒）
    [Export] public float DropThroughTime =0.15f;
    [Signal] public delegate void HpChangedEventHandler(int hp,int maxHp);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void RevivedEventHandler();
 
    public int Hp { get; private set; }
    private float _coyoteTimer;
    private float _jumpBufferTimer;

    private int _jumpsLeft;
    private Sprite2D _sprite;
    private float _animTimer;
    private int _runFrame;
    private float _breathTimer;
    private int _breathFrame;

    private float _hurtTimer;

    private float _knockTimer;

    private Vector2  _spawnPoint;

    private enum State { Alive, Dying, Reviving }
    private State _state = State.Alive;
    private float _stateTimer;

    private Area2D _weapon;
    private Sprite2D _weaponSprite;
    private CollisionShape2D _weaponShape;
    private float _attackTimer;
    private float _attackCd;
    private int _currentSlot = -1;   // 当前手持的武器槽，-1 = 空手
    private bool _holding;     // 现在有没有举在手里
    private bool _facingRight = true;   // 朝向只由输入决定，不被击退带偏
    private Vector2 _weaponHome;        // 手持位置基准（从场景里的 Weapon.Position 读）
    private bool _charging;             // 正在蓄力
    private float _chargeTime;          // 已经蓄了多久
    private int _pendingDamage;         // 这一击的伤害（蓄力后算出）
    private float _pendingKnockback;    // 这一击的击退（蓄力后算出）

    // 出招瞬间把武器参数抄一份，避免挥到一半换武器导致手感跳变
    private float _atkTime = 0.22f;
    private float _atkStartDeg = -75f;
    private float _atkSweepDeg = 120f;
    private float _atkUpRatio = 0.32f;

    // 鼠标瞄准：攻击方向由鼠标位置决定，360 度任意角度
    private float _aimAngle;          // 弧度，0 = 正右
    private Vector2 _aimDir = Vector2.Right;
    private float _atkAim;            // 出招瞬间锁定的攻击方向
    private bool _atkThrust;          // 这一招是不是突刺
    private float _lastChargeRatio;   // 上次蓄力比例（0~1），用来判断是否蓄满
    private bool _chargeSfxPlayed;    // 这一轮蓄力是否已经响过起手音（短按不响）

    // 武器判定框的基准（木棒那套），切换武器时按 RangeScale 缩放
    // 载具
    private bool _hasVehicle;                 // 捡到载具没有
    private bool _inVehicle;                  // 正在乘坐
    private Texture2D _vehicleSheet;
    private int _vehicleHframes = 1;
    private Texture2D _baseTexture;           // 原来的角色贴图
    private int _baseHframes = 16;
    private Vector2 _baseSpriteOffset;        // 人物贴图原始的 Offset（场景里设的，别写死 0）
    private Vector2 _baseSpriteScale;         // 人物贴图原始的 Scale
    private CollisionShape2D _bodyShape;      // 本体碰撞体
    private Shape2D _baseBodyShape;           // 人物状态的碰撞体（下车时恢复）

    public bool HasVehicle => _hasVehicle;
    public bool InVehicle => _inVehicle;

    private WeaponAura _aura;                 // 剑气特效（Weapon 下的子节点）
    private Shape2D _baseWeaponShape;
    private Vector2 _baseWeaponShapePos = new Vector2(17f, -16f);

    // 空手出拳
    private CircleShape2D _fistShape;
    private float _punchTimer;        // 出拳动画剩余时间
    private int _punchFrame = 16;     // 当前这一拳用哪一帧

    // 出拳帧交替：左半边 16 → 17 → 16…，右半边 18 → 19 → 18…
    // 左右各记各的，互不影响
    private int _leftPhase;
    private int _rightPhase;
    private int _combo;               // 当前连击数（1 起）
    private float _comboTimer;        // 连击窗口剩余时间
    private float _fistCd;            // 出拳冷却
    // 本次挥击已经打过的敌人。一次挥击只对同一个敌人结算一次伤害，
    // 这样就能放心地把 ignoreInvuln 传 true，让"连续攻击"每一刀都打得中。
    private readonly System.Collections.Generic.HashSet<Enemy> _hitThisSwing = new();
    private WeaponData Current;         // 当前手持武器的数据（空手时为 null）

    /// <summary>当前手持的武器槽索引，-1 表示空手。传送时要用。</summary>
    public int CurrentSlot => _currentSlot;
    private ChargeArc _chargeArc;
    private Camera2D _cam;
    private float _camBaseY;      // 镜头基准高度，从场景里 Camera2D.offset.Y 读进来

    /// <summary>
    /// 把「上下看」的基准高度归零并立刻对齐镜头。
    /// 传送 / 切场景后调用，避免镜头还停在旧地图的 offset 上导致视角偏移。
    /// </summary>
    public void ResetCameraLookY()
    {
        _camBaseY = 0f;
        if (_cam != null)
        {
            _cam.Offset = new Vector2(_cam.Offset.X, 0f);
            _cam.ResetSmoothing();
        }
    }
    private SwingTrail _trail;
    private float _dropTimer;
    private const uint OneWayLayerBit = 8;   // 第 4 层：单向平台专用（layer 4 已被敌人占用）
    private uint _baseMask = 1;     // 进场景时记下原本的 CollisionMask，穿透后按它恢复

    public override void _Ready()
    {
        // 按当前场景自动切 BGM（场景名 → 曲子的映射表在 AudioManager.SceneBgm）
        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _weapon = GetNode<Area2D>("Weapon");
        _weaponSprite = GetNode<Sprite2D>("Weapon/Sprite2D");
        _baseTexture = _sprite.Texture;
        _baseHframes = _sprite.Hframes;
        _baseSpriteOffset = _sprite.Offset;   // 人物贴图原始偏移（不是 0，别弄丢）
        _baseSpriteScale = _sprite.Scale;
        _bodyShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _baseBodyShape = _bodyShape?.Shape;

        // 上一个场景捡到的载具，带过来（只恢复"有没有"，不自动上车）
        if (GameState.I != null && GameState.I.HasVehicle
            && !string.IsNullOrEmpty(GameState.I.VehicleSheetPath))
        {
            var tex = GD.Load<Texture2D>(GameState.I.VehicleSheetPath);
            if (tex != null)
            {
                _hasVehicle = true;
                _vehicleSheet = tex;
                _vehicleHframes = Mathf.Max(1, GameState.I.VehicleHframes);
                _vehicleSpriteScale = Mathf.Max(0.05f, GameState.I.VehicleScale);
                GD.Print("[Vehicle] 从上一个场景带过来一辆载具");
            }
        }
        _weaponShape = GetNode<CollisionShape2D>("Weapon/CollisionShape2D");
        _aura = GetNodeOrNull<WeaponAura>("Weapon/Aura");
        _weaponSprite.Visible = false;
        _weaponHome = _weapon.Position;   // 场景里调的位置就是"手"的位置

        // 武器节点放到手的位置，旋转它就等于绕握把转。
        // weapon.png 48×48，握把在贴图内 (7,40)，贴图中心 (24,24)，
        // 所以偏移 (17,-16) 正好把握把对齐到节点原点 —— 棒子从手里伸出去，
        // 不会再从肚子中间穿出来。
        _weapon.Position = _weaponHome;
        // 贴图的握把偏移/缩放由武器数据决定，见 UpdateWeaponVisual
        _weaponShape.Position = _baseWeaponShapePos;
        _baseWeaponShape = _weaponShape.Shape;   // 记住木棒的基准形状
        _chargeArc = GetNodeOrNull<ChargeArc>("ChargeArc");
        _cam = GetNode<Camera2D>("Camera2D");
        _camBaseY = _cam.Offset.Y;   // 场景里摆的 offset 就是基准，代码不覆盖它
        _trail = GetNodeOrNull<SwingTrail>("SwingTrail");
        _fistShape = new CircleShape2D { Radius = FistRadius };
        _spawnPoint = GlobalPosition;

        // 从别的场景传送过来时，恢复血量和出生点
        var gs = GameState.I;
        Hp = gs != null ? gs.TakeHp(MaxHp) : MaxHp;

        Vector2? spawn = gs?.TakeSpawn();
        if (spawn.HasValue)
        {
            GlobalPosition = spawn.Value;
            Velocity = Vector2.Zero;
            _spawnPoint = GlobalPosition;
        }

        // 跨场景复活带过来的无敌
        float invuln = GameState.I != null ? GameState.I.TakeInvuln() : 0f;
        if (invuln > 0f) _hurtTimer = invuln;

        // 本场景就是存档点所在场景 → 复活位置用存档点（覆盖掉上面的门口位置）
        if (GameState.I != null && !string.IsNullOrEmpty(GameState.I.ReviveScenePath)
            && GameState.I.ReviveScenePath == (GetTree().CurrentScene?.SceneFilePath ?? ""))
        {
            SetRevivePoint(GameState.I.RevivePosition);
        }

        // 恢复武器（槽位内容 + 当前手持）
        if (gs != null)
        {
            var saved = gs.TakeWeapons(out int savedSlot);
            if (saved != null)
            {
                for (int i = 0; i < Weapons.Length && i < saved.Length; i++)
                    Weapons[i] = saved[i];

                if (savedSlot >= 0 && savedSlot < Weapons.Length && Weapons[savedSlot] != null)
                {
                    EquipAt(savedSlot);
                    _holding = true;
                }
            }
        }

        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        // 记下原本的碰撞掩码，单向平台穿透结束后按它恢复
        _baseMask = CollisionMask;

        // 主动向常驻 HUD 注册自己（HUD 是 Autoload，切场景后会拿到新的玩家实例）
        var hud = GetTree().Root.FindChild("Hud", true, false) as Hud;
        hud?.BindPlayer(this);

    }
        /// <summary>持续伤害（毒圈、岩浆）：只扣血，不击退、不触发受击无敌。</summary>
        public void TakeDotDamage(int amount)
        {
            if (_state != State.Alive) return;
            if (amount <= 0) return;

            Hp = Mathf.Max(0, Hp - amount);
            EmitSignal(SignalName.HpChanged, Hp, MaxHp);

            if (Hp <= 0) Die(GlobalPosition + Vector2.Down);
        }

        public void TakeDamage(int amount, Vector2 from)
    {
        if (_state != State.Alive) return;      // 死亡 / 复活期间完全免疫
        if (_hurtTimer > 0f) return;

        AudioManager.Instance?.PlaySfx("hurt", 0.9f);
        Hp = Mathf.Max(0, Hp - amount);
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        if (Hp <= 0)
        {
            Die(from);
            return;
        }

        float dirX = GlobalPosition.X < from.X ? -1f : 1f;
        Velocity = new Vector2(dirX * KnockBack, -KnockUp);
        _hurtTimer = HurtCooldown;
        _knockTimer = KnockTime;
    }

    private void Respawn()
    {
        GlobalPosition = _spawnPoint;
        Velocity = Vector2.Zero;
        Hp = MaxHp;
        // 复活后给一段无敌，挡住「上一帧还在怪物身边」的过期重叠检测
        _hurtTimer = HurtCooldown;
        _knockTimer = 0f;
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);
    }

    // ---------------------------------------------------------- 死亡 / 复活
    private void Die()
    {
        Die(GlobalPosition);
    }

    /// <summary>
    /// 敌人"真正重新接触"玩家时调用：清掉受击免疫，让这次接触立刻生效。
    /// 只在敌人确认离开过（超过 TouchGraceTime）之后才会被调用，
    /// 所以贴着敌人时免疫仍然正常，不会变成"每帧掉血"。
    /// </summary>
    public void ResetHurtCooldown()
    {
        _hurtTimer = 0f;
    }

    /// <summary>把血回满。已经满了就什么也不做，免得刷一堆 HpChanged 信号。</summary>
    public void HealFull()
    {
        if (Hp >= MaxHp) return;
        Hp = MaxHp;
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);
    }

    /// 存档点调用：把死亡后的复活位置改到这里。只在当前场景有效。
    /// 传进来的是存档点所在地面，这里往上抬半个身高，免得复活时半截陷进地板。
    /// </summary>
    public void SetRevivePoint(Vector2 groundPos)
    {
        _spawnPoint = groundPos + new Vector2(0f, -GetHalfHeight());
        GD.Print($"[Revive] 复活点已设为 {_spawnPoint}（地面 {groundPos}）");
    }

    [Export] public float SaveJumpSpeed = 420f;   // 存档时向上跳的速度

    /// <summary>存档时调用：往上跳一下 + 冒个气泡说话。</summary>
    public void DoSaveCheer(string text = "存档成功！")
    {
        if (_state != State.Alive) return;

        // 载具状态不跳：载具本来就不能跳，存档时把车弹起来很怪
        if (!_inVehicle)
            Velocity = new Vector2(Velocity.X, -SaveJumpSpeed);

        var bubble = GetNodeOrNull<DialogueUI>("DialogueUI");
        if (bubble == null) return;
        bubble.ShowLine(text);
        _ = HideBubbleAfter(bubble, 1.8f);
    }

    private async System.Threading.Tasks.Task HideBubbleAfter(DialogueUI bubble, float sec)
    {
        await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(bubble)) bubble.HideBubble();
    }

    /// <summary>碰撞体高度的一半，用来把"地面坐标"换算成"玩家中心坐标"。</summary>
    private float GetHalfHeight()
    {
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shape?.Shape is RectangleShape2D rect) return rect.Size.Y * 0.5f;
        if (shape?.Shape is CircleShape2D circle) return circle.Radius;
        return 16f;
    }

    /// <summary>外部（如深渊/尖刺碰撞区）调用：立刻死亡并走完整死亡+复活流程。</summary>
    public void Kill()
    {
        if (_state != State.Alive) return;   // 已经在死/复活中就不重复触发
        Die(GlobalPosition);
    }

    private void Die(Vector2 from)
    {
        AudioManager.Instance?.PlaySfx("death", 1f, 1f + GD.Randf() * 0.05f);
        // 载具状态下死亡：先下车恢复人物贴图。
        // 死亡动画用帧 8/9，而载具贴图只有 8 帧（0~7），不换回来会越界报错。
        if (_inVehicle)
        {
            _inVehicle = false;
            ApplyVehicleState();
        }

        _state = State.Dying;
        _stateTimer = DeathTime;
        _hurtTimer = 0f;
        _knockTimer = 0f;
        Hp = 0;                                  // 掉崖自杀时血条也要归零
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        // 被击杀的瞬间同样吃一次击退：被打飞出去，再摔在地上
        float dirX = 0f;
        if (!from.IsEqualApprox(GlobalPosition))
        {
            dirX = GlobalPosition.X < from.X ? -1f : 1f;
        }
        Velocity = new Vector2(dirX * KnockBack, -KnockUp);

        _sprite.Modulate = Colors.White;
        _sprite.Frame = 8;                       // 帧 8：平躺
        EmitSignal(SignalName.Died);
    }

    private void StartRevive()
    {
        // 存档点如果不在当前场景，先切回那个场景，到那边再复活
        var gs = GameState.I;
        if (gs != null && !string.IsNullOrEmpty(gs.ReviveScenePath))
        {
            string cur = GetTree().CurrentScene?.SceneFilePath ?? "";
            if (gs.ReviveScenePath != cur)
            {
                // 用存档点的地面位置换算成玩家中心位置（不能传 _spawnPoint，
                // 那是"当前场景"的复活点，在别的场景里是错的）
                Vector2 spawnPos = gs.RevivePosition + new Vector2(0f, -GetHalfHeight());
                // 把复活无敌一起带过去，否则新场景一出生就可能被打死
                gs.PrepareTravel(MaxHp, spawnPos, Weapons, _currentSlot,
                                 ReviveTime + HurtCooldown);
                GetTree().ChangeSceneToFile(gs.ReviveScenePath);
                return;
            }
        }

        _state = State.Reviving;
        _stateTimer = ReviveTime;
        GlobalPosition = _spawnPoint;
        Velocity = Vector2.Zero;

        // 复活是瞬移，相机开了 position_smoothing 会慢慢飘过去，
        // 结果复活动画发生在镜头外面。这里直接把它怼到玩家身上。
        if (_cam != null)
        {
            _cam.GlobalPosition = GlobalPosition;   // 立刻对齐
            _cam.ResetSmoothing();                  // 顺便清掉平滑状态
        }
        Hp = MaxHp;
        _hurtTimer = ReviveTime + HurtCooldown;  // 复活全程无敌，结束后再留一段
        _knockTimer = 0f;
        _sprite.Visible = true;
        _sprite.Modulate = Colors.White;
        _sprite.Frame = 9;                       // 帧 9：天使形态
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);
        EmitSignal(SignalName.Revived);
    }

    private void FinishRevive()
    {
        _state = State.Alive;
        _hurtTimer = 0f;             // 复活动画结束就清掉无敌，否则会接着闪红 1 秒
        _sprite.Visible = true;
        _sprite.Modulate = Colors.White;
        _sprite.Frame = 0;
    }

    /// <summary>死亡 / 复活期间每物理帧调用一次，不处理输入、重力和动画。</summary>
    private void UpdateSpecialState(float dt)
    {
        _stateTimer -= dt;

        if (_state == State.Dying)
        {
            // 死亡期间不再响应输入，但重力和击退照常生效：被打飞出去再摔在地上
            if (!IsOnFloor())
            {
                Velocity += new Vector2(0f, Gravity * dt);
            }
            // 水平速度慢慢衰减：空中阻力小飞得远，落地摩擦大很快停住
            float drag = IsOnFloor() ? 900f : 260f;
            Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, drag * dt), Velocity.Y);
            MoveAndSlide();

            if (_stateTimer <= 0f) StartRevive();
            return;
        }

        // Reviving：站定在出生点，不需要移动
        Velocity = Vector2.Zero;

        // 复活：前 ReviveWingTime 秒稳定显示天使形态，之后在天使和普通之间闪烁
        if (_stateTimer > ReviveTime - ReviveWingTime)
        {
            _sprite.Frame = 9;
        }
        else if (_stateTimer > 0f)
        {
            _sprite.Frame = ((int)(_stateTimer * 12f) % 2 == 0) ? 9 : 0;
        }
        else
        {
            FinishRevive();
        }
    }

    // ---------------------------------------------------------- 对话回应
    /// <summary>对话中说话时的回应动作：轻轻跳一下。jumpSpeed <= 0 或不在空中时不做事。</summary>
    public void SpeakReaction(float jumpSpeed)
    {
        if (jumpSpeed <= 0f) return;
        if (!IsOnFloor()) return;              // 空中就不跳了，避免打乱玩家操作

        Velocity = new Vector2(Velocity.X, -jumpSpeed);
    }

    // ---------------------------------------------------------- 单向平台
    /// <summary>判断脚底下踩的是不是单向平台（看它是否在 OneWayLayerBit 这一层）。</summary>
    private bool OnOneWayPlatform()
    {
        var col = GetLastSlideCollision();
        if (col == null) return false;

        var body = col.GetCollider() as CollisionObject2D;
        if (body == null) return false;

        return (body.CollisionLayer & OneWayLayerBit) != 0;
    }

    // ---------------------------------------------------------- 鼠标瞄准
    /// <summary>用鼠标位置更新攻击方向。鼠标离得太近时保持上一次的方向，避免抖动。</summary>
    private void UpdateAim()
    {
        Vector2 toMouse = GetGlobalMousePosition() - GlobalPosition;
        if (toMouse.LengthSquared() < 9f) return;   // 3 像素以内忽略

        _aimAngle = ClampAimAngle(toMouse.Angle());
        _aimDir = new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle));
    }

    /// <summary>把瞄准角度限制在可攻击范围内，脚下的扇形打不到。</summary>
    private float ClampAimAngle(float rad)
    {
        float deg = Mathf.RadToDeg(rad);
        // 归一化到 -180 ~ 180
        deg = Mathf.Wrap(deg, -180f, 180f);

        // 落在脚下禁区里就贴到最近的边界（按距离判断，正下方时不会突然跳到一边）
        if (deg > AimBlockFromDeg && deg < AimBlockToDeg)
        {
            float toLow = Mathf.Abs(deg - AimBlockFromDeg);
            float toHigh = Mathf.Abs(deg - AimBlockToDeg);
            deg = (toLow <= toHigh) ? AimBlockFromDeg : AimBlockToDeg;
        }

        return Mathf.DegToRad(deg);
    }

    // ---------------------------------------------------------- 空手出拳
    /// <summary>打出一拳。连击窗口内连续点会累加连击，打满时伤害和击退翻倍。</summary>
    private void StartFist()
    {
        AudioManager.Instance?.PlaySfx("attack", 0.9f, 1f + GD.Randf() * 0.06f);
        // 连击累加：窗口内算连击，否则从 1 开始
        _combo = _comboTimer > 0f ? Mathf.Min(_combo + 1, MaxCombo) : 1;
        _comboTimer = ComboWindow;
        _fistCd = FistCooldown;

        bool finisher = _combo >= MaxCombo;

        // 最后一拳更疼、推得更远
        int dmg = finisher ? FistDamage * 2 : FistDamage;
        float knock = finisher ? 320f : 120f;
        float reach = finisher ? FistReach * 1.25f : FistReach;

        // 出拳瞬间把人物朝向也对齐过去。
        // 否则「人物朝右、手臂朝左」会看起来像攻击方向反了。
        _facingRight = _aimDir.X >= 0f;

        // 鼠标在左半边用帧 16，右半边用帧 18。
        // 原本是 16↔17 / 18↔19 交替，但这两帧的头部像素对不齐
        // （实测差异 199 像素里 143 在头部），交替播放时眼睛会乱晃，
        // 所以固定用一帧，宁可少点微动感也不晃眼。
        _punchFrame = _aimDir.X < 0f ? 16 : 18;
        _punchTimer = FistTime;

        // 命中判定：在拳头伸出的位置查一个圆
        Vector2 hitCenter = GlobalPosition + _aimDir * reach;
        _fistShape.Radius = FistRadius * (finisher ? 1.2f : 1f);

        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _fistShape,
            Transform = new Transform2D(0f, hitCenter),
            CollisionMask = 4,
            CollideWithBodies = true,
            CollideWithAreas = false,
        };

        foreach (var hit in space.IntersectShape(query))
        {
            if (hit["collider"].As<Node>() is Enemy enemy)
            {
                // 空手是单帧判定，无视受击无敌，这样连击才打得出来
                enemy.TryTakeHit(dmg, GlobalPosition, knock, 0.25f, true);
            }
        }
    }

    // ---------------------------------------------------------- 近战挥击
    private void StartAttack()
    {
        AudioManager.Instance?.PlaySfx("swing", 0.85f, 1f + GD.Randf() * 0.08f);
        // 快照本招参数（武器可能在挥击过程中被卸下）
        var w = Current;
        _atkTime = w != null ? w.AttackTime : 0.22f;
        _atkStartDeg = w != null ? w.StartDeg : -75f;
        _atkSweepDeg = w != null ? w.AttackSweepDeg : 120f;
        _atkUpRatio = w != null ? w.KnockbackUpRatio : 0.32f;

        // 蓄满 + 这把武器支持突刺 → 这一招是突刺
        _atkThrust = w != null && w.ChargeThrust && _lastChargeRatio >= w.ThrustThreshold;
        if (_atkThrust)
        {
            _atkSweepDeg = 0f;      // 突刺不扫，方向锁死在瞄准线上
        }

        ApplyWeaponShape(w, _atkThrust);

        // 贴图不够长就补一道剑气
        if (_aura != null && w != null && w.AuraLength > 0f)
        {
            _aura.StartDist = w.AuraStartDist;
            _aura.Length = w.AuraLength;
            _aura.Fire();
        }

        _hitThisSwing.Clear();    // 新的一刀，清空命中记录
        _atkAim = _aimAngle;      // 出招瞬间锁定方向，挥击过程中不跟着鼠标飘
        _facingRight = Mathf.Cos(_atkAim) >= 0f;   // 人物朝向同步，避免"人朝右、刀朝左"

        _attackTimer = _atkTime;
        _attackCd = _atkTime + AttackCooldown;
        _weaponSprite.Visible = ShowWeaponSprite;
        _weapon.Monitoring = true;
    }

    private void UpdateAttack(float dt)
    {
        _attackCd = Mathf.Max(0f, _attackCd - dt);

        // 武器已经用 360° 旋转表示方向了，不要再 FlipH，
        // 否则「旋转 + 镜像」叠加会让贴图朝向错乱。
        _weaponSprite.FlipH = false;

        // 贴图大小按武器自己的 SpriteScale，突刺不额外拉伸
        // （横向贴图和斜向贴图的"长度方向"不一样，统一拉伸会变形）
        float iconScale = Current != null ? Current.SpriteScale : 1f;
        _weaponSprite.Scale = Vector2.One * iconScale;

        // 突刺：武器沿朝向往前送出去再收回（sin 曲线，出去一半再回来）
        float thrustExtend = 0f;
        if (_attackTimer > 0f && _atkThrust && Current != null)
        {
            float kk = 1f - Mathf.Max(0f, _attackTimer) / _atkTime;
            thrustExtend = Mathf.Sin(kk * Mathf.Pi) * Current.ThrustReach;
        }

        // 贴图偏移 = 握把对齐 + 突刺前伸
        Vector2 iconOffset = Current != null
            ? Current.SpriteOffset * Current.SpriteScale
            : Vector2.Zero;
        _weaponSprite.Position = iconOffset + new Vector2(thrustExtend, 0f);

        // 武器节点跟着朝向挪到左手/右手
        _weapon.Position = new Vector2(
            _facingRight ? Mathf.Abs(_weaponHome.X) : -Mathf.Abs(_weaponHome.X),
            _weaponHome.Y);

        if (_attackTimer <= 0f)
        {
            // 非攻击（含蓄力）：武器始终指向鼠标
            _weaponSprite.Visible = _holding && ShowWeaponSprite;
            _weapon.Rotation = _aimAngle + Mathf.DegToRad(Current != null ? Current.AngleOffset : WeaponAngleOffset);
            _weapon.Monitoring = false;
            _trail?.Clear();

            // 突刺结束，判定框恢复成普通形状（只恢复一次）
            if (_atkThrust)
            {
                _atkThrust = false;
                ApplyWeaponShape(Current, false);
            }
            return;
        }

        _attackTimer -= dt;

        // 360 度扫击：以出招瞬间的瞄准方向为中心，左右各扫一半
        float k = 1f - Mathf.Max(0f, _attackTimer) / _atkTime;
        float halfRad = Mathf.DegToRad(_atkSweepDeg) * 0.5f;

        // 扫击方向统一成「从上往下」：
        // 朝右时角度 -60° → +60° 就是从右上扫到右下；
        // 朝左时如果照搬会变成从下往上，所以要反过来扫（+60° → -60°）。
        bool sweepDownward = Mathf.Cos(_atkAim) >= 0f;
        float fromAngle = sweepDownward ? _atkAim - halfRad : _atkAim + halfRad;
        float toAngle = sweepDownward ? _atkAim + halfRad : _atkAim - halfRad;

        float angle = Mathf.Lerp(fromAngle, toAngle, k);
        float aimOffset = Current != null ? Current.AngleOffset : WeaponAngleOffset;
        _weapon.Rotation = angle + Mathf.DegToRad(aimOffset);   // 旋转整个武器节点，碰撞体跟着转

        // 气浪：从起始角画到当前角，越接近收招越淡（SwingTrail 用度数）
        // 突刺是一条直线，没有扇形气浪
        if (_trail != null && !_atkThrust)
        {
            float alpha = 1f - 0.55f * k;
            _trail.Show(
                Mathf.RadToDeg(fromAngle),
                Mathf.RadToDeg(angle),
                alpha);
        }

        // 命中判定：直接用物理空间查形状重叠（不依赖 Area2D 的 monitoring 时序）
        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _weaponShape.Shape,
            Transform = _weaponShape.GlobalTransform,
            CollisionMask = 4,
            CollideWithBodies = true,
            CollideWithAreas = false,
        };
        foreach (var hit in space.IntersectShape(query))
        {
            if (hit["collider"].As<Node>() is Enemy enemy)
            {
                // 这一刀已经打过它了，跳过（防止一次挥击在多帧里反复结算）
                if (!_hitThisSwing.Add(enemy)) continue;
                // ignoreInvuln = true：让连续攻击的每一刀都能打中，
                // 不会被敌人 0.25 秒的受击无敌吃掉
                enemy.TryTakeHit(_pendingDamage, GlobalPosition, _pendingKnockback, _atkUpRatio, true);
            }
        }

        if (_attackTimer <= 0f)
        {
            _weaponSprite.Visible = _holding && ShowWeaponSprite;
            _weapon.Monitoring = false;
            _trail?.Clear();
        }
    }

    /// <summary>捡起武器（由 Pickup 调用）。slot 从 1 开始。</summary>
    public void GiveWeapon(int slot, WeaponData data = null)
    {
        int index = slot - 1;
        if (data != null && index >= 0 && index < Weapons.Length)
        {
            Weapons[index] = data;      // 把武器存进这一格
        }
        EquipAt(index);
        _holding = true;
    }

    /// <summary>把某个槽的武器装到手上。-1 或超出范围表示空手。</summary>
    // ---------------------------------------------------------- 载具

    /// <summary>捡到载具（由 VehiclePickup 调用）。之后按 R 乘坐。</summary>
    public void GiveVehicle(Texture2D sheet, int hframes, float rideScale = 0.7f)
    {
        _hasVehicle = true;
        _vehicleSheet = sheet;
        _vehicleHframes = Mathf.Max(1, hframes);
        _vehicleSpriteScale = Mathf.Max(0.05f, rideScale);

        // 记到 GameState，换场景回来还能用
        if (GameState.I != null)
        {
            GameState.I.HasVehicle = true;
            GameState.I.VehicleSheetPath = sheet?.ResourcePath;
            GameState.I.VehicleHframes = _vehicleHframes;
            GameState.I.VehicleScale = _vehicleSpriteScale;
        }

        GD.Print("[Vehicle] 拿到载具，按 R 乘坐");
    }

    /// <summary>R 键：上车 / 下车。</summary>
    private void ToggleVehicle()
    {
        if (!_hasVehicle) return;

        _inVehicle = !_inVehicle;
        ApplyVehicleState();
    }

    private void ApplyVehicleState()
    {
        if (_inVehicle)
        {
            // 上车：收起武器、清掉蓄力
            _holding = false;
            _charging = false;
            _chargeArc?.SetProgress(0f);
            if (_weaponSprite != null) _weaponSprite.Visible = false;
            _trail?.Clear();

            if (_vehicleSheet != null)
            {
                _sprite.Texture = _vehicleSheet;
                _sprite.Hframes = _vehicleHframes;
                _sprite.Frame = 0;
                _sprite.Scale = Vector2.One * _vehicleSpriteScale;

                // 让车轮底和人物脚底落在同一条水平线上（以节点位置为基准）：
                //   人物：贴图 64 高，中心 32，脚底 y=57 → 相对节点 +25
                //   载具：贴图 128 高，中心 64，车轮底 y=121 → 相对中心 +57
                //   载具缩放后要落在 +25 → Offset = 25 / scale - 57
                // 这样改 RideScale 也不用重新调偏移，自动对齐。
                const float PlayerFootOffset = 25f;
                const float VehicleFootFromCenter = 57f;
                float alignY = PlayerFootOffset / Mathf.Max(0.05f, _vehicleSpriteScale)
                               - VehicleFootFromCenter;
                _sprite.Offset = new Vector2(0f, alignY + VehicleFootTweakY);

                // 碰撞体放大到接近载具视觉尺寸，别让车"碾过去"却不掉血
                if (_bodyShape != null)
                {
                    _bodyShape.Shape = new RectangleShape2D
                    {
                        Size = new Vector2(VehicleBodyWidth, VehicleBodyHeight)
                    };
                }
            }
            GD.Print("[Vehicle] 上车");
        }
        else
        {
            // 下车：恢复原贴图
            if (_baseTexture != null)
            {
                _sprite.Texture = _baseTexture;
                _sprite.Hframes = _baseHframes;
                _sprite.Frame = 0;
                _sprite.Offset = _baseSpriteOffset;   // 恢复原始偏移，不是 0
                _sprite.Scale = _baseSpriteScale;
                if (_bodyShape != null && _baseBodyShape != null)
                    _bodyShape.Shape = _baseBodyShape;
            }
            // 下车瞬间让朝向跟鼠标，避免沿用载具状态下的残留值
            _facingRight = _aimDir.X >= 0f;
            GD.Print("[Vehicle] 下车");
        }
    }

    /// <summary>
    /// 载具状态下的左键：从炮管水平发射一发子弹。
    /// 鼠标在左边就从左炮口往左打，在右边就从右炮口往右打（不做 360 度瞄准）。
    /// </summary>
    private void UpdateVehicleAttack(float dt)
    {
        if (!Input.IsActionJustPressed("attack")) return;

        AudioManager.Instance?.PlaySfx("vehicle_shoot", 0.8f, 1f + GD.Randf() * 0.06f);

        bool toRight = _aimDir.X >= 0f;
        Vector2 dir = toRight ? Vector2.Right : Vector2.Left;

        // 炮口跟着贴图走：贴图中心在 Offset，炮口相对中心 (iconX, iconY)，整体再乘缩放
        float iconCenterY = _sprite.Offset.Y;
        float muzzleX = (toRight ? VehicleMuzzleIconX : -VehicleMuzzleIconX) * _vehicleSpriteScale;
        float muzzleY = (iconCenterY + VehicleMuzzleIconY) * _vehicleSpriteScale;

        // 子弹加到场景根，不然会跟着玩家一起动
        Node root = GetTree().CurrentScene ?? GetTree().Root;

        int count = Mathf.Max(1, VehicleBulletPerShot);
        for (int i = 0; i < count; i++)
        {
            // 多发时纵向均匀错开，方向保持水平
            float offsetY = (i - (count - 1) * 0.5f) * VehicleBulletSpreadY;

            var bullet = new Bullet
            {
                Damage = VehicleBulletDamage,
                Speed = VehicleBulletSpeed,
                LifeFrames = VehicleBulletLifeFrames,
                Knockback = VehicleBulletKnockback,
            };

            root.AddChild(bullet);
            bullet.GlobalPosition = GlobalPosition + new Vector2(muzzleX, muzzleY + offsetY);
            bullet.Launch(dir);
        }
    }

    /// <summary>从 from 开始往后找下一个有武器的槽，找不到返回 -1。</summary>
    private int FindNextWeaponSlot(int from)
    {
        for (int k = 1; k <= Weapons.Length; k++)
        {
            int idx = ((from + k) % Weapons.Length + Weapons.Length) % Weapons.Length;
            if (idx < Weapons.Length && Weapons[idx] != null) return idx;
        }
        return -1;
    }

    private void EquipAt(int index)
    {
        if (index >= 0 && index < Weapons.Length && Weapons[index] != null)
        {
            _currentSlot = index;
            Current = Weapons[index];
            UpdateWeaponVisual();
            ApplyWeaponShape(Current, false);   // 切换武器时重置成普通形状
            if (_weaponSprite != null && Current != null)
                _weaponSprite.Scale = Vector2.One * Current.SpriteScale;
        }
        else
        {
            _currentSlot = -1;
            Current = null;
            _holding = false;
            if (_weaponSprite != null) _weaponSprite.Visible = false;
        }
    }

    /// <summary>按当前武器刷新手持贴图。</summary>
    private void UpdateWeaponVisual()
    {
        if (_weaponSprite == null) return;                  // _Ready 之前调用时保护
        if (Current == null) return;

        if (Current.Icon != null) _weaponSprite.Texture = Current.Icon;

        // 握把对齐节点原点：偏移也要乘缩放，否则放大后握把会跑偏
        _weaponSprite.Scale = Vector2.One * Current.SpriteScale;
        _weaponSprite.Position = Current.SpriteOffset * Current.SpriteScale;
    }

    /// <summary>
    /// 按武器参数调整攻击判定框。
    /// 普通攻击用 RangeScale 整体放大；突刺用 ThrustRangeScale 且把框压成一条细线。
    /// </summary>
    private void ApplyWeaponShape(WeaponData w, bool thrust)
    {
        if (_weaponShape == null) return;

        float scale = 1f;
        if (w != null)
        {
            scale = thrust ? w.ThrustRangeScale : w.RangeScale;
        }

        // 尺寸和位置一起放大，远端距离才是真正的 N 倍
        _weaponShape.Scale = new Vector2(scale, scale);
        _weaponShape.Position = _baseWeaponShapePos * scale;

        if (thrust && w != null)
        {
            // 突刺：细长的线
            _weaponShape.Shape = new RectangleShape2D
            {
                Size = new Vector2(w.ThrustWidth, 42f)
            };
        }
        else if (_baseWeaponShape != null)
        {
            _weaponShape.Shape = _baseWeaponShape;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // 对话进行中：锁住所有操作输入（移动/跳跃/攻击/上下车），
        // 只保留重力、动画和镜头。否则会出现"边对话边挥刀/边开枪"。
        bool inputLocked = GameState.I != null && GameState.I.DialogueActive;

        // 死亡 / 复活期间：不处理输入、重力和动画，只推进状态机
        if (_state != State.Alive)
        {
            UpdateSpecialState(dt);
            return;
        }

        // 下面的顺序就是执行顺序，不许随便调换：
        // 朝向要先于动画、攻击判定要先于贴图切换，否则会出现「朝右却播左走帧」。
        UpdateTimers(dt);
        HandleDropThrough(dt);
        ApplyGravity(dt);
        UpdateCameraLook(dt);
        UpdateHorizontalMove(dt, inputLocked);
        UpdateJump(dt, inputLocked);

        MoveAndSlide();

        LockCameraHeight();
        HandleWeaponSwitch(inputLocked);
        UpdateAim();
        UpdateFacing();
        HandleAttackInput(dt, inputLocked);
        UpdateVisual(dt);
    }

    /// <summary>无敌帧 / 击退 / 土狼时间 / 跳跃缓冲的倒计时，以及受击闪红。</summary>
    private void UpdateTimers(float dt)
    {
        _hurtTimer = Mathf.Max(0f, _hurtTimer - dt);
        _knockTimer = Mathf.Max(0f, _knockTimer - dt);
        _sprite.Modulate = _hurtTimer > 0f ? new Color(1f, 0.45f, 0.45f) : Colors.White;

        if (IsOnFloor())
        {
            _coyoteTimer = CoyoteTime;
            _jumpsLeft = MaxJumps;
        }
        else
        {
            _coyoteTimer = Mathf.Max(0f, _coyoteTimer - dt);
        }
        _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);
    }

    /// <summary>单向平台：站在上面按 S 时临时把平台层从碰撞掩码里摘掉。</summary>
    private void HandleDropThrough(float dt)
    {
        // 单向平台：站在上面按 S 就穿下去
        if (Input.IsActionJustPressed("look_down") && IsOnFloor() && OnOneWayPlatform())
        {
            _dropTimer = DropThroughTime;
        }

        if (_dropTimer > 0f)
        {
            _dropTimer -= dt;
            // 暂时不检测单向平台那一层，玩家就会掉下去
            CollisionMask = _baseMask & ~OneWayLayerBit;
        }
        else if (CollisionMask != _baseMask)
        {
            CollisionMask = _baseMask;   // 恢复
        }
    }

    /// <summary>不在地面时加重力。必须乘 dt，否则下落速度跟帧率挂钩。</summary>
    private void ApplyGravity(float dt)
    {
        if (!IsOnFloor())
        { Velocity += new Vector2(0, Gravity * dt); }
    }

    /// <summary>上下看：W 抬头、S 低头，镜头平滑升降。</summary>
    private void UpdateCameraLook(float dt)
    {
        // 上下看：镜头平滑升降，W 上移、S 下移
        float lookY = 0f;
        if (Input.IsActionPressed("look_up")) lookY -= 1f;
        if (Input.IsActionPressed("look_down")) lookY += 1f;
        _cam.Offset = new Vector2(
            _cam.Offset.X,
            Mathf.MoveToward(_cam.Offset.Y,
                             _camBaseY + lookY * LookDistance,
                             LookSpeed * dt));
    }

    /// <summary>左右移动：方向键加速到 Speed，松手用 Friction 刹车。</summary>
    private void UpdateHorizontalMove(float dt, bool inputLocked)
    {
        if (_knockTimer <= 0f)
        {
            float dir = inputLocked ? 0f : Input.GetAxis("move_left", "move_right");
            if (dir != 0)
            {
                _facingRight = dir > 0f;          // 按方向键才改朝向
                float speedMult = _inVehicle ? VehicleSpeedMult : 1f;
                Velocity = new Vector2(Mathf.MoveToward(Velocity.X, dir * Speed * speedMult, Acceleration * dt), Velocity.Y);
            }
            else
            {
                Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0, Friction * dt), Velocity.Y);
            }
        }
    }

    /// <summary>跳跃：跳跃缓冲 + 土狼时间 + 松手切跳跃（短按跳得矮）。</summary>
    private void UpdateJump(float dt, bool inputLocked)
    {
        if (_inVehicle)
        {
            _jumpBufferTimer = 0f;   // 载具不能跳
        }
        else if (!inputLocked && Input.IsActionJustPressed("jump"))
        {
            _jumpBufferTimer = JumpBufferTime;
        }
        if (_jumpBufferTimer > 0 && (_coyoteTimer > 0 || _jumpsLeft > 0))
        {
            Velocity = new Vector2(Velocity.X, -jumpVelocity);
            _jumpBufferTimer = 0;
            _coyoteTimer = 0;
            _jumpsLeft--;
            AudioManager.Instance?.PlaySfx("jump", 0.7f, 1f + GD.Randf() * 0.1f);
        }
        if (Input.IsActionJustReleased("jump") && Velocity.Y < 0)
        {
            Velocity = new Vector2(Velocity.X, Velocity.Y * 0.45f);
        }
    }

    /// <summary>锁定镜头高度：把相机子节点的 local Y 反向补偿掉。</summary>
    private void LockCameraHeight()
    {
        // 锁定镜头 Y：Camera2D 是 box 的子节点，把 local Y 反向补偿掉，
        // 世界 Y 就固定了，X 仍然跟着玩家走。
        if (LockCameraY)
        {
            _cam.Position = new Vector2(_cam.Position.X, CameraFixedY - GlobalPosition.Y);
        }
    }

    /// <summary>1~4 切武器（同槽再按收起）、R 上下车。</summary>
    private void HandleWeaponSwitch(bool inputLocked)
    {
        // 1/2/3/4 切换武器：有武器的槽可以切，同一槽再按一次收起
        for (int i = 0; i < 4; i++)
        {
            if (!Input.IsActionJustPressed($"slot{i + 1}")) continue;

            // 载具状态下不能换武器（换了也挥不出去，只会让木棒举在车上）
            if (_inVehicle) break;

            // 该槽没有武器就忽略
            if (i >= Weapons.Length || Weapons[i] == null) break;

            if (i == 0)
            {
                // 1 键在「有武器的槽」之间循环：举起 → 收起 → 下一个 → 举起 → 收起…
                // 例：木棒举起 → 收起 → 长枪举起 → 收起 → 木棒举起
                // 只要手上举着东西就先收起，不管当前是哪个槽
                if (_holding)
                {
                    _holding = false;
                }
                else
                {
                    int next = FindNextWeaponSlot(_currentSlot);
                    if (next >= 0)
                    {
                        EquipAt(next);
                        _holding = true;
                    }
                }
            }
            else if (_currentSlot == i)
            {
                _holding = !_holding;          // 其他槽保持原逻辑：举起 / 收起
            }
            else
            {
                EquipAt(i);
                _holding = true;
            }
            break;
        }

        // R 键：上车 / 下车（对话中不响应）
        if (!inputLocked && Input.IsActionJustPressed("vehicle"))
        {
            ToggleVehicle();
        }
    }

    /// <summary>朝向：挥击时锁定出招方向，持武器跟鼠标，空手跟方向键。</summary>
    private void UpdateFacing()
    {
        // 朝向：挥击过程中锁定在出招方向，避免鼠标乱动导致角色翻面；
        // 其他持武器状态下跟鼠标；空手走动还是跟移动方向。
        if (_attackTimer > 0f)
        {
            _facingRight = Mathf.Cos(_atkAim) >= 0f;
        }
        else if (_inVehicle)
        {
            // 载具：车头跟着鼠标那半边，鼠标在左就朝左开，不管往哪边走
            _facingRight = _aimDir.X >= 0f;
        }
        else if (_holding || _charging)
        {
            _facingRight = _aimDir.X >= 0f;
        }
    }

    /// <summary>攻击输入：载具射击 / 蓄力挥击 / 空手连击，含连击计时与蓄力条。</summary>
    private void HandleAttackInput(float dt, bool inputLocked)
    {
        // 对话中：把蓄力清掉，避免对话结束后"继续蓄着"
        if (inputLocked && _charging)
        {
            _charging = false;
            _chargeTime = 0f;
            _chargeArc?.SetProgress(0f);
        }

        // 载具：左键发射子弹，不处理武器攻击（对话中不响应）
        if (_inVehicle)
        {
            if (!inputLocked) UpdateVehicleAttack(dt);
        }
        // 蓄力挥击：按住蓄力、松开挥出，伤害随按住时间从 Min 涨到 Max
        else if (!inputLocked && _holding && Current != null)
        {
            if (Input.IsActionJustPressed("attack") && _attackCd <= 0f)
            {
                _charging = true;
                _chargeTime = 0f;
                _chargeSfxPlayed = false;
            }

            if (_charging)
            {
                _chargeTime = Mathf.Min(_chargeTime + dt, ChargeMaxTime);

                // 蓄力音跟着蓄力条一起出现：短按没到 ChargeShowThreshold 就不发声，
                // 否则「点一下就砍」也会先响一声蓄力，听起来很吵。
                if (!_chargeSfxPlayed && _chargeTime >= ChargeShowThreshold)
                {
                    AudioManager.Instance?.PlaySfx("charge", 0.7f);
                    _chargeSfxPlayed = true;
                }

                if (_chargeArc != null)
                {
                    // 短按不显示蓄力条，按够时间才亮出来
                    if (_chargeTime >= ChargeShowThreshold)
                    {
                        _chargeArc.Position = new Vector2(_facingRight ? -38f : 38f, -6f);
                        _chargeArc.Scale = new Vector2(_facingRight ? 1f : -1f, 1f);
                        _chargeArc.SetProgress(
                            ChargeMaxTime <= ChargeShowThreshold
                                ? 1f
                                : (_chargeTime - ChargeShowThreshold) / (ChargeMaxTime - ChargeShowThreshold));
                    }
                    else
                    {
                        _chargeArc.SetProgress(0f);
                    }
                }

                if (Input.IsActionJustReleased("attack"))
                {
                    float ratio = ChargeMaxTime <= 0f ? 1f : _chargeTime / ChargeMaxTime;
                    _lastChargeRatio = ratio;

                    // 伤害和击退都按当前武器的区间、随蓄力比例取值
                    var w = Current;
                    if (w != null)
                    {
                        _pendingDamage = Mathf.RoundToInt(
                            Mathf.Lerp(w.DamageMin, w.DamageMax, ratio));
                        _pendingKnockback = Mathf.Lerp(w.KnockbackMin, w.KnockbackMax, ratio);
                    }
                    else
                    {
                        _pendingDamage = 0;
                        _pendingKnockback = 0f;
                    }

                    StartAttack();
                    _charging = false;
                    _chargeArc?.SetProgress(0f);
                }
            }
        }
        else
        {
            // 空手：按下即出拳，可以连点打连击
            if (Input.IsActionJustPressed("attack") && _fistCd <= 0f)
            {
                StartFist();
            }
        }

        // 连击窗口与出拳冷却倒计时
        _comboTimer = Mathf.Max(0f, _comboTimer - dt);
        _fistCd = Mathf.Max(0f, _fistCd - dt);
        _punchTimer = Mathf.Max(0f, _punchTimer - dt);
        if (_comboTimer <= 0f) _combo = 0;

        UpdateAttack(dt);
    }

    /// <summary>贴图朝向与动画选帧（出拳 > 蓄力 > 挥击 > 上下看 > 空中 > 跑 > 站）。</summary>
    private void UpdateVisual(float dt)
    {
        // 贴图朝向：每帧无条件更新，不能只放在跑动分支里
        // （否则站着、蓄力、空中时转向不会翻面）
        // 载具的贴图分左右两套（0~3 朝右、4~7 朝左），不需要再镜像
        _sprite.FlipH = _inVehicle ? false : !_facingRight;

        // 载具：0~3 帧向右走，4~7 帧向左走（贴图不足 8 帧就只显示第 0 帧）
        if (_inVehicle)
        {
            if (_sprite.Hframes >= 8)
            {
                _animTimer += dt;
                if (_animTimer >= 0.1f)
                {
                    _animTimer = 0f;
                    int start = _facingRight ? 0 : 4;
                    int f = _sprite.Frame;
                    if (f < start || f >= start + 4) f = start;
                    else f = start + (f - start + 1) % 4;
                    _sprite.Frame = f;
                }
            }
            else
            {
                _sprite.Frame = 0;
                _animTimer = 0f;
            }
        }
        // 动画：出拳 > 蓄力 > 挥击 > 上下看 > 空中 > 跑步 > 站立
        else if (_punchTimer > 0f)
        {
            // 出拳帧 16~19 自带左右（16/17 朝左，18/19 朝右），
            // 所以这里必须关掉镜像，否则会「左拳被镜像成右拳」。
            _sprite.FlipH = false;
            _sprite.Frame = _punchFrame;   // 这一拳固定用一帧，下次点击换另一帧
            _animTimer = 0f;
        }
        else if (_charging)
        {
            _sprite.Frame = 12;                      // 帧 12：举棒蓄力
        }
        else if (_attackTimer > 0f)
        {
            _sprite.Frame = 13;                      // 帧 13：挥出
        }
        else if (Input.IsActionPressed("look_up"))
        {
            _sprite.Frame = 14;                      // 帧 14：向上看
        }
        else if (Input.IsActionPressed("look_down"))
        {
            _sprite.Frame = 15;                      // 帧 15：向下看
        }
        else if (_holding && IsOnFloor() && Mathf.Abs(Velocity.X) < 10f)
        {
            // 帧 11 里画的那根棒子太细，看着像没有。
            // 改成用空手呼吸帧 + 独立武器贴图，球棒才看得清。
            _animTimer = 0f;
            _breathTimer += dt;
            if (_breathTimer >= BreathInterval)
            {
                _breathTimer = 0f;
                _breathFrame = 1 - _breathFrame;
            }
            _sprite.Frame = _breathFrame;
        }
        else if (!IsOnFloor())
        {
            _sprite.Frame = Velocity.Y < 0f ? 6 : 7;      // 上升 6 / 下落 7
        }
        else if (Mathf.Abs(Velocity.X) > 10f)
        {
            _animTimer += dt;
            if (_animTimer >= 0.1f)
            {
                _animTimer = 0f;
                _runFrame = (_runFrame + 1) % 4;          // 0→1→2→3→0
                _sprite.Frame = 2 + _runFrame;
            }
        }
        else
        {
            // 站立呼吸：帧 0 ↔ 帧 1 缓慢交替
            _animTimer = 0f;
            _breathTimer += dt;
            if (_breathTimer >= BreathInterval)
            {
                _breathTimer = 0f;
                _breathFrame = 1 - _breathFrame;
            }
            _sprite.Frame = _breathFrame;
        }
    }


    // TODO ①：不在空中时才需要加重力
    //   判断条件：IsOnFloor() 返回 true 表示踩在地面上
    //   给 Velocity 的 Y 分量加上 Gravity
    //   关键：必须乘 delta，否则下落速度会跟帧率挂钩
    //   写法提示：Velocity += new Vector2(0, Gravity * (float)delta);


    // TODO ②：让角色按 Velocity 真正移动
    //   一个方法调用，名字提示：Move____Slide



}
