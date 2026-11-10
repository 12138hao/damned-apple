using Godot;

/// <summary>
/// 苹果仙子的战场管理器：锁区、镜头拉远固定、血条、战斗曲。
///
/// 镜头行为对齐第一关（BossArena）：
///   开战瞬间把镜头拉到 <see cref="FightZoom"/>（小于 1 = 看得更远），
///   水平方向**固定在战场中心**不再跟玩家，垂直方向仍由 Box 的锁高逻辑控制。
///   战败后恢复 1 倍缩放并重新打开平滑。
///
/// 空气墙走 ArenaWalls（和 firstry 同一套）：只在玩家靠近时渐显，
/// 不会一开战就杵着两块板子在画面边上。
///
/// 这个类监听 Boss 的 BattleStarted / Defeated，不主动触发战斗。
/// </summary>
public partial class FairyArena : Node2D
{
    [ExportGroup("① 战场范围")]
    [Export] public Rect2 ArenaRect = new Rect2(15250f, -200f, 2250f, 800f); // 和 AppleFairy.ArenaRect 对应（这里只管墙和镜头）
    [Export] public float WallThickness = 40f;      // 墙的碰撞厚度

    [ExportGroup("② 空气墙视觉")]
    [Export] public float WallVisualWidth = 150f;   // 墙贴图的宽度
    [Export] public float WallShowDistance = 220f;  // 玩家离多近开始显现
    [Export] public float WallMaxAlpha = 0.5f;      // 最大不透明度

    [ExportGroup("③ 镜头")]
    [Export] public float ViewWidth = 1200f;         // 战斗时画面横向覆盖多少世界像素（缩放由它反算；填 0 则用 FightZoom）
    [Export] public float FightZoom = 0.667f;       // 备用：ViewWidth=0 时直接用它（0.667 ≈ 800/1200）
    [Export] public float ZoomLerp = 8f;            // 缩放过渡速度（越大越快，0 = 瞬间）
    [Export] public float CamLift = 30f;            // 战斗时镜头上移多少（视野收窄后本来就不露底，这里只微调构图）
    [Export] public bool ClampCameraToArena = true; // 相机左右边界卡在战场范围内

    [ExportGroup("④ UI")]
    [Export] public string BossName = "苹果仙子";
    [Export] public float BarWidth = 620f;
    [Export] public float BarTop = 78f;

    [ExportGroup("⑤ 音频")]
    [Export] public string BattleBgm = "fairy_fight";   // 对应 assets/audio/bgm/fairy_fight.mp3（The Way to Victory）
    [Export] public float BgmVolume = 0.9f;            // 这首曲子母带比 beast_fight 轻约 5dB，稍微提一点

    [Export] public AppleFairy Boss;

    private StaticBody2D _wallL, _wallR;
    private Sprite2D _wallLVis, _wallRVis;
    private BossHealthBar _bar;
    private Box _player;
    private Camera2D _cam;
    private bool _active;
    private bool _ejectPending;   // 挑战失败后，等玩家复活完再把他推出战场
    private float _zoom = 1f;
    private float _targetZoom = 1f;
    private int _savedLimitL, _savedLimitR, _savedLimitT, _savedLimitB;
    private float _savedCameraFixedY;

    public override void _Ready()
    {
        if (Boss == null) Boss = GetNodeOrNull<AppleFairy>("../AppleFairy") ?? FindBoss();
        if (Boss == null)
        {
            GD.PushWarning("[FairyArena] 没找到 AppleFairy");
            return;
        }

        Boss.BattleStarted += OnBattleStarted;
        Boss.Defeated += OnDefeated;

        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _cam = _player?.GetNodeOrNull<Camera2D>("Camera2D");
    }

    public override void _ExitTree()
    {
        if (Boss != null && IsInstanceValid(Boss))
        {
            Boss.BattleStarted -= OnBattleStarted;
            Boss.Defeated -= OnDefeated;
        }
        RestoreCamera();
    }

    private AppleFairy FindBoss()
    {
        return GetTree().CurrentScene?.FindChild(nameof(AppleFairy), true, false) as AppleFairy;
    }

    private void OnBattleStarted()
    {
        if (_active) return;
        _active = true;

        AudioManager.Instance?.PlayBgm(BattleBgm, BgmVolume);

        if (_cam != null)
        {
            _cam.PositionSmoothingEnabled = false;
            _cam.Offset = new Vector2(0f, _cam.Offset.Y);

            // 相机边界卡在战场里：镜头跟着玩家走，但走到边界就停，不会拍到战场外面
            _savedLimitL = _cam.LimitLeft;
            _savedLimitR = _cam.LimitRight;
            _savedLimitT = _cam.LimitTop;
            _savedLimitB = _cam.LimitBottom;

            if (ClampCameraToArena)
            {
                _cam.LimitLeft = Mathf.RoundToInt(ArenaRect.Position.X);
                _cam.LimitRight = Mathf.RoundToInt(ArenaRect.Position.X + ArenaRect.Size.X);
            }

            // 上下边界也要放开：拉远后视野有 450/0.36≈1250 高，
            // 而场景原本的 limit 只有 0~900，相机会被卡死在中间，画面直接偏掉。
            _cam.LimitTop = Mathf.RoundToInt(ArenaRect.Position.Y - 1400f);
            _cam.LimitBottom = Mathf.RoundToInt(ArenaRect.Position.Y + ArenaRect.Size.Y + 1400f);
        }

        // 镜头整体上移：Box 每帧用 CameraFixedY + offset 算镜心，改这个值就等于抬镜头
        if (_player != null) { _savedCameraFixedY = _player.CameraFixedY; _player.CameraFixedY -= CamLift; }

        _zoom = _cam?.Zoom.X ?? 1f;
        _targetZoom = FightZoom;
        if (ViewWidth > 1f)
        {
            float vw = GetViewportRect().Size.X;
            if (vw > 1f) _targetZoom = vw / ViewWidth;
        }

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
        float h = ArenaRect.Size.Y + 300f;
        float cy = ArenaRect.Position.Y + ArenaRect.Size.Y * 0.5f;
        float lx = ArenaRect.Position.X;
        float rx = ArenaRect.Position.X + ArenaRect.Size.X;

        _wallL = ArenaWalls.Make(this, new Vector2(lx, cy), h, WallThickness, WallVisualWidth, h);
        _wallR = ArenaWalls.Make(this, new Vector2(rx, cy), h, WallThickness, WallVisualWidth, h);
        _wallLVis = _wallL.GetNodeOrNull<Sprite2D>("Visual");
        _wallRVis = _wallR.GetNodeOrNull<Sprite2D>("Visual");
    }

    private void BuildUi()
    {
        if (Boss == null) return;
        _bar = new BossHealthBar();
        AddChild(_bar);
        _bar.Setup(Boss, BossName, BarWidth, BarTop);
    }

    private void OnDefeated()
    {
        if (!_active) return;
        _active = false;

        if (_bar != null) { _bar.QueueFree(); _bar = null; }
        if (_wallL != null) { _wallL.QueueFree(); _wallL = null; }
        if (_wallR != null) { _wallR.QueueFree(); _wallR = null; }
        RestoreCamera();

        // 打赢后切回场景音乐：否则战斗曲会在战败对话 / 三选一里一直循环下去
        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");
    }

    /// <summary>
    /// 挑战失败：把这一场整个收掉。
    ///
    /// 和 <see cref="OnDefeated"/>（打赢）不同 —— 这里 Boss 不算被击败，
    /// 她只是变回 NPC，玩家可以重新跟她说话再打。
    /// </summary>
    private void ResetFight()
    {
        if (!_active) return;
        _active = false;
        _ejectPending = true;

        // Boss 侧：停止技能循环、清召唤物、满血回原位、恢复无敌
        if (Boss != null && IsInstanceValid(Boss)) Boss.AbortBattle();

        if (_bar != null) { _bar.QueueFree(); _bar = null; }
        if (_wallL != null) { _wallL.QueueFree(); _wallL = null; }
        if (_wallR != null) { _wallR.QueueFree(); _wallR = null; }
        _wallLVis = null;
        _wallRVis = null;

        RestoreCamera();
        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");
    }

    /// <summary>如果玩家还在战场范围内，把他挪到左边墙外一小段。</summary>
    private void EjectPlayerFromArena()
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        float l = ArenaRect.Position.X;
        float r = ArenaRect.Position.X + ArenaRect.Size.X;
        var p = _player.GlobalPosition;

        if (p.X > l - 60f && p.X < r + 60f)
        {
            _player.GlobalPosition = new Vector2(l - 110f, p.Y);
            _player.Velocity = Vector2.Zero;
        }
    }

    private void RestoreCamera()
    {
        if (_cam == null || !IsInstanceValid(_cam)) return;
        _cam.Zoom = Vector2.One;
        _cam.PositionSmoothingEnabled = true;
        _cam.LimitLeft = _savedLimitL;
        _cam.LimitRight = _savedLimitR;
        _cam.LimitTop = _savedLimitT;
        _cam.LimitBottom = _savedLimitB;
        if (_player != null && IsInstanceValid(_player)) _player.CameraFixedY = _savedCameraFixedY;
        _zoom = 1f;
    }

    public override void _Process(double delta)
    {
        // 复活完成后把玩家送出战场。
        // 不加这一步的话：他复活在战场范围内的存档点，墙虽然拆了，人还在原地，
        // 会以为"挑战没结束、被关在里面出不来"。
        if (_ejectPending && _player != null && IsInstanceValid(_player) && _player.IsAlive)
        {
            EjectPlayerFromArena();
            _ejectPending = false;
        }

        if (!_active) return;

        // 玩家被打死 → 把整场战斗收掉（拆墙、恢复镜头、Boss 变回 NPC）。
        // 用 IsAlive 而不是 Hp <= 0：复活动画一开始血量就回满了，只看血量会漏掉。
        if (_player != null && IsInstanceValid(_player) && !_player.IsAlive)
        {
            ResetFight();
            return;
        }

        _bar?.Refresh();

        // 镜头：缩放到战斗倍率，水平钉在战场中心
        if (_cam != null && IsInstanceValid(_cam))
        {
            float dt = (float)delta;
            float target = _targetZoom;
            _zoom = ZoomLerp <= 0f ? target : Mathf.Lerp(_zoom, target, 1f - Mathf.Exp(-ZoomLerp * dt));
            _cam.Zoom = Vector2.One * _zoom;

            // 镜头**跟着玩家走**（X 由 Box 的相机跟随控制），左右边界用上面设的 Limit 卡住，
            // 所以走到战场边缘就停，不会把战场外的空白拍进来。
        }

        ArenaWalls.UpdateAlpha(_player,
            ArenaRect.Position.X, ArenaRect.Position.X + ArenaRect.Size.X,
            WallShowDistance, WallMaxAlpha,
            _wallLVis, _wallRVis, (float)delta);
    }
}
