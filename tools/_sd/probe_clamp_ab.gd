extends SceneTree

# 复现 "225 cannot be greater than xxx"：攀爬时把 CameraFixedY 压到 200，
# 让 baseCenter(120) 小于半屏高(225)，上下限撞车。
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

	box.global_position = Vector2(v.global_position.x, v.ClimbBottomY - 12.0)
	box.SnapCamera()
	await create_timer(0.3).timeout
	Input.action_press("look_up")
	for i in range(6):
		await create_timer(0.1).timeout
		print("[AB]   t=%.1f y=%.0f rope=%s" % [i * 0.1, box.global_position.y, str(box.CurrentRope)])

	# 硬把镜头基准压到 200 → baseCenter = 200 - 80 = 120 < 225
	box.CameraFixedY = 200.0
	var halfH: float = root.get_visible_rect().size.y * 0.5
	print("[AB] CameraFixedY=200 offset=%.0f baseCenter=%.0f halfH=%.0f → %s" % [
		cam.offset.y, box.CameraFixedY + cam.offset.y, halfH,
		"上下限反转" if box.CameraFixedY + cam.offset.y < halfH else "正常"])

	await create_timer(1.0).timeout
	Input.action_release("look_up")
	print("[AB] 镜头世界 y=%.1f  （225 说明卡在世界顶边，120 说明未钳制）" % cam.global_position.y)
	quit()
