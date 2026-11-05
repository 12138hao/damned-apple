extends SceneTree

# 临时探针：读取 jungle.tscn 里 MudZone 实际生效的参数，并复测单跳上右岸。
# 目的：确认场景里的 override 是否覆盖了 MudPit.cs 的默认值。

func _jump_test(box: CharacterBody2D) -> void:
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
	Input.action_release("jump")

	for i in range(90):
		await physics_frame
		peak = min(peak, box.global_position.y)
		if box.global_position.x > 2830 and box.is_on_floor():
			break
	Input.action_release("move_right")
	for i in range(20):
		await physics_frame

	print("单跳：起跳 y=%.1f 最高 y=%.1f 升 %.1f 像素（脚底到 %.1f，需越过 337）→ 落点 x=%.1f y=%.1f %s" % [
		y0, peak, y0 - peak, peak + 16,
		box.global_position.x, box.global_position.y,
		"上去了" if box.global_position.x > 2830 else "没上去"])

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var zone: Node = node.get_node_or_null("MudZone")
	if zone:
		print("=== MudZone 实际参数 ===")
		print("SpeedMult  = %.3f" % zone.get("SpeedMult"))
		print("JumpMult   = %.3f" % zone.get("JumpMult"))
		print("AccelMult  = %.3f" % zone.get("AccelMult"))
		print("SurfaceOffsetY = %.1f" % zone.get("SurfaceOffsetY"))
	else:
		print("!! 找不到 MudZone")

	var box: CharacterBody2D = node.get_node_or_null("box")
	await _jump_test(box)

	quit()
