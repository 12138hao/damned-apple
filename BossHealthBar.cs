using Godot;

/// <summary>
/// 顶部 Boss 血条：金色边框 + 红色填充 + 苹果图标 + 名字。
///
/// 为什么单独一个节点：大苹果（BossArena）和野兽苹果（BeastArena）的这套 UI
/// 原本各自复制了一份，除了局部变量名以外逐字相同。抽出来以后改一次两边都变。
///
/// 用法（在 Arena 里）：
/// <code>
/// _bar = new BossHealthBar();
/// AddChild(_bar);
/// _bar.Setup(boss, BossName, BarWidth, BarTop);
/// // 每帧：_bar.Refresh();
/// // 收尾：_bar.QueueFree();
/// </code>
/// </summary>
public partial class BossHealthBar : CanvasLayer
{
    private const string FramePath = "res://assets/ui/boss/boss_bar_frame.png";
    private const string IconPath = "res://assets/ui/boss/boss_icon_apple.png";

    private Enemy _boss;
    private ColorRect _fill;
    private float _innerW;      // 框内可填充宽度

    /// <summary>建好血条并挂到当前 CanvasLayer 上。</summary>
    public void Setup(Enemy boss, string bossName, float barWidth, float barTop)
    {
        _boss = boss;
        Layer = 10;

        // 逻辑分辨率固定 800×450（project.godot 里 stretch/mode="viewport"），
        // 用 viewport 宽度而不是写死 800，以后改分辨率血条也会跟着居中。
        float screenW = GetViewport().GetVisibleRect().Size.X;
        float x = (screenW - barWidth) * 0.5f;
        float frameH = barWidth * 0.25f * 0.3f;   // 金框图 4:1，再压到 30% 高度（细长条）
        float frameY = barTop - 14f;
        float pad = frameH * 0.16f;               // 边框厚度（填充往里缩这么多）
        _innerW = barWidth - pad * 2f;

        // ① 红色填充（先加，在底层）
        _fill = new ColorRect
        {
            Color = new Color(0.82f, 0.17f, 0.20f),
            Position = new Vector2(x + pad, frameY + pad),
            Size = new Vector2(_innerW, frameH - pad * 2f),
        };
        AddChild(_fill);

        // ② 金色边框（后加，盖在填充上；中间透明所以能看见填充）
        var frame = new TextureRect
        {
            Texture = GD.Load<Texture2D>(FramePath),
            Position = new Vector2(x, frameY),
            Size = new Vector2(barWidth, frameH),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        AddChild(frame);
        frame.Size = new Vector2(barWidth, frameH);   // 加完再设，否则被贴图原始尺寸覆盖

        // ③ 苹果图标（血条最左边）
        var icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>(IconPath),
            Position = new Vector2(x - 50f, frameY + frameH * 0.5f - 23f),
            Size = new Vector2(46f, 46f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        AddChild(icon);
        icon.Size = new Vector2(46f, 46f);

        // ④ 名字
        var name = new Label
        {
            Text = bossName,
            Position = new Vector2(x, frameY - 28f),
        };
        name.AddThemeFontSizeOverride("font_size", 22);
        name.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.72f));
        name.AddThemeColorOverride("font_outline_color", new Color(0.06f, 0.04f, 0.06f));
        name.AddThemeConstantOverride("outline_size", 7);
        AddChild(name);
    }

    /// <summary>每帧刷新填充宽度。</summary>
    public void Refresh()
    {
        if (_fill == null || !IsInstanceValid(_fill)) return;
        if (_boss == null || !IsInstanceValid(_boss)) return;

        float ratio = Mathf.Clamp(_boss.Hp / (float)Mathf.Max(1, _boss.MaxHp), 0f, 1f);
        _fill.Size = new Vector2(_innerW * ratio, _fill.Size.Y);
    }
}
