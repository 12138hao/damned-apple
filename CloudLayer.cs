using Godot;
using System.Collections.Generic;

/// <summary>
/// 会飘的云层。云是从 `assets/jungle/bg/cloud_1..4.png` 读的像素素材（Seedream 出的，
/// 每朵形状不同、有受光面/背光面），不是代码画的，所以能跟桥、瀑布那些美术对上。
///
/// 深度感靠三件事叠出来：
///   1. 越靠上（y 越小）的云越小、越淡 —— 视觉上更远；
///   2. 每朵随机 0.8~1.2 倍缩放和随机左右翻转，避免看出是同一朵；
///   3. 各自不同速度向右漂，飘出右边界从左边绕回来。
///
/// 层级：天空 -100，云 -95，瀑布 -90。所以云在天空之上、岩壁和瀑布之下。
/// </summary>
public partial class CloudLayer : Node2D
{
    [ExportGroup("范围")]
    [Export] public float Length = 3600f;      // 云只在这段范围内循环
    [Export] public float TopY = -120f;        // 云带上边界（世界 Y）
    [Export] public float BottomY = 300f;      // 云带下边界
    [Export] public int Count = 14;

    [ExportGroup("运动")]
    [Export] public float SpeedMin = 3f;       // 每秒漂多少像素
    [Export] public float SpeedMax = 11f;

    [ExportGroup("外观")]
    [Export] public float CloudScale = 0.5f;   // 原画是 2px 大像素，0.5 = 屏幕像素 1:1
    [Export] public int Seed = 517;
    [Export] public float Opacity = 0.9f;

    // 云在自己这一段的两端淡出，别飘出去。
    // 以前是飘到 x = -width 才绕回，云会越过本段左边界、钻进**上一段场景**里，
    // 而云的 z(-95) 比上一段的背景(-100) 高 —— 看起来就是"云从墙里穿过去"。
    [Export] public float EdgeFade = 170f;     // 离本段左右边界多远开始淡出（像素）

    private readonly List<(Sprite2D sprite, float speed, float width, float baseAlpha)> _clouds = new();

    private static readonly string[] CloudPaths =
    {
        "res://assets/jungle/bg/cloud_1.png",
        "res://assets/jungle/bg/cloud_2.png",
        "res://assets/jungle/bg/cloud_3.png",
        "res://assets/jungle/bg/cloud_4.png",
    };

    public override void _Ready()
    {
        var textures = new List<Texture2D>();
        foreach (string p in CloudPaths)
        {
            var t = GD.Load<Texture2D>(p);
            if (t != null) textures.Add(t);
        }
        if (textures.Count == 0)
        {
            GD.PushWarning("[CloudLayer] 一张云贴图都没加载到，云层跳过");
            return;
        }

        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Seed;

        // 等分槽位再在槽内抖动。纯随机会出现"某一段一朵都没有、另一段挤成一堆"，
        // 玩家一停就正对着一片空天。按槽位放保证每个横屏窗口都有云。
        float slot = Count > 0 ? Length / Count : Length;

        for (int i = 0; i < Count; i++)
        {
            var tex = textures[rng.RandiRange(0, textures.Count - 1)];

            // y 归一化：0 = 云带顶部（远），1 = 底部（近）
            float y = rng.RandfRange(TopY, BottomY);
            float depth = Mathf.IsEqualApprox(BottomY, TopY) ? 0.5f
                        : Mathf.Clamp((y - TopY) / (BottomY - TopY), 0f, 1f);

            float k = CloudScale * Mathf.Lerp(0.8f, 1.2f, depth);
            float w = tex.GetWidth() * k;

            float baseAlpha = Opacity * Mathf.Lerp(0.55f, 1f, depth);
            var s = new Sprite2D
            {
                Name = $"Cloud_{i}",
                Texture = tex,
                Scale = new Vector2(k, k),
                Position = new Vector2(slot * i + rng.RandfRange(0.05f, 0.95f) * slot, y),
                FlipH = rng.Randf() > 0.5f,
                ZIndex = -95,
                Modulate = new Color(1f, 1f, 1f, baseAlpha),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            AddChild(s);
            _clouds.Add((s, rng.RandfRange(SpeedMin, SpeedMax), w, baseAlpha));
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float fade = Mathf.Max(1f, EdgeFade);

        foreach (var (sprite, speed, width, baseAlpha) in _clouds)
        {
            if (!IsInstanceValid(sprite)) continue;

            var p = sprite.Position;
            p.X += speed * dt;

            // 只在本段 [0, Length] 里绕回。以前绕回点是 -width，
            // 云会跑到上一段场景的地盘上（z 还比人家高），看着像穿墙。
            if (p.X > Length) p.X -= Length;
            if (p.X < 0f) p.X += Length;
            sprite.Position = p;

            // 越靠近本段两端越透明：绕回的瞬间透明度是 0，看不出"啪"地跳过去
            float edge = Mathf.Min(p.X, Length - p.X);
            float a = Mathf.Clamp(edge / fade, 0f, 1f);
            sprite.Modulate = new Color(1f, 1f, 1f, baseAlpha * a);
        }
    }
}
