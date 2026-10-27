using Godot;

/// <summary>
/// 互动式的门：靠近按 E 触发传送。
/// 两种模式（看 TargetScene 填不填）：
///   填了  → 切换到目标场景（跨场景传送）
///   留空  → 在**当前场景内**把玩家挪到 TargetSpawn（同场景传送，适合把房间放在远处的做法）
/// 挂在 Area2D 上，子节点可放一个名为 Hint 的 Label 作为提示。
/// </summary>
public partial class Door : Area2D
{
    [Export] public string TargetScene = "";              // 留空 = 同场景传送
    [Export] public Vector2 TargetSpawn = Vector2.Zero;   // 传送后玩家出现的位置
    [Export] public float InteractDistance = 70f;         // 多近能按 E
    [Export] public float Cooldown = 0.8f;                // 传送后的内置冷却（秒）
    [Export] public string HintText = "按 E 进入";

    [ExportGroup("同场景传送：摄像机边界")]
    [Export] public bool ChangeCameraLimit = false;       // 传送后是否改写摄像机边界
    [Export] public int CamLeft = 0;                      // 留全 0 表示不改
    [Export] public int CamTop = 0;
    [Export] public int CamRight = 0;
    [Export] public int CamBottom = 0;

    private Box _player;
    private Label _hint;
    private bool _inRange;
    private float _coolDown;      // 剩余冷却。>0 时不响应按 E

    public override void _Ready()
    {
        _hint = GetNodeOrNull<Label>("Hint");
        if (_hint != null)
        {
            _hint.Text = HintText;
            _hint.Visible = false;
        }

        // 用 FindChild 而不是 CurrentScene，避免启动方式不同导致 null
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        if (_player == null) GD.PushWarning("[Door] 没找到玩家 box");
    }

    public override void _Process(double delta)
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        // 冷却倒计时：不计帧率影响，用 delta 累减
        if (_coolDown > 0f) _coolDown = Mathf.Max(0f, _coolDown - (float)delta);

        // 距离判断：门和玩家两点之间的距离
        bool was = _inRange;
        _inRange = GlobalPosition.DistanceTo(_player.GlobalPosition) <= InteractDistance;

        // 提示只在这扇门「可用」时显示
        if (_hint != null)
        {
            bool wantShow = _inRange && _coolDown <= 0f;
            if (wantShow != _hint.Visible) _hint.Visible = wantShow;
        }

        // 对话进行中不响应 E，否则会一边说话一边被传走
        if (GameState.I != null && GameState.I.DialogueActive) return;

        if (_inRange && _coolDown <= 0f && Input.IsActionJustPressed("interact"))
        {
            Go();
        }
    }

    private void Go()
    {
        string path = TargetScene.Trim();

        // ── 模式一：同场景传送（TargetScene 留空）──
        if (string.IsNullOrEmpty(path))
        {
            if (TargetSpawn == Vector2.Zero)
            {
                GD.PushWarning("[Door] 同场景传送需要填 TargetSpawn");
                return;
            }

            _player.GlobalPosition = TargetSpawn;
            _player.Velocity = Vector2.Zero;
            ApplyCameraLimit();

            if (_hint != null) _hint.Visible = false;
            _coolDown = Cooldown;   // 冷却期内不再响应，防止落地瞬间被同一扇门弹回
            GD.Print($"[Door] 已传送到 {TargetSpawn}");
            return;
        }

        // ── 模式二：跨场景传送 ──
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[Door] 场景不存在：{path}");
            return;
        }

        var gs = GameState.I;
        if (gs != null)
        {
            // 切场景会销毁玩家实例，血量 / 出生点 / 武器都要带走
            gs.PrepareTravel(_player.Hp, TargetSpawn,
                             _player.Weapons, _player.CurrentSlot);
        }

        if (_hint != null) _hint.Visible = false;
        GetTree().ChangeSceneToFile(path);
    }

    /// <summary>改写玩家摄像机的可视边界，让它适配新区域。</summary>
    private void ApplyCameraLimit()
    {
        if (!ChangeCameraLimit) return;
        if (CamLeft >= CamRight || CamTop >= CamBottom) return;   // 参数没填好就不动

        var cam = _player.GetNodeOrNull<Camera2D>("Camera2D");
        if (cam == null)
        {
            GD.PushWarning("[Door] 玩家没有 Camera2D，无法切换边界");
            return;
        }

        cam.LimitLeft = CamLeft;
        cam.LimitTop = CamTop;
        cam.LimitRight = CamRight;
        cam.LimitBottom = CamBottom;
        cam.ResetSmoothing();       // 立刻归位，避免镜头从旧位置滑过来
    }
}
