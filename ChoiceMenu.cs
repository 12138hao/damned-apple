using Godot;

/// <summary>
/// 四选一对话菜单：面板画在世界坐标里（挂在 NPC 下面），用**鼠标滚轮**切换选项、
/// **鼠标左键**确认。也支持把鼠标直接移到某一行上（悬停＝选中），
/// 这样「滚轮党」和「点击党」都顺手。
///
/// 只负责「选第几个」，选了之后播什么由 OldAppleNpc 决定 —— 和 DialogueUI 一样，
/// 这个脚本不碰按键流程，避免两个节点抢同一次 E。
///
/// 文字用 Label 子节点而不是 DrawString：Label 会继承项目的主题字体，
/// 中文才渲染得出来（DrawString 用的是 ThemeDB.FallbackFont，不一定有中文字形）。
/// </summary>
public partial class ChoiceMenu : Node2D
{
    [Signal] public delegate void PickedEventHandler(int index);

    [ExportGroup("面板")]
    [Export] public Vector2 PanelSize = new Vector2(320f, 152f);
    [Export] public float Padding = 10f;
    [Export] public float RowHeight = 32f;
    [Export] public float BorderWidth = 2f;
    [Export] public float CornerRadius = 8f;

    [ExportGroup("配色")]
    [Export] public Color FillColor = new Color(0.06f, 0.08f, 0.10f, 0.95f);
    [Export] public Color BorderColor = new Color(0.86f, 0.76f, 0.42f);
    [Export] public Color TextColor = new Color(0.90f, 0.91f, 0.88f);
    [Export] public Color SelTextColor = new Color(1f, 0.94f, 0.55f);
    [Export] public Color SelFillColor = new Color(0.86f, 0.73f, 0.26f, 0.22f);

    [ExportGroup("文字")]
    [Export] public int FontSize = 16;
    [Export] public bool ShowNumbers = true;    // 每行前面自动加「1. 2. 3. 4.」

    private readonly Label[] _rows = new Label[4];
    private StyleBoxFlat _style;

    private int _count;
    private int _index;
    private bool _open;

    public bool IsOpen => _open;
    public int Current => _index;

    public override void _Ready()
    {
        _style = new StyleBoxFlat { BgColor = FillColor, BorderColor = BorderColor };
        _style.SetBorderWidthAll((int)BorderWidth);
        _style.SetCornerRadiusAll((int)CornerRadius);

        for (int i = 0; i < _rows.Length; i++)
        {
            var lb = new Label
            {
                Name = $"Row{i + 1}",
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = false,
            };
            lb.AddThemeFontSizeOverride("font_size", FontSize);
            lb.AddThemeColorOverride("font_color", TextColor);
            AddChild(lb);
            _rows[i] = lb;
        }

        Visible = false;
    }

    // ---------------------------------------------------------------- 开关

    public void Open(string[] titles)
    {
        _count = Mathf.Clamp(titles?.Length ?? 0, 1, _rows.Length);
        for (int i = 0; i < _rows.Length; i++)
        {
            bool used = i < _count;
            _rows[i].Visible = used;
            if (!used) continue;
            string t = titles != null && i < titles.Length ? titles[i] : $"...";
            _rows[i].Text = ShowNumbers ? $"{i + 1}. {t}" : t;
        }

        _index = 0;
        _open = true;
        Visible = true;
        LayoutRows();
        RefreshColors();
        QueueRedraw();
    }

    public void Close()
    {
        _open = false;
        Visible = false;
        foreach (var r in _rows) r.Visible = false;
        QueueRedraw();
    }

    /// <summary>外部（NPC 里按 E）确认当前高亮项。</summary>
    public void ConfirmCurrent()
    {
        if (!_open) return;
        Pick(_index);
    }

    private void Pick(int i)
    {
        if (!_open) return;
        if (i < 0 || i >= _count) return;
        _index = i;
        RefreshColors();
        QueueRedraw();
        EmitSignal(SignalName.Picked, i);
    }

    // ---------------------------------------------------------------- 输入

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_open) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.WheelUp:
                    Move(-1);
                    GetViewport().SetInputAsHandled();
                    return;
                case MouseButton.WheelDown:
                    Move(1);
                    GetViewport().SetInputAsHandled();
                    return;
                case MouseButton.Left:
                    // 点在某一行的范围里 → 直接选那一行；点在面板别处 → 选当前高亮
                    int hit = RowAt(EventLocal(mb));
                    Pick(hit >= 0 ? hit : _index);
                    GetViewport().SetInputAsHandled();
                    return;
            }
        }

        // 悬停＝选中。只在真的移动鼠标时才改，免得滚轮刚选完、鼠标稍微一抖就被抢回去。
        if (@event is InputEventMouseMotion)
        {
            int hit = RowAt(EventLocal(@event));
            if (hit >= 0 && hit != _index)
            {
                _index = hit;
                RefreshColors();
                QueueRedraw();
            }
        }
    }

    /// <summary>
    /// 把事件坐标换算到本节点的局部空间。
    /// 不用 GetLocalMousePosition()：那个取的是真实光标位置，而事件里带的是发出时的位置，
    /// 两者在「用代码合成的事件」下不一致（做自动测试时就会对不上）。
    /// </summary>
    private Vector2 EventLocal(InputEvent e)
    {
        var local = MakeInputLocal(e);
        return local is InputEventMouse m ? m.Position : GetLocalMousePosition();
    }

    private void Move(int step)
    {
        if (_count <= 0) return;
        _index = ((_index + step) % _count + _count) % _count;   // 循环
        RefreshColors();
        QueueRedraw();
    }

    // ---------------------------------------------------------------- 布局 / 绘制

    private Rect2 PanelRect =>
        new Rect2(-PanelSize.X * 0.5f, -PanelSize.Y * 0.5f, PanelSize.X, PanelSize.Y);

    private Rect2 RowRect(int i)
    {
        if (_count <= 0) return new Rect2();
        float usable = PanelSize.Y - Padding * 2f;
        float h = Mathf.Min(RowHeight, usable / _count);
        float totalH = h * _count;
        float top = -totalH * 0.5f + i * h;
        return new Rect2(-PanelSize.X * 0.5f + Padding, top, PanelSize.X - Padding * 2f, h);
    }

    /// <summary>鼠标落在第几行；不在任何一行返回 -1。</summary>
    private int RowAt(Vector2 local)
    {
        for (int i = 0; i < _count; i++)
            if (RowRect(i).HasPoint(local)) return i;
        return -1;
    }

    private void LayoutRows()
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            if (!_rows[i].Visible) continue;
            Rect2 r = RowRect(i);
            _rows[i].Position = new Vector2(r.Position.X + 8f, r.Position.Y);
            _rows[i].Size = new Vector2(r.Size.X - 16f, r.Size.Y);
        }
    }

    private void RefreshColors()
    {
        for (int i = 0; i < _rows.Length; i++)
            _rows[i].AddThemeColorOverride("font_color", i == _index ? SelTextColor : TextColor);
    }

    public override void _Draw()
    {
        if (!_open) return;

        DrawStyleBox(_style, PanelRect);

        for (int i = 0; i < _count; i++)
            if (i == _index)
                DrawRect(RowRect(i), SelFillColor, true);
    }
}
