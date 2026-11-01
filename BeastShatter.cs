using Godot;

/// <summary>
/// 野兽苹果的死亡演出：本体碎裂成渣，四分五裂。
///
/// 素材是一张 6 帧横排的序列图（assets/boss/beast/beast_shatter.png，
/// 每帧 750×750，纯黑底已抠成透明），由 Seedream 生成：
///   1 完整但布满裂纹 → 2 从中间裂成两半 → 3 崩成 4~5 块大碎片
///   → 4 碎片继续拉开 → 5 化成碎渣飞散 → 6 只剩余烬
///
/// 为什么单独开一个节点、不挂在 Boss 身上：
///   Boss 本体在爆炸那一刻就要「消失」（贴图藏掉），碎裂是独立演出的东西，
///   放一起的话没法让本体先没、碎片继续飞。
///
/// 缩放按「一帧的高度 = 本体高度」算，所以碎片的大小和本体完全对得上，
/// 不会出现「200 高的 Boss 炸出 400 高的碎片」这种破功。
/// </summary>
public partial class BeastShatter : Node2D
{
    [Export] public Texture2D Sheet;          // 横向序列图（留空则什么也不画）
    [Export] public int Frames = 6;
    [Export] public float FrameTime = 0.1f;  // 每帧多久，6 帧共 0.6 秒
    [Export] public float TargetHeight = 200f;// 一帧在屏幕上多高（对齐 Boss 的 BodyHeight）
    [Export] public float FlashTime = 0.08f;  // 开头那一下白闪

    private Sprite2D _sprite;
    private float _t;

    public override void _Ready()
    {
        ZIndex = 13;   // 压在冲击波和碎片之上

        if (Sheet == null) { QueueFree(); return; }

        float s = TargetHeight / Mathf.Max(1f, Sheet.GetHeight());
        _sprite = new Sprite2D
        {
            Texture = Sheet,
            Hframes = Mathf.Max(1, Frames),
            Frame = 0,
            Scale = new Vector2(s, s),
        };
        AddChild(_sprite);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;

        int f = Mathf.FloorToInt(_t / Mathf.Max(0.01f, FrameTime));
        if (f >= Frames) { QueueFree(); return; }
        if (_sprite != null) _sprite.Frame = f;

        QueueRedraw();
    }

    public override void _Draw()
    {
        // 白闪：碎裂的第一瞬间整个亮一下，让「炸开」有冲击力
        if (_t < FlashTime)
        {
            float a = 1f - _t / Mathf.Max(0.01f, FlashTime);
            DrawCircle(Vector2.Zero, TargetHeight * 0.55f, new Color(1f, 0.95f, 0.88f, a * 0.7f));
        }
    }
}
