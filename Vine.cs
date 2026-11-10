using Godot;

/// <summary>
/// 可攀爬的藤蔓。节点位置 = 藤蔓**顶端**（挂在树冠上的那一头），往下一路长到 Length。
///
/// 和 <see cref="Rope"/> 的差别（也就是为什么单独做一份，而不是复用麻绳）：
///   · **长度可配**：改 Length 一个数字，贴图自动堆够、判定框自动跟着长，不用手调任何东西；
///   · **上慢下滑**：UpSpeed 和 DownSpeed 分开配，下滑还会加速，是「溜下去」不是匀速下降；
///   · **能换藤**：空中碰到另一根藤就直接抓住（AutoGrabInAir），所以两根之间可以跳来跳去；
///   · **到顶停住**：不像绳索到顶把你抛出去（绳索上面接的是传送门），藤蔓到顶就停在最上面。
///
/// 贴图是可平铺的小片（assets/jungle/vine_tile.png，24×32，一个正弦周期刚好等于贴图高度），
/// 由 _Draw 按 32 像素一格往上堆。这样任意长度都不会把像素拉糊。
///
/// 因为藤蔓是靠 _Draw 画出来的（不是 Sprite2D 子节点），所以必须挂 [Tool]：
/// 不加的话脚本只在运行时跑，编辑器 2D 视图里一根藤都看不到，只剩 W 键帽那个 Sprite2D。
/// 加了 [Tool] 之后 _Ready 里要分叉 —— 编辑器只负责画，不碰碰撞框和提示标。
/// </summary>
[Tool]
public partial class Vine : Rope
{
    [ExportGroup("长度")]
    [Export] public float Length = 320f;          // 藤蔓总长（像素），从节点往下量
    [Export] public float TopPad = 18f;           // 爬到顶时玩家中心离节点还有多远（防止头钻进树冠）

    [ExportGroup("粗细")]
    [Export] public float GrabWidth = 30f;        // 判定区宽度（比贴图略宽，好抓一点）
    // 贴图整数放大倍数。巨藤那种要用 4（24px → 96px 宽）；1 = 保持原样，老场景不受影响。
    // 必须是整数，非整数会把像素格子拉得大小不一。
    [Export] public float ArtScale = 1f;

    [ExportGroup("速度")]
    [Export] public float ClimbUpSpeed = 95f;     // 往上爬：慢
    [Export] public float SlideDownSpeed = 430f;  // 往下滑：快
    [Export] public float SlideDownAccel = 1500f; // 下滑加速度：按 S 越来越快，像溜下去

    [ExportGroup("换藤")]
    // 名字不能叫 JumpOffSpeed / JumpOffUp —— 基类已经有同名属性，
    // 派生类再声明同名字段会直接编译报错（字段和属性不能同名）。
    [Export] public float JumpOffX = 280f;        // 蹬开时往面朝方向的横向初速度
    [Export] public float JumpOffUpRatio = 0.85f; // 蹬开时往上弹多少（乘 jumpVelocity）

    [ExportGroup("贴图")]
    [Export] public Texture2D Tile;               // 可平铺小片，高度就是平铺周期

    // ── 编辑器预览用的记录值（只在 [Tool] 下有意义）──────────
    private float _previewLength = -1f;
    private Texture2D _previewTile;

    // ── 覆盖基类的「怎么算」 ──────────────────────────────
    public override float ClimbTopY => GlobalPosition.Y + TopPad;
    public override float ClimbBottomY => GlobalPosition.Y + Length;
    public override float UpSpeed => ClimbUpSpeed;
    public override float DownSpeed => SlideDownSpeed;
    public override float DownAccel => SlideDownAccel;
    public override float JumpOffSpeed => JumpOffX;
    public override float JumpOffUp => JumpOffUpRatio;
    public override bool TossAtTop => false;      // 到顶停住，别弹
    public override bool AutoGrabInAir => true;   // 空中碰到就抓 → 能换藤

    public override void _Ready()
    {
        // 编辑器里只要「看得见」：画贴图、不建碰撞、不动提示标，也别跑基类的运行时逻辑
        if (Engine.IsEditorHint())
        {
            _previewLength = Length;
            _previewTile = Tile;
            QueueRedraw();
            return;
        }

        base._Ready();
        // ① 判定框按 Length 现算。新建一个 Shape 而不是改场景里那个，
        //    否则多根藤蔓共用同一个资源，改一根全都变。
        var shapeNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shapeNode != null)
        {
            shapeNode.Shape = new RectangleShape2D { Size = new Vector2(GrabWidth, Length) };
            shapeNode.Position = new Vector2(0f, Length * 0.5f);   // 判定区从顶端往下铺满
        }

        // ② W 提示键帽摆在藤蔓底端旁边。
        //    必须在 EMarker._Ready 之后改（子节点先 _Ready），而且要用 SetBasePosition，
        //    否则浮动动画每帧把 Position 写回它自己记下的原点，我改完立刻被覆盖。
        var hint = GetNodeOrNull<Sprite2D>(HintPath);
        if (hint != null)
        {
            var pos = new Vector2(-24f - Mathf.Max(0f, ArtScale - 1f) * 10f, Length - 60f);
            if (hint is EMarker em) em.SetBasePosition(pos);
            else hint.Position = pos;
        }

        QueueRedraw();
    }

    /// <summary>
    /// 把可平铺小片从顶端往下堆满 Length。
    /// 最后一片不满就裁掉下半截 —— 直接缩放会让像素变糊，裁切不会。
    /// </summary>
    public override void _Draw()
    {
        if (Tile == null) return;

        float tw = Tile.GetWidth();
        float th = Tile.GetHeight();
        if (th <= 0f) return;

        float sc = Mathf.Max(0.1f, ArtScale);
        float dw = tw * sc;          // 放大后的宽
        float dh = th * sc;          // 放大后单片的高

        int count = Mathf.CeilToInt(Length / dh);
        for (int i = 0; i < count; i++)
        {
            float y = i * dh;
            float h = Mathf.Min(dh, Length - y);
            if (h <= 0f) break;
            // DrawTextureRectRegion(贴图, 目标矩形, 源矩形)：源矩形取上半截，不满一片就裁
            // 源矩形要按放大倍数换算回纹理像素，否则最后一片会被压扁
            DrawTextureRectRegion(Tile,
                new Rect2(-dw * 0.5f, y, dw, h),                        // 摆到藤蔓中线上
                new Rect2(0f, 0f, tw, h / sc));
        }
    }

    /// <summary>
    /// 编辑器里改 Length / 换 Tile 不会自动重画（Inspector 改值不标脏），
    /// 所以这里盯着这两个值，一变就 QueueRedraw。运行时直接返回，不做任何事。
    /// </summary>
    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!Engine.IsEditorHint()) return;

        if (!Mathf.IsEqualApprox(_previewLength, Length) || _previewTile != Tile)
        {
            _previewLength = Length;
            _previewTile = Tile;
            QueueRedraw();
        }
    }
}
