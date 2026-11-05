using Godot;

/// <summary>
/// 到达即触发的传送点（不用按键）。
/// 挂在 Area2D 上，玩家身体进入判定区就切场景。
///
/// 和 Door 的区别：Door 要按 E，这个踩上去就走，适合做「绳索顶端通往下一关」这类节点。
///
/// TargetSpawn 填 Vector2.Zero 表示「不指定落点」，让目标场景用它自己摆的出生点。
/// 这一点很关键：GameState.PrepareTravel 收到 (0,0) 会把玩家扔到世界原点，
/// 所以零向量时要手动把 PendingSpawn 清成 null。
/// </summary>
public partial class AutoPortal : Area2D
{
    [Export] public string TargetScene = "res://firstry.tscn";  // 目标场景
    [Export] public Vector2 TargetSpawn = Vector2.Zero;         // 零向量 = 用目标场景自己的出生点
    [Export] public float Cooldown = 1.0f;                      // 触发后多久不再响应（防止连点）

    /// <summary>
    /// 走这个门进目标场景时，要不要播「从古井爬上来」的入场动画。
    /// 打开后目标场景里的 WellExit 会接手：把玩家塞进井里，演一段爬出来的过程。
    /// </summary>
    [Export] public bool WellEntry = false;

    private bool _fired;
    private float _coolDown;

    public override void _Ready()
    {
        Monitoring = true;
    }

    public override void _Process(double delta)
    {
        if (_coolDown > 0f)
        {
            _coolDown = Mathf.Max(0f, _coolDown - (float)delta);
            return;
        }
        if (_fired) return;

        // 对话中不传送，避免一句话没说完就被拉走
        if (GameState.I != null && GameState.I.DialogueActive) return;

        // 轮询而不是用 BodyEntered 信号：玩家爬到顶时是「松手上抛」，
        // 有可能在同一物理帧里既进入又离开，信号会漏掉。
        foreach (var body in GetOverlappingBodies())
        {
            if (body is Box player)
            {
                Fire(player);
                return;
            }
        }
    }

    private void Fire(Box player)
    {
        string path = TargetScene.Trim();
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[AutoPortal] 场景不存在：{path}");
            return;
        }

        _fired = true;
        _coolDown = Cooldown;

        var gs = GameState.I;
        if (gs != null)
        {
            // 血量 / 武器要跟着走，否则传送一次就变回满血空手
            gs.PrepareTravel(player.Hp, TargetSpawn, player.Weapons, player.CurrentSlot);

            // 零向量 = 不指定落点，让目标场景用它自己摆的出生点
            if (TargetSpawn == Vector2.Zero) gs.PendingSpawn = null;

            // 标记「这次是从井里上来的」，目标场景的 WellExit 会取走并演动画
            if (WellEntry) gs.MarkWellEntry();
        }

        GD.Print($"[AutoPortal] 触发传送 -> {path}  落点={TargetSpawn}");
        GetTree().ChangeSceneToFile(path);
    }
}
