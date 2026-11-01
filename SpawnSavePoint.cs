using Godot;

/// <summary>
/// 入口存档点：玩家从别的地图掉进来、**真正经过它**的时候自动存一次。
///
/// 和 RevivePoint 的分工（两者必须共存，不能互相顶掉）：
///   SpawnSavePoint  只管「刚进这张图」这一下，保证死了不会被打回上一张图
///   RevivePoint     管「本图内部推进」的进度，按 E 才存，存完消失
///
/// 用法：在 underground 场景里加一个 Node2D（比如 SpawnSave），
/// 挂上这个脚本，摆在**入口落点的正下方**（脚踩的那条地面线，不是玩家中心），
/// 然后把 TriggerRadius 调到「玩家从上面掉下来一定会进圈」的大小。
///
/// 三条硬规则（少一条就会和 RevivePoint 打架）：
///   1. 不是「进场景就存」，而是「玩家进半径才存」——否则从存档点复活重载场景时也会被它覆盖
///   2. 本场景已经在别的存档点存过 → 直接放弃，绝不覆盖
///   3. 存完立刻 SetProcess(false)，一辈子只跑一次
///
/// 为什么第 2 条这么重要：RevivePoint 用「GameState.RevivePointKey 是不是自己」判断该不该显示。
/// 如果这里把 key 改成自己的，RevivePoint 会以为「别的存档点被激活了」→ 重新亮起来。
/// </summary>
public partial class SpawnSavePoint : Node2D
{
    [ExportGroup("自动存档")]
    [Export] public bool Enable = true;                 // 关掉就不生效
    [Export] public bool SaveOnEveryEnter = true;       // false = 本次生命周期内只存一次
    // 玩家离存档点多近才算「碰到了」。入口落点在正上方，一般 90~140 够用。
    // 设成 0 或负数 = 退回旧行为（进场景第一帧就存），不建议。
    [Export] public float TriggerRadius = 120f;
    // 【已由 TriggerRadius 取代】旧的距离保险，只在 TriggerRadius <= 0 时还起作用。
    // 保留这个字段是因为 underground.tscn 里写了 MaxSaveDistance = 600，删掉会报属性不存在。
    [Export] public float MaxSaveDistance = 0f;
    [Export] public bool ShowBubble = false;            // 存档时要不要冒个气泡提示
    [Export] public string SaveText = "已存档";          // 气泡内容（ShowBubble 打开才有效果）

    private bool _saved;

    public override void _Ready()
    {
        if (!Enable) SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (!Enable) { SetProcess(false); return; }
        if (_saved && !SaveOnEveryEnter) { SetProcess(false); return; }

        var player = GetTree().Root.FindChild("box", true, false) as Box;
        if (player == null || !IsInstanceValid(player)) return;   // 玩家还没准备好，下一帧再看

        // ① 必须真的走到跟前。玩家从入口掉下来的过程中会经过这里，
        //    而从存档点复活时出生在别处，根本不会进圈。
        float dist = player.GlobalPosition.DistanceTo(GlobalPosition);
        if (TriggerRadius > 0f)
        {
            if (dist > TriggerRadius) return;
        }
        else if (MaxSaveDistance > 0f && dist > MaxSaveDistance)
        {
            return;   // 旧逻辑兜底
        }

        string scenePath = Owner?.SceneFilePath
                           ?? GetTree().CurrentScene?.SceneFilePath
                           ?? "";
        if (string.IsNullOrEmpty(scenePath))
        {
            GD.PushWarning("[SpawnSave] 拿不到场景路径，自动存档跳过");
            SetProcess(false);
            return;
        }

        // ② 本场景已经在别的存档点存过了 → 绝不覆盖。
        //    否则复活点被改回入口，RevivePoint 还会因为 key 变了重新亮起来。
        var gs = GameState.I;
        string myKey = scenePath + "::" + GetPath();
        if (gs != null
            && gs.ReviveScenePath == scenePath
            && !string.IsNullOrEmpty(gs.RevivePointKey)
            && gs.RevivePointKey != myKey)
        {
            GD.Print($"[SpawnSave] 本场景已在别处存档（{gs.RevivePointKey}），入口存档跳过");
            SetProcess(false);
            return;
        }

        DoSave(player, scenePath, myKey);
        _saved = true;
        SetProcess(false);   // ③ 一辈子只跑一次
    }

    private void DoSave(Box player, string scenePath, string myKey)
    {
        // ① 本地复活点（本场景死亡时用）
        player.SetRevivePoint(GlobalPosition);

        // ② 跨场景复活信息（别的场景死亡时用）
        var gs = GameState.I;
        if (gs != null)
        {
            gs.RevivePointKey = myKey;
            gs.ReviveScenePath = scenePath;
            gs.RevivePosition = GlobalPosition;
        }

        GD.Print($"[SpawnSave] 已在入口自动存档：{scenePath} @ {GlobalPosition}");

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
