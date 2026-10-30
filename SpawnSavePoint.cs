using Godot;

/// <summary>
/// 进入场景即自动存档的出生点。
///
/// 和 RevivePoint 的区别：
///   RevivePoint  玩家走到跟前按 E 才存，存完自己消失
///   SpawnSavePoint 玩家一进场景就自动存，不需要按键、不消失、可反复触发
///
/// 用法：在 underground 场景里加一个 Node2D 节点（命名随意，比如 SpawnSave），
/// 挂上这个脚本，把它摆到出生点的地面上（不是玩家中心，是脚踩的那条线），
/// 然后把玩家的初始 position 摆到差不多同一个位置。
///
/// 注意它只改「复活点」，不回血、不播音效——回血是 RevivePoint 的行为，
/// 这里进场景时玩家本来就该是满血/带过来的血量。
/// </summary>
public partial class SpawnSavePoint : Node2D
{
    [ExportGroup("自动存档")]
    [Export] public bool Enable = true;                 // 关掉就不生效
    [Export] public bool SaveOnEveryEnter = true;       // true = 每次进场景都覆盖存档
    // >0 时：玩家出生点离这里超过这么远就**不存**（0 = 不限制）。
    // 用来区分「从这里进入本场景」和「从别的地图走门回来」——
    // 后者出生点离得远，不该把玩家在别处存的档覆盖成这个入口。
    [Export] public float MaxSaveDistance = 0f;
    [Export] public bool ShowBubble = false;            // 存档时要不要冒个气泡提示
    [Export] public string SaveText = "已存档";          // 气泡内容（ShowBubble 打开才有效果）

    private bool _saved;
    private bool _pending = true;   // 等一帧再存，见 _Process

    public override void _Ready()
    {
        if (!Enable)
        {
            SetProcess(false);
            return;
        }
    }

    /// <summary>
    /// 用一帧的延迟来「等玩家准备好」：
    /// _Ready 里直接找 box，很可能它还没跑完自己的 _Ready（要从 GameState 读血量），
    /// 而且这时 GetTree().CurrentScene 可能还没定下来，拿到的场景路径是空的。
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_pending) return;
        _pending = false;
        DoSave();
        SetProcess(false);   // 存完就不用每帧跑了
    }

    private void DoSave()
    {
        if (_saved && !SaveOnEveryEnter) return;

        var player = GetTree().Root.FindChild("box", true, false) as Box;
        if (player == null || !IsInstanceValid(player))
        {
            GD.PushWarning("[SpawnSave] 没找到玩家节点 box，自动存档跳过");
            return;
        }

        // 出生点离这里太远 = 玩家不是从这里进来的（比如从房间走门回到大地图），
        // 这时不能覆盖他在别处存的档。
        if (MaxSaveDistance > 0f)
        {
            float dist = player.GlobalPosition.DistanceTo(GlobalPosition);
            if (dist > MaxSaveDistance)
            {
                GD.Print($"[SpawnSave] 出生点距存档点 {dist:F0}px（> {MaxSaveDistance}），保留原有存档");
                return;
            }
        }

        string scenePath = Owner?.SceneFilePath
                           ?? GetTree().CurrentScene?.SceneFilePath
                           ?? "";
        if (string.IsNullOrEmpty(scenePath))
        {
            GD.PushWarning("[SpawnSave] 拿不到场景路径，自动存档跳过");
            return;
        }

        // ① 本地复活点（本场景死亡时用）
        player.SetRevivePoint(GlobalPosition);

        // ② 跨场景复活信息（别的场景死亡时用）
        var gs = GameState.I;
        if (gs != null)
        {
            gs.RevivePointKey = scenePath + "::" + GetPath();
            gs.ReviveScenePath = scenePath;
            gs.RevivePosition = GlobalPosition;
        }

        _saved = true;
        GD.Print($"[SpawnSave] 已在出生点自动存档：{scenePath} @ {GlobalPosition}");

        if (ShowBubble)
        {
            var bubble = player.GetNodeOrNull<DialogueUI>("DialogueUI");
            if (bubble != null)
            {
                bubble.ShowLine(SaveText);
                _ = HideBubbleAfter(bubble, 1.5f);
            }
        }
    }

    private async System.Threading.Tasks.Task HideBubbleAfter(DialogueUI bubble, float sec)
    {
        await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(bubble)) bubble.HideBubble();
    }
}
