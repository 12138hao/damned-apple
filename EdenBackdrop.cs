using Godot;
using System.Collections.Generic;

/// <summary>
/// 黄昏伊甸园背景（第四章）。
///
/// 层次原则（2026-11-09 老大定的）：
///   **树木花草全部融进背景，地板单独拎出来放在景物最前面。**
///   所以植被、乔木、云、悬浮石都是有视差的背景层，只有地面带是"不视差"的前景景物层，
///   它盖在所有植物之上、玩家之下 —— 植物看起来是"长在地平线后面"的，不是戳在棋盘上的棋子。
///
/// 像素风铁律：**所有缩放只用整数分之一**（1 / 0.5 / 0.25 / 0.125），
/// 否则 Nearest 采样会让像素格子大小不均匀，画面一眼就"不是像素风"。
///
/// 生成的节点不设 Owner，不会写进 .tscn。
/// </summary>
[Tool]
public partial class EdenBackdrop : Node2D
{
    [ExportGroup("尺寸")]
    [Export] public float Length = 20000f;        // 地图总长
    [Export] public float SurfaceY = 700f;        // 草皮行走线（玩家脚踩的那条）
    [Export] public float ArtScale = 0.5f;        // 原画 2px 大像素，0.5 = 缩小一半（整数分之一）

    [ExportGroup("黄昏天空")]
    [Export] public Color SkyTopColor = new Color(0.42f, 0.36f, 0.62f, 1f);      // 天顶：暮紫
    [Export] public Color SkyMidColor = new Color(0.96f, 0.62f, 0.52f, 1f);      // 中段：霞粉
    [Export] public Color SkyHorizonColor = new Color(1f, 0.86f, 0.62f, 1f);     // 地平线：金
    [Export] public float SkyTopY = -1200f;
    [Export] public float SkyBottomY = 860f;
    // 两段渐变做不出黄昏：紫到金直接过渡会变成一坨灰褐色。
    // 这里把霞粉卡在中间高度，往上才是紫，往下越来越金。
    [Export] public float SkyHorizonStart = 430f;                     // 从这个世界 Y 开始转霞粉（镜头视口顶边）
    [Export] public float SkyHorizonEnd = 745f;                       // 到这里完全变金

    [ExportGroup("落日")]
    [Export] public bool DrawSun = true;
    [Export] public Vector2 SunScreenPos = new Vector2(250f, -140f);   // 相对视图中心的位置
    [Export] public float SunRadius = 200f;
    [Export] public Color SunCoreColor = new Color(1f, 0.96f, 0.80f, 1f);
    [Export] public Color SunGlowColor = new Color(1f, 0.72f, 0.42f, 1f);

    [ExportGroup("圣光")]
    [Export] public bool DrawGodRays = true;
    [Export] public int RayCount = 12;
    [Export] public float RayAlpha = 0.055f;      // 一定要很淡，否则像斜纹布
    [Export] public Color RayColor = new Color(1f, 0.88f, 0.62f, 1f);

    [ExportGroup("远景云海")]
    [Export] public bool DrawFar = true;
    [Export] public float FarBottomY = 800f;
    [Export] public float FarFactor = 0.80f;
    [Export] public Color FarTint = new Color(1f, 0.93f, 0.88f, 0.95f);   // 被夕阳染暖

    [ExportGroup("悬浮石块")]
    [Export] public bool DrawRocks = true;
    [Export] public int RockCount = 34;
    [Export] public float RockFactor = 0.87f;
    [Export] public float RockNearY = 70f;
    [Export] public float RockFarY = 300f;
    [Export] public int RockSeed = 20261109;

    [ExportGroup("乔木（大中小三档）")]
    [Export] public bool DrawTrees = true;
    [Export] public int TreeCountBig = 16;
    [Export] public int TreeCountMid = 38;
    [Export] public int TreeCountSmall = 58;
    [Export] public float TreeScaleBig = 0.5f;      // 918x962 → 459x481
    [Export] public float TreeScaleMid = 0.25f;     // 510x968 → 128x242
    [Export] public float TreeScaleSmall = 0.125f;  // 539x935 →  67x117
    [Export] public int TreeSeed = 7717;
    [Export] public Color TreeTintBig = new Color(0.86f, 0.84f, 0.92f, 1f);    // 最远，偏冷偏淡
    [Export] public Color TreeTintMid = new Color(0.95f, 0.94f, 0.96f, 1f);
    [Export] public Color TreeTintSmall = new Color(1f, 1f, 1f, 1f);

    [ExportGroup("近景植被（两排）")]
    [Export] public bool DrawNear = true;
    [Export] public float NearBackBottomY = 772f;
    [Export] public float NearBackFactor = 0.93f;
    [Export] public Color NearBackTint = new Color(0.82f, 0.86f, 0.94f, 1f);   // 后面那排压暗压蓝
    [Export] public float NearFrontBottomY = 814f;
    [Export] public float NearFrontFactor = 0.97f;

    [ExportGroup("天空的云")]
    [Export] public bool DrawUpperClouds = true;
    [Export] public int UpperCloudCount = 30;
    [Export] public float UpperCloudFactor = 0.78f;
    [Export] public float UpperCloudAlpha = 0.9f;
    [Export] public Color UpperCloudTint = new Color(1f, 0.94f, 0.90f, 1f);

    [ExportGroup("前景薄云")]
    [Export] public bool DrawMist = true;
    [Export] public int MistCount = 20;
    [Export] public float MistAlpha = 0.26f;

    [ExportGroup("地面（景物最前）")]
    [Export] public bool DrawGround = true;
    [Export] public float GroundTopOffset = 15f;   // 草尖比行走线高 15px
    [Export] public Color GroundTint = new Color(1f, 0.95f, 0.88f, 1f);   // 夕阳暖光

    [ExportGroup("实时阴影")]
    [Export] public bool DrawSunLight = false;   // 方向光阴影会把整片草地罩黑，默认关；真要用可以开
    // 角度越接近 0 越"正上方"，影子越短；黄昏太阳低，但拉太长会糊成一片，
    // 这里取一个偏正上方略斜的角度，读起来是"斜射的落日"又不至于满地黑条。
    [Export] public float SunLightAngleDeg = -16f;
    [Export] public float SunLightEnergy = 0.20f;      // 很弱的补光，只为了让影子有对比
    [Export] public float SunShadowAlpha = 0.42f;
    [Export] public bool TreeOccluders = true;
    [Export] public bool RockOccluders = true;

    [ExportGroup("地面软影")]
    [Export] public bool DrawGroundShadows = true;
    [Export] public Color GroundShadowColor = new Color(0.05f, 0.06f, 0.16f, 1f);
    [Export] public float TreeShadowAlpha = 0.74f;
    [Export] public float TreeShadowWidth = 0.78f;   // 影子宽度 = 树宽 × 这个
    [Export] public float RockShadowAlpha = 0.28f;
    [Export] public float ShadowShiftX = -0.16f;     // 太阳在右，影子往左偏（比例）

    [ExportGroup("整体")]
    [Export] public float Brightness = 1.10f;      // 全场提亮倍数（黄昏容易发闷，给个总闸）
    [Export] public bool EnableParallax = true;

    private const string FarPath = "res://assets/eden/far.png";
    private const string NearPath = "res://assets/eden/near.png";
    private const string GroundPath = "res://assets/eden/ground.png";
    private static readonly string[] TreePaths =
    {
        "res://assets/eden/tree_big.png",
        "res://assets/eden/tree_mid.png",
        "res://assets/eden/tree_small.png",
    };
    private static readonly string[] RockPaths =
    {
        "res://assets/eden/rock_1.png",
        "res://assets/eden/rock_2.png",
        "res://assets/eden/rock_3.png",
    };
    private static readonly string[] CloudPaths =
    {
        "res://assets/jungle/bg/cloud_1.png",
        "res://assets/jungle/bg/cloud_2.png",
        "res://assets/jungle/bg/cloud_3.png",
        "res://assets/jungle/bg/cloud_4.png",
    };

    private const float Bleed = 900f;

    private readonly List<(Node2D root, float factor)> _parallax = new();
    private Node2D _sunAnchor;
    private string _lastSignature = "";

    // 云贴图底边是平切的（原来是从色带上裁的），飘在天上会露出一条直边
    // → 底部 alpha 渐隐，结果缓存，不要每次重建都重算
    private static readonly Dictionary<string, Texture2D> _softBottomCache = new();

    // 同一张贴图的所有实例共用同一个遮挡多边形，不要每棵树各算一遍
    private static readonly Dictionary<string, OccluderPolygon2D> _occluderCache = new();

    private static Texture2D _ellipseTex;
    private int _shadowSeq;

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
            if (sig != _lastSignature)
            {
                _lastSignature = sig;
                Rebuild();
            }
            return;
        }

        if (!EnableParallax) return;

        var cam = GetViewport()?.GetCamera2D();
        if (cam == null) return;

        float anchor = GlobalPosition.X + Length * 0.5f;
        float dx = cam.GlobalPosition.X - anchor;

        for (int i = 0; i < _parallax.Count; i++)
        {
            var (root, factor) = _parallax[i];
            if (root != null && IsInstanceValid(root))
                root.Position = new Vector2(dx * (1f - factor), 0f);
        }

        // 太阳钉在镜头上：走多远它都不动，像真正的天边
        // 注意：Camera2D 有 offset(-80)，视图中心是 cam.GlobalPosition + cam.Offset，
        // 直接用 cam.GlobalPosition 会把太阳算到地面以下（之前就看不见太阳）。
        if (_sunAnchor != null && IsInstanceValid(_sunAnchor))
            _sunAnchor.Position = cam.GlobalPosition + cam.Offset + SunScreenPos;
    }

    private string Signature()
    {
        return string.Join("|", Length, SurfaceY, ArtScale,
            SkyTopColor, SkyMidColor, SkyHorizonColor, SkyTopY, SkyBottomY, SkyHorizonStart, SkyHorizonEnd,
            DrawSun, SunScreenPos, SunRadius, SunCoreColor, SunGlowColor,
            DrawGodRays, RayCount, RayAlpha, RayColor,
            DrawFar, FarBottomY, FarFactor, FarTint,
            DrawRocks, RockCount, RockFactor, RockNearY, RockFarY, RockSeed,
            DrawTrees, TreeCountBig, TreeCountMid, TreeCountSmall, TreeScaleBig, TreeScaleMid, TreeScaleSmall,
            TreeSeed, TreeTintBig, TreeTintMid, TreeTintSmall,
            DrawNear, NearBackBottomY, NearBackFactor, NearBackTint, NearFrontBottomY, NearFrontFactor,
            DrawUpperClouds, UpperCloudCount, UpperCloudFactor, UpperCloudAlpha, UpperCloudTint,
            DrawMist, MistCount, MistAlpha,
            DrawGround, GroundTopOffset, GroundTint, Brightness, EnableParallax,
            DrawSunLight, SunLightAngleDeg, SunLightEnergy, SunShadowAlpha, TreeOccluders, RockOccluders,
            DrawGroundShadows, GroundShadowColor, TreeShadowAlpha, TreeShadowWidth, RockShadowAlpha, ShadowShiftX);
    }

    private void Rebuild()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        _parallax.Clear();
        _sunAnchor = null;

        if (DrawSunLight) BuildSunLight();

        BuildSky();
        if (DrawSun) BuildSun();
        if (DrawGodRays) BuildGodRays();

        if (DrawFar)
        {
            var far = GD.Load<Texture2D>(FarPath);
            if (far != null)
            {
                var band = BuildBand("Far", far, FarBottomY, Mul(FarTint), -96);
                band.LightMask = 0;      // 远景云海不参与光照，避免被树影切出黑条
                AddParallax(band, FarFactor);
            }
        }
        if (DrawUpperClouds) BuildUpperClouds();
        if (DrawTrees) BuildTrees();
        if (DrawRocks) BuildRocks();
        if (DrawNear) BuildNearRows();
        if (DrawGround)
        {
            var ground = GD.Load<Texture2D>(GroundPath);
            if (ground != null) BuildGround(ground);
        }
        if (DrawMist) BuildMist();
    }

    /// <summary>把所有颜色统一乘一个亮度系数（&gt;1 会提亮）。</summary>
    private Color Mul(Color c)
    {
        return new Color(c.R * Brightness, c.G * Brightness, c.B * Brightness, c.A);
    }

    private void AddParallax(Node2D root, float factor)
    {
        if (root == null) return;
        _parallax.Add((root, factor));
    }

    // ─────────────────────────── 天空 ───────────────────────────

    /// <summary>
    /// 三段渐变：暮紫 → 霞粉 → 金。
    ///
    /// 两段做不出黄昏 —— 紫到金直接过渡会变成一坨灰褐色。
    /// 这里用 SkyHorizonStart / SkyHorizonEnd 把粉色卡在中间高度，再往上才是紫，越靠地平线越金。
    /// </summary>
    private void BuildSky()
    {
        const int N = 320;
        var img = Image.CreateEmpty(4, N, false, Image.Format.Rgba8);

        for (int y = 0; y < N; y++)
        {
            float t = (float)y / (N - 1);
            float wy = SkyTopY + (SkyBottomY - SkyTopY) * t;

            float k = Mathf.Clamp((wy - SkyHorizonStart) / Mathf.Max(1f, SkyHorizonEnd - SkyHorizonStart), 0f, 1f);
            k = Mathf.SmoothStep(0f, 1f, k);

            // k: 0 = 天顶紫，0.5 = 霞粉，1 = 地平线金
            Color c;
            if (k < 0.5f) c = SkyTopColor.Lerp(SkyMidColor, Mathf.SmoothStep(0f, 1f, k / 0.5f));
            else c = SkyMidColor.Lerp(SkyHorizonColor, Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f));

            for (int x = 0; x < 4; x++) img.SetPixel(x, y, Mul(c));
        }

        var s = new Sprite2D
        {
            Name = "Sky",
            Texture = ImageTexture.CreateFromImage(img),
            Centered = false,
            Position = new Vector2(-Bleed, SkyTopY),
            Scale = new Vector2((Length + Bleed * 2f) / 4f, (SkyBottomY - SkyTopY) / N),
            ZIndex = -100,
            LightMask = 0,        // 天空不参与光照，否则树的影子会在天上拉黑条
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        Spawn(s);
    }

    /// <summary>落日：一张程序化径向渐变，钉在镜头上不动，走多远都在天边。</summary>
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

                // 外圈：柔和光晕，衰减快一点，别糊满屏幕
                float glow = Mathf.Clamp(1f - d, 0f, 1f);
                glow = glow * glow * glow;

                // 内圈：**实心圆盘**，边缘 5% 过渡。
                // 只做渐变的话太阳会变成一团没有轮廓的糊光，看不出是落日。
                float disc = Mathf.Clamp(1f - (d - 0.30f) / 0.05f, 0f, 1f);

                var c = SunGlowColor.Lerp(SunCoreColor, disc);
                c.A = Mathf.Clamp(Mathf.Max(glow * 0.85f, disc), 0f, 1f);
                img.SetPixel(x, y, Mul(c));
            }
        }

        _sunAnchor = new Node2D { Name = "Sun", ZIndex = -99 };
        Spawn(_sunAnchor);

        var s = new Sprite2D
        {
            Name = "SunDisc",
            Texture = ImageTexture.CreateFromImage(img),
            Centered = true,
            Scale = Vector2.One * (SunRadius * 2f / S),
            LightMask = 0,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        _sunAnchor.AddChild(s);
    }

    /// <summary>斜射光柱：很淡的暖色平行四边形。</summary>
    private void BuildGodRays()
    {
        if (RayCount <= 0) return;

        float top = SkyTopY;
        float bottom = SurfaceY;
        float step = (Length + Bleed * 2f) / RayCount;
        float width = step * 1.15f;
        float slant = 620f;    // 底边相对顶边的水平偏移，决定倾斜角

        var root = new Node2D { Name = "GodRays", ZIndex = -98, LightMask = 0 };
        Spawn(root);

        for (int i = 0; i < RayCount; i++)
        {
            float x = -Bleed + i * step;
            var poly = new Polygon2D
            {
                Name = $"Ray_{i}",
                Color = new Color(RayColor.R, RayColor.G, RayColor.B, RayAlpha),
                Polygon = new Vector2[]
                {
                    new(x, top),
                    new(x + width, top),
                    new(x + width + slant, bottom),
                    new(x + slant, bottom),
                },
            };
            root.AddChild(poly);
        }
    }

    // ─────────────────────────── 实时阴影 ───────────────────────────

    /// <summary>
    /// 落日主光源。**能量给得很低（0.2 左右）**：它存在的意义不是"照亮画面"，
    /// 而是让 LightOccluder2D 投下的影子有对比度。能量一高整个画面就会被洗白，
    /// 黄昏的紫粉天色全毁。
    /// </summary>
    private void BuildSunLight()
    {
        var light = new DirectionalLight2D
        {
            Name = "SunLight",
            RotationDegrees = SunLightAngleDeg,
            Energy = SunLightEnergy,
            ShadowEnabled = true,
            ShadowColor = new Color(0.05f, 0.02f, 0.10f, SunShadowAlpha),
            ShadowFilter = Light2D.ShadowFilterEnum.Pcf5,
            ShadowFilterSmooth = 2f,
            ZIndex = -200,
        };
        Spawn(light);
    }

    /// <summary>
    /// 从贴图 alpha 生成一个"剪影"遮挡多边形：逐列取上下边界，拼成一个闭环。
    ///
    /// 不用凸包 —— 凸包会把树冠之间的空隙填平，影子变成一个大圆饼，
    /// 一眼就能看出是假的。逐列扫描虽然点多，但一次生成、同贴图共用，代价可以接受。
    /// </summary>
    private OccluderPolygon2D SilhouetteOf(string key, Texture2D tex, int hframes, float step, float alphaCut = 0.4f)
    {
        if (_occluderCache.TryGetValue(key, out var cached) && IsInstanceValid(cached)) return cached;

        var img = tex.GetImage();
        if (img == null) return null;
        img = (Image)img.Duplicate();
        if (img.IsCompressed()) img.Decompress();
        if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);

        int fw = img.GetWidth() / Mathf.Max(1, hframes);
        int fh = img.GetHeight();
        float cx = fw * 0.5f, cy = fh * 0.5f;
        int stepPx = Mathf.Max(1, Mathf.RoundToInt(step / 1f));

        var tops = new List<Vector2>();
        var bots = new List<Vector2>();

        for (int x = 0; x < fw; x += stepPx)
        {
            int top = -1, bot = -1;
            for (int y = 0; y < fh; y++)
            {
                if (img.GetPixel(x, y).A > alphaCut) { top = y; break; }
            }
            if (top < 0) continue;
            for (int y = fh - 1; y >= top; y--)
            {
                if (img.GetPixel(x, y).A > alphaCut) { bot = y; break; }
            }
            if (bot < 0) continue;

            float px = (x - cx) / 1f;
            tops.Add(new Vector2(px, top - cy));
            bots.Add(new Vector2(px, bot - cy));
        }

        if (tops.Count < 3) return null;

        var pts = new Vector2[tops.Count + bots.Count];
        for (int i = 0; i < tops.Count; i++) pts[i] = tops[i];
        for (int i = 0; i < bots.Count; i++) pts[tops.Count + i] = bots[bots.Count - 1 - i];

        var poly = new OccluderPolygon2D
        {
            Polygon = pts,
            CullMode = OccluderPolygon2D.CullModeEnum.Disabled,
        };
        _occluderCache[key] = poly;
        return poly;
    }

    /// <summary>给一个精灵挂遮挡体。多边形坐标是纹理像素，父节点缩放会一并生效。</summary>
    private void AttachOccluder(Sprite2D sp, OccluderPolygon2D poly, float extraScale = 1f)
    {
        if (poly == null || sp == null) return;
        var occ = new LightOccluder2D
        {
            Name = "Occ",
            Occluder = poly,
            Scale = Vector2.One * Mathf.Max(0.01f, extraScale),
        };
        // 遮挡体要跟着精灵一起被 z 排序打散，否则会挡住别的 sprite
        occ.ZAsRelative = true;
        occ.ZIndex = -1;
        sp.AddChild(occ);
    }

    /// <summary>一张 128x128 的径向渐变，用来当"接地软影"。只生成一次。</summary>
    private Texture2D EllipseTexture()
    {
        if (_ellipseTex != null && IsInstanceValid(_ellipseTex)) return _ellipseTex;

        const int S = 128;
        var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f) / (S * 0.5f) - 1f;
                float ny = (y + 0.5f) / (S * 0.5f) - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp(1f - d, 0f, 1f);
                a = a * a * (3f - 2f * a);              // smoothstep，边缘别是硬圆
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        _ellipseTex = ImageTexture.CreateFromImage(img);
        return _ellipseTex;
    }

    /// <summary>
    /// 在一块地面软影。**注意 z 用的是相对值**：挂在树木/石块的 root 下面，
    /// 这样影子会跟着那一层一起视差，不会走着走着和树脱节。
    /// z 偏移给 +37，让最终 z 落在 -55（草地 -60 之上、角色 10 之下）。
    /// </summary>
    private void AddSoftShadow(Node2D root, float x, float width, float alpha)
    {
        if (!DrawGroundShadows || root == null || width <= 1f || alpha <= 0.001f) return;

        var tex = EllipseTexture();
        if (tex == null) return;

        float w = width;
        float h = width * 0.30f;

        var sp = new Sprite2D
        {
            Name = $"SoftShadow_{_shadowSeq++}",
            Texture = tex,
            Centered = true,
            Position = new Vector2(x + width * ShadowShiftX, SurfaceY + 2f),
            Scale = new Vector2(w / tex.GetWidth(), h / tex.GetHeight()),
            Modulate = new Color(GroundShadowColor.R, GroundShadowColor.G, GroundShadowColor.B, alpha),
            ZAsRelative = true,
            ZIndex = 37,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        root.AddChild(sp);
    }

    // ─────────────────────────── 平铺色带 ───────────────────────────

    private Node2D BuildBand(string name, Texture2D tex, float bottomY, Color tint, int z, float scale = -1f)
    {
        float sc = scale < 0f ? ArtScale : scale;
        var root = new Node2D { Name = name, ZIndex = z };
        Spawn(root);

        float tw = tex.GetWidth() * sc;
        float th = tex.GetHeight() * sc;
        float top = bottomY - th;

        int i0 = Mathf.FloorToInt(-Bleed / tw);
        int i1 = Mathf.CeilToInt((Length + Bleed) / tw);

        for (int i = i0; i <= i1; i++)
        {
            var s = new Sprite2D
            {
                Name = $"{name}_{i}",
                Texture = tex,
                Centered = false,
                Position = new Vector2(i * tw, top),
                Scale = new Vector2(sc, sc),
                FlipH = ((i % 2) + 2) % 2 == 1,
                Modulate = tint,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(s);
        }
        return root;
    }

    /// <summary>地面带：不视差，z 在所有植物之上 —— 这就是"拎到最前端的地板"。</summary>
    private void BuildGround(Texture2D tex)
    {
        var root = new Node2D { Name = "GroundArt", ZIndex = -60 };
        Spawn(root);

        float tw = tex.GetWidth() * ArtScale;
        float top = SurfaceY - GroundTopOffset;
        var tint = Mul(GroundTint);

        int i1 = Mathf.CeilToInt(Length / tw);
        for (int i = 0; i <= i1; i++)
        {
            var s = new Sprite2D
            {
                Name = $"Ground_{i}",
                Texture = tex,
                Centered = false,
                Position = new Vector2(i * tw, top),
                Scale = new Vector2(ArtScale, ArtScale),
                FlipH = ((i % 2) + 2) % 2 == 1,
                Modulate = tint,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(s);
        }
    }

    // ─────────────────────────── 植被 ───────────────────────────

    /// <summary>
    /// 近景植被铺两排：后面那排压暗压蓝（空气透视），前面那排正常。
    /// 两排错开 + 不同视差，走起来有层次，也不会像一条贴纸带。
    /// </summary>
    private void BuildNearRows()
    {
        var tex = GD.Load<Texture2D>(NearPath);
        if (tex == null) return;

        var back = BuildBand("NearBack", tex, NearBackBottomY, Mul(NearBackTint), -86);
        AddParallax(back, NearBackFactor);

        var front = BuildBand("NearFront", tex, NearFrontBottomY, Mul(Colors.White), -79);
        AddParallax(front, NearFrontFactor);
    }

    /// <summary>
    /// 乔木三档，全部带视差（它们是背景，不是棋盘上的棋子）。
    /// 缩放只用 0.5 / 0.25 / 0.125 三个整数分之一，保证像素格子大小一致。
    /// </summary>
    private void BuildTrees()
    {
        var counts = new[] { TreeCountBig, TreeCountMid, TreeCountSmall };
        var scales = new[] { TreeScaleBig, TreeScaleMid, TreeScaleSmall };
        var factors = new[] { 0.84f, 0.905f, 0.95f };
        var tints = new[] { TreeTintBig, TreeTintMid, TreeTintSmall };
        var zs = new[] { -92, -88, -83 };
        var offsets = new[] { 0f, 0.37f, 0.71f };   // 三档水平错开，不会排成一条线

        var rng = new RandomNumberGenerator { Seed = (ulong)TreeSeed };

        for (int t = 0; t < 3; t++)
        {
            var tex = GD.Load<Texture2D>(TreePaths[t]);
            if (tex == null || counts[t] <= 0) continue;

            var root = new Node2D { Name = $"Trees_{t}", ZIndex = zs[t] };
            Spawn(root);
            AddParallax(root, factors[t]);

            OccluderPolygon2D poly = (TreeOccluders && DrawSunLight)
                ? SilhouetteOf(TreePaths[t], tex, 1, 6f)
                : null;

            float sc = scales[t];
            float halfH = tex.GetHeight() * sc * 0.5f;
            float step = Length / counts[t];
            var tint = Mul(tints[t]);

            for (int i = 0; i < counts[t]; i++)
            {
                float x = (i + offsets[t]) * step + rng.RandfRange(-step * 0.35f, step * 0.35f);
                x = Mathf.Clamp(x, 40f, Length - 40f);

                var sp = new Sprite2D
                {
                    Name = $"Tree{t}_{i}",
                    Texture = tex,
                    Centered = true,
                    // 底边压在行走线下方 6px：地板层会把根部盖住，看起来是"长在地平线后面"
                    Position = new Vector2(x, SurfaceY + 6f - halfH),
                    Scale = new Vector2(sc, sc),
                    FlipH = rng.Randf() < 0.5f,
                    Modulate = tint,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                };
                AttachOccluder(sp, poly);
                root.AddChild(sp);

                AddSoftShadow(root, x, tex.GetWidth() * sc * TreeShadowWidth, TreeShadowAlpha);
            }
        }
    }

    /// <summary>悬浮的带草破碎石块。缩放同样只用 0.5 / 0.25。</summary>
    private void BuildRocks()
    {
        var texs = new List<Texture2D>();
        foreach (var p in RockPaths)
        {
            var t = GD.Load<Texture2D>(p);
            if (t != null) texs.Add(t);
        }
        if (texs.Count == 0 || RockCount <= 0) return;

        var root = new Node2D { Name = "Rocks", ZIndex = -90 };
        Spawn(root);
        AddParallax(root, RockFactor);

        var rng = new RandomNumberGenerator { Seed = (ulong)RockSeed };
        float step = Length / RockCount;
        var tint = Mul(new Color(0.95f, 0.93f, 0.95f, 0.95f));

        var polys = new OccluderPolygon2D[RockPaths.Length];
        if (RockOccluders && DrawSunLight)
        {
            for (int i = 0; i < RockPaths.Length; i++)
            {
                var t = GD.Load<Texture2D>(RockPaths[i]);
                if (t != null) polys[i] = SilhouetteOf(RockPaths[i], t, 1, 4f);
            }
        }

        for (int i = 0; i < RockCount; i++)
        {
            int ti = rng.RandiRange(0, texs.Count - 1);
            var tex = texs[ti];
            float sc = rng.Randf() < 0.45f ? 0.5f : 0.25f;
            float x = i * step + rng.RandfRange(-step * 0.35f, step * 0.35f);
            float y = SurfaceY - rng.RandfRange(RockNearY, RockFarY);

            var sp = new Sprite2D
            {
                Name = $"Rock_{i}",
                Texture = tex,
                Centered = true,
                Position = new Vector2(x, y),
                Scale = new Vector2(sc, sc),
                FlipH = rng.Randf() < 0.5f,
                Modulate = tint,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            if (ti < polys.Length) AttachOccluder(sp, polys[ti]);
            root.AddChild(sp);

            // 悬浮石块离地越高，地上的影子越淡 —— 顺带把"它浮着"这件事讲清楚
            float lift = Mathf.Clamp(1f - (SurfaceY - y) / 460f, 0.35f, 1f);
            AddSoftShadow(root, x, tex.GetWidth() * sc * 0.9f, RockShadowAlpha * lift);
        }
    }

    // ─────────────────────────── 云 ───────────────────────────

    private void BuildUpperClouds()
    {
        var texs = new List<Texture2D>();
        foreach (var p in CloudPaths)
        {
            var t = GD.Load<Texture2D>(p);
            if (t != null) texs.Add(SoftBottom(p, t, 0.22f));
        }
        if (texs.Count == 0 || UpperCloudCount <= 0) return;

        var root = new Node2D { Name = "UpperClouds", ZIndex = -95, LightMask = 0 };
        Spawn(root);
        AddParallax(root, UpperCloudFactor);

        var rng = new RandomNumberGenerator { Seed = 4477 };
        float step = Length / UpperCloudCount;
        var tint = Mul(UpperCloudTint);
        tint.A = UpperCloudAlpha;

        for (int i = 0; i < UpperCloudCount; i++)
        {
            var tex = texs[rng.RandiRange(0, texs.Count - 1)];
            float sc = rng.Randf() < 0.5f ? 0.5f : 1f;
            float x = i * step + rng.RandfRange(-step * 0.4f, step * 0.4f);
            // 全部抬到草地上方 110~360px，是"飘在天上的云"而不是"铺在地上的棉花"
            float y = SurfaceY - rng.RandfRange(110f, 360f);

            var sp = new Sprite2D
            {
                Name = $"Cloud_{i}",
                Texture = tex,
                Centered = true,
                Position = new Vector2(x, y),
                Scale = new Vector2(sc, sc),
                FlipH = rng.Randf() < 0.5f,
                Modulate = tint,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(sp);
        }
    }

    private void BuildMist()
    {
        var texs = new List<Texture2D>();
        foreach (var p in CloudPaths)
        {
            var t = GD.Load<Texture2D>(p);
            if (t != null) texs.Add(SoftBottom(p, t, 0.30f));
        }
        if (texs.Count == 0 || MistCount <= 0) return;

        var root = new Node2D { Name = "Mist", ZIndex = 40 };
        Spawn(root);
        // factor 1.06 → 比地板跑得还快，是最靠前的一层
        AddParallax(root, 1.06f);

        var rng = new RandomNumberGenerator { Seed = 9911 };
        float step = Length / MistCount;
        var tint = new Color(1f, 0.93f, 0.86f, MistAlpha);

        for (int i = 0; i < MistCount; i++)
        {
            var tex = texs[rng.RandiRange(0, texs.Count - 1)];
            float sc = rng.Randf() < 0.5f ? 1f : 2f;
            float x = i * step + rng.RandfRange(-step * 0.4f, step * 0.4f);
            // 放在行走线下方 80~150px，只在云海里飘，不会糊住玩家和草地
            float y = SurfaceY + rng.RandfRange(80f, 150f);

            var sp = new Sprite2D
            {
                Name = $"Mist_{i}",
                Texture = tex,
                Centered = true,
                Position = new Vector2(x, y),
                Scale = new Vector2(sc, sc),
                FlipH = rng.Randf() < 0.5f,
                Modulate = tint,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            root.AddChild(sp);
        }
    }

    // ─────────────────────────── 工具 ───────────────────────────

    /// <summary>把贴图底部一段高度做 alpha 渐隐，消掉平切的硬边。第一次算完就缓存。</summary>
    private Texture2D SoftBottom(string path, Texture2D src, float fraction)
    {
        string key = $"{path}|{fraction:0.00}";
        if (_softBottomCache.TryGetValue(key, out var cached) && IsInstanceValid(cached)) return cached;

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
        _softBottomCache[key] = tex;
        return tex;
    }

    private void Spawn(Node node)
    {
        AddChild(node);
    }
}
