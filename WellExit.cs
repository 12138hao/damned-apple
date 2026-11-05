using Godot;

/// <summary>
/// 古井出口：从 underground 爬上绳索后，从这口井里爬出来进入 jungle。
///
/// 挂在 Node2D 上，节点原点 = **井底和地面接触的那个点**（不是井的中心），
/// 这样摆的时候直接对齐地面线就行。井的贴图由两个 Sprite2D 子节点组成：
///   · Back  z_index 比玩家低 —— 井的完整图
///   · Front z_index 比玩家高 —— 只有井口前沿那一横条（用 region 裁出来）
/// 玩家夹在两层中间，所以「在井里」的时候下半身会被前沿挡住，看起来是真在洞里，
/// 而不是站在井前面。只做一层的话，人是浮在井口上的。
///
/// 演出分四段（关键：脚底必须一直高过井口前沿的顶边，否则就会从井壁里穿出去）：
///   ① 升上来 —— 竖直往上，只到「脚踩在前沿上」为止，不横移
///   ② 跨出来 —— 保持在前沿高度上，横着挪到井口外侧（走个小抛物线，不会下沉）
///   ③ 落下去 —— 已经站在井身外面了，这才往下掉到地面
///   ④ 站稳   —— 停顿一下再交还操作
///
/// 演不演动画由 GameState 里的「井口入场」标记决定（AutoPortal 的 WellEntry 打开时置位）：
///   · 有标记 → 把玩家塞进井里，演一遍，然后把复活点设到这里
///   · 没标记 → 这口井就是个纯装饰，什么都不做
/// </summary>
public partial class WellExit : Node2D
{
    [ExportGroup("井口")]
    // 井口中心相对节点原点的偏移。按贴图算：井口在贴图 (127,173)，
    // 缩放到 0.6 之后 → 大约 (-2, -50)。改了 Sprite 的 offset/scale 就要重算。
    [Export] public Vector2 MouthOffset = new(-2f, -50f);

    [ExportGroup("演出")]
    [Export] public float StartDepth = 24f;      // 一开始人在井口前沿下方多深（要让身子藏在井口前沿后面）
    [Export] public float PerchPad = 2f;         // 站在前沿上时脚底再往上抬多少（微调浮空/陷进去）
    [Export] public Vector2 ExitOffset = new(100f, 0f);  // 最后落在井口右边多远的地面上（要越过右边那根木柱）
    [Export] public float ClimbTime = 1.15f;     // ① 从井里升上来的时间
    [Export] public float StepTime = 0.38f;      // ② 从井口前沿横着跨出去的时间
    [Export] public float StepArc = 10f;         // ② 跨出去时往上抬的抛物线高度（只往上，不会沉进井壁）
    [Export] public float FallTime = 0.30f;      // ③ 从前沿落到地面的时间
    [Export] public float StandTime = 0.35f;     // ④ 落地后停顿多久才交还操作
    [Export] public float SwayAmp = 3f;          // ① 上升时左右晃的幅度（像素）

    [ExportGroup("层次")]
    // 井的前沿那一层：只在演动画的时候盖在玩家前面（做出「人在井里」的效果）。
    // 演完必须放回 Back 后面 —— 否则玩家从井边走过去会被前沿整条挡住，人就消失了。
    [Export] public NodePath FrontPath = "Front";
    [Export] public int FrontZIndex = 12;

    [ExportGroup("存档")]
    // 演完把复活点设到落地位置。自动存档（SpawnSavePoint）照旧走它自己的逻辑。
    [Export] public bool SetReviveOnLand = true;

    private Box _player;
    private Sprite2D _front;
    private float _t;
    private bool _running;
    private bool _hopped;

    // 这三个都由前沿层的实际像素位置现算，改了井的 offset / scale 不用重新手调
    private float _rimTop;          // 井口前沿那条横条顶边的世界 y（= 井壁最高处）
    private Vector2 _perchPos;      // 站在前沿上的位置
    private Vector2 _startPos;      // 井里的起始位置
    private Vector2 _landPos;       // 最后落地位置

    public override void _Ready()
    {
        // 前沿层默认压在井后面（它画的内容和 Back 完全重合，看不出来），
        // 只有演动画时才提到玩家前面。见 Finish()。
        _front = GetNodeOrNull<Sprite2D>(FrontPath);
        if (_front != null) _front.ZIndex = -2;

        // 只有「这次是从井里上来的」才演。取一次就清掉，所以死亡复活不会重演。
        var gs = GameState.I;
        if (gs == null || !gs.TakeWellEntry())
        {
            SetProcess(false);      // 纯装饰：井照样画，但不接管玩家
            return;
        }

        _player = FindPlayer();
        if (_player == null)
        {
            GD.PushWarning("[WellExit] 找不到玩家，入场动画跳过");
            SetProcess(false);
            return;
        }

        MeasureRim();

        float half = _player.HalfHeight;
        // 站在前沿上：脚底压在前沿顶边（PerchPad 用来微调浮空/陷进去）
        _perchPos = new Vector2(Mouth.X, _rimTop - half - PerchPad);
        // 起始点：井口前沿往下 StartDepth，整条身子藏在前沿后面
        _startPos = new Vector2(Mouth.X, _rimTop + StartDepth);
        // 落地位置：井口右侧的地面上，玩家中心要抬半个身高
        _landPos = GlobalPosition + new Vector2(ExitOffset.X, -half + ExitOffset.Y);

        if (_front != null) _front.ZIndex = FrontZIndex;   // 演出期间：前沿挡住井里的玩家

        _player.BeginCutscene();
        _player.SetCutscenePosition(_startPos);
        _player.SnapCamera();       // 瞬移进井里之后镜头要立刻跟上，否则从上一个位置飘过来

        _running = true;
        _t = 0f;
        GD.Print($"[WellExit] 开始井口入场动画：前沿顶 y={_rimTop:F1} 起点 {_startPos} 踩前沿 {_perchPos} 落点 {_landPos}");
    }

    private Box FindPlayer()
    {
        // 先按约定找当前场景里的 box，找不到再全树搜一遍
        var scene = GetTree().CurrentScene;
        var p = scene?.GetNodeOrNull<Box>("box");
        if (p != null) return p;
        return GetTree().Root.FindChild("box", true, false) as Box;
    }

    /// <summary>
    /// 量井口前沿的顶边。Front 是 region 裁出来的一条横带，
    /// 它的顶边就是「井壁最高处」——玩家的脚必须高过这条线才能横着挪出去，
    /// 否则就是从井壁中间穿过去。
    /// </summary>
    private void MeasureRim()
    {
        // 兜底：万一没挂前沿层，就按井口往下 12 像素估一个
        _rimTop = Mouth.Y + 12f;
        if (_front == null) return;

        // Sprite2D.GetRect() 是本地坐标（含 offset、不含 scale），
        // 乘上 global_transform 才是屏幕坐标。
        Rect2 r = _front.GetRect();
        Vector2 a = _front.GlobalTransform * r.Position;
        Vector2 b = _front.GlobalTransform * (r.Position + r.Size);
        _rimTop = Mathf.Min(a.Y, b.Y);
    }

    private Vector2 Mouth => GlobalPosition + MouthOffset;

    public override void _Process(double delta)
    {
        if (!_running || _player == null || !IsInstanceValid(_player)) return;

        _t += (float)delta;

        float climbEnd = ClimbTime;
        float stepEnd = climbEnd + StepTime;
        float fallEnd = stepEnd + FallTime;
        float standEnd = fallEnd + StandTime;

        if (_t <= climbEnd)
        {
            // ① 升上来：先慢后快再收住（smoothstep），带一点左右晃，像在扒着井壁爬。
            //    只走竖直方向，脚一到前沿顶边就停，绝不在井壁高度上横移。
            float k = Mathf.Clamp(_t / ClimbTime, 0f, 1f);
            float e = k * k * (3f - 2f * k);
            Vector2 p = _startPos.Lerp(_perchPos, e);
            p.X += Mathf.Sin(_t * 9f) * SwayAmp * (1f - k);   // 越接近井口晃得越小
            _player.SetCutscenePosition(p);
        }
        else if (_t <= stepEnd)
        {
            // ② 跨出井口：Y 只往上抬（- sin），永远不低于前沿顶边，所以不会切进井壁；
            //    X 一次性挪到井身外面。这一步结束时人已经在井口外侧了。
            float k = Mathf.Clamp((_t - climbEnd) / StepTime, 0f, 1f);
            float e = k * k * (3f - 2f * k);
            float x = Mathf.Lerp(Mouth.X, _landPos.X, e);
            float y = _perchPos.Y - Mathf.Sin(k * Mathf.Pi) * StepArc;
            _player.SetCutscenePosition(new Vector2(x, y));

            if (!_hopped)
            {
                _hopped = true;
                AudioManager.PlayJump();
            }
        }
        else if (_t <= fallEnd)
        {
            // ③ 落下去：X 已经定在井口外侧，这里只掉高度。k*k 是加速下落（像重力）。
            float k = Mathf.Clamp((_t - stepEnd) / FallTime, 0f, 1f);
            float y = Mathf.Lerp(_perchPos.Y, _landPos.Y, k * k);
            _player.SetCutscenePosition(new Vector2(_landPos.X, y));
        }
        else if (_t <= standEnd)
        {
            // ④ 站稳
            _player.SetCutscenePosition(_landPos);
        }
        else
        {
            Finish();
        }
    }

    /// <summary>给探针看的内部状态（GDScript 读不到 C# 私有字段）。</summary>
    public string DebugWell()
        => $"_t={_t:F2} running={_running} hopped={_hopped} rimTop={_rimTop:F1} perch={_perchPos} land={_landPos}";

    private void Finish()
    {
        _running = false;
        _player.SetCutscenePosition(_landPos);
        _player.EndCutscene();

        // 交还操作之后前沿层就没用了，压回后面去，玩家从井边走过不会被挡住
        if (_front != null) _front.ZIndex = -2;

        if (SetReviveOnLand)
        {
            // 传「地面点」，SetRevivePoint 自己会抬半个身高。
            // 这样第一次从井里上来之后，本场景死亡都在井口复活。
            _player.SetRevivePoint(GlobalPosition + new Vector2(ExitOffset.X, ExitOffset.Y));
        }

        GD.Print("[WellExit] 入场动画结束，操作已交还");
        SetProcess(false);
    }
}
