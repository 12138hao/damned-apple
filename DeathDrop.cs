using Godot;

/// <summary>
/// 死亡掉落组件：挂在任何 Enemy 下面，怪物死时生成掉落物。
///
/// 用法：把本脚本挂到怪物下的一个空 Node 上，填好 DropScene 和 Count。
/// 不想掉落的怪物就不挂，Enemy.cs 一行都不用改。
/// </summary>
public partial class DeathDrop : Node
{
    [Export] public PackedScene DropScene;    // 掉落物场景（拖 .tscn 进来）
    [Export] public int Count = 1;            // 掉几个
    [Export] public float ScatterX = 18f;     // 左右散开的距离（让多个掉落物不叠在一起）

    public override void _Ready()
    {
        if (GetParent() is Enemy enemy)
        {
            enemy.Died += OnDied;
        }
        else
        {
            GD.PushWarning("[DeathDrop] 父节点不是 Enemy，掉落不会生效");
        }
    }

    private void OnDied()
    {
        if (DropScene == null)
        {
            GD.PushWarning("[DeathDrop] 没设置 DropScene，掉不出东西");
            return;
        }

        var owner = GetParent<Node2D>();
        if (owner == null) return;

        // 掉落物要加到场景根节点，不能留在怪物下面——
        // 怪物死亡动画结束后会 QueueFree，挂它下面的话掉落物会被一起销毁。
        Node root = owner.GetTree().CurrentScene ?? owner.GetTree().Root;

        for (int i = 0; i < Count; i++)
        {
            var item = DropScene.Instantiate<Node2D>();
            root.AddChild(item);

            // 以怪物为中心左右散开
            float offsetX = Count <= 1
                ? 0f
                : Mathf.Lerp(-ScatterX, ScatterX, (float)i / (Count - 1));

            item.GlobalPosition = owner.GlobalPosition + new Vector2(offsetX, -8f);
        }
    }
}
