using Godot;
using System.Threading.Tasks;

/// <summary>
/// 黑蛇 Boss 战场：进区锁左右 + 拉镜头 + 挂血条 + 战前对话（只播一次）。
///
/// 摆在 eden 里、把 SnakeBoss 作为子节点即可。区域尺寸按树干为中心算。
/// </summary>
public partial class SnakeArena : Area2D
{
    [ExportGroup("场地")]
    [Export] public float ArenaWidth = 1120f;
    [Export] public float WallHeight = 900f;
    [Export] public float WallThickness = 40f;
    [Export] public float GroundY = 700f;
    [Export] public float TriggerWidth = 300f;
    [Export] public float TriggerHeight = 420f;

    [ExportGroup("镜头")]
    [Export] public float CameraZoom = 0.62f;
    // 相机 Offset 被 Box 每帧按 _camBaseY 重置（-200），所以这里要把那 200 补回来，
    // 否则地面会被挤到画面底边、尾巴和预警带一半在屏幕外。
    [Export] public float CameraCenterY = 715f;

    [ExportGroup("音乐")]
    // 战斗曲。默认 "snake_fight" —— 文件还不存在，所以现在打的时候是静音的（沿用场景曲）。
    // 往 assets/audio/bgm/snake_fight.mp3 丢一首曲子就自动生效，不用改代码。
    // 想直接借用别的地图的战斗曲：把这里改成 boss_fight / beast_fight / fairy_fight。
    [Export] public string BattleBgm = "snake_fight";
    [Export] public float BattleBgmVolume = 0.8f;

    [ExportGroup("UI")]
    [Export] public string BossName = "黑蛇";
    [Export] public float BarWidth = 560f;
    [Export] public float BarTop = 74f;

    [ExportGroup("战前对话（只播一次）")]
    [Export] public string TalkKey = "eden_snake";
    [Export] public string[] IntroLines =
    {
        "黑蛇：嘶——又一个想来摘苹果的。",
        "黑蛇：这棵树上的果子，一颗都不许碰。",
        "黑蛇：既然来了，就留下当肥料吧。",
    };

    private SnakeBoss _boss;
    private Box _player;
    private Camera2D _cam;
    private BossHealthBar _bar;
    private StaticBody2D _wallL, _wallR;
    private Sprite2D _visL, _visR;
    private bool _started;
    private bool _locked;
    private int _gen;               // 战斗代次：重置一次 +1，用来掐掉还在跑的协程
    private float _left, _right;
    private float _shakeT, _shakeAmp;
    private Vector2 _camBackupPos;
    private Vector2 _camBackupZoom;
    private bool _camBackupSmooth;
    private bool _camBackupLockY;

    public float ArenaLeft => _left;
    public float ArenaRight => _right;

    public override void _Ready()
    {
        Monitoring = true;
        BodyEntered += OnBodyEntered;

        _boss = GetNodeOrNull<SnakeBoss>("SnakeBoss") ?? FindBoss();
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        if (_boss == null) GD.PushWarning("[SnakeArena] 没找到 SnakeBoss");
        // 甩尾的红色预警带是画在 Boss 节点上的，Boss 的 z 必须高过前景草丛层（z=2），
        // 否则整条红带会被画面底部的草盖掉，玩家完全看不到预警。
        if (_boss != null) _boss.ZIndex = 4;

        _left = GlobalPosition.X - ArenaWidth * 0.5f;
        _right = GlobalPosition.X + ArenaWidth * 0.5f;
    }

    private SnakeBoss FindBoss()
    {
        foreach (var c in GetChildren())
            if (c is SnakeBoss b) return b;
        return GetTree().Root.FindChild("SnakeBoss", true, false) as SnakeBoss;
    }

    private void OnBodyEntered(Node body)
    {
        if (_started) return;
        if (body is not Box box) return;
        if (box != _player) return;
        _started = true;
        RunIntro();
    }

    private async void RunIntro()
    {
        int gen = _gen;
        bool talked = GameState.I != null && GameState.I.HasTalked(TalkKey);

        if (!talked && IntroLines != null && IntroLines.Length > 0)
        {
            if (_boss != null) _boss.SetIntro();
            GameState.I?.MarkTalked(TalkKey);
            GameState.I.DialogueActive = true;
            _player?.BeginCutscene();

            var bubble = _player?.GetNodeOrNull<DialogueUI>("DialogueUI");
            foreach (var line in IntroLines)
            {
                bubble?.ShowLine(line);
                float hold = Mathf.Clamp(line.Length * 0.085f + 0.85f, 1.4f, 3.0f);
                await Wait(hold);
                if (gen != _gen) return;      // 对话期间玩家死了，别再开战
            }
            bubble?.HideBubble();
            GameState.I.DialogueActive = false;
            _player?.EndCutscene();
        }

        if (gen != _gen) return;
        // 进区的信号是在物理查询的 flush 里发出来的，这时候建碰撞体（建墙）
        // 会被引擎拒绝（Can't change this state while flushing queries），
        // 必须等下一物理帧再开战。
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (gen != _gen) return;
        StartFight();
    }

    private void StartFight()
    {
        LockCamera();
        BuildWalls();
        ShowBar();
        AudioManager.Instance?.PlayBgm(BattleBgm, 0.8f);
        _boss?.BeginBattle();
    }

    // ────────────────────────────────────────── 墙 / 镜头 / 血条

    private void BuildWalls()
    {
        if (_wallL != null) return;
        float h = WallHeight;
        _wallL = ArenaWalls.Make(this, new Vector2(_left - GlobalPosition.X, -h * 0.5f + GroundY - GlobalPosition.Y),
                                 h, WallThickness, 140f, h);
        _wallR = ArenaWalls.Make(this, new Vector2(_right - GlobalPosition.X, -h * 0.5f + GroundY - GlobalPosition.Y),
                                 h, WallThickness, 140f, h);
        _visL = _wallL.GetNodeOrNull<Sprite2D>("Visual");
        _visR = _wallR.GetNodeOrNull<Sprite2D>("Visual");
    }

    private void LockCamera()
    {
        if (_player == null || !IsInstanceValid(_player)) return;
        _cam = _player.GetNodeOrNull<Camera2D>("Camera2D");
        if (_cam == null) return;

        _camBackupZoom = _cam.Zoom;
        _camBackupSmooth = _cam.PositionSmoothingEnabled;
        _camBackupLockY = _player.LockCameraY;

        _player.LockCameraY = false;
        _cam.PositionSmoothingEnabled = false;
        _cam.Zoom = Vector2.One * CameraZoom;
        _locked = true;
    }

    private void UnlockCamera()
    {
        _locked = false;
        if (_cam == null || !IsInstanceValid(_cam)) return;

        // 战斗期间是把相机 GlobalPosition 直接怼到场地中心的，本地 X 会被改掉。
        // 只恢复 zoom / 平滑 / LockCameraY 是不够的：LockCameraHeight 每帧只写 Y，
        // 那个偏出去的 X 会永远留着（实测打完偏 60px，镜头一直歪在右边）。
        // 其它三个 Arena 都有 _cam.Position = Vector2.Zero，这边当初漏了。
        _cam.PositionSmoothingEnabled = _camBackupSmooth;
        _cam.Position = Vector2.Zero;
        _cam.Zoom = _camBackupZoom;
        _cam.ResetSmoothing();
        if (_player != null && IsInstanceValid(_player))
        {
            _player.LockCameraY = _camBackupLockY;
            // 不用手动调 LockCameraHeight：Box 的 _PhysicsProcess 每帧都会补一次
        }
    }

    private void ShowBar()
    {
        if (_boss == null) return;
        _bar = new BossHealthBar();
        AddChild(_bar);
        _bar.Setup(_boss, BossName, BarWidth, BarTop);
    }

    public void Shake(float amp, float time)
    {
        _shakeAmp = Mathf.Max(_shakeAmp, amp);
        _shakeT = Mathf.Max(_shakeT, time);
    }

    public void OnBossDefeated()
    {
        if (_wallL != null) { _wallL.QueueFree(); _wallL = null; }
        if (_wallR != null) { _wallR.QueueFree(); _wallR = null; }
        if (_bar != null) { _bar.QueueFree(); _bar = null; }
        UnlockCamera();
        // 战斗曲切回场景曲（eden 在 AudioManager.SceneBgm 里映射到 jungle）
        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");
        // 蛇死了以后这块区域就不再触发
        Monitoring = false;
    }

    /// <summary>
    /// 玩家被打死：立刻收掉整场战斗，不然墙还立着、Boss 还在放技能，
    /// 玩家复活在墙外进不来，直接卡死。
    /// </summary>
    private void ResetFight()
    {
        // Boss 已经倒下了就不用重置（否则会把尸体复活、血条又冒出来）
        if (_boss != null && IsInstanceValid(_boss) && _boss.State == SnakeBoss.SState.Dead) return;
        if (!_started && !_locked && _wallL == null) return;

        _gen++;                     // 掐掉 RunIntro / 技能协程
        _started = false;
        _shakeT = 0f;
        _shakeAmp = 0f;

        // 死在开场对话里的话，把对话状态也收干净
        if (GameState.I != null) GameState.I.DialogueActive = false;
        _player?.GetNodeOrNull<DialogueUI>("DialogueUI")?.HideBubble();

        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");

        if (_wallL != null) { _wallL.QueueFree(); _wallL = null; }
        if (_wallR != null) { _wallR.QueueFree(); _wallR = null; }
        _visL = null;
        _visR = null;

        if (_bar != null) { _bar.QueueFree(); _bar = null; }

        if (_locked) UnlockCamera();

        // 回满血 + 回休眠 + 重新上锁，并把场上残留的小怪/毒池/苹果清掉
        _boss?.ResetFight();

        Monitoring = true;
        GD.Print("[SnakeArena] 玩家阵亡，战斗重置");
    }

    // ────────────────────────────────────────── 每帧

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 血条必须每帧刷新。其它 Arena 都有这一行，SnakeArena 当初漏了 ——
        // 结果是蛇被打掉血、顶上的条一动不动，看起来像"根本没扣血"。
        _bar?.Refresh();

        // 玩家被打死 / 正在复活 → 立刻收场（拆墙、恢复镜头、Boss 回满休眠）。
        // 用 IsAlive 而不是 Hp<=0：复活那一刻血就回满了，只看血会在"复活中"又开一次战。
        if (_player != null && IsInstanceValid(_player) && !_player.IsAlive)
        {
            ResetFight();
            return;
        }

        // 复活点如果就在战场里，玩家不会再"走进来"，BodyEntered 不会重发，这里补一次
        if (!_started && _player != null && IsInstanceValid(_player) && _player.IsAlive
            && Monitoring && OverlapsBody(_player))
        {
            _started = true;
            RunIntro();
        }

        if (_locked && _cam != null && IsInstanceValid(_cam))
        {
            var target = new Vector2(GlobalPosition.X, CameraCenterY);
            if (_shakeT > 0f)
            {
                _shakeT -= dt;
                float k = Mathf.Clamp(_shakeT / 0.4f, 0f, 1f);
                target += new Vector2((float)GD.RandRange(-1f, 1f), (float)GD.RandRange(-1f, 1f)) * _shakeAmp * k;
                if (_shakeT <= 0f) _shakeAmp = 0f;
            }
            _cam.GlobalPosition = target;
        }

        if (_wallL != null && _player != null && IsInstanceValid(_player))
        {
            ArenaWalls.UpdateAlpha(_player, _left, _right, 200f, 0.55f, _visL, _visR, dt);
        }
    }

    private async Task Wait(float t)
    {
        await ToSignal(GetTree().CreateTimer(Mathf.Max(0.01f, t)), SceneTreeTimer.SignalName.Timeout);
    }
}
