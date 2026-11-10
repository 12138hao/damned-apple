using Godot;
using System.Threading.Tasks;

/// <summary>
/// 苹果仙子的三个技能（partial 拆出来，主文件只管状态和对话）。
///
///   技能 1 俯冲撒尘：飞到一侧上方 → 横穿俯冲 → 沿途按 <see cref="DustGap"/> 撒精灵粉尘。
///   技能 2 藤鞭：    施法前摇 → 在玩家背后连续召唤 <see cref="StemCount"/> 根藤鞭，
///                    每根甩一下（逐段延迟旋转 = 抽打感），抽完枯萎。
///   技能 3 水柱：    召唤魔法阵蓄力 → 发射一条持续 <see cref="BeamDuration"/> 秒的水柱，
///                    水柱缓慢扫射，站在里面会持续掉血。
///
/// 所有数值都在 AppleFairy 的检查器里（③④⑤ 三组）。
/// </summary>
public partial class AppleFairy
{
    private Vector2 ArenaCenter => ArenaRect.Position + ArenaRect.Size * 0.5f;

    // ─────────────────────────────────────── 技能 1：俯冲 + 精灵粉尘

    private async Task SkillDive()
    {
        if (!PlayerAlive()) return;

        SetAnim(_texIdle, 8, 1, 8, 0.10f, 0.2083f, 0.5f, true);

        // 从玩家的另一侧起飞，先拉开距离
        float side = _player.GlobalPosition.X > ArenaCenter.X ? -1f : 1f;
        float startX = Mathf.Clamp(_player.GlobalPosition.X + side * 480f,
            ArenaRect.Position.X + 90f, ArenaRect.Position.X + ArenaRect.Size.X - 90f);
        Vector2 start = new Vector2(startX, ArenaRect.Position.Y + 30f);

        await FlyTo(start, FloatSpeed * DiveApproachMul);
        if (!PlayerAlive()) return;
        FaceTo(_player.GlobalPosition.X);

        await Wait(DiveWindup);
        if (!PlayerAlive()) return;

        // 横穿俯冲
        float dirX = _player.GlobalPosition.X > GlobalPosition.X ? 1f : -1f;
        SetAnim(_texDive, 1, 4, 4, 0.07f, DiveArtScale, -2.5f);
        _anim.FlipH = dirX < 0f;

        float endX = dirX > 0f
            ? ArenaRect.Position.X + ArenaRect.Size.X - 90f
            : ArenaRect.Position.X + 90f;

        float travelled = 0f;
        float nextDrop = 0f;
        _diving = true;   // 俯冲期间放开"离玩家最远"的限制，允许横穿整个战场

        while (IsInstanceValid(this) && _battleOn)
        {
            if (dirX > 0f ? GlobalPosition.X >= endX : GlobalPosition.X <= endX) break;

            float step = DiveSpeed * (float)GetPhysicsProcessDeltaTime();
            GlobalPosition = new Vector2(GlobalPosition.X + dirX * step, GlobalPosition.Y);
            travelled += step;

            if (travelled >= nextDrop)
            {
                var dust = FairyDust.Spawn(GetParent(),
                    GlobalPosition + new Vector2(0f, DustSpawnOffsetY),
                    new Vector2(0f, DustFallSpeed));
                dust.Damage = DustDamage;
                nextDrop += DustGap;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        _diving = false;
        if (!_battleOn) return;
        SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);
        await FlyTo(HomePos, FloatSpeed);
    }

    // ─────────────────────────────────────── 技能 2：藤鞭（连续召唤）

    private async Task SkillStems()
    {
        if (!PlayerAlive()) return;

        FaceTo(_player.GlobalPosition.X);
        SetAnim(_texCast, 8, 4, 32, 0.05f, 0.4714f, -61f);
        await Wait(StemCastWindup);
        if (!PlayerAlive()) return;

        for (int i = 0; i < StemCount; i++)
        {
            if (!PlayerAlive() || !IsInstanceValid(this) || !_battleOn) break;

            float face = PlayerFace();
            float baseX = _player.GlobalPosition.X - face * StemBehindDist;
            var basePos = new Vector2(baseX, 361f);   // 草原地面

            var stem = FairyStem.Spawn(GetParent(), _texVine, basePos, StemHeight,
                StemTouchDamage, StemThrustDamage);
            stem.Whip((int)face);

            await Wait(StemSpawnGap);
        }

        await Wait(0.9f);   // 等最后一根收完
        if (_battleOn) SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);
    }

    private float PlayerFace()
    {
        if (_player == null || !IsInstanceValid(_player)) return 1f;
        var spr = _player.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (spr != null) return spr.FlipH ? -1f : 1f;
        return _player.GlobalPosition.X < GlobalPosition.X ? 1f : -1f;
    }

    // ─────────────────────────────────────── 技能 3：水流冲击波

    private const float AimLineWidth = 5f;   // 导向虚线的粗细（世界像素）

    /// <summary>
    /// 三技能：法阵蓄力 2 秒（持续瞄准）→ 锁定 1 秒（给玩家躲的窗口）→ 轰出一条
    /// 持续 <see cref="BeamDuration"/> 秒的完整光波。
    ///
    /// 蓄力期间会画一条**导向光标**（虚线 + 落点圆环），玩家能提前看出她要往哪儿打。
    /// 锁定阶段光标变亮并停止跟随，方向就定死了。
    /// </summary>
    private async Task SkillWater()
    {
        if (!PlayerAlive()) return;

        FaceTo(_player.GlobalPosition.X);
        SetAnim(_texCast, 8, 4, 32, 0.05f, 0.4714f, -61f);
        _casting = true;                 // 施法期间仙子原地不动
        await Wait(0.45f);
        if (!PlayerAlive() || !_battleOn) { _casting = false; return; }

        float face = _anim != null && _anim.FlipH ? -1f : 1f;
        Vector2 muzzle = GlobalPosition + new Vector2(face * 78f, 4f);

        // ── 魔法阵：固定在仙子身前
        var circle = new Sprite2D
        {
            Texture = _texCircle,
            Scale = Vector2.One * (110f / _texCircle.GetHeight()),
            ZIndex = 4,
            Modulate = new Color(1f, 1f, 1f, 0f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        GetParent().AddChild(circle);
        circle.GlobalPosition = muzzle;

        // ── 导向光标：一条虚线 + 落点圆环，蓄力期间告诉玩家"她要往这儿打"
        var aimLine = new Sprite2D
        {
            Name = "AimLine",
            Texture = MakeDashTexture(),
            Centered = false,
            ZIndex = 14,
            Modulate = new Color(0.55f, 0.90f, 1f, 0.30f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
        };
        GetParent().AddChild(aimLine);
        aimLine.GlobalPosition = muzzle;

        var reticle = new Sprite2D
        {
            Name = "AimReticle",
            Texture = MakeReticleTexture(),
            Scale = Vector2.One * 0.95f,
            ZIndex = 15,
            Modulate = new Color(0.65f, 0.95f, 1f, 0.0f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        GetParent().AddChild(reticle);
        reticle.GlobalPosition = muzzle;

        float baseSize = 150f / _texCircle.GetHeight();
        float aim = PlayerAlive() ? (PlayerAimPoint() - muzzle).Angle() : 0f;
        float aimDist = 420f;

        // ── 一阶段：法阵一点点浮现，同时持续瞄准玩家
        float t = 0f;
        while (t < WaterChargeTime && IsInstanceValid(this) && IsInstanceValid(circle) && _battleOn)
        {
            float dt = (float)GetPhysicsProcessDeltaTime();
            t += dt;
            float k = Mathf.Clamp(t / Mathf.Max(0.01f, WaterChargeTime), 0f, 1f);

            muzzle = GlobalPosition + new Vector2(face * 78f, 4f);
            circle.GlobalPosition = muzzle;
            circle.Rotation += 1.6f * dt;
            circle.Modulate = new Color(1f, 1f, 1f, k * 0.9f);
            circle.Scale = Vector2.One * baseSize * (0.45f + 0.55f * k) * (1f + 0.10f * Mathf.Sin(t * 12f));

            if (PlayerAlive())
            {
                Vector2 to = PlayerAimPoint() - muzzle;
                float want = to.Angle();
                aim = Mathf.LerpAngle(aim, want, 1f - Mathf.Exp(-6f * dt));   // 平滑跟瞄
                aimDist = Mathf.Clamp(to.Length(), 90f, BeamLength);
            }

            UpdateAimGuides(aimLine, reticle, muzzle, aim, aimDist,
                new Color(0.55f, 0.90f, 1f, 0.30f + 0.35f * k), 0.25f + 0.35f * k, t);

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        if (!IsInstanceValid(circle)) return;

        // ── 二阶段：方向锁定，光标变亮并停住，法阵发亮跳动 —— 给玩家 1 秒反应
        float t2 = 0f;
        while (t2 < WaterAimHold && IsInstanceValid(this) && IsInstanceValid(circle) && _battleOn)
        {
            float dt = (float)GetPhysicsProcessDeltaTime();
            t2 += dt;
            float k = Mathf.Clamp(t2 / Mathf.Max(0.01f, WaterAimHold), 0f, 1f);

            muzzle = GlobalPosition + new Vector2(face * 78f, 4f);
            circle.GlobalPosition = muzzle;
            circle.Rotation += 3.4f * dt;
            circle.Modulate = new Color(1f, 1f, 1f, 0.9f);
            circle.SelfModulate = new Color(1f + 0.35f * k, 1f + 0.35f * k, 1f + 0.35f * k);
            circle.Scale = Vector2.One * baseSize * (1f + 0.12f * Mathf.Sin(t2 * 30f));

            // 锁定后不再跟随玩家：方向、距离都用定下来的值
            UpdateAimGuides(aimLine, reticle, muzzle, aim, aimDist,
                new Color(0.90f, 1f, 1f, 0.70f + 0.28f * k), 0.70f + 0.30f * k, t2 * 2.2f);

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        if (!IsInstanceValid(circle)) return;

        // ── 发射：整条光波**立即**出现，不做长度渐变
        if (PlayerAlive() && _battleOn)
        {
            FairyBeam.Spawn(GetParent(), muzzle, aim,
                BeamDuration, BeamLength, BeamThickness, BeamDamage, BeamSweep);
        }

        // 瞄准辅助立刻收掉
        if (IsInstanceValid(aimLine)) aimLine.QueueFree();
        if (IsInstanceValid(reticle)) reticle.QueueFree();

        // 法阵淡出
        var fade = CreateTween();
        fade.TweenProperty(circle, "modulate:a", 0f, 0.25f);
        await ToSignal(fade, Tween.SignalName.Finished);
        if (IsInstanceValid(circle)) circle.QueueFree();

        await Wait(Mathf.Max(0f, BeamDuration - 0.25f));
        _casting = false;
        if (_battleOn) SetAnim(_texIdle, 8, 1, 8, 0.14f, 0.2083f, 0.5f, true);
    }

    /// <summary>每帧刷新导向光标：虚线从枪口拉到落点，圆环放在落点并随蓄力放大。</summary>
    private static void UpdateAimGuides(Sprite2D line, Sprite2D reticle, Vector2 from, float aim,
        float dist, Color color, float reticleAlpha, float pulseT)
    {
        if (line == null || !IsInstanceValid(line) || line.Texture == null) return;

        Vector2 dir = new Vector2(Mathf.Cos(aim), Mathf.Sin(aim));
        Vector2 to = from + dir * dist;

        // 贴图是横着的虚线：钉在枪口、按方向旋转、X 方向拉到落点距离
        float tw = line.Texture.GetWidth();
        float th = line.Texture.GetHeight();
        line.GlobalPosition = from;
        line.Rotation = aim;
        line.Scale = new Vector2(dist / Mathf.Max(1f, tw), AimLineWidth / Mathf.Max(1f, th));
        line.Modulate = color;

        if (reticle != null && IsInstanceValid(reticle))
        {
            reticle.GlobalPosition = to;
            reticle.Rotation = pulseT * 0.9f;
            reticle.Modulate = new Color(0.7f, 0.97f, 1f, reticleAlpha);
            reticle.Scale = Vector2.One * (0.90f + 0.07f * Mathf.Sin(pulseT * 3.4f));
        }
    }

    /// <summary>虚线贴图：64x4，12 亮 8 暗循环，沿 X 平铺。</summary>
    private static Texture2D MakeDashTexture()
    {
        const int w = 64, h = 4;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int x = 0; x < w; x++)
        {
            bool on = (x % 20) < 12;
            for (int y = 0; y < h; y++)
                img.SetPixel(x, y, new Color(1f, 1f, 1f, on ? 1f : 0f));
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>落点圆环：外圈 + 四个方向刻线 + 中心点。</summary>
    private static Texture2D MakeReticleTexture()
    {
        const int n = 64;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;

                if (r > 22f && r < 25f) a = 1f;                       // 外环
                else if ((Mathf.Abs(dx) < 1.6f || Mathf.Abs(dy) < 1.6f) && r > 26f && r < 31f) a = 0.95f; // 四向刻线
                else if (r < 2.4f) a = 1f;                            // 中心点

                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>攻击瞄准点：玩家原点在脚下，往上抬一点才是躯干，否则光柱会从身体下面擦过去。</summary>
    private Vector2 PlayerAimPoint()
    {
        if (_player == null || !IsInstanceValid(_player)) return GlobalPosition;
        return _player.GlobalPosition + new Vector2(0f, -40f);
    }

    private bool PlayerAlive()
    {
        return _player != null && IsInstanceValid(_player) && _player.IsAlive;
    }
}
