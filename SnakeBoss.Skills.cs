using Godot;
using System.Threading.Tasks;

/// <summary>SnakeBoss 的三个技能 + 狂暴。拆开写，主文件只管状态机。</summary>
public partial class SnakeBoss
{
    // ────────────────────────────────────────── 技能 1：蛇毒

    private async void DoSpit()
    {
        if (State == SState.Dead) return;
        State = SState.Spit;
        int gen = _fightGen;

        // 张嘴：整条蛇往前压一点，给个"吐"的预备感
        var basePos = Position;
        var tw = CreateTween();
        tw.TweenProperty(this, "position", basePos + new Vector2(0f, 6f), 0.12f);
        tw.TweenProperty(this, "position", basePos, 0.18f);

        SetMouthOpen(true);
        // 吐毒音效是一次技能播一声（这声约 3 秒，跟着整段喷吐走）。
        // 以前是每滩播一声，6 滩会在 1 秒内叠 6 层，糊成噪音。
        AudioManager.Instance?.PlaySfx(SfxSpit, 0.9f, 1f);
        await Wait(0.18f);
        if (State == SState.Dead || gen != _fightGen) return;

        _spitIndex = 0;
        for (int i = 0; i < SpitCount; i++)
        {
            if (State == SState.Dead || !IsInsideTree() || gen != _fightGen) return;
            var target = PickPoisonSpot();
            var b = PoisonBlob.Spawn(GetParent(), MouthWorld, target);
            b.HitDamage = SpitDamage;
            b.FlightTime = SpitFlightTime;
            b.PoolDamage = PoolDamage;
            b.PoolRadius = PoolRadius;
            b.PoolDuration = PoolDuration;
            b.PoolTickInterval = PoolTickInterval;
            b.SfxLand = SfxPoolLand;
            await Wait(SpitGap);
        }

        await Wait(0.35f);
        if (gen != _fightGen) return;
        SetMouthOpen(false);
        if (State != SState.Dead) BackToIdle();
    }

    private int _spitIndex;

    private Vector2 PickPoisonSpot()
    {
        float px = _player != null && IsInstanceValid(_player)
            ? _player.GlobalPosition.X
            : GlobalPosition.X;
        // 左右交替、一次比一次远：6 滩铺成一个扇形，比纯随机更均匀也不容易叠在一起
        float side = (_spitIndex % 2 == 0) ? -1f : 1f;
        float dist = SpitSpreadNear + (_spitIndex / 2) * SpitSpreadStep;
        _spitIndex++;
        float x = px + side * dist + (float)GD.RandRange(-SpitJitter, SpitJitter);
        if (_arena != null) x = Mathf.Clamp(x, _arena.ArenaLeft + 50f, _arena.ArenaRight - 50f);
        return new Vector2(x, _arena != null ? _arena.GroundY : 700f);
    }

    // ────────────────────────────────────────── 技能 2：甩尾
    //
    // 一次只扫**一边**：先选玩家所站的那一侧，抬尾蓄力 1.5 秒（期间整侧地面画红色危险带，
    // 蓄力条从树干一路推到墙），然后尾巴从树根处抽出去，一直扫到这一侧的墙。
    // 判定用的是「0 → 当前尾尖」整条带，所以只要站在这一侧、没在蓄力期跑到对面，就会被扫到。

    private float _tailT;
    private float _tailSide = 1f;      // +1 右 / -1 左
    private float _tailReach = 560f;   // 这一侧到墙的距离（正数）
    private int _tailFrame = -1;
    private bool _tailHitDone;
    private bool _applesDone;

    // 帧动画布局（与 tools/_art/snake/build_tail_sheet.py 一一对应）
    //   0~4 蓄力抬起  5~8 横扫出去  9~11 回抽
    private const int TailFrameCount = 12;
    private const float TailCellW = 560f;
    private const float TailCellH = 94f;
    private const float TailBaseY = 86f;       // 未抬升时尾根下沿在格子内的 y
    private static readonly float[] TailFrameReach =
    {
        115f, 150f, 185f, 215f, 240f,
        300f, 390f, 480f, 560f,
        470f, 330f, 170f,
    };

    private void DoTail()
    {
        if (State == SState.Dead) return;
        State = SState.Tail;
        _tailT = 0f;
        _tailAnim = 0f;
        _tailHitDone = false;
        _applesDone = false;
        _tailFrame = -1;

        // 往玩家那一侧扫
        float px = _player != null && IsInstanceValid(_player) ? _player.GlobalPosition.X : GlobalPosition.X + 1f;
        _tailSide = px >= GlobalPosition.X ? 1f : -1f;

        float reach = TailRange;
        if (_arena != null)
        {
            float wall = _tailSide > 0f ? _arena.ArenaRight : _arena.ArenaLeft;
            reach = Mathf.Min(TailRange, Mathf.Abs(wall - GlobalPosition.X));
        }
        _tailReach = Mathf.Max(140f, reach);

        if (_tailArt != null)
        {
            _tailArt.Visible = true;
            ApplyTailFrame(0);
        }
        QueueRedraw();
        AudioManager.Instance?.PlaySfx(SfxTailWind, 0.45f, 0.72f);
    }

    private void TickTail(float dt)
    {
        _tailT += dt;
        float warnEnd = TailWarn;
        float swingEnd = warnEnd + TailSwing;
        float retractEnd = swingEnd + TailRecover;

        if (_tailT <= warnEnd)
        {
            // 蓄力：0→4 帧，尾巴一边探出来一边抬起来，越接近出手抖得越厉害
            float k = Mathf.Clamp(_tailT / Mathf.Max(0.05f, warnEnd), 0f, 1f);
            int f = Mathf.Clamp((int)(k * 5f), 0, 4);
            ApplyTailFrame(f);
            ShakeTailTip(k);
        }
        else if (_tailT <= swingEnd)
        {
            // 甩出：5→8 帧，从抬起翻成平扫
            float k = (_tailT - warnEnd) / Mathf.Max(0.05f, TailSwing);
            int f = 5 + Mathf.Clamp((int)(k * 4f), 0, 3);
            ApplyTailFrame(f);
            CheckTailHit(_tailSide * TailFrameReach[f]);
        }
        else if (_tailT <= retractEnd)
        {
            float k = (_tailT - swingEnd) / Mathf.Max(0.05f, TailRecover);
            int f = 9 + Mathf.Clamp((int)(k * 3f), 0, 2);
            ApplyTailFrame(f);
        }
        else if (!_applesDone)
        {
            // 收尾 → 撞树的余震把苹果晃下来
            _applesDone = true;
            if (_tailArt != null) _tailArt.Visible = false;
            DropApples();
            return;
        }
        QueueRedraw();
    }

    /// <summary>蓄力时让尾尖小幅抖动（整帧左右微移 1~2px，够像素味了）。</summary>
    private void ShakeTailTip(float k)
    {
        if (_tailArt == null) return;
        float amp = 1f + 2f * k;
        float j = Mathf.Sin(_tailT * 52f) * amp;
        _tailArt.Position = new Vector2(BaseTailX + j, TailCellY);
    }

    private float BaseTailX => _tailSide > 0f ? 0f : -TailCellW;
    private float TailCellY => -TailGroundGap - TailBaseY;

    /// <summary>切到第 idx 帧，并按左右侧摆好位置（帧内已含长度与抬起高度，不再缩放）。</summary>
    private void ApplyTailFrame(int idx)
    {
        if (_tailArt == null || _tailArt.Texture == null) return;
        idx = Mathf.Clamp(idx, 0, TailFrameCount - 1);
        if (_tailFrame == idx) return;
        _tailFrame = idx;

        // 尾巴本身近黑，压在深色地面上会糊成一片 —— 提亮一点，让鳞片高光能看见
        _tailArt.Modulate = new Color(1.45f, 1.40f, 1.50f, 1f);
        _tailArt.Frame = idx;
        _tailArt.FlipH = _tailSide < 0f;
        _tailArt.Scale = Vector2.One;
        _tailArt.Position = new Vector2(BaseTailX, TailCellY);
    }

    private void CheckTailHit(float tip)
    {
        if (_tailHitDone || _player == null || !IsInstanceValid(_player)) return;

        // 跳得够高能从尾巴上方越过去
        float py = _player.GlobalPosition.Y;
        if (Mathf.Abs(py - GlobalPosition.Y) > TailHitHeight) return;

        float px = _player.GlobalPosition.X - GlobalPosition.X;
        // lo/hi 已经带着方向了（往右扫 = [0, tip]，往左扫 = [tip, 0]），再各放开 26px 容错
        float lo = Mathf.Min(0f, tip) - TailHitPad;
        float hi = Mathf.Max(0f, tip) + TailHitPad;
        if (px >= lo && px <= hi)
        {
            _tailHitDone = true;
            _player.TakeDamage(TailDamage, GlobalPosition);
            _arena?.Shake(TailShakeAmp, TailShakeTime);
            AudioManager.Instance?.PlaySfx(SfxTailHit, 0.9f, 0.9f);
        }
    }

    /// <summary>甩尾撞树：连震 3 波，每波撒一把苹果，间隔 1 秒。</summary>
    private async void DropApples()
    {
        float groundY = _arena != null ? _arena.GroundY : GlobalPosition.Y;
        float left = _arena != null ? _arena.ArenaLeft + 80f : GlobalPosition.X - 400f;
        float right = _arena != null ? _arena.ArenaRight - 80f : GlobalPosition.X + 400f;
        float span = Mathf.Max(120f, right - left);
        int n = Mathf.Max(1, ApplePerWave);
        int gen = _fightGen;

        for (int wave = 0; wave < AppleWaves; wave++)
        {
            if (!IsInsideTree() || State == SState.Dead || gen != _fightGen) return;

            // 震树：镜头抖 + 整条蛇抖 + 闷响
            _arena?.Shake(AppleShakeAmp, AppleShakeTime);
            ShakeBody(AppleShakeTime + 0.02f);
            AudioManager.Instance?.PlaySfx(SfxTreeShake, 0.42f, 1.4f);

            // 均匀铺满整条场地，再各自抖开一点，避免几颗叠在一个点上
            float slot = span / n;
            for (int i = 0; i < n; i++)
            {
                float x = left + slot * (i + 0.5f) + (float)GD.RandRange(-slot * 0.28f, slot * 0.28f);
                var a = FallingApple.Spawn(GetParent(), x, groundY);
                a.Damage = AppleDamage;
                a.StartHeight = AppleStartHeight;
                a.Gravity = AppleGravity;
                a.Spin = AppleSpin;
                a.WarnTime = AppleWarnTime;
                a.SfxLand = SfxAppleLand;
                a.SfxLandGap = SfxAppleLandGap;
            }

            await Wait(AppleWaveGap);
        }

        if (gen == _fightGen && State != SState.Dead) BackToIdle();
    }

    // ────────────────────────────────────────── 技能 3：召唤

    private async void DoSummon()
    {
        if (State == SState.Dead) return;
        State = SState.Summon;
        int gen = _fightGen;
        _summonCd = SummonCooldown;

        var spots = SummonSpots(SummonCount);
        foreach (var s in spots) SpawnCircle(s);

        await Wait(SummonCharge);
        if (State == SState.Dead || gen != _fightGen) return;

        foreach (var s in spots) SpawnMinion(s);
        AudioManager.Instance?.PlaySfx(SfxSummon, 0.7f, 0.9f);
        await Wait(0.3f);
        if (gen == _fightGen && State != SState.Dead) BackToIdle();
    }

    private Vector2[] SummonSpots(int count)
    {
        float groundY = _arena != null ? _arena.GroundY : 700f;
        var arr = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            // 左右交替、由近到远铺开
            float side = (i % 2 == 0) ? -1f : 1f;
            float dist = SummonSpreadNear + (i / 2) * SummonSpreadStep;
            arr[i] = new Vector2(GlobalPosition.X + side * dist, groundY);
        }
        return arr;
    }

    /// <summary>放一个法阵，顺便把音效名传进去（Boss 检查器里一处改）。</summary>
    private void SpawnCircle(Vector2 worldPos)
    {
        var c = MagicCircle.Spawn(GetParent(), worldPos, SummonRadius, SummonCharge);
        if (c != null) c.SfxSpawn = SfxCircle;
    }

    private void SpawnMinion(Vector2 worldPos)
    {
        var scene = GD.Load<PackedScene>("res://SnakeMinion.tscn");
        if (scene == null) { GD.PushWarning("[SnakeBoss] 小怪场景加载失败"); return; }
        var m = scene.Instantiate<SnakeMinion>();
        GetParent().AddChild(m);
        m.GlobalPosition = worldPos;
    }

    // ────────────────────────────────────────── 狂暴

    private async void EnterEnrage()
    {
        int gen = _fightGen;
        Enraged = true;
        IdleTime *= EnragedIdleScale;
        GD.Print("[SnakeBoss] 狂暴");

        // 全身泛红一下
        if (_art != null)
        {
            var tw = CreateTween();
            tw.TweenProperty(_art, "modulate", new Color(1.35f, 0.75f, 0.75f, 1f), 0.25f);
            tw.TweenProperty(_art, "modulate", new Color(1.08f, 0.92f, 0.92f, 1f), 0.4f);
        }
        AudioManager.Instance?.PlaySfx(SfxEnrage, 1f, 0.5f);
        _arena?.Shake(EnrageShakeAmp, EnrageShakeTime);

        // 一次性五个方位召五只
        var spots = SummonSpots(EnrageSummonCount);
        foreach (var s in spots) SpawnCircle(s);
        await Wait(SummonCharge);
        if (State == SState.Dead || gen != _fightGen) return;
        foreach (var s in spots) SpawnMinion(s);
        _summonCd = SummonCooldown;
    }

    // ────────────────────────────────────────── 通用

    private void BackToIdle()
    {
        if (State == SState.Dead) return;
        State = SState.Idle;
        _idleTimer = IdleTime;
        _tailAnim = 0f;
        if (_tailArt != null) _tailArt.Visible = false;
        QueueRedraw();          // 擦掉上一帧的预警带
    }

    private async Task Wait(float t)
    {
        if (t <= 0f) return;
        await ToSignal(GetTree().CreateTimer(t), SceneTreeTimer.SignalName.Timeout);
    }
}
