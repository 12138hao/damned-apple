using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 主菜单。三个入口：继续（读档）/ 新游戏（清档）/ 退出。
///
/// 按钮样式全部在代码里建，因为要按状态切换（禁用时压暗、悬停时提亮），
/// 在 .tscn 里写四份 StyleBox 反而更难维护。
/// </summary>
public partial class MainMenu : Control
{
    /// <summary>新游戏从哪一关开始。</summary>
    private const string FirstLevel = "res://firstry.tscn";

    private Button _continue;
    private Button _newGame;
    private Button _quit;
    private ConfirmationDialog _confirm;

    /// <summary>站在草地上的 box 立绘。</summary>
    private Sprite2D _boxSprite;
    private float _breathTimer;
    private int _breathFrame;

    /// <summary>飘动的云：精灵 + 每秒横向速度 + 贴图在屏幕上的实际宽度。</summary>
    private readonly List<(Sprite2D Sprite, float Speed, float Width)> _clouds = new();

    /// <summary>云飘到这个 x 就绕回去（树冠左缘约 430，别让云压到树上）。</summary>
    private const float CloudRightLimit = 430f;

    /// <summary>呼吸帧切换间隔，和游戏里 Box.BreathInterval 保持一致。</summary>
    private const float BreathInterval = 0.7f;

    /// <summary>正在切场景，防止连点两次。</summary>
    private bool _busy;

    public override void _Ready()
    {
        // Hud 是 autoload、跨场景常驻。从游戏回到主菜单时它还亮着，
        // 这里强制藏起来（Hud 自己在 BindPlayer 时会重新显示）。
        var hud = GetTree().Root.GetNodeOrNull<Hud>("Hud");
        if (hud != null) hud.Visible = false;

        _continue = GetNode<Button>("Buttons/ContinueButton");
        _newGame = GetNode<Button>("Buttons/NewGameButton");
        _quit = GetNode<Button>("Buttons/QuitButton");

        BuildButtonStyles();
        BuildSky();
        BuildBoxSprite();
        BuildLeaves();

        bool hasSave = SaveSystem.HasSave();
        _continue.Disabled = !hasSave;
        _continue.TooltipText = hasSave ? "读取上次的进度" : "还没有存档，先玩一局吧";

        _continue.Pressed += OnContinue;
        _newGame.Pressed += OnNewGame;
        _quit.Pressed += () => GetTree().Quit();

        // 键盘 / 手柄：默认焦点落在第一个可用按钮上
        if (!_continue.Disabled) _continue.GrabFocus();
        else _newGame.GrabFocus();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 云：向左飘，飘出画面就从右边绕回来，两端用透明度淡入淡出盖住接缝
        foreach (var (sprite, speed, width) in _clouds)
        {
            if (!IsInstanceValid(sprite)) continue;

            var p = sprite.Position;
            p.X -= speed * dt;
            if (p.X < -width) p.X = CloudRightLimit;
            sprite.Position = p;

            float fadeIn = Mathf.Clamp((CloudRightLimit - p.X) / 70f, 0f, 1f);
            float fadeOut = Mathf.Clamp((p.X + width) / 70f, 0f, 1f);
            sprite.Modulate = new Color(1f, 1f, 1f, 0.95f * fadeIn * fadeOut);
        }

        if (_boxSprite == null || !IsInstanceValid(_boxSprite)) return;

        _breathTimer += dt;
        if (_breathTimer >= BreathInterval)
        {
            _breathTimer = 0f;
            _breathFrame = 1 - _breathFrame;
            _boxSprite.Frame = _breathFrame;
        }
    }

    // ── 按钮行为 ──────────────────────────────────────────────

    private async void OnContinue()
    {
        if (_busy) return;

        if (!SaveSystem.LoadIntoState())
        {
            // 存档损坏或版本不符：当作没档，别把玩家卡在这里
            _continue.Disabled = true;
            _continue.TooltipText = "存档读取失败";
            GD.PushWarning("[MainMenu] 读档失败，已禁用「继续游戏」");
            return;
        }

        _busy = true;
        SetButtonsEnabled(false);
        await FadeOut();
        GetTree().ChangeSceneToFile(SaveSystem.PendingScene);
    }

    private void OnNewGame()
    {
        if (_busy) return;

        // 没有存档就直接开，不用吓唬人
        if (!SaveSystem.HasSave())
        {
            StartNewGame();
            return;
        }

        if (_confirm == null) _confirm = BuildConfirm();
        _confirm.PopupCentered(new Vector2I(360, 150));
    }

    private async void StartNewGame()
    {
        if (_busy) return;
        _busy = true;
        SetButtonsEnabled(false);

        GameState.I?.NewGame();
        SaveSystem.Delete();

        await FadeOut();
        GetTree().ChangeSceneToFile(FirstLevel);
    }

    // ── 界面搭建 ──────────────────────────────────────────────

    private ConfirmationDialog BuildConfirm()
    {
        var d = new ConfirmationDialog
        {
            Title = "开始新游戏",
            DialogText = "当前存档会被覆盖，确定要重新开始吗？",
            OkButtonText = "覆盖并开始",
            CancelButtonText = "取消",
        };
        AddChild(d);
        d.Confirmed += StartNewGame;
        return d;
    }

    /// <summary>
    /// 天空装饰：太阳 + 三朵飘动的云（不放放射光线，老大嫌太抢）。
    /// 素材是 tools/gen_sky_assets.gd 程序化画出来的像素图，不是模型生成的。
    /// 全部压在 z -7 ~ -8，在背景（-10）之上、落叶（-5）和小人（-4）之下。
    /// </summary>
    private void BuildSky()
    {
        const float SunX = 148f;
        const float SunY = 100f;

        // 太阳本体。光晕已经画进贴图里（最外圈 alpha 24），不需要额外的光晕节点。
        AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/sun.png"),
            Position = new Vector2(SunX, SunY),
            Scale = new Vector2(1.8f, 1.8f),   // 64 → 115
            ZIndex = -8,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        });

        // 三朵云：高度、大小、速度都错开，才不会像排队
        var big = GD.Load<Texture2D>("res://assets/ui/cloud_a.png");
        var small = GD.Load<Texture2D>("res://assets/ui/cloud_b.png");
        AddCloud(big, new Vector2(300f, 170f), 1.6f, 7f);
        AddCloud(small, new Vector2(120f, 218f), 1.3f, 5f);
        AddCloud(big, new Vector2(60f, 60f), 1.1f, 9f);
    }

    private void AddCloud(Texture2D tex, Vector2 pos, float scale, float speed)
    {
        var s = new Sprite2D
        {
            Texture = tex,
            Position = pos,
            Scale = new Vector2(scale, scale),
            ZIndex = -7,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        AddChild(s);
        _clouds.Add((s, speed, tex.GetWidth() * scale));
    }

    /// <summary>
    /// 站在草地上的 box 立绘。贴图是游戏在用的 player_sheet_v2.png
    /// （1344×64，21 帧，每帧 64×64），帧 0/1 就是站立呼吸的两帧。
    /// </summary>
    private void BuildBoxSprite()
    {
        var tex = GD.Load<Texture2D>("res://assets/player_sheet_v2.png");
        _boxSprite = new Sprite2D
        {
            Texture = tex,
            Hframes = 21,
            Frame = 0,
            Scale = new Vector2(1.9f, 1.9f),
            Position = new Vector2(112f, 360f),   // 左侧空地上，脚踩草地
            ZIndex = -4,                 // 在背景和落叶之上、UI 之下
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        AddChild(_boxSprite);
    }

    /// <summary>从右侧树冠不停飘下来的落叶。</summary>
    private void BuildLeaves()
    {
        var leaf = GD.Load<Texture2D>("res://assets/ui/leaf.png");

        // 出生颜色在嫩绿到枯黄之间随机
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(0.66f, 0.85f, 0.46f));
        ramp.SetColor(1, new Color(0.90f, 0.68f, 0.30f));

        var p = new CpuParticles2D
        {
            Texture = leaf,
            Amount = 60,
            Lifetime = 11f,
            Preprocess = 5f,             // 一进菜单就已经有叶子在半空
            Position = new Vector2(590f, 180f),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(180f, 130f),   // 覆盖树冠中下部
            // 略微偏左：叶子往空旷的左侧飘才看得见，垂直落下会埋进树冠里
            Direction = new Vector2(-0.18f, 1f),
            Spread = 26f,
            Gravity = new Vector2(0f, 20f),
            InitialVelocityMin = 12f,
            InitialVelocityMax = 40f,
            AngularVelocityMin = -80f,
            AngularVelocityMax = 80f,
            ScaleAmountMin = 0.9f,
            ScaleAmountMax = 1.8f,
            ColorInitialRamp = ramp,
            ZIndex = -5,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        AddChild(p);
    }

    private void BuildButtonStyles()
    {
        // 木牌贴图：Seedream 生成 → 品红抠图 → 512×102。
        // 用 9-slice 拉伸，保住两端的钉子和上下不规则边缘，只拉中间的木纹。
        var tex = GD.Load<Texture2D>("res://assets/ui/wood_button.png");

        var normal = MakeWood(tex, Colors.White);
        var hover = MakeWood(tex, new Color(1.20f, 1.14f, 1.05f));
        var pressed = MakeWood(tex, new Color(0.80f, 0.76f, 0.70f));
        // 禁用态：木牌压暗，但字要比木牌亮，否则整块糊成一片看不清
        var disabled = MakeWood(tex, new Color(0.46f, 0.46f, 0.50f));

        foreach (var b in new[] { _continue, _newGame, _quit })
        {
            b.AddThemeStyleboxOverride("normal", normal);
            b.AddThemeStyleboxOverride("hover", hover);
            b.AddThemeStyleboxOverride("pressed", pressed);
            b.AddThemeStyleboxOverride("focus", hover);
            b.AddThemeStyleboxOverride("disabled", disabled);

            // 奶油色字压在深棕木头上，描边用更深的褐色
            b.AddThemeColorOverride("font_color", new Color(0.96f, 0.92f, 0.82f));
            b.AddThemeColorOverride("font_hover_color", new Color(1f, 0.98f, 0.90f));
            b.AddThemeColorOverride("font_pressed_color", new Color(0.88f, 0.84f, 0.74f));
            b.AddThemeColorOverride("font_disabled_color", new Color(0.74f, 0.72f, 0.68f));
            b.AddThemeColorOverride("font_outline_color", new Color(0.14f, 0.08f, 0.04f));
            b.AddThemeConstantOverride("outline_size", 5);
        }
    }

    private static StyleBoxTexture MakeWood(Texture2D tex, Color tint)
    {
        var s = new StyleBoxTexture
        {
            Texture = tex,
            ModulateColor = tint,
        };
        // 上下各 16：按钮 50 高，边距之和必须小于它，否则九宫格会上下重叠。
        // 左右各 80：足够包住两端的钉子。
        s.TextureMarginLeft = 80;
        s.TextureMarginRight = 80;
        s.TextureMarginTop = 16;
        s.TextureMarginBottom = 16;

        s.ContentMarginLeft = 16;
        s.ContentMarginRight = 16;
        s.ContentMarginTop = 6;
        s.ContentMarginBottom = 6;
        return s;
    }

    private void SetButtonsEnabled(bool on)
    {
        _newGame.Disabled = !on;
        _quit.Disabled = !on;
        if (!_continue.Disabled) _continue.Disabled = !on;
    }

    private async Task FadeOut()
    {
        var t = CreateTween();
        t.TweenProperty(this, "modulate:a", 0.0f, 0.28f);
        await ToSignal(t, Tween.SignalName.Finished);
    }
}
