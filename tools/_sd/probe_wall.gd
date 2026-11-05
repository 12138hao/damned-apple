extends SceneTree

# 贴右墙起跳，这次按住不放（避免触发松手切跳跃），分别测单跳和二段跳。
# 玩家脚底 = 中心 y + 16；泥潭碰撞面 365，右草地顶面 337，落差 28。

func _jump_test(box: CharacterBody2D, double: bool) -> void:
	box.global_position = Vector2(2770, 320)
	for i in range(90):
		await physics_frame

	Input.action_press("move_right")
	for i in range(25):
		await physics_frame

	var y0: float = box.global_position.y
	var peak: float = y0

	Input.action_press("jump")
	for i in range(26):
		await physics_frame
		peak = min(peak, box.global_position.y)
		if double and i == 12:
			Input.action_release("jump")
			await physics_frame
			Input.action_press("jump")
	Input.action_release("jump")

	for i in range(90):
		await physics_frame
		peak = min(peak, box.global_position.y)
		if box.global_position.x > 2830 and box.is_on_floor():
			break
	Input.action_release("move_right")
	for i in range(20):
		await physics_frame

	var ok: bool = box.global_position.x > 2830
	print("%s：起跳 y=%.1f 最高 y=%.1f 升 %.1f 像素（脚底到 %.1f，需越过 337）→ 落点 x=%.1f y=%.1f 地面=%s  %s" % [
		"二段跳" if double else "单跳  ", y0, peak, y0 - peak, peak + 16,
		box.global_position.x, box.global_position.y, box.is_on_floor(),
		"上去了" if ok else "没上去"])

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	await _jump_test(box, false)
	await _jump_test(box, true)

	quit()
