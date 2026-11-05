extends SceneTree

# 古井入场动画验收（按内部状态等，不靠帧数估时间 —— headless 帧率不是 60）

func _wait_done(well: Node, max_ms: int = 6000) -> bool:
	var t0 := Time.get_ticks_msec()
	while Time.get_ticks_msec() - t0 < max_ms:
		await process_frame
		if str(well.call("DebugWell")).contains("running=False"):
			return true
	return false

func _initialize() -> void:
	# ① 有井口标记 = 从 underground 过来
	root.get_node("GameState").call("MarkWellEntry")
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	var box: CharacterBody2D = node.get_node("box")
	var well: Node2D = node.get_node("Well")
	print("井坐标=", well.global_position, " 井口=", well.global_position + well.get("MouthOffset"))
	print("起始 pos=", box.global_position.snapped(Vector2(0.1, 0.1)), " 前沿z=", well.get_node("Front").z_index)

	var ok := await _wait_done(well)
	print("动画结束？", ok, "  最终 pos=", box.global_position.snapped(Vector2(0.1, 0.1)),
		"  前沿z=", well.get_node("Front").z_index, "  ", well.call("DebugWell"))

	# ② 操作交还
	var x0: float = box.global_position.x
	Input.action_press("move_right")
	for i in range(20):
		await physics_frame
	Input.action_release("move_right")
	print("演完按右 20 帧：x %.1f → %.1f" % [x0, box.global_position.x])

	# ③ 复活点
	box.call("Kill")
	var t0 := Time.get_ticks_msec()
	while Time.get_ticks_msec() - t0 < 4000:
		await physics_frame
		if box.global_position.distance_to(Vector2(340, 344)) < 3.0:
			break
	print("死后复活位置=", box.global_position.snapped(Vector2(0.1, 0.1)), "（期望 ≈ (340,344)）")
	node.queue_free()
	await process_frame

	# ④ 对照：没有标记 → 井是纯装饰
	print("--- 对照：直接进 jungle ---")
	var n2: Node = load("res://jungle.tscn").instantiate()
	root.add_child(n2)
	await process_frame
	var b2: CharacterBody2D = n2.get_node("box")
	print("起始=", b2.global_position.snapped(Vector2(0.1, 0.1)), "（应 ≈ (172,83)，不在井里）  前沿z=", n2.get_node("Well/Front").z_index)
	for i in range(40):
		await physics_frame
	print("40 帧后=", b2.global_position.snapped(Vector2(0.1, 0.1)))
	quit()
