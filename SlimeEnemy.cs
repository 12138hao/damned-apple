using Godot;

/// <summary>
/// 场景里第一个敌人「Enemy」的专属脚本。
///
/// 通用逻辑（重力、巡逻、受击、击退、死亡、血条）都在 Enemy.cs 里。
/// 这个文件是给「只有这个怪才有」的东西准备的：
///   - 特殊的受击反应
///   - 不同的移动方式
///   - 独有的技能 / 掉落
///
/// 改这个文件不会影响 Enemy2 / Enemy3 / apple2。
/// </summary>
public partial class SlimeEnemy : Enemy
{
    // 目前没有特殊行为，先留空。
    // 需要的话重写这些方法：
    //   protected override float ComputeHorizontalVelocity(float dt)   —— 怎么走
    //   protected override void UpdateAnimation(float dt)              —— 播什么帧
    //   protected override void UpdateDead(float dt)                   —— 死亡表现
    //   protected override void OnDeath()                              —— 死亡瞬间的额外逻辑
}
