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
}
