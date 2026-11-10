using Godot;
using System.Threading.Tasks;

/// <summary>
/// 过场用的全屏黑幕。自己建一个 CanvasLayer（Layer 128，压在 HUD 之上），
/// 外面只用 SetAlpha / FadeTo，不用管节点结构。
/// 场景里放一个挂了本脚本的 Node 就行。
/// </summary>
public partial class ScreenFade : Node
{
    private ColorRect _rect;

    public override void _Ready()
    {
        var layer = new CanvasLayer { Name = "FadeLayer", Layer = 128 };
        AddChild(layer);

        _rect = new ColorRect
        {
            Name = "FadeRect",
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(_rect);

        // 用 SetAnchorsAndOffsetsPreset 一次性把 anchors 和 offsets 都对齐视口：
        // 光设 anchors 的话 CanvasLayer 下的 Control 要等一次布局才有尺寸，
        // 前几帧会变成 0×0 → 「一进场景全黑」失效。
        _rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public void SetAlpha(float a)
    {
        if (_rect == null) return;
        _rect.Color = new Color(0f, 0f, 0f, Mathf.Clamp(a, 0f, 1f));
    }

    public async Task FadeTo(float target, float dur)
    {
        if (_rect == null) return;
        if (dur <= 0f)
        {
            SetAlpha(target);
            return;
        }
        var tw = CreateTween();
        tw.TweenProperty(_rect, "color:a", Mathf.Clamp(target, 0f, 1f), dur);
        await ToSignal(tw, Tween.SignalName.Finished);
    }
}
