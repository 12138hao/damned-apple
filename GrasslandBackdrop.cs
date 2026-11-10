using Godot;

/// <summary>
/// 草原背景：天空 + 远景色带 + 近景植被带 + 可走的草地。可复用，用法和 CliffBackdrop 一样。
///
/// 分三层做纵深：
///   远景色带  视差 0.86（走得慢，显得远）
///   近景植被  视差 0.94（稍快，贴着地面）
///   草地本身  不视差，就是地面（碰撞另放）
///
/// 每一层都是**镜像平铺**（奇数块左右翻转），接缝两边像素天然连续，
/// 因为没有哪张图能保证左右边缘严格对上。
///
/// 和 CliffBackdrop 一样：[Tool] 让编辑器里也能看到；生成的节点**不设 Owner**，
/// 所以不会被写进 .tscn，保存场景不会膨胀。粒子之类只在运行时生成。
/// </summary>
[Tool]
public partial class GrasslandBackdrop : Node2D
{
    [ExportGroup("尺寸")]
    [Export] public float Length = 8000f;        // 这一片草原多长
    [Export] public float SurfaceY = 361f;       // 草皮表面（玩家脚踩的那条线）
    [Export] public float ArtScale = 0.5f;       // 原画 2px 大像素，0.5 = 1:1

    [ExportGroup("天空")]
    [Export] public bool DrawSky = true;
    [Export] public float SkyTopPad = 763f;      // 跟 CliffBackdrop 保持一致，接缝处颜色才对得上
    [Export] public float SkyBottomPad = 380f;

    [ExportGroup("高空")]
    [Export] public bool DrawHighSky = true;     // 往上再铺一层更高的天（给巨茎那种高物用）
    [Export] public float HighSkyTopY = -3600f;  // 天空最高铺到这个 Y
    [Export] public Color HighSkyTopColor = new Color(8f / 255f, 18f / 255f, 58f / 255f, 1f);
    [Export] public Color HighSkyBottomColor = new Color(21f / 255f, 64f / 255f, 150f / 255f, 1f);

    [ExportGroup("远景色带")]
    [Export] public bool DrawFar = true;
    [Export] public float FarBottomY = 420f;     // 色带底边（会被草地盖住）
    [Export] public float FarFactor = 0.86f;     // 视差：越小走得越慢
    [Export] public Color FarTint = new Color(1f, 1f, 1f, 0.92f);

    [ExportGroup("近景植被")]
    [Export] public bool DrawNear = true;
    [Export] public float NearBottomY = 486f;    // 植物根部埋进草地底下
    [Export] public float NearFactor = 0.94f;

    [ExportGroup("草地")]
    [Export] public bool DrawGround = true;
    [Export] public float GroundTopOffset = 6f;  // 草皮比行走线往上多露几像素，盖住脚

    [ExportGroup("视差")]
    [Export] public bool EnableParallax = true;

    private const string SkyPath = "res://assets/jungle/bg/sky_gradient.png";
    private const string FarPath = "res://assets/jungle/bg/grass_far.png";
    private const string NearPath = "res://assets/jungle/bg/grass_near.png";
    private const string GroundPath = "res://assets/jungle/bg/grass_ground.png";

    // 视差会让色带整体左右移动，所以两侧要多铺一点，免得边上露空
    private const float Bleed = 760f;

    private Node2D _farRoot;
    private Node2D _nearRoot;
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
            if (sig != _lastSignature)
            {
                _lastSignature = sig;
                Rebuild();
            }
            return;   // 编辑器里不做视差，免得选中节点时画面乱动
        }

        if (!EnableParallax) return;

        var cam = GetViewport()?.GetCamera2D();
        if (cam == null) return;

        // 以这一段的中点作为"视差原点"：玩家走到中间时两层都不偏移
        float anchor = GlobalPosition.X + Length * 0.5f;
        float dx = cam.GlobalPosition.X - anchor;

        if (_farRoot != null)
            _farRoot.Position = new Vector2(dx * (1f - FarFactor), 0f);
        if (_nearRoot != null)
            _nearRoot.Position = new Vector2(dx * (1f - NearFactor), 0f);
    }

    private string Signature()
    {
        return string.Join("|", Length, SurfaceY, ArtScale, DrawSky, SkyTopPad, SkyBottomPad,
            DrawHighSky, HighSkyTopY, HighSkyTopColor, HighSkyBottomColor,
            DrawFar, FarBottomY, FarFactor, FarTint, DrawNear, NearBottomY, NearFactor,
            DrawGround, GroundTopOffset, EnableParallax);
    }

    private void Rebuild()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        _farRoot = null;
        _nearRoot = null;

        var sky = GD.Load<Texture2D>(SkyPath);
        var far = GD.Load<Texture2D>(FarPath);
        var near = GD.Load<Texture2D>(NearPath);
        var ground = GD.Load<Texture2D>(GroundPath);

        if (DrawSky && sky != null) BuildSky(sky);
        if (DrawHighSky) BuildHighSky();
        if (DrawFar && far != null) _farRoot = BuildBand("Far", far, FarBottomY, FarTint, -96);
        if (DrawNear && near != null) _nearRoot = BuildBand("Near", near, NearBottomY, Colors.White, -93);
        if (DrawGround && ground != null) BuildGround(ground);
    }

    /// <summary>
    /// 高空层：从主天空的顶边（SurfaceY - SkyTopPad）继续往上铺到 HighSkyTopY。
    ///
    /// 不用把整张天空贴图拉高 —— 那样纹理要 8000 多像素高，而且画面里的渐变会被压扁变色。
    /// 这里单独用一条 4x256 的窄渐变，底边颜色**严格等于**主天空的顶色，接缝处看不出来；
    /// 越往上越深（高空的空气更稀薄、天更暗），巨茎那种顶到 -3400 的东西就不会插在虚空里。
    /// </summary>
    private void BuildHighSky()
    {
        float bottom = SurfaceY - SkyTopPad;     // 主天空的顶边
        if (HighSkyTopY >= bottom - 1f) return;

        const int N = 256;
        var img = Image.CreateEmpty(4, N, false, Image.Format.Rgba8);
        for (int y = 0; y < N; y++)
        {
            float t = (float)y / (N - 1);
            var c = HighSkyTopColor.Lerp(HighSkyBottomColor, t);
            for (int x = 0; x < 4; x++) img.SetPixel(x, y, c);
        }

        var s = new Sprite2D
        {
            Name = "HighSky",
            Texture = ImageTexture.CreateFromImage(img),
            Centered = false,
            Position = new Vector2(0f, HighSkyTopY),
            Scale = new Vector2(Length / 4f, (bottom - HighSkyTopY) / N),
            ZIndex = -101,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        Spawn(s);
    }

    private void BuildSky(Texture2D tex)
    {
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

    /// <summary>铺一条可平铺的色带，镜像拼接。返回根节点，用来做视差。</summary>
    private Node2D BuildBand(string name, Texture2D tex, float bottomY, Color tint, int z)
    {
        var root = new Node2D { Name = name, ZIndex = z };
        Spawn(root);

        float tw = tex.GetWidth() * ArtScale;
        float th = tex.GetHeight() * ArtScale;
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
                Scale = new Vector2(ArtScale, ArtScale),
                FlipH = ((i % 2) + 2) % 2 == 1,
                Modulate = tint,
            };
            root.AddChild(s);
        }
        return root;
    }

    /// <summary>草地：镜像平铺，草皮顶边比行走线高一点点，玩家踩上去像踩进草里。</summary>
    private void BuildGround(Texture2D tex)
    {
        var root = new Node2D { Name = "Ground", ZIndex = -70 };
        Spawn(root);

        float tw = tex.GetWidth() * ArtScale;
        float top = SurfaceY - GroundTopOffset;

        int i0 = 0;
        int i1 = Mathf.CeilToInt(Length / tw);
        for (int i = i0; i <= i1; i++)
        {
            var s = new Sprite2D
            {
                Name = $"Ground_{i}",
                Texture = tex,
                Centered = false,
                Position = new Vector2(i * tw, top),
                Scale = new Vector2(ArtScale, ArtScale),
                FlipH = ((i % 2) + 2) % 2 == 1,
            };
            root.AddChild(s);
        }
    }

    private void Spawn(Node node)
    {
        AddChild(node);
    }
}
