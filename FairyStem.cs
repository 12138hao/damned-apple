using Godot;
using System.Threading.Tasks;

/// <summary>
/// 苹果仙子二技能召唤出来的藤鞭。
///
/// 为什么不像之前那样"整根转一下"：
///   整根刚性旋转看起来像"建模倒下来"，完全没有抽打感。
///   这里把贴图切成 <see cref="Segments"/> 段，串成一条关节链，
///   每段转一点、并且**逐段延迟**（越靠鞭梢越晚转），
///   甩出去时鞭梢会滞后 → 形成鞭子的 S 形抽击。
///
/// 一次调用只抽一下：
///   破土而出 → 后仰蓄力 → 甩出去（带延迟）→ 收回 → 枯萎消失。
///   技能要召唤三根，由 AppleFairy.SkillStems 连续生成三根。
///
/// 伤害：鞭身蹭到 <see cref="TouchDamage"/>，甩击命中 <see cref="ThrustDamage"/>。
/// </summary>
public partial class FairyStem : Node2D
{
    public const int Segments = 6;

    // ── 数值（由 AppleFairy 传进来，那里可以调）
    public int TouchDamage = 10;
    public int ThrustDamage = 20;
    public float Height = 150f;
    public float ThicknessMul = 1.2f;   // 横向额外放大（新贴图已经占满画布宽，只需一点点加粗）

    private Node2D[] _joints;
    private Area2D[] _hits;
    private bool _thrusting;
    private float _hitCooldown;

    public static FairyStem Spawn(Node parent, Texture2D art, Vector2 basePos, float height, int touchDmg, int thrustDmg, float thicknessMul = 1.2f)
    {
        var stem = new FairyStem
        {
            Height = height,
            TouchDamage = touchDmg,
            ThrustDamage = thrustDmg,
            ThicknessMul = thicknessMul,
        };
        parent.AddChild(stem);
        stem.GlobalPosition = basePos;
        stem.Build(art);
        return stem;
    }

    private void Build(Texture2D art)
    {
        float scale = Height / art.GetHeight();
        float segH = Height / Segments;
        float bandH = art.GetHeight() / (float)Segments;

        _joints = new Node2D[Segments];

        Node2D parentJoint = null;
        for (int i = 0; i < Segments; i++)
        {
            var joint = new Node2D
            {
                Name = $"J{i}",
                Position = parentJoint == null ? Vector2.Zero : new Vector2(0f, -segH),
            };

            var atlas = new AtlasTexture
            {
                Atlas = art,
                // 贴图是从上往下画的（顶部是鞭梢、底部是根），链条是从地面往上长，
                // 所以这里必须反着取：第 0 段 = 最底下那段
                Region = new Rect2(0f, bandH * (Segments - 1 - i), art.GetWidth(), bandH),
                FilterClip = true,
            };

            var spr = new Sprite2D
            {
                Name = "Art",
                Texture = atlas,
                Centered = true,
                Scale = new Vector2(scale * ThicknessMul, scale),
                // 把这一段的底边钉在关节原点上，往上长
                Offset = new Vector2(0f, -bandH * 0.5f),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            joint.AddChild(spr);

            if (parentJoint == null) AddChild(joint);
            else parentJoint.AddChild(joint);

            _joints[i] = joint;
            parentJoint = joint;
        }

        // 判定：**每一节都挂一个圆形判定**。
        // 之前只在梢部挂一个，鞭子扫过去的弧线如果没正好穿过玩家就完全打不到，
        // 而且根部那块矩形是不跟着弯曲的，鞭子甩出去之后它还在原地竖着。
        _hits = new Area2D[Segments];
        for (int i = 1; i < Segments; i++)
        {
            var hit = new Area2D
            {
                Name = $"Hit{i}",
                CollisionLayer = 0,
                CollisionMask = 2,
                Position = new Vector2(0f, -segH * 0.5f),
            };
            hit.AddChild(new CollisionShape2D
            {
                Shape = new CircleShape2D { Radius = 28f },
            });
            hit.BodyEntered += OnBodyEntered;
            _joints[i].AddChild(hit);
            _hits[i] = hit;
        }
    }

    /// <summary>抽一下：冒出来 → 后仰 → 甩击 → 收回 → 枯萎。整段约 1.2 秒。</summary>
    public async void Whip(int face)
    {
        // 1) 破土而出
        Scale = new Vector2(1f, 0.05f);
        var grow = CreateTween();
        grow.TweenProperty(this, "scale", Vector2.One, 0.22f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        await ToSignal(grow, Tween.SignalName.Finished);
        if (!IsInstanceValid(this)) return;

        // 2) 后仰蓄力：先憋出一个反向的弧，越靠梢越晚动
        var wind = CreateTween();
        wind.SetParallel(true);
        for (int i = 0; i < Segments; i++)
        {
            wind.TweenProperty(_joints[i], "rotation", -face * Mathf.DegToRad(8f), 0.16f)
                .SetDelay(i * 0.02f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        await ToSignal(wind, Tween.SignalName.Finished);
        if (!IsInstanceValid(this)) return;

        // 3) 甩击：逐段延迟 0.028s，鞭梢最后到 —— 这就是"抽打感"的来源
        _thrusting = true;
        var crack = CreateTween();
        crack.SetParallel(true);
        for (int i = 0; i < Segments; i++)
        {
            crack.TweenProperty(_joints[i], "rotation", face * Mathf.DegToRad(20f), 0.085f)
                .SetDelay(i * 0.028f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        }
        await ToSignal(crack, Tween.SignalName.Finished);
        _thrusting = false;
        if (!IsInstanceValid(this)) return;

        await ToSignal(GetTree().CreateTimer(0.10), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;

        // 4) 收回
        var back = CreateTween();
        back.SetParallel(true);
        for (int i = 0; i < Segments; i++)
        {
            back.TweenProperty(_joints[i], "rotation", 0f, 0.26f)
                .SetDelay(i * 0.018f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        await ToSignal(back, Tween.SignalName.Finished);
        if (!IsInstanceValid(this)) return;

        await Wither();
    }

    private async Task Wither()
    {
        if (_hits != null)
            foreach (var h in _hits) if (h != null) h.Monitoring = false;
        Modulate = new Color(0.72f, 0.58f, 0.32f);

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(this, "scale", new Vector2(0.5f, 0.03f), 0.5f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(this, "modulate:a", 0f, 0.5f);
        await ToSignal(tw, Tween.SignalName.Finished);
        if (IsInstanceValid(this)) QueueFree();
    }


    public override void _Process(double delta)
    {
        _hitCooldown = Mathf.Max(0f, _hitCooldown - (float)delta);


        // BodyEntered 只在"刚进入"触发一次；甩击时鞭子是扫过去的，
        // 每帧把所有节的判定再补检一遍，保证扫到就打到。
        if (_hits != null && _hitCooldown <= 0f)
        {
            for (int i = 1; i < _hits.Length; i++)
            {
                if (_hits[i] == null || !_hits[i].Monitoring) continue;
                foreach (var body in _hits[i].GetOverlappingBodies())
                {
                    if (body is Box b && b.IsAlive) { OnBodyEntered(b); return; }
                }
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Box player || !player.IsAlive) return;
        if (_hitCooldown > 0f) return;
        player.TakeDamage(_thrusting ? ThrustDamage : TouchDamage, GlobalPosition);
        _hitCooldown = 0.5f;
    }
}
