using Godot;

/// <summary>
/// 可对话的 NPC。玩家靠近后按 E 开始对话。
///
/// 台词用前缀区分说话者，写在同一个数组里：
///   "npc:你好呀"        → 这个 NPC 说（气泡出现在自己头上）
///   "me:我是 Orange"    → 主角说（气泡出现在主角头上，并做一个轻跳动作）
///   不带前缀             → 默认当成 NPC 说
///
/// 首次对话和再次对话可以用两套完全不同的台词。
/// </summary>
public partial class Npc : CharacterBody2D
{
    [ExportGroup("首次对话（可以双向）")]
    [Export] public string[] FirstLines =
    {
        "npc:你好呀，你是新来的吧？",
        "me:嗯，我叫 Orange。",
        "npc:我是奶蛙，这里很危险的。",
        "me:我会小心的！",
    };

    [ExportGroup("再次对话（默认只有自己说）")]
    [Export] public string[] RepeatLines =
    {
        "npc:又见面了。",
        "npc:前面的坑小心点。",
    };

    [Export] public float TalkDistance = 130f;   // 玩家离多近可以对话（像素）

    // 每句台词对应的表情：-1 = 正常，0 = 微笑，1 = 生气，2 = 无奈，3 = 坚定。
    // 和 FirstLines 一一对应（长度不够时多出来的句子用 -1）
    [Export] public int[] LineFaces =
    {
        -1, -1, -1, -1, -1, -1, -1, -1, -1,
    };

    // 和 RepeatLines 一一对应
    [Export] public int[] RepeatFaces =
    {
        -1, 1, 1, -1,
    };

    [ExportGroup("主角回应")]
    [Export] public float PlayerTalkJump = 260f;   // 主角说话时向上轻跳的速度，0 = 不跳

    private DialogueUI _npcBubble;
    private DialogueUI _playerBubble;
    private Box _player;
    private Sprite2D _sprite;
    private Sprite2D _faceA;
    private Sprite2D _faceB;
    private Sprite2D _faceC;
    private Sprite2D _faceD;

    private string[] _lines;
    private int[] _faces;
    private int _index;
    private bool _talking;
    private bool _inRange;
    private bool _lastInRange;

    public override void _Ready()
    {
        // 自己的气泡（气泡跟着自己的头）
        _npcBubble = GetNodeOrNull<DialogueUI>("DialogueUI");
        if (_npcBubble == null)
            _npcBubble = GetTree().Root.FindChild("DialogueUI", true, false) as DialogueUI;
        if (_npcBubble == null)
            GD.PushWarning("[Npc] 没找到 DialogueUI，对话将无法显示");

        _player = GetTree().Root.FindChild("box", true, false) as Box;
        if (_player == null)
        {
            GD.PushWarning("[Npc] 没找到玩家节点 box");
        }
        else
        {
            // 主角的气泡（主角说话时用）
            _playerBubble = _player.GetNodeOrNull<DialogueUI>("DialogueUI");
            if (_playerBubble == null)
                GD.PushWarning("[Npc] 主角身上没有 DialogueUI，主角将无法开口");

            // 玩家死亡（掉坑等）时自动结束对话，否则 DialogueActive 会一直卡在 true
            _player.Died += OnPlayerDied;
        }

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _faceA = GetNodeOrNull<Sprite2D>("FaceA");
        _faceB = GetNodeOrNull<Sprite2D>("FaceB");
        _faceC = GetNodeOrNull<Sprite2D>("FaceC");
        _faceD = GetNodeOrNull<Sprite2D>("FaceD");
        if (_faceA != null) _faceA.Visible = false;
        if (_faceB != null) _faceB.Visible = false;
        if (_faceC != null) _faceC.Visible = false;
        if (_faceD != null) _faceD.Visible = false;
    }

    public override void _Process(double delta)
    {
        // 距离判断（比 Area2D 事件可靠）
        if (_player != null && IsInstanceValid(_player))
        {
            _inRange = GlobalPosition.DistanceTo(_player.GlobalPosition) <= TalkDistance;
        }

        if (_inRange != _lastInRange)
        {
            _lastInRange = _inRange;

            // 走远了就中断正在进行的对话
            if (!_inRange && _talking)
            {
                EndTalk();
            }
        }

        if (!Input.IsActionJustPressed("interact")) return;

        if (!_talking)
        {
            if (_inRange) StartTalk();
        }
        else
        {
            Advance();
        }
    }

    // ---------------------------------------------------------------- 对话流程

    private void StartTalk()
    {
        bool talked = GameState.I != null && GameState.I.HasTalked(Name);
        _lines = talked ? RepeatLines : FirstLines;
        _faces = talked ? RepeatFaces : LineFaces;

        if (_lines == null || _lines.Length == 0)
        {
            GD.PushWarning($"[Npc] {Name} 的台词是空的");
            return;
        }

        _talking = true;
        _index = 0;
        if (GameState.I != null) GameState.I.DialogueActive = true;
        ShowLine(0);
    }

    private void Advance()
    {
        // 打字机还没读完 → 先补全整句，不要跳到下一句（否则按快了会漏字）
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
        // 兜底：即使这个 NPC 没在说话，也把全局对话标志清掉
        else if (GameState.I != null) GameState.I.DialogueActive = false;
    }

    public override void _ExitTree()
    {
        if (_player != null && IsInstanceValid(_player))
        {
            _player.Died -= OnPlayerDied;
        }
    }

    private void EndTalk()
    {
        _talking = false;
        _lines = null;
        _index = 0;
        if (GameState.I != null) GameState.I.DialogueActive = false;

        _npcBubble?.HideBubble();
        _playerBubble?.HideBubble();
        RestoreFace();

        // 记下来，下次就是 RepeatLines
        GameState.I?.MarkTalked(Name);
    }

    /// <summary>显示第 i 句：按前缀决定谁说话、用哪个气泡。</summary>
    private void ShowLine(int i)
    {
        string raw = _lines[i];
        bool isPlayer = IsPlayerLine(raw, out string text);

        if (isPlayer)
        {
            // 主角说：自己的气泡关掉，主角气泡打开
            _npcBubble?.HideBubble();
            if (_playerBubble != null)
            {
                _playerBubble.ShowLine(text);
            }
            else
            {
                // 没有主角气泡时，退回用 NPC 气泡显示，至少台词不丢
                _npcBubble?.ShowLine(text);
            }

            RestoreFace();                        // 主角说话时 NPC 保持正常表情
            _player?.SpeakReaction(PlayerTalkJump);
        }
        else
        {
            // NPC 说：主角气泡关掉，自己的打开
            _playerBubble?.HideBubble();
            _npcBubble?.ShowLine(text);
            ApplyFace(i);
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

        return false;   // 没前缀，默认 NPC 说
    }

    // ---------------------------------------------------------------- 表情

    private void ApplyFace(int index)
    {
        int face = (_faces != null && index >= 0 && index < _faces.Length) ? _faces[index] : -1;

        if (_sprite != null) _sprite.Visible = (face < 0);
        if (_faceA != null) _faceA.Visible = (face == 0);   // 微笑
        if (_faceB != null) _faceB.Visible = (face == 1);   // 生气
        if (_faceC != null) _faceC.Visible = (face == 2);   // 无奈
        if (_faceD != null) _faceD.Visible = (face == 3);   // 坚定

        // 换表情时暂停呼吸，否则 SheetAnimator 会把帧覆盖回去
        var anim = _sprite as SheetAnimator;
        if (anim != null) anim.Paused = (face >= 0);
    }

    private void RestoreFace()
    {
        if (_sprite != null) _sprite.Visible = true;
        if (_faceA != null) _faceA.Visible = false;
        if (_faceB != null) _faceB.Visible = false;
        if (_faceC != null) _faceC.Visible = false;
        if (_faceD != null) _faceD.Visible = false;

        var anim = _sprite as SheetAnimator;
        if (anim != null) anim.Paused = false;
    }
}
