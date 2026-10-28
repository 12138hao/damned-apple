using Godot;

/// <summary>
/// 掉落传送触发器。放在 firstry 场景里（不要放在 BossArena 内部），方便直接改参数。
///
/// 流程：
///   BossArena 地板坍塌后调 Arm() → 解除相机 Y 锁（能看见人往下掉）
///   玩家掉到 TriggerY 以下 → 传送到 TeleportTo，重新锁 Y
/// </summary>
public partial class FallTrigger : Node2D
{
    [ExportGroup("掉落传送")]
    [Export] public bool Enable = true;                             // 关掉就完全不生效
    [Export] public float TriggerY = 3500f;                         // 玩家 Y 超过这个值就触发
    [Export] public float RangeHalfWidth = 750f;                    // X 判定半宽（8500~10000 的一半）

    [ExportGroup("去向")]
    [Export] public string NextScene = "";                          // 填场景路径就切场景（如 res://underground.tscn）
    [Export] public Vector2 NextSpawn = Vector2.Zero;               // 切场景后玩家出现在哪里
    [Export] public float NextInvuln = 0f;                          // 到新场景给多少秒无敌（0 = 不给）
    [Export] public bool KeepWeapons = true;                        // 武器要不要带过去
    [Export] public Vector2 TeleportTo = Vector2.Zero;              // NextScene 留空时才用：同场景瞬移到这

    private bool _armed;     // 只有地板塌了之后才开始监视
    private Box _player;

    public override void _Ready()
    {
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        SetProcess(false);   // 平时不跑
    }

    /// <summary>由 BossArena 在地板坍塌后调用：开始监视掉落，并解除相机 Y 锁。</summary>
    public void Arm()
    {
        if (!Enable) return;
        if (_armed) return;

        if (_player == null || !IsInstanceValid(_player))
            _player = GetTree().Root.FindChild("box", true, false) as Box;

        _armed = true;
        SetProcess(true);
        GD.Print($"[Fall] Arm: player={(_player != null && IsInstanceValid(_player))} enable={Enable} triggerY={TriggerY} halfW={RangeHalfWidth}");

        // 解除 Y 锁，镜头才能跟着人往下掉
        if (_player != null && IsInstanceValid(_player))
            _player.LockCameraY = false;
    }

    public override void _Process(double delta)
    {
        if (!_armed) return;
        if (_player == null || !IsInstanceValid(_player)) return;

        if (_player.GlobalPosition.Y <= TriggerY) return;

        // 只在本节点 X 范围内生效（宽度对齐 cliff_edge）
        float dx = Mathf.Abs(_player.GlobalPosition.X - GlobalPosition.X);
        if (dx > RangeHalfWidth)
        {
            GD.Print($"[Fall] 超出 X 范围: dx={dx:F0} > {RangeHalfWidth:F0}（玩家X={_player.GlobalPosition.X:F0} 中心X={GlobalPosition.X:F0}）");
            _armed = false;
            SetProcess(false);
            return;
        }

        GD.Print($"[Fall] 触发: X={_player.GlobalPosition.X:F0} Y={_player.GlobalPosition.Y:F0} → {(string.IsNullOrEmpty(NextScene) ? TeleportTo.ToString() : NextScene)}");

        // 到达触发深度 → 先关掉自己，避免重复触发
        _armed = false;
        SetProcess(false);

        if (!string.IsNullOrEmpty(NextScene))
        {
            GoToScene();
            return;
        }

        // 没填场景路径：老行为，同场景瞬移
        _player.GlobalPosition = TeleportTo;
        _player.Velocity = Vector2.Zero;

        // 关键：LockCameraY 是把镜头锁在 CameraFixedY 这个「世界高度」。
        // 如果不把 CameraFixedY 一起改到新位置，传送后镜头还看着旧高度（500），
        // 玩家在 5900 就直接掉出画面了。
        _player.CameraFixedY = TeleportTo.Y;

        _player.ResetCameraLookY();   // 镜头基准高度归零，避免视角偏移
        _player.LockCameraY = true;   // 新地方重新锁 Y

        var cam = _player.GetNodeOrNull<Camera2D>("Camera2D");
        if (cam != null)
        {
            cam.GlobalPosition = TeleportTo;
            cam.Offset = new Vector2(0f, cam.Offset.Y);   // 清掉可能残留的横向偏移
            cam.ResetSmoothing();
        }
    }

    /// <summary>
    /// 切到下一个场景（第二关）。切场景会销毁玩家实例，
    /// 所以血量、出生点、武器都要先打包进 GameState，新场景的 Box._Ready 会自己取回去。
    /// </summary>
    private void GoToScene()
    {
        if (!ResourceLoader.Exists(NextScene))
        {
            GD.PushWarning($"[Fall] 场景不存在：{NextScene}");
            return;
        }

        if (_player == null || !IsInstanceValid(_player))
        {
            GD.PushWarning("[Fall] 玩家已失效，切场景取消");
            return;
        }

        var gs = GameState.I;
        if (gs != null)
        {
            gs.PrepareTravel(_player.Hp, NextSpawn,
                             KeepWeapons ? _player.Weapons : null,
                             KeepWeapons ? _player.CurrentSlot : -1,
                             NextInvuln);
        }

        GD.Print($"[Fall] 切场景 → {NextScene}，血量 {_player.Hp}，出生点 {NextSpawn}");
        GetTree().ChangeSceneToFile(NextScene);
    }
}
