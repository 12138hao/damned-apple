using Godot;

/// <summary>
/// 全局 HUD。已注册为自动加载（Autoload），跨场景常驻。
/// 玩家在 _Ready 里主动调用 BindPlayer 注册自己，避免依赖查找时序。
/// </summary>
public partial class Hud : CanvasLayer
{
    private Label _hpLabel;
    private TextureProgressBar _hpBar;
    private Label _deathLabel;
    private Box _player;

    public override void _Ready()
    {
        _hpLabel = GetNode<Label>("HpLabel");
        _hpBar = GetNode<TextureProgressBar>("HpBar");
        _deathLabel = GetNode<Label>("DeathLabel");
        _deathLabel.Visible = false;
    }

    /// <summary>由玩家在 _Ready 里调用：绑定当前场景的玩家。</summary>
    public void BindPlayer(Box player)
    {
        if (player == null || !IsInstanceValid(player)) return;

        // 解绑旧的，避免残留信号
        if (_player != null && IsInstanceValid(_player))
        {
            _player.HpChanged -= OnHpChanged;
            _player.Died -= OnDied;
            _player.Revived -= OnRevived;
        }

        _player = player;
        _player.HpChanged += OnHpChanged;
        _player.Died += OnDied;
        _player.Revived += OnRevived;

        _deathLabel.Visible = false;
        Refresh();
        GD.Print($"[Hud] 已绑定玩家 Hp={player.Hp}/{player.MaxHp}");
    }

    public void Refresh()
    {
        if (_player == null || !IsInstanceValid(_player)) return;
        OnHpChanged(_player.Hp, _player.MaxHp);
    }

    private void OnHpChanged(int hp, int maxHp)
    {
        _hpBar.MaxValue = maxHp;
        _hpBar.Value = hp;
        _hpLabel.Text = hp + " / " + maxHp;
    }

    private void OnDied()
    {
        _deathLabel.Visible = true;
    }

    private void OnRevived()
    {
        _deathLabel.Visible = false;
    }
}
