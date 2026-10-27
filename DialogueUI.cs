using Godot;

/// <summary>
/// 跟随说话者的冒泡对话框：气泡画在说话者左上方，尾巴指向说话者。
/// 必须挂在角色节点（Node2D）下，不能放 CanvasLayer 里，否则会固定在屏幕上。
/// 子节点只需要一个名为 Text 的 Label。
///
/// 注意：这个脚本**只管显示**，不处理按键。对话流程（谁说话、什么时候推进）
/// 由 Npc.cs 统一管理，避免两个气泡同时响应 E 键导致一次跳两句。
/// </summary>
public partial class DialogueUI : Node2D
{
    [Signal] public delegate void DialogueEndedEventHandler();
    [Signal] public delegate void LineChangedEventHandler(int index);

    [Export] public Vector2 BubbleSize = new Vector2(280f, 100f);  // 气泡大小（字号变大，气泡同步放大）
    [Export] public int FontSize = 20;                             // 文字字号
    [Export] public Color FillColor = new Color(0.98f, 0.98f, 1f, 0.96f);
    [Export] public Color BorderColor = new Color(0.12f, 0.12f, 0.18f);
    [Export] public Color TextColor = new Color(0.10f, 0.10f, 0.15f);
    [Export] public float BorderWidth = 2f;
    [Export] public float CornerRadius = 10f;
    [Export] public float TailWidth = 18f;                    // 尾巴根部宽度
    [Export] public Vector2 TailTip = new Vector2(88f, 62f);  // 尾巴尖端（相对气泡中心）
    [Export] public float Padding = 12f;                      // 文字内边距

    // ---- 打字机 ----
    [Export] public float CharsPerSecond = 30f;   // 每秒显示几个字（<=0 表示整句瞬间出现）
    [Export] public int BlipsPerChar = 2;         // 每显示几个字响一声
    [Export] public string BlipSfx = "blip";      // 音效名，对应 assets/audio/sfx/<名字>.wav
    [Export] public float BlipPitch = 1f;         // 音高倍率：不同角色填不同值就分开了
    [Export] public float BlipVolume = 0.55f;     // 单次播放的音量倍率

    private Label _text;
    private StyleBoxFlat _style;

    private string _fullText = "";
    private int _shown;          // 已经显示到第几个字
    private int _blipCount;      // 这一句已经响过几声
    private float _typeTimer;

    public bool IsActive { get; private set; }
    public bool IsTyping { get; private set; }   // 打字机是否还在跑

    public override void _Ready()
    {
        _text = GetNodeOrNull<Label>("Text");

        _style = new StyleBoxFlat
        {
            BgColor = FillColor,
            BorderColor = BorderColor,
        };
        _style.SetBorderWidthAll((int)BorderWidth);
        _style.SetCornerRadiusAll((int)CornerRadius);

        if (_text != null)
        {
            _text.Visible = false;
            _text.AddThemeColorOverride("font_color", TextColor);
            _text.AddThemeFontSizeOverride("font_size", FontSize);
            LayoutText();
        }

        Visible = false;
    }

    /// <summary>显示一句话（不处理按键，流程由外部驱动）。</summary>
    public void ShowLine(string line)
    {
        if (_text == null) return;

        IsActive = true;
        Visible = true;
        _text.Visible = true;

        _fullText = line ?? "";
        _text.Text = _fullText;

        // 从头开始逐字显示
        _shown = 0;
        _blipCount = 0;
        _typeTimer = 0f;
        _text.VisibleCharacters = 0;

        // 速度设成 0，或者台词是空的 → 直接全出来
        IsTyping = CharsPerSecond > 0f && _fullText.Length > 0;
        if (!IsTyping) _text.VisibleCharacters = -1;

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!IsTyping || _text == null) return;

        _typeTimer += (float)delta * CharsPerSecond;
        int target = Mathf.Min(_fullText.Length, (int)_typeTimer);

        if (target > _shown)
        {
            _shown = target;
            _text.VisibleCharacters = _shown;

            // 每 BlipsPerChar 个字响一声。
            // 音高带 ±4% 随机，否则连续几十声会像蜂鸣器一样死板。
            int want = _shown / Mathf.Max(1, BlipsPerChar);
            if (want > _blipCount)
            {
                _blipCount = want;
                AudioManager.Instance?.PlaySfx(
                    BlipSfx, BlipVolume, BlipPitch * (0.96f + GD.Randf() * 0.08f));
            }
        }

        if (_shown >= _fullText.Length) IsTyping = false;
    }

    /// <summary>立刻把整句显示完（玩家抢先按 E 时调用，避免跳字）。</summary>
    public void FinishTyping()
    {
        IsTyping = false;
        _shown = _fullText.Length;
        if (_text != null) _text.VisibleCharacters = -1;
    }

    /// <summary>隐藏气泡。</summary>
    public void HideBubble()
    {
        IsActive = false;
        IsTyping = false;
        Visible = false;
        if (_text != null) _text.Visible = false;
    }

    public override void _Draw()
    {
        if (!IsActive) return;

        // 气泡主体
        var half = BubbleSize * 0.5f;
        var rect = new Rect2(-half.X, -half.Y, BubbleSize.X, BubbleSize.Y);
        DrawStyleBox(_style, rect);

        // 尾巴：从底边伸出一个三角形，尖端指向说话者
        float baseY = half.Y - 1f;
        var p1 = new Vector2(TailTip.X - TailWidth * 0.5f, baseY);
        var p2 = new Vector2(TailTip.X + TailWidth * 0.5f, baseY);
        var p3 = TailTip;

        DrawColoredPolygon(new[] { p1, p2, p3 }, FillColor);
        DrawLine(p1, p3, BorderColor, BorderWidth);
        DrawLine(p2, p3, BorderColor, BorderWidth);
    }

    private void LayoutText()
    {
        if (_text == null) return;
        var half = BubbleSize * 0.5f;
        _text.Position = new Vector2(-half.X + Padding, -half.Y + Padding);
        _text.Size = new Vector2(BubbleSize.X - Padding * 2f, BubbleSize.Y - Padding * 2f);
        _text.HorizontalAlignment = HorizontalAlignment.Center;
        _text.VerticalAlignment = VerticalAlignment.Center;
        _text.AutowrapMode = TextServer.AutowrapMode.Word;
    }
}
