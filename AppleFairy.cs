using Godot;
using System.Threading.Tasks;

/// <summary>
/// 苹果仙子（Apple Fairy）—— 草原段 Boss，位置 16300。
///
/// 核心流程：
///   首次对话（剧情） → 之后二选一（对战 / 退出） → 开战锁区 → 循环三技能
///   → 战败喘气 → 回原位 → 回血 → 三选一（带我上去 / 再战 / 闲聊）
///
/// 继承 Enemy 的原因：玩家伤害判定写死了 `if (collider is Enemy)`（Box.Combat.cs），
/// 不继承就打不到。物理帧整个接管，父类只留血量 / 受击 / 死亡回调。
///
/// 打了 [Tool]：编辑器里也能看到她的模型（以前只有运行才出现）。
/// 编辑器预览只建一个 Sprite2D，**不设 Owner**，所以不会被写进 .tscn。
///
/// ⚠ 所有可调数值都在下面 ExportGroup 里，带注释，直接改检查器就行。
/// </summary>
[Tool]
public partial class AppleFairy : Enemy
{
    [Signal] public delegate void BattleStartedEventHandler();
    [Signal] public delegate void DefeatedEventHandler();

    // ══════════════════════════ 基础数值

    [ExportGroup("① 基础数值")]
    [Export] public int BossHp = 3000;              // 血量上限（玩家平砍约 15 / 蓄力约 30）
    [Export] public int TouchDamage = 8;            // 撞到她的伤害（开战前是 0）
    [Export] public float TalkDistance = 170f;      // 多远能按 E 对话
    [Export] public bool DebugAutoBattle = false;   // 调试：进场立刻开打（正式用要关）

    // ══════════════════════════ 战斗区域

    [ExportGroup("② 战斗区域")]
    [Export] public Rect2 ArenaRect = new Rect2(15250f, 80f, 2250f, 250f); // 左、上、宽、高（她只在这个范围飞）
    [Export] public Vector2 HomePos = new Vector2(16300f, 210f);            // 初始悬浮点，也是战败归位点
    [Export] public float FloatSpeed = 260f;        // 平时移动速度（像素/秒）
    [Export] public float HoverBobAmp = 12f;        // 悬浮上下浮动幅度
    [Export] public float HoverLowY = 190f;         // 换位时最低悬到这里（越小越低，越容易被砍到）
    [Export] public float HoverHighY = 240f;        // 换位时最高悬到这里（太高玩家跳起来也够不着）
    [Export] public float MaxDistFromPlayer = 0f;   // 非俯冲时离玩家最远多少；0 = 不限制（镜头固定看全场时不需要）

    // ══════════════════════════ 技能 1

    [ExportGroup("③ 技能1 俯冲撒尘")]
    [Export] public float DiveSpeed = 720f;         // 俯冲横穿速度
    [Export] public float DiveApproachMul = 2.2f;   // 起飞速度倍率（乘 FloatSpeed）
    [Export] public float DiveWindup = 0.38f;       // 俯冲前预警时间，给玩家反应
    [Export] public float DustGap = 74f;            // 粉尘间距（74 - 约 10 的粉尘宽度 = 64 = 两个身位）
    [Export] public int DustDamage = 10;            // 粉尘伤害
    [Export] public float DustFallSpeed = 40f;      // 粉尘初始下落速度
    [Export] public float DustSpawnOffsetY = 34f;   // 粉尘从她身下多少像素处落下

    // ══════════════════════════ 技能 2

    [ExportGroup("④ 技能2 藤鞭")]
    [Export] public int StemCount = 3;              // 召唤几根（连续召唤）
    [Export] public float StemHeight = 150f;        // 藤鞭高度 = 攻击范围
    [Export] public float StemBehindDist = 130f;    // 长在玩家朝向背后多远
    [Export] public float StemSpawnGap = 0.75f;     // 两根之间的间隔
    [Export] public int StemTouchDamage = 10;       // 鞭身蹭到的伤害
    [Export] public int StemThrustDamage = 20;      // 甩击命中伤害
    [Export] public float StemCastWindup = 0.55f;   // 施法前摇

    // ══════════════════════════ 技能 3

    [ExportGroup("⑤ 技能3 水柱")]
    [Export] public float WaterChargeTime = 2.0f;   // 一阶段：法阵从无到有地浮现，并持续瞄准玩家
    [Export] public float WaterAimHold = 1.0f;      // 二阶段：锁定方向、法阵发亮，给玩家躲避时间
    [Export] public float BeamDuration = 1.0f;      // 光波持续多久（1 秒，出现即满长）
    [Export] public float BeamLength = 1600f;       // 水柱长度（要能横穿整个战场）
    [Export] public float BeamThickness = 150f;     // 水柱粗细（世界像素，屏幕上还要乘缩放）
    [Export] public int BeamDamage = 20;            // 每次结算伤害（0.22s 一跳，站在柱子里会连吃）
    [Export] public float BeamTick = 0.22f;         // 伤害结算间隔
    [Export] public float BeamSweep = 7f;           // 每秒最多转多少度（很小=几乎只轻微偏一下，很好躲）

    // ══════════════════════════ 状态机 / 技能编排

    [ExportGroup("⑦ 状态机 / 技能编排")]
    [Export] public float IdleTime = 0.4f;          // 两次出手之间悬浮待机多久（秒）
    [Export] public float RecoverTime = 0.5f;       // 一个技能收招后停顿多久（秒）
    [Export] public float DiveWeight = 1f;          // 技能1 俯冲的抽取权重（0 = 不打这个技能）
    [Export] public float StemWeight = 1f;          // 技能2 藤鞭的抽取权重
    [Export] public float WaterWeight = 1f;         // 技能3 水波的抽取权重
    [Export] public int SkillCooldown = 1;          // 同一个技能至少隔几轮才能再抽到（1 = 不连着放）
    [Export] public bool LogStates = false;         // 打印状态切换，调试用
    [Export] public float PantBobAmount = 12f;      // 喘气时整只上下起伏多少像素（夸张程度主要看它）
    [Export] public float PantSpeed = 1.1f;         // 每秒喘几下（1.1 正好和 4 帧 pingpong 对上节拍）
    [Export] public float PantSwayDeg = 2.5f;       // 喘气时左右轻微晃动的角度
    [Export] public bool PantExaggerate = true;     // 关掉就只播 4 帧贴图，不加程序化起伏

    // ══════════════════════════ 动画基准

    // 三套动画的原画尺寸不一样（idle 格 402x708、俯冲格 470x512、喘气格 332x768），
    // 直接按"内容高度"统一到 140px 是错的 —— 俯冲是斜着飞的，内容高度只是她的对角线投影，
    // 归一化后整个人会被放大 50%+，表现成"一冲锋就突然变大"。
    // 正确做法：拿**苹果帽子**当基准（刚性物体，各套原画里都一样大）。
    // 实测帽宽 idle 156px → 屏幕 32.5px；俯冲 175px、喘气 206px。

    private const float DiveArtScale = 0.1857f;   // 32.5 / 175
    // 2026-11-09 重画了"弯腰撑膝"的喘气帧（370x3072，苹果帽宽约 240），
    // 缩放按同一个基准重算：32.5 / 240。
    private const float PantArtScale = 0.1354f;
    // 脚要踩在同一条地平线上：idle 的脚在原点下方 70px，新喘气帧内容底边距格中心 374 纹理px，
    // 374*0.1354 = 50.6px → 还差 19.4px，换算成纹理px 就是 143。
    private const float PantArtOffsetY = 143f;
    private const float PantFrameTime = 0.30f;    // 喘气帧的播放间隔（越短喘得越急）

    // ══════════════════════════ 台词

    [ExportGroup("⑥ 台词（npc: 她说 / me: 主角说）")]
    [Export] public string[] FirstLines =
    {
        "npc:呀，好久没有活物走到这片草地上来了。",
        "npc:我是这片草原的看护者，他们都叫我苹果仙子。",
        "me:看护者？",
        "npc:唔……如果你想知道前面还有什么，得先让我看看你的斤两。",
    };

    [Export] public string[] BattleMenuTitles = { "来打一场吧", "先不了" };
    [Export] public string[] BattlePromptLines = { "npc:那就……别怪我不客气了。" };
    [Export] public string[] DeclineLines = { "npc:胆小鬼。想清楚了再来找我。" };
    [Export] public string[] DefeatLines = { "npc:呼……呼……你比我想的能打。" };
    [Export] public string[] AfterWinMenuTitles = { "带我飞向第四章", "再来一次", "随便聊聊" };
    [Export] public string[] ChatLines = { "npc:（这里的内容你自己填）" };
    [Export] public string[] RematchLines = { "npc:好，那就再来！" };

    // ══════════════════════════ 运行时

    public bool InBattle => _battleOn;
    public bool DefeatedOnce => _defeated;

    private Sprite2D _spriteHolder;   // 改名：父类已经有 _sprite 字段
    private SheetAnimator _anim;
    private DialogueUI _bubble;
    private ChoiceMenu _menu;
    private Sprite2D _marker;
    private Box _player;
    private DialogueUI _playerBubble;

    private bool _battleOn;
    private bool _defeated;
    private bool _talking;
    private bool _inRange;
    private bool _carrying;
    private bool _diving;
    private bool _casting;   // 施法期间冻住悬浮浮动，仙子原地不动
    private bool _holdTalk;
    private bool _carriedUp;   // 已经抱着玩家飞上去了：之后不再允许搭话
    private float _flash;
    private float _bobT;
    private float _pantT;
    private int _lastHp = -1;
    private int _runToken;         // 每次中止战斗 +1，正在跑的 FlyTo 看到它变了就立刻退出

    private string[] _lines;
    private int _lineIdx;
    private bool _playingLines;
    private System.Action _linesDone;

    private Texture2D _texIdle, _texCast, _texDive, _texPant, _texVine, _texCircle;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            BuildEditorPreview();
            return;
        }

        BuildNodes();
        // 分身用一份"虚拟血量"：它只是代理，真正掉的血会转给本体（见 AppleFairy.Clone.cs）
        if (!_isClone) MaxHp = BossHp;   // 必须在 base._Ready() 之前，父类用它填满血
        base._Ready();

        Invulnerable = true;
        Damage = 0;

        LoadArt();
        GlobalPosition = HomePos;
        SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);

        _player = GetTree().Root.FindChild("box", true, false) as Box;
        if (_player != null) _playerBubble = _player.GetNodeOrNull<DialogueUI>("DialogueUI");

        if (_menu != null) _menu.Picked += OnMenuPicked;

        InitStateMachine();

        // 分身：不走对话，进场直接开打
        if (_isClone)
        {
            _lastHp = MaxHp;
            CallDeferred(nameof(BeginCloneBattle));
            return;
        }

        // 注意必须延后：FairyArena 的 _Ready 在本节点之后才跑，
        // 这里直接开战的话 BattleStarted 信号没人接，锁区和镜头都不会生效。
        if (DebugAutoBattle) CallDeferred(nameof(StartBattle));
    }

    public override void _ExitTree()
    {
        if (_menu != null) _menu.Picked -= OnMenuPicked;
    }

    // ─────────────────────────────────────── 编辑器预览

    /// <summary>
    /// 编辑器里只放一个静态 Sprite2D，让场景里能看见她。
    /// 不设 Owner → 不会被保存进 .tscn；每次打开场景重建一遍。
    /// </summary>
    private void BuildEditorPreview()
    {
        foreach (Node c in GetChildren()) if (c.Name == "Sprite2D") return;

        var tex = GD.Load<Texture2D>("res://assets/boss/fairy/idle_sheet.png");
        if (tex == null) return;

        var spr = new Sprite2D
        {
            Name = "Sprite2D",
            Texture = tex,
            Hframes = 8,
            Frame = 0,
            Scale = Vector2.One * 0.2083f,
            Offset = new Vector2(0f, 0.5f),
        };
        AddChild(spr);
    }

    // ─────────────────────────────────────── 子节点

    private void BuildNodes()
    {
        // 受击碰撞盒必须**和贴图重合**。
        // 贴图是以节点原点为中心画的（idle 内容高约 140，即 ±70），
        // 原来这里写的是 Position=(0,-60)、高 120 → 覆盖 -120~0，
        // 比贴图整整高了 60px：玩家跳起来打她腿和下半身，判定盒却在头顶上方，
        // 表现就是"拳拳都打在建模上，却一点血都不掉"。
        AddChild(new CollisionShape2D
        {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(68f, 142f) },
            Position = new Vector2(0f, 0f),
        });

        // 外层 Sprite2D 给父类找（它按名字找 "Sprite2D"），内层 SheetAnimator 播帧
        _spriteHolder = new Sprite2D { Name = "Sprite2D" };
        AddChild(_spriteHolder);
        _anim = new SheetAnimator { Name = "Anim", FrameCount = 8, Interval = 0.14f, PingPong = true };
        _spriteHolder.AddChild(_anim);

        var hurt = new Area2D { Name = "HurtArea", CollisionLayer = 0, CollisionMask = 2 };
        // 和上面的受击盒对齐（这个是"玩家压在身上才吃接触伤害"的开关，同样不能飘在头上）
        hurt.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(62f, 134f) },
            Position = new Vector2(0f, 0f),
        });
        AddChild(hurt);

        AddChild(new RayCast2D { Name = "RayLeft", TargetPosition = new Vector2(-40f, 0f) });
        AddChild(new RayCast2D { Name = "RayRight", TargetPosition = new Vector2(40f, 0f) });

        // 分身不参与对话：气泡、选择菜单、E 提示统统不建
        if (!_isClone)
        {
            // 气泡：比默认小一号，免得顶出 800x450 的画面
            _bubble = new DialogueUI
            {
                Name = "DialogueUI",
                Position = new Vector2(-58f, -120f),
                BubbleSize = new Vector2(210f, 76f),
                FontSize = 15,
                BlipPitch = 1.35f,
            };
            var label = new Label
            {
                Name = "Text",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Word,
            };
            label.AddThemeFontSizeOverride("font_size", 15);
            _bubble.AddChild(label);
            AddChild(_bubble);

            // 选择菜单：同样缩小、贴近她
            _menu = new ChoiceMenu
            {
                Name = "ChoiceMenu",
                Position = new Vector2(-250f, -46f),
                PanelSize = new Vector2(220f, 104f),
                RowHeight = 26f,
                Padding = 8f,
            };
            AddChild(_menu);

            // 按 E 提示
            _marker = new Sprite2D
            {
                Name = "EMarker",
                Texture = GD.Load<Texture2D>("res://e-marker-64.png"),
                Position = new Vector2(0f, -175f),
                Scale = Vector2.One * 0.42f,
                ZIndex = 20,
                Visible = false,
            };
            AddChild(_marker);
        }
    }

    private void LoadArt()
    {
        _texIdle = GD.Load<Texture2D>("res://assets/boss/fairy/idle_sheet.png");
        _texCast = GD.Load<Texture2D>("res://assets/boss/fairy/cast_sheet.png");
        _texDive = GD.Load<Texture2D>("res://assets/boss/fairy/dive_sheet.png");
        _texPant = GD.Load<Texture2D>("res://assets/boss/fairy/pant_sheet.png");
        _texVine = GD.Load<Texture2D>("res://assets/boss/fairy/vine.png");
        _texCircle = GD.Load<Texture2D>("res://assets/boss/fairy/magic_circle.png");
    }

    /// <summary>切动画。每套表格尺寸不同，缩放和偏移要跟着换，否则会突然变大或跳位。</summary>
    private void SetAnim(Texture2D tex, int h, int v, int frames, float interval, float scale, float offsetY, bool pingpong = false)
    {
        if (tex == null || _anim == null) return;

        // 喘气会给 _anim 加程序化的上下位移/摆动，切动画时必须清掉，否则姿势会歪着定格
        _anim.Position = Vector2.Zero;
        _anim.Rotation = 0f;

        _anim.Texture = tex;
        _anim.Hframes = h;
        _anim.Vframes = v;
        _anim.Scale = Vector2.One * scale;
        _anim.Offset = new Vector2(0f, offsetY);
        _anim.Frame = 0;
        _anim.FrameCount = frames;
        _anim.Interval = interval;
        _anim.PingPong = pingpong;
        _anim.Restart();
    }

    private void FaceTo(float x)
    {
        if (_anim != null) _anim.FlipH = x < GlobalPosition.X;
    }

    // ─────────────────────────────────────── 每物理帧

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return;

        float dt = (float)delta;

        // 分身：把这一帧掉的血转给本体，然后把自己补满（共血条的关键）
        if (_isClone) SyncCloneHp();

        // 父类的 TryTakeHit 不是虚方法，改不了；用"血量变了"判断挨打
        if (_lastHp < 0) _lastHp = Hp;
        if (Hp != _lastHp)
        {
            _lastHp = Hp;
            _flash = 0.12f;
            if (PlayerAlive()) FaceTo(_player.GlobalPosition.X);
        }

        // 本体：半血分裂 + 防止两个建模叠在一起
        if (!_isClone)
        {
            CheckHalfHpClone();
            KeepCloneSeparated(dt);
        }

        _flash = Mathf.Max(0f, _flash - dt);
        if (_anim != null)
            _anim.Modulate = _flash > 0f ? new Color(1f, 0.45f, 0.45f) : Colors.White;

        if (_carrying && _player != null && IsInstanceValid(_player))
        {
            _player.GlobalPosition = GlobalPosition + new Vector2(0f, 62f);
            _player.Velocity = Vector2.Zero;
        }

        UpdateState(dt);
    }

    private Vector2 ClampToArena(Vector2 p)
    {
        float x = Mathf.Clamp(p.X, ArenaRect.Position.X + 40f, ArenaRect.Position.X + ArenaRect.Size.X - 40f);

        // 不俯冲的时候别离玩家太远：镜头是跟着玩家走的，她跑到战场另一头就出画了，玩家看不见她
        if (!_diving && PlayerAlive() && MaxDistFromPlayer > 0f)
            x = Mathf.Clamp(x, _player.GlobalPosition.X - MaxDistFromPlayer, _player.GlobalPosition.X + MaxDistFromPlayer);

        return new Vector2(x, Mathf.Clamp(p.Y, ArenaRect.Position.Y, ArenaRect.Position.Y + ArenaRect.Size.Y));
    }

    // ─────────────────────────────────────── 对话

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        if (_isClone) return;      // 分身不参与对话，也不显示 E 提示


        UpdateRange();
        UpdateMarker();

        // 已经把她抱上去（或正在抱）时不处理对话。
        // 以前这里没拦住：玩家在半空中再按 E 会重新打开菜单，
        // 而"抱着"的状态（_carrying）永远不会解除 —— 选完就变成一个既动不了、
        // 又对话标志卡在 true 的死局。
        if (_carrying || _carriedUp) return;

        if (!Input.IsActionJustPressed("interact")) return;
        if (_battleOn) return;
        if (!_talking && GameState.I != null && GameState.I.DialogueActive) return;

        if (!_talking)
        {
            if (_inRange) StartTalk();
            return;
        }

        if (_menu != null && _menu.Visible) { _menu.ConfirmCurrent(); return; }
        AdvanceLine();
    }

    private void UpdateMarker()
    {
        if (_marker == null) return;
        bool want = _inRange && !_talking && !_battleOn && !_carrying && !_carriedUp;
        _marker.Visible = want;
        if (!want) return;

        float t = Time.GetTicksMsec() * 0.001f;
        _marker.Position = new Vector2(0f, -175f + Mathf.Sin(t * 4f) * 5f);
        _marker.Modulate = new Color(1f, 1f, 1f, 0.7f + 0.25f * Mathf.Sin(t * 6f));
    }

    private void UpdateRange()
    {
        if (_player == null || !IsInstanceValid(_player)) { _inRange = false; return; }
        _inRange = _player.IsAlive && GlobalPosition.DistanceTo(_player.GlobalPosition) <= TalkDistance;
        if (!_inRange && _talking && !_battleOn && !_holdTalk) EndTalk();
    }

    private string TalkKey
    {
        get
        {
            string scenePath = GetTree().CurrentScene?.SceneFilePath ?? "";
            return scenePath + "::" + GetPath();
        }
    }

    private void StartTalk()
    {
        if (_carrying || _carriedUp) return;   // 抱着玩家的时候不能搭话

        _talking = true;
        if (GameState.I != null) GameState.I.DialogueActive = true;
        _bubble?.HideBubble();

        bool talked = GameState.I != null && GameState.I.HasTalked(TalkKey);
        if (!talked)
        {
            PlayLines(FirstLines, () =>
            {
                GameState.I?.MarkTalked(TalkKey);
                EndTalk();
            });
            return;
        }

        OpenMenu(_defeated ? AfterWinMenuTitles : BattleMenuTitles);
    }

    private void OpenMenu(string[] titles) => _menu?.Open(titles);

    private void OnMenuPicked(int index)
    {
        _menu?.Close();

        if (_defeated)
        {
            switch (index)
            {
                case 0: CarryPlayerUp(); break;
                case 1: PlayLines(RematchLines, StartBattle); break;
                default: PlayLines(ChatLines, EndTalk); break;
            }
            return;
        }

        if (index == 0) PlayLines(BattlePromptLines, StartBattle);
        else PlayLines(DeclineLines, EndTalk);
    }

    private void PlayLines(string[] lines, System.Action done)
    {
        if (lines == null || lines.Length == 0) { done?.Invoke(); return; }
        _playingLines = true;
        _lines = lines;
        _lineIdx = 0;
        _linesDone = done;
        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        string raw = _lines[_lineIdx];
        bool isPlayer = raw.StartsWith("me:");
        string text = raw.Substring(3);

        if (isPlayer) { _bubble?.HideBubble(); _playerBubble?.ShowLine(text); }
        else { _playerBubble?.HideBubble(); _bubble?.ShowLine(text); }
    }

    private void AdvanceLine()
    {
        if (!_playingLines) return;

        var typing = _bubble != null && _bubble.IsActive && _bubble.IsTyping ? _bubble
                   : (_playerBubble != null && _playerBubble.IsActive && _playerBubble.IsTyping ? _playerBubble : null);
        if (typing != null) { typing.FinishTyping(); return; }

        _lineIdx++;
        if (_lineIdx < _lines.Length) { ShowCurrentLine(); return; }

        _playingLines = false;
        _bubble?.HideBubble();
        _playerBubble?.HideBubble();
        var done = _linesDone;
        _linesDone = null;
        done?.Invoke();
    }

    private void EndTalk()
    {
        _talking = false;
        _holdTalk = false;
        _playingLines = false;
        _bubble?.HideBubble();
        _playerBubble?.HideBubble();
        if (GameState.I != null) GameState.I.DialogueActive = false;
    }

    // ─────────────────────────────────────── 开战 / 战败

    private void StartBattle()
    {
        EndTalk();
        DespawnClone();          // 重新开打时先把上一轮的分身清掉
        _battleOn = true;
        Invulnerable = false;
        Damage = TouchDamage;
        EmitSignal(SignalName.BattleStarted);
        SetState(FairyState.Idle);
        SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);
        BattleLoop();
    }

    /// <summary>
    /// 挑战失败（玩家被打死）时由 FairyArena 调用：把这一场战斗整个收掉。
    ///
    /// 必须做的几件事，少一件玩家就会被卡在战场里：
    ///   1. _battleOn = false —— 技能循环的下一次判定就会退出；
    ///   2. 清掉所有召唤物（藤鞭 / 粉尘 / 光波），否则复活后还在挨打；
    ///   3. 满血、回原位、恢复无敌、关掉碰撞伤害 —— 她会变回能对话的 NPC；
    ///   4. 动画切回 idle，免得卡在施法姿势。
    /// </summary>
    public void AbortBattle()
    {
        if (!_battleOn && !_casting && !_diving && !_carrying) return;

        _battleOn = false;
        _casting = false;
        _diving = false;
        _defeated = false;
        _runToken++;               // 让还在跑的 FlyTo / 演出立刻停手，否则它们会把她又拖走

        DespawnClone();
        ClearSpawned();
        Respawn();                     // 父类：回出生点、满血、清死亡状态
        GlobalPosition = HomePos;
        Velocity = Vector2.Zero;

        Invulnerable = true;
        Damage = 0;
        SetState(FairyState.Npc);
        SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);
    }

    /// <summary>把战斗期间召唤出来的东西全清掉，并把瞄准用的辅助节点也收掉。</summary>
    private void ClearSpawned()
    {
        var parent = GetParent();
        if (parent == null) return;

        foreach (Node c in parent.GetChildren())
        {
            if (c is FairyStem || c is FairyDust || c is FairyBeam)
                c.QueueFree();
            else if (c is Sprite2D spr && (spr.Name == "AimLine" || spr.Name == "AimReticle"))
                c.QueueFree();
        }
    }

    protected override void OnDeath()
    {
        _defeated = true;
        _battleOn = false;
        SetState(FairyState.Defeated);
        DespawnClone();
        Invulnerable = true;
        Damage = 0;
        EmitSignal(SignalName.Defeated);
        DefeatSequence();
    }

    /// <summary>父类的死亡表现是压扁淡出，这里自己演，整个盖掉。</summary>
    protected override void UpdateDead(float dt) { }

    // ─────────────────────────────────────── 工具

    private async Task Wait(float sec)
    {
        await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task FlyTo(Vector2 target, float speed)
    {
        float guard = 0f;
        int token = _runToken;
        while (IsInstanceValid(this))
        {
            if (token != _runToken) return;   // 战斗被中止了，别再飞
            var p = GlobalPosition;
            if (p.DistanceTo(target) < 6f) break;

            float dt = (float)GetPhysicsProcessDeltaTime();
            guard += dt;
            if (guard > 4f) break;   // 兜底：走不到就放弃，别让整段演出卡死

            GlobalPosition = ClampToArena(p.MoveToward(target, speed * dt));
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
