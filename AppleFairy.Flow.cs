using Godot;
using System.Threading.Tasks;

/// <summary>
/// 苹果仙子的「战后流程」：战败演出、三选一、带主角飞向第四章。
/// 拆出来是因为这几段都是长演出，和战斗循环放在一起会很乱。
/// </summary>
public partial class AppleFairy
{
    /// <summary>
    /// 战败演出：喘气 1 秒 → 飞回原位 → 再喘 1 秒 → 给主角回满血 → 说一句 → 弹三选一。
    /// </summary>
    private async void DefeatSequence()
    {
        _battleOn = false;
        SetState(FairyState.Defeated);

        // 掉到地面上，弯腰喘。
        // 291 = 地面 361 − 脚底 70：她的脚画在原点下方 70px，落到 320 会陷进泥里 29px。
        SetAnim(_texPant, 1, 4, 4, PantFrameTime, PantArtScale, PantArtOffsetY, true);
        await FlyTo(new Vector2(GlobalPosition.X, 291f), 300f);
        await Wait(1.0f);

        // 回初始位置，再喘 1 秒
        await FlyTo(HomePos, 320f);
        await Wait(1.0f);

        // 先给主角回满状态（他说"先帮主角回复状态后和主角对话"）
        if (PlayerAlive()) _player.HealFull();

        _talking = true;
        _holdTalk = true;
        if (GameState.I != null) GameState.I.DialogueActive = true;

        PlayLines(DefeatLines, () =>
        {
            PlayLines(new[] { "npc:……你先歇会儿。想好了再跟我说。" }, () => OpenMenu(AfterWinMenuTitles));
        });
    }

    /// <summary>
    /// 战败后的喘气表现（每物理帧）。
    ///
    /// 4 帧贴图本身起伏很小，单靠它看起来只是"原地抖"。这里再叠一层程序化的呼吸：
    ///   · 整只身体按秒速 <see cref="PantSpeed"/> 上下抬落 <see cref="PantBobAmount"/> 像素 —— 吸气抬起、呼气落下；
    ///   · 同时带一点左右轻微摆动，像撑不住重心。
    /// 两者叠起来才有"上气不接下气"的感觉。把 <see cref="PantExaggerate"/> 关掉就只剩贴图。
    /// </summary>
    private void UpdatePanting(float dt)
    {
        if (!PantExaggerate || _anim == null) return;

        _pantT += dt;
        float w = _pantT * PantSpeed * Mathf.Tau;

        // 0..1 的"吸气进度"：用 1-cos 保证起落两端是停住的，不像正弦那样一上来就冲到一半
        float lift = 0.5f - 0.5f * Mathf.Cos(w);
        _anim.Position = new Vector2(0f, -PantBobAmount * lift);
        _anim.Rotation = Mathf.DegToRad(PantSwayDeg) * Mathf.Sin(w * 0.5f);
    }

    /// <summary>
    /// 三选一的第一项：送主角去第四章（天上伊甸园）。
    /// 按老大要求不再播「抱着飞上去」的过场 —— 说完话直接淡出，落到 eden 开头那 600px 藤地板上。
    /// </summary>
    private async void CarryPlayerUp()
    {
        // 只允许执行一次，避免重复触发把演出叠起来
        if (_carrying || _carriedUp) return;
        if (!PlayerAlive()) return;

        EndTalk();
        SetState(FairyState.Carrying);
        _carriedUp = true;      // 说完就是"已经送过了"，防止再触发

        SetAnim(_texIdle, 8, 1, 8, 0.12f, 0.2083f, 0.5f, true);
        _bubble?.ShowLine("抓稳了，我们上去。");
        await Wait(1.2f);
        if (!PlayerAlive()) return;

        // 不再播「抱着主角飞上去」的过场：直接淡出黑屏，落到第四章开头。
        // 巨茎上升那段（VineAscent + AscentFlight）已按老大要求整个删掉。
        _bubble?.HideBubble();
        _holdTalk = false;

        if (_player != null && IsInstanceValid(_player))
            _player.BeginCutscene();

        var fade = new ScreenFade { Name = "TravelFade" };
        AddChild(fade);
        await fade.FadeTo(1f, 0.7f);
        if (!IsInstanceValid(this)) return;

        var gs = GameState.I;
        if (gs != null && _player != null && IsInstanceValid(_player))
        {
            gs.PrepareTravel(_player.Hp, Vector2.Zero, _player.Weapons, _player.CurrentSlot);
            // 零向量会被 TakeSpawn 当成"落到世界原点"，必须清掉，让新场景用自己的出生点
            gs.PendingSpawn = null;
        }

        GD.Print("[AppleFairy] 淡出直接传送到第四章 -> eden.tscn");
        GetTree().ChangeSceneToFile("res://eden.tscn");
    }
}
