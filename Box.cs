using Godot;

// Box 按模块拆成了多个 partial 文件：
//   Box.Move.cs     移动、跳跃、重力、单向平台、相机
//   Box.Combat.cs   瞄准、朝向、空手连击、蓄力挥击、攻击输入
//   Box.Weapon.cs   武器槽、贴图与碰撞形状、剑气
//   Box.Vehicle.cs  载具上下车与射击
//   Box.Life.cs     受击、死亡、复活、存档点、对话回应
//   Box.Visual.cs   贴图朝向与动画选帧
//
// 本文件保留：字段总表、信号、_Ready、主循环 _PhysicsProcess、每帧计时器。


// 第 1 关：让方块受重力掉下来，砸在地面上停住。
// 这个文件现在是空的，答案要你自己写。
public partial class Box : CharacterBody2D
{

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
    [Export] public float VehicleMuzzleIconX = 55.5f;
    [Export] public float VehicleMuzzleIconY = 2f;
    [Export] public int VehicleBulletPerShot = 1;       // 点一次鼠标发几发
    [Export] public float VehicleBulletSpreadY = 10f;   // 多发时每发纵向间隔
    [Export] public float VehicleBulletKnockback = 120f; // 子弹把敌人推多远
    [Export] public float VehicleFootTweakY = 0f;       // 车轮对齐后如果还想微调，正数往下
    [Export] public float VehicleBodyWidth = 56f;
    [Export] public float VehicleBodyHeight = 48f;
    private float _vehicleSpriteScale = 0.7f;           // 载具贴图缩放（由 VehiclePickup 传进来）
    [Export] public float ReviveTime = 2.0f;     // 复活动画总时长
    [Export] public float ReviveWingTime = 1.0f; // 前 1 秒稳定展示翅膀光环，之后闪烁
    [Export] public WeaponData[] Weapons = new WeaponData[4];
    [Export] public bool ShowWeaponSprite = true;
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
    private float _atkTime = 0.22f;
    private float _atkStartDeg = -75f;
    private float _atkSweepDeg = 120f;
    private float _atkUpRatio = 0.32f;
    private float _aimAngle;          // 弧度，0 = 正右
    private Vector2 _aimDir = Vector2.Right;
    private float _atkAim;            // 出招瞬间锁定的攻击方向
    private bool _atkThrust;          // 这一招是不是突刺
    private float _lastChargeRatio;   // 上次蓄力比例（0~1），用来判断是否蓄满
    private bool _chargeSfxPlayed;    // 这一轮蓄力是否已经响过起手音（短按不响）
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
    private CircleShape2D _fistShape;
    private float _punchTimer;        // 出拳动画剩余时间
    private int _punchFrame = 16;     // 当前这一拳用哪一帧
    private int _leftPhase;
    private int _rightPhase;
    private int _combo;               // 当前连击数（1 起）
    private float _comboTimer;        // 连击窗口剩余时间
    private float _fistCd;            // 出拳冷却
    private readonly System.Collections.Generic.HashSet<Enemy> _hitThisSwing = new();
    private WeaponData Current;         // 当前手持武器的数据（空手时为 null）
    public int CurrentSlot => _currentSlot;
    private ChargeArc _chargeArc;
    private Camera2D _cam;
    private float _camBaseY;      // 镜头基准高度，从场景里 Camera2D.offset.Y 读进来
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
    [Export] public float SaveJumpSpeed = 420f;   // 存档时向上跳的速度
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
        UpdateCrouch(inputLocked);   // 蹲下要在移动/跳跃之前决定，移动会读 IsCrouching
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
}
