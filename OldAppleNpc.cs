using Godot;

/// <summary>
/// 老苹果：jungleinside 里那位白胡子苹果老人。
///
/// 流程和 <see cref="Npc"/> 的区别就在「再次对话」这一层：
///   · **第一次**按 E：直接播 <see cref="FirstLines"/>（内容自己做，随便填）；
///   · **之后**按 E：先弹一个**四选一菜单**，用鼠标滚轮切换、左键确认（或直接按 E 确认当前项），
///     选完再播对应那一段台词。
///
/// 台词沿用 Npc 的前缀约定：
///   "npc:..." 他说（气泡出现在他头上） / "me:..." 主角说（气泡在主角头上 + 轻跳）。
///
/// 美术：单帧贴图 + <see cref="Breath"/> 呼吸（没有多帧序列，靠纵向缩放做呼吸感）。
/// </summary>
public partial class OldAppleNpc : CharacterBody2D
{
    private enum Mode { None, Choice, Lines }

    [ExportGroup("交互")]
    [Export] public float TalkDistance = 150f;    // 离多近能按 E

    [ExportGroup("美术")]
    // 贴图 512 高，0.75 → 屏幕上 384 高。想改大小只动这个数，
    // 碰撞框、E 提示、气泡、菜单都会跟着重算（见 ApplyArtScale）。
    [Export] public float ArtScale = 0.75f;


    [ExportGroup("首次对话")]
    // 第一次搭话播这一串。想写多长写多长，前缀 npc: / me: 决定谁说话。
    [Export] public string[] FirstLines =
    {
        "npc:……哦？好久没见过活人走进这间屋子了。",
        "npc:我这把老骨头，在这儿守了不知道多少个年头。",
        "me:您一直一个人住在这儿？",
        "npc:（笑）一个人？这屋子里的东西，可比人热闹多了。",
        "npc:坐吧，想聊什么，随便挑。",
    };

    [ExportGroup("再次对话：四选一")]
    [Export] public string[] TopicTitles =
    {
        "这片林子是怎么回事",
        "您到底是什么人",
        "有什么能帮上忙的",
        "我先走了",
    };

    [Export] public string[] Topic1Lines =
    {
        "npc:（内容待填）",
    };

    [Export] public string[] Topic2Lines =
    {
        "npc:（内容待填）",
    };

    [Export] public string[] Topic3Lines =
    {
        "npc:（内容待填）",
    };

    [Export] public string[] Topic4Lines =
    {
        "npc:路上小心。",
    };

    [ExportGroup("主角回应")]
    [Export] public float PlayerTalkJump = 260f;  // 主角说话时向上轻跳的速度，0 = 不跳

    // ── 运行时 ────────────────────────────────────────────────
    private DialogueUI _npcBubble;
    private DialogueUI _playerBubble;
    private ChoiceMenu _menu;
    private Sprite2D _marker;
    private Box _player;

    private string[] _lines;
    private int _index;
    private bool _talking;
    private bool _inRange;
    private Mode _mode = Mode.None;

    private readonly string[][] _topics = new string[4][];
    private float _artW, _artH;               // 贴图按 ArtScale 之后的屏幕尺寸

    public override void _Ready()
    {
        _npcBubble = GetNodeOrNull<DialogueUI>("DialogueUI");
        _menu = GetNodeOrNull<ChoiceMenu>("ChoiceMenu");
        _marker = GetNodeOrNull<Sprite2D>("EMarker");

        LayoutUi();

        if (_menu != null) _menu.Picked += OnPicked;
        if (_marker != null) _marker.Visible = false;

        _player = GetTree().Root.FindChild("box", true, false) as Box;
        if (_player == null)
        {
            GD.PushWarning("[OldApple] 没找到玩家节点 box");
        }
        else
        {
            _playerBubble = _player.GetNodeOrNull<DialogueUI>("DialogueUI");
            if (_playerBubble == null)
                GD.PushWarning("[OldApple] 主角身上没有 DialogueUI，主角将无法开口");
            _player.Died += OnPlayerDied;
        }

        _topics[0] = Topic1Lines;
        _topics[1] = Topic2Lines;
        _topics[2] = Topic3Lines;
        _topics[3] = Topic4Lines;
    }

    /// <summary>
    /// 按 <see cref="ArtScale"/> 重算「精灵 / 碰撞框 / E 提示 / 气泡 / 菜单」的位置。
    /// 这样改大小只需要动一个数字，其他部件不会错位。
    ///
    /// ⚠ 必须在 _EnterTree 里做（父节点先于子节点），因为 Sprite2D 上挂着 Breath：
    /// Breath 在自己 _Ready 时把当前 Scale 记成呼吸基准，晚设的话会被它每帧写回旧值。
    /// </summary>
    public override void _EnterTree()
    {
        var spr = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (spr?.Texture == null) return;

        _artW = spr.Texture.GetWidth() * ArtScale;
        _artH = spr.Texture.GetHeight() * ArtScale;

        spr.Scale = Vector2.One * ArtScale;
        spr.Position = new Vector2(0f, -_artH * 0.5f);      // 脚底钉在节点原点

        // 碰撞框：宽度取身宽的 2/3，高度取下半个身子
        var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (col?.Shape is RectangleShape2D rect)
        {
            var dup = (RectangleShape2D)rect.Duplicate();   // 别改共享资源，否则多放几只一起变
            Vector2 size = new Vector2(_artW * 0.66f, _artH * 0.48f);
            dup.Size = size;
            col.Shape = dup;
            col.Position = new Vector2(0f, -size.Y * 0.5f);
        }
    }

    /// <summary>气泡 / 菜单 / 提示图标的位置。字段在 _Ready 里才拿得到，所以单独一段。</summary>
    private void LayoutUi()
    {
        // 提示图标永远在头顶
        if (_marker != null) _marker.Position = new Vector2(0f, -_artH - 26f);

        // 气泡**始终在头顶**：DialogueUI 的尾巴是朝右下伸的（TailTip=(88,62)），
        // 摆在头顶时尾巴正好戳到苹果的天灵盖，视觉上才是「从它身上说出来」。
        if (_npcBubble != null) _npcBubble.Position = new Vector2(-70f, -_artH - 60f);

        // 菜单面板高 152，头顶放不下（苹果顶端离画面顶只剩 ~150），所以摆在左侧。
        if (_menu != null)
            _menu.Position = new Vector2(-_artW * 0.5f - 170f, -_artH * 0.62f);

        QueueRedraw();      // 地面投影要在 _Ready 之后才能按 _artW 画
    }

    /// <summary>
    /// 地面投影。像素画里一个大圆东西没有接触影子会显得飘在半空 ——
    /// 这也是「老苹果浮空」的真正原因（精灵底部其实严丝合缝压在地板 y=341 上）。
    /// </summary>
    public override void _Draw()
    {
        if (_artW <= 0f) return;

        DrawEllipseShadow(1.00f, new Color(0f, 0f, 0f, 0.42f));
        DrawEllipseShadow(0.58f, new Color(0f, 0f, 0f, 0.30f));
    }

    /// <summary>画一个压扁的圆 = 椭圆影子。
    /// 用 DrawSetTransform 缩放坐标系再 DrawCircle，比 DrawColoredPolygon 稳
    /// （多边形的绕序/三角化在 Godot 4 里偶尔会画出空结果）。</summary>
    private void DrawEllipseShadow(float k, Color c)
    {
        float r = _artW * 0.48f * k;
        DrawSetTransform(new Vector2(0f, -2f), 0f, new Vector2(1f, 0.24f * k));
        DrawCircle(Vector2.Zero, r, c);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    public override void _ExitTree()
    {
        if (_player != null && IsInstanceValid(_player)) _player.Died -= OnPlayerDied;
        if (_menu != null) _menu.Picked -= OnPicked;
    }

    public override void _Process(double delta)
    {
        UpdateRange();

        if (!Input.IsActionJustPressed("interact")) return;

        // 别人正在说话（不是我说）→ 这次 E 不归我管
        if (!_talking && GameState.I != null && GameState.I.DialogueActive) return;

        if (!_talking)
        {
            if (_inRange) StartTalk();
            return;
        }

        // 菜单开着的时候，E 也能确认当前高亮项（鼠标党的备用键）
        if (_mode == Mode.Choice)
        {
            _menu?.ConfirmCurrent();
            return;
        }

        Advance();
    }

    private void UpdateRange()
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _inRange = false;
            return;
        }

        _inRange = _player.IsAlive
                   && GlobalPosition.DistanceTo(_player.GlobalPosition) <= TalkDistance;

        if (_marker != null)
        {
            bool want = _inRange && !_talking;
            if (_marker.Visible != want) _marker.Visible = want;
        }

        // 走远了自动结束，否则 DialogueActive 会一直卡住
        if (!_inRange && _talking) EndTalk();
    }

    // ---------------------------------------------------------------- 对话流程

    /// <summary>对话记录的 key：场景路径 + 节点路径（和 Npc.cs 一致，别串到别的关卡）。</summary>
    private string TalkKey
    {
        get
        {
            // 先看当前场景：这个 NPC 是做成独立场景再实例化进 jungleinside 的，
            // 只看 Owner 的话会拿到 res://OldApple.tscn，所有关卡共用一条记录。
            string scenePath = GetTree().CurrentScene?.SceneFilePath
                               ?? Owner?.SceneFilePath
                               ?? "";
            return scenePath + "::" + GetPath();
        }
    }

    private void StartTalk()
    {
        _talking = true;
        if (GameState.I != null) GameState.I.DialogueActive = true;
        if (_marker != null) _marker.Visible = false;

        bool talked = GameState.I != null && GameState.I.HasTalked(TalkKey);
        if (!talked) PlayLines(FirstLines);
        else OpenChoice();
    }

    private void OpenChoice()
    {
        _mode = Mode.Choice;
        _npcBubble?.HideBubble();
        _playerBubble?.HideBubble();

        if (_menu == null || TopicTitles == null || TopicTitles.Length == 0)
        {
            // 没配菜单就退回播第一段，至少不至于按 E 没反应
            GD.PushWarning("[OldApple] 没找到 ChoiceMenu 或 TopicTitles 为空");
            PlayLines(Topic1Lines);
            return;
        }

        _menu.Open(TopicTitles);
    }

    private void OnPicked(int index)
    {
        _menu?.Close();
        string[] lines = index >= 0 && index < _topics.Length ? _topics[index] : null;
        PlayLines(lines);
    }

    private void PlayLines(string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            EndTalk();
            return;
        }
        _mode = Mode.Lines;
        _lines = lines;
        _index = 0;
        ShowLine(0);
    }

    private void Advance()
    {
        // 打字机还没读完 → 先补全整句，不要跳
        if (_npcBubble != null && _npcBubble.IsTyping) { _npcBubble.FinishTyping(); return; }
        if (_playerBubble != null && _playerBubble.IsTyping) { _playerBubble.FinishTyping(); return; }

        _index++;
        if (_lines == null || _index >= _lines.Length)
        {
            EndTalk();
            return;
        }
        ShowLine(_index);
    }

    private void OnPlayerDied()
    {
        if (_talking) EndTalk();
        else if (GameState.I != null) GameState.I.DialogueActive = false;
    }

    private void EndTalk()
    {
        _talking = false;
        _mode = Mode.None;
        _lines = null;
        _index = 0;

        _menu?.Close();
        _npcBubble?.HideBubble();
        _playerBubble?.HideBubble();

        if (GameState.I != null) GameState.I.DialogueActive = false;
        GameState.I?.MarkTalked(TalkKey);      // 下次再来就是「四选一」了
    }

    private void ShowLine(int i)
    {
        string raw = _lines[i];
        bool isPlayer = IsPlayerLine(raw, out string text);

        if (isPlayer)
        {
            _npcBubble?.HideBubble();
            if (_playerBubble != null) _playerBubble.ShowLine(text);
            else _npcBubble?.ShowLine(text);
            _player?.SpeakReaction(PlayerTalkJump);
        }
        else
        {
            _playerBubble?.HideBubble();
            _npcBubble?.ShowLine(text);
        }
    }

    /// <summary>解析台词前缀。返回 true 表示这句是主角说的。</summary>
    private static bool IsPlayerLine(string raw, out string text)
    {
        text = raw ?? "";
        if (string.IsNullOrEmpty(raw)) return false;
        string t = raw.TrimStart();

        if (t.StartsWith("me:") || t.StartsWith("me："))
        {
            text = t.Substring(3).TrimStart();
            return true;
        }
        if (t.StartsWith("npc:") || t.StartsWith("npc："))
        {
            text = t.Substring(4).TrimStart();
            return false;
        }
        return false;
    }
}
