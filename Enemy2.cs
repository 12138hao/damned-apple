using Godot;

/// <summary>
/// 场景里第二个敌人「Enemy2」的专属脚本。
///
/// 通用逻辑在 Enemy.cs。改这个文件不影响 Enemy / Enemy3 / apple2。
///
/// 可以重写：
///   protected override float ComputeHorizontalVelocity(float dt)   —— 怎么走
///   protected override void UpdateAnimation(float dt)              —— 播什么帧
///   protected override void UpdateDead(float dt)                   —— 死亡表现
///   protected override void OnDeath()                              —— 死亡瞬间的额外逻辑
/// </summary>
public partial class Enemy2 : Enemy
{
    // 目前没有特殊行为，先留空。
}
