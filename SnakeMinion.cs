using Godot;

/// <summary>
/// 蛇小怪：苹果蛇。跳跃式追击玩家，撞到人扣 20 后自己消散；200 血，能被刀砍死。
///
/// 三套动画分工：
///   Hop    — 腾空时循环（8 帧 110x160）
///   Breath — 落地待机（8 帧 110x128）
///   Idle   — 从法阵里冒出来那一下（单帧）
/// </summary>
public partial class SnakeMinion : Enemy
{
    [Export] public int ContactDamage = 20;
    [Export] public float HopInterval = 0.85f;   // 两次跳之间歇多久
    [Export] public float HopUp = 430f;          // 起跳竖直初速
    [Export] public float HopForward = 190f;     // 朝玩家的水平速度
    [Export] public float MinionScale = 0.55f;

    private Sprite2D _art;
    private Texture2D _texHop, _texBreath, _texIdle;
    private int _botHop, _botBreath, _botIdle;   // 各套表里画面内容的底边（纹理像素）
    private float _timer;
    private float _animT;
    private int _frame;
    private bool _grounded = true;
    private string _anim = "idle";
    private Box _player;
    private float _life;
    private bool _dying;

    private CollisionShape2D _bodyShape;
    private System.Collections.Generic.List<Rect2I> _hopRects = new();
    private System.Collections.Generic.List<Rect2I> _breathRects = new();
    private System.Collections.Generic.List<Rect2I> _idleRects = new();

    public override void _Ready()
    {
        base._Ready();
        Damage = ContactDamage;
        ImmuneToKnockback = true;
        _player = GetTree().Root.FindChild("box", true, false) as Box;

        _art = GetNodeOrNull<Sprite2D>("Art");
        _texHop = GD.Load<Texture2D>("res://assets/eden/snake_minion_hop.png");
        _texBreath = GD.Load<Texture2D>("res://assets/eden/snake_minion_breath.png");
        _texIdle = GD.Load<Texture2D>("res://assets/eden/snake_minion_idle.png");

        // 三套表的透明留白不一样，脚底要按"内容的底边"对齐，不能按贴图高度
        _botHop = ContentBottom(_texHop);
        _botBreath = ContentBottom(_texBreath);
        _botIdle = ContentBottom(_texIdle);

        // 每帧的内容矩形：跳跃动画里身体在画格内上下浮动（最多 18 纹理像素 ≈ 10 世界像素），
        // 光靠固定碰撞体就会"贴图浮起来了、判定还趴在地上"。这里把每帧的矩形算出来，
        // 换帧时让碰撞体跟着那一帧的画面对齐。
        _bodyShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (_bodyShape != null)
            _bodyShape.Shape = new RectangleShape2D();   // 每个实例一份，别和别的小怪共用同一个 shape
        _hopRects = FrameRects(_texHop, 8);
        _breathRects = FrameRects(_texBreath, 8);
        _idleRects = FrameRects(_texIdle, 1);

        // 出场：从法阵里淡入、缩放到正常
        Modulate = new Color(1f, 1f, 1f, 0f);
        var final = Vector2.One * MinionScale;
        Scale = final * 0.4f;
        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(this, "modulate:a", 1f, 0.28f);
        tw.TweenProperty(this, "scale", final, 0.32f)
          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        PlayAnim("idle");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dying) return;
        float dt = (float)delta;
        // 父类的 _PhysicsProcess 会被这里整个顶掉，受击闪红要自己推一下，
        // 否则砍上去小怪连变红都不会，玩家会以为它无敌。
        TickHitFeedback(dt);
        _life += dt;
        _timer -= dt;

        // 重力 + 移动（不走父类，父类是巡逻）
        var v = Velocity;
        v.Y += Gravity * dt;
        Velocity = v;
        MoveAndSlide();
        _grounded = IsOnFloor();

        if (_grounded)
        {
            if (v.Y > 0f) Velocity = new Vector2(0f, Velocity.Y);
            if (_anim != "breath") PlayAnim("breath");
            if (_timer <= 0f && _player != null && IsInstanceValid(_player))
            {
                _timer = HopInterval;
                float dir = _player.GlobalPosition.X > GlobalPosition.X ? 1f : -1f;
                Velocity = new Vector2(dir * HopForward, -HopUp);
                PlayAnim("hop");
                if (_art != null) _art.FlipH = dir < 0f;
            }
        }
        else if (_anim != "hop")
        {
            PlayAnim("hop");
        }

        // 撞到玩家：扣 20，然后自己消散
        if (_player != null && IsInstanceValid(_player))
        {
            if (GlobalPosition.DistanceTo(_player.GlobalPosition) < 30f)
            {
                _player.TakeDamage(ContactDamage, GlobalPosition);
                ExplodeAndDie();
            }
        }

        StepAnim(dt);
        if (_life > 26f) QueueFree();     // 防止永久残留
    }

    public override void _Process(double delta) { }

    private void PlayAnim(string name)
    {
        if (_art == null) return;
        _anim = name;
        _frame = 0;
        _animT = 0f;
        int bottom;
        switch (name)
        {
            case "hop":
                _art.Texture = _texHop; _art.Hframes = 8; bottom = _botHop; break;
            case "breath":
                _art.Texture = _texBreath; _art.Hframes = 8; bottom = _botBreath; break;
            default:
                _art.Texture = _texIdle; _art.Hframes = 1; bottom = _botIdle; break;
        }
        // 把"内容底边"压到节点原点，三套动画换着播也不会飘
        if (bottom > 0) _art.Offset = new Vector2(0f, -bottom);
        _art.Frame = 0;
        ApplyFrameShape();
    }

    // ─────────────────────────────── 判定框跟着画面走
    // 跳跃动画里身体在画格内上下浮动（实测最多 18 纹理像素 ≈ 10 世界像素），
    // 而脚底是按"整张表的最底边"对齐的 —— 所以跳起来那几帧贴图会浮在判定框上方。
    // 这里把每一帧的内容矩形先算好，换帧时把碰撞体挪到那一帧的画面上。

    /// <summary>逐帧算内容矩形（纹理像素，相对该帧左上角）。空表返回空列表。</summary>
    private static System.Collections.Generic.List<Rect2I> FrameRects(Texture2D tex, int frames)
    {
        var list = new System.Collections.Generic.List<Rect2I>();
        if (tex == null || frames <= 0) return list;

        var img = tex.GetImage();
        if (img == null) return list;
        if (img.IsCompressed()) img.Decompress();

        int fw = img.GetWidth() / frames;
        int fh = img.GetHeight();
        for (int i = 0; i < frames; i++)
        {
            var sub = img.GetRegion(new Rect2I(i * fw, 0, fw, fh));
            list.Add(sub.GetUsedRect());
        }
        return list;
    }

    /// <summary>把这一帧的画面矩形同步到碰撞体（尺寸 + 位置）。</summary>
    private void ApplyFrameShape()
    {
        if (_bodyShape == null || _art == null) return;

        var rects = _anim == "hop" ? _hopRects : _anim == "breath" ? _breathRects : _idleRects;
        int unionBottom = _anim == "hop" ? _botHop : _anim == "breath" ? _botBreath : _botIdle;
        int frame = _art.Frame;
        if (rects == null || frame < 0 || frame >= rects.Count) return;

        Rect2I r = rects[frame];
        if (r.Size.X <= 0 || r.Size.Y <= 0) return;

        // 注意：这里用的都是**本地单位**（= 纹理像素）。
        // 小怪自己 Scale = 0.55，Sprite2D 和 CollisionShape2D 都是它的子节点，
        // 会被同一个缩放作用一次 —— 所以形状直接填纹理像素尺寸，
        // 千万别再乘 MinionScale，否则判定会变成贴图的一半（以前就是这么错的）。
        float h = r.Size.Y;
        float w = r.Size.X;
        float bottom = r.End.Y - unionBottom;   // 0 = 贴地，负数 = 这一帧画得比整表底边高

        if (_bodyShape.Shape is not RectangleShape2D box)
        {
            box = new RectangleShape2D();
            _bodyShape.Shape = box;
        }
        box.Size = new Vector2(w, h);
        _bodyShape.Position = new Vector2(0f, bottom - h * 0.5f);
    }

    /// <summary>贴图里最底下那一行不透明像素的 y（纹理像素）。用来对齐脚底。</summary>
    private static int ContentBottom(Texture2D tex)
    {
        if (tex == null) return 0;
        var img = tex.GetImage();
        if (img == null) return tex.GetHeight();
        if (img.IsCompressed()) img.Decompress();
        var used = img.GetUsedRect();
        return used.Size.Y <= 0 ? tex.GetHeight() : used.End.Y;
    }

    private void StepAnim(float dt)
    {
        if (_art == null || _art.Hframes <= 1) return;
        _animT += dt;
        const float interval = 0.11f;
        while (_animT >= interval)
        {
            _animT -= interval;
            _art.Frame = (_art.Frame + 1) % _art.Hframes;
            ApplyFrameShape();
        }
    }

    protected override void OnDeath()
    {
        _dying = true;
        if (_art != null)
        {
            var tw = CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(_art, "modulate:a", 0f, 0.18f);
            tw.TweenProperty(this, "scale", new Vector2(1.3f, 0.4f), 0.18f);
        }
        AudioManager.Instance?.PlaySfx(DeathSfx, 0.6f, 1.25f);
        GetTree().CreateTimer(0.2f).Timeout += QueueFree;
    }

    /// <summary>撞到玩家后强制消散（走正常死亡流程，但不扣血提示）。</summary>
    private void ExplodeAndDie()
    {
        if (_dying) return;
        TryTakeHit(99999, GlobalPosition, 0f, 0f, true);
    }
}
