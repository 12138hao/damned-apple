extends SceneTree

# 不死海盗苹果的行为探针：把玩家丢到它附近，观察
#   巡逻 → 发现 → 瞄准 → 蓄力 → 发射骷髅手 → 抓人 → 投技（扣血）→ CD
# 不依赖私有字段，只看「位置 / 血量 / 场景里有没有 SkeletonHand」。

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	var enemy: CharacterBody2D = node.get_node_or_null("undead_apple1")
	if box == null or enemy == null:
		print("!! 找不到 box 或 undead_apple1"); quit(); return

	print("敌人初始位置 ", enemy.global_position, "  hp=", enemy.get("Hp"))

	# 玩家放到敌人左侧 240 像素（在 DetectRange 520 内）
	box.global_position = enemy.global_position + Vector2(-240, -40)

	var lastHp: int = box.get("Hp")
	var lastPos := Vector2.ZERO
	var handSeen := false

	for f in range(900):
		await physics_frame

		var hands := 0
		for c in node.get_children():
			if c.get_class() == "Node2D" and c.get_script() != null:
				if str(c.get_script().resource_path).ends_with("SkeletonHand.cs"):
					hands += 1
		if hands > 0: handSeen = true

		var hp: int = box.get("Hp")
		if f % 20 == 0 or hp != lastHp:
			print("f%-4d 怪(%6.1f,%6.1f) vx=%6.1f  玩家(%6.1f,%6.1f) HP=%d  手=%d" % [
				f, enemy.global_position.x, enemy.global_position.y,
				enemy.velocity.x,
				box.global_position.x, box.global_position.y, hp, hands])
			lastHp = hp

		if hp <= 0:
			print("玩家死亡，停止")
			break

	print("--- 结束：骷髅手出现过=", handSeen, " 玩家最终HP=", box.get("Hp"))
	quit()
