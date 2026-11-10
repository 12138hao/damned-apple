using Godot;

/// <summary>
/// 全局战斗数值：不挂在任何场景上，所有地图共用同一份 assets/balance/combat.tres。
///
/// 为什么要有这个：以前这些数字是 Box 上的 [Export]，而 Box 是每个场景各一个，
/// 在 A 图的 Box 检查器里改了，B 图根本不知道 —— 表现就是"改了没生效"。
/// 武器伤害（木棒/尺子/粉笔）现在放在各自 assets/weapons/*.tres 里；
/// 这里只放"没有武器资源"的项（空手出拳）。
///
/// 用法：<c>CombatBalance.I.FistDamage</c>。文件缺失时会用代码里的默认值兜底。
/// </summary>
[GlobalClass]
public partial class CombatBalance : Resource
{
    private const string ResPath = "res://assets/balance/combat.tres";
    private static CombatBalance _instance;

    /// <summary>全局唯一实例（首次访问时加载 .tres）。</summary>
    public static CombatBalance I => _instance ??= GD.Load<CombatBalance>(ResPath) ?? new CombatBalance();

    [ExportGroup("空手出拳")]
    [Export] public int FistDamage = 8;         // 第一拳伤害（连击最后一拳翻倍，由 Box 内部决定）
}
