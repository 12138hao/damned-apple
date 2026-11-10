using Godot;
using System.Collections.Generic;

/// <summary>
/// 「巨藤上升」过渡场景的纵向背景。
///
/// 和 EdenBackdrop 的区别：那是一张 20000 长的横版地图，这是**一口竖井** ——
/// 玩家从下往上爬，所以视差要按 **Y** 做，不是 X。
///
/// 三层：
///   · 天空     —— 竖着的黄昏渐变（底金 → 中霞粉 → 顶暮紫），跟第四章同一套色；
///   · 云层     —— 顶上那一片"云海"，藤蔓爬到那儿就斜趴在云上了；
///   · 途中云团 —— 沿路飘的云，越往上越密，给"正在往上爬"的空间感。
///
/// 生成的节点不设 Owner，不会写进 .tscn。
/// </summary>
[Tool]
public partial class AscentBackdrop : Node2D
{
    [ExportGroup("尺寸")]
    [Export] public float Width = 2400f;       // 场景宽
    [Export] public float TopY = -400f;        // 背景上边界
    [Export] public float BottomY = 4800f;     // 背景下边界

    [ExportGroup("黄昏天空")]
    [Export] public Color SkyTopColor = new Color(0.48f, 0.42f, 0.68f, 1f);     // 高处：暮紫
    [Export] public Color SkyMidColor = new Color(0.96f, 0.64f, 0.58f, 1f);     // 中段：霞粉
    [Export] public Color SkyBottomColor = new Color(1f, 0.86f, 0.62f, 1f);     // 低处：金
    [Export] public float HorizonStart = 900f;      // 从这个 Y 开始转霞粉
    [Export] public float HorizonEnd = 4200f;       // 到这里完全变金（贴着下部地面）

    [ExportGroup("落日")]
    [Export] public bool DrawSun = true;
    [Export] public Vector2 SunPos = new Vector2(300f, 4300f);   // 世界坐标
    [Export] public float SunRadius = 320f;
    [Export] public Color SunCoreColor = new Color(1f, 0.96f, 0.80f, 1f);
    [Export] public Color SunGlowColor = new Color(1f, 0.72f, 0.42f, 1f);

    [ExportGroup("顶部云海")]
    [Export] public bool DrawCloudSea = true;
    [Export] public float CloudSeaTopY = 1560f;    // 云海草皮面（藤蔓就趴在这上面）
    [Export] public float FloorLeftX = 880f;       // 云海（地板）左边缘：左边是藤蔓爬上来的断崖
    [Export] public float CloudSeaLeftX = -600f;   // 云海实际画到哪儿：往画面外多画一截，免得露硬竖直切口
    [Export] public float CloudSeaBottomY = 2600f; // 云海底边

    [ExportGroup("途中云团")]
    [Export] public bool DrawClouds = true;
    [Export] public int CloudCount = 70;
    [Export] public float CloudTopY = 700f;        // 云团分布范围（一部分飘在地板之上的天空里）
    [Export] public float CloudBottomY = 4600f;
    [Export] public float CloudFactor = 0.86f;

    [ExportGroup("远处巨藤")]
    [Export] public bool DrawFarVines = true;
    [Export] public int FarVineCount = 5;

    [ExportGroup("整体")]
    [Export] public float Brightness = 1.05f;
    [Export] public bool EnableParallax = true;

    private const string BeanPath = "res://beanstalk_2000x4000.png";
    private const string GroundPath = "res://assets/eden/ground.png";
    private static readonly string[] CloudPaths =
    {
        "res://assets/jungle/bg/cloud_1.png",
        "res://assets/jungle/bg/cloud_2.png",
        "res://assets/jungle/bg/cloud_3.png",
        "res://assets/jungle/bg/cloud_4.png",
    };

    private readonly List<Node2D> _layers = new();
    private float _anchorY;

    // 云贴图底边是平切的，堆成云海会露出硬边 → 底部做 alpha 渐隐，结果缓存
    private static readonly Dictionary<string, Texture2D> _softCache = new();
    private string _lastSignature = "";

    public override void _Ready()
    {
        _lastSignature = Signature();
        Rebuild();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            string sig = Signature();
            if (sig != _lastSignature) { _lastSignature = sig; Rebuild(); }
            return;
        }

        if (!EnableParallax) return;
        var cam = GetViewport()?.GetCamera2D();
        if (cam == null) return;

        float dy = cam.GlobalPosition.Y + cam.Offset.Y - _anchorY;
        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            if (layer == null || !IsInstanceValid(layer)) continue;
            float f = layer.GetMeta("pf", 1f).AsSingle();
            layer.Position = new Vector2(0f, dy * (1f - f));
        }
    }

    private string Signature()
    {
        return string.Join("|", Width, TopY, BottomY,
            SkyTopColor, SkyMidColor, SkyBottomColor, HorizonStart, HorizonEnd,
            DrawSun, SunPos, SunRadius, SunCoreColor, SunGlowColor,
            DrawCloudSea, CloudSeaTopY, FloorLeftX, CloudSeaLeftX, CloudSeaBottomY,
            DrawClouds, CloudCount, CloudTopY, CloudBottomY, CloudFactor,
            DrawFarVines, FarVineCount, Brightness, EnableParallax);
    }

    private Color Mul(Color c) => new Color(c.R * Brightness, c.G * Brightness, c.B * Brightness, c.A);

    private void Rebuild()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        _layers.Clear();
        _anchorY = (TopY + BottomY) * 0.5f;

        BuildSky();
        if (DrawSun) BuildSun();
        if (DrawFarVines) BuildFarVines();
        if (DrawClouds) BuildClouds();
        if (DrawCloudSea) BuildCloudSea();
    }

    private Node2D NewLayer(string name, int z, float factor)
    {
        var root = new Node2D { Name = name, ZIndex = z };
        root.SetMeta("pf", factor);
        Spawn(root);
        _layers.Add(root);
        return root;
    }

    /// <summary>竖着的三段渐变天空。</summary>
    private void BuildSky()
    {
        const int N = 384;
        var img = Image.CreateEmpty(4, N, false, Image.Format.Rgba8);

        for (int y = 0; y < N; y++)
        {
            float t = (float)y / (N - 1);
            float wy = TopY + (BottomY - TopY) * t;

            // 和 EdenBackdrop 同一个思路：把"霞粉"卡在中间，两头才是紫和金
            float k = Mathf.Clamp((wy - HorizonStart) / Mathf.Max(1f, HorizonEnd - HorizonStart), 0f, 1f);
            k = Mathf.SmoothStep(0f, 1f, k);

            Color c;
            if (k < 0.5f) c = SkyTopColor.Lerp(SkyMidColor, Mathf.SmoothStep(0f, 1f, k / 0.5f));
            else c = SkyMidColor.Lerp(SkyBottomColor, Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f));

            for (int x = 0; x < 4; x++) img.SetPixel(x, y, Mul(c));
        }

        var s = new Sprite2D
        {
            Name = "Sky",
            Texture = ImageTexture.CreateFromImage(img),
            Centered = false,
            Position = new Vector2(-600f, TopY),
            Scale = new Vector2((Width + 1200f) / 4f, (BottomY - TopY) / N),
            ZIndex = -100,
            LightMask = 0,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        Spawn(s);
    }

    private void BuildSun()
    {
        const int S = 256;
        var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f) / (S * 0.5f) - 1f;
                float ny = (y + 0.5f) / (S * 0.5f) - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);

                float glow = Mathf.Clamp(1f - d, 0f, 1f);
                glow = glow * glow * glow;
                float disc = Mathf.Clamp(1f - (d - 0.30f) / 0.05f, 0f, 1f);

                var c = SunGlowColor.Lerp(SunCoreColor, disc);
                c.A = Mathf.Clamp(Mathf.Max(glow * 0.85f, disc), 0f, 1f);
                img.SetPixel(x, y, Mul(c));
            }
        }

        var s = new Sprite2D
        {
            Name = "Sun",
            Texture = ImageTexture.CreateFromImage(img),
            Centered = true,
            Position = SunPos,
            Scale = Vector2.One * (SunRadius * 2f / S),
            ZIndex = -99,
            LightMask = 0,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        Spawn(s);
    }

    /// <summary>远处隐约几根巨藤，拉开纵深（不参与碰撞，纯背景）。</summary>
    private void BuildFarVines()
    {
        var tex = GD.Load<Texture2D>(BeanPath);
        if (tex == null || FarVineCount <= 0) return;

        var root = NewLayer("FarVines", -92, 0.88f);
        var rng = new RandomNumberGenerator { Seed = 5150 };

        for (int i = 0; i < FarVineCount; i++)
        {
            float x = rng.RandfRange(-200f, Width + 200f);
            float y = rng.RandfRange(1200f, 4200f);
            float sc = rng.RandfRange(0.18f, 0.3f);

            var s = new Sprite2D
            {
                Name = $"FarVine_{i}",
                Texture = tex,
                Centered = true,
                Position = new Vector2(x, y),
                Scale = new Vector2(sc, sc),
                Modulate = Mul(new Color(0.72f, 0.80f, 0.86f, 0.38f)),
                FlipH = rng.Randf() < 0.5f,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(s);
        }
    }

    /// <summary>沿路飘的云团。越靠上越密，暗示"快到云层了"。</summary>
    private void BuildClouds()
    {
        var texs = new List<Texture2D>();
        foreach (var p in CloudPaths)
        {
            var t = GD.Load<Texture2D>(p);
            if (t != null) texs.Add(t);
        }
        if (texs.Count == 0 || CloudCount <= 0) return;

        var root = NewLayer("Clouds", -94, CloudFactor);
        var rng = new RandomNumberGenerator { Seed = 8282 };

        for (int i = 0; i < CloudCount; i++)
        {
            int ti = rng.RandiRange(0, texs.Count - 1);
            var tex = FeatherEdges(CloudPaths[ti], texs[ti], 0.12f);
            // 用一个偏向底部的分布：越往上越容易出云
            float t = rng.Randf();
            float y = Mathf.Lerp(CloudTopY, CloudBottomY, t);
            float sc = rng.RandfRange(0.6f, 1.4f);

            var s = new Sprite2D
            {
                Name = $"Cloud_{i}",
                Texture = tex,
                Centered = true,
                Position = new Vector2(rng.RandfRange(-300f, Width + 300f), y),
                Scale = new Vector2(sc, sc),
                Modulate = Mul(new Color(1f, 0.93f, 0.90f, 0.85f)),
                FlipH = rng.Randf() < 0.5f,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(s);
        }
    }

    /// <summary>
    /// 顶上那片云海：藤蔓爬到这儿就斜趴上去，也是通往伊甸园的门槛。
    ///
    /// 不用一堆 cloud 贴片平铺 —— 那些贴片边缘是平的，堆起来会露出方块状的拼接痕迹。
    /// 这里直接拿 **eden 的地面带**（草皮 + 云）横向镜像平铺当"云面"，
    /// 和第四章同一张图：玩家从藤上踩上去，脚下就是伊甸园的地面，接得毫无缝隙。
    /// 云面下面再叠两层"只取云"的裁片，把厚度撑出来。
    /// </summary>
    private void BuildCloudSea()
    {
        var ground = GD.Load<Texture2D>(GroundPath);
        if (ground == null) return;

        var root = NewLayer("CloudSea", -88, 0.95f);
        var rng = new RandomNumberGenerator { Seed = 3737 };

        const float sc = 0.5f;
        float tw = ground.GetWidth() * sc;
        float th = ground.GetHeight() * sc;
        float top = CloudSeaTopY - 15f;      // 草尖比行走线高 15px

        for (float x = CloudSeaLeftX; x < Width + tw; x += tw)
        {
            int i = Mathf.RoundToInt(x / tw);
            var sp = new Sprite2D
            {
                Name = $"SeaTop_{i}",
                Texture = ground,
                Centered = false,
                Position = new Vector2(x, top),
                Scale = new Vector2(sc, sc),
                FlipH = ((i % 2) + 2) % 2 == 1,
                Modulate = Mul(new Color(1f, 0.98f, 0.95f, 1f)),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(sp);
        }

        var cloudOnly = CropRegion(ground, new Rect2I(0, 150, ground.GetWidth(), ground.GetHeight() - 150));
        if (cloudOnly != null)
        {
            float cw = cloudOnly.GetWidth() * sc;
            float ch = cloudOnly.GetHeight() * sc;
            for (int row = 1; row <= 2; row++)
            {
                float y = top + th - 8f + (row - 1) * ch;
                for (float x = CloudSeaLeftX; x < Width + cw; x += cw)
                {
                    int i = Mathf.RoundToInt(x / cw) + row;
                    var sp = new Sprite2D
                    {
                        Name = $"SeaFill_{row}_{i}",
                        Texture = cloudOnly,
                        Centered = false,
                        Position = new Vector2(x, y),
                        Scale = new Vector2(sc, sc),
                        FlipH = ((i % 2) + 2) % 2 == 1,
                        FlipV = (row % 2) == 0,   // 上下镜像交替：两行交界处像素天然对齐
                        Modulate = Mul(new Color(0.98f, 0.97f, 0.99f, 1f)),
                        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    };
                    root.AddChild(sp);
                }
            }
        }

        var patch = new ColorRect
        {
            Name = "SeaBase",
            Color = Mul(new Color(0.90f, 0.94f, 0.99f, 1f)),
            Position = new Vector2(CloudSeaLeftX - 60f, CloudSeaBottomY - 200f),
            Size = new Vector2(Width - CloudSeaLeftX + 400f, 900f),
            ZAsRelative = true,
            ZIndex = -1,
        };
        root.AddChild(patch);

        var texs = new List<Texture2D>();
        foreach (var cp in CloudPaths)
        {
            var t = GD.Load<Texture2D>(cp);
            if (t != null) texs.Add(SoftBottom(cp, t, 0.35f));
        }
        if (texs.Count == 0) return;

        for (int i = 0; i < 18; i++)
        {
            var tex = texs[rng.RandiRange(0, texs.Count - 1)];
            float s2 = rng.RandfRange(0.7f, 1.3f);
            var sp = new Sprite2D
            {
                Name = $"SeaPuff_{i}",
                Texture = tex,
                Centered = true,
                Position = new Vector2(rng.RandfRange(-200f, Width + 200f), CloudSeaTopY - rng.RandfRange(20f, 170f)),
                Scale = new Vector2(s2, s2),
                Modulate = Mul(new Color(1f, 0.99f, 0.97f, 0.9f)),
                FlipH = rng.Randf() < 0.5f,
                ZAsRelative = true,
                ZIndex = -2,        // 草皮面之后，不能盖住草地
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(sp);
        }
    }

    /// <summary>从贴图上裁一块出来做成新纹理（把地面图里的"云"单独取出来）。</summary>
    private Texture2D CropRegion(Texture2D src, Rect2I region)
    {
        var img = src.GetImage();
        if (img == null) return null;
        img = (Image)img.Duplicate();
        if (img.IsCompressed()) img.Decompress();
        if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);

        region = region.Intersection(new Rect2I(0, 0, img.GetWidth(), img.GetHeight()));
        if (region.Size.X <= 0 || region.Size.Y <= 0) return null;

        var sub = img.GetRegion(region);
        return ImageTexture.CreateFromImage(sub);
    }

    /// <summary>把贴图底部一段高度做 alpha 渐隐，消掉平切的硬边。</summary>
    /// <summary>
    /// 四边都羽化。云贴片偶尔会把云画到贴图边界上，放大后就是一条硬直边，
    /// 在过场里特别显眼（会看成一块方块贴在天空上）。四边各压一圈透明，任何缩放下都不会露方块。
    /// </summary>
    private Texture2D FeatherEdges(string path, Texture2D src, float fraction)
    {
        string key = $"{path}|edge|{fraction:0.00}";
        if (_softCache.TryGetValue(key, out var cached) && IsInstanceValid(cached)) return cached;

        var img = src.GetImage();
        if (img == null) return src;
        img = (Image)img.Duplicate();
        if (img.IsCompressed()) img.Decompress();
        if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);

        int w = img.GetWidth(), h = img.GetHeight();
        float fx = Mathf.Max(1f, w * Mathf.Clamp(fraction, 0.02f, 0.45f));
        float fy = Mathf.Max(1f, h * Mathf.Clamp(fraction, 0.02f, 0.45f));

        for (int y = 0; y < h; y++)
        {
            float ky = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, Mathf.Min(y, h - 1 - y) / fy));
            for (int x = 0; x < w; x++)
            {
                float kx = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, Mathf.Min(x, w - 1 - x) / fx));
                var c = img.GetPixel(x, y);
                c.A *= kx * ky;
                img.SetPixel(x, y, c);
            }
        }

        var tex = ImageTexture.CreateFromImage(img);
        _softCache[key] = tex;
        return tex;
    }

    private Texture2D SoftBottom(string path, Texture2D src, float fraction)
    {
        string key = $"{path}|{fraction:0.00}";
        if (_softCache.TryGetValue(key, out var cached) && IsInstanceValid(cached)) return cached;

        var img = src.GetImage();
        if (img == null) return src;
        img = (Image)img.Duplicate();
        if (img.IsCompressed()) img.Decompress();
        if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);

        int w = img.GetWidth(), h = img.GetHeight();
        int start = Mathf.RoundToInt(h * (1f - Mathf.Clamp(fraction, 0f, 0.9f)));
        int span = Mathf.Max(1, h - 1 - start);

        for (int y = start; y < h; y++)
        {
            float k = 1f - (float)(y - start) / span;
            k = Mathf.SmoothStep(0f, 1f, k);
            for (int x = 0; x < w; x++)
            {
                var c = img.GetPixel(x, y);
                c.A *= k;
                img.SetPixel(x, y, c);
            }
        }

        var tex = ImageTexture.CreateFromImage(img);
        _softCache[key] = tex;
        return tex;
    }

    private void Spawn(Node node) => AddChild(node);
}
