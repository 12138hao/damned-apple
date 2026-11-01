using Godot;

/// <summary>
/// 野兽苹果的战场管理器。
///
/// 管四件事，全都不碰 Boss 自己的状态机：
///   · 触发：Boss 自己判断「玩家 X 距离 250」后发 BattleStarted，这里接手
///   · 相机：锁到战场中心并拉远，让玩家看得到 1580 宽的区域
///   · UI：顶部 Boss 血条（复用 assets/ui/boss 的金框和苹果图标）
///   · 封锁：左右两侧建空气墙，玩家跑不出战场
///
/// 震屏走 Boss 的 ShakeRequested 信号——Boss 只管「什么时候该震」，
/// 具体怎么震由这里决定，两边互不依赖。
///
/// 用法：Boss 作为这个节点的子节点，把 ArenaRect 填成 Rect2(21970, 0, 1580, 500)。
/// </summary>
public partial class BeastArena : Node2D
{
    [ExportGroup("区域")]
    [Export] public Rect2 ArenaRect = new Rect2(21970f, 0f, 1580f, 500f);
    [Export] public float WallThickness = 48f;

    [ExportGroup("空气墙")]
    [Export] public float WallVisualWidth = 150f;    // 空气墙视觉宽度（和第一关统一）
    [Export] public float WallShowDistance = 140f;   // 玩家离墙多近开始显形
    [Export] public float WallMaxAlpha = 0.5f;       // 最大不透明度

    [ExportGroup("相机")]
    [Export] public float FightZoom = 0.52f;      // 800 / 1580 ≈ 0.506，取 0.52 留一点边
    [Export] public Vector2 CamOffset = Vector2.Zero;

    [ExportGroup("UI")]
    [Export] public string BossName = "野兽苹果";
    [Export] public float BarWidth = 620f;
    [Export] public float BarTop = 78f;      // 别和左上角的玩家血条撞上

    [ExportGroup("音乐")]
    [Export] public string BattleBgm = "beast_fight";   // 野兽苹果自己的战斗曲，和第一关的 boss_fight 分开

    private BeastAppleBoss _boss;
    private Box _player;
    private Camera2D _cam;
    private bool _active;
    private bool _cleared;

    private StaticBody2D _wallL, _wallR;
    private Sprite2D _wallLVis, _wallRVis;
    private readonly CameraShaker _shaker = new();
    private BossHealthBar _bar;

    public override void _Ready()
    {
        _boss = FindBoss();
        if (_boss == null) { GD.PushWarning("[BeastArena] 没找到 BeastAppleBoss 子节点"); return; }

        _boss.Invulnerable = true;   // 开战前上锁：区域外的远程攻击打不掉血
        _boss.BattleStarted += OnBattleStarted;
        _boss.ShakeRequested += OnShake;
        _boss.Defeated += OnDefeated;
    }

    public override void _ExitTree()
    {
        if (_boss != null && IsInstanceValid(_boss))
        {
            _boss.BattleStarted -= OnBattleStarted;
            _boss.ShakeRequested -= OnShake;
            _boss.Defeated -= OnDefeated;
        }
    }

    private BeastAppleBoss FindBoss()
    {
        foreach (var c in GetChildren())
            if (c is BeastAppleBoss b) return b;
        return null;
    }

    private Vector2 ArenaCenter => ArenaRect.Position + ArenaRect.Size * 0.5f;

    // ─────────────────────────────────────── 开战

    private void OnBattleStarted()
    {
        if (_active || _cleared) return;

        // 玩家正在死亡 / 复活时绝不能开战。
        // 出问题的链路：玩家被打死 → ResetFight 把 Boss 送回倒挂休眠 →
        // 下一帧 Boss 发现「尸体还离得很近」又发 BattleStarted → 镜头拉到 0.52 倍 →
        // 同一帧 _Process 发现玩家血量为 0 又 ResetFight → 镜头弹回 1 倍……
        // 两个缩放每帧来回切，屏幕上就是一直闪。
        var p = GetTree().Root.FindChild("box", true, false) as Box;
        if (p != null && !p.IsAlive) return;

        _active = true;

        _player = p;
        _cam = _player?.GetNodeOrNull<Camera2D>("Camera2D");
        _shaker.Cam = _cam;      // 震屏组件跟着开战一起接上

        // 正式开打 → 解除无敌
        _boss.Invulnerable = false;

        AudioManager.Instance?.PlayBgm(BattleBgm, 0.8f);

        if (_cam != null)
        {
            // 战斗中必须关平滑：每帧设的是「目标位置」，平滑会让画面滞后
            _cam.PositionSmoothingEnabled = false;
            _cam.Zoom = Vector2.One * FightZoom;
            _cam.Offset = new Vector2(0f, _cam.Offset.Y);
            _cam.GlobalPosition = ArenaCenter + CamOffset;
            _cam.ResetSmoothing();
        }

        // 不能在物理回调里直接 AddChild 带碰撞体的节点，延到帧末
        CallDeferred(nameof(BuildWallsAndUi));
    }

    private void BuildWallsAndUi()
    {
        if (!_active) return;
        BuildWalls();
        BuildUi();
    }

    private void BuildWalls()
    {
        float h = ArenaRect.Size.Y + 200f;
        float cy = ArenaRect.Position.Y + ArenaRect.Size.Y * 0.5f;
        float lx = ArenaRect.Position.X;
        float rx = ArenaRect.Position.X + ArenaRect.Size.X;

        _wallL = ArenaWalls.Make(this, new Vector2(lx, cy), h, WallThickness, WallVisualWidth, h);
        _wallR = ArenaWalls.Make(this, new Vector2(rx, cy), h, WallThickness, WallVisualWidth, h);
        _wallLVis = _wallL.GetNodeOrNull<Sprite2D>("Visual");
        _wallRVis = _wallR.GetNodeOrNull<Sprite2D>("Visual");
    }

    /// <summary>玩家靠近哪面墙，哪面墙就渐显（第一关有、这里原来漏了）。</summary>
    private void UpdateWallAlpha(float dt)
    {
        ArenaWalls.UpdateAlpha(_player,
            ArenaRect.Position.X, ArenaRect.Position.X + ArenaRect.Size.X,
            WallShowDistance, WallMaxAlpha,
            _wallLVis, _wallRVis, dt);
    }

    // ─────────────────────────────────────── 血条

    private void BuildUi()
    {
        _bar = new BossHealthBar();
        AddChild(_bar);
        _bar.Setup(_boss, BossName, BarWidth, BarTop);
    }

    private void UpdateBar() => _bar?.Refresh();

    // ─────────────────────────────────────── 每帧

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        UpdateShake(dt);      // 震屏独立于 _active：死亡爆炸那一下也要震

        if (!_active) return;

        // 玩家死了 / 正在复活 → 立刻重置，否则墙关着、Boss 战还在打，复活后进不来。
        // 用 IsAlive 而不是 Hp <= 0：复活动画一开始血量就回满了，
        // 只看血量会在「复活中」这段时间又把战场开起来，镜头又要跳一次。
        if (_player != null && IsInstanceValid(_player) && !_player.IsAlive)
        {
            ResetFight();
            return;
        }

        if (_cam != null && IsInstanceValid(_cam))
            _cam.GlobalPosition = ArenaCenter + CamOffset;

        UpdateBar();
        UpdateWallAlpha(dt);
    }

    // ─────────────────────────────────────── 震屏

    private void OnShake(float strength, float duration) => _shaker.Shake(strength, duration);

    private void UpdateShake(float dt) => _shaker.Update(dt);

    // ─────────────────────────────────────── 收尾 / 重置

    private void OnDefeated()
    {
        if (!_active) return;
        _cleared = true;
        _active = false;

        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");

        // 别立刻拆墙收镜头：爆炸要放 2 秒多，镜头当场拉回去会让爆炸缩成一小块。
        var timer = GetTree().CreateTimer(1.7);
        timer.Timeout += TearDown;
    }

    private void ResetFight()
    {
        _active = false;
        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");

        if (_boss != null && IsInstanceValid(_boss))
        {
            _boss.Respawn();
            _boss.Invulnerable = true;   // 重新上锁，等玩家再踏进来才解锁
        }

        TearDown();
    }

    private void TearDown()
    {
        _wallL?.QueueFree();
        _wallR?.QueueFree();
        _wallL = null;
        _wallR = null;
        _wallLVis = null;
        _wallRVis = null;

        if (_bar != null && IsInstanceValid(_bar)) _bar.QueueFree();
        _bar = null;

        RestoreCamera();
    }

    private void RestoreCamera()
    {
        if (_cam == null || !IsInstanceValid(_cam)) return;

        _shaker.Reset();
        _cam.PositionSmoothingEnabled = true;
        _cam.Zoom = Vector2.One;
        _cam.Position = Vector2.Zero;
        _cam.Offset = new Vector2(0f, _cam.Offset.Y);
        _cam.ResetSmoothing();
    }

    /// <summary>在编辑器里把战场范围画出来，方便对齐坐标。</summary>
    public override void _Draw()
    {
        if (Engine.IsEditorHint())
        {
            DrawRect(ArenaRect, new Color(1f, 0.3f, 0.3f, 0.9f), false, 3f);
            float zc = ArenaRect.Position.X + ArenaRect.Size.X * 0.5f;
            float zh = ArenaRect.Size.X * 0.1f;
            DrawRect(new Rect2(zc - zh, ArenaRect.Position.Y, zh * 2f, ArenaRect.Size.Y),
                     new Color(0.3f, 1f, 0.4f, 0.8f), false, 2f);
        }
    }
}
