using Godot;

/// <summary>
/// 结局演出：和奶蛙说完最后一段话 → 缓慢黑屏 → 滚动字幕 → 回主菜单。
///
/// 用法（在 eden.tscn 里）：
///   把本节点挂在场景根下，`NpcPath` 指向奶蛙（留空会自动找同级里叫 naiwa 的 Npc）。
///   台词写在 Npc 的 FirstLines / RepeatLines 上；字幕写在本节点的 Credits 里。
///
/// 只有「把台词说完」才会触发；中途走开被打断不会触发。
/// </summary>
public partial class EndingSequence : Node
{
    [ExportGroup("触发")]
    // 留空 = 自动找同级里名字是 naiwa 的 Npc 节点
    [Export] public NodePath NpcPath;

    [ExportGroup("字幕内容（随便改，支持多行）")]
    // 在检查器里是多行输入框；\n 换行。想加空行就连续两个 \n。
    [Export(PropertyHint.MultilineText)]
    public string Credits =
        "《我的第一个游戏》\n" +
        "\n" +
        "\n" +
        "—— 制作 ——\n" +
        "\n" +
        "策划 / 程序 / 美术\n" +
        "老大\n" +
        "\n" +
        "\n" +
        "—— 技术 ——\n" +
        "\n" +
        "Godot Engine 4.7\n" +
        "C# / .NET\n" +
        "\n" +
        "\n" +
        "—— 致谢 ——\n" +
        "\n" +
        "一路陪玩到这里的你\n" +
        "谢谢\n" +
        "\n" +
        "\n" +
        "感谢游玩";

    [ExportGroup("演出节奏")]
    [Export] public float FadeOutTime = 2.5f;    // 对话结束后慢慢黑屏用多久
    [Export] public float BlackHoldTime = 0.8f;  // 全黑之后停一下再出字幕
    [Export] public float ScrollSpeed = 45f;     // 字幕滚动速度（像素/秒，越小越慢）
    [Export] public float EndHoldTime = 2.0f;    // 滚完之后停多久再回主菜单
    [Export] public int FontSize = 22;
    [Export] public int LineSpacing = 12;
    [Export] public Color TextColor = new Color(0.94f, 0.91f, 0.84f);

    [ExportGroup("去向")]
    [Export] public string MenuScene = "res://MainMenu.tscn";

    [ExportGroup("结尾曲")]
    // 黑屏之后开始播，一路播到主菜单，进下一关才会被新场景的曲子替掉。
    // 对应 res://assets/audio/bgm/<名字>.mp3；想换曲子改这里，留空 = 黑屏时不换曲。
    [Export] public string EndingBgm = "ending";
    [Export] public float EndingBgmFade = 1.5f;

    private bool _playing;
    private bool _skip;
    private Box _player;
    private ColorRect _black;
    private Label _creditLabel;

    public override void _Ready()
    {
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        Bind(ResolveNpc());
    }

    public override void _ExitTree()
    {
        Unbind(ResolveNpc());
    }

    private Npc ResolveNpc()
    {
        if (NpcPath != null && !NpcPath.IsEmpty)
        {
            var n = GetNodeOrNull<Npc>(NpcPath);
            if (n != null) return n;
        }

        // 没填路径：先在同级里找叫 naiwa 的，再退化成任意 Npc
        foreach (var c in GetParent().GetChildren())
            if (c is Npc npc && npc.Name.ToString().ToLowerInvariant().Contains("naiwa"))
                return npc;
        foreach (var c in GetParent().GetChildren())
            if (c is Npc npc) return npc;
        return null;
    }

    private void Bind(Npc npc)
    {
        if (npc == null) return;
        npc.TalkFinished += OnTalkFinished;
    }

    private void Unbind(Npc npc)
    {
        if (npc == null || !IsInstanceValid(npc)) return;
        npc.TalkFinished -= OnTalkFinished;
    }

    private void OnTalkFinished()
    {
        Play();
    }

    /// <summary>开始结局演出。重复调用只会生效一次。</summary>
    public async void Play()
    {
        if (_playing) return;
        _playing = true;
        _skip = false;

        // 锁住玩家：不能走、不能再按 E 触发别的交互
        if (GameState.I != null) GameState.I.DialogueActive = true;
        _player?.BeginCutscene();

        // 隐藏 HUD（血条、武器冷却这些在字幕里很出戏）
        var hud = GetTree().Root.GetNodeOrNull<Hud>("Hud");
        bool hudWasVisible = hud != null && hud.Visible;
        if (hud != null) hud.Visible = false;

        BuildOverlay();

        // ① 缓慢黑屏
        var fade = CreateTween();
        fade.TweenProperty(_black, "color:a", 1f, Mathf.Max(0.05f, FadeOutTime));
        await ToSignal(fade, Tween.SignalName.Finished);

        // 黑透了才起结尾曲：一路播到主菜单，进下一关才被新场景的曲子替掉。
        // 放在跳过判断之前 —— 就算玩家把字幕跳了，这首曲子照样进主菜单。
        if (!string.IsNullOrEmpty(EndingBgm))
            AudioManager.Instance?.PlayBgm(EndingBgm, Mathf.Max(0.05f, EndingBgmFade));

        if (_skip) { Finish(); return; }

        // ② 黑场里起字幕
        var label = BuildCredits();
        await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, BlackHoldTime)), SceneTreeTimer.SignalName.Timeout);
        if (_skip) { Finish(); return; }

        float screenH = GetViewport().GetVisibleRect().Size.Y;
        float textH = label.Size.Y;
        float distance = screenH + textH;
        float dur = Mathf.Max(1f, distance / Mathf.Max(5f, ScrollSpeed));

        var scroll = CreateTween();
        scroll.TweenProperty(label, "position:y", -textH, dur).SetTrans(Tween.TransitionType.Linear);
        await ToSignal(scroll, Tween.SignalName.Finished);

        // ③ 停一会儿再回主菜单
        if (!_skip)
            await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, EndHoldTime)), SceneTreeTimer.SignalName.Timeout);

        if (hud != null && hudWasVisible) hud.Visible = true;
        Finish();
    }

    private void Finish()
    {
        // 记下"已通关"，并存盘：主菜单点「继续游戏」时靠它弹提示
        if (GameState.I != null) GameState.I.Cleared = true;
        if (GameState.I != null) GameState.I.DialogueActive = false;
        SaveSystem.Save();

        GetTree().ChangeSceneToFile(MenuScene);
    }

    public override void _Process(double delta)
    {
        if (!_playing || _skip) return;

        // 黑屏之后按 E / ESC 可以直接跳过剩下的字幕
        if (_black != null && _black.Color.A >= 0.99f
            && (Input.IsActionJustPressed("interact") || Input.IsActionJustPressed("ui_cancel")))
        {
            _skip = true;
        }
    }

    // ────────────────────────────────────────── 画面

    private void BuildOverlay()
    {
        var layer = new CanvasLayer { Name = "EndingLayer", Layer = 100 };
        AddChild(layer);

        var vp = GetViewport().GetVisibleRect().Size;

        _black = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0f),
            Position = Vector2.Zero,
            Size = vp,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(_black);
    }

    private Label BuildCredits()
    {
        var vp = GetViewport().GetVisibleRect().Size;

        _creditLabel = new Label
        {
            Text = Credits,
            Position = new Vector2(0f, vp.Y),
            Size = new Vector2(vp.X, EstimateHeight(Credits, vp.X, FontSize, LineSpacing)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _creditLabel.AddThemeFontSizeOverride("font_size", FontSize);
        _creditLabel.AddThemeConstantOverride("line_spacing", LineSpacing);
        _creditLabel.AddThemeColorOverride("font_color", TextColor);

        ((CanvasLayer)_black.GetParent()).AddChild(_creditLabel);
        return _creditLabel;
    }

    /// <summary>
    /// 估一下这段字幕排出来有多高（Label 没有 fit_content 可用，
    /// 滚动距离得自己算；宁可算高一点，别把最后一行截掉）。
    /// 中文按 1 字号算宽，ASCII 按 0.52 字号算。
    /// </summary>
    private static float EstimateHeight(string text, float width, int fontSize, int lineSpacing)
    {
        float lineH = fontSize * 1.25f + lineSpacing;
        float safeW = Mathf.Max(40f, width - 40f);
        int lines = 0;

        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            float w = 0f;
            foreach (char ch in raw)
                w += ch < 128 ? fontSize * 0.52f : fontSize * 1.0f;

            lines += Mathf.Max(1, Mathf.CeilToInt(w / safeW));
        }

        return lines * lineH + fontSize * 2f;
    }
}
