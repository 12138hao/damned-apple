using Godot;

/// <summary>
/// 悬崖藤桥背景：一整段"天空 + 桥下瀑布"的布景，可复用。
///
/// 用法：在一个场景里放一个空 Node2D，挂上这个脚本，把 Length 调成想要的长度，
/// 它会自己铺出天空、藤桥桥面、桥柱、桥下整片瀑布和水花。**不产生任何碰撞体**，
/// 走不走得过去由你自己摆的 StaticBody2D 决定（布景和碰撞分开，改地形不会带着背景一起动）。
///
/// 为什么不用平铺大图：
///   桥面原图 2880 宽，柱子不等距，而且每一行的"重复周期"都不一样（实测 36~256 都有），
///   所以整张图没法平铺，只能"桥面 + 柱子"拆成两个零件，桥面用镜像拼接（见 tools/_art/build_jungle_bg.py）。
///   这个脚本负责把这两个零件按长度重新排布。
///
/// 关于 [Tool]：
///   不加的话这些布景只会在运行时生成，编辑器里打开场景是一片空的。
///   加了之后编辑器里也能看到，但**生成的节点不设 Owner**，所以不会被存进 .tscn 文件
///   （不然每保存一次场景就多几百个精灵节点）。
///
/// 层级约定（都排在玩家 z_index=10 后面）：
///   天空 -100 / 瀑布 -90 / 水花 -85 / 桥柱 -80 / 桥面 -70
/// </summary>
[Tool]
public partial class CliffBackdrop : Node2D
{
    [ExportGroup("尺寸")]
    [Export] public float Length = 3600f;        // 整段背景多长（px）
    [Export] public float SurfaceY = 230f;       // 桥面"脚踩的那条线"的世界 Y
    [Export] public float ChasmDepth = 460f;     // 桥下面山谷有多深
    [Export] public float ArtScale = 0.5f;       // 原画是 2px 大像素，0.5 = 屏幕像素 1:1

    [ExportGroup("天空")]
    [Export] public float SkyTopPad = 420f;      // 天空上边比桥面高出多少（要盖住画面顶部）
    [Export] public float SkyBottomPad = 520f;   // 天空下边比桥面低多少

    [ExportGroup("藤桥")]
    [Export] public bool DrawBridge = true;
    [Export] public bool DrawPillars = true;
    [Export] public float PillarSpacing = 660f;  // 柱子间隔（世界坐标）

    [ExportGroup("瀑布")]
    [Export] public bool DrawWaterfall = true;
    [Export] public float WaterTopOffset = 10f;  // 水幕顶边往上提多少（塞进桥面底下，避免露出一条缝）
    [Export] public float FlowSpeed = 0.16f;     // 每秒流下多少张贴图高
    [Export] public float WobbleAmp = 0.0035f;
    [Export] public Color WaterTint = new Color(0.62f, 0.74f, 0.82f, 1f); // 压暗 + 降饱和，让水退到背景里
    [Export] public float DeckShadow = 130f;     // 桥下这道阴影多高，0 = 不画
    [Export] public bool DrawSplash = true;
    [Export] public bool SplashParticles = true;

    [ExportGroup("随机")]
    [Export] public int Seed = 20261106;
    [Export] public bool RandomizeFlip = true;

    private const string SkyPath = "res://assets/jungle/bg/sky_gradient.png";
    private const string DeckPath = "res://assets/jungle/bg/bridge_deck.png";
    private const string PillarPath = "res://assets/jungle/bg/bridge_pillar.png";
    private const string FallPath = "res://assets/jungle/bg/waterfall_fall.png";
    private const string SplashPath = "res://assets/jungle/bg/waterfall_splash.png";
    private const string FallShaderPath = "res://assets/jungle/bg/waterfall.gdshader";

    // 原画里"贴图顶部 → 栏杆底部"这一段是栏杆，玩家是站在栏杆后面那条线上的。
    // 桥面贴图 195px 高，其中栏杆占 90px，0.5 倍就是 45px。
    private const float RailingPx = 90f;

    private string _lastSignature = "";

    public override void _Ready()
    {
        _lastSignature = Signature();
        Rebuild();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        // 只在编辑器里盯着参数变化，改一个数就重建一次，不用手动重开场景
        if (!Engine.IsEditorHint()) return;
        string sig = Signature();
        if (sig == _lastSignature) return;
        _lastSignature = sig;
        Rebuild();
    }

    private string Signature()
    {
        return string.Join("|",
            Length, SurfaceY, ChasmDepth, ArtScale, SkyTopPad, SkyBottomPad,
            DrawBridge, DrawPillars, PillarSpacing, DrawWaterfall, WaterTopOffset,
            FlowSpeed, WobbleAmp, WaterTint, DeckShadow, DrawSplash, SplashParticles,
            Seed, RandomizeFlip);
    }

    private void Rebuild()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Seed;

        var sky = GD.Load<Texture2D>(SkyPath);
        var deck = GD.Load<Texture2D>(DeckPath);
        var pillar = GD.Load<Texture2D>(PillarPath);
        var fall = GD.Load<Texture2D>(FallPath);
        var splash = GD.Load<Texture2D>(SplashPath);
        var shader = GD.Load<Shader>(FallShaderPath);

        float railing = RailingPx * ArtScale;
        float deckH = deck != null ? deck.GetHeight() * ArtScale : 0f;
        float deckW = deck != null ? deck.GetWidth() * ArtScale : 1f;
        float deckTop = SurfaceY - railing;
        float deckBottom = deckTop + deckH;

        BuildSky(sky);
        if (DrawWaterfall && fall != null)
            BuildWaterfall(fall, splash, shader, deckBottom, rng);
        if (DrawPillars && pillar != null)
            BuildPillars(pillar, deckTop);
        if (DrawBridge && deck != null)
            BuildDeck(deck, deckW, deckTop);
        if (DeckShadow > 0f) BuildDeckShadow(deckBottom);
    }

    /// <summary>
    /// 天空：一张纯垂直渐变，横向拉满即可，因为是逐行同色所以拉多宽都无缝。
    /// 上下范围要比画面大一圈，否则镜头在画面顶部会露出没有背景的黑边。
    /// </summary>
    private void BuildSky(Texture2D tex)
    {
        if (tex == null) return;
        float top = SurfaceY - SkyTopPad;
        float bottom = SurfaceY + SkyBottomPad;
        var s = new Sprite2D
        {
            Name = "Sky",
            Texture = tex,
            Centered = false,
            Position = new Vector2(0f, top),
            Scale = new Vector2(Length / tex.GetWidth(), (bottom - top) / tex.GetHeight()),
            ZIndex = -100,
            TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
        };
        Spawn(s);
    }

    /// <summary>
    /// 桥肚子下面那道阴影：瀑布顶端直接贴着桥面会有一条"硬切线"，
    /// 压一层从上到下渐隐的暗部，水就像是"从桥底涌出来"，而不是贴了张图。
    /// </summary>
    private void BuildDeckShadow(float deckBottom)
    {
        var img = Image.CreateEmpty(4, 64, false, Image.Format.Rgba8);
        for (int y = 0; y < 64; y++)
        {
            float t = 1f - (float)y / 63f;
            var c = new Color(0.02f, 0.06f, 0.07f, 0.55f * t * t);
            for (int x = 0; x < 4; x++) img.SetPixel(x, y, c);
        }
        var s = new Sprite2D
        {
            Name = "DeckShadow",
            Texture = ImageTexture.CreateFromImage(img),
            Centered = false,
            Position = new Vector2(0f, deckBottom - 4f),
            Scale = new Vector2(Length / 4f, DeckShadow / 64f),
            ZIndex = -86,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        Spawn(s);
    }

    /// <summary>桥下整片瀑布：按贴图尺寸铺格子，每格随机相位和横向偏移，避免"同一张图重复"的节奏感。</summary>
    private void BuildWaterfall(Texture2D fall, Texture2D splash, Shader shader, float deckBottom, RandomNumberGenerator rng)
    {
        float tw = fall.GetWidth() * ArtScale;
        float th = fall.GetHeight() * ArtScale;
        // 水幕顶边往上提一点，塞进桥面底下；不然桥底和水面之间会露出一条缝
        float waterTop = deckBottom - WaterTopOffset;
        int cols = Mathf.CeilToInt(Length / tw);
        // 水幕只需要铺到谷底为止。多算一行会多出整整一块贴图挂在谷底下面，
        // 在编辑器里一眼就能看出"底下又重复了一段"，所以最后一行按剩余高度裁掉。
        float waterHeight = ChasmDepth + WaterTopOffset;
        int rows = Mathf.CeilToInt(waterHeight / th);

        var root = new Node2D { Name = "Waterfall", ZIndex = -90 };
        Spawn(root);

        for (int r = 0; r < rows; r++)
        {
            float rowH = Mathf.Min(th, waterHeight - r * th);
            if (rowH <= 0.5f) break;

            for (int c = 0; c < cols; c++)
            {
                var s = new Sprite2D
                {
                    Name = $"Fall_{c}_{r}",
                    Texture = fall,
                    Centered = false,
                    Position = new Vector2(c * tw, waterTop + r * th),
                    Scale = new Vector2(ArtScale, ArtScale),
                    TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
                    Modulate = WaterTint,
                };
                if (rowH < th - 0.5f)
                {
                    s.RegionEnabled = true;
                    s.RegionRect = new Rect2(0f, 0f, fall.GetWidth(), rowH / ArtScale);
                }
                if (RandomizeFlip && rng.Randf() > 0.5f) s.FlipH = true;

                var mat = new ShaderMaterial { Shader = shader };
                mat.SetShaderParameter("speed", FlowSpeed);
                mat.SetShaderParameter("wobble_amp", WobbleAmp);
                mat.SetShaderParameter("phase", rng.Randf());
                mat.SetShaderParameter("shift_x", rng.Randf());
                s.Material = mat;
                root.AddChild(s);
            }
        }

        if (!DrawSplash || splash == null) return;

        float sw = splash.GetWidth() * ArtScale;
        float sh = splash.GetHeight() * ArtScale;
        float splashY = waterTop + ChasmDepth - sh;
        int scols = Mathf.CeilToInt(Length / sw);
        var sroot = new Node2D { Name = "Splash", ZIndex = -85 };
        Spawn(sroot);
        for (int c = 0; c < scols; c++)
        {
            var s = new Sprite2D
            {
                Name = $"Splash_{c}",
                Texture = splash,
                Centered = false,
                Position = new Vector2(c * sw, splashY),
                Scale = new Vector2(ArtScale, ArtScale),
            };
            if (RandomizeFlip && rng.Randf() > 0.5f) s.FlipH = true;
            sroot.AddChild(s);
        }

        if (SplashParticles) BuildSplashParticles(splashY);
    }

    /// <summary>水花：一条横贯整段的粒子带，往上溅、带重力落回去。</summary>
    private void BuildSplashParticles(float splashY)
    {
        // 编辑器里不生成粒子：会一直在动、还容易刷警告，静态布景看得清就够了
        if (Engine.IsEditorHint()) return;

        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(Length * 0.5f, 2f, 1f),
            Direction = new Vector3(0, -1, 0),
            Spread = 55f,
            InitialVelocityMin = 30f,
            InitialVelocityMax = 110f,
            Gravity = new Vector3(0, 320f, 0),
            ScaleMin = 0.6f,
            ScaleMax = 1.6f,
            LifetimeRandomness = 0.4f,
        };

        var p = new GpuParticles2D
        {
            Name = "SplashParticles",
            Amount = 120,
            Lifetime = 0.9,
            Position = new Vector2(Length * 0.5f, splashY + 6f),
            ProcessMaterial = mat,
            Texture = MakeDroplet(),
            ZIndex = -84,
            Emitting = true,
        };
        Spawn(p);
    }

    /// <summary>现搓一颗 6x6 的水珠贴图，省得再单独放一张素材。</summary>
    private static Texture2D MakeDroplet()
    {
        const int n = 6;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - (n - 1) * 0.5f;
                float dy = y - (n - 1) * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (n * 0.5f);
                float a = Mathf.Clamp(1.2f - d, 0f, 1f);
                if (a <= 0f) continue;
                img.SetPixel(x, y, new Color(0.94f, 1f, 1f, a));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>桥柱：从桥面下沿往下扎，按 PillarSpacing 等距摆。</summary>
    private void BuildPillars(Texture2D tex, float deckTop)
    {
        var root = new Node2D { Name = "Pillars", ZIndex = -80 };
        Spawn(root);

        float pw = tex.GetWidth() * ArtScale;
        // 柱顶稍微塞进桥面里一点，免得接缝露白
        float top = deckTop + 60f;
        for (float x = PillarSpacing * 0.5f; x < Length; x += PillarSpacing)
        {
            var s = new Sprite2D
            {
                Name = $"Pillar_{Mathf.RoundToInt(x)}",
                Texture = tex,
                Centered = false,
                Position = new Vector2(x - pw * 0.5f, top),
                Scale = new Vector2(ArtScale, ArtScale),
            };
            root.AddChild(s);
        }
    }

    /// <summary>桥面：镜像拼接。奇数块左右翻转，接缝两边像素天然连续。</summary>
    private void BuildDeck(Texture2D tex, float deckW, float deckTop)
    {
        var root = new Node2D { Name = "Bridge", ZIndex = -70 };
        Spawn(root);

        int n = Mathf.CeilToInt(Length / deckW);
        for (int i = 0; i < n; i++)
        {
            var s = new Sprite2D
            {
                Name = $"Deck_{i}",
                Texture = tex,
                Centered = false,
                Position = new Vector2(i * deckW, deckTop),
                Scale = new Vector2(ArtScale, ArtScale),
                FlipH = i % 2 == 1,
            };
            // 最后一节常常会超出 Length 一截，不裁的话桥面会一直铺到下一片草原里去
            float over = (i + 1) * deckW - Length;
            if (over > 0.5f)
            {
                s.RegionEnabled = true;
                s.RegionRect = new Rect2(0f, 0f, (deckW - over) / ArtScale, tex.GetHeight());
            }
            root.AddChild(s);
        }
    }

    /// <summary>
    /// 加节点。**故意不设 Owner**：编辑器里 [Tool] 生成的这些节点因此不会被写进 .tscn，
    /// 只是临时显示用。运行时 Owner 本来也不影响渲染。
    /// </summary>
    private void Spawn(Node node)
    {
        AddChild(node);
    }
}
