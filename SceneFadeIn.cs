using Godot;

/// <summary>
/// 进场黑幕淡入：刚进入场景时先全黑，再淡到透明。
/// 挂在场景根节点上，它去找子节点里挂了 ScreenFade 的 "Fade"。
/// 配合上一场景的 ScreenFade 淡出，做出「淡出 → 切场景 → 淡入」的传送感。
/// </summary>
public partial class SceneFadeIn : Node
{
    /// <summary>全黑停留时间（秒）。</summary>
    [Export] public float HoldTime = 0.12f;

    /// <summary>淡入耗时（秒）。</summary>
    [Export] public float FadeTime = 0.8f;

    public override async void _Ready()
    {
        var fade = GetNodeOrNull<ScreenFade>("Fade");
        if (fade == null)
        {
            GD.PushWarning("[SceneFadeIn] 找不到子节点 Fade（ScreenFade），跳过淡入");
            return;
        }

        // 子节点的 _Ready 先跑，所以这里 _rect 一定已经建好了
        fade.SetAlpha(1f);

        if (HoldTime > 0f)
            await ToSignal(GetTree().CreateTimer(HoldTime), SceneTreeTimer.SignalName.Timeout);

        await fade.FadeTo(0f, FadeTime);
    }
}
