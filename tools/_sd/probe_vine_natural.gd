extends SceneTree

# 真实操作复现：先按着 W 走过去（镜头抬高 72），再撞上藤蔓抓住 → 镜头回收
func _initialize() -> void:
	var scn: Node2D = load("res://jungle.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var vines := []
	var stack := [scn]
	while stack.size() > 0:
		var cur = stack.pop_back()
		var s = cur.get_script()
		if s != null and str(s.resource_path).ends_with("Vine.cs"):
			vines.append(cur)
		for c in cur.get_children():
			stack.append(c)
	var v: Node2D = vines[0]
	var box: Node2D = scn.get_node("box")
	var cam: Camera2D = box.get_node("Camera2D")
	# 站在藤左边 70px（还不在抓取范围里）
	box.global_position = Vector2(v.global_position.x - 70.0, 340.0)
	box.SnapCamera()
	await create_timer(0.3).timeout
	print("[Nat] 起点 x=%.0f offset=%.0f baseCenter=%.0f" % [box.global_position.x, cam.offset.y, box.CameraFixedY + cam.offset.y])

	Input.action_press("look_up")     # 镜头抬高 72（baseCenter → 178，比半屏高 225 低）
	await create_timer(0.5).timeout
	print("[Nat] 抬完 offset=%.0f baseCenter=%.0f ← 已低于 225" % [cam.offset.y, box.CameraFixedY + cam.offset.y])

	Input.action_press("move_right")  # 走过去撞上藤
	for i in range(14):
		await create_timer(0.1).timeout
		if i % 2 == 0:
			print("[Nat] t=%.1f x=%.0f y=%.0f rope=%s offset=%.0f baseCenter=%.0f" % [
				i * 0.1, box.global_position.x, box.global_position.y,
				"抓到" if box.CurrentRope != null else "无", cam.offset.y,
				box.CameraFixedY + cam.offset.y])
	Input.action_release("move_right")
	Input.action_release("look_up")
	await create_timer(0.3).timeout
	print("[Nat] 结束 y=%.0f 镜头世界 y=%.1f" % [box.global_position.y, cam.global_position.y])
	quit()
