extends SceneTree

# 验证「狂按空格挣脱」：
#   1. 不按 → 血被扣满 20
#   2. 狂按 → 中途脱手，血只掉一点，人被弹起来
#
# GDScript 读不到 C# 的私有属性（Grabbed），所以看三件事：
#   玩家坐标还在不在抓取点上、velocity.y 有没有变成负的（往上弹）、血量还掉不掉。

func _run_throw(press_count: int) -> Dictionary:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	var enemy: CharacterBody2D = node.get_node_or_null("undead_apple1")
	box.global_position = enemy.global_position + Vector2(-200, -30)
	enemy.set("RandomPatrol", false)
	enemy.set("Speed", 0.0)

	# 等骷髅手射出来
	for f in range(600):
		await physics_frame
		if _find_hand(node) != null: break
	# 等手消失（= 抓到并拖回，投技开始）
	for f in range(300):
		if _find_hand(node) == null: break
		await physics_frame

	var hp_start: int = box.get("Hp")
	var pressed := 0
	var hp_at_break := -1

	for f in range(120):
		await physics_frame

		# 投技开始后第 6 帧起开始狂按
		if press_count > 0 and f % 5 == 0:
			print("   f%-3d HP=%d pos=%s vy=%.0f 怪x=%.1f 距离=%.1f" % [f, box.get("Hp"), box.global_position, box.velocity.y, enemy.global_position.x, abs(box.global_position.x - enemy.global_position.x)])
		if f >= 6 and pressed < press_count and f % 3 == 0:
			Input.action_press("jump")
			await physics_frame
			Input.action_release("jump")
			pressed += 1
			if hp_at_break < 0 and box.velocity.y < -50.0:
				hp_at_break = box.get("Hp")

	var result := {
		"按下次数": pressed,
		"起始血": hp_start,
		"结束血": box.get("Hp"),
		"脱手时血": hp_at_break,
		"最后速度y": snappedf(box.velocity.y, 0.1),
		"最后坐标": box.global_position.snapped(Vector2(0.1, 0.1)),
		"抓取点": enemy.global_position + Vector2(70, -22) * 0.55,
	}
	node.queue_free()
	await process_frame
	return result

func _find_hand(node: Node) -> Node:
	for c in node.get_children():
		var s = c.get_script()
		if s != null and str(s.resource_path).ends_with("SkeletonHand.cs"):
			return c
	return null

func _initialize() -> void:
	var a := await _run_throw(0)
	print("【完全不按】", a)
	var b := await _run_throw(8)
	print("【狂按 8 下】", b)
	quit()
