using Godot;

/// <summary>
/// Boss 战区域。
///
/// 流程：
///   玩家进入 → 左右两侧建墙封锁 + 相机拉到区域中心并放大视野 + 唤醒 Boss
///   Boss 死亡 → 播胜利对话（逐句）
///   对话结束 → Boss 消失、墙拆除、相机恢复跟随
///
/// 用法：把 BossArena.tscn（或手动搭）放进场景，Boss 作为它的子节点，
/// 触发区比战斗区小一圈（玩家走到中间才触发）。
/// </summary>
[Tool]
public partial class BossArena : Area2D
{
    [ExportGroup("区域")]
    [Export] public Vector2 ArenaSize = new Vector2(1143f, 643f); // 封锁范围（宽 × 高），匹配 FightZoom 0.7
    [Export] public float WallThickness = 40f;                    // 两侧墙的厚度

    [ExportGroup("掉落后传送")]
    [Export] public FallTrigger FallTrigger;   // 拖入 firstry 里的 FallTrigger（留空自动找兄弟节点）

    [ExportGroup("空气墙")]
    [Export] public float WallVisualWidth = 150f;    // 空气墙视觉宽度
    [Export] public float WallShowDistance = 140f;   // 玩家离墙多近开始显形
    [Export] public float WallMaxAlpha = 0.5f;       // 最大不透明度（0.5 = 50% 透明）

    [ExportGroup("相机")]
    [Export] public float FightZoom = 0.7f;                       // 越小看到越多

    [ExportGroup("UI")]
    [Export] public string BossName = "大苹果";
    [Export] public float BarWidth = 560f;      // 血条宽度（逻辑分辨率 800 宽时居中）
    [Export] public float BarHeight = 22f;
    [Export] public float BarTop = 74f;         // 距屏幕顶部多远（要给名字留空间）

    [ExportGroup("对话")]
    // 每句写成「说话者：台词」。
    // 前缀等于 BossName（或者 Boss/boss）→ Boss 说；其他 → 玩家说。
    // 不写前缀时默认 Boss 说。
    [Export] public string[] LinesOnWin =
    {
        "大苹果：……算你厉害。",
        "我：你也不看看自己多弱。",
        "大苹果：哼……我还会回来的。",
    };
    // 对话改为按 E 推进后，这个参数不再使用（保留以免场景引用报错）

    private bool _active;
    private bool _cleared;      // 已击败 Boss：不再重新触发战斗
    private bool _talking;
    private BossEnemy _boss;
    private Box _player;
    private Camera2D _cam;
    private StaticBody2D _wallL, _wallR;
    private CollapseFloor _floor;
    private Sprite2D _wallLVis, _wallRVis;
    private int _lineIdx;

    private float _shakeTime;
    private float _shakeStrength;

    private CanvasLayer _ui;
    private ColorRect _barBg;
    private ColorRect _barFill;
    private TextureRect _barFrame;
    private TextureRect _barIcon;
    private Label _nameLabel;
    private float _barInnerW;   // 框内可填充宽度

    public override void _Ready()
    {
        // 可坍塌地板：宽度跟随 ArenaSize.X，和两侧 road_break 衔接
        _floor = GetNodeOrNull<CollapseFloor>("Floor");
        _floor?.SetWidth(ArenaSize.X);

        if (Engine.IsEditorHint()) return;   // 编辑器里不初始化战斗

        BodyEntered += OnBodyEntered;

        _boss = FindBoss();
        if (_boss == null) GD.PushWarning("[BossArena] 没找到 BossEnemy 子节点");
        else _boss.Died += OnBossDied;
    }

    public override void _ExitTree()
    {
        if (_boss != null && IsInstanceValid(_boss)) _boss.Died -= OnBossDied;
    }

    private BossEnemy FindBoss()
    {
        foreach (var c in GetChildren())
            if (c is BossEnemy b) return b;
        return null;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_cleared) return;      // 打过了，别再把玩家关起来
        if (_active) return;
        if (body is not Box p) return;

        _active = true;
        _player = p;
        // 开战：换战斗音乐（打完 / 挑战失败都会切回场景原曲）
        AudioManager.Instance?.PlayBgm("boss_fight", 0.8f);
        _cam = p.GetNodeOrNull<Camera2D>("Camera2D");

        if (_cam != null)
        {
            // 战斗中必须关掉平滑：_Process 每帧设的是"目标位置"，
            // 平滑会让画面滞后追赶，看起来就是"镜头偏了"。
            _cam.PositionSmoothingEnabled = false;
            _cam.Zoom = Vector2.One * FightZoom;
            _cam.Offset = new Vector2(0f, _cam.Offset.Y);   // 清掉可能残留的横向偏移（震屏留下的）
            _cam.GlobalPosition = GlobalPosition;
            _cam.ResetSmoothing();
        }

        // 不能在 BodyEntered（物理回调）里直接 AddChild 带碰撞体的节点，
        // Godot 会报 body_set_shape_disabled 错误。延迟到帧末再建。
        CallDeferred(nameof(BuildWallsAndWake));
    }

    private void BuildWallsAndWake()
    {
        if (!_active) return;   // 期间玩家可能已经死了
        BuildWalls();
        BuildUi();
        // 注意：这里不唤醒 Boss。Boss 保持待机，等玩家第一次打中它才苏醒。
    }

    // ---------------------------------------------------------------- 顶部 Boss 血条
    private void BuildUi()
    {
        _ui = new CanvasLayer { Layer = 10 };
        AddChild(_ui);

        // 逻辑分辨率 800×450，血条居中
        float x = (800f - BarWidth) * 0.5f;
        float frameH = BarWidth * 0.25f * 0.3f;   // 金框图 4:1，再压到 30% 高度（细长条）
        float frameY = BarTop - 14f;
        float pad = frameH * 0.16f;           // 边框厚度（填充往里缩这么多）

        _barInnerW = BarWidth - pad * 2f;

        // ① 红色填充（先加，在底层）
        _barFill = new ColorRect
        {
            Color = new Color(0.82f, 0.17f, 0.20f),
            Position = new Vector2(x + pad, frameY + pad),
            Size = new Vector2(_barInnerW, frameH - pad * 2f),
        };
        _ui.AddChild(_barFill);

        // ② 金色边框（后加，盖在填充上；中间透明所以能看见填充）
        _barFrame = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/boss/boss_bar_frame.png"),
            Position = new Vector2(x, frameY),
            Size = new Vector2(BarWidth, frameH),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        _ui.AddChild(_barFrame);
        _barFrame.Size = new Vector2(BarWidth, frameH);   // 加完再设，否则被贴图原始尺寸覆盖

        // ③ 苹果图标（血条最左边）
        _barIcon = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/boss/boss_icon_apple.png"),
            Position = new Vector2(x - 50f, frameY + frameH * 0.5f - 23f),
            Size = new Vector2(46f, 46f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        _ui.AddChild(_barIcon);
        _barIcon.Size = new Vector2(46f, 46f);

        // ④ 名字
        _nameLabel = new Label
        {
            Text = BossName,
            Position = new Vector2(x, frameY - 28f),
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 22);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.72f));
        _nameLabel.AddThemeColorOverride("font_outline_color", new Color(0.06f, 0.04f, 0.06f));
        _nameLabel.AddThemeConstantOverride("outline_size", 7);
        _ui.AddChild(_nameLabel);
    }

    private void UpdateBar()
    {
        if (_barFill == null || !IsInstanceValid(_barFill)) return;
        if (_boss == null || !IsInstanceValid(_boss)) return;

        float ratio = Mathf.Clamp(_boss.Hp / (float)Mathf.Max(1, _boss.MaxHp), 0f, 1f);
        _barFill.Size = new Vector2(_barInnerW * ratio, _barFill.Size.Y);
    }

    private void RemoveUi()
    {
        if (_ui != null && IsInstanceValid(_ui)) _ui.QueueFree();
        _ui = null;
        _barBg = null;
        _barFill = null;
        _barFrame = null;
        _barIcon = null;
        _nameLabel = null;
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            UpdateBorderMarkers();
            return;
        }

        // 震屏不受 _active 限制：死亡演出那一下重砸发生在 _active=false 之后
        UpdateShake((float)delta);

        if (!_active) return;

        // 玩家被打死 → 立刻重置整场战斗。
        // 否则墙一直关着、Boss 战还在进行，玩家复活在墙外进不来，永远卡关。
        if (_player != null && IsInstanceValid(_player) && _player.Hp <= 0)
        {
            ResetFight();
            return;
        }

        // 相机锁在区域中心：玩家在战场里跑动时镜头不动，能看到全局
        if (_cam != null && IsInstanceValid(_cam))
            _cam.GlobalPosition = GlobalPosition;

        UpdateBar();
        UpdateWallAlpha((float)delta);



        // 对话由玩家按 E 推进（不再按时间自动播）
        if (_talking && Input.IsActionJustPressed("interact"))
        {
            NextLine();
        }
    }

    private void BuildWalls()
    {
        _wallL = MakeWall(-ArenaSize.X * 0.5f);
        _wallR = MakeWall(ArenaSize.X * 0.5f);
        _wallLVis = _wallL.GetNodeOrNull<Sprite2D>("Visual");
        _wallRVis = _wallR.GetNodeOrNull<Sprite2D>("Visual");
    }

    private StaticBody2D MakeWall(float offsetX)
    {
        var wall = new StaticBody2D
        {
            CollisionLayer = 1,      // 和地形同层，玩家/敌人都会被挡住
            CollisionMask = 0,
            Position = new Vector2(offsetX, 0f),
        };
        wall.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D
            {
                Size = new Vector2(WallThickness, ArenaSize.Y)
            }
        });

        // 空气墙视觉：初始完全透明，玩家靠近才渐显
        var vis = new Sprite2D
        {
            Name = "Visual",
            Texture = GD.Load<Texture2D>("res://assets/ui/boss/air_wall.png"),
            Modulate = new Color(1f, 1f, 1f, 0f),
            Scale = new Vector2(WallVisualWidth / 512f, ArenaSize.Y / 2048f * 1.15f),
        };
        wall.AddChild(vis);

        AddChild(wall);
        return wall;
    }

    /// <summary>
    /// 玩家死亡时调用：拆墙、Boss 回满血回原位、相机恢复，让玩家能重新挑战。
    /// </summary>
    private void ResetFight()
    {
        _active = false;
        _talking = false;
        // 挑战失败：音乐切回场景原曲
        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");

        _wallL?.QueueFree();
        _wallR?.QueueFree();
        _wallL = null;
        _wallR = null;
        _wallLVis = null;
        _wallRVis = null;

        if (_boss != null && IsInstanceValid(_boss))
        {
            _boss.Respawn();       // 回满血 + 回出生点 + 清死亡状态
            _boss.Dormant = true;  // 重新待机，等玩家再进来
        }

        RemoveUi();
        RestoreCamera();
    }

    /// <summary>
    /// 把两条边界线摆到 ArenaSize 的左右边缘。
    /// 在编辑器里拖动 ArenaSize 就能实时看到范围，运行时这两条线也会显示。
    /// </summary>
    private void UpdateBorderMarkers()
    {
        var left = GetNodeOrNull<Line2D>("LeftBorder");
        var right = GetNodeOrNull<Line2D>("RightBorder");
        if (left == null || right == null) return;

        float halfW = ArenaSize.X * 0.5f;
        float halfH = ArenaSize.Y * 0.5f;
        var pts = new Vector2[] { new(0f, -halfH), new(0f, halfH) };

        left.Position = new Vector2(-halfW, 0f);
        right.Position = new Vector2(halfW, 0f);
        left.Points = pts;
        right.Points = pts;
    }

    /// <summary>玩家靠近哪面墙，哪面墙就渐显（最多 50% 不透明度）。</summary>
    private void UpdateWallAlpha(float dt)
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        float halfW = ArenaSize.X * 0.5f;
        float px = _player.GlobalPosition.X;
        float dist = WallShowDistance <= 0.01f ? 1f : WallShowDistance;

        float aL = Mathf.Clamp(1f - Mathf.Abs(px - (GlobalPosition.X - halfW)) / dist, 0f, 1f) * WallMaxAlpha;
        float aR = Mathf.Clamp(1f - Mathf.Abs(px - (GlobalPosition.X + halfW)) / dist, 0f, 1f) * WallMaxAlpha;

        FadeTo(_wallLVis, aL, dt);
        FadeTo(_wallRVis, aR, dt);
    }

    private static void FadeTo(Sprite2D vis, float targetAlpha, float dt)
    {
        if (vis == null || !IsInstanceValid(vis)) return;
        var c = vis.Modulate;
        c.A = Mathf.Lerp(c.A, targetAlpha, Mathf.Min(1f, dt * 8f));
        vis.Modulate = c;
    }


    /// <summary>每帧推进震屏。独立于 _active，死亡演出的重砸也能震。</summary>
    private void UpdateShake(float dt)
    {
        if (_cam == null || !IsInstanceValid(_cam)) return;

        if (_shakeTime > 0f)
        {
            _shakeTime -= dt;

            // 必须「赋值」而不是在上一帧基础上累加：
            // 累加会留下残差，而 Box 每帧只重设 Offset.Y，Offset.X 没人管 → 永久偏移。
            float sx = (GD.Randf() - 0.5f) * _shakeStrength * 2f;
            _cam.Offset = new Vector2(sx, _cam.Offset.Y);

            if (_shakeTime <= 0f)
            {
                _shakeStrength = 0f;
                _cam.Offset = new Vector2(0f, _cam.Offset.Y);
            }
        }
        else if (_cam.Offset.X != 0f)
        {
            _cam.Offset = new Vector2(0f, _cam.Offset.Y);
        }
    }

    /// <summary>震屏：叠加在相机的 Offset 上（Box 每帧会重设 Offset，所以不会累积）。</summary>
    public void Shake(float strength, float duration)
    {
        _shakeStrength = Mathf.Max(_shakeStrength, strength);
        _shakeTime = Mathf.Max(_shakeTime, duration);
    }

    private void RestoreCamera()
    {
        if (_cam == null || !IsInstanceValid(_cam)) return;

        _cam.PositionSmoothingEnabled = true;   // 恢复跟随平滑
        _cam.Zoom = Vector2.One;
        _cam.Position = Vector2.Zero;
        _cam.Offset = new Vector2(0f, _cam.Offset.Y);   // 震屏可能改过 X，清掉
        _cam.ResetSmoothing();
    }

    private void OnBossDied()
    {
        if (!_active) return;

        // 死亡瞬间换成哭泣贴图（大小/脚底位置已对齐，不会有瞬移感）
        if (_boss != null && IsInstanceValid(_boss))
            _boss.ShowCryTexture();

        AudioManager.Instance?.PlaySfx("boss_cry", 1f);

        _talking = true;
        _lineIdx = 0;
        NextLine();        // 立刻显示第一句，之后每按一次 E 推进一句
    }

    private void NextLine()
    {
        if (_lineIdx >= LinesOnWin.Length)
        {
            EndFight();
            return;
        }

        string raw = LinesOnWin[_lineIdx];
        _lineIdx++;

        // 解析「说话者：台词」
        bool bossSpeaks = true;          // 不写前缀时默认 Boss
        string text = raw;

        int colon = raw.IndexOf('：');
        if (colon < 0) colon = raw.IndexOf(':');
        if (colon > 0)
        {
            string speaker = raw[..colon].Trim();
            text = raw[(colon + 1)..].Trim();
            bossSpeaks = speaker == BossName
                         || speaker.Equals("Boss", System.StringComparison.OrdinalIgnoreCase);
        }

        var playerBubble = _player != null && IsInstanceValid(_player)
            ? _player.GetNodeOrNull<DialogueUI>("DialogueUI")
            : null;

        if (bossSpeaks)
        {
            // Boss 说：收玩家气泡，开 Boss 气泡
            playerBubble?.HideBubble();
            if (_boss != null && IsInstanceValid(_boss)) _boss.ShowLine(text);
        }
        else
        {
            // 玩家说：收 Boss 气泡，开玩家气泡
            if (_boss != null && IsInstanceValid(_boss)) _boss.HideBubble();
            playerBubble?.ShowLine(text);
        }
    }

    private void EndFight()
    {
        _talking = false;
        _active = false;
        _cleared = true;           // 这场战斗永久结束，不再重开

        if (_boss != null && IsInstanceValid(_boss)) _boss.HideBubble();
        if (_player != null && IsInstanceValid(_player))
            _player.GetNodeOrNull<DialogueUI>("DialogueUI")?.HideBubble();

        // 不立即删：让它跳向战场中央砸一下再自己消失。
        // 墙和相机要等 Boss 真正消失后才拆/恢复，
        // 否则重砸还没落地，玩家就已经能走出去了。
        if (_boss != null && IsInstanceValid(_boss))
        {
            _boss.TreeExited += OnBossGone;
            _boss.StartDeathSequence(GlobalPosition);
        }
        else
        {
            OnBossGone();
        }
    }

    /// <summary>Boss 真正从场景里消失后：拆墙、收 UI、恢复相机。</summary>
    private void OnBossGone()
    {
        // 战斗彻底结束（含哭泣剧情、地板坍塌）：音乐切回场景原曲
        AudioManager.Instance?.PlayBgmForScene(GetTree().CurrentScene?.SceneFilePath ?? "");
        _wallL?.QueueFree();
        _wallR?.QueueFree();
        _wallL = null;
        _wallR = null;
        _wallLVis = null;
        _wallRVis = null;

        RemoveUi();
        RestoreCamera();

        // 最后一次重踏结束（Boss 已消失）→ 地板坍塌，让人掉下去
        _floor?.Collapse();

        // 交给 FallTrigger：解除相机 Y 锁 + 掉到指定深度后传送
        var ft = FallTrigger;
        if (ft == null) ft = GetNodeOrNull<FallTrigger>("../FallTrigger");
        if (ft == null) ft = GetTree().Root.FindChild("FallTrigger", true, false) as FallTrigger;
        GD.Print($"[Arena] OnBossGone: FallTrigger 找到 = {ft != null}");
        ft?.Arm();
    }
}
