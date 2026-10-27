using Godot;
using System.Collections.Generic;

/// <summary>
/// 敌人管理器：玩家复活时把所有怪物重置回初始状态。
///
/// 挂到场景根节点下的一个空 Node 上即可，不需要配置。
/// 会监听玩家 box 的 Revived 信号，然后遍历场景里所有 Enemy（含子类）调 Respawn()。
/// </summary>
public partial class EnemyManager : Node
{
    [Export] public bool RespawnOnPlayerRevive = true;   // 关掉就不重生

    private Box _player;

    public override void _Ready()
    {
        _player = GetTree().Root.FindChild("box", true, false) as Box;

        if (_player == null)
        {
            GD.PushWarning("[EnemyManager] 没找到玩家 box，怪物不会重生");
            return;
        }

        _player.Revived += OnPlayerRevived;
    }

    public override void _ExitTree()
    {
        if (_player != null && IsInstanceValid(_player))
        {
            _player.Revived -= OnPlayerRevived;
        }
    }

    private void OnPlayerRevived()
    {
        if (!RespawnOnPlayerRevive) return;

        var scene = GetTree().CurrentScene;
        if (scene == null) return;

        var enemies = new List<Enemy>();
        Collect(scene, enemies);

        foreach (var e in enemies)
        {
            if (IsInstanceValid(e)) e.Respawn();
        }

        if (enemies.Count > 0)
            GD.Print($"[EnemyManager] 玩家复活，重置了 {enemies.Count} 只怪物");
    }

    /// <summary>递归收集场景里所有 Enemy（包括 SlimeEnemy / AppleEnemy 这些子类）。</summary>
    private static void Collect(Node node, List<Enemy> into)
    {
        if (node is Enemy e) into.Add(e);

        foreach (var child in node.GetChildren())
        {
            if (child is Node n) Collect(n, into);
        }
    }
}
