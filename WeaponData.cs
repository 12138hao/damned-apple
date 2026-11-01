using Godot;

/// <summary>
/// 一种武器的全部参数。
/// 用 Resource 是为了能在编辑器里建 .tres 文件配置，加新武器不用改代码。
/// </summary>
[GlobalClass]
public partial class WeaponData : Resource
{
    [ExportGroup("基本信息")]
    [Export] public string DisplayName = "武器";

    [ExportGroup("伤害")]
    [Export] public int DamageMin = 20;        // 不蓄力时的伤害
    [Export] public int DamageMax = 40;        // 蓄满时的伤害

    [ExportGroup("击退")]
    [Export] public float KnockbackMin = 180f;  // 不蓄力时把怪物打退多远
    [Export] public float KnockbackMax = 700f;  // 蓄满时的击退
    [Export] public float KnockbackUpRatio = 0.32f; // 击退的垂直分量比例（越大越往上飞）

    [ExportGroup("挥击手感")]
    [Export] public float AttackTime = 0.22f;    // 一次挥击持续多久
    [Export] public float AttackSweepDeg = 120f; // 挥击扫过的角度
    [Export] public float StartDeg = -75f;       // 挥击起始角度

    [ExportGroup("攻击距离")]
    // 攻击判定框按这个倍率放大，位置也跟着外移。
    // 木棒 = 1，新武器想要 1.5 倍就填 1.5。
    [Export] public float RangeScale = 1f;

    [ExportGroup("蓄满突刺")]
    [Export] public bool ChargeThrust = false;       // 蓄力满时是否变成突刺
    [Export] public float ThrustRangeScale = 2f;     // 突刺时的距离倍率
    [Export] public float ThrustWidth = 10f;         // 突刺判定框的宽度（越小越像一条线）
    [Export] public float ThrustThreshold = 0.95f;   // 蓄力到多少算"满"
    [Export] public float ThrustReach = 20f;         // 突刺时武器往前送出去多少像素

    [ExportGroup("外观")]
    [Export] public Texture2D Icon;              // 手上/地上的贴图

    // 贴图朝向补偿：贴图本身不是"正右指向"时要加。
    // 木棒/长枪的贴图是斜 45°（左下握把 → 右上枪头），所以填 45。
    // 横向的贴图（握把在左、尖端在右）填 0。
    [Export] public float AngleOffset = 45f;

    // 握把在贴图里的位置偏移（未缩放时）。填法：
    //   (贴图宽/2 - 握把x, 贴图高/2 - 握把y)
    // 例：48×48 图、握把在 (7,40) → (24-7, 24-40) = (17, -16)
    [Export] public Vector2 SpriteOffset = new Vector2(17f, -16f);

    // 贴图缩放。武器比攻击距离长就调小，短就调大。
    [Export] public float SpriteScale = 1f;

    [ExportGroup("剑气（补足贴图不够长的部分）")]
    // 贴图视觉长度 < 攻击距离时，多出来的那段用剑气表现。
    // AuraLength = 0 就不画。
    [Export] public float AuraLength = 0f;      // 剑气长度
    [Export] public float AuraStartDist = 38f;  // 从武器尖端往外多少像素开始

    // ─────────────────────────── 远程武器（粉笔这类）
    // 勾上 IsRanged 后，这把武器**不再走挥击判定**（不用 RangeScale / 蓄力 / 突刺），
    // 改成：按住左键 → 按 Cooldown 节奏发射投射物，投射物飞满 MaxDistance 消失。
    [ExportGroup("远程武器")]
    // 勾上 = 远程武器。这里写成属性而不是普通字段，是为了在勾选的瞬间刷新检查器：
    // 远程武器根本不读 DamageMin / DamageMax，勾上后这两个字段会被下面的
    // _ValidateProperty 直接藏掉，避免改了没反应还以为伤害没导出。
    private bool _isRanged = false;
    [Export]
    public bool IsRanged
    {
        get => _isRanged;
        set
        {
            if (_isRanged == value) return;
            _isRanged = value;
            NotifyPropertyListChanged();
        }
    }
    [Export] public int RangedDamage = 30;          // 每一发的伤害
    [Export] public float RangedSpeed = 900f;       // 投射物飞行速度（px/s）
    [Export] public float RangedMaxDistance = 400f; // 最远飞多远就消失（px）
    /// <summary>
    /// 内置发射冷却（秒）。从「起手」那一刻开始计时，所以实际射速就等于这个数，
    /// 不会变成「冷却 + 挥臂时长」。
    /// 填到 RangedWindup 以下没有意义：会被 Mathf.Max 自动抬回 RangedWindup（动作还没做完发不出下一发）。
    /// 粉笔盒外面那圈冷却弧的长度也按它算：弧长 = 冷却秒数 × RangedCooldownArc.DegPerSecond。
    /// </summary>
    [Export] public float RangedCooldown = 1.5f;
    [Export] public float RangedWindup = 0.22f;     // 挥臂动作时长（到 55% 时射出）
    [Export] public Texture2D ProjectileIcon;       // 投射物贴图（留空则用 Icon）
    [Export] public float ProjectileScale = 0.5f;   // 投射物贴图缩放（同时决定长度和基础粗细）
    [Export] public float ProjectileThickness = 1.4f; // 只加粗不加长：横向额外放大这么多倍
    [Export] public float ProjectileRadius = 5f;    // 投射物命中判定半径
    [Export] public float ProjectileKnockback = 160f; // 命中后把敌人推多远
    [Export] public float ProjectileSpawnDist = 22f;  // 从手前方多远处生成（避免贴脸时生成在身后）

    /// <summary>
    /// 检查器里显示哪些字段的最后一道闸。
    /// 远程武器（IsRanged = true）走的是 RangedDamage，不读 DamageMin / DamageMax，
    /// 所以把这两个从检查器里去掉——留着只会让人改了没反应。
    /// 只摘掉 Editor 标志位，Storage 保留，.tres 里已经写好的值不受影响。
    /// </summary>
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (!_isRanged) return;

        string name = property["name"].AsString();
        if (name != "DamageMin" && name != "DamageMax") return;

        long usage = property["usage"].AsInt64();
        property["usage"] = usage & ~(long)PropertyUsageFlags.Editor;
    }
}
